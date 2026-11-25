using ConsultasPlan.Data;
using ConsultasPlan.Dominio.Cupo;
using ConsultasPlan.Servicios;
using Microsoft.Extensions.Options;

namespace ConsultasPlan.Tests.Infraestructura;

/// <summary>
/// Construcción de los servicios de dominio sobre un contexto de prueba.
/// </summary>
/// <remarks>
/// Los tests de reglas de negocio se escriben contra el servicio y no contra el
/// controlador: la regla no cambia según el transporte, y verificarla a través
/// de HTTP obliga a traducir cada expectativa a un código de estado.
/// </remarks>
public static class ServiciosDePrueba
{
    public static ICupoPolicy Politica(
        int porAno = 6, int porMes = 1) =>
        new CupoNutricionPolicy(Options.Create(new CupoNutricionOptions
        {
            ConsultasPorAno = porAno,
            ConsultasPorMes = porMes
        }));

    public static ICupoNutricionService Cupo(
        AppDbContext db,
        TimeProvider? reloj = null,
        ICupoPolicy? politica = null,
        IPeriodoCupoProvider? periodos = null) =>
        new CupoNutricionService(
            db,
            politica ?? Politica(),
            periodos ?? new PeriodoCalendario(),
            reloj ?? Reloj.Fijo());

    public static IConsultaService Consultas(
        AppDbContext db,
        TimeProvider? reloj = null,
        ICupoNutricionService? cupo = null)
    {
        var elReloj = reloj ?? Reloj.Fijo();
        return new ConsultaService(db, elReloj, cupo ?? Cupo(db, elReloj));
    }

    public static IAfiliadoService Afiliados(AppDbContext db, TimeProvider? reloj = null) =>
        new AfiliadoService(db, reloj ?? Reloj.Fijo());
}
