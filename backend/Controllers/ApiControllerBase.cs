using ConsultasPlan.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasPlan.Controllers;

/// <summary>
/// Traducción única de falla de dominio a respuesta HTTP.
/// </summary>
/// <remarks>
/// Si cada controlador escribiera su propio <c>Problem(...)</c>, el formato de
/// error dejaría de ser uno: eso era exactamente el problema del código base,
/// donde cada endpoint devolvía un objeto anónimo distinto.
///
/// Es lo único que los controladores agregan sobre el servicio. No toman
/// decisiones de negocio: eligen el código de estado que corresponde al motivo
/// que el servicio ya nombró.
/// </remarks>
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Problema(Falla falla)
    {
        // 400 se construye con ModelState para que la respuesta tenga la misma
        // forma que la validación automática de [ApiController]: el cliente lee
        // los errores por campo en un solo lugar.
        if (falla.Tipo == TipoDeFalla.EntradaInvalida)
        {
            ModelState.AddModelError(falla.Campo ?? string.Empty, falla.Detalle);
            return ValidationProblem(ModelState);
        }

        var (codigo, titulo) = falla.Tipo switch
        {
            TipoDeFalla.NoEncontrado =>
                (StatusCodes.Status404NotFound, "Recurso no encontrado"),

            // 409: la petición es válida y está bien formada, pero el estado
            // actual del sistema no permite la operación. 400 queda reservado
            // para entrada malformada, que es un problema del cliente.
            TipoDeFalla.Conflicto =>
                (StatusCodes.Status409Conflict, "La operación no es posible en el estado actual"),

            _ => throw new ArgumentOutOfRangeException(
                nameof(falla), falla.Tipo, "Tipo de falla sin traducción a HTTP.")
        };

        return Problem(detail: falla.Detalle, statusCode: codigo, title: titulo);
    }
}
