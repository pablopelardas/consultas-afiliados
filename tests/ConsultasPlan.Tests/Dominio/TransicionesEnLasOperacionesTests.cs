using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;

namespace ConsultasPlan.Tests.Dominio;

/// <summary>
/// Las operaciones del servicio respetan el ciclo de vida.
/// </summary>
/// <remarks>
/// La tabla de transiciones se verifica aparte, sin infraestructura. Acá se
/// comprueba que toda operación que cambia de estado la consulta antes de
/// asignar — que es justamente lo que el código base no hacía.
///
/// Los tres primeros reemplazan tests de caracterización que documentaban el
/// defecto.
/// </remarks>
public class TransicionesEnLasOperacionesTests
{
    [Fact]
    public async Task No_se_puede_marcar_inasistencia_sobre_una_consulta_cancelada()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(),
            Reloj.Ahora.AddDays(-10), EstadoConsulta.Cancelado);

        var resultado = await ServiciosDePrueba.Consultas(db).RegistrarInasistencia(consulta.Id);

        AssertResultado.Conflicto(resultado);
        Assert.Equal(EstadoConsulta.Cancelado, db.Consultas.Find(consulta.Id)!.Estado);
    }

    [Fact]
    public async Task No_se_puede_registrar_atencion_sobre_una_consulta_cancelada()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(),
            Reloj.Ahora.AddDays(-10), EstadoConsulta.Cancelado);

        var resultado = await ServiciosDePrueba.Consultas(db).RegistrarAtencion(consulta.Id);

        AssertResultado.Conflicto(resultado);
        Assert.Equal(EstadoConsulta.Cancelado, db.Consultas.Find(consulta.Id)!.Estado);
    }

    [Fact]
    public async Task No_se_puede_volver_de_Atendido_a_Pendiente()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(),
            Reloj.Ahora.AddDays(-10), EstadoConsulta.Atendido);

        var resultado = await ServiciosDePrueba.Consultas(db)
            .CambiarEstado(consulta.Id, EstadoConsulta.Pendiente);

        AssertResultado.Conflicto(resultado);
        Assert.Equal(EstadoConsulta.Atendido, db.Consultas.Find(consulta.Id)!.Estado);
    }

    [Fact]
    public async Task No_se_puede_cancelar_dos_veces()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(),
            Reloj.Ahora.AddDays(10), EstadoConsulta.Cancelado);

        AssertResultado.Conflicto(await ServiciosDePrueba.Consultas(db).Cancelar(consulta.Id));
    }

    // El flujo operativo normal del staff sigue habilitado: la operación
    // genérica existe para eso y las transiciones válidas no se tocaron.
    [Fact]
    public async Task El_flujo_operativo_Pendiente_Confirmado_Atendido_sigue_habilitado()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(),
            Reloj.Ahora.AddDays(-1), EstadoConsulta.Pendiente);
        var consultas = ServiciosDePrueba.Consultas(db);

        AssertResultado.Exito(
            await consultas.CambiarEstado(consulta.Id, EstadoConsulta.Confirmado));
        AssertResultado.Exito(
            await consultas.CambiarEstado(consulta.Id, EstadoConsulta.Atendido));

        Assert.Equal(EstadoConsulta.Atendido, db.Consultas.Find(consulta.Id)!.Estado);
    }

    // Corrección de carga: una inasistencia mal registrada perjudica al
    // afiliado, así que debe poder revertirse hacia Atendido.
    [Fact]
    public async Task Una_inasistencia_mal_cargada_se_puede_corregir_a_Atendido()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(),
            Reloj.Ahora.AddDays(-5), EstadoConsulta.Inasistencia);

        var resultado = await ServiciosDePrueba.Consultas(db)
            .CambiarEstado(consulta.Id, EstadoConsulta.Atendido);

        AssertResultado.Exito(resultado);
        Assert.Equal(EstadoConsulta.Atendido, db.Consultas.Find(consulta.Id)!.Estado);
    }

    // La transición es legal y la regla temporal no: una consulta que todavía no
    // ocurrió no puede tener inasistencia, y el camino genérico tampoco puede
    // forzarla.
    [Fact]
    public async Task El_cambio_de_estado_generico_no_saltea_la_regla_de_inasistencia()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(), Reloj.Ahora.AddDays(5));

        var resultado = await ServiciosDePrueba.Consultas(db)
            .CambiarEstado(consulta.Id, EstadoConsulta.Inasistencia);

        AssertResultado.Conflicto(resultado);
        Assert.Equal(EstadoConsulta.Pendiente, db.Consultas.Find(consulta.Id)!.Estado);
    }
}
