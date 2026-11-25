using ConsultasPlan.Contratos;
using ConsultasPlan.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasPlan.Controllers;

[ApiController]
[Route("[controller]")]
public class ConsultasController : ApiControllerBase
{
    private readonly IConsultaService _consultas;

    public ConsultasController(IConsultaService consultas) => _consultas = consultas;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok((await _consultas.Listar()).Select(ConsultaResponse.De));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id) => Responder(await _consultas.Obtener(id));

    [HttpPost]
    public async Task<IActionResult> CrearConsulta([FromBody] CrearConsultaRequest request)
    {
        var resultado = await _consultas.Crear(request);
        if (!resultado.EsExito) return Problema(resultado.Falla!);

        return CreatedAtAction(nameof(GetById),
            new { id = resultado.Valor.Id },
            ConsultaResponse.De(resultado.Valor));
    }

    /// <summary>
    /// Cancela una consulta.
    /// </summary>
    /// <remarks>
    /// El código base exponía esta operación como <c>GET /consultas/cancelar/{id}</c>.
    /// GET está definido como seguro: no debe producir efectos. Navegadores,
    /// proxies y crawlers lo asumen, y cualquier precarga de enlaces, un
    /// rastreador o una etiqueta &lt;img&gt; apuntando a esa URL cancelaba la
    /// consulta sin que nadie lo pidiera.
    ///
    /// Pasa a POST sobre un sub-recurso, igual que las demás operaciones de la
    /// consulta (/inasistencia, /atencion).
    /// </remarks>
    [HttpPost("{id}/cancelacion")]
    public async Task<IActionResult> CancelarConsulta(int id) =>
        Responder(await _consultas.Cancelar(id));

    [HttpPost("{id}/inasistencia")]
    public async Task<IActionResult> MarcarInasistencia(int id) =>
        Responder(await _consultas.RegistrarInasistencia(id));

    [HttpPost("{id}/atencion")]
    public async Task<IActionResult> RegistrarAtencion(int id) =>
        Responder(await _consultas.RegistrarAtencion(id));

    [HttpPut("{id}/estado")]
    public async Task<IActionResult> ActualizarEstado(int id, [FromBody] ActualizarEstadoRequest request) =>
        Responder(await _consultas.CambiarEstado(id, request.Estado));

    private IActionResult Responder(ServiceResult<Models.Consulta> resultado) =>
        resultado.EsExito
            ? Ok(ConsultaResponse.De(resultado.Valor))
            : Problema(resultado.Falla!);
}
