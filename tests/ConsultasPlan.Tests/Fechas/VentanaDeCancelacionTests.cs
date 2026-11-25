using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;

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

        var resultado = await ServiciosDePrueba.Consultas(db).Cancelar(consulta.Id);

        var estadoFinal = db.Consultas.Find(consulta.Id)!.Estado;

        if (deberiaAceptar)
        {
            AssertResultado.Exito(resultado);
            Assert.Equal(EstadoConsulta.Cancelado, estadoFinal);
        }
        else
        {
            AssertResultado.Conflicto(resultado, caso);
            Assert.Equal(EstadoConsulta.Pendiente, estadoFinal);
        }
    }

    // El borde se cruza por un minuto, no por una hora: a 23:59 de la consulta
    // ya no se puede cancelar.
    [Fact]
    public async Task Cancelar_un_minuto_antes_del_limite_es_rechazado()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(),
            Reloj.Ahora.AddHours(24).AddMinutes(-1));

        var resultado = await ServiciosDePrueba.Consultas(db).Cancelar(consulta.Id);

        AssertResultado.Conflicto(resultado);
        Assert.Equal(EstadoConsulta.Pendiente, db.Consultas.Find(consulta.Id)!.Estado);
    }

    // Regresión del desvío por huso horario: el código base escribía con
    // DateTime.UtcNow y comparaba contra DateTime.Now. Con el reloj inyectado
    // en UTC, el resultado no depende del huso del servidor que ejecuta.
    [Fact]
    public async Task La_ventana_no_depende_del_huso_horario_del_servidor()
    {
        using var db = ContextoDePrueba.Crear();

        // 25 horas de anticipación: aceptable bajo cualquier lectura correcta.
        // Con el desvío de 3 horas de Argentina, el cálculo erróneo lo habría
        // visto como 22 y lo habría rechazado.
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(), Reloj.Ahora.AddHours(25));

        AssertResultado.Exito(await ServiciosDePrueba.Consultas(db).Cancelar(consulta.Id));
    }

    // El endpoint genérico de cambio de estado era, en el código base, la vía
    // por la que se evadían todas las reglas. Un solo camino de transición
    // dentro del servicio cierra el atajo sin que haya que recordarlo.
    [Fact]
    public async Task El_cambio_de_estado_generico_no_saltea_la_ventana()
    {
        using var db = ContextoDePrueba.Crear();
        var consulta = db.AgregarConsulta(
            db.AgregarAfiliado(), db.AgregarProfesional(), Reloj.Ahora.AddHours(12));

        var resultado = await ServiciosDePrueba.Consultas(db)
            .CambiarEstado(consulta.Id, EstadoConsulta.Cancelado);

        AssertResultado.Conflicto(resultado);
        Assert.Equal(EstadoConsulta.Pendiente, db.Consultas.Find(consulta.Id)!.Estado);
    }
}
