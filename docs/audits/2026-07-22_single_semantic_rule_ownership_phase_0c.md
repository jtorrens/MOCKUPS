# Propiedad única de reglas semánticas — Fase 0C

Fecha: 2026-07-22
Estado: inventario en curso.

## Objetivo

Localizar responsabilidades vivas resueltas en más de un lugar, distinguirlas
de similitudes superficiales y consolidar únicamente las reglas cuya
divergencia sería necesariamente un defecto.

Norma de ejecución: contrato 72.

## Registro obligatorio por familia

Cada familia se documentará antes de cambiar código con:

| Campo | Contenido requerido |
|---|---|
| Regla | Descripción en términos del producto. |
| Rutas actuales | Implementaciones y consumidores activos. |
| Paridad | Resultado y errores que deben coincidir. |
| Owner definitivo | Capa y contrato que le asignan la decisión. |
| Cambio mínimo | Consumidores que migran y ruta completa que desaparece. |
| Pruebas | Cobertura de paridad, regresión y enforcement. |
| Riesgo | Persistencia, Preview, plataforma y UX. |
| Decisión | Consolidar, mantener separadas, diferir o descartar. |

## Límites de la batida

- no se consolidará por nombre, forma o proximidad;
- no se moverán validaciones de manera general antes de la fase 1;
- no se cambiarán datos, UX ni contratos actuales;
- no se crearán managers, bases o interfaces anticipatorias;
- no se dejarán wrappers ni rutas alternativas tras una consolidación;
- cualquier divergencia funcional se registrará para decisión antes de editar.

## Familias por revisar

La primera batida cubrirá como mínimo:

- duración, origen temporal, extensión de secuencias y retime;
- targets, vocabulario animable y proyección de keyframes;
- referencias exactas de Actor, Theme, Device, Variant y assets;
- Runtime Inputs, forwarding y contratos efectivos;
- composición de payloads entre Design, Production y Preview;
- lectura y escritura de documentos current JSON;
- resolución de tokens, fuentes, iconos y rutas de media;
- Usage, protección destructiva y navegación a referencias;
- selección de contexto Shot, Screen y Module Instance;
- procesos externos, empaquetado y localización de recursos.

## Resultado de la batida

### Familia 0C.1 — Resolución del ejecutable Node

| Campo | Resultado |
|---|---|
| Regla | Localizar el mismo ejecutable Node empaquetado o disponible en el sistema para los procesos externos de Preview. |
| Rutas actuales | `WebDesignPreviewRenderer` y `ChromiumPreviewRasterizer` mantienen listas locales equivalentes para Mac y Windows. |
| Paridad | Mismo orden de candidatos, misma comprobación de existencia y mismo fallback final `node`/`node.exe`. |
| Owner definitivo | `DesktopChildProcess`, soporte común ya usado por ambos procesos para construir su arranque oculto. |
| Cambio mínimo | Exponer allí la resolución existente, migrar dos llamadas y borrar los dos métodos locales. |
| Pruebas | Test directo de resolución por plataforma y enforcement que exige ambos consumidores comunes y prohíbe las copias locales. |
| Riesgo | Bajo; no cambia argumentos, working directory, ciclo de vida, timeout, renderer ni Raster. |
| Decisión | Consolidar como primer slice. |

Las búsquedas de raíz de repositorio se mantienen separadas: parten de lugares
distintos y aplican criterios distintos, por lo que su parecido no demuestra
una única regla. Los ciclos de vida de los procesos persistentes, one-shot y
FFmpeg también quedan fuera de este slice hasta comparar sus contratos de
cancelación, timeout y errores.

### Familia 0C.2 — Gramática de referencias completas a Variants

| Campo | Resultado |
|---|---|
| Regla | Formar y separar la referencia estable común `ownerId::variant::variantId`, y reconocer su Variant por id. |
| Rutas actuales | Component Classes y Modules tienen formatters/parsers equivalentes; cuatro controles de editor vuelven a concatenar, cortar o comprobar el mismo separador. |
| Paridad | Un owner y una Variant no vacíos, primer separador ordinal y resto completo como Variant; el id `default` se reconoce sin inferirlo del tipo, nombre u orden. |
| Owner definitivo | Helper común de identidad de Variant; la existencia y semántica concreta permanecen en los owners Component/Module. |
| Cambio mínimo | Migrar formatters, parsers y comprobaciones activas; retirar constantes, métodos y cortes locales. |
| Pruebas | Gramática válida/malformada, formato exacto y detección de `default`; enforcement contra parsers locales conocidos. |
| Riesgo | Bajo-medio por número de consumidores; no cambia ids persistidos ni referencias. |
| Decisión | Consolidar en un slice aislado. |

