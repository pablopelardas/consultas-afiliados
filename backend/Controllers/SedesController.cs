using ConsultasPlan.Contratos;
using ConsultasPlan.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasPlan.Controllers;

[ApiController]
[Route("[controller]")]
public class SedesController : ControllerBase
{
    private readonly ICatalogoService _catalogo;

    public SedesController(ICatalogoService catalogo) => _catalogo = catalogo;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok((await _catalogo.ListarSedes()).Select(SedeResponse.De));
}
