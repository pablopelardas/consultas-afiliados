using ConsultasPlan.Data;
using ConsultasPlan.Servicios;

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
    public static IConsultaService Consultas(AppDbContext db, TimeProvider? reloj = null) =>
        new ConsultaService(db, reloj ?? Reloj.Fijo());

    public static IAfiliadoService Afiliados(AppDbContext db, TimeProvider? reloj = null) =>
        new AfiliadoService(db, reloj ?? Reloj.Fijo());
}
