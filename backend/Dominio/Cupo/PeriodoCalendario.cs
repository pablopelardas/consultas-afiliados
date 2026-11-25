using ConsultasPlan.Models;

namespace ConsultasPlan.Dominio.Cupo;

/// <summary>
/// Año y mes de calendario. Interpretación adoptada.
/// </summary>
/// <remarks>
/// Criterio: el enunciado dice que las consultas no usadas no se acumulan, lo
/// que exige un período con un corte conocido. El calendario lo tiene y es el
/// que entienden sin explicación tanto el afiliado como quien atiende el
/// teléfono.
///
/// No depende del afiliado: el corte es el mismo para todos.
/// </remarks>
public class PeriodoCalendario : IPeriodoCupoProvider
{
    public PeriodosDelCupo Para(DateTime fechaDeLaConsulta, Afiliado afiliado)
    {
        var inicioDeAno = Utc(fechaDeLaConsulta.Year, 1, 1);
        var inicioDeMes = Utc(fechaDeLaConsulta.Year, fechaDeLaConsulta.Month, 1);

        return new PeriodosDelCupo(
            Anual: new Periodo(inicioDeAno, inicioDeAno.AddYears(1)),
            Mensual: new Periodo(inicioDeMes, inicioDeMes.AddMonths(1)));
    }

    private static DateTime Utc(int ano, int mes, int dia) =>
        new(ano, mes, dia, 0, 0, 0, DateTimeKind.Utc);
}
