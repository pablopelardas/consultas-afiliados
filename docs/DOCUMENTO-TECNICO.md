# Documento técnico — Challenge Desarrollador .NET

**Autor:** Pablo Pelardas
**Stack:** .NET 8 (ASP.NET Core · EF Core · SQLite) + Vue 3 (Vite)
**Repositorio:** https://github.com/pablopelardas/consultas-afiliados

> Escrito en paralelo al código: cada decisión quedó anotada en el momento en
> que se tomó, no reconstruida al cierre. Por eso incluye los supuestos tal como
> se resolvieron y los errores que aparecieron en el camino.

---

## 1. Resumen ejecutivo

El challenge tiene dos partes: intervenir una base de código existente y
agregar una política de cupo de consultas de nutrición.

El punto de partida es que **el código base no compila**. A partir de ahí, el
trabajo se organizó en dos ejes:

1. **Corrección.** Los problemas encontrados se agrupan por naturaleza
   (compilación, semántica de operaciones, manejo de fechas, ciclo de vida,
   integridad del historial, contratos de API) y se corrigen en ramas
   independientes.
2. **Diseño para la nueva funcionalidad.** La política de cupo se implementa
   como una **regla pura** — sin acceso a base de datos ni efectos secundarios —
   detrás de una interfaz, de modo que toda la lógica de negocio sea verificable
   sin levantar infraestructura.

### Resultado

```
dotnet test   →  164 passed, 0 failed
npm test      →    8 passed, 0 failed
```

**28 problemas** relevados en el código base: 24 corregidos, 3 con alcance
acotado y declarado, 1 fuera de alcance con su razón (§4).

Lo que conviene mirar primero, porque es donde está el criterio y no solo la
corrección:

| Dónde | Qué decidió |
|---|---|
| §3.5 | El ciclo de vida de la consulta como tabla de transiciones, no como `if` por endpoint |
| §3.8 | Una capa de servicios que cerró un agujero real: `PUT /estado` salteaba la ventana de cancelación |
| §3.9 | La política de cupo como función pura, con la ambigüedad del enunciado materializada en dos implementaciones |
| §3.10 | Que el cliente pregunte las reglas en vez de copiarlas |
| §5 | Los once supuestos, cada uno con la alternativa que se descartó |

Tres hallazgos que no venían del enunciado y cambiaron el código:

- Un **test de propiedad** encontró que `PeriodoVigencia` dejaba tres días al
  año sin período para ciertos afiliados, y un hueco cada cuatro años para
  quien se da de alta un 29 de febrero (§3.9).
- Migrar el catálogo **en un solo paso perdía datos** en el caso que ninguna
  migración resuelve sola: un profesional con una especialidad que el catálogo
  no contempla. De ahí las dos etapas con una consulta de control en el medio
  (§3.9).
- **Abrir el navegador** encontró dos defectos que el build y los 164 tests no
  veían: una fecha de calendario que se corría un día y una vista que quedaba
  mostrando el registro anterior (§3.10).

---

## 2. Metodología de trabajo

### Ramas

El primer commit de `main` es el **código base sin modificar**. Eso convierte a
`main` en la línea base de comparación: todo diff posterior es intervención
propia y se lee aislado.

Las correcciones y la nueva funcionalidad van en **ramas separadas**, una por naturaleza de cambio, cada una con su Pull Request.

```
main                            línea base + merges

chore/setup-proyecto            normalización, convenciones, proyecto de tests
fix/errores-compilacion         los dos errores que impiden compilar
fix/manejo-fechas-utc           TimeProvider + conversión UTC en EF
fix/semantica-operaciones       verbos HTTP, códigos de respuesta, ProblemDetails
fix/ciclo-vida-consulta         transiciones de estado válidas
fix/historial-afiliados         baja lógica en lugar de baja física
fix/contratos-api               DTOs de entrada/salida y validación
refactor/capa-servicios         interfaces de servicio y resultado tipado
feat/cupo-nutricion             política de cupo (Parte 2)
fix/ux-manejo-errores           frontend: errores visibles y validación en cliente
```

