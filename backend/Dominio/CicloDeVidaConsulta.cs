using ConsultasPlan.Models;

namespace ConsultasPlan.Dominio;

/// <summary>
/// Transiciones de estado admitidas para una consulta.
/// </summary>
/// <remarks>
/// Regla pura: no accede a la base, no produce efectos y no depende del reloj.
/// Recibe dos estados y responde si el paso entre ellos existe en el dominio.
/// Toda la tabla se puede verificar sin levantar infraestructura.
///
/// El código base no tenía esta noción: cada endpoint asignaba el estado
/// directamente, de modo que se podía registrar una inasistencia sobre una
/// consulta cancelada o devolver a Pendiente una ya atendida.
/// </remarks>
public static class CicloDeVidaConsulta
{
    private static readonly IReadOnlyDictionary<EstadoConsulta, EstadoConsulta[]> Transiciones =
        new Dictionary<EstadoConsulta, EstadoConsulta[]>
        {
            // El turno existe y todavía no ocurrió. El afiliado puede no llegar
            // a confirmar y aun así ser atendido.
            [EstadoConsulta.Pendiente] = new[]
            {
                EstadoConsulta.Confirmado,
                EstadoConsulta.Cancelado,
                EstadoConsulta.Atendido,
                EstadoConsulta.Inasistencia
            },

            // Confirmado no vuelve a Pendiente: retroceder no describe ningún
            // hecho ocurrido.
            [EstadoConsulta.Confirmado] = new[]
            {
                EstadoConsulta.Cancelado,
                EstadoConsulta.Atendido,
                EstadoConsulta.Inasistencia
            },

            [EstadoConsulta.Atendido] = Array.Empty<EstadoConsulta>(),

            // Terminal. Una cancelación liberó el cupo del afiliado y la franja
            // del profesional, que otro pudo haber tomado. Reactivarla saltearía
            // esas validaciones; agendar de nuevo crea una consulta que las
            // vuelve a atravesar todas.
            [EstadoConsulta.Cancelado] = Array.Empty<EstadoConsulta>(),

            // Única corrección admitida: una inasistencia mal cargada perjudica
            // al afiliado —consume cupo y puede derivar en bloqueo—, así que no
            // puede ser irreversible.
            [EstadoConsulta.Inasistencia] = new[] { EstadoConsulta.Atendido }
        };

    public static bool PuedeTransicionar(EstadoConsulta desde, EstadoConsulta hacia) =>
        Transiciones.TryGetValue(desde, out var permitidas) && permitidas.Contains(hacia);

    public static bool EsTerminal(EstadoConsulta estado) =>
        TransicionesDesde(estado).Count == 0;

    /// <summary>
    /// Estados en los que un turno a futuro sigue ocupando la franja del
    /// profesional.
    /// </summary>
    /// <remarks>
    /// Inasistencia no entra aunque no sea terminal: no se puede marcar sobre
    /// una consulta que todavía no ocurrió. La lista es explícita porque una
    /// consulta de EF la tiene que traducir a SQL.
    /// </remarks>
    public static readonly EstadoConsulta[] OcupanAgenda =
    {
        EstadoConsulta.Pendiente,
        EstadoConsulta.Confirmado
    };

    public static IReadOnlyCollection<EstadoConsulta> TransicionesDesde(EstadoConsulta estado) =>
        Transiciones.TryGetValue(estado, out var permitidas)
            ? permitidas
            : Array.Empty<EstadoConsulta>();

    /// <summary>
    /// Explica por qué una transición no es posible, en términos que le sirvan
    /// a quien opera el sistema.
    /// </summary>
    public static string ExplicarRechazo(EstadoConsulta desde, EstadoConsulta hacia)
    {
        if (desde == hacia)
            return $"La consulta ya se encuentra en estado {desde}.";

        if (EsTerminal(desde))
            return $"Una consulta en estado {desde} no admite cambios de estado.";

        var admitidos = string.Join(", ", TransicionesDesde(desde));
        return $"No se puede pasar de {desde} a {hacia}. Estados admitidos: {admitidos}.";
    }
}
