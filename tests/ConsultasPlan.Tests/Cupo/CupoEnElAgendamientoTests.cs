using ConsultasPlan.Contratos;
using ConsultasPlan.Data;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;

namespace ConsultasPlan.Tests.Cupo;

/// <summary>
/// La política aplicada sobre datos reales, al agendar.
/// </summary>
/// <remarks>
/// Lo que se verifica acá no es la regla —eso ya está en
/// <see cref="CupoNutricionPolicyTests"/> sin infraestructura— sino el armado:
/// que se busque el historial correcto, que se mire la especialidad por código
/// y que el rechazo llegue como conflicto.
/// </remarks>
public class CupoEnElAgendamientoTests
{
    private const string Nutricion = CodigosDeEspecialidad.Nutricion;

    private static DateTime Utc(int mes, int dia, int hora = 10) =>
        new(2026, mes, dia, hora, 0, 0, DateTimeKind.Utc);

    private static Task<ConsultasPlan.Servicios.ServiceResult<Consulta>> Agendar(
        AppDbContext db, Afiliado afiliado, Profesional profesional, DateTime fechaHora,
        ModalidadConsulta modalidad = ModalidadConsulta.Presencial) =>
        ServiciosDePrueba.Consultas(db).Crear(new CrearConsultaRequest
        {
            AfiliadoId = afiliado.Id,
            ProfesionalId = profesional.Id,
            FechaHora = fechaHora,
            Modalidad = modalidad,
            Motivo = "Seguimiento nutricional"
        });

    [Fact]
    public async Task Con_el_cupo_anual_agotado_no_se_puede_agendar()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var nutricionista = db.AgregarProfesional(Nutricion);

        // Seis presenciales atendidas, una por mes de enero a junio.
        for (var mes = 1; mes <= 6; mes++)
            db.AgregarConsulta(afiliado, nutricionista, Utc(mes, 10), EstadoConsulta.Atendido);

        var septima = Utc(8, 20);
        var resultado = await Agendar(db, afiliado, nutricionista, septima);