Convención de commits: [Conventional Commits](https://www.conventionalcommits.org/).

### Verificación

Cada corrección arranca por un test que **falla** y describe el comportamiento actual. Para el código base existente se escribieron **tests de caracterización**: tests
que documentan el comportamiento tal como es hoy, incluyendo el incorrecto. Sin
esa red, cada corrección posterior es una apuesta sobre qué más se rompió.

---

## 3. Cambios realizados

### 3.1 Errores de compilación — `fix/errores-compilacion`

`dotnet build` sobre el código base devuelve **2 errores `CS1061`**.

| Ubicación | Problema | Corrección |
|---|---|---|
| `Controllers/AfiliadosController.cs:50` | `Update` asigna `afiliado.Nombre`, propiedad que no existe en el modelo `Afiliado` | `afiliado.Nombre` → `afiliado.NombreCompleto` |
| `Controllers/ConsultasController.cs:102` | `RegistrarAtencion` invoca la extensión `IsInCurrentYear()` sin importar su namespace | `+ using ConsultasPlan.Helpers;` |

Sobre el segundo: `ImplicitUsings` está habilitado en el `.csproj`, pero solo
cubre los namespaces de `System` y ASP.NET Core — **no los del propio
proyecto**. La extensión vive en `ConsultasPlan.Helpers` y requiere `using`
explícito.

El alcance de esa rama se limitó a lo que impide compilar. Ningún otro problema
se tocó ahí, para que el diff que habilita la compilación se lea solo.

**Verificación:** `dotnet build` → 0 errores, 0 warnings. `dotnet run` → API
operativa, seeder ejecutado (3 sedes, 8 afiliados, 20 consultas).

Un build verde no era verificación suficiente: había que confirmar que el
seeder corre y la API responde.

### 3.2 Normalización y red de tests — `chore/setup-proyecto`

**`.gitattributes`** con `* text=auto eol=lf`. El repositorio no definía
finales de línea, lo que produce diffs de archivos completos al clonar en
Windows y cambiar `core.autocrlf`. Normalizar en el índice hace el repositorio
portable sin depender de la configuración local de cada colaborador.

**`.editorconfig`** con las convenciones de .NET. Se incluye deliberadamente la
regla de nomenclatura `PascalCase` para miembros públicos con severidad
`warning`: el modelo `Afiliado` declara `createdAt` e `isActive` en camelCase,
violando la convención del lenguaje. La regla vuelve eso un warning visible en
cada build en lugar de un detalle que se arrastra.

**Reubicación de la solución.** `ConsultasPlan.sln` estaba dentro de `backend/`
y referenciaba un único proyecto. El `.csproj` es SDK-style, por lo que incluye
`**/*.cs` recursivamente: un proyecto de tests dentro de `backend/` haría que la
API intentara compilar los tests. La solución subió a la raíz y el proyecto de
tests vive en `tests/`.

```
/
├── ConsultasPlan.sln
├── backend/                     API
├── frontend/                    SPA
├── tests/ConsultasPlan.Tests/   tests
└── docs/                        este documento
```

La solución se generó en el **formato clásico `.sln`** de forma deliberada. El
SDK 10 crea por defecto el formato nuevo `.slnx`, que requiere SDK 9.0.200 o
superior: con eso, un evaluador con SDK 8 —el que declara el enunciado— no
podría ejecutar `dotnet test` desde la raíz. El formato clásico lo entienden
todas las versiones.

**Proyecto de tests:** xUnit sobre `net8.0`, con

| Paquete | Para qué |
|---|---|
| `Microsoft.EntityFrameworkCore.InMemory` | Tests de lógica sin levantar base de datos |
| `Microsoft.AspNetCore.Mvc.Testing` | Tests de integración sobre el pipeline HTTP real: ruteo, verbos y códigos de respuesta |
| `Microsoft.Extensions.TimeProvider.Testing` | `FakeTimeProvider`, para controlar el reloj y poder verificar los bordes exactos de las reglas temporales |

Las versiones de EF Core y Mvc.Testing quedan clavadas en `8.0.11`, la misma
que usa la API, para no introducir divergencia entre lo que se testea y lo que
corre.

No se incorporó `FluentAssertions`: desde la versión 8 su licencia exige pago
para uso comercial. Se usan las aserciones de xUnit, que cubren el caso sin
agregar una dependencia con condiciones de uso.

**Tests de caracterización.** Ocho tests que documentan el comportamiento
actual del código base, incluido el incorrecto:

| Test | Problema que evidencia |
|---|---|
| Cancelar a 23:30 de la consulta es aceptado | #4 — el código exige 23 h de anticipación y el mensaje promete 24: en esa hora de diferencia el sistema acepta lo que dice rechazar |
| Marcar inasistencia sobre una consulta cancelada es aceptado | #6 |
| Registrar atención sobre una consulta cancelada es aceptado | #6 |
| Actualizar estado permite volver de `Atendido` a `Pendiente` | #5 |
| Actualizar estado acepta un valor que no pertenece al enum | #5 |
| Crear consulta con fecha en el pasado es aceptado | #9 |
| Crear consulta un minuto después de otra del mismo profesional es aceptado | #10 |
| Crear consulta con un afiliado anidado persiste un afiliado nuevo | #8 — over-posting |

Los ocho **pasan** contra el código base. Eso es el punto: un test de
caracterización no afirma que la conducta sea correcta, afirma que es la que
hay. Cuando la rama de corrección cambie el comportamiento, el test fallará, y
ese fallo es la confirmación de que el arreglo tuvo efecto.

Los datos de prueba no reutilizan `DbSeeder`: sus fechas se derivan de
`DateTime.Now`, por lo que los datos cambian según el día de ejecución. Un test
cuyo resultado depende de cuándo se corre no verifica nada.

### 3.3 Manejo de fechas — `fix/manejo-fechas-utc`

Dos defectos distintos con la misma raíz: el sistema no tenía una noción única
de "ahora" ni de en qué zona persiste.

#### El desvío por huso horario

El código base **escribía en UTC y comparaba contra hora local**:

| Operación | Código base |
|---|---|
| `AfiliadosController.Create` | `afiliado.createdAt = DateTime.UtcNow` |
| `ConsultasController.CrearConsulta` | `consulta.FechaCreacion = DateTime.UtcNow` |
| `ConsultasController.CancelarConsulta` | `consulta.FechaHora - DateTime.Now` |
| `ConsultasController.MarcarInasistencia` | `consulta.FechaHora > DateTime.Now` |
| `DateTimeExtensions.IsInCurrentYear` | `fecha.Year == DateTime.Now.Year` |

En Argentina eso son **tres horas de desvío** sobre la ventana de cancelación y
sobre el cálculo de "año en curso". No produce ningún error: produce una
respuesta equivocada.

#### La pérdida de `DateTimeKind` en SQLite

SQLite no tiene tipo fecha: guarda texto sin zona. EF Core devuelve esos
valores con `DateTimeKind.Unspecified`, y .NET trata lo `Unspecified` como hora
local al convertir. El resultado es que un instante guardado en UTC se
reinterpreta como local al leerlo, con un desvío igual al huso del servidor,
**sin error visible en ningún punto**.

La corrección es un `ValueConverter` aplicado por convención a todas las
propiedades de tipo fecha, en lugar de recordarlo propiedad por propiedad:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
{
    builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    builder.Properties<DateTime?>().HaveConversion<UtcNullableDateTimeConverter>();
}
```

Un `DateTime` con `Kind` `Unspecified` se asume UTC en lugar de convertirse
desde hora local: así el instante almacenado no depende del huso del servidor
que recibió la petición. Lo estricto sería **rechazar** fechas sin zona en el
borde de la API; se difiere a la rama de validación de contratos.

#### El reloj como dependencia

`TimeProvider` se registra en el contenedor y se inyecta donde se necesita
saber qué hora es. `TimeProvider.System` en producción; `FakeTimeProvider` en
los tests.

Esto no es ceremonia: **sin un reloj controlable, el borde de una regla
temporal no se puede verificar**. Probar que la ventana de cancelación corta
exactamente a las 24 horas exigiría esperar a que el reloj del sistema llegue
a ese instante. Con el reloj inyectado, el borde es un parámetro del test.

Es también la base de la Parte 2: la política de cupo depende de en qué año y
en qué mes cae una fecha, y sus bordes —31 de diciembre, último día del mes—
requieren exactamente esta capacidad.

#### La ventana de cancelación (problema #4)

Se corrige en esta rama porque es un defecto de cálculo temporal y porque su
verificación depende del reloj inyectado.

El código validaba `TimeSpan.FromHours(23)` mientras el mensaje al usuario
prometía 24 horas. En la franja `[23 h, 24 h)` el sistema **aceptaba la
cancelación mientras mostraba el cartel que decía rechazarla**. El límite pasa
a 24 horas, nombrado en una constante en lugar de ser un literal dentro de la
condición.

#### Flujo completo

El input `datetime-local` del formulario entrega `"2026-12-01T10:00"`, sin
zona. Enviado así, el backend no puede saber a qué instante se refiere. El
frontend lo convierte a ISO-8601 con sufijo `Z` antes de enviarlo.

```
navegador          datetime-local → new Date(...).toISOString() → "...T13:30:00Z"
     │
API                TimeProvider.GetUtcNow() para todo "ahora"
     │
EF Core            UtcDateTimeConverter por convención
     │
SQLite             texto sin zona — pero siempre el mismo instante
     │
respuesta          "2027-03-15T13:30:00Z"  ← con marca de zona
     │
navegador          new Date(...).toLocaleString('es-AR') → hora local correcta
```

**Verificación del round-trip** contra la API corriendo:

```
POST /consultas   fechaHora: "2027-03-15T13:30:00Z"
GET  /consultas   fechaHora: "2027-03-15T13:30:00Z"   ← idéntico, con sufijo Z
```

Antes de la corrección la respuesta no llevaba sufijo, por lo que el navegador
la interpretaba como hora local y mostraba un instante distinto del almacenado.

#### Tests

Los tests de persistencia usan **SQLite en memoria, no el proveedor InMemory de
EF**. El problema que verifican es específico de SQLite —que no tiene tipo
fecha—, y el proveedor InMemory conserva los objetos .NET tal cual: no
reproduce la pérdida de `DateTimeKind` y daría verde sin probar nada.

El test de la ventana de cancelación recorre `48 h`, `25 h`, `24 h`, `23 h` y
`12 h` de anticipación, más el caso de `23 h 59 min`, que es donde el límite se
cruza por un minuto.

### 3.4 Semántica de las operaciones — `fix/semantica-operaciones`

#### El `GET` que cancelaba consultas

El código base exponía la cancelación como:

```csharp
[HttpGet("cancelar/{id}")]
public async Task<IActionResult> CancelarConsulta(int id)
```

GET está definido por especificación como **seguro**: no debe producir efectos
sobre el servidor. No es una convención de estilo — es un contrato del que
depende toda la infraestructura web. Navegadores que precargan enlaces,
aceleradores de navegación, proxies que cachean, antivirus corporativos que
siguen URLs y rastreadores asumen que un GET se puede repetir sin consecuencias.

Con esa ruta, una etiqueta `<img src="...">` en cualquier página, un enlace
precargado o un crawler cancelaban consultas médicas sin que nadie lo pidiera.
Es el defecto más grave del código base, y no produce ningún error: produce
cancelaciones.

| Antes | Ahora |
|---|---|
| `GET /consultas/cancelar/{id}` | `POST /consultas/{id}/cancelacion` |

La ruta nueva sigue la forma que el propio controlador ya usaba para las demás
operaciones de la consulta (`/inasistencia`, `/atencion`): un sub-recurso en
sustantivo sobre POST. El código base era inconsistente consigo mismo.

Es un cambio de contrato. El único consumidor es el frontend, que se actualiza
en el mismo PR.

#### Códigos de respuesta

El código base devolvía `400 Bad Request` para toda regla de negocio incumplida.
`400` significa que la petición está mal formada —el cliente la escribió mal— y
eso no es lo que ocurre cuando un afiliado está bloqueado o cuando faltan menos
de 24 horas: ahí la petición es impecable y lo que impide la operación es el
**estado del sistema**. Esa distinción es la que le permite a un cliente decidir
si reintentar tiene sentido.

| Situación | Antes | Ahora |
|---|---|---|
| Afiliado o profesional inexistente | `404` | `404` |
| Afiliado bloqueado | `400` | `409` |
| Horario ocupado por el profesional | `400` | `409` |
| Cancelación fuera de plazo | `400` | `409` |
| Inasistencia sobre consulta futura | `400` | `409` |
| Atención de una consulta de otro año | `400` | `409` |
| Estado fuera del enum | `200` ✗ | `400` |

El último invierte el criterio a propósito: un entero que no pertenece a
`EstadoConsulta` **sí** es entrada malformada. El deserializador lo aceptaba y
lo persistía, dejando la consulta en un estado que no existe en el dominio.

Es el mismo criterio que se aplicará al cupo excedido (§5.11).

#### Formato de error

Las respuestas de error devolvían objetos anónimos (`new { mensaje = "..." }`),
distintos según el endpoint y sin contrato declarado. Pasan a `ProblemDetails`
(RFC 7807), que es el formato estándar de la plataforma: `title`, `detail`,
`status`, con `Content-Type: application/problem+json`.

El cliente lee siempre el mismo campo para mostrarle algo útil al usuario —
habilitante directo de la corrección del frontend, donde hoy todo error termina
en el mismo `alert` genérico.

Se agrega además `UseExceptionHandler`, para que una excepción no controlada
produzca un `500` con `ProblemDetails` en lugar de una página de error o un
stack trace.

#### Tests

Estos tests **atraviesan el pipeline HTTP** con `WebApplicationFactory`, no
instancian el controlador. La razón es precisa: que una ruta acepte o rechace
GET lo decide el ruteo, no el cuerpo del método. Llamando al método
directamente, `Cancelar_ya_no_responde_a_GET` sería imposible de escribir.

La API de prueba sustituye SQLite por una base en memoria y el reloj por
`FakeTimeProvider`, usando `ConfigureTestServices` — que corre *después* de los
registros de la aplicación y por lo tanto efectivamente reemplaza al original.

### 3.5 Ciclo de vida de la consulta — `fix/ciclo-vida-consulta`

El código base no tenía ninguna noción de qué cambios de estado son posibles:
cada endpoint asignaba `consulta.Estado` directamente. Se podía registrar una
inasistencia sobre una consulta cancelada, registrar la atención de una
cancelada, o devolver a `Pendiente` una ya atendida.

#### La tabla

| Desde | Hacia | Por qué |
|---|---|---|
| `Pendiente` | `Confirmado`, `Cancelado`, `Atendido`, `Inasistencia` | El turno existe y todavía no ocurrió. El afiliado puede no confirmar y aun así ser atendido |
| `Confirmado` | `Cancelado`, `Atendido`, `Inasistencia` | No vuelve a `Pendiente`: retroceder no describe ningún hecho ocurrido |
| `Atendido` | — | Terminal |
| `Cancelado` | — | Terminal (§5.7) |
| `Inasistencia` | `Atendido` | Única corrección admitida |

**Ningún estado transiciona hacia sí mismo.** Cancelar una consulta ya cancelada
devuelve `409` con un mensaje explícito, en lugar de fingir éxito. Para quien
opera el sistema, saber que no pasó nada es información; un `200` que no cambió
nada, no.

Sobre `Inasistencia → Atendido`: una inasistencia mal cargada **perjudica al
afiliado** —consume cupo y puede derivar en bloqueo—, así que no puede ser
irreversible. Es la única corrección prevista, y es deliberadamente angosta.

#### Dónde vive la regla

`CicloDeVidaConsulta` es una clase de dominio **pura**: no accede a la base, no
produce efectos y no depende del reloj. Recibe dos estados y responde si el
paso entre ellos existe.

Eso permite verificar la tabla completa —todas las combinaciones de cinco
estados— sin levantar infraestructura, en tests que describen el dominio en
lugar de la mecánica de montarlo. Los cuatro endpoints la consultan antes de
asignar.

Es además el patrón que usará la política de cupo de la Parte 2: regla pura
detrás de una interfaz, orquestación afuera.

#### El endpoint genérico sobrevive, validado

`PUT /{id}/estado` no se elimina. El flujo operativo del personal
—`Pendiente → Confirmado → Atendido`— necesita una vía directa, y las
transiciones válidas no se tocaron. Lo que cambia es que ahora consulta la
tabla: dejó de ser la puerta por la que se evadían las reglas del dominio.

### 3.6 Integridad del historial — `fix/historial-afiliados`

El código base ejecutaba `Remove` sobre el afiliado. Con la clave foránea en
`OnDelete(SetNull)`, eso dejaba **sus consultas sin paciente**: se perdía de
forma irreversible a quién correspondía cada atención médica.

```
antes      DELETE /afiliados/3
           → el registro desaparece
           → sus 6 consultas quedan con AfiliadoId = NULL
           → el historial clínico sigue ahí, sin dueño

ahora      DELETE /afiliados/3
           → Activo = false, FechaBaja = <ahora>
           → el afiliado desaparece de las vistas operativas
           → sus 6 consultas conservan nombre, DNI y vínculo
```

Y el modelo **ya traía el campo** para hacerlo bien. `isActive` existía desde el
principio y nadie lo usaba.

#### Dónde se filtra, y por qué no con un filtro global

EF Core ofrece `HasQueryFilter`, que aplicaría la condición a todas las
consultas automáticamente. Se descartó.

Un filtro global sobre `Afiliado` también alcanza a
`Include(c => c.Afiliado)` al leer consultas: las consultas de alguien dado de
baja quedarían **sin afiliado**, y el historial clínico perdería el nombre del
paciente. Es exactamente la integridad que la baja lógica viene a proteger.

El criterio es comparar qué pasa cuando alguien se olvida:

| Enfoque | Si se olvida | Consecuencia |
|---|---|---|
| Filtro explícito | Falta un `Where(a => a.Activo)` | Se lista a alguien de más. Visible, molesto |
| Filtro global | Falta un `IgnoreQueryFilters()` | **Se pierde un historial.** Silencioso |

El segundo error es más grave y más difícil de notar. El filtro vive donde se
decide a quién se lista como afiliado vigente.

#### Alcance del filtrado

| Operación | Comportamiento |
|---|---|
| `GET /afiliados` | Solo vigentes |
| `GET /afiliados/{id}` | `404` si está dado de baja |
| `PUT /afiliados/{id}` | `404` si está dado de baja |
| `DELETE /afiliados/{id}` | Baja lógica. `409` si ya estaba de baja |
| `POST /afiliados/{id}/reactivacion` | Revierte la baja. `409` si ya estaba activo |
| `POST /consultas` | `404` si el afiliado está dado de baja |
| `GET /consultas` | **Sin filtrar.** El historial se conserva completo |

La última fila es el punto: las consultas de un afiliado dado de baja **siguen
apareciendo con su paciente**. La baja lo saca de la operación, no de la
historia.

Agendar merece una aclaración: el id de un afiliado dado de baja sigue
existiendo aunque no figure en ninguna vista, así que `POST /consultas` tiene
que negarse explícitamente. Devuelve `404` y no `409` porque, desde la
perspectiva de quien agenda, ese afiliado no existe.

#### La reactivación

Una baja que no se puede deshacer es, en la práctica, un borrado con otro
nombre que además ocupa lugar. `POST /afiliados/{id}/reactivacion` es lo que
hace que la baja lógica sea realmente lógica.

#### Nomenclatura y clave foránea

Aprovechando que la rama trabaja sobre este modelo, `createdAt` e `isActive`
pasan a `FechaAlta` y `Activo` (problema #16). **Las columnas conservan su
nombre** mediante `HasColumnName`: la convención del lenguaje se corrige en el
código sin requerir una migración de esquema sobre un sistema en producción.

La clave foránea pasa de `SetNull` a `Restrict`. Con baja lógica el borrado
físico ya no es parte del flujo, y si alguien lo intentara la base debe negarse
en lugar de dejar consultas sin afiliado. Es defensa en profundidad sobre la
misma decisión que toma el controlador.

#### Frontend

El botón decía "Eliminar" y ya no elimina. Pasa a "Dar de baja", y el método del
cliente se renombra en consecuencia. El texto de un botón es parte del contrato
con quien lo usa: si dice que borra y da de baja, la próxima decisión que tome
el operador va a estar basada en algo falso.

### 3.7 Contratos de la API — `fix/contratos-api`

#### Entrada: el over-posting

El código base recibía las entidades de EF directamente:

```csharp
public async Task<IActionResult> CrearConsulta([FromBody] Consulta consulta)
public async Task<IActionResult> Create([FromBody] Afiliado afiliado)
```

Eso expone **todas** las propiedades de la entidad como campos de entrada. Un
cliente podía enviar `Bloqueado`, `ConsultasUsadas`, `Activo`, `Estado` o un
objeto `Afiliado` anidado, y EF lo persistía. El test de caracterización lo
demostraba: enviar una consulta con un afiliado anidado **creaba un afiliado
nuevo** que nadie pidió.

La corrección no es agregar comprobaciones. Es declarar qué acepta cada
operación:

| Operación | Contrato |
|---|---|
| `POST /consultas` | `CrearConsultaRequest` |
| `POST /afiliados` | `CrearAfiliadoRequest` |
| `PUT /afiliados/{id}` | `ActualizarAfiliadoRequest` |
| `PUT /consultas/{id}/estado` | `ActualizarEstadoRequest` |

Lo que no está en el contrato **no entra**, y no porque alguien se acordó de
comprobarlo, sino porque no existe en el tipo. El test de caracterización del
afiliado anidado ya no se puede siquiera escribir: la propiedad no existe.

`ActualizarAfiliadoRequest` deja afuera `Plan`, `Bloqueado`, `ConsultasUsadas`
y `Activo` a propósito. Cambiar el plan, bloquear o dar de baja son operaciones
con reglas propias, no una edición de ficha de contacto.

#### Validación

Las reglas de forma viven en el contrato como atributos, de modo que
`[ApiController]` las aplique antes de entrar al método:

| Campo | Regla |
|---|---|
| `NombreCompleto` | Obligatorio, 3 a 120 caracteres |
| `DNI` | Obligatorio, 7 u 8 dígitos sin puntos ni espacios |
| `Email` | Obligatorio, formato válido |
| `Telefono` | Obligatorio, 8 a 15 dígitos |
| `Motivo` | Obligatorio, 3 a 300 caracteres |
| `Modalidad` / `Estado` | Debe pertenecer al enum |

Dos reglas no son de forma y viven en el controlador:

- **La fecha debe ser futura.** Depende del reloj, que es una dependencia
  inyectada; un atributo no puede acceder a él. El código base aceptaba agendar
  en 1990.
- **El DNI es único.** El documento identifica a la persona: dos registros con
  el mismo DNI son el mismo afiliado cargado dos veces, y a partir de ahí el
  historial clínico y el cupo quedan partidos entre dos afiliados que son uno
  solo. Devuelve `409`, porque es un conflicto con el estado existente.

#### Salida

Devolver entidades de EF tiene tres consecuencias: expone campos internos
(`Activo`, `FechaBaja`, `SedeId`), **ata el contrato público al esquema** —un
cambio del modelo rompe a los clientes sin aviso— y arrastra el grafo de
navegación completo.

Esto último explica una línea del código base:

```csharp
options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
```

Ese ajuste era un paliativo para poder serializar entidades con navegación
circular. **Se elimina**: los contratos de salida no tienen ciclos porque no
los necesitan. Cuando la causa se corrige, el síntoma y su parche desaparecen
juntos.

Los contratos preservan exactamente las formas que el frontend ya consume, de
modo que el cambio no lo rompe.

---

### 3.8 Capa de servicios — `refactor/capa-servicios`

Hasta acá los controladores hacían tres trabajos a la vez: leer y escribir en la
base, decidir las reglas de negocio y traducir el resultado a HTTP. Esta rama
los deja con uno solo.

#### El problema de fondo

No es "mucho código en el controlador". Es que **las reglas de negocio solo
existen dentro de un método de controlador**, y eso tiene tres consecuencias
concretas:

1. **No se pueden reusar.** Si mañana la cancelación también se dispara desde un
   job nocturno o un comando de consola, hay que reescribir la regla o invocar
   un controlador desde fuera de HTTP.
2. **No se pueden verificar sin traducir.** Para afirmar "no se cancela con 12
   horas de anticipación" había que afirmar "devuelve 409". La regla no habla de
   códigos de estado; el test sí estaba obligado a hablarlos.
3. **Se pueden duplicar en silencio.** Y se duplicaron: la misma regla escrita
   en dos endpoints se desincroniza en cuanto alguien toca uno.

#### Las tres piezas

**`ServiceResult<T>` — el resultado de una operación de dominio.**

Una operación de negocio tiene tres desenlaces posibles: salió bien y devuelve
algo, o no salió y hay un motivo. Las alternativas habituales para expresarlo
son peores:

| Alternativa | Por qué no |
|---|---|
| Devolver `null` en el error | Un `null` no distingue "no existe" de "no se permite", y el consumidor se olvida de chequearlo |
| Lanzar excepciones | Una regla de negocio que se cumple todos los días no es una condición excepcional; además el flujo de control queda invisible en la firma |
| Devolver `IActionResult` | Ata el servicio a HTTP: es exactamente lo que se quiere romper |

`ServiceResult<T>` dice en la firma que la operación puede no completarse, y
obliga a mirar el motivo antes de leer el valor:

```csharp
public T Valor => EsExito
    ? _valor!
    : throw new InvalidOperationException(
        $"La operación no tuvo éxito ({Falla!.Tipo}): no hay valor que leer.");
```

Lanza a propósito. Leer el valor de una operación fallida no es un caso a
manejar, es un error de programación: si devolviera `null`, el defecto
aparecería mucho más adelante y en otro lugar.

**`Falla` — el motivo, nombrado en términos de dominio.**

```csharp
public enum TipoDeFalla { NoEncontrado, Conflicto, EntradaInvalida }
```

Son tres, no treinta, porque son las tres situaciones que un consumidor trata
distinto. El servicio nombra el motivo; **traducirlo a un código HTTP es del
controlador**.

**Interfaces — `IConsultaService`, `IAfiliadoService`, `ICatalogoService`.**

El controlador depende del contrato, no de la implementación. Eso no es
ceremonia: es lo que permite el test de la sección siguiente.

#### Qué quedó en el controlador

Exactamente una cosa, y está en un único lugar:

```csharp
// ApiControllerBase
protected IActionResult Problema(Falla falla)
{
    if (falla.Tipo == TipoDeFalla.EntradaInvalida) { /* 400 con ModelState */ }

    var (codigo, titulo) = falla.Tipo switch
    {
        TipoDeFalla.NoEncontrado => (404, "Recurso no encontrado"),
        TipoDeFalla.Conflicto    => (409, "La operación no es posible en el estado actual"),
        ...
    };

    return Problem(detail: falla.Detalle, statusCode: codigo, title: titulo);
}
```

Un solo lugar donde se elige el código de estado. Si cada controlador escribiera
su propio `Problem(...)`, el formato de error volvería a ser distinto por
endpoint — que es de donde se venía.

Los endpoints quedan así:

```csharp
[HttpPost("{id}/cancelacion")]
public async Task<IActionResult> CancelarConsulta(int id) =>
    Responder(await _consultas.Cancelar(id));
```

**Invariante verificable del refactor:** ningún controlador referencia
`AppDbContext` ni `Microsoft.EntityFrameworkCore`. Por eso `SedesController` y
`ProfesionalesController`, que son dos lecturas sin reglas, también pasan por
`ICatalogoService`: dos controladores con acceso directo a la persistencia
alcanzan para que la invariante no sea una. Se agrupan en un solo servicio en
lugar de uno por tabla, porque un servicio por catálogo serían archivos que no
dicen nada.

#### Un agujero que el refactor cerró

El código base tenía el endpoint genérico `PUT /consultas/{id}/estado`, que
asignaba el estado sin validar nada. En §3.5 se le agregó la tabla de
transiciones, pero seguía **salteando las reglas temporales**: con
`{"estado": "Cancelado"}` se cancelaba una consulta de las próximas 12 horas,
evadiendo la ventana de 24 que `POST /cancelacion` sí respeta.

La causa es estructural: las reglas estaban repetidas por endpoint, así que
agregarlas a uno no las agregaba al otro. Con la capa de servicios hay un solo
camino:

```csharp
private async Task<ServiceResult<Consulta>> Transicionar(int id, EstadoConsulta destino)
{
    // buscar → validar transición → validar regla temporal del destino → guardar
}

private string? ReglaTemporalIncumplida(Consulta consulta, EstadoConsulta destino) => destino switch
{
    EstadoConsulta.Cancelado when consulta.FechaHora - Ahora < AnticipacionMinimaParaCancelar => "...",
    EstadoConsulta.Inasistencia when consulta.FechaHora > Ahora => "...",
    EstadoConsulta.Atendido when !consulta.FechaHora.IsInCurrentYear(Ahora) => "...",
    _ => null
};
```

Las cuatro operaciones de estado —tres con nombre propio y la genérica— pasan
por el mismo método. **El atajo no se cierra recordando cerrarlo: deja de
existir.** Eso es lo que significa que un refactor corrija defectos sin
buscarlos.

#### Lo que esto habilita en los tests

Los tests de reglas de negocio pasaron del controlador al servicio. Dejaron de
hablar HTTP:

```csharp
// antes
Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(resultado).StatusCode);

