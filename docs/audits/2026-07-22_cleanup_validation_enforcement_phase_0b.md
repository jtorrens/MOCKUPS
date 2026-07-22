# Limpieza, validación y enforcement — Fase 0B

Fecha: 2026-07-22
Estado: en curso, por slices cerrados.

## Objetivo

Retirar únicamente código activo demostrado como huérfano, después del cierre
de fallbacks de la fase 0A. Esta fase no cambia comportamiento, modelo, UX,
persistencia ni ownership; las duplicaciones vivas se reservan para 0C.

Norma de ejecución: contrato 71.

## Inventario inicial

| Candidato | Evidencia | Decisión |
|---|---|---|
| Nueve imports, locales o parámetros no usados en Desktop Preview | TypeScript los identifica con `noUnusedLocals` / `noUnusedParameters`; llamadas e inicializadores revisados | Retirar en 0B.1. |
| Parse de `designPreviewJson` dentro del resolver común de items | Ambos owners ya validan el documento antes de entregar el item; el valor no participa en el helper | Retirar el parse duplicado en 0B.1. |
| Comandos raíz `legacy:*` | Acceso histórico deliberado, declarado por contratos 25, 26 y el handoff de limpieza | Mantener; no son runtime activo huérfano. |
| Checks negativos de rutas retiradas | Impiden reintroducir Simplified Editor, componentes legacy y migraciones cerradas | Mantener; son enforcement activo. |
| Nueve parámetros C# sin uso | Diagnóstico `IDE0060`, llamadas y firmas privadas/públicas revisadas | Retirar en 0B.2; ningún parámetro participaba en output o side effects. |
| `UpdateActionButtons` y `SyncBooleanInput` | Métodos vacíos; todas sus llamadas eran no-op y un wrapper solo las repetía | Retirar en 0B.2. |
| Controles XAML nombrados | Cada nombre tiene referencia compilada desde el shell | Mantener; no hay candidato huérfano. |
| Converters XAML | No existen converters declarados en la superficie actual | Sin acción. |
| Métodos privados y DTOs C# restantes | El build estándar no demuestra por sí solo que estén huérfanos | Continuar con referencias directas, reflection y serialización. |
| Generador privado de layouts de Component Classes | Única aparición en un archivo autocontenido de 780 líneas; los layouts actuales proceden de SQLite/repository | Retirar en 0B.3 como fuente paralela dormida. |
| Normalizador privado de Preview Actions | Archivo autocontenido de 181 líneas sin caller; añadía defaults y reparaciones | Retirar en 0B.3; el formato actual no se normaliza en runtime. |
| Helpers privados aislados | Varias declaraciones sin ninguna llamada; una segunda pasada descubre helpers agrupadores adicionales | Retirar en 0B.3 y repetir hasta cero candidatos. |
| Aproximaciones exportadas de tamaño multilínea/wrapped | Solo aparece su declaración; el checker usa únicamente width y line wrapping | Retirar en 0B.3. |
| `planRasterFrame` | Sin caller runtime actual, pero protegido como contrato genérico de planificación Raster por enforcement | Mantener mientras se decida Render Mode; no confundir incompleto con huérfano. |

## Slice 0B.1 — Bindings TypeScript sin consumidor

Se retiran cálculos locales, imports y un parámetro que no participan en ningún
resultado de Audio, Bubble, Collection Stack, Conversation, Keypad, Lock Screen,
Text Input Bar ni el adapter HTML. Los resolvers propietarios conservan todas
las validaciones de entrada; el helper de item deja de repetir un parse cuyo
resultado se descartaba.

El `typecheck` pasa a exigir ausencia de locales y parámetros TypeScript no
usados. Esto convierte el inventario inicial en una regla continua sin añadir
conocimiento de Components al bridge o renderer.

## Slice 0B.2 — Parámetros y no-op C# sin consumidor

El analizador de estilo localizó nueve parámetros cuyo valor no se consumía.
Se retiraron junto con sus argumentos en Runtime Inputs, Device Import,
Dictionary layout, Animation, Usage y las superficies de Preview. En Usage se
mantiene el catálogo de Component Variants únicamente en las rutas que sí
recorren componentes embebidos; el escaneo de bindings escalares deja de
transportarlo sin usar.

`UpdateActionButtons`, `SyncBooleanInput` y el wrapper que solo invocaba el
segundo eran no-op completos. Su retirada no cambia Play, Restore, valores de
acción, temporización ni refresco de Preview; elimina llamadas que aparentaban
sincronizar controles inexistentes.

Las firmas Raster dejan de aceptar una cadencia que no usaban y un callback de
cancelación que nunca se mostraba ni se invocaba. Continúan intactas la
cancelación interna entre preparaciones y la limitación ya declarada: Raster
Preview no constituye todavía Render Mode ni un flujo final de exportación.

El comando `check:unused:desktop` incorpora `IDE0060` a la validación completa.
La revisión cruzada confirma además que todos los controles XAML nombrados se
consumen y que no hay converters declarados pendientes de retirada.

## Slice 0B.3 — Fuentes paralelas dormidas y segunda pasada

Se elimina el generador privado de layouts de Component Classes. Sus 780 líneas
no tenían caller y competían conceptualmente con la autoridad actual formada
por layouts persistidos, repository y metadatos de editor. No se modifica
ningún layout actual ni la base canónica.

También se elimina el normalizador privado de Preview Actions: un bloque de 181
líneas sin entrada que completaba defaults y reescribía acciones. Mantenerlo
habría conservado una falsa ruta de reparación contraria a los documentos
actuales estrictos.

La pasada por métodos privados retira helpers sin llamada en bindings,
playback, layout, Device metrics, Theme colors e infraestructura JSON. Al
repetir el inventario desaparecen dos agrupadores que solo eran consumidos por
otro helper retirado. La pasada final queda sin tipos, clases ni métodos
privados con una única aparición.

En TypeScript se retiran dos funciones exportadas de medida aproximada sin
consumidor. Se conserva `planRasterFrame`: no está conectado al runtime actual,
pero define el plan genérico `full/hold/tiles` protegido por el checker; su
destino pertenece a la decisión futura de Render Mode.

## Siguientes pasadas

- comprobar métodos y tipos C# con una única aparición aparente;
- cruzar cada candidato con XAML, reflection, manifests y serialización;
- revisar scripts activos y entradas de packaging sin tocar archivos
  históricos;
- repetir la detección tras cada slice hasta que no queden candidatos claros.
