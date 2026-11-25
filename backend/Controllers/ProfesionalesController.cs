using ConsultasPlan.Contratos;
using ConsultasPlan.Dominio.Cupo;
using ConsultasPlan.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasPlan.Controllers;

[ApiController]
[Route("[controller]")]
public class ProfesionalesController : ControllerBase
{
    private readonly ICatalogoService _catalogo;
    private readonly ICupoPolicy _cupo;

    public ProfesionalesController(ICatalogoService catalogo, ICupoPolicy cupo)
    {
        _catalogo = catalogo;
        _cupo = cupo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok((await _catalogo.ListarProfesionales())
            .Select(p => ProfesionalResponse.De(p, _cupo.CodigoEspecialidad)));
}
