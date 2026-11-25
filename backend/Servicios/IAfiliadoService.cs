using ConsultasPlan.Contratos;
using ConsultasPlan.Models;

namespace ConsultasPlan.Servicios;

/// <summary>
/// Operaciones de negocio sobre afiliados.
/// </summary>
public interface IAfiliadoService
{
    /// <summary>
    /// Afiliados vigentes. Los dados de baja quedan excluidos.
    /// </summary>
    /// <remarks>
    /// El filtro es explícito y no un <c>HasQueryFilter</c> global, a propósito.
    /// Un filtro global también alcanzaría a <c>Include(c => c.Afiliado)</c> al
    /// leer consultas, dejando sin paciente al historial clínico de alguien dado
    /// de baja — justamente la integridad que la baja lógica viene a proteger.
    /// </remarks>
    Task<IReadOnlyList<Afiliado>> ListarVigentes();

    Task<ServiceResult<Afiliado>> Obtener(int id);

    Task<ServiceResult<Afiliado>> Crear(CrearAfiliadoRequest request);

    Task<ServiceResult<Afiliado>> Actualizar(int id, ActualizarAfiliadoRequest request);

    /// <summary>
    /// Baja lógica: conserva el registro y su historial.
    /// </summary>
    Task<ServiceResult> DarDeBaja(int id);

    /// <summary>
    /// Turnos a futuro que siguen agendados, por afiliado.
    /// </summary>
    /// <remarks>
    /// La baja no los cancela, y no debería: se revierte con
    /// <see cref="Reactivar"/> mientras una cancelación es terminal, así que
    /// cancelar en cascada convertiría un error administrativo en pérdida
    /// irrecuperable. Lo que falta es que quien da de baja sepa cuántos deja en
    /// pie —el afiliado desaparece de las vistas y el turno no—.
    /// </remarks>
    Task<IReadOnlyDictionary<int, int>> ConsultasFuturasAgendadas(
        IReadOnlyCollection<int> afiliadoIds);

    Task<ServiceResult<Afiliado>> Reactivar(int id);
}