        AssertResultado.Conflicto(resultado);
        Assert.Contains("por año", resultado.Falla!.Detalle);
        Assert.Empty(db.Consultas.Where(c => c.FechaHora == septima));
    }

    [Fact]
    public async Task Con_una_consulta_en_el_mes_no_se_puede_agendar_otra_ese_mes()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var nutricionista = db.AgregarProfesional(Nutricion);
        db.AgregarConsulta(afiliado, nutricionista, Utc(8, 5), EstadoConsulta.Atendido);

        var resultado = await Agendar(db, afiliado, nutricionista, Utc(8, 25));

        AssertResultado.Conflicto(resultado);
        Assert.Contains("por mes", resultado.Falla!.Detalle);
    }

    // "No más de 1 por mes" y "no se acumulan": el mes siguiente trae su cupo,
    // sin arrastrar el del anterior.
    [Fact]
    public async Task Pero_si_se_puede_agendar_el_mes_siguiente()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var nutricionista = db.AgregarProfesional(Nutricion);
        db.AgregarConsulta(afiliado, nutricionista, Utc(8, 5), EstadoConsulta.Atendido);

        AssertResultado.Exito(await Agendar(db, afiliado, nutricionista, Utc(9, 5)));
    }

    // El catálogo es lo que hace que esto funcione: la política compara contra
    // el código NUT, no contra el texto de la especialidad.
    [Fact]
    public async Task Las_consultas_de_otra_especialidad_no_consumen_el_cupo()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var nutricionista = db.AgregarProfesional(Nutricion);
        var cardiologo = db.AgregarProfesional("CAR");

        for (var mes = 1; mes <= 6; mes++)
            db.AgregarConsulta(afiliado, cardiologo, Utc(mes, 10), EstadoConsulta.Atendido);

        AssertResultado.Exito(await Agendar(db, afiliado, nutricionista, Utc(8, 20)));
    }

    [Fact]
    public async Task Agendar_con_un_profesional_de_otra_especialidad_no_mira_el_cupo()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var nutricionista = db.AgregarProfesional(Nutricion);
        var cardiologo = db.AgregarProfesional("CAR");

        for (var mes = 1; mes <= 6; mes++)
            db.AgregarConsulta(afiliado, nutricionista, Utc(mes, 10), EstadoConsulta.Atendido);

        AssertResultado.Exito(await Agendar(db, afiliado, cardiologo, Utc(8, 20)));
    }

    [Fact]
    public async Task Una_virtual_se_puede_agendar_con_el_cupo_agotado()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var nutricionista = db.AgregarProfesional(Nutricion);

        for (var mes = 1; mes <= 6; mes++)
            db.AgregarConsulta(afiliado, nutricionista, Utc(mes, 10), EstadoConsulta.Atendido);

        AssertResultado.Exito(await Agendar(
            db, afiliado, nutricionista, Utc(8, 20), ModalidadConsulta.Virtual));
    }

    [Fact]
    public async Task Una_cancelada_libera_el_cupo_del_mes()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var nutricionista = db.AgregarProfesional(Nutricion);
        db.AgregarConsulta(afiliado, nutricionista, Utc(8, 5), EstadoConsulta.Cancelado);

        AssertResultado.Exito(await Agendar(db, afiliado, nutricionista, Utc(8, 25)));
    }

    // §5.5: el período que se evalúa es el de la fecha solicitada, no el de hoy.
    // El reloj de los tests está fijado en junio de 2026.
    [Fact]
    public async Task El_cupo_se_mide_contra_la_fecha_solicitada_y_no_contra_hoy()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var nutricionista = db.AgregarProfesional(Nutricion);

        // Una consulta en agosto. Hoy es junio: no toca el mes corriente.
        db.AgregarConsulta(afiliado, nutricionista, Utc(8, 5), EstadoConsulta.Atendido);

        // Agendar otra en agosto se rechaza, aunque el mes corriente esté libre.
        AssertResultado.Conflicto(await Agendar(db, afiliado, nutricionista, Utc(8, 25)));

        // Y en septiembre se acepta.
        AssertResultado.Exito(await Agendar(db, afiliado, nutricionista, Utc(9, 25)));
    }

    // El cupo se verifica antes de la superposición horaria: a un afiliado sin
    // cupo hay que decirle que no tiene cupo, no que el horario está ocupado.
    [Fact]
    public async Task El_cupo_se_informa_antes_que_el_horario_ocupado()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var otro = db.AgregarAfiliado("Otro Afiliado", dni: "30000009");
        var nutricionista = db.AgregarProfesional(Nutricion);

        var horario = Utc(8, 20);
        db.AgregarConsulta(otro, nutricionista, horario, EstadoConsulta.Confirmado);
        db.AgregarConsulta(afiliado, nutricionista, Utc(8, 5), EstadoConsulta.Atendido);

        var resultado = await Agendar(db, afiliado, nutricionista, horario);

        AssertResultado.Conflicto(resultado);
        Assert.Contains("por mes", resultado.Falla!.Detalle);
        Assert.DoesNotContain("horario", resultado.Falla.Detalle);
    }

    // La inasistencia consume (§5.4): la franja se ocupó igual.
    [Fact]
    public async Task Una_inasistencia_consume_el_cupo_del_mes()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var nutricionista = db.AgregarProfesional(Nutricion);
        db.AgregarConsulta(afiliado, nutricionista, Utc(8, 5), EstadoConsulta.Inasistencia);

        AssertResultado.Conflicto(await Agendar(db, afiliado, nutricionista, Utc(8, 25)));
    }

    [Fact]
    public async Task El_cupo_de_un_afiliado_no_afecta_al_de_otro()
    {
        using var db = ContextoDePrueba.Crear();
        var agotado = db.AgregarAfiliado();
        var nuevo = db.AgregarAfiliado("Afiliado Nuevo", dni: "30000008");
        var nutricionista = db.AgregarProfesional(Nutricion);

        for (var mes = 1; mes <= 6; mes++)
            db.AgregarConsulta(agotado, nutricionista, Utc(mes, 10), EstadoConsulta.Atendido);

        AssertResultado.Conflicto(await Agendar(db, agotado, nutricionista, Utc(8, 20)));
        AssertResultado.Exito(await Agendar(db, nuevo, nutricionista, Utc(8, 21)));
    }
}
