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

## Slice 1.13 — Campos current de Component y Overrides

| Campo | Resultado |
|---|---|
| Hallazgo | La lectura de campos Component convertía booleanos, números, objetos y arrays con forma incorrecta en `false`, el default del descriptor o texto aparente. La escritura conservaba otro serializador que aceptaba booleanos/números permisivos y fabricaba `{}`/`[]` para documentos compuestos. Los slots y Overrides embebidos existentes con raíz incorrecta podían ser reemplazados durante un edit. |
| Owner | El descriptor declara el `ValueKind`; `RuntimeInputValueKindContract` valida el nodo current y serializa el texto de editor. El dominio de Component conserva la ruta exacta, la coordinación Default/Variant y la creación explícita de una nueva frontera embebida. |
| Cambio mínimo | Reutilizar el owner en lecturas y en todos los writes de Class, Variant y Override; distinguir campo ausente de campo presente inválido; exigir objetos existentes para slot y Overrides. |
| Rutas eliminadas | `StringToBool`, `NumberNode`, blank-to-`{}`/`[]` y los fallbacks de lectura a default para un nodo presente con otra forma. |
| Migración explícita | `component.keyboard.emojiScale`, declarado Decimal, estaba guardado como texto en la config de clase y en las Variants estables `default` y `default_copy`. Se convirtió únicamente ese valor a número, sin cambiar ids, referencias ni contenido. También se actualizó el mismo valor en la base schema-v1 versionada, que conserva su envelope histórico `presets`. Los scripts temporales se eliminaron. Base current: `ca53a71d8a51f6fc56ae1699ceb669eb49f02653` → `5ce6a2a01d7e585ae30dae9bcea9af4b40ce2793`. Base schema-v1: `6b6b5b13a7fedfcd7dbe76ce2acadb4f13963211` → `a733943a65615aaaf10d8781ea9f0564cade5ada`. |
| Pruebas | 108/108 escritorio: se leen todos los campos explícitos de cada Component Class y Variant; booleano, integer, objeto y colección inválidos se rechazan sin escritura; un decimal válido hace round-trip. |
| Enforcement | Owner público de nodo requerido, lectura/escritura de Component fijada al owner y serializers/fallbacks permisivos concretos prohibidos. |
| Riesgo | Bajo después de la migración. Los campos válidos, defaults realmente ausentes, ids, Variants completas, forwarding, Overrides explícitos y Preview no cambian. |

## Slice 1.14 — Escrituras escalares de recursos

| Campo | Resultado |
|---|---|
| Hallazgo | El helper numérico común convertía cualquier texto inválido en `0`. Device, Actor, Theme y App lo usaban para escala, opacidad, geometría y tokens. Palette y Actor convertían además cualquier booleano desconocido en `false`. |
| Owner | La ruta de campo declarada elige la forma; `JsonPath.ParseRequiredNumberNode` valida números finitos y `BooleanText.ParseRequired` valida booleanos explícitos antes de la escritura preparada. |
| Cambio mínimo | Hacer estricta la única creación común de nodo numérico y usar el parser booleano requerido en las escrituras de Palette/Actor. Los controles, paths, repositorios y documentos resultantes no cambian. |
| Rutas eliminadas | `TryParse ? valor : 0` para integer/decimal y `BooleanText.Parse` en los cuatro writes booleanos persistentes. |
| Pruebas | 109/109 escritorio: Device scalar/pair, Actor scalar/boolean, Theme token, App scalar/pair y Palette boolean inválidos fallan; la copia SQLite conserva exactamente los mismos bytes. |
| Enforcement | El helper numérico debe delegar al parser finito requerido; los repositorios de Palette/Actor deben usar booleanos requeridos; las coerciones retiradas quedan prohibidas. |
| Datos | Sin migración. Los datos current ya eran válidos y la base permanece `5ce6a2a01d7e585ae30dae9bcea9af4b40ce2793`. |
| Riesgo | Bajo. Las entradas válidas producen el mismo JSON; únicamente deja de convertirse una entrada inválida en un cambio real a falso/cero. |

