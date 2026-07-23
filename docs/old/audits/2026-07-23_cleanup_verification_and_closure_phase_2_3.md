# Verificación y cierre del baseline — Fases 2 y 3

Fecha: 2026-07-23
Estado: automatización Mac completa, incluidas las decisiones de cierre;
revisión UI y smoke PC pendientes.

Norma de ejecución:
`docs/architecture/74_cleanup_verification_and_baseline_closure_contract.md`.

## Fase 2 — Scripts y tests antidesviación

La protección se entregó junto a cada slice de limpieza, no como una capa
posterior separada:

- cada nuevo owner tiene cobertura válida e inválida;
- `check:architecture` exige la nueva ruta y prohíbe el patrón concreto
  retirado;
- los fixtures sintéticos encontrados con contratos antiguos se actualizaron
  a documentos current completos;
- los términos Component Preset retirados solo permanecen como patrones que el
  propio script busca y rechaza; Render Presets no se modificó;
- no se añadió framework general de validación, reflexión ni manager común.

Resultado tras cerrar las tres decisiones: 125/125 pruebas Preview y 116/116
pruebas de escritorio.

## Fase 3 — Evidencia automática Mac

| Comprobación | Resultado |
|---|---|
| Suite completa | Correcta. |
| TypeScript estricto | Correcto. |
| Código desktop sin unused diagnostic configurado | Correcto. |
| Arquitectura | Correcta. |
| Build desktop | Correcto, 0 warnings y 0 errores. |
| Base current | Validación explícita read-only con SQLite 3.53.3. |
| Hash de base antes/después | `6f13ae36420fb60f55f47738cc563a2b58d3fdadff67211210f38e5aec480cbb` (SHA-256), sin cambio. |
| `git diff --check` | Correcto. |
| Assets/parity data | Sin cambios incidentales. |

## Gates manuales pendientes

- abrir la última build validada cuando termine toda la limpieza y recorrer
  conjuntamente Diseño y Producción;
- verificar en PC apertura, edición, Preview, Shot playback, fuentes, assets y
  procesos sin consola;
- obtener aprobación explícita del usuario para aceptar el baseline.

La app no se abre todavía porque el usuario pidió agrupar la revisión UI al
final de todas las fases y no tiene acceso actual al Mac.

## Decisiones de cierre implementadas

| Decisión | Cierre |
|---|---|
| Icon Rows de Text Box/Text Input Bar | Contrato 77, migración completa y retirada de listas planas, inferencia y helper permisivo residual. |
| `durationInputId` | Contrato 75: field id estable único, sin alias por JSON key. |
| `localFrame` | Contrato 76: frame local de frontera separado del `screenFrame` raíz. |

## Estado del baseline

La limpieza automática disponible y sus decisiones de modelo están
estabilizadas. El baseline todavía no se declara aceptado ni se habilita
scaffolding/nueva expansión: faltan los gates manuales de UI y PC y la
aprobación explícita del usuario.
