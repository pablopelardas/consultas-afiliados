using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConsultasPlan.Data;
using ConsultasPlan.Models;

namespace ConsultasPlan.Controllers;

[ApiController]
[Route("[controller]")]
public class AfiliadosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _reloj;

    public AfiliadosController(AppDbContext context, TimeProvider reloj)
    {
        _context = context;
        _reloj = reloj;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    private ObjectResult NoEncontrado() => Problem(
        detail: "El afiliado indicado no existe.",
        statusCode: StatusCodes.Status404NotFound,
        title: "Recurso no encontrado");

    private ObjectResult Conflicto(string detalle) => Problem(
        detail: detalle,
        statusCode: StatusCodes.Status409Conflict,
        title: "La operación no es posible en el estado actual");

    /// <summary>
    /// Afiliados vigentes. Los dados de baja quedan excluidos.
    /// </summary>
    /// <remarks>
    /// El filtro es explícito y no un <c>HasQueryFilter</c> global, a propósito.
    /// Un filtro global también alcanzaría a <c>Include(c => c.Afiliado)</c> al
    /// leer consultas, dejando sin afiliado a las consultas de alguien dado de
    /// baja: el historial clínico perdería el nombre del paciente. Esa es
    /// justamente la integridad que la baja lógica viene a proteger.
    ///
    /// La diferencia entre olvidarse de un filtro y olvidarse de una excepción
    /// al filtro es de gravedad: en el primer caso se lista a alguien de más,
    /// en el segundo se pierde un historial.
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var afiliados = await _context.Afiliados
            .Where(a => a.Activo)
            .ToListAsync();
        return Ok(afiliados);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var afiliado = await _context.Afiliados
            .FirstOrDefaultAsync(a => a.Id == id && a.Activo);
        if (afiliado == null) return NoEncontrado();
        return Ok(afiliado);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Afiliado afiliado)
    {
        afiliado.FechaAlta = Ahora;
        afiliado.Activo = true;
        afiliado.FechaBaja = null;
        _context.Afiliados.Add(afiliado);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = afiliado.Id }, afiliado);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Afiliado afiliado)
    {
        var existing = await _context.Afiliados
            .FirstOrDefaultAsync(a => a.Id == id && a.Activo);
        if (existing == null) return NoEncontrado();

        existing.NombreCompleto = afiliado.NombreCompleto;
        existing.DNI = afiliado.DNI;
        existing.Email = afiliado.Email;
        existing.Telefono = afiliado.Telefono;

        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    /// <summary>
    /// Da de baja a un afiliado sin borrar su registro.
    /// </summary>
    /// <remarks>
    /// El código base ejecutaba <c>Remove</c>. Con la clave foránea en
    /// <c>SetNull</c>, eso dejaba las consultas del afiliado sin paciente: se
    /// perdía de forma irreversible a quién correspondía cada atención médica.
    ///
    /// El modelo ya traía el campo para hacerlo bien y nadie lo usaba.
    /// </remarks>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var afiliado = await _context.Afiliados.FindAsync(id);
        if (afiliado == null) return NoEncontrado();

        if (!afiliado.Activo)
            return Conflicto("El afiliado ya se encuentra dado de baja.");

        afiliado.Activo = false;
        afiliado.FechaBaja = Ahora;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Revierte una baja lógica.
    /// </summary>
    /// <remarks>
    /// Una baja que no se puede deshacer es, en la práctica, un borrado con
    /// otro nombre. La reactivación es lo que distingue la baja lógica de un
    /// borrado que además ocupa lugar.
    /// </remarks>
    [HttpPost("{id}/reactivacion")]
    public async Task<IActionResult> Reactivar(int id)
    {
        var afiliado = await _context.Afiliados.FindAsync(id);
        if (afiliado == null) return NoEncontrado();

        if (afiliado.Activo)
            return Conflicto("El afiliado ya se encuentra activo.");

        afiliado.Activo = true;
        afiliado.FechaBaja = null;
        await _context.SaveChangesAsync();
        return Ok(afiliado);
    }
}