// ahora
AssertResultado.Conflicto(resultado, caso);
```

Y apareció una clase de test que antes era imposible: **sustituir el servicio**.

```csharp
private sealed class ConsultaServiceFalso : IConsultaService { /* devuelve la Falla pedida */ }

[Fact]
public async Task Conflicto_se_traduce_a_409()
{
    var resultado = await new ConsultasController(
        new ConsultaServiceFalso(Falla.Conflicto("No se puede en este estado."))
    ).CancelarConsulta(1);

    Assert.Equal(StatusCodes.Status409Conflict,
        Assert.IsType<ObjectResult>(resultado).StatusCode);
}
```

Sin base de datos, sin reloj y sin pipeline: se le entrega al controlador un
servicio que devuelve exactamente la falla a probar. **Antes, para ver un 404
había que construir el estado de base que lo produce.** Esa es la diferencia
práctica entre depender de una interfaz y depender de una clase concreta — no
un principio abstracto, un test que antes no se podía escribir.

```
dotnet test   →  84 passed, 0 failed    (eran 77)
```

#### La decisión discutible

Los servicios **devuelven entidades** (`Consulta`, `Afiliado`), no contratos de
salida. Mapear a `ConsultaResponse` queda en el controlador, porque qué campos
se exponen y cómo se anidan es una decisión de presentación.

La **entrada**, en cambio, reutiliza los contratos de `Contratos.Requests`.
Es asimétrico a propósito: un `CrearConsultaRequest` es exactamente la lista de
parámetros de la operación y nada más —no trae identidad ni estado—, así que
definir un tipo paralelo idéntico en la capa de servicios sería duplicación sin
beneficio. Los contratos de salida sí codifican decisiones de presentación, y
por eso no cruzan hacia adentro.

En un sistema con más de un consumidor la simetría valdría la pena. Acá se
prefirió no agregar una capa de tipos que no separa nada.

---

### 3.9 Política de cupo de nutrición — `feat/cupo-nutricion`

**Parte 2 del challenge.** El enunciado:

> *El plan incluye 6 consultas de nutrición por año, no más de 1 por mes. Las
> consultas no usadas no se acumulan. Aplica a consultas presenciales.*

Tres frases, cinco decisiones que el enunciado no toma. Están todas en §5; acá
está cómo quedaron en el código.

#### Primero: el catálogo de especialidades

La política necesita saber qué consultas son "de nutrición". En el código base
eso era texto libre:

```csharp
public string Especialidad { get; set; } = string.Empty;   // "Nutrición"
```

Una política que compara contra el literal `"Nutrición"` se **desactiva en
silencio** con un error de tipeo al dar de alta un profesional:
`"Nutricionista"`, `"Nutrición Clínica"`, `"nutricion"`. El afiliado agenda sin
límite y nadie se entera, porque nada falla.

| Campo | Rol |
|---|---|
| `Codigo` (`"NUT"`) | **Identidad.** Índice único, inmutable por contrato. Es contra esto que compara la política |
| `Nombre` (`"Nutrición"`) | **Presentación.** Es lo que muestra la interfaz |

Ni el `Id` ni el nombre sirven como identidad: el `Id` depende del orden en que
se poblaron las filas y difiere entre ambientes, y el nombre es editable desde
un ABM. El razonamiento completo está en §5.8.

El catálogo va en la migración con `HasData`, no en el sembrado de datos de
demostración: **una regla de negocio depende de que la fila `NUT` exista**. Los
afiliados y consultas de ejemplo son otra cosa y siguen en `DbSeeder`.

El contrato público no cambió. `ProfesionalResponse.Especialidad` sigue siendo el
nombre, así que el frontend no se enteró:

```csharp
p.Especialidad?.Nombre ?? string.Empty
```

#### Segundo: migraciones, y por qué son dos

Cambiar el esquema obliga a introducir migraciones de EF Core, lo que resuelve
el problema #15 del inventario: `EnsureCreated()` en un sistema descrito como
productivo crea el esquema desde el modelo y **no deja registro de versión**, así
que un cambio posterior no tiene forma de aplicarse sobre una base existente sin
perder los datos.

El scaffolding automático generó la conversión en un solo paso, y mal:

```csharp
migrationBuilder.DropColumn("Especialidad", "Profesionales");   // ← primero
migrationBuilder.CreateTable("Especialidades", ...);            // ← después
```

Descarta el texto **antes** de crear el catálogo: cuando llega el momento de
convertir, la información a convertir ya no existe. EF lo advierte
—*"An operation was scaffolded that may result in the loss of data"*— y la
advertencia es correcta.

Reordenarlo no alcanza. Al probar la versión de un solo paso sobre datos reales
apareció el caso que ninguna migración puede resolver sola: **un profesional con
una especialidad que el catálogo no contempla**. La migración abortaba con
`FOREIGN KEY constraint failed`, que es el comportamiento correcto —mejor fallar
que asignar una especialidad inventada— pero con un mensaje que no le dice a
nadie qué pasó.

De ahí las dos etapas, que es el despliegue que corresponde:

| Etapa | Qué hace | Propiedad |
|---|---|---|
| `CatalogoDeEspecialidades` | Crea el catálogo, agrega `EspecialidadId` **nullable**, convierte por nombre y **conserva la columna de texto** | Siempre segura de aplicar. Nada se pierde, nada falla |
| `EspecialidadObligatoria` | Vuelve la columna obligatoria, agrega la FK y descarta el texto | Falla si quedó alguien sin clasificar |

Entre las dos va la consulta de control:

```sql
SELECT Id, NombreCompleto, Especialidad
  FROM Profesionales
 WHERE EspecialidadId IS NULL;