## Slice 1.15 — Lecturas current de recursos

| Campo | Resultado |
|---|---|
| Hallazgo | Los readers de Device, Actor, App y Theme aceptaban números almacenados como texto o devolvían cero ante ausencia; Actor/Palette convertían tipos booleanos incorrectos a falso. Theme reconstruía además alpha, modo, estilo y motion ausentes como `1`, `light`, `normal` o `{}`. `DeviceMetricRules` aceptaba números string y omitía coeficientes opcionales presentes pero inválidos. |
| Owner | `JsonPath` valida el scalar del path exacto; el field mapper declara paths required; `DeviceMetricRules` interpreta métricas Preview. Solo `dynamicIsland` es nested opcional declarado por el Device. |
| Cambio mínimo | Añadir readers exactos de string/número/boolean/pairs; usarlos en Device, Actor, App y Theme; hacer estrictos los números de Preview y los booleanos Palette presentes; conservar ausencia de Dynamic Island como `0|0` de edición sin persistir nada. |
| Rutas eliminadas | Numeric-string-to-number, wrong-boolean-to-false, `JsonNumberString` fallbacks en campos current y los defaults `1`/`light`/`normal`/`{}` de Theme. |
| Pruebas | 110/110 escritorio: se recorren todos los campos visibles de cada App, Device, Actor, Theme y Palette current; corrupciones representativas de número, booleano, Theme, App y Dynamic Island fallan sin mutar la copia SQLite. |
| Enforcement | Helpers exactos requeridos, field mappers fijados a ellos y fallbacks concretos prohibidos; `DeviceMetricRules` no puede aceptar strings numéricos. |
| Datos | Sin migración. Los documentos current cumplen; la ausencia de Dynamic Island ya era semántica válida. Base canónica sin cambios: `5ce6a2a01d7e585ae30dae9bcea9af4b40ce2793`. |
| Riesgo | Bajo. No cambia ningún recurso válido ni la geometría resuelta. Los Devices sin isla siguen mostrando cero; un documento presente dañado deja de aparentar un valor válido. |

## Slice 1.16 — Pairs y controles primitivos de diccionario

| Campo | Resultado |
|---|---|
| Hallazgo | `IntegerPair`, Theme/Palette pairs y Palette+Alpha eran solo strings para el owner común; podían faltar miembros o contener números inválidos. `PaletteAlphaPair` rellenaba colors/alpha y limitaba alpha inválido a 1. Controles de pair, boolean, Alpha, Hue e Icon Token List mantenían parsers que fabricaban vacío, falso, cero o uno ante current data inválida. |
| Owner | `RuntimeInputValueKindContract` declara la gramática y rangos; `PaletteAlphaPair` valida su envelope; los controles consumen esos owners y solo gestionan el draft interactivo. |
| Cambio mínimo | Validar dos enteros/tokens/colores, cuatro miembros Palette+Alpha, Alpha 0–1 y Hue 0–360; normalizar solo texto válido; sustituir `Split`/catch/fallbacks de asignación current por el owner compartido. |
| Rutas eliminadas | Pair incompleto a miembro vacío, alpha inválido a 1, boolean inválido a false, Hue inválido a 0 e Icon List inválida a `[]`. |
| Pruebas | 110/110 escritorio ampliadas: pairs y rangos válidos/incorrectos; writes de Component pair/Alpha inválidos rechazados byte-for-byte; lectura completa de todas las Classes/Variants current. |
| Enforcement | Casos de `ValueKind`, parser Palette+Alpha y consumidores de controles requeridos; parsers/fallbacks locales retirados quedan prohibidos. |
| Datos | Sin migración. Todos los pairs y rangos current cumplen; base canónica `5ce6a2a01d7e585ae30dae9bcea9af4b40ce2793`. |
| Riesgo | Bajo. No cambian valores válidos ni sus ids/paths; solo deja de representarse o persistirse una cadena que contradice su `ValueKind`. |

