using ConsultasPlan.Dominio;
using ConsultasPlan.Dominio.Cupo;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;

namespace ConsultasPlan.Tests.Cupo;

/// <summary>
/// Por qué el cupo solo hace falta verificarlo al agendar.
/// </summary>
/// <remarks>
/// La política se aplica en un único punto: al crear la consulta. Eso solo es
/// correcto si ninguna transición de estado puede hacer que una consulta que no
/// consumía cupo empiece a consumirlo — porque ahí habría que revalidar, y no se
/// revalida.
///
/// La propiedad se verifica contra las dos reglas a la vez: la tabla de
/// transiciones de <see cref="CicloDeVidaConsulta"/> y el predicado de consumo de
/// la política. Si alguien agrega una transición o cambia qué consume, este test
/// avisa — que es exactamente cuando hay que decidir si el cupo necesita
/// revalidarse.
/// </remarks>
public class InvarianteDeConsumoTests
{
    private static readonly PeriodosDelCupo Periodos = new(
        Anual: new Periodo(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                           new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        Mensual: new Periodo(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                             new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)));

    /// <summary>
    /// Si una consulta presencial en ese estado cuenta como usada.
    /// </summary>
    /// <remarks>
    /// Se pregunta a través de la decisión de la política en lugar de exponer el
    /// predicado: la respuesta es la que el sistema usa, no una copia del test.
    /// </remarks>
    private static bool Consume(EstadoConsulta estado)
    {
        var consulta = new Consulta
        {
            FechaHora = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc),
            Estado = estado,
            Modalidad = ModalidadConsulta.Presencial
        };

        var decision = ServiciosDePrueba.Politica().Evaluar(
            new SolicitudDeCupo(ModalidadConsulta.Presencial, [consulta], Periodos));

        return decision.Estado.UsadasEnElAno == 1;
    }

    [Fact]
    public void Ninguna_transicion_legal_hace_que_una_consulta_empiece_a_consumir_cupo()
    {
        var transicionesRevisadas = 0;

        foreach (var desde in Enum.GetValues<EstadoConsulta>())
        {
            if (Consume(desde)) continue;

            foreach (var hacia in CicloDeVidaConsulta.TransicionesDesde(desde))
            {
                transicionesRevisadas++;
                Assert.False(Consume(hacia),
                    $"La transición {desde} -> {hacia} haría consumir cupo a una consulta " +
                    "que no consumía, así que el cupo tendría que revalidarse ahí.");
            }
        }

        // El único estado que no consume es Cancelado, y es terminal: por eso no
        // hay transiciones que revisar. Si alguien lo volviera reversible, el
        // conteo deja de ser cero y el assert de arriba lo frena.
        Assert.Equal(0, transicionesRevisadas);
        Assert.False(Consume(EstadoConsulta.Cancelado));
        Assert.True(CicloDeVidaConsulta.EsTerminal(EstadoConsulta.Cancelado));
    }

    // La otra mitad de la invariante: cancelar libera cupo, y eso no necesita
    // verificación porque relaja la restricción en lugar de apretarla.
    [Fact]
    public void Cancelar_es_la_unica_transicion_que_cambia_el_consumo()
    {
        var queCambian = new List<string>();

        foreach (var desde in Enum.GetValues<EstadoConsulta>())
            foreach (var hacia in CicloDeVidaConsulta.TransicionesDesde(desde))
                if (Consume(desde) != Consume(hacia))
                    queCambian.Add($"{desde} -> {hacia}");

        Assert.All(queCambian, t => Assert.EndsWith("-> Cancelado", t));
    }
}
