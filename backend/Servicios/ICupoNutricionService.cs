using ConsultasPlan.Dominio.Cupo;
using ConsultasPlan.Models;

namespace ConsultasPlan.Servicios;

/// <summary>
/// Aplica la política de cupo sobre datos reales.
/// </summary>
/// <remarks>
/// La división de trabajo es deliberada:
/// <list type="bullet">
/// <item><see cref="ICupoNutricionPolicy"/> decide. Es pura.</item>
/// <item>Este servicio busca lo que la política necesita para decidir.</item>
/// </list>
/// Sin esta separación la regla queda enredada con la consulta a la base y hay
/// que montar un esquema para verificarla.
/// </remarks>
public interface ICupoNutricionService
{
    /// <summary>
    /// Verifica el cupo antes de agendar.
    /// </summary>
    /// <returns>
    /// La falla a devolver si no se permite, o <c>null</c> si se permite —
    /// también cuando la especialidad del profesional no está alcanzada.
    /// </returns>
    Task<Falla?> VerificarAntesDeAgendar(
        Afiliado afiliado, Profesional profesional, DateTime fechaHora, ModalidadConsulta modalidad);

    /// <summary>
    /// Estado del cupo de un afiliado, para mostrarlo antes de que intente
    /// agendar. Una política que solo se manifiesta al ser rechazada obliga al
    /// usuario a descubrirla por error.
    /// </summary>
    Task<ServiceResult<EstadoDelCupo>> ConsultarEstado(int afiliadoId);
}
