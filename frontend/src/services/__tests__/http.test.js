import { describe, it, expect } from 'vitest'
import { clasificar, TipoDeError } from '../http'

/**
 * El código base hacía `catch { alert('Error al procesar la solicitud') }` en
 * seis lugares: descartaba el error sin siquiera recibirlo. El backend responde
 * ProblemDetails con un `detail` escrito para el usuario final, y esa es la
 * única explicación que el sistema sabe dar.
 *
 * Estos tests fijan esa traducción, porque es la pieza de la que depende que la
 * política de cupo sea visible.
 */

const respuesta = (status, data) => ({ response: { status, data } })

describe('clasificar', () => {
  it('sin respuesta, es una falla de red', () => {
    const error = clasificar({ message: 'Network Error' })

    expect(error.tipo).toBe(TipoDeError.Red)
    expect(error.message).toContain('conexión')
    expect(error.esDeNegocio).toBe(false)
  })

  it('un 409 conserva el motivo del rechazo', () => {
    const motivo = 'El plan permite 1 consulta de nutrición por mes y el afiliado ' +
      'ya tiene 1 entre el 01/06/2026 y el 30/06/2026.'

    const error = clasificar(respuesta(409, { status: 409, detail: motivo }))

    expect(error.tipo).toBe(TipoDeError.Regla)
    expect(error.message).toBe(motivo)
    expect(error.esDeNegocio).toBe(true)
  })

  it('un 400 ubica cada mensaje en su campo', () => {
    const error = clasificar(respuesta(400, {
      status: 400,
      errors: {
        FechaHora: ['La fecha tiene que ser futura.'],
        Motivo: ['Requerido.', 'Máximo 500 caracteres.']
      }
    }))

    expect(error.tipo).toBe(TipoDeError.Validacion)
    expect(error.campos.fechaHora).toBe('La fecha tiene que ser futura.')
    expect(error.campos.motivo).toBe('Requerido. Máximo 500 caracteres.')
  })

  it('un 404 usa el detalle del backend', () => {
    const error = clasificar(respuesta(404, { detail: 'La consulta indicada no existe.' }))

    expect(error.tipo).toBe(TipoDeError.NoEncontrado)
    expect(error.message).toBe('La consulta indicada no existe.')
  })

  // Un detalle de 5xx describe una falla interna. Mostrarlo filtra información
  // del servidor y no le dice nada útil a quien está del otro lado.
  it('un 500 descarta el detalle del servidor', () => {
    const error = clasificar(respuesta(500, { detail: 'Object reference not set' }))

    expect(error.tipo).toBe(TipoDeError.Servidor)
    expect(error.message).not.toContain('Object reference')
    expect(error.esDeNegocio).toBe(false)
  })

  it('sin detalle, cae en un mensaje propio en lugar de quedar vacío', () => {
    const error = clasificar(respuesta(409, {}))

    expect(error.message.length).toBeGreaterThan(0)
    expect(error.tipo).toBe(TipoDeError.Regla)
  })

  // Un cuerpo que no es ProblemDetails no debe hacer estallar la clasificación:
  // el usuario tiene que ver un mensaje, no una pantalla en blanco.
  it('un cuerpo que no es ProblemDetails no rompe', () => {
    const error = clasificar(respuesta(409, 'texto plano'))

    expect(error.tipo).toBe(TipoDeError.Regla)
    expect(error.message.length).toBeGreaterThan(0)
  })
})
