# Ownership de validación — Fase 1

Fecha: 2026-07-22
Estado: implementación en curso.

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

## Slice 1.1 — Documentos objeto del payload web

| Campo | Resultado |
|---|---|
| Hallazgo | El boundary solo exigía `runtimeContractJson`; el parser compartido convertía ausencia o raíz incorrecta de cualquier otro documento en `{}`. Forwarding mantenía además otro parser de objetos. |
| Owner | `renderablePayloadBoundary` declara el envelope requerido; `previewJsonHelpers.parseObject` realiza una única conversión estricta reutilizable. |
| Cambio mínimo | Validar siete raíces requeridas antes del registry, validar el icon mapping opcional cuando está presente, endurecer `parseObject` y retirar el parser local de forwarding. |
| Ruta eliminada | `JSON.parse(json || "{}")` seguido de `asRecord`, más `runtimeInputForwarding.parseRecord`. |
| Pruebas | Documento completo; ausencia, blank, JSON malformado y raíz array para cada campo; icon mapping ausente y presente inválido. |
| Enforcement | Lista explícita de campos en el boundary y prohibición de las dos coerciones retiradas. |
| Riesgo | Bajo para current data; las entradas inválidas ahora fallan antes de routing. No cambia payload válido, forwarding ni renderables. |

## Slice 1.2 — Autoridad del modo de Theme efectivo

| Campo | Resultado |
|---|---|
| Hallazgo | La factory respetaba `light`/`dark` explícitos, pero el renderer volvía a combinar payload y sesión: una sesión oscura anulaba un `light` explícito. La lectura y la escritura del módulo también convertían valores ausentes o desconocidos en `inherit`. |
| Owner | `ModuleAppearanceModeContract` valida el valor del documento; `DesignPreviewPayloadFactory` resuelve `inherit` y prepara un `ThemeMode` final. |
| Cambio mínimo | Los payloads de Component, Module y Screen llevan siempre `light` o `dark`; el renderer transporta solo ese valor; lectura, escritura y presentación consumen el mismo contrato estricto. |
| Rutas eliminadas | Segunda combinación en `WebDesignPreviewRenderer`, fallback del controller, fallback de lectura y coerción de escritura a `inherit`. |
| Pruebas | `light` y `dark` explícitos sobre la sesión contraria; `inherit`; Component aislado; valor ausente, tipo incorrecto y valor desconocido; escritura rechazada sin modificar el documento. |
| Enforcement | Owner común requerido en factory, data y controller; renderer sin parámetro de sesión ni recomposición; coerciones antiguas prohibidas. |
| Riesgo | Bajo para current data, cuyos valores son válidos. Cambia únicamente la entrada inválida y corrige la precedencia de `light` explícito aprobada por el usuario. |

## Slice 1.3 — Typography Style estricta

| Campo | Resultado |
|---|---|
| Hallazgo | El value object convertía una raíz array, número u otra raíz incorrecta en un objeto vacío; las escrituras de Component podían saltarse el value object y `TypographySystemStyle` se serializaba por la rama genérica. Usage repetía un parser permisivo. |
| Owner | `TypographyStyleValue` para `TypographyStyle` y `TypographySystemStyle`, tanto en representación de texto como en nodo JSON. |
| Cambio mínimo | Conservar únicamente blank e `inherited` como sentinels vacíos; exigir objeto para cualquier otro valor; usar el owner en lectura, escritura y descubrimiento de Usage. |
| Rutas eliminadas | `as JsonObject ?? []`, parse directo de escritura y parser local de Usage. |
| Pruebas | Sentinels, objeto válido, texto malformado, array y número; escritura `TypographySystemStyle` válida como objeto; rechazo sin modificar el documento. |
| Enforcement | Parser requerido y consumidores de persistence/Usage fijados; fallbacks y bypass de escritura prohibidos. |
| Riesgo | Bajo para current data; el Keyboard actual ya persiste un objeto. Las entradas que antes quedaban ocultas o podían guardar una raíz incorrecta ahora fallan. |

