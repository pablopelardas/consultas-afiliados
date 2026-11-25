import { describe, it, expect } from 'vitest'
import { fechaDeCalendario } from '../fechas'

/**
 * Lo encontró el navegador, no un test: el panel de cupo mostraba "hasta el
 * 30/12/2026" sobre un `2026-12-31T00:00:00Z`. Medianoche UTC es el día
 * anterior en cualquier zona al oeste de Greenwich, que es donde corre esto.
 */
describe('fechaDeCalendario', () => {
  it('no se corre un día al formatear medianoche UTC', () => {
    expect(fechaDeCalendario('2026-12-31T00:00:00Z')).toBe('31/12/2026')
    expect(fechaDeCalendario('2026-11-30T00:00:00Z')).toBe('30/11/2026')
    expect(fechaDeCalendario('2026-01-01T00:00:00Z')).toBe('1/1/2026')
  })
})
