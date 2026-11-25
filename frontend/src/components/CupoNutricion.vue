<template>
  <div class="cupo">
    <div class="titulo">
      <span>Cupo de nutrición</span>
      <span v-if="cargando" class="hilandero" />
    </div>

    <div v-if="cupo" class="periodos">
      <div v-for="p in periodos" :key="p.nombre" class="periodo">
        <span class="nombre">{{ p.nombre }}</span>

        <span class="barra" :aria-label="`${p.datos.usadas} de ${p.datos.limite} usadas`">
          <span
            v-for="n in p.datos.limite"
            :key="n"
            :class="['tramo', n <= p.datos.usadas ? 'usado' : 'libre']"
          />
        </span>

        <span class="numeros">
          <strong :class="p.datos.disponibles === 0 && 'agotado'">
            {{ p.datos.usadas }} de {{ p.datos.limite }}
          </strong>
          <span class="hasta">hasta el {{ fecha(p.datos.hasta) }}</span>
        </span>
      </div>
    </div>
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
  margin-bottom: var(--e-md);
  padding: var(--e-sm) var(--e-md);
  background: var(--superficie-tenue);
  border: 1px solid var(--borde-sutil);
  border-radius: var(--radio-control);
}

.titulo {
  display: flex;
  align-items: center;
  gap: var(--e-xs);
  margin-bottom: var(--e-xs);
  color: var(--texto-sutil);
  font-size: var(--t-xs);
  font-weight: 600;
  letter-spacing: .04em;
  text-transform: uppercase;
}

.periodos { display: flex; flex-direction: column; gap: 6px; }

.periodo {
  display: flex;
  align-items: center;
  gap: var(--e-sm);
  font-size: var(--t-xs);
}

.nombre {
  width: 28px;
  flex-shrink: 0;
  color: var(--texto-apagado);
  font-weight: 600;
}

.barra { display: flex; gap: 3px; }

.tramo {
  width: 16px;
  height: 7px;
  border-radius: 2px;
}

.tramo.usado { background: var(--marca); }
.tramo.libre { background: var(--borde-sutil); }

.numeros {
  display: flex;
  align-items: baseline;
  gap: var(--e-xs);
  color: var(--texto-apagado);
}

.numeros strong { color: var(--texto-sutil); font-weight: 600; }
.numeros strong.agotado { color: var(--texto-alerta); }
.hasta { color: var(--texto-deshabilitado); }
</style>
