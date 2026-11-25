using ConsultasPlan.Servicios;

namespace ConsultasPlan.Tests.Infraestructura;

/// <summary>
/// Afirmaciones sobre el resultado de una operación de dominio.
/// </summary>
/// <remarks>
/// El mensaje de la falla forma parte de lo que el usuario termina leyendo, así
/// que un test que rechaza algo también deja constancia de que hay un motivo
/// escrito. Un 409 con el detalle vacío es tan inútil como no devolver nada.
/// </remarks>
public static class AssertResultado
{
    public static void Exito(ServiceResult resultado) =>
        Assert.True(resultado.EsExito,
            $"Se esperaba éxito y falló: {resultado.Falla?.Tipo} — {resultado.Falla?.Detalle}");

    public static void Conflicto(ServiceResult resultado, string? porque = null) =>
        Fallo(resultado, TipoDeFalla.Conflicto, porque);

    public static void NoEncontrado(ServiceResult resultado, string? porque = null) =>
        Fallo(resultado, TipoDeFalla.NoEncontrado, porque);

    public static void EntradaInvalida(ServiceResult resultado, string? porque = null) =>
        Fallo(resultado, TipoDeFalla.EntradaInvalida, porque);

    private static void Fallo(ServiceResult resultado, TipoDeFalla esperado, string? porque)
    {
        var contexto = porque is null ? string.Empty : $" ({porque})";

        Assert.False(resultado.EsExito, $"Se esperaba {esperado} y la operación tuvo éxito{contexto}.");
        Assert.Equal(esperado, resultado.Falla!.Tipo);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Falla.Detalle),
            "La falla no explica el motivo.");
    }
}
