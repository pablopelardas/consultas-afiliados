<template>
  <div class="cupo">
    <p class="titulo">Cupo de nutrición</p>

    <p v-if="cargando" class="estado">Consultando cupo…</p>

    <template v-else-if="cupo">
      <div v-for="p in periodos" :key="p.nombre" class="periodo">
        <span class="nombre">{{ p.nombre }}</span>
        <span class="barra">
          <span
            v-for="n in p.datos.limite"
            :key="n"
            :class="['tramo', n <= p.datos.usadas ? 'usado' : 'libre']"
          />
        </span>
        <span :class="['numeros', p.datos.disponibles === 0 && 'agotado']">
          {{ p.datos.usadas }} de {{ p.datos.limite }} · hasta el {{ fecha(p.datos.hasta) }}
        </span>
      </div>
    </template>
  </div>
</template>

<script>
import { fechaDeCalendario } from '../services/fechas'

/**
 * Estado del cupo tal como lo informa el backend.
 *
 * Presentacional a propósito: no calcula disponibles ni interpreta períodos. La
 * regla ya decidió y acá se muestra, porque una segunda implementación de la
 * política en el cliente se desincroniza del plan sin que nada falle.
 */
export default {
  name: 'CupoNutricion',
  props: {
    cupo: { type: Object, default: null },
    cargando: { type: Boolean, default: false }
  },
  computed: {
    periodos () {
      if (!this.cupo) return []
      return [
        { nombre: 'Año', datos: this.cupo.ano },
        { nombre: 'Mes', datos: this.cupo.mes }
      ]
    }
  },
  methods: {
    fecha: fechaDeCalendario
  }
}
</script>

<style scoped>
.cupo {
  background: #f0f4f8;
  border-radius: 6px;
  padding: 14px 16px;
  margin-bottom: 18px;
}
.titulo {
  margin: 0 0 10px;
  font-size: 13px;
  font-weight: 600;
  color: #555;
}
.estado { margin: 0; font-size: 13px; color: #777 }
.periodo {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 13px;
  padding: 3px 0;
}
.nombre { width: 32px; color: #666; font-weight: 600 }
.barra { display: flex; gap: 3px }
.tramo {
  width: 14px;
  height: 8px;
  border-radius: 2px;
}
.tramo.usado { background: #1a73e8 }
.tramo.libre { background: #cfd8e3 }
.numeros { color: #555 }
.numeros.agotado { color: #b26a00; font-weight: 600 }
</style>