```

Si devuelve filas, alguien que conozca el negocio tiene que decidir a qué
especialidad corresponde cada una. **La base no puede: un `UPDATE` no sabe qué
hacer con "Homeopatía".** Y si se aplica la etapa 2 de todas formas, falla —
pero con la etapa 1 ya aplicada, es decir, con los datos en un estado donde se
los puede revisar.

Con una sola migración no hay ningún momento en el que mirar los datos a medio
convertir. Eso es lo que compran las dos etapas.

**Dos limitaciones de SQLite que aparecieron al escribir la vuelta atrás,** y
que no son anecdóticas porque determinan cómo hay que escribir la migración:

1. **SQLite no sabe eliminar una restricción ni una columna**; EF lo emula
   reconstruyendo la tabla. Una sentencia de datos intercalada entre operaciones
   de esquema corre contra la tabla vieja y su resultado se descarta al copiar.
   EF lo avisa: *"will be attempted while a rebuild of table is pending"*. Por
   eso el `UPDATE` que repone el texto es la **primera** operación del `Down`, y
   el `Down` de la etapa 2 no tiene ninguna sentencia de datos.
2. **`PRAGMA foreign_keys` es un no-op dentro de una transacción.** Sin él, el
   `DROP TABLE Profesionales` de la reconstrucción falla cuando hay consultas que
   lo referencian. De ahí que el `Down` de la etapa 1 esté escrito en SQL
   explícito con `suppressTransaction: true`: es lo que hace que el pragma sirva.

El ida y vuelta completo está verificado sobre una base con profesionales,
afiliados y consultas: el texto se repone, las consultas sobreviven y el catálogo
desaparece.

Se agregó además una `IDesignTimeDbContextFactory`. Sin ella `dotnet ef` arranca
la aplicación para obtener el contexto, lo que **ejecuta la migración y el
sembrado antes de poder generar una migración nueva**.

#### Tercero: la política, y lo que deliberadamente no sabe

```
ICupoPolicy                contrato del límite: cuántas consumidas, cuántas quedan,
   │                       ¿puede agendar? Recibe el período ya resuelto.
   └── CupoNutricionPolicy       la regla del enunciado: 6 por año, 1 por mes

