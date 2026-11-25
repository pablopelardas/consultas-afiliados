using ConsultasPlan.Models;
using Microsoft.Extensions.Options;

namespace ConsultasPlan.Dominio.Cupo;

public class CupoNutricionPolicy : ICupoNutricionPolicy
{
    private readonly CupoNutricionOptions _opciones;

    public CupoNutricionPolicy(IOptions<CupoNutricionOptions> opciones) =>
        _opciones = opciones.Value;

    public string CodigoEspecialidad => _opciones.CodigoEspecialidad;

    public DecisionDeCupo Evaluar(SolicitudDeCupo solicitud)
    {
        var estado = Calcular(solicitud);

        // La modalidad virtual no se limita ni consume: el enunciado dice que la
        // política "aplica a consultas presenciales". Si una virtual consumiera
        // cupo sin estar limitada, el afiliado perdería cupo presencial por una
        // prestación que la política declara fuera de su alcance.
        if (solicitud.Modalidad != ModalidadConsulta.Presencial)
            return new DecisionDeCupo(true, null, estado);

        // El anual se verifica primero: si el año está agotado, el mensaje
        // mensual ("la próxima a partir del 01/12") sería una promesa falsa.
        if (estado.DisponiblesEnElAno <= 0)
            return new DecisionDeCupo(false, MotivoAnual(estado), estado);

        if (estado.DisponiblesEnElMes <= 0)
            return new DecisionDeCupo(false, MotivoMensual(estado), estado);

        return new DecisionDeCupo(true, null, estado);
    }

    private EstadoDelCupo Calcular(SolicitudDeCupo solicitud)
    {
        var consumidas = solicitud.HistorialDeNutricion.Where(Consume).ToList();

        var enElAno = consumidas.Count(c => solicitud.Periodos.Anual.Contiene(c.FechaHora));
        var enElMes = consumidas.Count(c => solicitud.Periodos.Mensual.Contiene(c.FechaHora));

        return new EstadoDelCupo(
            LimiteAnual: _opciones.ConsultasPorAno,
            UsadasEnElAno: enElAno,
            DisponiblesEnElAno: Math.Max(0, _opciones.ConsultasPorAno - enElAno),
            LimiteMensual: _opciones.ConsultasPorMes,
            UsadasEnElMes: enElMes,
            DisponiblesEnElMes: Math.Max(0, _opciones.ConsultasPorMes - enElMes),
            Anual: solicitud.Periodos.Anual,
            Mensual: solicitud.Periodos.Mensual);
    }

    private static bool Consume(Consulta consulta) =>
        consulta.Modalidad == ModalidadConsulta.Presencial && ElEstadoConsume(consulta.Estado);

    /// <remarks>
    /// Sin rama <c>default</c> permisiva: si mañana se agrega un estado, esto
    /// lanza en el primer test en lugar de decidir en silencio que no consume.
    /// Un estado nuevo que no consume cupo por omisión es un agujero en la
    /// política que nadie ve hasta que alguien lo usa.
    /// </remarks>
    private static bool ElEstadoConsume(EstadoConsulta estado) => estado switch
    {
        // El cupo se reserva al agendar: si una consulta pendiente no consumiera,
        // un afiliado podría reservar seis turnos sin consumir nada.
        EstadoConsulta.Pendiente => true,
        EstadoConsulta.Confirmado => true,
        EstadoConsulta.Atendido => true,

        // La consulta ocupó una franja de la agenda que nadie más pudo usar. La
        // política protege un recurso escaso y el recurso se consumió.
        EstadoConsulta.Inasistencia => true,

        // Liberó el cupo y liberó la franja horaria.
        EstadoConsulta.Cancelado => false,

        _ => throw new ArgumentOutOfRangeException(
            nameof(estado), estado, "Estado sin decisión de consumo de cupo.")
    };

    private string MotivoAnual(EstadoDelCupo estado) =>
        $"El plan incluye {_opciones.ConsultasPorAno} " +
        $"{Consultas(_opciones.ConsultasPorAno)} de nutrición por año y el afiliado " +
        $"ya tiene {estado.UsadasEnElAno} entre el {estado.Anual.DesdeTexto} y el " +
        $"{estado.Anual.UltimoDiaTexto}. Las consultas no utilizadas no se acumulan, " +
        $"así que el cupo se renueva el {estado.Anual.ProximoInicioTexto}.";

    private string MotivoMensual(EstadoDelCupo estado) =>
        $"El plan permite {_opciones.ConsultasPorMes} " +
        $"{Consultas(_opciones.ConsultasPorMes)} de nutrición por mes y el afiliado " +
        $"ya tiene {estado.UsadasEnElMes} entre el {estado.Mensual.DesdeTexto} y el " +
        $"{estado.Mensual.UltimoDiaTexto}. La próxima se puede agendar a partir del " +
        $"{estado.Mensual.ProximoInicioTexto}, y quedan {estado.DisponiblesEnElAno} " +
        $"{Consultas(estado.DisponiblesEnElAno)} en el año.";

    private static string Consultas(int cantidad) => cantidad == 1 ? "consulta" : "consultas";
}
