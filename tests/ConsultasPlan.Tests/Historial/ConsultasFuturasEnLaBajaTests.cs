using ConsultasPlan.Dominio;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;

namespace ConsultasPlan.Tests.Historial;

/// <summary>
/// Turnos a futuro de un afiliado que se da de baja.
/// </summary>
/// <remarks>
/// La baja deja los turnos agendados y el afiliado desaparece de todas las
/// vistas, así que nada permitía descubrir que una baja dejó turnos en pie.
/// Estos tests fijan las dos mitades: que no se cancelan en cascada, y que el
/// conteo que lo hace visible cuenta lo que tiene que contar.
/// </remarks>
public class ConsultasFuturasEnLaBajaTests
{
    private static readonly DateTime Futuro = Reloj.Ahora.AddDays(10);
    private static readonly DateTime Pasado = Reloj.Ahora.AddDays(-10);

    /// <remarks>
    /// Cancelar en cascada parece lo correcto y no lo es: la baja se revierte
    /// con <c>Reactivar</c> y una cancelación es terminal (§ciclo de vida), así
    /// que una baja equivocada destruiría turnos sin forma de recuperarlos.
    /// </remarks>
    [Fact]
    public async Task La_baja_no_cancela_los_turnos_futuros()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        var turno = db.AgregarConsulta(afiliado, profesional, Futuro);

        await ServiciosDePrueba.Afiliados(db).DarDeBaja(afiliado.Id);

        var persistido = await db.Consultas.FindAsync(turno.Id);
        Assert.Equal(EstadoConsulta.Pendiente, persistido!.Estado);
    }

    [Fact]
    public async Task Reactivar_al_afiliado_le_devuelve_sus_turnos_intactos()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliados = ServiciosDePrueba.Afiliados(db);
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        db.AgregarConsulta(afiliado, profesional, Futuro, EstadoConsulta.Confirmado);

        await afiliados.DarDeBaja(afiliado.Id);
        await afiliados.Reactivar(afiliado.Id);

        Assert.Equal(1, await Contar(db, afiliado));
    }

    [Theory]
    [InlineData(EstadoConsulta.Pendiente, 1)]
    [InlineData(EstadoConsulta.Confirmado, 1)]
    [InlineData(EstadoConsulta.Cancelado, 0)]
    [InlineData(EstadoConsulta.Atendido, 0)]
    [InlineData(EstadoConsulta.Inasistencia, 0)]
    public async Task Solo_cuentan_los_estados_que_ocupan_la_agenda(
        EstadoConsulta estado, int esperado)
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        db.AgregarConsulta(afiliado, profesional, Futuro, estado);

        Assert.Equal(esperado, await Contar(db, afiliado));
    }

    // El turno que ya pasó no ocupa nada: no es lo que una baja deja en el aire.
    [Fact]
    public async Task Un_turno_pasado_no_cuenta_aunque_siga_pendiente()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        db.AgregarConsulta(afiliado, profesional, Pasado);

        Assert.Equal(0, await Contar(db, afiliado));
    }

    [Fact]
    public async Task El_conteo_no_mezcla_los_turnos_de_otros_afiliados()
    {
        using var db = ContextoDePrueba.Crear();
        var profesional = db.AgregarProfesional();
        var uno = db.AgregarAfiliado("Uno", dni: "30000001");
        var otro = db.AgregarAfiliado("Otro", dni: "30000002");
        db.AgregarConsulta(uno, profesional, Futuro);
        db.AgregarConsulta(uno, profesional, Futuro.AddDays(1));
        db.AgregarConsulta(otro, profesional, Futuro);

        var conteo = await ServiciosDePrueba.Afiliados(db)
            .ConsultasFuturasAgendadas(new[] { uno.Id, otro.Id });

        Assert.Equal(2, conteo[uno.Id]);
        Assert.Equal(1, conteo[otro.Id]);
    }

    // Un afiliado sin turnos no aparece en el diccionario: quien lo use tiene
    // que resolver la ausencia como cero y no como dato faltante.
    [Fact]
    public async Task Un_afiliado_sin_turnos_futuros_no_aparece_en_el_conteo()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();

        var conteo = await ServiciosDePrueba.Afiliados(db)
            .ConsultasFuturasAgendadas(new[] { afiliado.Id });

        Assert.False(conteo.ContainsKey(afiliado.Id));
    }

    [Fact]
    public async Task Sin_afiliados_no_se_consulta_la_base()
    {
        using var db = ContextoDePrueba.Crear();

        var conteo = await ServiciosDePrueba.Afiliados(db)
            .ConsultasFuturasAgendadas(Array.Empty<int>());

        Assert.Empty(conteo);
    }

    /// <remarks>
    /// Fija la lista contra la tabla de transiciones: un estado terminal no
    /// puede ocupar agenda, porque el turno ya tuvo su desenlace.
    /// </remarks>
    [Fact]
    public void Ningun_estado_que_ocupa_agenda_es_terminal()
    {
        Assert.All(CicloDeVidaConsulta.OcupanAgenda,
            estado => Assert.False(CicloDeVidaConsulta.EsTerminal(estado)));
    }

    private static async Task<int> Contar(
        ConsultasPlan.Data.AppDbContext db, Afiliado afiliado)
    {
        var conteo = await ServiciosDePrueba.Afiliados(db)
            .ConsultasFuturasAgendadas(new[] { afiliado.Id });

        return conteo.GetValueOrDefault(afiliado.Id);
    }
}
