using ConsultasPlan.Controllers;
using ConsultasPlan.Contratos;
using ConsultasPlan.Data;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    private static AfiliadosController Afiliados(AppDbContext db) => new(db, Reloj.Fijo());
    private static ConsultasController Consultas(AppDbContext db) => new(db, Reloj.Fijo());

    private static void AssertConflicto(IActionResult resultado) =>
        Assert.Equal(StatusCodes.Status409Conflict,
            Assert.IsType<ObjectResult>(resultado).StatusCode);

    private static void AssertNoEncontrado(IActionResult resultado) =>
        Assert.Equal(StatusCodes.Status404NotFound,
            Assert.IsType<ObjectResult>(resultado).StatusCode);

    // El punto central: dar de baja no destruye el historial clínico.
    [Fact]
    public async Task Dar_de_baja_conserva_las_consultas_con_su_afiliado()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado("Mariana López");
        var profesional = db.AgregarProfesional();
        db.AgregarConsulta(afiliado, profesional, Reloj.Ahora.AddDays(-30), EstadoConsulta.Atendido);
        db.AgregarConsulta(afiliado, profesional, Reloj.Ahora.AddDays(-60), EstadoConsulta.Atendido);

        await Afiliados(db).Delete(afiliado.Id);

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

        await Afiliados(db).Delete(afiliado.Id);

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
        var baja = db.AgregarAfiliado("Dado de baja");

        await Afiliados(db).Delete(baja.Id);

        var resultado = await Afiliados(db).GetAll();
        var listado = Assert.IsAssignableFrom<IEnumerable<AfiliadoResponse>>(
            Assert.IsType<OkObjectResult>(resultado).Value).ToList();

        Assert.Single(listado);
        Assert.Equal(vigente.Id, listado[0].Id);
    }

    [Fact]
    public async Task Un_afiliado_dado_de_baja_no_se_puede_consultar_por_id()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();

        await Afiliados(db).Delete(afiliado.Id);

        AssertNoEncontrado(await Afiliados(db).GetById(afiliado.Id));
    }

    [Fact]
    public async Task Un_afiliado_dado_de_baja_no_se_puede_modificar()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();

        await Afiliados(db).Delete(afiliado.Id);

        var resultado = await Afiliados(db).Update(
            afiliado.Id, new ActualizarAfiliadoRequest
            {
                NombreCompleto = "Nombre nuevo",
                DNI = "31111111",
                Email = "nuevo@test.local",
                Telefono = "1145678901"
            });

        AssertNoEncontrado(resultado);
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

        await Afiliados(db).Delete(afiliado.Id);

        var resultado = await Consultas(db).CrearConsulta(new CrearConsultaRequest
        {
            AfiliadoId = afiliado.Id,
            ProfesionalId = profesional.Id,
            FechaHora = Reloj.Ahora.AddDays(10),
            Modalidad = ModalidadConsulta.Presencial,
            Motivo = "No debería crearse"
        });

        AssertNoEncontrado(resultado);
        Assert.Empty(db.Consultas.Where(c => c.AfiliadoId == afiliado.Id));
    }

    [Fact]
    public async Task No_se_puede_dar_de_baja_dos_veces()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();

        await Afiliados(db).Delete(afiliado.Id);

        AssertConflicto(await Afiliados(db).Delete(afiliado.Id));
    }

    // Una baja que no se puede deshacer es un borrado con otro nombre.
    [Fact]
    public async Task Un_afiliado_dado_de_baja_se_puede_reactivar()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        await Afiliados(db).Delete(afiliado.Id);

        var resultado = await Afiliados(db).Reactivar(afiliado.Id);

        Assert.IsType<OkObjectResult>(resultado);

        var persistido = db.Afiliados.Find(afiliado.Id)!;
        Assert.True(persistido.Activo);
        Assert.Null(persistido.FechaBaja);

        // Y vuelve a estar disponible para las vistas operativas.
        Assert.IsType<OkObjectResult>(await Afiliados(db).GetById(afiliado.Id));
    }

    [Fact]
    public async Task No_se_puede_reactivar_un_afiliado_que_ya_esta_activo()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();

        AssertConflicto(await Afiliados(db).Reactivar(afiliado.Id));
    }

    [Fact]
    public async Task Un_afiliado_creado_nace_activo_y_sin_fecha_de_baja()
    {
        using var db = ContextoDePrueba.Crear();

        var resultado = await Afiliados(db).Create(new CrearAfiliadoRequest
        {
            NombreCompleto = "Nuevo Afiliado",
            DNI = "40000000",
            Email = "nuevo@test.local",
            Telefono = "1145678901"
        });

        var creado = Assert.IsType<AfiliadoResponse>(
            Assert.IsType<CreatedAtActionResult>(resultado).Value);
        Assert.Equal(Reloj.Ahora, creado.FechaAlta);

        // Activo y FechaBaja no se exponen en el contrato de salida: son
        // estado interno. Se verifican contra la entidad persistida.
        var persistido = db.Afiliados.Find(creado.Id)!;
        Assert.True(persistido.Activo);
        Assert.Null(persistido.FechaBaja);
    }
}
