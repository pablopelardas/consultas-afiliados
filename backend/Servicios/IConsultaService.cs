using ConsultasPlan.Contratos;
using ConsultasPlan.Models;

namespace ConsultasPlan.Servicios;

/// <summary>
/// Operaciones de negocio sobre consultas.
/// </summary>
/// <remarks>
/// Devuelve entidades, no contratos de salida: qué campos se exponen y cómo se
/// anidan es una decisión de presentación y vive en el controlador. La entrada,
/// en cambio, reutiliza los contratos de <c>Contratos.Requests</c> porque son
/// exactamente la lista de parámetros de cada operación y nada más — no traen
/// identidad ni estado.
///
/// Las lecturas no devuelven <see cref="ServiceResult{T}"/> cuando no pueden
/// fallar. Un resultado que nunca trae falla obliga al consumidor a manejar un
/// caso imposible.
/// </remarks>
public interface IConsultaService
{
    Task<IReadOnlyList<Consulta>> Listar();

    Task<ServiceResult<Consulta>> Obtener(int id);

    Task<ServiceResult<Consulta>> Crear(CrearConsultaRequest request);

    Task<ServiceResult<Consulta>> Cancelar(int id);

    Task<ServiceResult<Consulta>> RegistrarInasistencia(int id);

    Task<ServiceResult<Consulta>> RegistrarAtencion(int id);

    /// <summary>
    /// Cambio de estado genérico, para el flujo operativo del staff.
    /// </summary>
    /// <remarks>
    /// Pasa por las mismas reglas que las operaciones con nombre propio. En el
    /// código base era la vía por la que se evadían todas.
    /// </remarks>
    Task<ServiceResult<Consulta>> CambiarEstado(int id, EstadoConsulta estadoDestino);
}