## Slice 1.17 — Etiquetas explícitas de pairs

| Campo | Resultado |
|---|---|
| Hallazgo | Los controles deducían `W/H`, `X/Y`, `Top/Bottom` o `Light/Dark` a partir del id del campo y usaban `A/B` como salida genérica. El lector de contratos Runtime añadía además `W/H` cuando las etiquetas no existían. |
| Owner | `PairFieldLabelsContract` exige el metadata completo; los catálogos y cada definición Runtime declaran las dos etiquetas de presentación. `ValueKind` sigue siendo el único owner del valor almacenado. |
| Cambio mínimo | Declarar etiquetas en todos los pairs de Record/Component, validar las definiciones Runtime current y retirar la inferencia por nombre y los defaults del parser. Las etiquetas visibles actuales se conservan exactamente. |
| Rutas eliminadas | Sufijos `.size`, `.position`, `.vertical`, `.horizontal`, `.modes`, prefijo `theme.` y fallbacks `A/B` o `W/H`. |
| Pruebas | 111/111 escritorio: todos los descriptores pair tienen metadata completo; una definición Runtime sin etiqueta falla y la copia SQLite permanece byte-for-byte intacta. |
| Enforcement | Owner requerido en controles y Runtime; inferencias por id y defaults concretos prohibidos. |
| Datos | Sin migración. Los contratos Runtime persistidos ya tenían etiquetas completas y los catálogos solo hacen explícita la presentación existente; base canónica `5ce6a2a01d7e585ae30dae9bcea9af4b40ce2793`. |
| Riesgo | Bajo. No cambia ningún valor, id, referencia, payload ni etiqueta visible; solo falla metadata incompleto que antes se reconstruía por convención. |

## Slice 1.18 — Valores current y drafts numéricos

| Campo | Resultado |
|---|---|
| Hallazgo | Los controles Integer/Decimal convertían un valor asignado inválido en cero. El slider hacía lo mismo con texto provisional y recortaba silenciosamente valores current fuera del rango declarado. |
| Owner | `RuntimeInputValueKindContract` conserva la gramática numérica; `DictionaryNumericValueContract` añade el rango explícito de `NumberDefinition` y separa current data de draft interactivo. |
| Cambio mínimo | Validar toda asignación/actualización current, ignorar drafts incompletos o fuera de rango hasta que sean válidos y restaurar el último valor válido al cerrar la edición. |
| Rutas eliminadas | `NumericText.Integer/Decimal(..., 0)`, integer decimal redondeado a entero, `NumericUpDown null → 0` y clamp silencioso del valor current del slider. |
| Inconsistencia resuelta | `device.metrics.cornerRadius` estaba declarado Integer aunque los Devices current incluyen valores legítimos fraccionales (`37.8` y `54.234`) que Preview ya consumía como números. El descriptor pasa a Decimal con tres posiciones y conserva esos valores; no se redondea ni se modifica la base. |
| Pruebas | 112/112 escritorio: contrato específico para Integer/Decimal válido, malformado, fraccional, fuera de rango y drafts; además se validan contra sus límites todos los campos numéricos visibles de Components, Variants y recursos current. |
| Enforcement | Los tres controles deben usar el owner requerido; fallbacks numéricos a cero prohibidos y draft del slider explícito. |
| Datos | Sin migración. La declaración se corrige para reflejar los datos y semántica existentes; la base permanece `5ce6a2a01d7e585ae30dae9bcea9af4b40ce2793`. |
| Riesgo | Bajo. Los valores válidos conservan su serialización; una entrada provisional inválida ya no genera una escritura real inesperada. |

## Slice 1.19 — Contrato declarativo de Preview Actions

