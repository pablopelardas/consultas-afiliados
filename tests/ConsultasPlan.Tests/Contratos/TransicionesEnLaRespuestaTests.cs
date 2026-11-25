using System.Net.Http.Json;
using ConsultasPlan.Contratos;
using ConsultasPlan.Dominio;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;

namespace ConsultasPlan.Tests.Contratos;

/// <summary>
/// La respuesta declara a qué estados puede pasar la consulta.
/// </summary>
/// <remarks>
/// Sin esto el cliente tiene dos opciones malas: reimplementar la tabla de
/// transiciones —una copia que se desincroniza sin que nada falle— u ofrecer las
/// cinco acciones siempre y dejar que el usuario descubra la regla cuando lo
/// frena un 409.
/// </remarks>
public class TransicionesEnLaRespuestaTests
{
    /// <summary>
    /// El contrato no copia la tabla: la consulta.
    /// </summary>
    /// <remarks>
    /// Recorre todos los estados en lugar de ejemplificar dos: si alguien agrega
    /// un estado o cambia una fila de la tabla, acá se entera.
    /// </remarks>
    [Fact]
    public void Lo_que_declara_la_respuesta_es_la_tabla_del_dominio()
    {
        foreach (var estado in Enum.GetValues<EstadoConsulta>())
        {
            var respuesta = ConsultaResponse.De(new Consulta { Id = 1, Estado = estado });

            Assert.Equal(
                CicloDeVidaConsulta.TransicionesDesde(estado),
                respuesta.TransicionesPermitidas);
        }
    }

    [Fact]
    public void Un_estado_terminal_no_declara_ninguna()
    {
        var respuesta = ConsultaResponse.De(
            new Consulta { Id = 1, Estado = EstadoConsulta.Cancelado });

        Assert.Empty(respuesta.TransicionesPermitidas);
    }
}

/// <summary>
/// Que el campo además llegue serializado. El mapeo puede estar bien y el
/// cliente recibir un enum numérico que no sabe interpretar.
/// </summary>
public class TransicionesPorHttpTests : IClassFixture<ApiDePrueba>
{
    private readonly ApiDePrueba _api;

    public TransicionesPorHttpTests(ApiDePrueba api) => _api = api;

    [Fact]
    public async Task Llegan_como_texto_en_el_cuerpo_de_la_consulta()
    {
        int id;
        using (var db = _api.CrearContexto())
        {
            db.Database.EnsureCreated();
            var afiliado = db.AgregarAfiliado(dni: $"3{Random.Shared.Next(1000000, 9999999)}");
            var profesional = db.AgregarProfesional();
            id = db.AgregarConsulta(afiliado, profesional, Reloj.Ahora.AddDays(10)).Id;
        }

        var cuerpo = await _api.CreateClient().GetFromJsonAsync<Dictionary<string, object>>(
            $"/consultas/{id}");

        var transiciones = Assert.IsType<System.Text.Json.JsonElement>(
            cuerpo!["transicionesPermitidas"]);

        Assert.Equal(
            CicloDeVidaConsulta.TransicionesDesde(EstadoConsulta.Pendiente)
                .Select(e => e.ToString()),
            transiciones.EnumerateArray().Select(e => e.GetString()));
    }
}
