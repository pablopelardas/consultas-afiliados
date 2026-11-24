using ConsultasPlan.Controllers;
using ConsultasPlan.Contratos;
using ConsultasPlan.Data;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasPlan.Tests.Dominio;

/// <summary>
/// Los endpoints respetan el ciclo de vida.
/// </summary>
/// <remarks>
/// La tabla de transiciones se verifica aparte, sin infraestructura. Acá se
/// comprueba que cada endpoint la consulta antes de asignar el estado — que es
/// justamente lo que el código base no hacía.
///
/// Los tres primeros reemplazan tests de caracterización que documentaban el
/// defecto y que esta rama vuelve obsoletos.
/// </remarks>
public class TransicionesEnLosEndpointsTests
{
    private static ConsultasController Controlador(AppDbContext db) => new(db, Reloj.Fijo());

    private static void AssertConflicto(IActionResult resultado) =>
        Assert.Equal(StatusCodes.Status409Conflict,
            Assert.IsType<ObjectResult>(resultado).StatusCode);

    [Fact]
    public async Task No_se_puede_marcar_inasistencia_sobre_una_consulta_cancelada()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(),
            Reloj.Ahora.AddDays(-10), EstadoConsulta.Cancelado);

        var resultado = await Controlador(db).MarcarInasistencia(consulta.Id);

        AssertConflicto(resultado);
        Assert.Equal(EstadoConsulta.Cancelado, db.Consultas.Find(consulta.Id)!.Estado);
    }

    [Fact]
    public async Task No_se_puede_registrar_atencion_sobre_una_consulta_cancelada()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(),
            Reloj.Ahora.AddDays(-10), EstadoConsulta.Cancelado);

        var resultado = await Controlador(db).RegistrarAtencion(consulta.Id);

        AssertConflicto(resultado);
        Assert.Equal(EstadoConsulta.Cancelado, db.Consultas.Find(consulta.Id)!.Estado);
    }

    [Fact]
    public async Task No_se_puede_volver_de_Atendido_a_Pendiente()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(),
            Reloj.Ahora.AddDays(-10), EstadoConsulta.Atendido);

        var resultado = await Controlador(db).ActualizarEstado(
            consulta.Id, new ActualizarEstadoRequest { Estado = EstadoConsulta.Pendiente });

        AssertConflicto(resultado);
        Assert.Equal(EstadoConsulta.Atendido, db.Consultas.Find(consulta.Id)!.Estado);
    }

    [Fact]
    public async Task No_se_puede_cancelar_dos_veces()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(),
            Reloj.Ahora.AddDays(10), EstadoConsulta.Cancelado);

        var resultado = await Controlador(db).CancelarConsulta(consulta.Id);

        AssertConflicto(resultado);
    }

    // El flujo operativo normal del staff sigue habilitado: el endpoint genérico
    // existe para eso y las transiciones válidas no se tocaron.
    [Fact]
    public async Task El_flujo_operativo_Pendiente_Confirmado_Atendido_sigue_habilitado()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(),
            Reloj.Ahora.AddDays(-1), EstadoConsulta.Pendiente);

        var aConfirmado = await Controlador(db).ActualizarEstado(
            consulta.Id, new ActualizarEstadoRequest { Estado = EstadoConsulta.Confirmado });
        Assert.IsType<OkObjectResult>(aConfirmado);

        var aAtendido = await Controlador(db).ActualizarEstado(
            consulta.Id, new ActualizarEstadoRequest { Estado = EstadoConsulta.Atendido });
        Assert.IsType<OkObjectResult>(aAtendido);

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

        var resultado = await Controlador(db).ActualizarEstado(
            consulta.Id, new ActualizarEstadoRequest { Estado = EstadoConsulta.Atendido });

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(EstadoConsulta.Atendido, db.Consultas.Find(consulta.Id)!.Estado);
    }
}
