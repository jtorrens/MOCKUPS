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
| Actor de muestra como Actor principal de un Screen de Producción | Fallback semántico cerrado | Retirado en el slice 0A.2; el Shot siempre aporta un owner Actor explícito. |
| Actor de muestra en Diseño aislado | Fixture visual deliberado | Mantener: permite inspeccionar un Module o Component sin payload persistente. |
| Actor de muestra para un mensaje de Producción sin `actorId` | Migración funcional aprobada y pendiente | Mantener solo hasta el slice 0A.3; el proyecto actual contiene mensajes que deben migrarse al contrato explícito por dirección. |
| `animation_json` nulo reparado como `{}` durante la preparación del payload | Fallback de documento actual cerrado | Retirado en el slice 0A.2; el documento actual debe llegar completo. |
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

## Slice 0A.2 — Actor principal y animación exactos en Producción

El Actor principal de un Screen se resuelve mediante el Runtime Actor explícito
cuando existe y, en caso contrario, mediante el owner Actor exacto del Shot. Si
ambos faltan, el payload falla; nunca fabrica `sample_actor`. La animación se
consume como el objeto actual completo ya validado por el repository, sin
convertir un documento nulo en `{}`.

Se mantiene de forma deliberada:

- `sample_actor` en Diseño aislado;
- el sample visual de mensajes sin `actorId` únicamente hasta ejecutar la
  migración aprobada del slice 0A.3.

Condición exacta para retirar el segundo caso:

1. documentar el contrato aprobado: incoming explícito, outgoing heredado del
   Shot y system opcional;
2. migrar los documentos actuales sin inferencias por nombre o posición;
3. validar las referencias en el owner del Runtime payload;
4. hacer que el formato incompleto falle y retirar el sample del mensaje en el
   mismo cambio.

La prueba de este slice recorre todos los Screens actuales, comprueba que su
Actor resuelto nunca es el sample, compara la animación del payload con el
documento persistido y demuestra que la lectura es byte a byte inmutable.
