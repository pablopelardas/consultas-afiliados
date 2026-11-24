# Documento técnico — Challenge Desarrollador .NET

**Autor:** Pablo Pelardas
**Stack:** .NET 8 (ASP.NET Core · EF Core · SQLite) + Vue 3 (Vite)
**Repositorio:** https://github.com/pablopelardas/consultas-afiliados

> Documento vivo. Se escribe en paralelo al código: cada decisión se anota en el
> momento en que se toma, no se reconstruye al cierre.

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

---

## 4. Problemas detectados en el código base

Inventario completo de lo relevado. La columna **Estado** indica si está
corregido, planificado o fuera de alcance.

### Backend

| # | Problema | Dónde | Estado |
|---|---|---|---|
| 1 | No compila: propiedad `Nombre` inexistente | `AfiliadosController.cs:50` | Corregido |
| 2 | No compila: falta `using` de `Helpers` | `ConsultasController.cs:102` | Corregido |
| 3 | `GET` que muta estado: cancelar una consulta responde a `[HttpGet]`. Un prefetch del navegador o un crawler puede cancelar consultas | `ConsultasController.cs:68` | Planificado |
| 4 | Ventana de cancelación mal calculada: valida `FromHours(23)` mientras el mensaje al usuario dice 24 horas | `ConsultasController.cs:74` | Corregido |
| 5 | `PUT /{id}/estado` permite cualquier transición sin validar. Saltea la regla de 24 horas, la de inasistencia sobre consulta futura y salteará también el cupo de nutrición. Tampoco valida `Enum.IsDefined` | `ConsultasController.cs:110` | Planificado |
| 6 | Sin máquina de estados: se puede marcar inasistencia sobre una consulta cancelada, o registrar atención sobre una cancelada | `ConsultasController.cs` | Planificado |
| 7 | `DateTime.Now` y `DateTime.UtcNow` mezclados: se persiste en UTC y se compara contra hora local. En Argentina son 3 horas de desvío sobre la ventana de 24 horas y sobre el cálculo de "año en curso". Se suma la pérdida de `DateTimeKind` al leer de SQLite | Varios | Corregido |
| 8 | Over-posting: los endpoints reciben las entidades de EF directamente (`[FromBody] Consulta`, `[FromBody] Afiliado`). El cliente puede enviar `Bloqueado`, `ConsultasUsadas`, `isActive` y objetos anidados que EF intentará persistir | Controllers | Planificado |
| 9 | Sin validación de entrada: no hay `[Required]`, ni unicidad de DNI, ni formato de email, ni verificación de que `FechaHora` sea futura | Controllers | Planificado |
| 10 | Detección de superposición incompleta: compara igualdad exacta de `FechaHora`, sin duración de consulta ni índice único que evite la condición de carrera | `ConsultasController.cs:54` | Parcial (ver §6) |
| 11 | `Afiliado.ConsultasUsadas` es un contador que nadie actualiza. Dato desnormalizado y engañoso | `Models/Afiliado.cs` | Ver §5.9 |
| 12 | `Afiliado.FechaBloqueo` nunca se escribe | `Models/Afiliado.cs` | Planificado |
| 13 | Baja física de afiliados existiendo `isActive`, y con FK `OnDelete(SetNull)`: las consultas quedan sin afiliado y se pierde el historial | `AfiliadosController.cs:59` | Planificado |
| 14 | `Especialidad` es texto libre. La política de cupo dependería de comparar contra el literal `"Nutrición"` | `Models/Profesional.cs` | Planificado (ver §5.8) |
| 15 | `EnsureCreated()` sin migraciones, en un sistema descrito como productivo: el esquema no puede evolucionar de forma controlada | `Program.cs` | Planificado |
| 16 | Nomenclatura: `createdAt` e `isActive` en camelCase | `Models/Afiliado.cs` | Planificado |
| 17 | Sin capa de aplicación: los controladores acceden a `DbContext` directamente y las entidades se filtran como contrato de API. `ReferenceHandler.IgnoreCycles` es un paliativo para serializar grafos de EF | Controllers | Planificado |
| 18 | Sin manejo global de errores, sin `ProblemDetails`, sin logging | `Program.cs` | Planificado |
| 19 | Sin tests | — | Corregido |
| 20 | CORS `AllowAnyOrigin` y Swagger expuesto en todos los ambientes | `Program.cs` | Ver §6 |

### Frontend

| # | Problema | Dónde | Estado |
|---|---|---|---|
| 21 | `cancelar()` sin manejo de error y sin refrescar la vista: el usuario hace clic y no ocurre nada visible; el error queda en consola. Igual `marcarInasistencia()` | `ConsultasList.vue:63`, `ConsultaDetalle.vue:77` | Planificado |
| 22 | Todos los errores muestran el mismo `alert('Error al procesar la solicitud')`, **descartando el mensaje que el backend ya envía**. Sin esto, la política de cupo es invisible para el usuario | Todas las vistas | Planificado |
| 23 | Sin validación en el cliente: el formulario se envía vacío | `ConsultaNueva.vue` | Planificado |
| 24 | `datetime-local` envía una fecha local sin zona, alimentando el problema de UTC del backend | `ConsultaNueva.vue:32` | Corregido |
| 25 | Baja de afiliado sin confirmación | `AfiliadosList.vue:32` | Planificado |
| 26 | Sin estados de carga ni botones deshabilitados durante la petición: permite doble envío | Todas las vistas | Planificado |
| 27 | Selector de estado libre en el detalle, que expone el problema #5 desde la interfaz | `ConsultaDetalle.vue:18` | Planificado |
| 28 | URL del backend fija en el código en lugar de variable de entorno | `services/api.js:3` | Planificado |

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

### 5.7 Reactivar una consulta cancelada revalida el cupo

**Resolución:** cualquier transición que lleve una consulta desde `Cancelado` a
un estado que consume cupo vuelve a validar la política.

**Criterio:** sin esto, `PUT /{id}/estado` es la vía por la que se evade la
política completa: se cancela una consulta, se libera el cupo, se agenda otra y
luego se reactiva la cancelada.

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
productivo. Se documenta el script de normalización de los datos existentes
(`"Nutrición"`, `"Clínica Médica"`) hacia el catálogo.

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
ICupoNutricionPolicy     regla pura: cuántas consumidas, cuántas quedan, ¿puede agendar?
      │                  recibe el período ya resuelto; no sabe qué es un año
      │
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
| 2 | Normalización, convenciones, proyecto de tests | En curso |
| 3 | Correcciones del backend | En curso — manejo de fechas y ventana de cancelación completos |
| 4 | Capa de servicios con interfaces | Pendiente |
| 5 | Política de cupo de nutrición | Pendiente |
| 6 | Frontend: errores visibles y validación | Pendiente |
| 7 | Cierre del documento | En curso |

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
La base SQLite (`consultas.db`) y los datos iniciales se crean en el primer
arranque.

### Tests

```bash
dotnet test
```

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Cliente en `http://localhost:5173`.
