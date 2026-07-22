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
