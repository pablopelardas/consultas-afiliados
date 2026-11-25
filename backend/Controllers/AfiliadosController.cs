using ConsultasPlan.Contratos;
using ConsultasPlan.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasPlan.Controllers;

[ApiController]
[Route("[controller]")]
public class AfiliadosController : ApiControllerBase
{
    private readonly IAfiliadoService _afiliados;

    public AfiliadosController(IAfiliadoService afiliados) => _afiliados = afiliados;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok((await _afiliados.ListarVigentes()).Select(AfiliadoResponse.De));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id) => Responder(await _afiliados.Obtener(id));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearAfiliadoRequest request)
    {
        var resultado = await _afiliados.Crear(request);
        if (!resultado.EsExito) return Problema(resultado.Falla!);

        return CreatedAtAction(nameof(GetById),
            new { id = resultado.Valor.Id },
            AfiliadoResponse.De(resultado.Valor));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarAfiliadoRequest request) =>
        Responder(await _afiliados.Actualizar(id, request));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var resultado = await _afiliados.DarDeBaja(id);
        return resultado.EsExito ? NoContent() : Problema(resultado.Falla!);
    }

    [HttpPost("{id}/reactivacion")]
    public async Task<IActionResult> Reactivar(int id) =>
        Responder(await _afiliados.Reactivar(id));

    private IActionResult Responder(ServiceResult<Models.Afiliado> resultado) =>
        resultado.EsExito
            ? Ok(AfiliadoResponse.De(resultado.Valor))
            : Problema(resultado.Falla!);
}
