# Documento técnico — Challenge Desarrollador .NET

**Autor:** Pablo Pelardas
**Stack:** .NET 8 (ASP.NET Core · EF Core · SQLite) + Vue 3 (Vite)
**Repositorio:** https://github.com/pablopelardas/consultas-afiliados

> Este documento es el resumen de decisiones: qué estaba mal, qué se corrigió y
> con qué criterio. El razonamiento completo de cada cambio —con el código, los
> tests y las alternativas descartadas— está en
> [`ANEXO-TECNICO.md`](ANEXO-TECNICO.md), que se escribió en paralelo al
> desarrollo. Las referencias del tipo *(anexo §3.9)* apuntan ahí.

---

## 1. Resultado

```
dotnet test   →  164 passed, 0 failed
npm test      →    8 passed, 0 failed
```

El punto de partida es que **el código base no compila**. A partir de ahí se
relevaron **28 problemas**: 24 corregidos, 3 con alcance acotado y declarado, 1
fuera de alcance con su razón (§3).

La Parte 2 está implementada y verificada, con la ambigüedad del enunciado
—qué significa "año", qué significa "mes"— materializada en dos
implementaciones de período y no solo descrita en este documento (§4).

### Las tres decisiones que estructuran la solución

| Decisión | En qué consiste |
|---|---|
| **El ciclo de vida es una tabla** (§3, anexo §3.5) | Las transiciones válidas son un dato del dominio declarado una vez, no un `if` repetido en cada endpoint |
| **La política de cupo es una función pura** (§4) | No consulta la base, no lee el reloj, no produce efectos: los casos borde se escriben como entrada y no como estado de base |
| **El cliente pregunta las reglas en vez de copiarlas** (§4, anexo §3.10) | El frontend no reimplementa la tabla de transiciones ni compara `especialidad === 'Nutrición'`: el backend le declara qué acciones existen y qué profesionales tienen cupo |

---

## 2. Cómo se organizó el trabajo

El primer commit de `main` es el **código base sin modificar**. Eso convierte a
`main` en la línea base: todo diff posterior es intervención propia y se lee
aislado.

Una rama por naturaleza de cambio, cada una con su Pull Request:

| Rama | Alcance |
|---|---|
| `chore/setup-proyecto` | Normalización, convenciones, proyecto de tests |
| `fix/errores-compilacion` | Los dos errores que impiden compilar |
| `fix/manejo-fechas-utc` | `TimeProvider` inyectable + conversión UTC en EF |
| `fix/semantica-operaciones` | Verbos HTTP, códigos de respuesta, `ProblemDetails` |
| `fix/ciclo-vida-consulta` | Tabla de transiciones de estado |
| `fix/historial-afiliados` | Baja lógica en lugar de baja física |
| `fix/contratos-api` | DTOs de entrada/salida y validación |
| `refactor/capa-servicios` | Interfaces de servicio y resultado tipado |
| `feat/cupo-nutricion` | **Parte 2:** política de cupo |
| `fix/ux-manejo-errores` | Frontend: errores visibles y cupo en pantalla |
| `feat/rediseno-interfaz` | Frontend: capa de tokens y rediseño |

**Verificación.** Cada corrección arranca por un test que **falla** y describe
el comportamiento actual. Para el código base existente se escribieron **tests
de caracterización**: tests que documentan el comportamiento tal como es hoy,
incluido el incorrecto. Sin esa red, cada corrección posterior es una apuesta
sobre qué más se rompió.

| Alcance de los tests | Suites |
|---|---|
| Dominio sin infraestructura | Ciclo de vida, política de cupo, períodos, invariante de consumo |
| Servicios sobre EF InMemory | Agendamiento, cupo, historial, baja lógica |
| Migraciones sobre SQLite real | Las dos etapas del catálogo, incluido el ida y vuelta |
| Pipeline HTTP completo | Verbos, códigos, `ProblemDetails`, validación, contratos |
| Frontend | Traducción de errores y formateo de fechas |

Tres defectos aparecieron **ejecutando y no leyendo código**, lo que es el
argumento a favor de haber invertido en esa red antes que en avanzar:

- Un **test de propiedad** encontró que `PeriodoVigencia` dejaba tres días al
  año sin período para ciertos afiliados, y un hueco cada cuatro años para quien
  se da de alta un 29 de febrero.
- **Probar la migración del catálogo sobre datos reales** mostró que en un solo
  paso perdía información. El caso que ninguna migración resuelve sola es un
  profesional con una especialidad que el catálogo no contempla: de ahí las dos
  etapas con una consulta de control en el medio.