## Slice 1.4 — Documento de animación v2

| Campo | Resultado |
|---|---|
| Hallazgo | Persistence mantenía un validador privado mientras el documento del editor validaba solo raíz y versión. Ambos ignoraban entradas de array que no fueran objetos. Los writes combinados de colección/animación no revalidaban el documento completo. Un track current conservaba físicamente KF28 antes de KF0 y el resolver lo ordenaba en memoria. |
| Owner | `ModuleInstanceAnimationDocumentContract` para la forma current v2; timeline y resolvers conservan por separado los cálculos temporales y la interpretación de valores. |
| Cambio mínimo | Unificar startup, writes, cambio de Variant y editor; exigir entradas explícitas, ids, interpolation/enabled, retime positivo, KF0 y orden persistido; validar también los writes combinados. |
| Ruta eliminada | `SpikeDatabase.ValidateAnimationJson` privado y la validación parcial del constructor del editor. |
| Migración explícita | Se ordenó por frame/id un único track de `module_instance_900f1616432d4f63a97f2a74dd647e08`; 1 fila y 1 track. Se restituyó después la codificación escapada de Unicode que usa el escritor C# para conservar el round-trip textual exacto. No cambiaron ids, frames, valores, targets ni interpolaciones. Ambos scripts temporales se eliminaron en la misma entrega. SHA-1 anterior `9b0eae03ff952821162687e61c34b72afb88093a`; posterior `0a5f67db62f4969cec8e3ef67c4ed39dff0b00a9`. |
| Pruebas | Raíces, entradas no objeto, propiedades obligatorias, duplicados, frames negativos, orden, retime, KF0, persistencia exacta del store y apertura read-only de la base migrada. |
| Enforcement | Owner común requerido en startup/writes/editor, validador paralelo prohibido y orden current de la base comprobado. |
| Riesgo | Bajo: la migración solo materializa el orden que el resolver ya aplicaba, pero elimina una tolerancia contraria al contrato y evita que vuelva a persistirse. |

## Slice 1.5 — Correspondencia Runtime Input `kind`/`ValueKind`

| Campo | Resultado |
|---|---|
| Hallazgo | Startup comprobaba dos vocabularios permitidos por separado, el panel volvía a parsear `ValueKind` y ninguna ruta exigía que ambos campos describieran la misma forma. Había dos pares current incoherentes. |
| Owner | `RuntimeInputValueKindContract` mantiene la única correspondencia exhaustiva y valida el par exacto sin derivar ni reparar un campo desde el otro. |
| Cambio mínimo | Startup y presentación consumen `RequireCompatible`; se retiran la lista y el parser paralelos. La construcción de Forward sigue usando el mismo `InputKind(ValueKind)`. |
| Migración explícita | Por ids estables se cambió `component_project_foqn_s2_componentStack / alternatives` de `text` a `collection`, y `module_core_chat / mediaSource` de `text` a `mediaFilePath`. No cambió ningún `ValueKind`, id, default, forwarding ni payload. El script temporal se eliminó. SHA-1 anterior `0a5f67db62f4969cec8e3ef67c4ed39dff0b00a9`; posterior `ca53a71d8a51f6fc56ae1699ceb669eb49f02653`. |
| Pruebas | Pares válidos de texto, media y colección; pares incompatibles y nombres desconocidos; validación read-only de todos los documentos current. |
| Enforcement | Owner y consumidores requeridos, parsers/listas paralelos prohibidos y par exacto comprobado sobre la base canónica. |
| Riesgo | Bajo: `ValueKind` ya seleccionaba los controles correctos. La migración alinea la metadata de forma que consume forwarding y evita futuras interpretaciones contradictorias. |

## Slice 1.6 — Config current de Module y sus Variants

