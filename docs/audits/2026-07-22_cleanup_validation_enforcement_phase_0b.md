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
| Métodos privados, XAML, converters y DTOs C# | El build estándar no demuestra por sí solo que estén huérfanos | Auditar después de 0B.1 con referencias directas, XAML, reflection y serialización. |

## Slice 0B.1 — Bindings TypeScript sin consumidor

Se retiran cálculos locales, imports y un parámetro que no participan en ningún
resultado de Audio, Bubble, Collection Stack, Conversation, Keypad, Lock Screen,
Text Input Bar ni el adapter HTML. Los resolvers propietarios conservan todas
las validaciones de entrada; el helper de item deja de repetir un parse cuyo
resultado se descartaba.

El `typecheck` pasa a exigir ausencia de locales y parámetros TypeScript no
usados. Esto convierte el inventario inicial en una regla continua sin añadir
conocimiento de Components al bridge o renderer.

## Siguientes pasadas

- comprobar métodos y tipos C# con una única aparición aparente;
- cruzar cada candidato con XAML, reflection, manifests y serialización;
- revisar scripts activos y entradas de packaging sin tocar archivos
  históricos;
- repetir la detección tras cada slice hasta que no queden candidatos claros.
