using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConsultasPlan.Contratos;
using ConsultasPlan.Data;
using ConsultasPlan.Dominio;
using ConsultasPlan.Helpers;
using ConsultasPlan.Models;

namespace ConsultasPlan.Controllers;

[ApiController]
[Route("[controller]")]
public class ConsultasController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _reloj;

    public ConsultasController(AppDbContext context, TimeProvider reloj)
    {
        _context = context;
        _reloj = reloj;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    // El código base validaba 23 horas mientras el mensaje al usuario prometía
    // 24: en esa franja el sistema aceptaba lo que decía rechazar.
    private static readonly TimeSpan AnticipacionMinimaParaCancelar = TimeSpan.FromHours(24);

    /// <summary>
    /// 409: la petición es válida y está bien formada, pero el estado actual
    /// del sistema no permite la operación. 400 queda reservado para entrada
    /// malformada, que es un problema del cliente y no del estado.
    /// </summary>
    private ObjectResult Conflicto(string detalle) => Problem(
        detail: detalle,
        statusCode: StatusCodes.Status409Conflict,
        title: "La operación no es posible en el estado actual");

    private ObjectResult NoEncontrado(string detalle) => Problem(
        detail: detalle,
        statusCode: StatusCodes.Status404NotFound,
        title: "Recurso no encontrado");

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var consultas = await _context.Consultas
            .Include(c => c.Afiliado)
            .Include(c => c.Profesional)
            .ToListAsync();
        return Ok(consultas.Select(ConsultaResponse.De));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var consulta = await _context.Consultas
            .Include(c => c.Afiliado)
            .Include(c => c.Profesional)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (consulta == null) return NoEncontrado("La consulta indicada no existe.");
        return Ok(ConsultaResponse.De(consulta));
    }

    [HttpPost]
    public async Task<IActionResult> CrearConsulta([FromBody] CrearConsultaRequest request)
    {
        // La fecha se valida acá y no con un atributo porque depende del reloj,
        // que es una dependencia inyectada.
        if (request.FechaHora <= Ahora)
        {
            ModelState.AddModelError(nameof(request.FechaHora),
                "La consulta debe agendarse en una fecha futura.");
            return ValidationProblem(ModelState);
        }

        var afiliado = await _context.Afiliados.FindAsync(request.AfiliadoId);
        if (afiliado == null)
            return NoEncontrado("El afiliado indicado no existe.");

        // Un afiliado dado de baja no figura en ninguna vista operativa, pero
        // su id sigue existiendo: agendar por id debe negarse igual.
        if (!afiliado.Activo)
            return NoEncontrado("El afiliado indicado no existe.");

        if (afiliado.Bloqueado)
            return Conflicto("El afiliado se encuentra bloqueado para agendar consultas.");

        var profesionalExiste = await _context.Profesionales.AnyAsync(p => p.Id == request.ProfesionalId);
        if (!profesionalExiste)
            return NoEncontrado("El profesional indicado no existe.");

        var consultaConflicto = await _context.Consultas.AnyAsync(c =>
            c.ProfesionalId == request.ProfesionalId &&
            c.FechaHora == request.FechaHora &&
            c.Estado != EstadoConsulta.Cancelado);
        if (consultaConflicto)
            return Conflicto("El profesional ya tiene una consulta agendada en ese horario.");

        var consulta = new Consulta
        {
            AfiliadoId = request.AfiliadoId,
            ProfesionalId = request.ProfesionalId,
            FechaHora = request.FechaHora,
            Modalidad = request.Modalidad,
            Motivo = request.Motivo,
            FechaCreacion = Ahora,
            Estado = EstadoConsulta.Pendiente
        };

        _context.Consultas.Add(consulta);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = consulta.Id }, ConsultaResponse.De(consulta));
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
    public async Task<IActionResult> CancelarConsulta(int id)
    {
        var consulta = await _context.Consultas.FindAsync(id);
        if (consulta == null) return NoEncontrado("La consulta indicada no existe.");

        if (!CicloDeVidaConsulta.PuedeTransicionar(consulta.Estado, EstadoConsulta.Cancelado))
            return Conflicto(CicloDeVidaConsulta.ExplicarRechazo(consulta.Estado, EstadoConsulta.Cancelado));

        if (consulta.FechaHora - Ahora < AnticipacionMinimaParaCancelar)
            return Conflicto("No se puede cancelar con menos de 24 horas de anticipación.");

        consulta.Estado = EstadoConsulta.Cancelado;
        await _context.SaveChangesAsync();
        return Ok(ConsultaResponse.De(consulta));
    }

    [HttpPost("{id}/inasistencia")]
    public async Task<IActionResult> MarcarInasistencia(int id)
    {
        var consulta = await _context.Consultas.FindAsync(id);
        if (consulta == null) return NoEncontrado("La consulta indicada no existe.");

        if (!CicloDeVidaConsulta.PuedeTransicionar(consulta.Estado, EstadoConsulta.Inasistencia))
            return Conflicto(CicloDeVidaConsulta.ExplicarRechazo(consulta.Estado, EstadoConsulta.Inasistencia));

        if (consulta.FechaHora > Ahora)
            return Conflicto("No se puede registrar una inasistencia sobre una consulta futura.");

        consulta.Estado = EstadoConsulta.Inasistencia;
        await _context.SaveChangesAsync();
        return Ok(ConsultaResponse.De(consulta));
    }

    [HttpPost("{id}/atencion")]
    public async Task<IActionResult> RegistrarAtencion(int id)
    {
        var consulta = await _context.Consultas.FindAsync(id);
        if (consulta == null) return NoEncontrado("La consulta indicada no existe.");

        if (!CicloDeVidaConsulta.PuedeTransicionar(consulta.Estado, EstadoConsulta.Atendido))
            return Conflicto(CicloDeVidaConsulta.ExplicarRechazo(consulta.Estado, EstadoConsulta.Atendido));

        if (!consulta.FechaHora.IsInCurrentYear(Ahora))
            return Conflicto("Solo se pueden registrar atenciones de consultas del año en curso.");

        consulta.Estado = EstadoConsulta.Atendido;
        await _context.SaveChangesAsync();
        return Ok(ConsultaResponse.De(consulta));
    }

    [HttpPut("{id}/estado")]
    public async Task<IActionResult> ActualizarEstado(int id, [FromBody] ActualizarEstadoRequest request)
    {
        // Que el valor pertenezca al enum lo valida [EnumDataType] en el
        // contrato: [ApiController] rechaza con 400 antes de llegar acá.
        var consulta = await _context.Consultas.FindAsync(id);
        if (consulta == null) return NoEncontrado("La consulta indicada no existe.");

        // El código base asignaba el estado sin validar nada: este endpoint era
        // la vía por la que se evadían todas las reglas del dominio.
        if (!CicloDeVidaConsulta.PuedeTransicionar(consulta.Estado, request.Estado))
            return Conflicto(CicloDeVidaConsulta.ExplicarRechazo(consulta.Estado, request.Estado));

        consulta.Estado = request.Estado;
        await _context.SaveChangesAsync();
        return Ok(ConsultaResponse.De(consulta));
    }
}