- **Abrir el navegador** encontró dos defectos con el build y los 164 tests en
  verde: una fecha de calendario que se corría un día en UTC-3 y una vista que
  quedaba mostrando el registro anterior al cambiar solo el parámetro de ruta.

---

## 3. Parte 1 — Problemas detectados

### Backend

| # | Problema | Dónde | Estado |
|---|---|---|---|
| 1 | No compila: propiedad `Nombre` inexistente | `AfiliadosController.cs:50` | Corregido |
| 2 | No compila: falta `using` de `Helpers` | `ConsultasController.cs:102` | Corregido |
| 3 | **`GET` que muta estado**: cancelar responde a `[HttpGet]`. Un prefetch del navegador o un crawler puede cancelar consultas | `ConsultasController.cs:68` | Corregido |
| 4 | Ventana de cancelación mal calculada: valida `FromHours(23)` y el mensaje al usuario dice 24 horas | `ConsultasController.cs:74` | Corregido |
| 5 | **`PUT /{id}/estado` permite cualquier transición**: saltea la regla de 24 horas, la de inasistencia sobre consulta futura y habría salteado el cupo. Tampoco valida `Enum.IsDefined` | `ConsultasController.cs:110` | Corregido (anexo §3.5, §3.8) |
| 6 | Sin máquina de estados: se puede marcar inasistencia o registrar atención sobre una consulta cancelada | `ConsultasController.cs` | Corregido |
| 7 | **`DateTime.Now` y `UtcNow` mezclados**: persiste en UTC y compara contra hora local — 3 horas de desvío sobre la ventana de cancelación y sobre "el año en curso". Se suma la pérdida de `DateTimeKind` al leer de SQLite | Varios | Corregido |
| 8 | **Over-posting**: los endpoints reciben entidades de EF (`[FromBody] Consulta`). El cliente puede enviar `Bloqueado`, `ConsultasUsadas`, `isActive` y grafos anidados que EF persiste | Controllers | Corregido |
| 9 | Sin validación de entrada: ni `[Required]`, ni unicidad de DNI, ni formato de email, ni fecha futura | Controllers | Corregido |
| 10 | Superposición de turnos incompleta: compara igualdad exacta de `FechaHora`, sin duración ni índice único | `ConsultasController.cs:54` | Parcial (§6) |
| 11 | `ConsultasUsadas` es un contador que nadie actualiza | `Models/Afiliado.cs` | No se usa como fuente de verdad (§5) |
| 12 | `FechaBloqueo` nunca se escribe | `Models/Afiliado.cs` | Fuera de alcance (§6) |
| 13 | **Baja física de afiliados** existiendo `isActive`, con FK `OnDelete(SetNull)`: las consultas quedan sin afiliado y se pierde el historial | `AfiliadosController.cs:59` | Corregido |
| 14 | `Especialidad` es texto libre: la política dependería de comparar contra el literal `"Nutrición"` | `Models/Profesional.cs` | Corregido — catálogo (§5) |
| 15 | `EnsureCreated()` sin migraciones en un sistema descrito como productivo | `Program.cs` | Corregido — `Migrate()` |
| 16 | Nomenclatura `createdAt` / `isActive` en camelCase | `Models/Afiliado.cs` | Corregido — propiedades renombradas, columnas intactas |
| 17 | **Sin capa de aplicación**: los controladores usan `DbContext` directo y las entidades se filtran como contrato. `ReferenceHandler.IgnoreCycles` es un paliativo | Controllers | Corregido — `Servicios/` + `ServiceResult<T>` |
| 18 | Sin manejo global de errores, sin `ProblemDetails`, sin logging | `Program.cs` | Corregido (logging estructurado: §6) |
| 19 | Sin tests | — | Corregido |
| 20 | CORS `AllowAnyOrigin` y Swagger en todos los ambientes | `Program.cs` | Señalado (§6) |

### Frontend

