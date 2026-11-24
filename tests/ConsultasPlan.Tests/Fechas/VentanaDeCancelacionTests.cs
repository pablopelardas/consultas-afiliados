using ConsultasPlan.Controllers;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasPlan.Tests.Fechas;

/// <summary>
/// Ventana mínima de anticipación para cancelar una consulta.
/// </summary>
/// <remarks>
/// El código base validaba 23 horas mientras el mensaje al usuario prometía 24.
/// Estos tests fijan el borde exacto, que es lo que el reloj inyectado vuelve
/// verificable: sin él habría que esperar al instante real para probarlo.
/// </remarks>
public class VentanaDeCancelacionTests
{
    [Theory]
    [InlineData(48, true,  "con dos días de anticipación")]
    [InlineData(25, true,  "una hora por encima del límite")]
    [InlineData(24, true,  "exactamente en el límite")]
    [InlineData(23, false, "la franja donde el código base aceptaba y el mensaje decía 24 horas")]
    [InlineData(12, false, "bien dentro de las 24 horas")]
    public async Task Cancelar_segun_las_horas_de_anticipacion(
        int horasDeAnticipacion, bool deberiaAceptar, string caso)
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        var consulta = db.AgregarConsulta(
            afiliado, profesional, Reloj.Ahora.AddHours(horasDeAnticipacion));

        var controlador = new ConsultasController(db, Reloj.Fijo());

        var resultado = await controlador.CancelarConsulta(consulta.Id);

        var estadoFinal = db.Consultas.Find(consulta.Id)!.Estado;

        if (deberiaAceptar)
        {
            Assert.True(resultado is OkObjectResult, $"Debía aceptar {caso}.");
            Assert.Equal(EstadoConsulta.Cancelado, estadoFinal);
        }
        else
        {
            // 409: la petición es válida, el estado del recurso no la permite.
            Assert.Equal(StatusCodes.Status409Conflict,
                Assert.IsType<ObjectResult>(resultado).StatusCode);
            Assert.Equal(EstadoConsulta.Pendiente, estadoFinal);
        }
    }

    // El borde se cruza por un minuto, no por una hora: a 23:59 de la consulta
    // ya no se puede cancelar.
    [Fact]
    public async Task Cancelar_un_minuto_antes_del_limite_es_rechazado()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        var consulta = db.AgregarConsulta(
            afiliado, profesional, Reloj.Ahora.AddHours(24).AddMinutes(-1));

        var controlador = new ConsultasController(db, Reloj.Fijo());

        var resultado = await controlador.CancelarConsulta(consulta.Id);

        Assert.Equal(StatusCodes.Status409Conflict,
            Assert.IsType<ObjectResult>(resultado).StatusCode);
        Assert.Equal(EstadoConsulta.Pendiente, db.Consultas.Find(consulta.Id)!.Estado);
    }

    // Regresión del desvío por huso horario: el código base escribía con
    // DateTime.UtcNow y comparaba contra DateTime.Now. Con el reloj inyectado
    // en UTC, el resultado no depende del huso del servidor que ejecuta.
    [Fact]
    public async Task La_ventana_no_depende_del_huso_horario_del_servidor()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();

        // 25 horas de anticipación: aceptable bajo cualquier lectura correcta.
        // Con el desvío de 3 horas de Argentina, el cálculo erróneo lo habría
        // visto como 22 y lo habría rechazado.
        var consulta = db.AgregarConsulta(
            afiliado, profesional, Reloj.Ahora.AddHours(25));

        var controlador = new ConsultasController(db, Reloj.Fijo());

        var resultado = await controlador.CancelarConsulta(consulta.Id);

        Assert.IsType<OkObjectResult>(resultado);
    }
}
