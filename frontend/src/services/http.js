import axios from 'axios'

/**
 * Categorías de falla que la interfaz necesita distinguir.
 *
 * No son los códigos HTTP: son las decisiones distintas que toma la vista. Un
 * 409 y un 500 se muestran distinto porque significan cosas distintas —uno es
 * el sistema funcionando, el otro es el sistema roto— y mezclarlos fue
 * exactamente el problema del código base.
 */
export const TipoDeError = {
  Validacion: 'validacion',
  NoEncontrado: 'noEncontrado',
  Regla: 'regla',
  Servidor: 'servidor',
  Red: 'red'
}

const MENSAJES_POR_DEFECTO = {
  [TipoDeError.Validacion]: 'Revisá los datos ingresados.',
  [TipoDeError.NoEncontrado]: 'No encontramos lo que buscabas.',
  [TipoDeError.Regla]: 'La operación no es posible en este momento.',
  [TipoDeError.Servidor]: 'Hubo un problema en el servidor. Volvé a intentar en unos minutos.',
  [TipoDeError.Red]: 'No pudimos contactar al servidor. Revisá tu conexión.'
}

export class ErrorDeApi extends Error {
  constructor ({ tipo, mensaje, campos = {}, status = null }) {
    super(mensaje || MENSAJES_POR_DEFECTO[tipo])
    this.name = 'ErrorDeApi'
    this.tipo = tipo
    this.campos = campos
    this.status = status
  }

  /**
   * Si la causa es una decisión del dominio y no una falla técnica.
   *
   * Lo usa la vista para elegir el tono: un cupo agotado no es un error rojo,
   * es una regla del plan, y mostrarlo como si algo se hubiera roto hace que el
   * usuario llame a soporte en lugar de leer el mensaje.
   */
  get esDeNegocio () {
    return this.tipo === TipoDeError.Regla || this.tipo === TipoDeError.Validacion
  }
}

/**
 * Traduce los errores de ValidationProblem a {campo: mensaje}.
 *
 * Las claves vienen con el nombre de la propiedad del request (`FechaHora`) y
 * el formulario las nombra en camelCase, así que se normalizan: sin esto el
 * mensaje llega pero no se puede ubicar debajo del campo que lo causó.
 */
function camposDeValidacion (errors) {
  if (!errors) return {}

  return Object.entries(errors).reduce((acc, [campo, mensajes]) => {
    const clave = campo.charAt(0).toLowerCase() + campo.slice(1)
    acc[clave] = Array.isArray(mensajes) ? mensajes.join(' ') : String(mensajes)
    return acc
  }, {})
}

/**
 * Clasifica una respuesta de error del backend.
 *
 * El backend responde ProblemDetails (RFC 7807) en todos los fallos, así que
 * `detail` es el mensaje escrito para el usuario final: el motivo del cupo
 * agotado, la ventana de cancelación, el estado que no admite la transición.
 * Descartarlo —lo que hacía `catch { alert('Error...') }`— desperdicia la única
 * explicación que el sistema sabe dar.
 */
export function clasificar (error) {
  if (!error.response) {
    return new ErrorDeApi({ tipo: TipoDeError.Red })
  }

  const { status, data } = error.response
  const detalle = typeof data === 'object' && data !== null ? data.detail : null

  if (status === 400) {
    return new ErrorDeApi({
      tipo: TipoDeError.Validacion,
      mensaje: detalle,
      campos: camposDeValidacion(data?.errors),
      status
    })
  }

  if (status === 404) {
    return new ErrorDeApi({ tipo: TipoDeError.NoEncontrado, mensaje: detalle, status })
  }

  if (status >= 500) {
    // El detalle de un 5xx describe una falla interna y no le sirve a nadie del
    // otro lado de la pantalla. Se descarta a propósito.
    return new ErrorDeApi({ tipo: TipoDeError.Servidor, status })
  }

  return new ErrorDeApi({ tipo: TipoDeError.Regla, mensaje: detalle, status })
}

export const http = axios.create({
  baseURL: import.meta.env.VITE_API_URL,
  headers: { 'Content-Type': 'application/json' }
})

// Rechaza con el error tipado en lugar de devolverlo: así `try/catch` sigue
// siendo la forma natural de escribirlo, y una llamada que se olvide de
// manejarlo falla a la vista en lugar de seguir con un valor indefinido.
http.interceptors.response.use(
  (respuesta) => respuesta,
  (error) => Promise.reject(clasificar(error))
)
