using ConsultasPlan.Models;

namespace ConsultasPlan.Dominio.Cupo;

/// <summary>
/// Lo que se le pide decidir a la política.
/// </summary>
/// <param name="Modalidad">Modalidad de la consulta que se quiere agendar.</param>
/// <param name="HistorialDeNutricion">
/// Consultas del afiliado en la especialidad alcanzada, ya acotadas al período
/// anual. Vienen sin filtrar por estado ni modalidad a propósito: cuáles
/// consumen es la regla, y la regla vive acá.
/// </param>
public sealed record SolicitudDeCupo(
    ModalidadConsulta Modalidad,
    IReadOnlyCollection<Consulta> HistorialDeNutricion,
    PeriodosDelCupo Periodos);

/// <summary>
/// Cuánto cupo hay consumido y cuánto queda.
/// </summary>
public sealed record EstadoDelCupo(
    int LimiteAnual,
    int UsadasEnElAno,
    int DisponiblesEnElAno,
    int LimiteMensual,
    int UsadasEnElMes,
    int DisponiblesEnElMes,
    Periodo Anual,
    Periodo Mensual);

/// <param name="Motivo">
/// Explicación para el usuario final cuando no se permite. Es parte del
/// entregable: una política que rechaza sin decir por qué ni hasta cuándo
/// obliga a adivinar.
/// </param>
public sealed record DecisionDeCupo(bool Permitida, string? Motivo, EstadoDelCupo Estado);

/// <summary>
/// Política de cupo de consultas de nutrición.
/// </summary>
/// <remarks>
/// Función pura: no consulta la base de datos, no lee el reloj y no produce
/// efectos. Recibe el historial y los períodos ya resueltos, y devuelve una
/// decisión.
///
/// Eso es lo que la hace verificable sin infraestructura: los casos de borde
/// —el sexto del año, el séptimo, el segundo del mes— se escriben como datos de
/// entrada en lugar de como estado de base de datos.
/// </remarks>
public interface ICupoNutricionPolicy
{
    /// <summary>Código de la especialidad que esta política gobierna.</summary>
    string CodigoEspecialidad { get; }

    DecisionDeCupo Evaluar(SolicitudDeCupo solicitud);
}
