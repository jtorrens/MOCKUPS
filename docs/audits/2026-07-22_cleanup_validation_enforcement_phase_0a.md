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
| Actor de muestra para un mensaje de Producción sin `actorId` | Fallback semántico cerrado | Retirado en el slice 0A.3 mediante el contrato explícito por dirección. |
| `animation_json` nulo reparado como `{}` durante la preparación del payload | Fallback de documento actual cerrado | Retirado en el slice 0A.2; el documento actual debe llegar completo. |
| Placeholders visuales de iconos y media | Policy visual deliberada | Mantener; no equivalen a reparación de contexto. |
| Defaults de sesión en selectores de Design Preview | Estado visual de sesión | Mantener; no pueden cruzar a Producción. |
| Theme solicitado inexistente sustituido por el primer Theme del Project al inspeccionar tokens | Fallback semántico cerrado | Retirado en el slice 0A.4; la selección exacta debe existir. |
| Valores de tokens de `iOS/Android starting point` al crear un Theme | Construcción explícita de documento nuevo | Mantener mientras no se rediseñe el flujo de creación. |
| Primer Icon Theme y primeras fuentes elegidos al crear un Theme | Automatismo semántico pendiente | No tocar sin aprobar un flujo de selección explícita; no es un fallback de lectura. |

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

## Slice 0A.3 — Propiedad explícita del Actor de cada mensaje

Contrato aprobado:

- incoming persiste un Actor explícito del mismo Project;
- outgoing persiste `actorId` vacío y proyecta el owner Actor exacto del Shot
  únicamente al preparar el payload de Producción;
- system admite un Actor explícito opcional y nunca recibe un Actor de muestra
  si está vacío.

La regla reside en un único owner de dominio seleccionado por el record class
estable del Module. La validación de apertura y todas las escrituras de content
usan ese mismo owner. El editor solo ejecuta metadatos declarativos y guarda en
una única operación los cambios de dirección que también limpian el Actor.

La representación visual permanece separada de la propiedad semántica: solo
un incoming de grupo puede mostrar identidad por mensaje; outgoing y system no
la muestran aunque el payload resuelva o contenga un Actor.

La migración canónica ha eliminado la referencia redundante del outgoing de
prueba y conserva el incoming explícito. La selección `None` aparece únicamente
para system; incoming exige Actor antes de persistirse. Quedan cubiertos el
rechazo read-only de documentos inválidos, la escritura atómica, la proyección
del Shot owner y la ausencia de `sample_actor` en Producción.

## Slice 0A.4 — Selección exacta al inspeccionar tokens de Theme

El selector visual puede iniciar una sesión mostrando el primer Theme de su
lista, pero una vez elegido un id la consulta exige ese Theme exacto dentro del
Project exacto. Un id ausente o perteneciente a otro Project falla y no muestra
silenciosamente los tokens de otro Theme.

La creación queda clasificada, no rediseñada:

- los documentos base iOS/Android son construcción explícita de una entidad
  nueva y no reparación de datos existentes;
- la elección automática del primer Icon Theme y las primeras fuentes es un
  automatismo semántico pendiente de retirar;
- sustituirlo requiere aprobar antes las elecciones y presentación del diálogo
  de creación, por lo que no se mezcla con este slice sin cambio de UX válida.

La regresión prueba el rechazo del Theme inexistente y el enforcement impide
reintroducir la sustitución por el primero del Project.