| # | Problema | Dónde | Estado |
|---|---|---|---|
| 21 | `cancelar()` y `marcarInasistencia()` sin manejo de error y sin refrescar: el usuario hace clic y no pasa nada visible | `ConsultasList.vue:63`, `ConsultaDetalle.vue:77` | Corregido |
| 22 | **Todos los errores son el mismo `alert('Error al procesar la solicitud')`**, descartando el mensaje que el backend ya envía. Sin esto la política de cupo es invisible | Todas las vistas | Corregido |
| 23 | Sin validación en el cliente: el formulario se envía vacío | `ConsultaNueva.vue` | Corregido |
| 24 | `datetime-local` envía fecha local sin zona, alimentando el problema de UTC | `ConsultaNueva.vue:32` | Corregido |
| 25 | Baja de afiliado sin confirmación | `AfiliadosList.vue:32` | Corregido |
| 26 | Sin estados de carga ni botones deshabilitados: permite doble envío | Todas las vistas | Corregido |
| 27 | Selector de estado libre en el detalle, que expone el problema #5 desde la interfaz | `ConsultaDetalle.vue:18` | Corregido |
| 28 | URL del backend fija en el código | `services/api.js:3` | Corregido — variable de entorno |

### Lo que no fue solo corrección

**El ciclo de vida es una tabla, no una cadena de `if`.** Las transiciones
válidas se declaran una vez en `Dominio/CicloDeVidaConsulta.cs` y los cuatro
endpoints la consultan. El endpoint genérico `PUT /estado` sobrevive, pero
validado.

**La capa de servicios cerró un agujero real.** Al mover las reglas a
`Servicios/` se hizo evidente que `PUT /estado` era una vía paralela que
salteaba la ventana de cancelación: la regla vivía en el controlador de
cancelar, no en el dominio. Ahora ningún controlador referencia `AppDbContext`.

**Las entidades dejaron de ser el contrato.** DTOs explícitos de entrada y
salida. `ActualizarAfiliadoRequest` **excluye a propósito** `Bloqueado` y
`ConsultasUsadas`, de modo que no se puedan cambiar de contrabando desde la
edición de la ficha de contacto.

**La baja es lógica y el historial se conserva.** El filtrado de inactivos se
hace en el servicio y no con un filtro global de EF, para que el historial de
una consulta siga mostrando a su afiliado aunque esté dado de baja.

---

## 4. Parte 2 — La política de cupo

> *"El plan incluye 6 consultas de nutrición por año, no más de 1 por mes. Las
> consultas no usadas no se acumulan. Aplica a consultas presenciales."*

### El diseño

La regla **no sabe qué día es hoy ni cómo se cuenta un año**. Recibe el
historial y los períodos ya resueltos, y cuenta:

```
Dominio/Cupo/
  Periodo                  intervalo semiabierto [desde, hasta)
  SolicitudDeCupo          historial + períodos ya resueltos
  DecisionDeCupo           permitida, motivo, estado del cupo

  ICupoPolicy              el límite que un plan impone sobre una especialidad
    └── CupoNutricionPolicy      la regla del enunciado

  IPeriodoCupoProvider     traduce una fecha en (período anual, período mensual)
    ├── PeriodoCalendario        año = [1-ene, 1-ene)        ← adoptado
    └── PeriodoVigencia          año = [aniversario, +1 año) ← implementado y testeado

  CupoNutricionOptions     los valores 6 y 1, con nombre
```

Tres consecuencias prácticas:

1. **Los casos borde se escriben como datos de entrada**, no como estado de base
   de datos. El sexto del año y el segundo del mes son dos líneas de test, no un
   seeder.
2. **La ambigüedad del enunciado está en el código, no solo en este documento.**
   Cambiar de interpretación de "año" es cambiar un proveedor, sin tocar la
   política ni sus tests.
3. **Los límites son configuración con nombre**, así que los tests verifican los
   bordes inyectando otros valores en lugar de sembrar seis consultas para
   probar la séptima.

La interfaz nombra el **rol** (`ICupoPolicy`) y la clase nombra la **variante**
(`CupoNutricionPolicy`), igual que `ILogger` / `ConsoleLogger`. Hay una sola
implementación, deliberadamente: la variación previsible —otra especialidad con
otros números— es configuración, no un subtipo, porque el algoritmo no cambia.

### Dónde se aplica

| Punto | Qué hace |
|---|---|
| `ConsultaService.Agendar` | Única vía de creación. Verifica antes de persistir y responde `409 Conflict` con `ProblemDetails` |
| Tabla de transiciones | `PUT /estado` ya no puede crear una consulta que esquive la verificación |
| `GET /afiliados/{id}/cupo-nutricion` | Expone el estado del cupo con sus fechas, para mostrarlo **antes** de enviar |
| `GET /profesionales` | Devuelve `tieneCupo` resuelto en el servidor contra el código de la especialidad |

