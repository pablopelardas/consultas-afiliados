using Microsoft.Extensions.Time.Testing;

namespace ConsultasPlan.Tests.Infraestructura;

/// <summary>
/// Reloj controlado para los tests, fijado en un instante conocido.
/// </summary>
/// <remarks>
/// Un lunes de junio al mediodía UTC: ni fin de mes, ni fin de año, ni cambio
/// de huso. Los tests que necesitan un borde lo construyen explícitamente desde
/// este instante, de modo que el borde sea visible en el propio test y no un
/// efecto del día en que se ejecuta.
/// </remarks>
public static class Reloj
{
    public static readonly DateTime Ahora = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    public static FakeTimeProvider Fijo() => new(new DateTimeOffset(Ahora));

    public static FakeTimeProvider FijadoEn(DateTime instanteUtc) =>
        new(new DateTimeOffset(DateTime.SpecifyKind(instanteUtc, DateTimeKind.Utc)));
}
