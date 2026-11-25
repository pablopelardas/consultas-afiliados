<template>
  <div class="app">
    <aside class="barra">
      <div class="marca">
        <span class="marca-sigla">PC</span>
        <span class="marca-nombre">Plan Consultas</span>
      </div>

      <p class="seccion">Secciones</p>

      <nav class="menu">
        <router-link v-for="i in items" :key="i.ruta" :to="i.ruta" class="item">
          <svg class="item-icono" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path :d="i.icono" />
          </svg>
          <span>{{ i.etiqueta }}</span>
        </router-link>
      </nav>

      <div class="usuario">
        <span class="usuario-sigla">PP</span>
        <span class="usuario-datos">
          <span class="usuario-nombre">Operaciones</span>
          <span class="usuario-rol">Mesa de turnos</span>
        </span>
      </div>
    </aside>

    <div class="columna">
      <header class="encabezado">
        <h1 class="titulo">{{ titulo }}</h1>
      </header>

      <main class="contenido">
        <router-view />
      </main>
    </div>
  </div>
</template>

<script>
const ICONOS = {
  calendario: 'M8 7V3m8 4V3M4 11h16M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z',
  mas: 'M12 8v8m-4-4h8m5 0a9 9 0 11-18 0 9 9 0 0118 0z',
  personas: 'M17 20h4v-2a3 3 0 00-5.4-1.8M17 20H7m10 0v-2c0-.7-.1-1.3-.4-1.9M7 20H3v-2a3 3 0 015.4-1.8M7 20v-2c0-.7.1-1.3.4-1.9m0 0a5 5 0 019.3 0M15 7a3 3 0 11-6 0 3 3 0 016 0z'
}

export default {
  name: 'App',
  data () {
    return {
      items: [
        { ruta: '/consultas', etiqueta: 'Consultas', icono: ICONOS.calendario },
        { ruta: '/consultas/nuevo', etiqueta: 'Nueva consulta', icono: ICONOS.mas },
        { ruta: '/afiliados', etiqueta: 'Afiliados', icono: ICONOS.personas }
      ]
    }
  },
  computed: {
    // El encabezado sale de la ruta y no de cada vista: así ninguna puede
    // olvidarse de ponerlo ni contradecir al ítem del menú que está activo.
    titulo () {
      const ruta = this.$route.path
      if (ruta === '/afiliados') return 'Afiliados'
      // El alta es un modal sobre la lista: el título de atrás sigue siendo el
      // de la lista, y el del modal lo pone el modal.
      if (ruta === '/consultas' || ruta === '/consultas/nuevo') return 'Consultas'
      return ruta.startsWith('/consultas/') ? 'Detalle de consulta' : 'Plan Consultas'
    }
  }
}
</script>

<style scoped>
.app {
  display: flex;
  min-height: 100vh;
}

/* ── Barra lateral ────────────────────────────────────────────────── */

.barra {
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  width: var(--nav-ancho);
  padding: var(--e-lg);
  background: var(--superficie-tenue);
  /* Fija: en una tabla larga el menú tiene que seguir a la vista, y el bloque
     de usuario queda al pie del panel y no al final del scroll. */
  position: sticky;
  top: 0;
  height: 100vh;
}

.marca {
  display: flex;
  align-items: center;
  gap: var(--e-sm);
  margin-bottom: var(--e-xl);
}

.marca-sigla {
  display: grid;
  place-items: center;
  width: 32px;
  height: 32px;
  border-radius: var(--radio-control);
  background: var(--superficie-fuerte);
  color: var(--texto-inverso);
  font-family: var(--fuente-display);
  font-size: var(--t-xs);
  font-weight: 700;
}

.marca-nombre {
  font-family: var(--fuente-display);
  font-size: var(--t-md);
  font-weight: 600;
  color: var(--texto-fuerte);
}

.seccion {
  margin: 0 0 var(--e-sm);
  color: var(--nav-seccion);
  font-size: var(--t-xs);
  font-weight: 600;
  letter-spacing: .08em;
  text-transform: uppercase;
}

.menu {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.item {
  display: flex;
  align-items: center;
  gap: var(--e-sm);
  min-height: var(--nav-item-alto);
  padding: 0 var(--e-sm);
  border-radius: var(--radio-control);
  color: var(--texto-fuerte);
  font-size: var(--t-sm);
  font-weight: 500;
  text-decoration: none;
  transition: background-color .12s ease, color .12s ease;
}

.item:hover { background: var(--nav-hover); }

/* `exact-active` y no `active`: /consultas es prefijo de /consultas/nuevo, y
   con la clase laxa quedarían dos ítems encendidos a la vez. */
.item.router-link-exact-active {
  background: var(--nav-activo);
  color: var(--texto-inverso);
  font-weight: 600;
}

.item-icono { width: 20px; height: 20px; flex-shrink: 0; }

.usuario {
  display: flex;
  align-items: center;
  gap: var(--e-sm);
  margin-top: auto;
  padding-top: var(--e-md);
  border-top: 1px solid var(--borde-sutil);
}

.usuario-sigla {
  display: grid;
  place-items: center;
  width: 32px;
  height: 32px;
  border-radius: var(--radio-pill);
  background: var(--marca-suave);
  color: var(--marca-activo);
  font-size: var(--t-xs);
  font-weight: 700;
}

.usuario-datos { display: flex; flex-direction: column; line-height: 1.3; }
.usuario-nombre { font-size: var(--t-sm); font-weight: 600; color: var(--texto-fuerte); }
.usuario-rol { font-size: var(--t-xs); color: var(--texto-apagado); }

/* ── Columna de contenido ─────────────────────────────────────────── */

.columna {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

.encabezado {
  display: flex;
  align-items: center;
  height: 64px;
  padding: 0 var(--e-lg) 0 0;
}

.titulo {
  font-size: var(--t-xl);
  font-weight: 600;
}

.contenido {
  flex: 1;
  margin: 0 var(--e-lg) var(--e-lg) 0;
  padding: var(--e-lg);
  background: var(--superficie);
  border: 1px solid var(--borde-sutil);
  border-radius: var(--radio-tarjeta);
}

@media (max-width: 860px) {
  .app { flex-direction: column; }
  .barra {
    width: 100%;
    height: auto;
    position: static;
    padding: var(--e-md);
  }
  .marca { margin-bottom: var(--e-md); }
  .seccion { display: none; }
  .menu { flex-direction: row; flex-wrap: wrap; }
  .usuario { display: none; }
  .encabezado { height: auto; padding: var(--e-md) var(--e-md) 0; }
  .contenido { margin: var(--e-md); padding: var(--e-md); }
}
</style>