IPeriodoCupoProvider       traduce una fecha en (período anual, período mensual)
   ├── PeriodoCalendario         año = [1-ene, 1-ene)         ← adoptado
   └── PeriodoVigencia           año = [aniversario, +1 año)  ← implementado y testeado
```

La política recibe un rango y cuenta. Cambiar de interpretación de "año" es
cambiar una línea de `Program.cs`, sin tocar la regla ni sus tests. **La
ambigüedad del enunciado queda materializada en el código, no solo descrita en
este documento.**

##### Por qué el contrato es genérico y la implementación no

La interfaz nombra el **rol**; la clase nombra la **variante**. Es la misma
relación que `ILogger` / `ConsoleLogger`: nadie escribe `IConsoleLogger` con una
sola clase `ConsoleLogger` adentro, porque esa interfaz no agrega información
sobre su implementación más que la letra `I`. De ahí `ICupoPolicy` y
`CupoNutricionPolicy`, y no `ICupoNutricionPolicy`.

El contrato es genérico, pero **la implementación es una sola, y eso es
deliberado**. La variación previsible —otra especialidad con otros números— no
es una variación del algoritmo: *"N por período anual, M por período mensual,
sin acumulación"* es idéntico para nutrición y para kinesiología. Lo único que
cambia son los límites y el código de especialidad, y eso ya es configuración
(`CupoNutricionOptions`). Resolverlo con un subtipo por especialidad produciría
clases con **el mismo cuerpo y distintos literales**: duplicación disfrazada de
arquitectura.

`CodigoEspecialidad` vive en el contrato precisamente por eso: es la costura de
despacho. Con más de una política, se elige la que coincide con la especialidad
del profesional — sin que el contrato cambie.

Una segunda implementación se justifica cuando aparezca una regla de **otra
forma**: cupo compartido entre especialidades, ilimitado con autorización
previa, un límite que depende de la edad del afiliado. Ninguna de esas entra en
`Evaluar(SolicitudDeCupo)`, y cuál de las tres va a aparecer no se sabe hoy —
así que su contrato se deriva recién ahí, con dos casos reales a la vista.

El contraste está en la misma carpeta y es intencional: `IPeriodoCupoProvider`
tiene **dos implementaciones escritas y testeadas**, porque ahí la variación es
real y presente. Una interfaz se justifica por la variación que existe, no por
la que uno imagina.

La pureza no es un gesto estético, se paga sola en los tests:

```csharp
// El séptimo del año es un dato de entrada, no un estado de base a sembrar.
var decision = Evaluar([C(Utc(2026,1,10)), ..., C(Utc(2026,7,10))]);
Assert.False(decision.Permitida);
```

Los límites son configuración (`CupoNutricionOptions`), no literales dentro de la
regla. Además de que un plan comercial cambia sus números, permite probar el
borde del año sin sembrar seis consultas: `porAno: 2`.

El predicado de consumo es un `switch` **sin rama permisiva**:

```csharp
EstadoConsulta.Pendiente    => true,   // el cupo se reserva al agendar
EstadoConsulta.Confirmado   => true,
EstadoConsulta.Atendido     => true,
EstadoConsulta.Inasistencia => true,   // §5.4: la franja se ocupó igual
EstadoConsulta.Cancelado    => false,  // liberó cupo y liberó la franja
_ => throw new ArgumentOutOfRangeException(...)
```

Si mañana se agrega un estado, esto lanza en el primer test. Un `default => false`
sería un agujero en la política que nadie ve hasta que alguien lo usa.

#### Los períodos son intervalos semiabiertos

`[Desde, Hasta)`. Con intervalos cerrados hay que elegir el último instante
representable del período, y ese instante depende de la precisión con la que la
base guarda la fecha. Con el extremo abierto, el límite es el primer instante del
período siguiente: los períodos consecutivos no se solapan ni dejan huecos,
cualquiera sea la precisión. El borde está fijado en un test — una consulta a
medianoche del 1 de enero se cuenta en un año y solo uno.

#### Un bug que encontró un test de propiedad

`PeriodoVigencia` calculaba el fin del período como `inicio.AddMonths(1)`. Con un
alta el día 31:

| Período | Arranca | Termina con `AddMonths(1)` |
|---|---|---|
| Febrero | 28/02 (recortado) | **28/03** |
| Marzo | 31/03 | 30/04 |

Del 28 al 31 de marzo **no hay período que contenga esos días**. Ninguno de los
tests de ejemplo lo veía, porque todos miraban fechas cómodas. Lo encontró un
test que recorre tres años día por día y afirma una propiedad en lugar de un
caso:

```csharp
Assert.True(actual.Contiene(dia), $"{dia:dd/MM/yyyy} no cae en su período.");
if (actual == anterior) continue;
Assert.Equal(anterior.Hasta, actual.Desde);   // sin huecos ni solapamientos
```

La corrección: el fin de un período es **el ancla siguiente**, no un mes después
del inicio. El mismo error estaba en el período anual, donde con un alta el 29 de
febrero dejaba un hueco cada cuatro años.

Se registra porque es la clase de defecto que un test de ejemplo no encuentra: no
falla en el caso típico, falla tres días al año para un subconjunto de los
afiliados.

#### Dónde se aplica, y por qué solo ahí

En `ConsultaService.Crear`, antes de verificar la superposición horaria: a un
afiliado sin cupo hay que decirle que no tiene cupo, no que el horario está
ocupado.

Se aplica **únicamente al agendar**, y eso es correcto por una invariante que
también está testeada:

> Ninguna transición legal convierte una consulta que no consumía cupo en una que
> sí consume.

El único estado que no consume es `Cancelado`, y es terminal (§5.7). La única
transición que cambia el consumo es *hacia* `Cancelado`, que **libera** cupo —
relaja la restricción en lugar de apretarla, así que no necesita verificación.

El test lo verifica contra las dos reglas a la vez: la tabla de transiciones y el
predicado de consumo. Si alguien agrega una transición o cambia qué consume, el
test avisa — que es exactamente cuando hay que decidir si el cupo necesita
revalidarse.

#### La respuesta, y hacer visible la política

Exceder el cupo devuelve **409 Conflict** con `ProblemDetails` (§5.11). El
detalle no dice "no se puede":

> *El plan incluye 6 consultas de nutrición por año y el afiliado ya tiene 6
> entre el 01/01/2026 y el 31/12/2026. Las consultas no utilizadas no se
> acumulan, así que el cupo se renueva el 01/01/2027.*

> *El plan permite 1 consulta de nutrición por mes y el afiliado ya tiene 1 entre
> el 01/06/2026 y el 30/06/2026. La próxima se puede agendar a partir del
> 01/07/2026, y quedan 5 consultas en el año.*

Dice el límite, el período, cuánto queda y **desde cuándo se puede**. El límite
anual se verifica primero a propósito: con el año agotado, el mensaje mensual
sería una promesa falsa.

Las fechas se formatean con cultura invariante. El mensaje es parte del contrato
de la API y no debe cambiar según la cultura del servidor que lo genera.

Y se agregó un endpoint para que la política no se manifieste **solo** como un
rechazo:

```
GET /afiliados/{id}/cupo-nutricion
→ { "ano": { "limite": 6, "usadas": 4, "disponibles": 2, "desde": ..., "hasta": ... },
    "mes": { "limite": 1, "usadas": 0, "disponibles": 1, ... } }