| Campo | Resultado |
|---|---|
| Hallazgo | El repositorio exigía únicamente raíz objeto. La presentación y la escritura de campos convertían objetos o arrays con raíz incorrecta en `{}`/`[]`, valores booleanos desconocidos en `false`, números inválidos en cero y un alignment desconocido en `left`. Las Variants completas tampoco pasaban por un contrato semántico de su clase. |
| Owner | `CurrentModuleConfigContract` enruta por `record_class_id` exacto a los contratos de Conversation y Lock Screen. Definición y Variants usan el mismo owner. |
| Cambio mínimo | Validar ambos documentos en startup, repository y commits; hacer estrictas las raíces anidadas, booleanos, números, options, slots y referencias completas requeridas; conservar `messageViewportMotion` ausente como optional declarado. |
| Rutas eliminadas | `as JsonObject ?? new JsonObject`, `as JsonArray ?? new JsonArray`, lecturas `?? {}`/`?? []`, `JsonBoolString` y las coerciones de número/alignment en los campos de Module. |
| Pruebas | 104/104 escritorio: ambos record classes válidos; config de definición y Default Variant dañada rechazada read-only; objetos/arrays, booleanos, números y options inválidos rechazados sin modificar la base; escritura válida de Variant verificada. |
| Enforcement | Router y tres consumidores obligatorios; validación de cada `variant.Config`; fallbacks retirados prohibidos. |
| Datos | Sin migración. La base ya cumplía los dos contratos; SHA-1 permanece `ca53a71d8a51f6fc56ae1699ceb669eb49f02653`. |
| Riesgo | Bajo para current data. Solo deja de ocultarse entrada inválida; ids, referencias, forwarding, Overrides, payloads y resultado visual no cambian. |

## Slice 1.7 — Defaults de Runtime Input y `BehaviorTiming`

| Campo | Resultado |
|---|---|
| Hallazgo | La reconciliación mantenía un parser propio por `kind`: un booleano inválido pasaba a `false`, un número inválido terminaba como texto, icon lists/collections podían quedar vacías y `BehaviorTimingValue` capturaba cualquier error para devolver fixed/0. Startup solo validaba el par `kind`/`ValueKind`, no su default. |
| Owner | `RuntimeInputValueKindContract` valida el par y materializa el default exacto por `ValueKind`; `BehaviorTimingValue` valida su objeto semántico. |
| Cambio mínimo | Exigir defaults string actuales, parsear formas escalares y arrays estrictamente, declarar el array vacío únicamente para `StructuredCollection` con contrato de colección explícito y usar el owner en startup, reconciliación y cambio de Variant. |
| Rutas eliminadas | `RuntimeDefaultValue` privado, `bool.TryParse && value`, parseos `?? []`/`?? {}` y el catch-all de `BehaviorTimingValue`. |
| Pruebas | 105/105 escritorio: defaults válidos por familia, raíces y valores inválidos, colección proyectada explícita, semántica de timing y dos corrupciones de base rechazadas byte-for-byte read-only. |
| Enforcement | Método owner y tres consumidores obligatorios; parser paralelo y catch de timing prohibidos. |
| Datos | Sin migración. Todos los defaults current ya cumplen; SHA-1 permanece `ca53a71d8a51f6fc56ae1699ceb669eb49f02653`. |
| Riesgo | Bajo. Solo cambia la entrada inválida y la reconciliación futura de contratos dañados; payload current y UI válida no cambian. |

## Slice 1.8 — Colecciones Runtime persistidas

