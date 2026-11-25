using ConsultasPlan.Contratos;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace ConsultasPlan.Tests.Historial;

/// <summary>
/// Baja lógica de afiliados e integridad del historial.
/// </summary>
/// <remarks>
/// El código base ejecutaba <c>Remove</c> sobre el afiliado. Con la clave
/// foránea en <c>SetNull</c>, eso dejaba sus consultas sin paciente: se perdía
/// de forma irreversible a quién correspondía cada atención médica.
/// </remarks>
public class BajaLogicaAfiliadoTests
{
    // El punto central: dar de baja no destruye el historial clínico.
    [Fact]
    public async Task Dar_de_baja_conserva_las_consultas_con_su_afiliado()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado("Mariana López");
        var profesional = db.AgregarProfesional();
        db.AgregarConsulta(afiliado, profesional, Reloj.Ahora.AddDays(-30), EstadoConsulta.Atendido);
        db.AgregarConsulta(afiliado, profesional, Reloj.Ahora.AddDays(-60), EstadoConsulta.Atendido);

        await ServiciosDePrueba.Afiliados(db).DarDeBaja(afiliado.Id);

        var consultas = await db.Consultas
            .Include(c => c.Afiliado)
            .Where(c => c.AfiliadoId == afiliado.Id)
            .ToListAsync();

        Assert.Equal(2, consultas.Count);
        Assert.All(consultas, c =>
        {
            Assert.NotNull(c.Afiliado);
            Assert.Equal("Mariana López", c.Afiliado!.NombreCompleto);
        });
    }

    [Fact]
    public async Task Dar_de_baja_no_borra_el_registro_del_afiliado()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();

        AssertResultado.Exito(await ServiciosDePrueba.Afiliados(db).DarDeBaja(afiliado.Id));

        var persistido = await db.Afiliados.FindAsync(afiliado.Id);
        Assert.NotNull(persistido);
        Assert.False(persistido!.Activo);
        Assert.Equal(Reloj.Ahora, persistido.FechaBaja);
    }

    [Fact]
    public async Task Un_afiliado_dado_de_baja_no_aparece_en_el_listado()
    {
        using var db = ContextoDePrueba.Crear();
        var vigente = db.AgregarAfiliado("Vigente");
        var baja = db.AgregarAfiliado("Dado de baja", dni: "30000001");
        var afiliados = ServiciosDePrueba.Afiliados(db);

        await afiliados.DarDeBaja(baja.Id);

        var listado = await afiliados.ListarVigentes();

        Assert.Single(listado);
        Assert.Equal(vigente.Id, listado[0].Id);
    }

    [Fact]
    public async Task Un_afiliado_dado_de_baja_no_se_puede_consultar_por_id()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var afiliados = ServiciosDePrueba.Afiliados(db);

        await afiliados.DarDeBaja(afiliado.Id);

        AssertResultado.NoEncontrado(await afiliados.Obtener(afiliado.Id));
    }

    [Fact]
    public async Task Un_afiliado_dado_de_baja_no_se_puede_modificar()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var afiliados = ServiciosDePrueba.Afiliados(db);

        await afiliados.DarDeBaja(afiliado.Id);

        var resultado = await afiliados.Actualizar(
            afiliado.Id, new ActualizarAfiliadoRequest
            {
                NombreCompleto = "Nombre nuevo",
                DNI = "31111111",
                Email = "nuevo@test.local",
                Telefono = "1145678901"
            });

        AssertResultado.NoEncontrado(resultado);
        Assert.NotEqual("Nombre nuevo", db.Afiliados.Find(afiliado.Id)!.NombreCompleto);
    }

    // El id sigue existiendo aunque el afiliado no figure en ninguna vista:
    // agendar por id debe negarse igual.
    [Fact]
    public async Task Un_afiliado_dado_de_baja_no_puede_agendar_consultas()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();

        await ServiciosDePrueba.Afiliados(db).DarDeBaja(afiliado.Id);

        var resultado = await ServiciosDePrueba.Consultas(db).Crear(new CrearConsultaRequest
        {
            AfiliadoId = afiliado.Id,
            ProfesionalId = profesional.Id,
            FechaHora = Reloj.Ahora.AddDays(10),
            Modalidad = ModalidadConsulta.Presencial,
            Motivo = "No debería crearse"
        });

        AssertResultado.NoEncontrado(resultado);
        Assert.Empty(db.Consultas.Where(c => c.AfiliadoId == afiliado.Id));
    }

    [Fact]
    public async Task No_se_puede_dar_de_baja_dos_veces()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var afiliados = ServiciosDePrueba.Afiliados(db);

        await afiliados.DarDeBaja(afiliado.Id);

        AssertResultado.Conflicto(await afiliados.DarDeBaja(afiliado.Id));
    }

    // Una baja que no se puede deshacer es un borrado con otro nombre.
    [Fact]
    public async Task Un_afiliado_dado_de_baja_se_puede_reactivar()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var afiliados = ServiciosDePrueba.Afiliados(db);
        await afiliados.DarDeBaja(afiliado.Id);

        AssertResultado.Exito(await afiliados.Reactivar(afiliado.Id));

        var persistido = db.Afiliados.Find(afiliado.Id)!;
        Assert.True(persistido.Activo);
        Assert.Null(persistido.FechaBaja);

        // Y vuelve a estar disponible para las vistas operativas.
        AssertResultado.Exito(await afiliados.Obtener(afiliado.Id));
    }

    [Fact]
    public async Task No_se_puede_reactivar_un_afiliado_que_ya_esta_activo()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();

        AssertResultado.Conflicto(await ServiciosDePrueba.Afiliados(db).Reactivar(afiliado.Id));
    }

    [Fact]
    public async Task Un_afiliado_creado_nace_activo_y_sin_fecha_de_baja()
    {
        using var db = ContextoDePrueba.Crear();

        var resultado = await ServiciosDePrueba.Afiliados(db).Crear(new CrearAfiliadoRequest
        {
            NombreCompleto = "Nuevo Afiliado",
            DNI = "40000000",
            Email = "nuevo@test.local",
            Telefono = "1145678901"
        });

        AssertResultado.Exito(resultado);
        Assert.Equal(Reloj.Ahora, resultado.Valor.FechaAlta);
        Assert.True(resultado.Valor.Activo);
        Assert.Null(resultado.Valor.FechaBaja);
    }
}