```

Una regla que el usuario solo descubre cuando lo frena es la misma experiencia
que daba el código base. El frontend lo consume en §3.10.

#### Tests

```
dotnet test   →  158 passed, 0 failed    (eran 84)
```

| Suite | Qué cubre | Infraestructura |
|---|---|---|
| `CupoNutricionPolicyTests` | La regla: bordes del año y del mes, qué consume, virtual, configuración de límites, precedencia y texto de los mensajes | Ninguna |
| `PeriodosDelCupoTests` | Las dos interpretaciones, meses cortos, años bisiestos, y las propiedades sin huecos ni solapamientos | Ninguna |
| `InvarianteDeConsumoTests` | Que el cupo solo haga falta verificarlo al agendar | Ninguna |
| `CupoEnElAgendamientoTests` | El armado: qué historial se busca, especialidad por código, orden de las validaciones | EF InMemory |
| `NormalizacionDeEspecialidadesTests` | Las dos etapas de migración sobre datos del código base, incluido el caso sin clasificar y el ida y vuelta | SQLite real |
| `CupoPorHttpTests` | Toda la pila sobre los datos sembrados: 409, `ProblemDetails`, el endpoint de cupo | API completa |

---

### 3.10 Manejo de errores y experiencia de uso — `fix/ux-manejo-errores`

Resuelve los problemas **#21 a #23** y **#25 a #28**.

#### El problema: el backend explica y el frontend lo tira

Las secciones anteriores invirtieron trabajo en que cada rechazo diga qué pasó,
por qué y hasta cuándo. Ese texto viaja en `ProblemDetails.detail`, y el código
base lo descartaba en seis lugares:

```js
catch { alert('Error al procesar la solicitud') }
```

No es que lo ignorara: **ni siquiera lo recibía**. El `catch` sin binding
descarta el error antes de poder leerlo. Toda la política de cupo, para el
usuario, era esa misma frase.

Dos operaciones estaban peor todavía:

```js
async cancelar (id) {
  await consultasApi.cancelar(id)      // sin catch y sin refrescar
}
```

Un 409 acá no muestra nada y la fila sigue diciendo "Pendiente". **Y en el caso
exitoso tampoco cambia nada**, porque la respuesta se descarta. El usuario hace
clic, no ocurre nada visible, y concluye que el sistema está roto.

#### La traducción de errores

`services/http.js` clasifica cada falla en una categoría que la interfaz
necesita distinguir:

| Tipo | Origen | Qué significa |
|---|---|---|
| `validacion` | 400 con `errors` | Lo que se escribió no sirve, y se sabe en qué campo |
| `noEncontrado` | 404 | El recurso no existe |
| `regla` | 409 | El sistema funciona; la operación no corresponde |
| `servidor` | 5xx | El sistema falló |
| `red` | sin respuesta | No se llegó al servidor |

No son los códigos HTTP: son las **decisiones distintas que toma la vista**. Un
cupo agotado y un `NullReferenceException` son ambos "error" para axios, y cosas
opuestas para quien mira la pantalla.

De ahí que `AvisoDeError` los pinte distinto. Un rechazo por regla va en ámbar y
un fallo técnico en rojo, y solo el segundo ofrece **Reintentar**: un 409 va a
volver a fallar igual, así que ofrecer el botón sería mentir. Mostrar un cupo
agotado en rojo manda al usuario a llamar a soporte; mostrarlo como aviso lo
manda a leer el mensaje, que es donde está la respuesta.

El detalle de un **5xx se descarta a propósito**: describe una falla interna, no
le dice nada a quien está del otro lado y filtra información del servidor.

El interceptor **rechaza** con el error tipado en lugar de devolverlo, para que
`try/catch` siga siendo la forma natural de escribirlo y una llamada que se
olvide de manejarlo falle a la vista en lugar de seguir con un valor indefinido.

#### Lo que el cliente no debe saber

La tentación acá era replicar reglas en el frontend: deshabilitar "Cancelar"
según el estado, mostrar el panel de cupo si `especialidad === 'Nutrición'`. Las
dos crean una **segunda fuente de verdad que se desincroniza sin que nada
falle** — exactamente lo que el catálogo de especialidades vino a eliminar del
backend.

El criterio adoptado: **prevenir preguntándole al servidor, no copiando sus
reglas.** Dos agregados al contrato:

| Campo | Dónde | Reemplaza a |
|---|---|---|
| `ConsultaResponse.TransicionesPermitidas` | `GET /consultas/{id}` | Que el cliente reimplemente `CicloDeVidaConsulta` |
| `ProfesionalResponse.TieneCupo` | `GET /profesionales` | Que el cliente compare el nombre de la especialidad |

`TransicionesPermitidas` sale de `CicloDeVidaConsulta.TransicionesDesde`, que ya
existía. Con eso, el detalle deja de ofrecer los cinco estados —incluidos los
que la tabla rechaza con un 409— y una consulta terminal muestra *"Una consulta
en estado Atendido no admite cambios de estado"* en lugar de botones que no
funcionan.

`TieneCupo` se resuelve contra `ICupoPolicy.CodigoEspecialidad`, que es
justamente la costura de despacho descrita en §3.9. El código de especialidad
sigue sin salir al contrato público: lo que sale es la respuesta a la pregunta.

#### El cupo deja de ser solo un rechazo

Elegido el afiliado y un profesional con cupo, el formulario muestra el estado
**antes de enviar**:

```
Cupo de nutrición
Año  ██████  6 de 6 · hasta el 31/12/2026
Mes  ░       0 de 1 · hasta el 30/11/2026
```

Una regla que el usuario solo descubre cuando lo frena es la misma experiencia
que daba el código base. Si el envío igual falla por cupo —pudo cambiar entre
que se cargó el panel y se confirmó— el panel se recarga para reflejar el estado
real.

El panel también está en la lista de afiliados, a demanda por fila. Ahí
reemplaza a la columna `consultasUsadas`, que mostraba un contador que **ya no
es fuente de verdad** (§5.9): un número global no puede representar un cupo que
se cuenta por especialidad y por período.

#### Dos bugs que solo aparecieron al abrir el navegador

El build pasaba y los tests pasaban. La interfaz tenía dos defectos igual:

**1. La fecha de cupo se corría un día.** El panel mostraba *"hasta el
30/12/2026"* sobre un `2026-12-31T00:00:00Z`. Medianoche UTC es el día anterior
en cualquier zona al oeste de Greenwich.

La causa de fondo: el backend manda dos cosas distintas con el mismo formato. La
fecha del turno es un **instante** —hay que mostrarlo en la hora local de quien
mira— y el fin del período es una **fecha de calendario**, una decisión ya
tomada que convertir mueve. `services/fechas.js` las separa en dos funciones con
nombre, que es exactamente la razón por la que el bug existía: se formateaban
igual.

**2. El detalle quedaba mostrando la consulta anterior.** Vue Router reusa el
componente cuando solo cambia el parámetro de ruta, así que `mounted` no vuelve
a correr. Se agregó un `watch` sobre el parámetro.

Ninguno de los dos lo encontraba la batería de tests existente. El primero quedó
cubierto; el segundo es comportamiento de router y queda anotado en §6.

#### El resto del inventario

| Problema | Resolución |
|---|---|
| #23 Sin validación en cliente | Envío deshabilitado hasta que el formulario esté completo, `min` en la fecha, y errores por campo desde `ValidationProblem` |
| #25 Baja sin confirmación | `BotonConConfirmacion`, en la propia fila |
| #26 Sin estados de carga | Cada operación deshabilita su control y los que compiten con ella mientras está en vuelo |
| #28 URL fija en el código | `import.meta.env.VITE_API_URL`, con `.env.development` y `.env.example` |

La confirmación **no usa `confirm()`**: un diálogo nativo bloquea el hilo de la
página, no se puede estilar y no deja mostrar en el mismo lugar qué se está por
hacer.

#### Tests

```
dotnet test   →  164 passed, 0 failed    (eran 158)
npm test      →    8 passed, 0 failed    (no había)
```

| Suite | Qué cubre |
|---|---|
| `http.test.js` | La traducción de `ProblemDetails`: que el 409 conserve el motivo, que el 400 ubique cada mensaje en su campo, que el 5xx descarte el detalle interno, y que un cuerpo inesperado no deje la pantalla en blanco |
| `fechas.test.js` | Que una fecha de calendario no se corra un día al formatearse |
| `TransicionesEnLaRespuestaTests` | Que lo que declara la respuesta sea la tabla del dominio, recorriendo todos los estados, y que llegue serializado como texto |
| `CupoEnElCatalogoTests` | Que solo la especialidad de la política declare `TieneCupo` |

El frontend no tenía tests. Se agregó **Vitest** —versión 2, porque la última
exige Vite 6 y el proyecto está en Vite 5— y se cubrió la capa con lógica real:
la traducción de errores y el formateo de fechas. Los componentes no se testean:
son presentacionales y su comportamiento depende de contratos que ya están
verificados del lado del backend.

También se agregó `package-lock.json`, que el código base no tenía: sin él, dos
instalaciones del mismo `package.json` pueden resolver versiones distintas.

---

## 4. Problemas detectados en el código base

Inventario completo de lo relevado. La columna **Estado** indica si está
corregido, planificado o fuera de alcance.

### Backend

| # | Problema | Dónde | Estado |
|---|---|---|---|
| 1 | No compila: propiedad `Nombre` inexistente | `AfiliadosController.cs:50` | Corregido |
| 2 | No compila: falta `using` de `Helpers` | `ConsultasController.cs:102` | Corregido |
| 3 | `GET` que muta estado: cancelar una consulta responde a `[HttpGet]`. Un prefetch del navegador o un crawler puede cancelar consultas | `ConsultasController.cs:68` | Corregido |
| 4 | Ventana de cancelación mal calculada: valida `FromHours(23)` mientras el mensaje al usuario dice 24 horas | `ConsultasController.cs:74` | Corregido |
| 5 | `PUT /{id}/estado` permite cualquier transición sin validar. Saltea la regla de 24 horas, la de inasistencia sobre consulta futura y salteará también el cupo de nutrición. Tampoco valida `Enum.IsDefined` | `ConsultasController.cs:110` | Corregido — tabla de transiciones en §3.5; reglas temporales en §3.8, donde el atajo deja de existir |
| 6 | Sin máquina de estados: se puede marcar inasistencia sobre una consulta cancelada, o registrar atención sobre una cancelada | `ConsultasController.cs` | Corregido |
| 7 | `DateTime.Now` y `DateTime.UtcNow` mezclados: se persiste en UTC y se compara contra hora local. En Argentina son 3 horas de desvío sobre la ventana de 24 horas y sobre el cálculo de "año en curso". Se suma la pérdida de `DateTimeKind` al leer de SQLite | Varios | Corregido |
| 8 | Over-posting: los endpoints reciben las entidades de EF directamente (`[FromBody] Consulta`, `[FromBody] Afiliado`). El cliente puede enviar `Bloqueado`, `ConsultasUsadas`, `isActive` y objetos anidados que EF intentará persistir | Controllers | Corregido |
| 9 | Sin validación de entrada: no hay `[Required]`, ni unicidad de DNI, ni formato de email, ni verificación de que `FechaHora` sea futura | Controllers | Corregido |
| 10 | Detección de superposición incompleta: compara igualdad exacta de `FechaHora`, sin duración de consulta ni índice único que evite la condición de carrera | `ConsultasController.cs:54` | Parcial (ver §6) |
| 11 | `Afiliado.ConsultasUsadas` es un contador que nadie actualiza. Dato desnormalizado y engañoso | `Models/Afiliado.cs` | No se usa como fuente de verdad — ver §5.9 |
| 12 | `Afiliado.FechaBloqueo` nunca se escribe | `Models/Afiliado.cs` | Fuera de alcance — no hay operación de bloqueo en el enunciado (§6) |
| 13 | Baja física de afiliados existiendo `isActive`, y con FK `OnDelete(SetNull)`: las consultas quedan sin afiliado y se pierde el historial | `AfiliadosController.cs:59` | Corregido |
| 14 | `Especialidad` es texto libre. La política de cupo dependería de comparar contra el literal `"Nutrición"` | `Models/Profesional.cs` | Corregido — catálogo con `Codigo` como identidad (§3.9, §5.8) |
| 15 | `EnsureCreated()` sin migraciones, en un sistema descrito como productivo: el esquema no puede evolucionar de forma controlada | `Program.cs` | Corregido — `Migrate()` y migraciones versionadas (§3.9) |
| 16 | Nomenclatura: `createdAt` e `isActive` en camelCase | `Models/Afiliado.cs` | Corregido — propiedades renombradas; las columnas conservan su nombre para no requerir migración |
| 17 | Sin capa de aplicación: los controladores acceden a `DbContext` directamente y las entidades se filtran como contrato de API. `ReferenceHandler.IgnoreCycles` es un paliativo para serializar grafos de EF | Controllers | Corregido — ningún controlador referencia `AppDbContext`; reglas en `Servicios/`, resultado tipado `ServiceResult<T>` |
| 18 | Sin manejo global de errores, sin `ProblemDetails`, sin logging | `Program.cs` | Corregido — logging estructurado queda fuera de alcance (§6) |
| 19 | Sin tests | — | Corregido |
| 20 | CORS `AllowAnyOrigin` y Swagger expuesto en todos los ambientes | `Program.cs` | Ver §6 |

### Frontend

| # | Problema | Dónde | Estado |
|---|---|---|---|
| 21 | `cancelar()` sin manejo de error y sin refrescar la vista: el usuario hace clic y no ocurre nada visible; el error queda en consola. Igual `marcarInasistencia()` | `ConsultasList.vue:63`, `ConsultaDetalle.vue:77` | Corregido — §3.10 |
| 22 | Todos los errores muestran el mismo `alert('Error al procesar la solicitud')`, **descartando el mensaje que el backend ya envía**. Sin esto, la política de cupo es invisible para el usuario | Todas las vistas | Corregido — §3.10 |
| 23 | Sin validación en el cliente: el formulario se envía vacío | `ConsultaNueva.vue` | Corregido — §3.10 |
| 24 | `datetime-local` envía una fecha local sin zona, alimentando el problema de UTC del backend | `ConsultaNueva.vue:32` | Corregido |
| 25 | Baja de afiliado sin confirmación | `AfiliadosList.vue:32` | Corregido — §3.10 |
| 26 | Sin estados de carga ni botones deshabilitados durante la petición: permite doble envío | Todas las vistas | Corregido — §3.10 |
| 27 | Selector de estado libre en el detalle, que expone el problema #5 desde la interfaz | `ConsultaDetalle.vue:18` | Corregido — §3.10, con las transiciones que declara el backend |
| 28 | URL del backend fija en el código en lugar de variable de entorno | `services/api.js:3` | Corregido — §3.10 |

---

## 5. Supuestos tomados

El requerimiento es:

> *"Implementar política de cupo de consultas: el plan incluye 6 consultas de
> nutrición por año, no más de 1 por mes. Las consultas no usadas no se
> acumulan. Aplica a consultas presenciales."*

Son cuatro oraciones y **ocho puntos ambiguos**. Cada uno se resolvió con un
criterio explícito; donde la lectura contraria también era defendible, queda
registrada.

Los datos iniciales (`Data/DbSeeder.cs`) sirvieron como evidencia: los ocho
afiliados sembrados cubren, uno por uno, los casos borde de esta política. Se
usaron como especificación ejecutable.

### 5.1 "Año" significa año calendario

**Alternativas:** año calendario (1 de enero – 31 de diciembre) · año de
vigencia contado desde el alta del afiliado (`createdAt`) · ventana móvil de 365
días.

**Resolución:** año calendario.

**Criterio:** la frase *"las consultas no usadas no se acumulan"* solo tiene
sentido con períodos que **cierran**. Acumular presupone un contador que se
vacía y se repone; con una ventana móvil no hay nada que acumular, el cupo se
libera progresivamente y la cláusula queda sin efecto.

El dato lo confirma. El afiliado 8 (alta 2024-03-14, una consulta de nutrición
el 2025-12-15) discrimina entre las tres lecturas:

| Interpretación | Consultas consumidas al 2026-11-24 |
|---|---|
| Año calendario 2026 | 0 |
| Año de vigencia (aniversario 14 de marzo) | 0 |
| Ventana móvil de 365 días | 1 |

El comentario del propio seeder describe ese registro como *"consulta de
nutrición del año anterior"*. Con ventana móvil no existe "el año anterior";
el vocabulario es de períodos discretos.

**Contra registrada:** el año calendario es **inequitativo para quien se afilia
tarde en el año**. Un afiliado que ingresa el 15 de noviembre recibe 6 consultas
que vencen el 31 de diciembre y, por la regla mensual, solo puede usar 2. Paga
lo mismo que quien se afilió en enero. El año de vigencia desde `createdAt`
corrige exactamente eso, y el modelo ya tiene el dato. No se adoptó porque los
datos sembrados y el vocabulario del requerimiento apuntan al calendario, pero
la implementación deja la decisión como punto de configuración (§5.10).

### 5.2 "Mes" significa mes calendario

**Alternativas:** mes calendario (se repone el día 1) · mínimo de 30 días entre
consultas.

**Resolución:** mes calendario.

**Criterio:** bajo la lectura de 30 días, **los datos sembrados serían
inválidos**. El afiliado 3 — descrito en el seeder como *"6 consultas de
nutrición en el año (enero a junio)"*, es decir, el caso del afiliado que
consumió correctamente su cupo — tiene consultas el día 10 de cada mes:

```
2026-01-10 → 2026-02-10    31 días
2026-02-10 → 2026-03-10    28 días   ← febrero 2026 tiene 28 días
2026-03-10 → 2026-04-10    31 días
2026-04-10 → 2026-05-10    30 días
2026-05-10 → 2026-06-10    31 días
```

Con un mínimo de 30 días, la consulta de marzo sería ilegal. El caso de
referencia de uso correcto violaría la política que se está implementando.

Se suma que el afiliado 2 tiene exactamente 30 días entre su primera y segunda
consulta — el borde preciso de esa regla, donde el resultado depende de usar
`<` o `<=`. Y que el afiliado 4, que es el caso de prueba de la regla mensual,
se siembra como `new DateTime(y, hoy.Month, 1)`: anclado al **día 1 del mes
corriente**. Bajo mes calendario ese caso se comporta igual cualquier día en
que se ejecute; bajo ventana de 30 días el resultado depende del día del mes.

### 5.3 Estados que consumen cupo

**Resolución:** consumen `Pendiente`, `Confirmado` y `Atendido`. No consume
`Cancelado`.

**Criterio:** si una consulta `Pendiente` no consumiera cupo, un afiliado podría
reservar seis turnos sin consumir nada y la política no tendría efecto
práctico. El cupo se reserva al agendar.

### 5.4 La inasistencia consume cupo

**Resolución:** `Inasistencia` consume.

**Criterio:** la consulta reservó una franja en la agenda del profesional y
nadie más pudo usarla. La política protege un recurso escaso, y el recurso se
consumió. Es también coherente con que el modelo ya contemple bloqueo de
afiliados, lo que sugiere que el sistema penaliza la inasistencia en lugar de
tratarla como neutra.

**Contra registrada:** es defendible lo opuesto — el afiliado no recibió la
prestación, y el plan "incluye 6 consultas" en el sentido de seis atenciones
efectivas. Bajo esa lectura la inasistencia no debería descontar. Se eligió la
primera por el argumento del recurso ocupado; el cambio es un predicado y su
test.

Este es el caso sembrado en el afiliado 6, que tiene una inasistencia y una
consulta futura pendiente.

### 5.5 El cupo se evalúa contra la fecha solicitada

**Resolución:** el año y el mes que se verifican son los de la `FechaHora` de la
consulta que se está agendando, no los de la fecha actual.

**Criterio:** agendar en diciembre una consulta para enero debe consumir el cupo
de enero, no el de diciembre. Con este criterio *"no se acumulan"* se cumple
sin lógica adicional: cada consulta pesa sobre el período en que efectivamente
ocurre.

### 5.6 Solo la modalidad presencial participa

**Resolución:** las consultas presenciales se limitan y consumen cupo. Las
virtuales no se limitan **ni consumen**.

**Criterio:** *"aplica a consultas presenciales"*. Si una virtual consumiera
cupo sin estar limitada, el afiliado perdería cupo presencial por una
prestación que la política declara fuera de su alcance.

Caso sembrado en el afiliado 5: una virtual atendida y una presencial
cancelada. Ninguna consume.

### 5.7 Una consulta cancelada no se reactiva

**Resolución:** `Cancelado` es un estado terminal. No existe transición desde
una consulta cancelada hacia ningún otro estado.

**Criterio:** el riesgo a cubrir era que `PUT /{id}/estado` se convirtiera en la
vía para evadir la política: cancelar una consulta, liberar el cupo, agendar
otra y luego reactivar la cancelada, terminando con dos consultas sobre un cupo
de una.

La primera formulación de este supuesto resolvía eso **revalidando el cupo en la
reactivación**. Al escribir la tabla de transiciones quedó claro que había una
respuesta más simple y más sólida: no permitir la reactivación.

Una cancelación no es un cambio de opinión reversible: **liberó el cupo del
afiliado y liberó la franja horaria del profesional**, que otro afiliado pudo
haber tomado en el intervalo. Reactivar exige revalidar el cupo, la
disponibilidad del profesional y el estado del afiliado — es decir, repetir
todas las validaciones de agendar. Si hay que repetirlas, lo correcto es
**agendar de nuevo**: se crea una consulta que las atraviesa todas por el camino
normal, y la cancelada queda en el historial como lo que fue.

Se registra el cambio de criterio porque el supuesto original quedó escrito
antes de modelar el ciclo de vida completo.

**Contra registrada:** una cancelación por error operativo —el administrativo
canceló la consulta equivocada— obliga a agendar de nuevo y la consulta original
conserva un desenlace que no ocurrió. Se acepta: el historial refleja lo que el
sistema registró, y la corrección queda trazable como dos registros en lugar de
uno reescrito.

### 5.8 Identificación de la especialidad

**Problema:** `Profesional.Especialidad` es texto libre. Hacer que la política
dependa de comparar contra `"Nutrición"` significa que un error de tipeo al dar
de alta un profesional —`"Nutricionista"`, `"Nutrición Clínica"`,
`"nutricion"`— **desactiva la política en silencio**. El afiliado agenda sin
límite y nadie se entera.

**Resolución:** catálogo de especialidades como entidad, con dos campos
distintos:

| Campo | Rol |
|---|---|
| `Codigo` (ej. `"NUT"`) | **Identidad.** Estable, inmutable por contrato. Es contra esto que compara la política |
| `Nombre` (ej. `"Nutrición"`) | **Presentación.** Es lo que muestra la interfaz |

**Criterio — por qué un código y no el `Id`:** el `Id` es una clave subrogada
que genera la base de datos. No es un dato de dominio. Si la política comparara
`EspecialidadId == 4`, la regla de negocio quedaría atada al orden en que se
poblaron las filas, y ese orden difiere entre ambientes: "Nutrición" puede ser
`Id = 4` en desarrollo e `Id = 11` en producción. El código no sería portable.

**Criterio — por qué un código y no el nombre:** el nombre es presentación y un
ABM puede editarlo. Si mañana la especialidad pasa a llamarse "Nutrición y
Dietética", la política no debe enterarse.

Es la misma separación que usan los nomencladores de salud —CIE-10, SNOMED—:
un código identifica el concepto independientemente de cómo se lo muestre.

**Contra registrada:** el código requiere disciplina para seguir siendo
confiable: índice único, no exponerlo en el ABM y tratarlo como inmutable. Sin
esas tres cosas es un texto libre con otro nombre.

Este cambio modifica el esquema, lo que obliga a introducir migraciones de EF
Core — y de paso resuelve el problema #15, `EnsureCreated()` en un sistema
productivo.

La conversión de los datos existentes (`"Nutrición"`, `"Clínica Médica"`) hacia
el catálogo quedó implementada en **dos migraciones**, no en una: la primera
convierte y conserva el texto original, la segunda exige que la conversión haya
sido completa. El motivo es que un profesional con una especialidad que el
catálogo no contempla no lo puede resolver ningún `UPDATE` — hace falta que
alguien decida, y para eso hace falta un momento en el que mirar los datos a
medio convertir. El detalle y la consulta de control están en §3.9.

### 5.9 El contador `ConsultasUsadas` no se usa como fuente de verdad

**Resolución:** el cupo se calcula consultando el historial de consultas, no
leyendo `Afiliado.ConsultasUsadas`.

**Criterio:** ese campo hoy no lo actualiza nadie y no distingue especialidad,
modalidad ni período — tres dimensiones que la política necesita. Un contador
desnormalizado que puede divergir del historial es una fuente de error
silencioso; el historial es la única fuente de verdad disponible.

### 5.10 Los límites no son constantes dispersas

Los valores 6 y 1 viven en una configuración con nombre
(`CupoNutricionOptions`), no como literales dentro de la regla. Esto permite
que los tests verifiquen los bordes inyectando otros valores, sin necesidad de
sembrar seis consultas para probar el séptimo caso.

La interpretación del período (§5.1, §5.2) se resuelve detrás de una abstracción
propia:

```
ICupoPolicy              contrato del límite; recibe el período ya resuelto
   └── CupoNutricionPolicy     la regla del enunciado

