import { http } from './http'

// Las vistas consumen datos, no respuestas HTTP. Devolver `res.data` desde acá
// evita que cada componente repita el desempaquetado.
const datos = (respuesta) => respuesta.data

export const consultasApi = {
  getAll:             ()          => http.get('/consultas').then(datos),
  getById:            (id)        => http.get(`/consultas/${id}`).then(datos),
  create:             (data)      => http.post('/consultas', data).then(datos),
  cancelar:           (id)        => http.post(`/consultas/${id}/cancelacion`).then(datos),
  marcarInasistencia: (id)        => http.post(`/consultas/${id}/inasistencia`).then(datos),
  registrarAtencion:  (id)        => http.post(`/consultas/${id}/atencion`).then(datos),
  actualizarEstado:   (id, data)  => http.put(`/consultas/${id}/estado`, data).then(datos)
}

export const afiliadosApi = {
  getAll:  ()          => http.get('/afiliados').then(datos),
  getById: (id)        => http.get(`/afiliados/${id}`).then(datos),
  create:  (data)      => http.post('/afiliados', data).then(datos),
  update:  (id, data)  => http.put(`/afiliados/${id}`, data).then(datos),
  // Baja lógica: el registro y su historial de consultas se conservan.
  darDeBaja: (id)      => http.delete(`/afiliados/${id}`).then(datos),
  reactivar: (id)      => http.post(`/afiliados/${id}/reactivacion`).then(datos),
  // Estado del cupo de nutrición: límite, usadas y disponibles por período.
  getCupoNutricion: (id) => http.get(`/afiliados/${id}/cupo-nutricion`).then(datos)
}

export const profesionalesApi = {
  getAll: () => http.get('/profesionales').then(datos)
}

export const sedesApi = {
  getAll: () => http.get('/sedes').then(datos)
}