| Campo | Resultado |
|---|---|
| Hallazgo | El reader filtraba acciones/miembros dañados, derivaba `id` desde `playInputId`, añadía `Play`, asumía segundos y aceptaba números/booleanos string. Arrays y opciones incorrectos podían convertirse en listas vacías; los clones mantenían fallbacks imposibles a `{}`. |
| Owner | `ComponentPreviewActions` valida y materializa el contrato genérico; startup invoca el mismo owner sobre cada `design_preview_json`; el payload factory solo resuelve duraciones después de esta forma explícita. |
| Cambio mínimo | Exigir arrays de objetos, ids únicos y todos los campos temporales; validar tipos opcionales y grupos target/visibility; conservar ausencia legítima de acciones y los defaults declarados del host (`prewarmFrames`) solo cuando el campo es realmente opcional. |
| Migración explícita | Las acciones `play` de Audio, Media y Bubble dependían del default oculto `seconds`. Se añadió `timeUnit: seconds` por ids estables. Dos contratos embebidos en Component Stack se localizaron por los ids estables de item/State/action y recibieron el mismo unit; esos contratos y su acción `fullScreen` recibieron además `completionBehavior: reset`, que ya era su comportamiento current. No se usaron nombres ni posiciones como identidad. |
| Rutas eliminadas | `OfType`/`Where` sobre action arrays, `id = playInputId`, label `Play`, timeUnit desconocido a segundos, numeric/boolean string, lista filtrada y clone a objeto vacío. |
| Pruebas | 113/113 escritorio: contrato válido, roots/entries/ids/labels/duración/unit/boolean/list/options incorrectos, valores action tipados y corrupción SQLite rechazada read-only; todos los flujos Component/Module/Stack siguen pasando. |
| Enforcement | Owner requerido en startup/reader; fallbacks concretos prohibidos; payload duration loop y clones mantienen objetos exactos. |
| Datos | Migración solo de metadata declarativa en la base activa: `5ce6a2a01d7e585ae30dae9bcea9af4b40ce2793` → `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. El artefacto histórico schema-v1 no es autoridad current y permanece sin cambios. |
| Riesgo | Bajo tras migrar. La reproducción, duración, resultado, ids y payload no cambian; únicamente deja de desaparecer o reconstruirse una acción incompleta. |

## Slice 1.20 — Documento de archivos de Production Font

| Campo | Resultado |
|---|---|
| Hallazgo | El array tenía raíz estricta, pero sus entradas se filtraban con `OfType`; una ruta vacía se omitía, un peso inválido se convertía en 400 y cualquier estilo distinto de `italic` se presentaba como `normal`. El resumen podía mostrar valores JSON aparentes sin validar su tipo. |
| Owner | `ProductionFontFilesContract` define cada entrada current; persistencia conserva las filas y el facade conserva importación, resumen y construcción de `ProductionFontFace`. La existencia física del asset sigue perteneciendo al boundary de recursos/Preview. |
| Cambio mínimo | Exigir objeto con nombre final, ruta relativa normalizada y segura, estilo `normal`/`italic`, peso integer 1–1000 y rutas únicas; usar el mismo owner en startup, repository, summary y Preview-face projection. |
| Rutas eliminadas | Filtro de entradas no objeto, skip de ruta vacía, `TryParse → 400`, estilo desconocido → normal y stringify aparente de scalars incorrectos. |
| Pruebas | 114/114 escritorio: documento válido y vacío declarado, entrada nula/incompleta, peso string, estilo desconocido, traversal, nombre discordante y path duplicado; tres corrupciones SQLite se rechazan byte-for-byte read-only. |
| Enforcement | Owner común y sus tres consumidores obligatorios; fallbacks/filtros concretos prohibidos en el facade. |
| Datos | Sin migración. Las cuatro familias y sus 22 archivos current ya cumplen; base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia ningún font face, asset ni salida válida; un documento dañado deja de producir una familia parcial o tipografía aparentemente válida. |
