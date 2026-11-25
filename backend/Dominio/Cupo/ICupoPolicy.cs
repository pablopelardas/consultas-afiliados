using ConsultasPlan.Models;

namespace ConsultasPlan.Dominio.Cupo;

/// <summary>
/// Lo que se le pide decidir a la política.
/// </summary>
/// <param name="Modalidad">Modalidad de la consulta que se quiere agendar.</param>
/// <param name="Historial">
/// Consultas del afiliado en la especialidad alcanzada, ya acotadas al período
/// anual. Vienen sin filtrar por estado ni modalidad a propósito: cuáles
/// consumen es la regla, y la regla vive acá.
/// </param>
public sealed record SolicitudDeCupo(
    ModalidadConsulta Modalidad,
    IReadOnlyCollection<Consulta> Historial,
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
/// Límite de consultas que un plan impone sobre una especialidad.
/// </summary>
/// <remarks>
/// Función pura: no consulta la base, no lee el reloj y no produce efectos.
/// Recibe el historial y los períodos ya resueltos, así que los casos de borde
/// —el sexto del año, el segundo del mes— se escriben como datos de entrada en
/// lugar de como estado de base de datos.
/// </remarks>
public interface ICupoPolicy
{
    /// <summary>
    /// Especialidad que esta política gobierna. Es la costura de despacho: con
    /// más de una política, se elige la que coincide con la del profesional.
    /// </summary>
    string CodigoEspecialidad { get; }

    DecisionDeCupo Evaluar(SolicitudDeCupo solicitud);
}