No se unifican los envelopes de Component y Module ni sus reglas de ciclo de
vida: comparten la identidad de referencia, pero siguen teniendo propietarios
y documentos distintos.

### Familia 0C.3 — Selección de recursos del Preview

| Campo | Resultado |
|---|---|
| Regla | Conservar el Device o Theme de Preview si todavía existe; si no, seleccionar la primera opción disponible para la sesión. |
| Rutas actuales | `RefreshOptions` y `EnsureSelectedOptionsExist` repiten el mismo bloque para Device y Theme dentro de `EditorPreviewController`. |
| Paridad | Selección existente, selección ausente/desaparecida y lista vacía deben resolverse igual en carga y recuperación. |
| Owner definitivo | `EditorPreviewController`; es estado de selección del Preview, no contexto persistente de Shot ni fallback de repositorio. |
| Cambio mínimo | Un helper propietario consumido por ambos recorridos y ambos tipos de recurso. |
| Pruebas | Selección conservada, primera opción ante valor inexistente o vacío, y `null` ante lista vacía. |
| Riesgo | Bajo; no cambia orden, opciones, persistencia ni contexto de Production. |
| Decisión | Consolidar. |

### Familia 0C.4 — Preparación de payload desde definición o Variant

| Campo | Resultado |
|---|---|
| Regla | Una fuente Component o Module ya cargada se transforma del mismo modo en payload de Design Preview, venga de la definición o de una Variant concreta. |
| Rutas actuales | `FromComponentClass`/`FromComponentVariant` y `FromModule`/`FromModuleVariant` duplican toda la preparación tras elegir la fuente. |
| Paridad | Config, forwarding, Runtime JSON, Actor de prueba, recursos visuales, bases de Components, App config y appearance mode deben coincidir para la misma fuente. |
| Owner definitivo | `DesignPreviewPayloadFactory`; el data source conserva las cargas explícitas de definición y Variant. |
| Cambio mínimo | El switch elige la fuente y dos constructores únicos preparan Component o Module. |
| Pruebas | Suite de payload/render de definición, Variant, Module, forwarding y composición existente; enforcement prohíbe restaurar los cuatro constructores paralelos. |
| Riesgo | Medio-bajo; cruza Preview pero es una extracción mecánica sin alterar documentos. |
| Decisión | Consolidar. |

### Familia 0C.5 — Origen absoluto de una Screen

| Campo | Resultado |
|---|---|
| Regla | Sumar las duraciones de las Screens anteriores, en orden estable, para obtener el inicio absoluto de una Module Instance dentro del Shot. |
| Rutas actuales | `ModuleInstanceTimeline.ScreenStartFrame` es el owner declarado; `EditorPreviewController` y `DesignPreviewPayloadDataSource` repiten el bucle. |
| Paridad | Mismo Shot, mismos ids ordenados y mismas duraciones contract-owned producen el mismo inicio; una id ausente conserva el resultado actual `0`. |
| Owner definitivo | `ModuleInstanceTimeline`, por contrato 52. |
| Cambio mínimo | Los dos consumidores delegan el origen; el factory conserva la conversión al frame local y el controlador conserva presentación/log. |
| Pruebas | Integración existente de Module Instance/Shot y enforcement contra el helper local y el bucle del data source. |
| Riesgo | Bajo; no cambia duración, orden, playhead ni keyframes. |
| Decisión | Consolidar. |

La selección de la Screen activa para un frame de Shot queda diferida. El
factory la posee para construir payload según contrato 51, mientras el
controlador la usa para navegación de sesión. Antes de compartir una proyección
hay que decidir si ambos contextos deben consumir un nuevo resultado temporal o
si sus límites justifican operaciones distintas; no se crea esa abstracción en
esta batida.

### Familia 0C.6 — Localización e id nuevo dentro del envelope Variant

