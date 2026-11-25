using ConsultasPlan.Contratos;
using ConsultasPlan.Controllers;
using ConsultasPlan.Models;
using ConsultasPlan.Servicios;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasPlan.Tests.Servicios;

/// <summary>
/// El controlador se verifica con el servicio sustituido.
/// </summary>
/// <remarks>
/// Esto es lo que compra la interfaz. Sin base de datos, sin reloj y sin
/// pipeline: se le entrega al controlador un servicio que devuelve exactamente
/// la falla que se quiere probar, y se comprueba la única cosa que el
/// controlador hace — elegir el código de estado.
///
/// Antes de la capa de servicios no había forma de hacerlo: para ver un 404
/// había que construir el estado de base que lo produce.
/// </remarks>
public class SustitucionDelServicioTests
{
    private sealed class ConsultaServiceFalso : IConsultaService
    {
        private readonly Falla? _falla;

        public ConsultaServiceFalso(Falla? falla = null) => _falla = falla;

        private ServiceResult<Consulta> Respuesta()
        {
            if (_falla is not null) return _falla;
            return ServiceResult.Ok(new Consulta { Id = 1, Motivo = "Consulta de prueba" });
        }

        public Task<IReadOnlyList<Consulta>> Listar() =>
            Task.FromResult<IReadOnlyList<Consulta>>(Array.Empty<Consulta>());

        public Task<ServiceResult<Consulta>> Obtener(int id) => Task.FromResult(Respuesta());
        public Task<ServiceResult<Consulta>> Crear(CrearConsultaRequest r) => Task.FromResult(Respuesta());
        public Task<ServiceResult<Consulta>> Cancelar(int id) => Task.FromResult(Respuesta());
        public Task<ServiceResult<Consulta>> RegistrarInasistencia(int id) => Task.FromResult(Respuesta());
        public Task<ServiceResult<Consulta>> RegistrarAtencion(int id) => Task.FromResult(Respuesta());

        public Task<ServiceResult<Consulta>> CambiarEstado(int id, EstadoConsulta estado) =>
            Task.FromResult(Respuesta());
    }

    private static ConsultasController Controlador(Falla? falla = null) =>
        new(new ConsultaServiceFalso(falla));

    [Fact]
    public async Task NoEncontrado_se_traduce_a_404()
    {
        var resultado = await Controlador(Falla.NoEncontrado("No existe.")).CancelarConsulta(1);

        Assert.Equal(StatusCodes.Status404NotFound,
            Assert.IsType<ObjectResult>(resultado).StatusCode);
    }

    [Fact]
    public async Task Conflicto_se_traduce_a_409()
    {
        var resultado = await Controlador(Falla.Conflicto("No se puede en este estado.")).CancelarConsulta(1);

        Assert.Equal(StatusCodes.Status409Conflict,
            Assert.IsType<ObjectResult>(resultado).StatusCode);
    }

    // El código de estado de la entrada inválida se verifica por HTTP en
    // Contratos/ValidacionDeEntradaTests: ValidationProblem depende de
    // ProblemDetailsFactory, que se resuelve por inyección. Acá se verifica lo
    // que sí es propio del mapeo — que el error quede asociado al campo, para
    // que el cliente lo muestre al lado del input y no en un cartel genérico.
    [Fact]
    public async Task EntradaInvalida_asocia_el_error_al_campo()
    {
        var controlador = Controlador(
            Falla.EntradaInvalida("FechaHora", "Debe ser futura."));

        await controlador.CrearConsulta(new CrearConsultaRequest());

        Assert.True(controlador.ModelState.ContainsKey("FechaHora"));
        Assert.Equal("Debe ser futura.",
            controlador.ModelState["FechaHora"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task El_exito_se_traduce_a_200_con_el_contrato_de_salida()
    {
        var resultado = await Controlador().CancelarConsulta(1);

        var cuerpo = Assert.IsType<OkObjectResult>(resultado).Value;
        Assert.IsType<ConsultaResponse>(cuerpo);
    }

    [Fact]
    public async Task La_creacion_exitosa_se_traduce_a_201()
    {
        var resultado = await Controlador().CrearConsulta(new CrearConsultaRequest());

        Assert.IsType<CreatedAtActionResult>(resultado);
    }
}