El último punto es el que importa para el criterio: la alternativa era que el
frontend comparara `especialidad === 'Nutrición'`, que es exactamente la
fragilidad que el catálogo vino a eliminar del backend.

### El catálogo de especialidades

`Profesional.Especialidad` era texto libre. Hacer que la política compare contra
`"Nutrición"` significa que un error de tipeo al dar de alta un profesional
—`"Nutricionista"`, `"nutricion"`— **desactiva la política en silencio**.

Se modeló como entidad con dos campos de rol distinto:

| Campo | Rol |
|---|---|
| `Codigo` (`"NUT"`) | **Identidad.** Estable, inmutable por contrato. Es contra esto que compara la política |
| `Nombre` (`"Nutrición"`) | **Presentación.** Es lo que muestra la interfaz |

No el `Id`, porque es una clave subrogada: "Nutrición" puede ser `4` en
desarrollo y `11` en producción, y la regla quedaría atada al orden en que se
poblaron las filas. No el nombre, porque un ABM puede editarlo. Es la misma
separación que usan los nomencladores de salud (CIE-10, SNOMED).

**Contra registrada:** el código requiere disciplina para seguir siendo
confiable —índice único, no exponerlo en el ABM, tratarlo como inmutable—. Sin
esas tres cosas es texto libre con otro nombre.

La conversión de los datos existentes quedó en **dos migraciones** y no en una:
la primera convierte y conserva el texto original, la segunda exige que la
conversión haya sido completa. El motivo es que un profesional con una
especialidad que el catálogo no contempla no lo resuelve ningún `UPDATE` —hace
falta que alguien decida, y para eso hace falta un momento en el que mirar los
datos a medio convertir (anexo §3.9).

---

## 5. Supuestos

El requerimiento son cuatro oraciones con **ocho puntos ambiguos**. Cada uno se
resolvió con un criterio explícito; donde la lectura contraria también era
defendible, queda registrada.

Los datos iniciales (`Data/DbSeeder.cs`) sirvieron como evidencia: los ocho
afiliados sembrados cubren, uno por uno, los casos borde de esta política. Se
usaron como especificación ejecutable.

| # | Supuesto | Criterio | Contra registrada |
|---|---|---|---|
| 5.1 | **"Año" = año calendario**, no ventana móvil ni aniversario del alta | *"No se acumulan"* solo tiene sentido con períodos que **cierran**. Con ventana móvil no hay nada que acumular y la cláusula queda sin efecto. El afiliado 8 discrimina entre las tres lecturas, y el seeder describe su consulta como *"del año anterior"*: vocabulario de períodos discretos | Es **inequitativo para quien se afilia tarde**: alta el 15 de noviembre son 6 consultas que vencen el 31 de diciembre y, por la regla mensual, solo 2 usables. El año de vigencia lo corrige y el dato ya está en el modelo; queda como punto de configuración |
| 5.2 | **"Mes" = mes calendario**, no mínimo de 30 días | Con 30 días **los datos sembrados serían inválidos**: el afiliado 3, descrito como el caso de uso correcto, tiene consultas el día 10 de cada mes y de febrero a marzo hay 28 días. El caso de referencia violaría la política que se implementa | — |
| 5.3 | Consumen cupo `Pendiente`, `Confirmado` y `Atendido` | Si `Pendiente` no consumiera, un afiliado reserva seis turnos sin consumir nada y la política no tiene efecto. El cupo se reserva al agendar | — |
| 5.4 | **`Inasistencia` consume cupo** | La consulta reservó una franja en la agenda del profesional y nadie más pudo usarla. La política protege un recurso escaso y el recurso se consumió | Es defendible lo opuesto: el afiliado no recibió la prestación y el plan "incluye 6 consultas" en el sentido de seis atenciones efectivas. El cambio es un predicado y su test |
| 5.5 | El cupo se evalúa contra la **fecha solicitada**, no la fecha actual | Agendar en diciembre para enero debe consumir el cupo de enero. Así *"no se acumulan"* se cumple sin lógica adicional: cada consulta pesa sobre el período en que ocurre | — |
| 5.6 | Solo **presencial** se limita **y consume** | Si una virtual consumiera sin estar limitada, el afiliado perdería cupo presencial por una prestación que la política declara fuera de su alcance | — |
| 5.7 | **`Cancelado` es terminal**: no se reactiva | El riesgo era que `PUT /estado` fuera la vía de evasión: cancelar, liberar cupo, agendar otra, reactivar la cancelada. La primera versión revalidaba el cupo al reactivar; la tabla de transiciones mostró la respuesta más simple: si reactivar exige repetir **todas** las validaciones de agendar, lo correcto es agendar de nuevo | Una cancelación por error operativo obliga a agendar de nuevo y la original conserva un desenlace que no ocurrió. Se acepta: el historial refleja lo que el sistema registró, trazable como dos registros en lugar de uno reescrito |
| 5.8 | La especialidad se identifica por **código**, no por nombre ni `Id` | Ver §4 | Requiere disciplina (índice único, inmutabilidad) para no ser texto libre con otro nombre |
| 5.9 | `ConsultasUsadas` **no es fuente de verdad** | Nadie lo actualiza y no distingue especialidad, modalidad ni período — las tres dimensiones que la política necesita. Un contador desnormalizado que puede divergir del historial es error silencioso | — |
| 5.10 | Los límites son **configuración con nombre**, no literales en la regla | Permite verificar los bordes inyectando otros valores | — |
| 5.11 | Exceder el cupo responde **`409 Conflict`** | La petición es sintácticamente válida y está autorizada; lo que falla es que el estado del sistema no permite la operación. `400` se reserva para entrada malformada | — |

