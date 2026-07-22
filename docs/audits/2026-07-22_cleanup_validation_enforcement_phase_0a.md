# Limpieza, validación y enforcement — Fase 0A

Fecha: 2026-07-22
Estado: en curso, por slices cerrados.

## Objetivo

Cerrar rutas de compatibilidad y fallbacks semánticos ya prohibidos sin cambiar
el comportamiento de datos actuales válidos. Esta fase precede a la retirada de
código muerto, la consolidación de duplicaciones y el movimiento de
validaciones a sus owners.

## Inventario inicial

| Hallazgo | Clasificación | Decisión |
|---|---|---|
| Vocabulario Component Preset en producción | Migración cerrada | Retirado en el commit `cadf4611`; Render Presets permanecen. |
| `::preset::` en el test de Component Stack | Test negativo vigente | Mantener: demuestra que la referencia antigua falla. |
| Comandos raíz `legacy:` | Referencia histórica deliberada | Mantener mientras los contratos 25 y 26 los declaren accesibles y fuera de la validación actual. |
| Sustitución del Theme de Producción por el Theme seleccionado en Preview | Fallback semántico cerrado | Retirar en este slice. |
| Device vacío cuando falta el contexto heredado del Shot | Fallback semántico cerrado | Retirar en este slice. |
| Placeholders visuales de iconos y media | Policy visual deliberada | Mantener; no equivalen a reparación de contexto. |
| Defaults de sesión en selectores de Design Preview | Estado visual de sesión | Mantener; no pueden cruzar a Producción. |

## Slice 0A.1 — Contexto exacto del payload de Producción

Ruta anterior:

```text
Shot o Screen
→ buscar owner Actor
→ si falta Actor o Theme, usar Theme seleccionado en el panel
→ si falta Device, devolver vacío
```

Ruta vigente:

```text
Shot o Screen
→ Shot exacto
→ owner Actor exacto
→ default Theme y default Device explícitos
→ comprobar que ambos registros existen
→ preparar payload o fallar de forma visible
```

Ownership conservado:

- `DesignPreviewPayloadDataSource` lee el contexto exacto requerido por el
  payload y no elige sustitutos;
- `ProductionShotContextService` conserva los mensajes de contexto y el bloqueo
  de navegación;
- el selector Theme/Device de Design Preview sigue siendo estado de sesión y no
  repara Producción;
- repositories, bridge y renderer no reciben nuevas reglas.

Protección añadida:

- prueba con Actor, Theme y Device incompletos sobre una copia desechable;
- chequeo arquitectónico contra el retorno del Theme seleccionado, Device vacío
  y captura silenciosa de Theme ausente;
- las lecturas válidas continúan cubiertas por la prueba byte a byte existente.

## Riesgo y alcance

El riesgo funcional es bajo. Con datos válidos, el payload no cambia. Ante una
base corrupta o incompleta, el resultado pasa de un Preview aparentemente válido
a un error explícito. No cambian tablas, documentos persistidos, UI, navegación,
Variants, Runtime Inputs, animación ni renderizado.

## Siguientes candidatos, aún no ejecutados

- terminar la clasificación de defaults semánticos en creación de Themes;
- revisar valores de muestra de Actor dentro de payloads de Producción;
- retirar código que quede huérfano después de cerrar todos los fallbacks;
- construir el inventario de validaciones antes de mover ninguna a sus owners.

Cada candidato requiere un slice separado con evidencia, pruebas y enforcement.
