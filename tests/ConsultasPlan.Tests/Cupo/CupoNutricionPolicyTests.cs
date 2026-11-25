using ConsultasPlan.Dominio.Cupo;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;

namespace ConsultasPlan.Tests.Cupo;

/// <summary>
/// La política de cupo, sin infraestructura.
/// </summary>
/// <remarks>
/// Ni base de datos, ni reloj, ni pipeline: los períodos se escriben literales
/// en el test, lo que además demuestra que la política no conoce el calendario.
/// Los casos de borde —el sexto del año, el séptimo, el segundo del mes— son
/// datos de entrada en lugar de estado a sembrar.
/// </remarks>
public class CupoNutricionPolicyTests
{
    private static DateTime Utc(int ano, int mes, int dia) =>
        new(ano, mes, dia, 10, 0, 0, DateTimeKind.Utc);

    private static readonly PeriodosDelCupo Junio2026 = new(
        Anual: new Periodo(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                           new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        Mensual: new Periodo(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                             new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)));

    private static Consulta C(
        DateTime fecha,
        EstadoConsulta estado = EstadoConsulta.Atendido,
        ModalidadConsulta modalidad = ModalidadConsulta.Presencial) =>
        new() { FechaHora = fecha, Estado = estado, Modalidad = modalidad };

    private static DecisionDeCupo Evaluar(
        IEnumerable<Consulta> historial,
        ModalidadConsulta modalidad = ModalidadConsulta.Presencial,
        int porAno = 6,
        int porMes = 1) =>
        ServiciosDePrueba.Politica(porAno, porMes)
            .Evaluar(new SolicitudDeCupo(modalidad, historial.ToList(), Junio2026));

    [Fact]
    public void Sin_historial_se_puede_agendar()
    {
        var decision = Evaluar([]);

        Assert.True(decision.Permitida);
        Assert.Null(decision.Motivo);
        Assert.Equal(0, decision.Estado.UsadasEnElAno);
        Assert.Equal(6, decision.Estado.DisponiblesEnElAno);
    }

    // Cinco consultas repartidas en meses anteriores: queda cupo anual y el mes
    // está libre.
    [Fact]
    public void Con_cinco_en_el_ano_y_el_mes_libre_se_puede_agendar()
    {
        var decision = Evaluar([
            C(Utc(2026, 1, 10)), C(Utc(2026, 2, 10)), C(Utc(2026, 3, 10)),
            C(Utc(2026, 4, 10)), C(Utc(2026, 5, 10))
        ]);

        Assert.True(decision.Permitida);
        Assert.Equal(5, decision.Estado.UsadasEnElAno);
        Assert.Equal(1, decision.Estado.DisponiblesEnElAno);
        Assert.Equal(0, decision.Estado.UsadasEnElMes);
    }

    [Fact]
    public void Con_seis_en_el_ano_no_se_puede_agendar_la_septima()
    {
        var decision = Evaluar([
            C(Utc(2026, 1, 10)), C(Utc(2026, 2, 10)), C(Utc(2026, 3, 10)),
            C(Utc(2026, 4, 10)), C(Utc(2026, 5, 10)), C(Utc(2026, 7, 10))
        ]);

        Assert.False(decision.Permitida);
        Assert.Equal(6, decision.Estado.UsadasEnElAno);
        Assert.Equal(0, decision.Estado.DisponiblesEnElAno);
    }

    [Fact]
    public void Con_una_en_el_mes_no_se_puede_agendar_otra_el_mismo_mes()
    {
        var decision = Evaluar([C(Utc(2026, 6, 3))]);

        Assert.False(decision.Permitida);
        Assert.Equal(1, decision.Estado.UsadasEnElMes);
        Assert.Equal(0, decision.Estado.DisponiblesEnElMes);

        // Pero el cupo anual sigue casi entero: el límite que frena es el mensual.
        Assert.Equal(5, decision.Estado.DisponiblesEnElAno);
    }

    // §5.3: una consulta cancelada liberó el cupo y liberó la franja horaria.
    [Fact]
    public void Una_consulta_cancelada_no_consume()
    {
        var decision = Evaluar([C(Utc(2026, 6, 3), EstadoConsulta.Cancelado)]);

        Assert.True(decision.Permitida);
        Assert.Equal(0, decision.Estado.UsadasEnElAno);
    }

    // §5.3: el cupo se reserva al agendar. Si lo pendiente no consumiera, un
    // afiliado podría reservar seis turnos sin consumir nada.
    [Theory]
    [InlineData(EstadoConsulta.Pendiente)]
    [InlineData(EstadoConsulta.Confirmado)]
    [InlineData(EstadoConsulta.Atendido)]
    [InlineData(EstadoConsulta.Inasistencia)]
    public void Los_demas_estados_consumen(EstadoConsulta estado)
    {
        var decision = Evaluar([C(Utc(2026, 6, 3), estado)]);

        Assert.Equal(1, decision.Estado.UsadasEnElAno);
        Assert.False(decision.Permitida);
    }