| Campo | Resultado |
|---|---|
| Regla | Encontrar una entrada current por su id estable y generar un id nuevo no colisionante a partir de un nombre. |
| Rutas actuales | Component y Module mantienen búsquedas equivalentes y generadores `lower_snake` con sufijos `_2`, `_3`, etc. |
| Paridad | Mismo nombre y mismos ids existentes producen el mismo id; la búsqueda ordinal devuelve la misma entrada o ausencia. |
| Owner definitivo | `VariantEnvelopeContract`, que ya valida y expone el array current común. |
| Cambio mínimo | Añadir las dos operaciones al contrato, migrar ambos ciclos de vida y borrar helpers locales. |
| Pruebas | Búsqueda, ausencia, normalización, fallback `variant` y colisiones; permanecen las pruebas de lifecycle de Component/Module. |
| Riesgo | Bajo; no cambia documentos ni acciones permitidas. |
| Decisión | Consolidar. |

### Familia 0C.7 — Construcción de una Variant completa

| Campo | Resultado |
|---|---|
| Regla | Una Variant nueva es siempre un envelope completo con id, nombre, `protected`, `locked` y config objeto explícitos. |
| Rutas actuales | Duplicar/guardar Component Variant y guardar Module Variant construyen manualmente el mismo objeto. |
| Paridad | Las tres rutas crean exactamente las mismas claves y valores de flags; cada owner sigue suministrando su snapshot config. |
| Owner definitivo | `VariantEnvelopeContract`. |
| Cambio mínimo | Un constructor de source completo y tres consumidores; desaparecen los tres literales paralelos. |
| Pruebas | Forma exacta del objeto y mantenimiento de los tests de lifecycle. |
| Riesgo | Bajo. |
| Decisión | Consolidar. |

### Familia 0C.8 — Identificador estable de la Default Variant

| Campo | Resultado |
|---|---|
| Regla | La Variant protegida que existe por defecto en todo owner actual usa el id estable `default`. |
| Rutas actuales | Component y Module declaran constantes locales distintas; validación, Usage, repositorios y selección repiten además el literal. |
| Paridad | Component y Module deben validar, localizar y preferir exactamente el mismo id sin inferirlo del nombre, orden o tipo. |
| Owner definitivo | `VariantEnvelopeContract`, propietario del envelope current compartido. |
| Cambio mínimo | Una constante común; migrar comparaciones activas y retirar las dos constantes locales. |
| Pruebas | Valor estable directo, suites de lifecycle y enforcement contra la reaparición de constantes por owner. |
| Riesgo | Bajo; no cambia ids, referencias, documentos ni selección. |
| Decisión | Consolidar. |

### Familia 0C.9 — Resolución local de media de referencia

| Campo | Resultado |
|---|---|
| Regla | Una ruta local absoluta o relativa se resuelve contra el media root explícito del Project. |
| Rutas actuales | `ProjectPathService.ResolveLocalPath` ya sirve a imágenes y avatar; `PreviewReferenceOverlay` reconstruye la misma decisión localmente. |
| Paridad | Una ruta absoluta se conserva y una relativa usa el mismo Project media root; no se buscan archivos por nombre ni ubicación. |
| Owner definitivo | `ProjectPathService`, owner común de rutas locales del Project. |
| Cambio mínimo | La referencia visual delega al servicio existente y elimina su método local. |
| Pruebas | Ruta relativa, ruta absoluta y enforcement contra la reaparición del resolver local. |
| Riesgo | Bajo; no cambia URI, caché, lectura, vídeo, FFmpeg ni persistencia. |
| Decisión | Consolidar. |

### Familia 0C.10 — Texto de iniciales del Actor

| Campo | Resultado |
|---|---|
| Regla | Un Actor se representa con las iniciales de las dos primeras palabras de `shortName`, o de `displayName` cuando el primero está vacío, en mayúsculas. |
| Rutas actuales | El payload Runtime y la inspección inline mantienen implementaciones literales independientes. |
| Paridad | El mismo Actor debe mostrar exactamente las mismas iniciales en ambas superficies; solo el tamaño y la presentación permanecen locales. |
| Owner definitivo | `ActorIdentityText`, helper común de identidad; cada factory conserva su payload o control visual según contrato 53. |
| Cambio mínimo | Dos consumidores comunes y retirada de los dos métodos locales. |
| Pruebas | Nombre corto, fallback a nombre visible, una palabra, vacío y enforcement de ambos consumidores. |
| Riesgo | Bajo; no cambia datos, payload, campos, colores, crop ni persistencia. |
| Decisión | Consolidar. |
