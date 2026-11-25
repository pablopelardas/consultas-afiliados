/**
 * Dos tipos de fecha que el backend envía igual y hay que mostrar distinto.
 *
 * Un instante —cuándo es el turno— se convierte a la hora local de quien mira:
 * las 14:00 UTC son las 11:00 en Buenos Aires y el turno es a las 11.
 *
 * Una fecha de calendario —hasta cuándo rige el cupo— ya es una decisión
 * tomada: el backend dice "el último día incluido es el 31/12". Convertirla
 * mueve el límite un día al oeste de Greenwich y el período aparece cerrando
 * antes de lo que cierra.
 */

const FORMATO_INSTANTE = {
  day: '2-digit', month: '2-digit', year: 'numeric',
  hour: '2-digit', minute: '2-digit', hour12: false
}

// Sin segundos: el turno se agenda al minuto y mostrar ':00' es ruido en una
// tabla de veinte filas.
export function instante (iso) {
  return new Date(iso).toLocaleString('es-AR', FORMATO_INSTANTE)
}

export function fechaDeCalendario (iso) {
  return new Date(iso).toLocaleDateString('es-AR', { timeZone: 'UTC' })
}