IPeriodoCupoProvider     traduce una fecha en (período anual, período mensual)
   ├── PeriodoCalendario       año = [1-ene, 31-dic]      ← adoptado
   └── PeriodoVigencia         año = [aniversario, +1 año] ← implementado y testeado
```

La regla de negocio no conoce el calendario: recibe un rango y cuenta. Cambiar
de interpretación es cambiar un proveedor, sin tocar la política ni sus tests.
La ambigüedad queda materializada en el código, no solo descrita en este
documento.

### 5.11 Respuesta HTTP al exceder el cupo

**Resolución:** `409 Conflict` con cuerpo `ProblemDetails`.

**Criterio:** la petición es sintácticamente válida y está autorizada; lo que
falla es que el estado actual del sistema no permite la operación. Eso es
conflicto, no error de sintaxis. `400` se reserva para entrada malformada.

---

## 6. Fuera de alcance

### Cupo por plan

El requerimiento dice *"**el plan** incluye 6 consultas de nutrición por año"*,
y `Afiliado.Plan` es un texto libre (`"Plan Integral"`). El cupo es,
conceptualmente, un atributo del plan: el diseño correcto a escala es una
relación `Plan × Especialidad → Cupo` persistida, de modo que un "Plan Básico"
con 3 consultas o un "Plan Premium" con 12 sean datos y no código.

**No se implementa.** Existe un solo plan y una sola especialidad con cupo.
Construir un motor de cupos configurable sin un segundo caso real es diseñar
contra un requerimiento que nadie formuló. El catálogo de especialidades (§5.8)
deja el camino abierto: agregar la relación no exige rediseñar lo hecho.

Se señala explícitamente porque la diferencia entre no haber visto el problema y
haberlo visto y acotado el alcance es parte de lo que se evalúa.

### Operación de bloqueo de afiliados

El modelo trae `Afiliado.Bloqueado` y `Afiliado.FechaBloqueo` (problema #12). El
primero **sí se respeta**: un afiliado bloqueado no puede agendar. El segundo no
lo escribe nadie, y no se corrigió porque **no hay ninguna operación que bloquee
o desbloquee**: el campo solo lo puebla el sembrado.

Agregar `POST /afiliados/{id}/bloqueo` y su reverso es simple, pero el enunciado
no menciona el bloqueo y la pregunta que hay que contestar antes es de negocio:
quién puede bloquear, por qué motivos y si el motivo se registra. Inventar esa
respuesta para completar un campo sería agregar una operación que nadie pidió,
con reglas supuestas.

Lo que sí se hizo es no empeorarlo: `ActualizarAfiliadoRequest` **excluye**
`Bloqueado` a propósito (§3.7), de modo que el estado de bloqueo no se pueda
cambiar de contrabando desde la edición de la ficha de contacto.

### Reactivación de afiliados desde la interfaz

El backend expone `POST /afiliados/{id}/reactivacion`, pero la lista solo
muestra afiliados activos: no hay desde dónde invocarla. Completarlo requiere un
filtro de "incluir dados de baja" en el listado, que es una decisión de producto
—quién puede ver y reactivar una baja— más que una corrección.

### Tests de componentes del frontend

Se testeó la capa con lógica propia (traducción de errores, formateo de fechas).
Los componentes quedan sin tests: hacerlo bien implica Vue Test Utils y un
entorno DOM, y lo que verificarían —que el aviso se pinta según el tipo, que el
botón se deshabilita en vuelo— es comportamiento de presentación sobre
contratos que ya están verificados del lado del backend.

Un defecto de esta categoría apareció en esta misma rama: el detalle quedaba
mostrando la consulta anterior al cambiar solo el parámetro de ruta. Lo encontró
el navegador, no un test, y un test de componente tampoco lo habría encontrado
—es comportamiento del router—; lo habría encontrado uno de integración, que es
otra inversión.

### Autenticación y autorización

El sistema no tiene identidad de usuario. Varias operaciones deberían estar
restringidas por rol — marcar inasistencia, cambiar estado y dar de baja un
afiliado son acciones de personal interno, no del afiliado. Modelarlo requiere
definir roles y un flujo de autenticación que el enunciado no plantea.

### Condición de carrera al agendar

Dos peticiones simultáneas pueden pasar ambas la validación de cupo antes de
que cualquiera persista. La solución correcta combina transacción con
revalidación y un índice único que respalde la invariante. Se documenta como
riesgo conocido: en SQLite con un único proceso la ventana es mínima, y
resolverlo bien exige decisiones de infraestructura (nivel de aislamiento,
reintentos) que exceden el alcance.

El mismo razonamiento aplica a la detección de superposición de horarios
(problema #10): se corrige la comparación, pero modelar duración de consulta y
agenda del profesional es un requerimiento aparte.

### Endurecimiento para producción

`AllowAnyOrigin` en CORS y Swagger expuesto en todos los ambientes son
configuraciones inadecuadas para producción, pero corregirlas requiere conocer
los orígenes reales y la estrategia de ambientes. Se señalan sin modificar.

### Paginación

`GET /afiliados` y `GET /consultas` devuelven la tabla completa. Con volumen
real hace falta paginación y proyección. Queda fuera por no ser parte de lo
solicitado y por afectar el contrato que consume el frontend.

---

## 7. Estado de la entrega

| Fase | Alcance | Estado |
|---|---|---|
| 0 | Repositorio, línea base | Completo |
| 1 | Errores de compilación | Completo |
| 2 | Normalización, convenciones, proyecto de tests | Completo |
| 3 | Correcciones del backend | Completo — fechas, semántica, ciclo de vida, historial y contratos |
| 4 | Capa de servicios con interfaces | Completo |
| 5 | Política de cupo de nutrición | Completo |
| 6 | Frontend: errores visibles, validación y cupo en pantalla | Completo |
| 7 | Cierre del documento | Completo |

### Verificación final

```
dotnet test   →  164 passed, 0 failed
npm test      →    8 passed, 0 failed
```

| Alcance de los tests | Suites |
|---|---|
| Reglas de dominio sin infraestructura | ciclo de vida, política de cupo, períodos, invariante de consumo |
| Servicios sobre EF InMemory | agendamiento, cupo, historial, baja lógica |
| Migraciones sobre SQLite real | las dos etapas del catálogo, incluido el ida y vuelta |
| Pipeline HTTP completo | verbos, códigos, `ProblemDetails`, validación, contratos |
| Frontend | traducción de errores y formateo de fechas |

La Parte 2 se verificó además de punta a punta en el navegador: el panel de cupo
antes de enviar, el `409` con su motivo en pantalla, y las acciones disponibles
según las transiciones que declara el backend.

---

## 8. Cómo ejecutar

### Requisitos

- SDK de .NET 8 o superior. Si solo hay SDK 9 o 10 instalado, el proyecto
  compila igual: el SDK es retrocompatible con `net8.0` y basta con tener el
  runtime 8 presente.
- Node.js 18 o superior.

### Backend

```bash
cd backend
dotnet run
```

API en `http://localhost:5000`, Swagger en `http://localhost:5000/swagger`.

La base SQLite (`consultas.db`) se crea en el primer arranque aplicando las
**migraciones**, y después se siembran los datos de demostración.

> Si existe un `backend/consultas.db` de una corrida anterior a las migraciones
> —creado con `EnsureCreated()`, es decir sin tabla de versiones— hay que
> borrarlo: `Migrate()` no puede aplicarse sobre un esquema del que no hay
> registro. Es exactamente el problema que las migraciones vienen a resolver.

### Tests

```bash
dotnet test
```

### Migraciones

```bash
cd backend
dotnet ef migrations list
dotnet ef database update                      # al día
dotnet ef database update CatalogoDeEspecialidades   # solo la etapa 1
```

Entre las dos etapas de la conversión del catálogo va la consulta de control de
§3.9.

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Cliente en `http://localhost:5173`.

La URL de la API sale de `VITE_API_URL`. `.env.development` ya trae el valor
para desarrollo; `.env.example` documenta la variable. Vite la resuelve **en
tiempo de compilación**, así que un despliegue la inyecta antes del `build`, no
después.

#### Tests del frontend

```bash
cd frontend
npm test
```
