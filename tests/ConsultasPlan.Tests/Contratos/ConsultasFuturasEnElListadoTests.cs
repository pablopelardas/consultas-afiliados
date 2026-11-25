using System.Net.Http.Json;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;

namespace ConsultasPlan.Tests.Contratos;

/// <summary>
/// El listado de afiliados informa cuántos turnos futuros tiene cada uno.
/// </summary>
/// <remarks>
/// La alternativa era que el cliente pidiera las consultas de cada fila y las
/// filtrara por fecha y estado, es decir que reimplementara qué significa
/// "turno que todavía ocupa agenda". El backend ya lo sabe.
/// </remarks>
public class ConsultasFuturasEnElListadoTests : IClassFixture<ApiDePrueba>
{
    private readonly ApiDePrueba _api;

    public ConsultasFuturasEnElListadoTests(ApiDePrueba api) => _api = api;

    private record AfiliadoLeido(int Id, int ConsultasFuturasAgendadas);

    [Fact]
    public async Task El_conteo_viaja_en_la_respuesta_y_distingue_por_afiliado()
    {
        int conTurnos, sinTurnos;
        using (var db = _api.CrearContexto())
        {
            db.Database.EnsureCreated();
            var profesional = db.AgregarProfesional();
            var uno = db.AgregarAfiliado("Con turnos", dni: "31000001");
            var otro = db.AgregarAfiliado("Sin turnos", dni: "31000002");
            conTurnos = uno.Id;
            sinTurnos = otro.Id;

            db.AgregarConsulta(uno, profesional, DateTime.UtcNow.AddDays(30));
            db.AgregarConsulta(uno, profesional, DateTime.UtcNow.AddDays(31),
                EstadoConsulta.Confirmado);
            db.AgregarConsulta(uno, profesional, DateTime.UtcNow.AddDays(-30),
                EstadoConsulta.Atendido);
        }

        var afiliados = (await _api.CreateClient()
            .GetFromJsonAsync<List<AfiliadoLeido>>("/afiliados"))!;

        Assert.Equal(2, afiliados.Single(a => a.Id == conTurnos).ConsultasFuturasAgendadas);
        Assert.Equal(0, afiliados.Single(a => a.Id == sinTurnos).ConsultasFuturasAgendadas);
    }
}
