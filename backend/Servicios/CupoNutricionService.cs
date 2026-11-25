using ConsultasPlan.Data;
using ConsultasPlan.Dominio.Cupo;
using ConsultasPlan.Models;
using Microsoft.EntityFrameworkCore;

namespace ConsultasPlan.Servicios;

public class CupoNutricionService : ICupoNutricionService
{
    private readonly AppDbContext _contexto;
    private readonly ICupoPolicy _politica;
    private readonly IPeriodoCupoProvider _periodos;
    private readonly TimeProvider _reloj;

    public CupoNutricionService(
        AppDbContext contexto,
        ICupoPolicy politica,
        IPeriodoCupoProvider periodos,
        TimeProvider reloj)
    {
        _contexto = contexto;
        _politica = politica;
        _periodos = periodos;
        _reloj = reloj;
    }

    public async Task<Falla?> VerificarAntesDeAgendar(
        Afiliado afiliado, Profesional profesional, DateTime fechaHora, ModalidadConsulta modalidad)
    {
        if (profesional.Especialidad?.Codigo != _politica.CodigoEspecialidad) return null;

        var periodos = _periodos.Para(fechaHora, afiliado);
        var historial = await HistorialEnElPeriodo(afiliado.Id, periodos.Anual);

        var decision = _politica.Evaluar(new SolicitudDeCupo(modalidad, historial, periodos));

        return decision.Permitida ? null : Falla.Conflicto(decision.Motivo!);
    }

    public async Task<ServiceResult<EstadoDelCupo>> ConsultarEstado(int afiliadoId)
    {
        var afiliado = await _contexto.Afiliados
            .FirstOrDefaultAsync(a => a.Id == afiliadoId && a.Activo);
        if (afiliado is null) return Falla.NoEncontrado("El afiliado indicado no existe.");

        // Sin fecha pedida, el período es el que corre hoy.
        var periodos = _periodos.Para(_reloj.GetUtcNow().UtcDateTime, afiliado);
        var historial = await HistorialEnElPeriodo(afiliado.Id, periodos.Anual);

        var decision = _politica.Evaluar(
            new SolicitudDeCupo(ModalidadConsulta.Presencial, historial, periodos));

        return ServiceResult.Ok(decision.Estado);
    }

    /// <summary>
    /// Consultas del afiliado en la especialidad alcanzada, dentro del año.
    /// </summary>
    /// <remarks>
    /// La consulta acota por afiliado, especialidad y período —tres criterios
    /// que no son decisiones de negocio— y nada más. Qué estados y qué
    /// modalidades consumen cupo lo decide la política: si ese filtro estuviera
    /// acá, la regla viviría en un <c>Where</c> y el test tendría que levantar
    /// una base para verificarla.
    ///
    /// El período mensual está contenido en el anual, así que un solo rango
    /// alcanza para ambos.
    /// </remarks>
    private async Task<IReadOnlyCollection<Consulta>> HistorialEnElPeriodo(int afiliadoId, Periodo anual) =>
        await _contexto.Consultas
            .Where(c => c.AfiliadoId == afiliadoId
                     && c.Profesional!.Especialidad!.Codigo == _politica.CodigoEspecialidad
                     && c.FechaHora >= anual.Desde
                     && c.FechaHora < anual.Hasta)
            .ToListAsync();
}
