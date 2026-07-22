# Ownership de validación — Fase 1

Fecha: 2026-07-22
Estado: inventario inicial; implementación no iniciada.

Norma de ejecución:
`docs/architecture/73_owner_validation_and_preview_document_boundary_contract.md`.

## Objetivo

Retirar validaciones y coerciones dispersas únicamente cuando su owner actual
sea inequívoco. No se creará un framework general de validación ni se cambiará
el resultado de datos current válidos.

## Inventario previo obligatorio

| Regla | Rutas actuales | Owner definitivo | Consumidores | Decisión inicial |
|---|---|---|---|---|
| Raíz objeto de los documentos serializados del Preview web | `renderablePayloadBoundary` solo exige Runtime Contract; `previewJsonHelpers.parseObject` convierte ausente o raíz incorrecta en `{}`; resolvers lo consumen | `renderablePayloadBoundary` para el envelope y parser estricto para la conversión local | todos los Component/Module resolvers y helpers de assets/tokens | Mover en 1.1; validar antes del registry y retirar coerción. |
| Modo Light/Dark efectivo | `DesignPreviewPayloadFactory` resuelve `appearanceMode`; `WebDesignPreviewRenderer` vuelve a combinar payload y sesión con precedencia asimétrica | payload factory; renderer solo transporta el modo ya efectivo | Preview estático, playback, Module y Screen | Corregir en 1.2 con aprobación del usuario: `light` y `dark` explícitos prevalecen; `inherit` usa sesión. |
| JSON object/array persistido | `JsonPath` estricto; wrappers de repositorios/fachada añaden contexto | documento/repository actual | startup, writes y data sources | Mantener; ya existe un owner común de root y no hay ruta permisiva equivalente. |
| Envelope Variant | `VariantEnvelopeContract` en reads/writes; startup y repositories lo consumen | `VariantEnvelopeContract` | Components, Modules, Usage y selección | Mantener; consolidado y estricto. |
| Runtime documents de Module Instance y Actor de mensajes | `ModuleRuntimeDocumentContracts` y `ConversationMessageActorContract` consumidos por startup y writes | owner de Runtime document por record class | persistence, editor y payload | Mantener; ya owner-driven. Revisar solo bypasses. |
| Animación v2, targets, retime y duraciones | validación de documento en data layer más contratos comunes de timeline/duration | documento de animación y common timeline según contrato | startup, editor, playback y payload | Auditar antes de mover; no fusionar validación de forma con cálculo temporal. |
| Typography Style | `TypographyStyleValue.Parse` acepta blank/`inherited`, pero también convierte raíz incorrecta en objeto vacío | `TypographyStyleValue` | control de diccionario y writes de Component Variant | Endurecer en 1.3 conservando solo los dos sentinels explícitos. |
| Referencias Component Variant embebidas | validación exacta en facade/domain y payload data source | contrato de composición embebida | writes, Preview y Usage | Auditar consumidores; no mover al registry ni repository genérico. |
| Contexto Shot → Actor → Theme/Device | servicios de Production context y payload data source | contexto explícito del Shot | Preview y navegación | Mantener; ya falla sin selección por nombre/orden. |
| Fuentes, iconos y media finales | `previewAssetResolver`, repositories de recursos y overlays con policies diferentes | resolver del asset concreto | Preview y futura exportación | Mantener por ahora; distinguir falta contractual de placeholder visual. |
| Validaciones del editor | controles de diccionario y handlers comprueban forma/selección antes del commit | ValueKind o owner document según cada campo | UI y writes | Inventariar por familia; retirar solo reconstrucciones semánticas demostradas. |

## Slices previstos

1. payload object roots y parser web estricto;
2. autoridad única del Theme mode efectivo;
3. Typography Style estricta;
4. animación y Runtime documents: localizar bypasses o validadores paralelos;
5. referencias y assets: confirmar owner o registrar separación deliberada;
6. pasada final de editores, Preview, scripts y tests.

Cada slice registrará ruta eliminada, pruebas, enforcement, riesgo y cualquier
responsabilidad que permanezca deliberadamente separada.

## Baseline

- rama: `main`;
- punto inicial de fase: `6244b9d7`;
- árbol: limpio;
- base canónica SHA-1:
  `9b0eae03ff952821162687e61c34b72afb88093a`;
- validación heredada: 52/52 Preview, 99/99 escritorio, arquitectura y build
  correctos.
