using ConsultasPlan.Models;

namespace ConsultasPlan.Dominio.Cupo;

/// <summary>
/// Año y mes contados desde el alta del afiliado. Interpretación alternativa.
/// </summary>
/// <remarks>
/// Está implementada y testeada pero no es la que se registra: existe para que
/// la ambigüedad del enunciado sea un hecho del código y no solo una nota del
/// documento técnico. Si el negocio decide que "año" es el año de vigencia del
/// plan, se cambia el registro en <c>Program.cs</c> y nada más.
///
/// El afiliado dado de alta el 14/03 tiene su año de cupo del 14/03 al 13/03 y
/// sus meses del 14 de cada mes al 13 del siguiente.
/// </remarks>
public class PeriodoVigencia : IPeriodoCupoProvider
{
    public PeriodosDelCupo Para(DateTime fechaDeLaConsulta, Afiliado afiliado) =>
        new(Anual: PeriodoAnual(fechaDeLaConsulta, afiliado.FechaAlta),
            Mensual: PeriodoMensual(fechaDeLaConsulta, afiliado.FechaAlta.Day));

    /// <remarks>
    /// El período termina en el ancla siguiente, no en
    /// <c>inicio.AddMonths(1)</c>. La diferencia importa cuando el mes de inicio
    /// es más corto que el siguiente: con un alta el día 31, el período de
    /// febrero arranca el 28 y <c>AddMonths(1)</c> lo cierra el 28 de marzo,
    /// mientras el de marzo arranca el 31 — tres días que no caen en ningún
    /// período. Lo detectó el test de propiedad, no uno de ejemplo.
    /// </remarks>
    private static Periodo PeriodoMensual(DateTime fecha, int diaDeAlta)
    {
        var anclaDeEsteMes = Ancla(fecha.Year, fecha.Month, diaDeAlta);

        if (fecha >= anclaDeEsteMes)
        {
            var siguiente = PrimeroDelMes(fecha).AddMonths(1);
            return new Periodo(anclaDeEsteMes, Ancla(siguiente.Year, siguiente.Month, diaDeAlta));
        }

        var anterior = PrimeroDelMes(fecha).AddMonths(-1);
        return new Periodo(Ancla(anterior.Year, anterior.Month, diaDeAlta), anclaDeEsteMes);
    }

    /// <remarks>
    /// Mismo criterio que el mensual: el fin es el ancla del año siguiente. Un
    /// alta el 29 de febrero tiene ancla el 28 en los años comunes y el 29 en
    /// los bisiestos, así que cerrar con <c>AddYears(1)</c> dejaría un hueco
    /// cada cuatro años.
    /// </remarks>
    private static Periodo PeriodoAnual(DateTime fecha, DateTime alta)
    {
        var anclaDeEsteAno = Ancla(fecha.Year, alta.Month, alta.Day);

        return fecha >= anclaDeEsteAno
            ? new Periodo(anclaDeEsteAno, Ancla(fecha.Year + 1, alta.Month, alta.Day))
            : new Periodo(Ancla(fecha.Year - 1, alta.Month, alta.Day), anclaDeEsteAno);
    }

    /// <summary>
    /// Día de inicio del período en un mes dado, recortado al último del mes.
    /// </summary>
    /// <remarks>
    /// Un alta el 31 de enero no tiene día 31 en febrero, y un alta el 29 de
    /// febrero no lo tiene en años no bisiestos. Sin el recorte,
    /// <c>new DateTime</c> lanza y el cupo del afiliado deja de poder calcularse.
    /// </remarks>
    private static DateTime Ancla(int ano, int mes, int diaDeseado) =>
        new(ano, mes, Math.Min(diaDeseado, DateTime.DaysInMonth(ano, mes)),
            0, 0, 0, DateTimeKind.Utc);

    private static DateTime PrimeroDelMes(DateTime fecha) =>
        new(fecha.Year, fecha.Month, 1, 0, 0, 0, DateTimeKind.Utc);
}
