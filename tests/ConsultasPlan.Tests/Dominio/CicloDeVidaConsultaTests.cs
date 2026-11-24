using ConsultasPlan.Dominio;
using ConsultasPlan.Models;

namespace ConsultasPlan.Tests.Dominio;

/// <summary>
/// La tabla de transiciones completa, verificada sin infraestructura.
/// </summary>
/// <remarks>
/// Estos tests no instancian un contexto, ni un controlador, ni un reloj.
/// Esa es la ventaja de mantener la regla como función pura: se verifica cada
/// combinación de estados en milisegundos y el test describe el dominio, no la
/// mecánica de levantarlo.
/// </remarks>
public class CicloDeVidaConsultaTests
{
    [Theory]
    [InlineData(EstadoConsulta.Pendiente,  EstadoConsulta.Confirmado)]
    [InlineData(EstadoConsulta.Pendiente,  EstadoConsulta.Cancelado)]
    [InlineData(EstadoConsulta.Pendiente,  EstadoConsulta.Atendido)]
    [InlineData(EstadoConsulta.Pendiente,  EstadoConsulta.Inasistencia)]
    [InlineData(EstadoConsulta.Confirmado, EstadoConsulta.Cancelado)]
    [InlineData(EstadoConsulta.Confirmado, EstadoConsulta.Atendido)]
    [InlineData(EstadoConsulta.Confirmado, EstadoConsulta.Inasistencia)]
    [InlineData(EstadoConsulta.Inasistencia, EstadoConsulta.Atendido)]
    public void Transiciones_admitidas(EstadoConsulta desde, EstadoConsulta hacia)
    {
        Assert.True(CicloDeVidaConsulta.PuedeTransicionar(desde, hacia));
    }

    [Theory]
    // Retroceder no describe ningún hecho ocurrido.
    [InlineData(EstadoConsulta.Confirmado,   EstadoConsulta.Pendiente)]
    [InlineData(EstadoConsulta.Atendido,     EstadoConsulta.Pendiente)]
    [InlineData(EstadoConsulta.Atendido,     EstadoConsulta.Confirmado)]
    // Una consulta atendida ya ocurrió.
    [InlineData(EstadoConsulta.Atendido,     EstadoConsulta.Cancelado)]
    [InlineData(EstadoConsulta.Atendido,     EstadoConsulta.Inasistencia)]
    // Cancelado es terminal: liberó cupo y franja horaria.
    [InlineData(EstadoConsulta.Cancelado,    EstadoConsulta.Pendiente)]
    [InlineData(EstadoConsulta.Cancelado,    EstadoConsulta.Confirmado)]
    [InlineData(EstadoConsulta.Cancelado,    EstadoConsulta.Atendido)]
    [InlineData(EstadoConsulta.Cancelado,    EstadoConsulta.Inasistencia)]
    // Una inasistencia solo se corrige hacia Atendido.
    [InlineData(EstadoConsulta.Inasistencia, EstadoConsulta.Pendiente)]
    [InlineData(EstadoConsulta.Inasistencia, EstadoConsulta.Confirmado)]
    [InlineData(EstadoConsulta.Inasistencia, EstadoConsulta.Cancelado)]
    public void Transiciones_rechazadas(EstadoConsulta desde, EstadoConsulta hacia)
    {
        Assert.False(CicloDeVidaConsulta.PuedeTransicionar(desde, hacia));
    }

    [Theory]
    [InlineData(EstadoConsulta.Pendiente)]
    [InlineData(EstadoConsulta.Confirmado)]
    [InlineData(EstadoConsulta.Cancelado)]
    [InlineData(EstadoConsulta.Atendido)]
    [InlineData(EstadoConsulta.Inasistencia)]
    public void Ningun_estado_transiciona_hacia_si_mismo(EstadoConsulta estado)
    {
        Assert.False(CicloDeVidaConsulta.PuedeTransicionar(estado, estado));
    }

    [Theory]
    [InlineData(EstadoConsulta.Atendido)]
    [InlineData(EstadoConsulta.Cancelado)]
    public void Estados_terminales(EstadoConsulta estado)
    {
        Assert.True(CicloDeVidaConsulta.EsTerminal(estado));
    }

    // Toda combinación posible está contemplada: la tabla no tiene huecos que
    // dependan de que nadie los pruebe.
    [Fact]
    public void La_tabla_cubre_todos_los_estados_del_enum()
    {
        foreach (var estado in Enum.GetValues<EstadoConsulta>())
        {
            var transiciones = CicloDeVidaConsulta.TransicionesDesde(estado);
            Assert.DoesNotContain(estado, transiciones);
        }
    }

    [Fact]
    public void El_rechazo_a_si_mismo_se_explica_como_estado_ya_alcanzado()
    {
        var explicacion = CicloDeVidaConsulta.ExplicarRechazo(
            EstadoConsulta.Cancelado, EstadoConsulta.Cancelado);

        Assert.Contains("ya se encuentra", explicacion);
    }

    [Fact]
    public void El_rechazo_desde_un_estado_terminal_lo_indica()
    {
        var explicacion = CicloDeVidaConsulta.ExplicarRechazo(
            EstadoConsulta.Atendido, EstadoConsulta.Pendiente);

        Assert.Contains("no admite cambios", explicacion);
    }

    [Fact]
    public void El_rechazo_enumera_los_estados_admitidos()
    {
        var explicacion = CicloDeVidaConsulta.ExplicarRechazo(
            EstadoConsulta.Confirmado, EstadoConsulta.Pendiente);

        Assert.Contains(nameof(EstadoConsulta.Cancelado), explicacion);
        Assert.Contains(nameof(EstadoConsulta.Atendido), explicacion);
    }
}
