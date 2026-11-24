using System.Net;
using System.Net.Http.Json;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;

namespace ConsultasPlan.Tests.Semantica;

/// <summary>
/// Verbos y códigos de respuesta de las operaciones sobre una consulta.
/// </summary>
/// <remarks>
/// Estos tests atraviesan el pipeline HTTP porque lo que verifican lo decide el
/// ruteo, no el cuerpo del método: si una ruta acepta GET, qué código devuelve
/// y con qué formato de error.
/// </remarks>
public class VerbosHttpTests : IClassFixture<ApiDePrueba>
{
    private readonly ApiDePrueba _api;

    public VerbosHttpTests(ApiDePrueba api) => _api = api;

    private (int consultaId, int afiliadoId, int profesionalId) SembrarConsulta(
        DateTime fechaHora, EstadoConsulta estado = EstadoConsulta.Pendiente)
    {
        using var db = _api.CrearContexto();
        db.Database.EnsureCreated();

        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        var consulta = db.AgregarConsulta(afiliado, profesional, fechaHora, estado);

        return (consulta.Id, afiliado.Id, profesional.Id);
    }

    // El defecto central del código base: cancelar respondía a GET.
    // GET está definido como seguro, así que una precarga de enlaces, un
    // crawler o una etiqueta <img> apuntando a esa URL cancelaba la consulta.
    [Fact]
    public async Task Cancelar_ya_no_responde_a_GET()
    {
        var (consultaId, _, _) = SembrarConsulta(Reloj.Ahora.AddDays(10));
        var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync($"/consultas/cancelar/{consultaId}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);

        using var db = _api.CrearContexto();
        Assert.Equal(EstadoConsulta.Pendiente, db.Consultas.Find(consultaId)!.Estado);
    }

    [Fact]
    public async Task Cancelar_responde_a_POST_sobre_el_sub_recurso()
    {
        var (consultaId, _, _) = SembrarConsulta(Reloj.Ahora.AddDays(10));
        var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsync($"/consultas/{consultaId}/cancelacion", null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var db = _api.CrearContexto();
        Assert.Equal(EstadoConsulta.Cancelado, db.Consultas.Find(consultaId)!.Estado);
    }

    // Una regla de negocio incumplida es 409, no 400: la petición está bien
    // formada, lo que no permite la operación es el estado del recurso.
    [Fact]
    public async Task Cancelar_fuera_de_plazo_devuelve_409_con_ProblemDetails()
    {
        var (consultaId, _, _) = SembrarConsulta(Reloj.Ahora.AddHours(2));
        var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsync($"/consultas/{consultaId}/cancelacion", null);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetailsLeido>();
        Assert.Equal(409, problema!.Status);
        Assert.Contains("24 horas", problema.Detail);
    }

    [Fact]
    public async Task Una_consulta_inexistente_devuelve_404_con_ProblemDetails()
    {
        var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsync("/consultas/999999/cancelacion", null);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    // Un valor fuera del enum sí es 400: eso es entrada malformada, un
    // problema del cliente y no del estado del sistema.
    [Fact]
    public async Task Un_estado_fuera_del_enum_devuelve_400_y_no_se_persiste()
    {
        var (consultaId, _, _) = SembrarConsulta(Reloj.Ahora.AddDays(20));
        var cliente = _api.CreateClient();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/consultas/{consultaId}/estado", new { estado = 99 });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        using var db = _api.CrearContexto();
        Assert.Equal(EstadoConsulta.Pendiente, db.Consultas.Find(consultaId)!.Estado);
    }

    private sealed record ProblemDetailsLeido(string? Title, string? Detail, int Status);
}