    // §5.6: "aplica a consultas presenciales". Si una virtual consumiera cupo
    // sin estar limitada, el afiliado perdería cupo presencial por una
    // prestación que la política declara fuera de su alcance.
    [Fact]
    public void Una_consulta_virtual_no_consume()
    {
        var decision = Evaluar([C(Utc(2026, 6, 3), modalidad: ModalidadConsulta.Virtual)]);

        Assert.True(decision.Permitida);
        Assert.Equal(0, decision.Estado.UsadasEnElAno);
    }

    [Fact]
    public void Una_virtual_se_puede_agendar_con_el_ano_agotado()
    {
        var seisPresenciales = Enumerable.Range(1, 6).Select(m => C(Utc(2026, m, 10)));

        var decision = Evaluar(seisPresenciales, modalidad: ModalidadConsulta.Virtual);

        Assert.True(decision.Permitida);

        // El cupo igual se informa: está agotado, pero no aplica a esta consulta.
        Assert.Equal(0, decision.Estado.DisponiblesEnElAno);
    }

    [Fact]
    public void Las_consultas_de_otro_ano_no_cuentan()
    {
        var decision = Evaluar([C(Utc(2025, 12, 20)), C(Utc(2027, 1, 5))]);

        Assert.True(decision.Permitida);
        Assert.Equal(0, decision.Estado.UsadasEnElAno);
    }

    // El intervalo es semiabierto: el primer instante del período cuenta, el
    // primero del siguiente no. Sin este borde fijado, una consulta a
    // medianoche del 1 de enero podría contarse en los dos años o en ninguno.
    [Fact]
    public void El_primer_instante_del_periodo_cuenta_y_el_del_siguiente_no()
    {
        var primerInstante = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var primerInstanteDelSiguiente = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(1, Evaluar([C(primerInstante)]).Estado.UsadasEnElMes);
        Assert.Equal(0, Evaluar([C(primerInstanteDelSiguiente)]).Estado.UsadasEnElMes);

        // La del 1 de julio sigue siendo del año, aunque no del mes.
        Assert.Equal(1, Evaluar([C(primerInstanteDelSiguiente)]).Estado.UsadasEnElAno);
    }

    // §5.10: el límite es configuración. Probar el borde del año no exige
    // sembrar seis consultas.
    [Fact]
    public void El_limite_anual_es_configurable()
    {
        var historial = new[] { C(Utc(2026, 1, 10)), C(Utc(2026, 2, 10)) };

        Assert.False(Evaluar(historial, porAno: 2).Permitida);
        Assert.True(Evaluar(historial, porAno: 3).Permitida);
    }

    [Fact]
    public void El_limite_mensual_es_configurable()
    {
        var historial = new[] { C(Utc(2026, 6, 3)) };

        Assert.False(Evaluar(historial, porMes: 1).Permitida);
        Assert.True(Evaluar(historial, porMes: 2).Permitida);
    }

    // Si el año está agotado, el mensaje mensual ("la próxima a partir del
    // 01/07") sería una promesa falsa.
    [Fact]
    public void Con_los_dos_limites_excedidos_manda_el_anual()
    {
        var seisIncluidaUnaDeEsteMes = Enumerable.Range(1, 6).Select(m => C(Utc(2026, m, 10)));

        var decision = Evaluar(seisIncluidaUnaDeEsteMes);

        Assert.False(decision.Permitida);
        Assert.Contains("por año", decision.Motivo);
        Assert.DoesNotContain("por mes", decision.Motivo);
    }

    // El mensaje es parte del entregable: una política que rechaza sin decir
    // por qué ni hasta cuándo obliga al usuario a adivinar.
    [Fact]
    public void El_motivo_anual_dice_el_periodo_y_cuando_se_renueva()
    {
        var motivo = Evaluar(Enumerable.Range(1, 6).Select(m => C(Utc(2026, m, 10)))).Motivo;

        Assert.Contains("6 consultas de nutrición por año", motivo);
        Assert.Contains("01/01/2026", motivo);
        Assert.Contains("31/12/2026", motivo);
        Assert.Contains("no se acumulan", motivo);
        Assert.Contains("01/01/2027", motivo);
    }

    [Fact]
    public void El_motivo_mensual_dice_desde_cuando_se_puede_y_cuanto_queda_en_el_ano()
    {
        var motivo = Evaluar([C(Utc(2026, 6, 3))]).Motivo;

        Assert.Contains("1 consulta de nutrición por mes", motivo);
        Assert.Contains("a partir del 01/07/2026", motivo);
        Assert.Contains("quedan 5 consultas en el año", motivo);
    }
}