---

## 6. Fuera de alcance

| Tema | Por qué queda afuera |
|---|---|
| **Cupo por plan** | El cupo es conceptualmente un atributo del plan, y el diseño correcto a escala es una relación `Plan × Especialidad → Cupo` persistida. Pero existe **un solo plan y una sola especialidad con cupo**: construir un motor configurable sin un segundo caso real es diseñar contra un requerimiento que nadie formuló. El catálogo deja el camino abierto |
| **Operación de bloqueo** | `Bloqueado` **sí se respeta** (un afiliado bloqueado no puede agendar); `FechaBloqueo` no lo escribe nadie porque **no hay operación que bloquee**. Agregarla es simple, pero la pregunta previa es de negocio: quién puede bloquear, por qué motivos y si el motivo se registra |
| **Condición de carrera al agendar** | Dos peticiones simultáneas pueden pasar ambas la validación antes de que cualquiera persista. La solución combina transacción, revalidación e índice único; hacerlo bien exige decisiones de infraestructura (aislamiento, reintentos) fuera del alcance. Lo mismo aplica a la superposición de turnos (#10): se corrige la comparación, pero modelar duración y agenda es otro requerimiento |
| **Autenticación y autorización** | El sistema no tiene identidad de usuario. Marcar inasistencia, cambiar estado y dar de baja son acciones de personal interno, no del afiliado. Modelarlo requiere definir roles y un flujo que el enunciado no plantea |
| **Endurecimiento para producción** | `AllowAnyOrigin` y Swagger en todos los ambientes son inadecuados para producción, pero corregirlos exige conocer los orígenes reales y la estrategia de ambientes. Se señalan sin modificar |
| **Paginación** | `GET /afiliados` y `GET /consultas` devuelven la tabla completa. Con volumen real hace falta paginación y proyección; afecta el contrato que consume el frontend |
| **Reactivación de afiliados desde la UI** | El endpoint existe (`POST /afiliados/{id}/reactivacion`) pero la lista solo muestra activos. Completarlo requiere un filtro "incluir dados de baja", que es decisión de producto |
| **Librería de componentes de la organización** | Es privada, se distribuye como fuentes crudas y está construida sobre Quasar. Adoptarla dejaría este repositorio **público sin poder construirse** para quien no tenga acceso al registry interno. Se replicaron las convenciones visuales con CSS propio y cero dependencias nuevas |
| **Tests de componentes del frontend** | Se testeó la capa con lógica propia (traducción de errores, formateo de fechas). Los componentes verifican presentación sobre contratos ya verificados del lado del backend |
| **Logging estructurado** | Hay logging y manejo global de errores; sink, correlación y niveles por ambiente son configuración de operación |

---

## 7. Cómo ejecutar

**Requisitos:** .NET 8 SDK · Node 18+

```bash
# Backend — http://localhost:5000 (Swagger en /swagger)
cd backend
dotnet run

# Tests del backend
dotnet test

# Frontend — http://localhost:5173
cd frontend
npm install
npm run dev

# Tests del frontend
npm test
```

La base SQLite se crea y migra al arrancar (`Migrate()`), y se siembra con los
ocho afiliados que cubren los casos borde de la política. Para empezar de cero,
borrar el archivo `.db` del directorio del backend.

El frontend lee la URL de la API de `VITE_API_URL` (`frontend/.env`).
