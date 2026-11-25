using ConsultasPlan.Models;

namespace ConsultasPlan.Dominio.Cupo;

/// <summary>
/// Traduce una fecha en los períodos anual y mensual que le corresponden.
/// </summary>
/// <remarks>
/// El enunciado dice "6 consultas por año, no más de 1 por mes" sin definir qué
/// es un año ni un mes. Son dos lecturas posibles —calendario o vigencia del
/// plan— y la ambigüedad queda acá, detrás de una interfaz, en lugar de
/// resolverse dentro de la regla de negocio.
///
/// La consecuencia práctica: <see cref="ICupoNutricionPolicy"/> recibe rangos y
/// cuenta. No sabe qué es un año. Cambiar de interpretación es cambiar de
/// proveedor, sin tocar la política ni sus tests.
/// </remarks>
public interface IPeriodoCupoProvider
{
    /// <param name="fechaDeLaConsulta">
    /// Fecha de la consulta que se evalúa, no la fecha actual: agendar en
    /// diciembre una consulta para enero consume el cupo de enero.
    /// </param>
    PeriodosDelCupo Para(DateTime fechaDeLaConsulta, Afiliado afiliado);
}
