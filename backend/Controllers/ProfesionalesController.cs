using ConsultasPlan.Contratos;
using ConsultasPlan.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasPlan.Controllers;

[ApiController]
[Route("[controller]")]
public class ProfesionalesController : ControllerBase
{
    private readonly ICatalogoService _catalogo;

    public ProfesionalesController(ICatalogoService catalogo) => _catalogo = catalogo;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok((await _catalogo.ListarProfesionales()).Select(ProfesionalResponse.De));
}
