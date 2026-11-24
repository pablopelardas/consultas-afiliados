namespace ConsultasPlan.Helpers;

public static class DateTimeExtensions
{
    /// <summary>
    /// Indica si <paramref name="fecha"/> cae en el mismo año que
    /// <paramref name="referencia"/>.
    /// </summary>
    /// <remarks>
    /// La referencia se recibe en lugar de leerse de <c>DateTime.Now</c>: el
    /// resultado cambia al cruzar el 31 de diciembre, y ese borde solo se puede
    /// verificar si el instante de comparación es controlable. Ambas fechas se
    /// esperan en UTC.
    /// </remarks>
    public static bool IsInCurrentYear(this DateTime fecha, DateTime referencia)
    {
        return fecha.Year == referencia.Year;
    }
}
