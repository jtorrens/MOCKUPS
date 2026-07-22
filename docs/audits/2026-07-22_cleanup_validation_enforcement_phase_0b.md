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

## Siguientes pasadas

- comprobar métodos y tipos C# con una única aparición aparente;
- cruzar cada candidato con XAML, reflection, manifests y serialización;
- revisar scripts activos y entradas de packaging sin tocar archivos
  históricos;
- repetir la detección tras cada slice hasta que no queden candidatos claros.
