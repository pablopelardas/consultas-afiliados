using ConsultasPlan.Contratos;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasPlan.Tests.Caracterizacion;

/// <summary>
/// Tests de caracterización: documentan el comportamiento ACTUAL del código
/// base, incluido el incorrecto.
/// </summary>
/// <remarks>
/// Un test de caracterización no afirma que el comportamiento sea correcto:
/// afirma que es el que hay. Cuando la rama de corrección correspondiente
/// cambia la conducta, el test falla — y ese fallo es la confirmación de que el
/// fix hizo efecto. Por eso esta clase se vacía a medida que avanza el trabajo.
///
/// Lo que queda acá es el problema #10 del inventario, cuyo alcance completo
/// —modelar duración de consulta y agenda del profesional— excede el challenge
/// y está declarado fuera de alcance en §6 del documento técnico.
/// </remarks>
public class ConsultasControllerTests
{
    // Problema #10 — La detección de superposición compara igualdad exacta de
    // FechaHora. Un minuto de diferencia sobre el mismo profesional pasa.
    [Fact]
    public async Task Crear_consulta_un_minuto_despues_de_otra_del_mismo_profesional_es_aceptado()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        var ocupado = Reloj.Ahora.AddDays(30);
        db.AgregarConsulta(afiliado, profesional, ocupado, EstadoConsulta.Confirmado);

        var controlador = new ConsultasPlan.Controllers.ConsultasController(db, Reloj.Fijo());

        var resultado = await controlador.CrearConsulta(new CrearConsultaRequest
        {
            AfiliadoId = afiliado.Id,
            ProfesionalId = profesional.Id,
            FechaHora = ocupado.AddMinutes(1),
            Modalidad = ModalidadConsulta.Presencial,
            Motivo = "Se superpone con la anterior"
        });

        Assert.IsType<CreatedAtActionResult>(resultado);
    }
}