| Campo | Resultado |
|---|---|
| Hallazgo | Add e Insert creaban un array cuando la colección faltaba o tenía otra raíz; Insert añadía al final cuando no encontraba el id de referencia. Las demás operaciones comprobaban el array pero no que su key estuviera declarada. La reconciliación proyectada filtraba items no objeto o sin id. |
| Owner | El contrato Runtime efectivo declara la storage key; `RuntimeCollectionDocumentContract` valida array, objetos e ids estables únicos; el coordinador de instancia conserva la escritura completa y la sincronización temporal. |
| Cambio mínimo | Exigir una declaración exacta en todas las mutaciones, rechazar roots e ids inválidos, hacer explícito el único caso que crea array vacío al cruzar a una Variant que declara una colección nueva y validar startup/read-write con el mismo owner. |
| Rutas eliminadas | `as JsonArray ?? new JsonArray`, append ante anchor ausente y filtros `OfType` que descartaban silenciosamente items actuales inválidos. |
| Pruebas | 105/105 escritorio: operaciones completas sobre `messages` real; key no declarada, item sin id, id duplicado y anchor ausente rechazados sin escritura; corrupción de id duplicado y root Lock Screen incorrecta rechazadas read-only. |
| Enforcement | Owner común y consumidores obligatorios; startup owner requerido; creación implícita y append ambiguo prohibidos. |
| Datos | Sin migración. Todas las colecciones current ya cumplen; SHA-1 permanece `ca53a71d8a51f6fc56ae1699ceb669eb49f02653`. |
| Riesgo | Bajo. Se conserva la creación explícita de colección al reconciliar una nueva frontera; solo dejan de aceptarse documento o intención inválidos. |

## Slice 1.9 — Valores Runtime declarados y serialización de editor

| Campo | Resultado |
|---|---|
| Hallazgo | La escritura escalar aceptaba cualquier key/nodo y las celdas de colección cualquier field. Test Values, bindings embebidos y keyframes mantenían serializadores paralelos que convertían booleanos o números inválidos en `false`/cero y algunos documentos en objetos/arrays vacíos. Startup comprobaba presencia, pero no forma por `ValueKind`. |
| Owner | El contrato Runtime efectivo resuelve la definición exacta; `RuntimeInputValueKindContract` serializa el texto del editor y valida el nodo persistido por `ValueKind`. |
| Cambio mínimo | Validar top-level inputs y fields current, rechazar source no Runtime persistido, exigir definición única en cada write y reutilizar `ParseValue` en Test Values, bindings y keyframes. Se añadieron formas objeto explícitas para Motion, Placement, Motion Timing, Typography y bindings. |
| Rutas eliminadas | Writes por key libre, serializers por `ComponentInputKind`, `BooleanText.Parse` permisivo, `TryParse ? value : 0` y parseos vacíos de arrays/objetos en estas rutas. |
| Pruebas | 105/105 escritorio: scalar/field válido real; key/field no declarados y tipos incorrectos rechazados sin escritura; dos corrupciones de valor current rechazadas read-only; formas Motion/Placement y roots inválidos comprobados. |
| Enforcement | Owner con `ParseValue`/`ValidateRuntimeValue`, cuatro consumidores y validación startup obligatorios; serializadores permisivos concretos prohibidos. |
| Datos | Sin migración. Todos los valores current ya cumplen; SHA-1 permanece `ca53a71d8a51f6fc56ae1699ceb669eb49f02653`. |
| Riesgo | Bajo. El payload válido no cambia; se elimina únicamente persistencia o presentación derivada de una entrada que contradice su contrato. |

## Slice 1.10 — Envelope y proyección de Forwarding

| Campo | Resultado |
|---|---|
| Hallazgo | El envelope reservado de Forwarding se ignoraba si tenía otra raíz, las definiciones no objeto se filtraban y varias listas/proyecciones se sustituían por objetos o arrays vacíos. Un contrato Runtime de un hijo proyectado podía fabricarse como `{}`. El boundary web repetía parte de esa tolerancia. |
| Owner | `RuntimeInputForwardingContract` prepara el Preview efectivo y recorre el envelope explícito; startup valida el documento current y el boundary web aplica el mismo contrato antes del registry. |
| Cambio mínimo | Exigir objeto para `$forwardedInputs` y cada definición, arrays para listas Runtime presentes, objetos para contratos de hijo y keys explícitas para proyecciones; conservar únicamente la creación intencional de las listas top-level ausentes en un Preview nuevo. |
| Rutas eliminadas | Clones `as JsonObject ?? {}`, listas wrong-root convertidas a `[]`, `OfType` que omitía entradas, nested Runtime contract ausente convertido a `{}` y forwarding web no objeto ignorado. |
| Pruebas | 106/106 escritorio y 86/86 Preview: envelope/definición/root inválidos, metadata interna inválida, forwarding válido y corrupción de la base rechazada byte-for-byte read-only. |
| Enforcement | Owner requerido en startup y payload; fallbacks concretos prohibidos; comprobación explícita del envelope tanto en C# como en TypeScript. |
| Datos | Sin migración. Todos los envelopes y proyecciones current ya cumplen; SHA-1 permanece `ca53a71d8a51f6fc56ae1699ceb669eb49f02653`. |
| Riesgo | Bajo. Forwarding válido conserva ids, referencias completas, valores y resultado; solo deja de publicarse un payload plausible a partir de documentos dañados. |

