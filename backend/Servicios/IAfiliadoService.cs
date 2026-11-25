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

    Task<ServiceResult<Afiliado>> Reactivar(int id);
}
