using System.Net.Http.Json;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;

namespace ConsultasPlan.Tests.Contratos;

/// <summary>
/// El catálogo informa qué profesionales están alcanzados por una política.
/// </summary>
/// <remarks>
/// La alternativa era que el cliente comparara el nombre de la especialidad
/// contra el literal "Nutrición". Es la misma fragilidad que el catálogo
/// eliminó del backend, movida al frontend: renombrar la especialidad desde un
/// ABM apagaría la interfaz de cupo sin que nada falle.
/// </remarks>
public class CupoEnElCatalogoTests : IClassFixture<ApiDePrueba>
{
    private readonly ApiDePrueba _api;

    public CupoEnElCatalogoTests(ApiDePrueba api) => _api = api;

    private record ProfesionalLeido(int Id, string Especialidad, bool TieneCupo);

    [Theory]
    [InlineData(CodigosDeEspecialidad.Nutricion, true)]
    [InlineData("CLI", false)]
    [InlineData("CAR", false)]
    public async Task Solo_la_especialidad_de_la_politica_declara_cupo(
        string codigo, bool esperado)
    {
        int id;
        using (var db = _api.CrearContexto())
        {
            db.Database.EnsureCreated();
            id = db.AgregarProfesional(codigoEspecialidad: codigo).Id;
        }

        var profesionales = await _api.CreateClient()
            .GetFromJsonAsync<List<ProfesionalLeido>>("/profesionales");

        Assert.Equal(esperado, profesionales!.Single(p => p.Id == id).TieneCupo);
    }
}