## Slice 1.11 — Documentos compuestos de diccionario

| Campo | Resultado |
|---|---|
| Hallazgo | Component Input Bindings convertía blank, JSON malformado o raíz incorrecta en `{}`; Icon Slots hacía lo mismo con `[]` y filtraba items no objeto; la colección estructurada fabricaba inputs/Overrides vacíos y una identidad `item-{posición}` al encontrar documentos incompletos. |
| Owner | `RuntimeInputValueKindContract` parsea el `ValueKind` compuesto; `RuntimeCollectionDocumentContract` valida items e ids; `RuntimeInputForwardingContract` valida el envelope reservado dentro de bindings. |
| Cambio mínimo | Reutilizar esos owners en los tres controles, exigir inputs/Overrides de items existentes, clonar sin fallback y conservar objetos vacíos únicamente al crear explícitamente un item o cruzar una frontera aún sin Component seleccionado. |
| Rutas eliminadas | Catch-all de Icon Slots/bindings, blank-to-empty, `OfType` que descartaba items, parse de inputs wrong-root a `{}`, clone fallback e id derivado de posición. |
| Pruebas | 106/106 escritorio específicas: collections e Icon Slots sin id/duplicados, bindings wrong-root y forwarding interno wrong-root; build del editor correcto. |
| Enforcement | Los tres controles deben consumir el owner compartido y no pueden recuperar los parsers/coerciones retirados. |
| Datos | Sin migración. Los documentos current usados por estas superficies ya cumplen; SHA-1 permanece `ca53a71d8a51f6fc56ae1699ceb669eb49f02653`. |
| Riesgo | Bajo. La creación explícita mantiene ids nuevos, Default/selección y Overrides vacíos intencionales; solo deja de mutarse silenciosamente un documento existente inválido. |

## Slice 1.12 — Documento transitorio de Design Test Values

| Campo | Resultado |
|---|---|
| Hallazgo | Un `testValues` con raíz incorrecta se ignoraba o reemplazaba por `{}`; collections wrong-root se sustituían por `[]`; los items no objeto se filtraban y una source collection sin id recibía el índice como identidad. La sesión externa también reemplazaba una colección transitoria dañada. |
| Owner | `DesignPreviewTestValues` conserva el envelope transitorio; `RuntimeCollectionDocumentContract` valida sources/overrides; `ComponentPreviewInputSession` conserva scope y aplicación al payload. |
| Cambio mínimo | Distinguir ausencia legítima de raíz incorrecta, validar arrays e ids antes de merge/clone/promote, crear envelope/override array solo durante un edit explícito y rechazar una colección transitoria presente con otra raíz. |
| Rutas eliminadas | `testValues as JsonObject ?? {}`, collection `as JsonArray ?? []`, `OfType` silencioso, clone fallback e id derivado de `itemIndex`. |
| Pruebas | 107/107 escritorio: envelope y collection wrong-root, ids duplicados, escritura transitoria válida y aplicación Runtime sin persistencia. |
| Enforcement | Owner y stable collection contract requeridos; fallbacks y el id por posición retirados quedan prohibidos. |
| Datos | Sin migración: Test Values permanecen de sesión y la base canónica no cambia (`ca53a71d8a51f6fc56ae1699ceb669eb49f02653`). |
| Riesgo | Bajo. No cambia el payload de una sesión válida ni la separación Design/Production; únicamente se deja de ocultar estado transitorio corrupto. |
