using ConsultasPlan.Contratos;
using ConsultasPlan.Data;
using ConsultasPlan.Dominio;
using ConsultasPlan.Helpers;
using ConsultasPlan.Models;
using Microsoft.EntityFrameworkCore;

namespace ConsultasPlan.Servicios;

public class ConsultaService : IConsultaService
{
    private const string ConsultaInexistente = "La consulta indicada no existe.";
    private const string AfiliadoInexistente = "El afiliado indicado no existe.";

    // El código base validaba 23 horas mientras el mensaje al usuario prometía
    // 24: en esa franja el sistema aceptaba lo que decía rechazar.
    private static readonly TimeSpan AnticipacionMinimaParaCancelar = TimeSpan.FromHours(24);

    private readonly AppDbContext _contexto;
    private readonly TimeProvider _reloj;

    public ConsultaService(AppDbContext contexto, TimeProvider reloj)
    {
        _contexto = contexto;
        _reloj = reloj;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<Consulta>> Listar() =>
        await ConsultasConRelaciones().ToListAsync();

    public async Task<ServiceResult<Consulta>> Obtener(int id)
    {
        var consulta = await ConsultasConRelaciones().FirstOrDefaultAsync(c => c.Id == id);
        if (consulta is null) return Falla.NoEncontrado(ConsultaInexistente);
        return ServiceResult.Ok(consulta);
    }

    public async Task<ServiceResult<Consulta>> Crear(CrearConsultaRequest request)
    {
        // La fecha no se valida con un atributo porque depende del reloj, que es
        // una dependencia inyectada.
        if (request.FechaHora <= Ahora)
            return Falla.EntradaInvalida(nameof(request.FechaHora),
                "La consulta debe agendarse en una fecha futura.");

        var afiliado = await _contexto.Afiliados.FindAsync(request.AfiliadoId);

        // Un afiliado dado de baja no figura en ninguna vista operativa, pero su
        // id sigue existiendo: agendar por id debe negarse igual.
        if (afiliado is null || !afiliado.Activo)
            return Falla.NoEncontrado(AfiliadoInexistente);

        if (afiliado.Bloqueado)
            return Falla.Conflicto("El afiliado se encuentra bloqueado para agendar consultas.");

        var profesionalExiste = await _contexto.Profesionales
            .AnyAsync(p => p.Id == request.ProfesionalId);
        if (!profesionalExiste)
            return Falla.NoEncontrado("El profesional indicado no existe.");

        var horarioOcupado = await _contexto.Consultas.AnyAsync(c =>
            c.ProfesionalId == request.ProfesionalId &&
            c.FechaHora == request.FechaHora &&
            c.Estado != EstadoConsulta.Cancelado);
        if (horarioOcupado)
            return Falla.Conflicto("El profesional ya tiene una consulta agendada en ese horario.");

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

        _contexto.Consultas.Add(consulta);
        await _contexto.SaveChangesAsync();
        return ServiceResult.Ok(consulta);
    }

    public Task<ServiceResult<Consulta>> Cancelar(int id) =>
        Transicionar(id, EstadoConsulta.Cancelado);

    public Task<ServiceResult<Consulta>> RegistrarInasistencia(int id) =>
        Transicionar(id, EstadoConsulta.Inasistencia);

    public Task<ServiceResult<Consulta>> RegistrarAtencion(int id) =>
        Transicionar(id, EstadoConsulta.Atendido);

    public Task<ServiceResult<Consulta>> CambiarEstado(int id, EstadoConsulta estadoDestino) =>
        Transicionar(id, estadoDestino);

    /// <summary>
    /// Único camino por el que una consulta cambia de estado.
    /// </summary>
    /// <remarks>
    /// Las cuatro operaciones de estado tienen la misma forma: buscar, validar
    /// la transición, validar la regla temporal del destino, asignar y guardar.
    /// Con un solo camino, el endpoint genérico <c>PUT /estado</c> no puede
    /// saltearse lo que las operaciones con nombre propio verifican.
    /// </remarks>
    private async Task<ServiceResult<Consulta>> Transicionar(int id, EstadoConsulta destino)
    {
        var consulta = await _contexto.Consultas.FindAsync(id);
        if (consulta is null) return Falla.NoEncontrado(ConsultaInexistente);

        if (!CicloDeVidaConsulta.PuedeTransicionar(consulta.Estado, destino))
            return Falla.Conflicto(CicloDeVidaConsulta.ExplicarRechazo(consulta.Estado, destino));

        var rechazo = ReglaTemporalIncumplida(consulta, destino);
        if (rechazo is not null) return Falla.Conflicto(rechazo);

        consulta.Estado = destino;
        await _contexto.SaveChangesAsync();
        return ServiceResult.Ok(consulta);
    }

    /// <summary>
    /// Reglas que dependen del instante actual, por estado de destino.
    /// </summary>
    private string? ReglaTemporalIncumplida(Consulta consulta, EstadoConsulta destino) => destino switch
    {
        EstadoConsulta.Cancelado when consulta.FechaHora - Ahora < AnticipacionMinimaParaCancelar =>
            "No se puede cancelar con menos de 24 horas de anticipación.",

        EstadoConsulta.Inasistencia when consulta.FechaHora > Ahora =>
            "No se puede registrar una inasistencia sobre una consulta futura.",

        EstadoConsulta.Atendido when !consulta.FechaHora.IsInCurrentYear(Ahora) =>
            "Solo se pueden registrar atenciones de consultas del año en curso.",

        _ => null
    };

    private IQueryable<Consulta> ConsultasConRelaciones() => _contexto.Consultas
        .Include(c => c.Afiliado)
        .Include(c => c.Profesional);
}
