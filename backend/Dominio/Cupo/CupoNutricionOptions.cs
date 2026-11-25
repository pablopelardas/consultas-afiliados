namespace ConsultasPlan.Dominio.Cupo;

/// <summary>
/// Límites de la política de cupo de nutrición.
/// </summary>
/// <remarks>
/// Los valores 6 y 1 son configuración, no literales dentro de la regla. Dos
/// razones concretas:
///
/// 1. Un plan comercial cambia sus números sin que cambie la regla.
/// 2. Los tests verifican el borde inyectando límites chicos, sin tener que
///    sembrar seis consultas para probar el caso de la séptima.
/// </remarks>
public class CupoNutricionOptions
{
    public const string Seccion = "CupoNutricion";

    public int ConsultasPorAno { get; set; } = 6;

    public int ConsultasPorMes { get; set; } = 1;

    /// <summary>
    /// Código de la especialidad alcanzada. Código y no nombre: el nombre es
    /// editable y la regla no debe enterarse si lo cambian.
    /// </summary>
    public string CodigoEspecialidad { get; set; } = Models.CodigosDeEspecialidad.Nutricion;
}
