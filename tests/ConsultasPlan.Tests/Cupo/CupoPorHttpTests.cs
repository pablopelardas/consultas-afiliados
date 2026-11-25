using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConsultasPlan.Contratos;
using ConsultasPlan.Tests.Infraestructura;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasPlan.Tests.Cupo;

/// <summary>
/// La política de cupo atravesando toda la pila, contra los datos sembrados.
/// </summary>
/// <remarks>
/// Lo que estos tests agregan sobre los anteriores: que las migraciones apliquen,
/// que el catálogo quede poblado, que el sembrado referencie la especialidad por
/// código y que el rechazo llegue como 409 con <c>ProblemDetails</c> y un detalle
/// legible. Cada una de esas piezas se verifica aparte; acá se verifica que
/// encajan.
/// </remarks>
public class CupoPorHttpTests : IClassFixture<ApiDePrueba>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly ApiDePrueba _api;
    private readonly HttpClient _cliente;

    public CupoPorHttpTests(ApiDePrueba api)
    {
        _api = api;
        _cliente = api.CreateClient();
    }

    private async Task<int> AfiliadoPorDni(string dni)
    {
        var afiliados = await _cliente.GetFromJsonAsync<List<AfiliadoResponse>>("/afiliados", Json);
        return afiliados!.Single(a => a.DNI == dni).Id;
    }

    private async Task<int> UnNutricionista()
    {
        var profesionales = await _cliente.GetFromJsonAsync<List<ProfesionalResponse>>(
            "/profesionales", Json);

        // El contrato público sigue exponiendo la especialidad por su nombre: el
        // catálogo es identidad interna y el frontend no se enteró del cambio.
        return profesionales!.First(p => p.Especialidad == "Nutrición").Id;
    }

    private Task<CupoNutricionResponse?> Cupo(int afiliadoId) =>
        _cliente.GetFromJsonAsync<CupoNutricionResponse>(
            $"/afiliados/{afiliadoId}/cupo-nutricion", Json);

    // El afiliado sembrado con seis consultas de nutrición en el año.
    private const string DniConElAnoAgotado = "25789012";

    [Fact]
    public async Task El_cupo_se_puede_consultar_antes_de_intentar_agendar()
    {
        var cupo = await Cupo(await AfiliadoPorDni(DniConElAnoAgotado));

        Assert.Equal(6, cupo!.Ano.Limite);
        Assert.Equal(6, cupo.Ano.Usadas);
        Assert.Equal(0, cupo.Ano.Disponibles);

        // El reloj de los tests está fijado en 2026.
        Assert.Equal(new DateTime(2026, 1, 1), cupo.Ano.Desde.Date);
        Assert.Equal(new DateTime(2026, 12, 31), cupo.Ano.Hasta.Date);
    }

    [Fact]
    public async Task Un_afiliado_sin_historial_tiene_el_cupo_entero()
    {
        var cupo = await Cupo(await AfiliadoPorDni("28445123"));

        Assert.Equal(0, cupo!.Ano.Usadas);
        Assert.Equal(6, cupo.Ano.Disponibles);
        Assert.Equal(1, cupo.Mes.Disponibles);
    }

    // §5.6 y §5.3 sobre los datos sembrados: una virtual atendida y una
    // presencial cancelada. Ninguna consume.
    [Fact]
    public async Task Una_virtual_y_una_cancelada_no_aparecen_como_consumidas()
    {
        var cupo = await Cupo(await AfiliadoPorDni("29876543"));

        Assert.Equal(0, cupo!.Ano.Usadas);
    }

    // El afiliado sembrado tiene una consulta de nutrición del año anterior.
    [Fact]
    public async Task Las_consultas_del_ano_anterior_no_consumen_el_cupo_actual()
    {
        var cupo = await Cupo(await AfiliadoPorDni("36901234"));

        Assert.Equal(0, cupo!.Ano.Usadas);
    }

    [Fact]
    public async Task El_cupo_de_un_afiliado_inexistente_es_404()
    {
        var respuesta = await _cliente.GetAsync("/afiliados/999999/cupo-nutricion");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>
    /// §5.11: la petición es válida y está autorizada; lo que falla es que el
    /// estado del sistema no permite la operación. Eso es conflicto, no error de
    /// sintaxis.
    /// </summary>
    [Fact]
    public async Task Exceder_el_cupo_devuelve_409_con_ProblemDetails_legible()
    {
        var afiliadoId = await AfiliadoPorDni(DniConElAnoAgotado);
        var profesionalId = await UnNutricionista();

        var respuesta = await _cliente.PostAsJsonAsync("/consultas", new
        {
            afiliadoId,
            profesionalId,
            fechaHora = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            modalidad = "Presencial",
            motivo = "La septima del anio"
        });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(Json);

        Assert.Equal(409, problema!.Status);
        Assert.False(string.IsNullOrWhiteSpace(problema.Detail));

        // El detalle le dice al usuario cuál es el límite, de qué período y que
        // lo no usado no se acumula. Sin eso, el rechazo obliga a adivinar.
        Assert.Contains("6 consultas de nutrición por año", problema.Detail);
        Assert.Contains("no se acumulan", problema.Detail);
    }

    [Fact]
    public async Task Con_el_cupo_disponible_se_agenda_normalmente()
    {
        var afiliadoId = await AfiliadoPorDni("28445123");
        var profesionalId = await UnNutricionista();

        var respuesta = await _cliente.PostAsJsonAsync("/consultas", new
        {
            afiliadoId,
            profesionalId,
            fechaHora = new DateTime(2026, 12, 3, 9, 0, 0, DateTimeKind.Utc),
            modalidad = "Presencial",
            motivo = "Primera consulta nutricional"
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        // Y el cupo lo refleja: la pendiente recién creada ya consume.
        var cupo = await Cupo(afiliadoId);
        Assert.Equal(1, cupo!.Ano.Usadas);
        Assert.Equal(5, cupo.Ano.Disponibles);
    }

    // Verifica que el sembrado haya pasado por el catálogo: si el profesional
    // hubiera quedado sin especialidad, esto sería vacío y la política no
    // aplicaría a nada.
    [Fact]
    public async Task Los_profesionales_sembrados_tienen_su_especialidad_del_catalogo()
    {
        var profesionales = await _cliente.GetFromJsonAsync<List<ProfesionalResponse>>(
            "/profesionales", Json);

        Assert.All(profesionales!, p => Assert.False(string.IsNullOrWhiteSpace(p.Especialidad)));
        Assert.Contains(profesionales!, p => p.Especialidad == "Nutrición");
        Assert.Contains(profesionales!, p => p.Especialidad == "Clínica Médica");
    }
}
