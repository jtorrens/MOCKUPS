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

## Slice 1.21 — Valor y metadata de `BehaviorTiming`

| Campo | Resultado |
|---|---|
| Hallazgo | El value object ya exigía objeto, pero aceptaba cualquier pace token. Los resolvers C# y web convertían `fixedFrames` o `baseFramesPerUnit` ausentes/incorrectos en cero; la UI ocultaba cualquier error como duración calculada ausente y el lector de metadata convertía una definición incompleta en `null`. Startup no comprobaba que la fuente semántica fuera un sibling string exacto. |
| Owner | `BehaviorTimingValue` común conserva el valor; `RuntimeInputValueKindContract` valida metadata y fuente sibling; `BehaviorTimingResolver`/`behaviorTiming.ts` resuelven el frame exacto sin redefinir defaults. |
| Cambio mínimo | Mover el value object a Common, exigir pace token del catálogo, metadata natural completa con unit grapheme/rate positiva/source string exacta, y hacer estrictas las dos rutas de resolución y la lectura UI. Zero sigue siendo un fixed duration explícito válido. |
| Rutas eliminadas | Missing integer/rate → 0, numeric string en web, `naturalTiming` wrong-root → `{}`, metadata incompleta → null, error de cálculo → null y draft inválido → frame 0. |
| Pruebas | 114/114 escritorio ampliadas y 3 pruebas focales web: valor/root/entero/token, metadata/rate/unit/source, Theme multiplier y cuatro corrupciones SQLite read-only; resolver válido conserva 525 frames. |
| Enforcement | Owner y consumidores de startup/presentación requeridos; fallbacks concretos prohibidos en escritorio y web. |
| Datos | Sin migración. Los contratos, Themes y valores current cumplen; `fixedFrames: 0` se conserva como intención explícita. Base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo para datos válidos. Natural/Fix producen las mismas duraciones; solo deja de ocultarse un contrato temporal roto. |

## Slice 1.22 — Duraciones numéricas de Theme y State actions

| Campo | Resultado |
|---|---|
| Hallazgo | Tres hosts de Preview recorrían strings de token por su cuenta y convertían un path ausente o tipo incorrecto en cero/uno. Las State actions omitían silenciosamente colecciones, States o Motion dañados; `reflowDurationMs` se usaba sin estar declarado en el catálogo numérico común. |
| Owner | `ThemeNumericTokenCatalog` declara id/path y `ThemeNumericTokenValue` valida el número/rango. `ComponentPreviewActions` valida metadata declarativa, States exactos y Motion; el panel y el controlador solo consumen el owner compartido. |
| Cambio mínimo | Declarar Reflow, exigir tokens conocidos y valores finitos positivos para acciones/pace, no negativos para delay/duration, y exigir State/Motion exactos cuando la transición ya tiene ids de sesión. La ausencia transitoria de source/destination antes de ejecutar la acción sigue significando que ese lado aún no aporta Motion. |
| Rutas eliminadas | Theme path ausente → 0/1, token adicional desconocido, Motion ausente → 0, transición desconocida interpretada parcialmente y State id presente pero inexistente ignorado. |
| Pruebas | 114/114 escritorio: catálogo/valor/rangos, metadata action incompleta, tokens desconocidos, Theme sin Reflow, timing string, State inexistente y corrupción SQLite read-only; los flujos Lock Screen/forwarding siguen resolviendo la misma duración válida. |
| Enforcement | Los cuatro consumidores temporales deben usar el owner común; Reflow pertenece al catálogo; los walkers/fallbacks locales y tokens action no declarados quedan prohibidos. |
| Datos | Sin migración. Themes, acciones, States y Motion current ya cumplen; base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. La duración válida no cambia. Solo se diferencia la ausencia legítima de selección de sesión de un id/documento temporal current realmente inválido. |

## Slice 1.23 — Proyección de opciones Runtime dinámicas

| Campo | Resultado |
|---|---|
| Hallazgo | Una source collection ausente o con otra raíz se presentaba como cero opciones; items/values dañados se filtraban, labels vacíos se convertían en `State N` por posición y una Variant inexistente se mostraba con su referencia raw. La normalización de target actions repetía otro lector permisivo de esos valores. |
| Owner | `RuntimeInputDynamicOptions` consume la colección estable y las keys explícitas declaradas; `RuntimeInputOptionsDataSource` resuelve el nombre de una referencia completa. El host de acciones reutiliza esa misma lista tipada. |
| Cambio mínimo | Exigir array current, items estables, value/label strings no vacíos, valores únicos y lookup exacto de Variant; sustituir el segundo reader del action host por el owner común. El badge del primer item se conserva porque está declarado explícitamente por metadata. |
| Rutas eliminadas | Source inválida → `[]`, `OfType`/`Where` que omitía entradas, reference rota → texto raw, label vacío → `State {posición}` y extracción duplicada de valores de acción. |
| Pruebas | 114/114 escritorio: fuente válida con Variant/nombre/badge; source ausente, root/entry incorrectos, value/label ausentes, valor duplicado y Variant rota; lectura completa sigue byte-for-byte read-only. |
| Enforcement | Owner de colección y strings requerido; filtros/fallbacks/label posicional prohibidos; normalización action debe llamar al resolver común. |
| Datos | Sin migración. Las opciones dinámicas current ya declaran `alternatives`, `id` y `name`; base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. Las opciones y selección válidas conservan ids, orden y labels; solo deja de ocultarse un contrato o referencia rota. |

## Slice 1.24 — Dependencia calculada de `BehaviorTiming`

| Campo | Resultado |
|---|---|
| Hallazgo | El value, metadata y resolver ya eran estrictos, pero el control todavía hacía `resolver?.Invoke(...) ?? 0`; un servicio de diccionario incompleto aparentaba una duración natural calculada de cero. El servicio devolvía además `null` si faltaba metadata. |
| Owner | `EditorDictionaryFieldServices` compone el resolver desde el `FieldDefinition`; `DictionaryBehaviorTimingControl` lo requiere para presentar el mismo cálculo del owner timeline. |
| Cambio mínimo | Hacer no-null el resultado del delegate, fallar al crear el control sin resolver, fallar si el FieldDefinition carece de metadata o el resultado es negativo y mostrar el entero exacto válido. |
| Rutas eliminadas | Resolver ausente → frame 0, definición sin `BehaviorTiming` → `null` y clamp de un resultado negativo a cero. |
| Pruebas | 114/114 escritorio: el control rechaza construcción sin resolver; los escenarios Natural/Fix, Password y timeline conservan sus duraciones válidas. |
| Enforcement | Delegate/resolver requerido y patrones nullable/`?? 0` prohibidos en control y servicio. |
| Datos | Sin migración. No cambia ningún documento ni cálculo current; base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. Todos los hosts reales ya suministraban el servicio; solo deja de ocultarse una composición de editor incompleta. |

## Slice 1.25 — Duración de actions derivada de Motion

| Campo | Resultado |
|---|---|
| Hallazgo | `durationMotionConfigPath` tenía un resolver aislado que devolvía “sin cambio” si faltaban path, Motion, transition o Theme timing. El contrato action seguía considerándolo una fuente finita, pero los hosts posteriores acababan usando cero o un segundo. State actions mantenían en paralelo otra implementación ya estricta de la misma duración Motion. |
| Owner | `MotionTimingDuration` valida el `MotionVariantValue` y resuelve delay+duration mediante `ThemeNumericTokenValue`. Payload preparation exige duración positiva para una action; State transitions permiten Motion explícito `none` y combinan sus lados/Reflow como antes. |
| Cambio mínimo | Compartir la resolución, exigir objeto en el path exacto y duración positiva, materializar `durationSeconds` y eliminar el walker Theme permisivo de la factory. |
| Rutas eliminadas | Motion/path ausente → no-op, Theme timing ausente/string → 0, duración no positiva → no-op y algoritmo duplicado en State actions. |
| Pruebas | 114/114 escritorio: Slide, Fade y `none`; timing ausente/string, Motion no finito y path action roto read-only. Keyboard, Media, Lock Screen y forwarding conservan sus flujos válidos. |
| Enforcement | Ambos consumidores deben usar el owner compartido; el walker `JsonPath.NumberDouble(..., 0)` y los retornos silenciosos de la factory quedan prohibidos. |
| Datos | Sin migración. Los Motion paths y Themes current ya son completos; base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. Las duraciones válidas son las mismas; solo deja de ejecutarse una action con un tiempo plausible inventado cuando su contrato está roto. |

## Slice 1.26 — Valores Runtime de duración de actions

| Campo | Resultado |
|---|---|
| Hallazgo | Después de validar la declaración, el panel y el preparador de frames aún convertían un `durationInputId` ausente/string/no positivo en cero, un frame o un segundo. Las duraciones de colección filtraban items y aceptaban ausencia como colección vacía. El panel ignoraba además `durationOwnerTimeline`, por lo que `playConversation` quedaba reducido a un segundo privado. Los owner timelines C#/web aceptaban strings o uno implícito para acciones finitas activas. |
| Owner | `ComponentPreviewActionRuntimeValue` posee los valores temporales de una action en Diseño; `RuntimeAnimationFrameOrigin` y `RuntimeOwnerTimeline` aplican el mismo contrato al endpoint de Producción. `JsonPath` aporta la lectura numérica JSON exacta reutilizable. |
| Cambio mínimo | Exigir duración directa positiva y finita, tiempo no negativo y trigger boolean; exigir array/items/números de colección; resolver definiciones `BehaviorTiming` desde el owner exacto; delegar `durationOwnerTimeline` al timeline común. En Producción solo una action condicional realmente activada exige su duración, para conservar items legítimos a los que esa acción no aplica. |
| Rutas eliminadas | `durationInputId → 0/1`, numeric/boolean JSON string, colección ausente → base/1, `OfType` sobre items temporales, panel `durationOwnerTimeline → 1 segundo`, `Number(...) || 0` y `optionalNumber(..., 1)` en las dos rutas de Producción. |
| Pruebas | 114/114 escritorio y 88/88 Preview: duración/time/state válidos y tipos/rangos inválidos; colección completa y contributor string; action embebida; endpoint Production numérico/string/cero; conversación y todos los escenarios previos conservan su timing válido. |
| Enforcement | Los dos hosts de Diseño deben consumir el owner común; los timelines C#/web deben exigir duración positiva solo al activarse; quedan prohibidos los parsers/fallbacks locales retirados. |
| Datos | Sin migración. Los valores y colecciones current cumplen; base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. Los valores válidos conservan su duración; se corrige `playConversation` para respetar su owner declarado y solo fallan contratos temporales realmente mal formados. |

## Slice 1.27 — Lectura completa de definiciones Runtime

| Campo | Resultado |
|---|---|
| Hallazgo | El reader común filtraba entradas de `inputs`, `collections`, `fields`, options y listas; una definición incompleta o una raíz incorrecta podía desaparecer de la UI. Cualquier `source` desconocido se trataba como Runtime y cualquier `uiOrigin` desconocido como Self. Objetos de presentación, composición, animación o transition con otra raíz se convertían en ausencia. |
| Owner | `RuntimeInputValueKindContract` sigue poseyendo `kind`/`ValueKind`/default/BehaviorTiming; `ComponentPreviewInputSession` conserva y materializa el documento completo para presentación. `JsonPath` se usa para scalars JSON exactos. |
| Cambio mínimo | Distinguir miembro ausente de miembro presente incorrecto; exigir arrays/objetos/identidades completos; validar toda definición incluso si después queda oculta o su source no es editable; hacer estrictas options, string lists, visibility, itemPresentation, componentItems, nested collection, animation y transition. Se conservan los defaults estructurales ya declarados: source ausente = Runtime y uiOrigin ausente = Self. |
| Rutas eliminadas | `OfType`/`Where` que filtraban definitions/options/lists, incomplete field → `continue`, wrong root → `[]`/`null`, unknown source → Runtime, unknown origin → Self y objetos nested incorrectos → ausencia. |
| Pruebas | 115/115 escritorio: input/collection válido y ausencia opcional; roots/entries/required fields incorrectos; source/origin desconocidos; option root/entry/duplicado; list, visibility, animation/timeline/transition, fields, componentItems e itemPresentation inválidos. Todos los escenarios current siguen pasando. |
| Enforcement | Los readers deben exigir arrays/objects y conservar los defaults solo por ausencia; quedan prohibidos los filtros y ramas catch-all retiradas. |
| Datos | Sin migración. Las 257 definiciones y cinco colecciones current cumplen; las ausencias existentes de `source`/`uiOrigin` son formas estructurales declaradas. Base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. La presentación válida no cambia; un contrato incompleto deja de aparentar que el campo o colección simplemente no existe. |

## Slice 1.28 — Valores Runtime actuales y estado de sesión de actions

| Campo | Resultado |
|---|---|
| Hallazgo | La definición Runtime ya era estricta, pero el panel y los Test Values convertían cualquier valor presente con tipo o raíz incorrectos en el `defaultValue`. El estado Play y el tiempo de una action aceptaban además strings y los reducían a valores de sesión plausibles. |
| Owner | `RuntimeInputValueKindContract` valida el nodo actual y produce su representación de almacenamiento; `DesignPreviewTestValues` y `ComponentPreviewInputSession` distinguen ausencia de valor inválido. `ComponentPreviewActionRuntimeValue` posee la lectura Play/tiempo. |
| Cambio mínimo | Compartir una serialización current posterior a la validación `ValueKind`; usar el default solo si la key no existe; exigir boolean/number JSON cuando Play/tiempo están presentes y conservar `false`/`0` únicamente como inicio explícito cuando faltan. |
| Rutas eliminadas | Wrong scalar/object/array → `defaultValue`, Test Value inválido → valor persistido/default, action boolean/number string → texto aceptado y action wrong-root → `false`/`0`. |
| Pruebas | 115/115 escritorio y 88/88 Preview: ausencia válida, scalar Runtime/Test Value/collection incorrecto, decimal JSON frente a string y action Play/tiempo ausente/presente incorrecto. Todos los escenarios current conservan sus valores. |
| Enforcement | Los tres consumidores de valores de presentación deben usar `CurrentStorageText`; Play/tiempo de sesión deben delegar en el owner action; el catch-all a `input.DefaultValue` queda prohibido. |
| Datos | Sin migración. La base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia un valor válido ni el inicio de una action sin estado materializado; solo deja de ocultarse un valor presente corrupto. |

## Slice 1.29 — Una sola lectura de colecciones para Test Values y actions

| Campo | Resultado |
|---|---|
| Hallazgo | El lector principal de colecciones ya exigía definiciones completas, pero la aplicación de Test Values volvía a recorrer metadata raw y convertía un `sourceCollectionJsonKey` presente con tipo incorrecto en ausencia. La resolución de records y el lookup de actions mantenían además recorridos directos que omitían una colección ausente o filtraban items no objeto. |
| Owner | `ComponentPreviewInputSession.ReadRuntimeCollections` materializa todas las definiciones, también las ocultas cuando el consumidor lo pide; `DesignPreviewTestValues.CollectionItems` conserva la proyección lógica de Test Values y `CurrentCollectionItems` conserva la identidad mutable del documento efectivo ya preparado. |
| Cambio mínimo | Aplicar sources desde definiciones tipadas completas; obtener las keys sourced de esas mismas definiciones; resolver records y targets de actions desde los items current estrictos sin clonarlos; leer el record actual mediante su `ValueKind` en lugar de otro acceso raw. |
| Rutas eliminadas | Segundo reader de `collections`, metadata source incorrecta → ausente, colección wrong-root → omitida, `OfType<JsonObject>` sobre items y record ausente → string vacío aunque tuviera default declarado. |
| Pruebas | 115/115 escritorio y 88/88 Preview, incluyendo source metadata con tipo incorrecto y todos los escenarios current de forwarding, Component Stack, Collection Stack y Lock Screen. |
| Enforcement | Test Values debe invocar el lector completo con `includeHidden`; los consumidores que preparan Preview deben usar `DesignPreviewTestValues.CurrentCollectionItems`; el walker raw retirado queda prohibido. |
| Datos | Sin migración. La base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. Las colecciones válidas conservan orden, ids y overrides; solo deja de interpretarse como colección vacía una definición o raíz inválida. |

## Slice 1.30 — Documento completo de items con Component embebido

| Campo | Resultado |
|---|---|
| Hallazgo | `componentItems` declaraba las keys, pero cada consumidor volvía a interpretar el item. Una referencia corta, Inputs/Overrides ausentes o wrong-root podían omitirse en Preview y Usage, aparentar que no había Overrides o crearse al abrir el editor. Dos editores aún filtraban items o fabricaban un id por posición. |
| Owner | `RuntimeComponentCollectionItemDocumentContract` común posee metadata, keys distintas, referencia y raíces Inputs/Overrides. El reader tipado exige un único field `ComponentVariant`; startup, Test Values, editor, Usage y actions consumen el mismo owner. |
| Cambio mínimo | Validar cada item estable antes de presentarlo o resolverlo; aceptar solo referencia completa o el string vacío explícito de un State visualmente vacío; exigir siempre objetos Inputs y Overrides; mantener identidad mutable en la preparación de Preview; retirar filtros e ids posicionales. |
| Rutas eliminadas | Referencia corta → lookup, ref/input/override incorrecto → skip/null, Overrides ausente → `{}` al abrir, `OfType<JsonObject>` y `item-{index}`. |
| Pruebas | 115/115 escritorio y 88/88 Preview: metadata ausente/null/incompleta, keys solapadas, field inexistente/wrong kind, item válido, sentinel vacío, referencia corta, Overrides ausente, Inputs wrong-root e identidad mutable. Component Stack conserva su State Replace vacío y todos los escenarios current pasan. |
| Enforcement | El owner común debe estar presente en startup, Test Values, actions, editor estructurado y Usage; los filtros/ids posicionales y la creación de Overrides al abrir quedan prohibidos. |
| Datos | Sin migración. Los items current ya conservan sus tres miembros; la base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. Las referencias completas y el sentinel vacío explícito no cambian; únicamente fallan documentos embebidos incompletos o ambiguos. |

## Slice 1.31 — Reconciliación Runtime sin filtros ni reparación posicional

| Campo | Resultado |
|---|---|
| Hallazgo | Las escrituras ordinarias ya eran estrictas, pero los coordinadores de cambio de Module Variant recorrían definitions con `OfType`, omitían ids/keys incompletos, convertían un valor presente null en default y asignaban ids a items por posición. Los lookups de escritura repetían esos filtros. |
| Owner | `RuntimeDefinitionObjects` conserva arrays y entradas completas; `RuntimeInputValueKindContract`, `RuntimeCollectionDocumentContract` y la key explícita conservan valor, items e identidad. La reconciliación sigue siendo un workflow explícito del facade, no startup ni repository. |
| Cambio mínimo | Distinguir ausencia de null; validar valores existentes antes de clonarlos; exigir source/key/id exactos; recorrer definitions/items sin filtros; casar projected items solo por id estable; conservar creación de default/array vacío exclusivamente para keys nuevas ausentes. |
| Rutas eliminadas | Definition/item no objeto → omitido, source desconocido → non-runtime, key vacía → skip, current null → default, item sin id → `{storageKey}_{posición}` y lookup parcial → “no declarado” plausible. |
| Pruebas | 115/115 escritorio y 88/88 Preview; workflows de Module Variant y todas las mutaciones Runtime siguen pasando; corrupción con id ausente y scalar null falla byte-for-byte read-only. |
| Enforcement | El coordinador y el cambio de Variant deben usar el reader completo; `OfType<JsonObject>`, id posicional y null-to-default quedan prohibidos en estas rutas. |
| Datos | Sin migración. La base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. La creación explícita de datos nuevos no cambia; solo deja de repararse silenciosamente contenido existente inválido. |

## Slice 1.32 — Runtime contract embebido por item proyectado

| Campo | Resultado |
|---|---|
| Hallazgo | Una colección podía declarar `itemRuntimeContractJsonKey`, pero actions, Runtime API y animación comprobaban localmente `is JsonObject`; si el miembro faltaba o tenía otra raíz, cada superficie omitía los inputs/actions/targets del item sin señalar el documento roto. `collections: null` también se confundía con ausencia en el reader de actions. |
| Owner | La definición tipada conserva la key explícita; `DesignPreviewTestValues` valida el objeto de cada item efectivo. Actions, `RuntimeInputsCollectionEditor` y `ModuleInstanceAnimationEditor` requieren ese mismo objeto exacto. |
| Cambio mínimo | Exigir object root cuando la metadata declara la key; distinguir miembro ausente de presente null; conservar colección ausente como “no declarada” y rechazar present wrong-root; usar el id estable en el contexto del error. |
| Rutas eliminadas | Nested runtime contract ausente/wrong-root → skip, `collections: null` → sin actions, collection current wrong-root → sin embedded actions y target id ausente → string vacío. |
| Pruebas | 115/115 escritorio y 88/88 Preview: colección/action válida, contract nested ausente/wrong-root, `collections: null`, proyección lógica válida/dañada y todos los escenarios current de Lock Screen/forwarding/animación. |
| Enforcement | Los cuatro consumidores deben exigir `JsonPath.RequiredObject`; los guards `is JsonObject` que ocultaban el item quedan prohibidos. |
| Datos | Sin migración. Los projected items current ya contienen sus objetos completos; base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia un input/action/target válido; solo se hace visible una contradicción que antes variaba entre superficies. |

## Slice 1.33 — Envelopes Runtime del owner temporal de escritorio

| Campo | Resultado |
|---|---|
| Hallazgo | El cálculo temporal C# aún recorría `collections`, `inputs`, `fields`, `itemActions`, actions raíz e items con casts y `OfType`; una raíz o entrada dañada se convertía en timeline vacío. También omitía un Runtime contract proyectado ausente y filtraba ids de pre/post duration inválidos. |
| Owner | `JsonPath` aporta las lecturas estructurales reutilizables; `RuntimeAnimationFrameOrigin` conserva exclusivamente las fórmulas, la secuencia y la proyección owner-relative. |
| Cambio mínimo | Distinguir ausencia opcional de presencia inválida; exigir arrays de objetos, items con id estable, metadata timeline objeto y listas de field ids no vacíos; exigir el objeto Runtime proyectado cuando está declarado. |
| Rutas eliminadas | Wrong root/entrada no objeto → `[]`, item sin id → omitido, nested Runtime contract roto → sin fields/actions, timeline wrong-root → objeto vacío y pre/post id no string → filtrado. |
| Pruebas | 116/116 escritorio: ausencia válida y casos collections/inputs/actions/items/fields/nested Runtime/timeline list mal formados; todas las duraciones, retime, re-entry y escenarios current conservan sus resultados. |
| Enforcement | El timeline debe consumir los helpers estructurales comunes y quedan prohibidos sus lectores `OfType` concretos de contracts, fields, actions e items. |
| Datos | Sin migración. La base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia ninguna fórmula ni un documento válido; solo deja de calcularse una duración plausible desde un envelope incompleto. |

## Slice 1.34 — Paridad web de los envelopes del owner temporal

| Campo | Resultado |
|---|---|
| Hallazgo | `RuntimeOwnerTimeline` mantenía el equivalente web de los filtros retirados en escritorio: `records` y `strings` convertían roots incorrectas, entries no objeto e ids no string en colecciones vacías; `asRecord` ocultaba metadata timeline y Runtime contracts embebidos/proyectados dañados. |
| Owner | El timeline web consume el envelope ya preparado y comparte la semántica temporal del owner común; no valida campos concretos de Components ni pinta el resultado. |
| Cambio mínimo | Añadir lectores locales exactos de miembros opcionales; exigir arrays de objetos, item id estable, objetos de timeline/embedded/projected Runtime y listas pre/post de strings no vacíos. Mantener ausencia opcional como ausencia. |
| Rutas eliminadas | `records(contract/runtime/fields/actions) → []`, `map(asRecord) → {}`, id vacío → item omitido, `strings.filter` y nested contract wrong-root → `{}`. |
| Pruebas | 89/89 Preview y 116/116 escritorio: los mismos envelopes válidos/dañados en ambas plataformas y todos los escenarios de timing/forwarding/Stacks/Conversation sin regresión. |
| Enforcement | El timeline web debe usar sus readers exactos y quedan prohibidas las invocaciones `records`/`asRecord` retiradas sobre contract, runtime collection, fields, itemActions y nested Runtime documents. |
| Datos | Sin migración. La base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. El frame válido no cambia; Preview deja de discrepar silenciosamente del editor ante un envelope inválido. |

## Slice 1.35 — Envelope de cálculo de tracks y Retime en escritorio

| Campo | Resultado |
|---|---|
| Hallazgo | Aunque `animation_json` persistido ya tenía un owner v2 estricto, el timeline común admite también animación transitoria y volvía a filtrar tracks/keyframes no objeto. Tracks/keyframes wrong-root aparentaban ausencia y un Retime null/string/cero se trataba como Retime off. La búsqueda de owner-origin filtraba además items dañados. |
| Owner | `ModuleInstanceAnimationDocumentContract` conserva el documento persistido completo; `RuntimeAnimationFrameOrigin` posee el envelope mínimo que consume al calcular, incluida la forma transitoria vacía legítima. |
| Cambio mínimo | Validar una vez el envelope al construir el modelo: arrays de objetos, field/target ids, frame entero no negativo, enabled boolean si está presente, retime/targets objeto y duraciones positivas. Compartir una lectura exacta de keyframes y source items. |
| Rutas eliminadas | Tracks/keyframes/items `OfType`, root incorrecta → sin track, field/target inválido → no match, frame inválido → cero y Retime inválido/no positivo → off. |
| Pruebas | 116/116 escritorio: transient vacío válido y 14 formas inválidas de tracks, keyframes y Retime; todas las pruebas owner-relative, duration, actions y retime actuales pasan. |
| Enforcement | El constructor debe ejecutar el guard; tracks/keyframes/source items usan arrays exactos y quedan prohibidos los tres filtros `OfType` retirados. |
| Datos | Sin migración. La base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. El v2 current ya cumple; se conserva el objeto transitorio vacío y solo deja de confundirse animación presente dañada con ausencia. |

## Slice 1.36 — Paridad web del envelope de tracks y Retime

| Campo | Resultado |
|---|---|
| Hallazgo | El timeline web seguía convirtiendo `tracks`/`keyframes` wrong-root o entries dañadas en ausencia mediante `records`; `asRecord` y `optionalNumber` convertían Retime inválido/no positivo en off. |
| Owner | `RuntimeOwnerTimeline` consume la misma animación transitoria resuelta por frame; el renderer continúa recibiendo solo el estado final. |
| Cambio mínimo | Ejecutar un guard equivalente al de escritorio antes del cálculo; usar arrays/objetos exactos para lookup y keyframes; validar frame/enabled y Retime raíz/targets. Conservar `{}` y el `targetId: ""` explícito que representa al owner Screen. |
| Rutas eliminadas | `records(animation.tracks/keyframes) → []`, `asRecord(retime/targets/target) → {}` y `optionalNumber(invalid, 0) → Retime off`. |
| Pruebas | 89/89 Preview y 116/116 escritorio: vacío y sentinel Screen válidos, 16 formas inválidas web más la matriz de escritorio; todos los frames y resolvers actuales pasan. |
| Enforcement | Constructor web con guard obligatorio; tracks/keyframes/retime exactos; patrones `records`/`asRecord` retirados prohibidos. |
| Datos | Sin migración. La base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia animación válida ni ownership; desaparece únicamente el falso “sin animación/Retime off” ante datos presentes incorrectos. |

## Slice 1.37 — Metadata temporal y valores de duración en escritorio

| Campo | Resultado |
|---|---|
| Hallazgo | Las raíces de metadata ya eran objetos, pero el cálculo convertía kind desconocido, origin/completion incompleto, minimum/offset inválido, field de duración inexistente y valor Runtime ausente/string en ownerStart o frame cero. `firstMatchingValue` incompleto se detectaba solo si esa rama llegaba a ejecutarse. |
| Owner | El contrato 29 define el vocabulario cerrado; `RuntimeAnimationFrameOrigin` posee su validación semántica de cálculo y `BehaviorTimingResolver` conserva el tipo temporal compuesto. |
| Cambio mínimo | Validar metadata de toda collection/input/field aunque la colección esté vacía; exigir serial/boolean/listas, owner origin completo, origins y completion soportados; resolver referencias por id y valores numéricos exactos. Mantener los defaults estructurales declarados: sin origin = owner zero, sin minimum = 2 y sin `extendsOwnerDuration` = true. |
| Rutas eliminadas | Unknown kind → ownerStart, `offset/minimum Number(...fallback)`, base field ausente → 0, pre/post field/value ausente o string → 0 y metadata de collection vacía → no validada. |
| Pruebas | 116/116 escritorio: 14 formas de metadata inválida, base field inexistente y duration value ausente/string; fixtures contractuales actualizados con `offsetFrames: 0`; todo timing current conserva resultados. |
| Enforcement | `ValidateCollectionTimeline`/`ValidateFieldTimeline` obligatorios; origins/completion exactos y `FieldValue` numérico estricto; casts/fallbacks retirados prohibidos. |
| Datos | Sin migración. Los documentos canónicos ya declaran metadata y valores completos; base SHA-1 `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia una fórmula ni un valor current; solo impide calcular con metadata contradictoria o Runtime incompleto. |

## Slice 1.38 — Paridad web de metadata temporal y duraciones

| Campo | Resultado |
|---|---|
| Hallazgo | La ruta web aún convertía origin/completion/ownerOrigin incorrectos en `{}`, clampaba offset/minimum, aceptaba vocabulario desconocido y devolvía cero si un field o su valor de duración no existía. La búsqueda embebida filtraba además definitions dañadas. |
| Owner | `RuntimeOwnerTimeline` aplica el contrato 29 después del payload boundary; los valores embedded se localizan por su lista de inputs explícita y JSON key, no por el nombre del componente. |
| Cambio mínimo | Replicar validators de collection/field; exigir referencias y números no negativos; materializar defaults explícitamente en fixtures antes de cruzar la frontera; retirar por completo `records`/`asRecord` del timeline; conservar únicamente el sentinel field-level `animationTimeline: null` que emite forwarding. |
| Rutas eliminadas | Wrong metadata → `{}`, unknown kind → ownerStart, offset/minimum inválido → clamp/default, missing field/value → 0 y embedded inputs malformed → omitidos. |
| Pruebas | 89/89 Preview y 116/116 escritorio: misma matriz de 14 metadatas, referencias y valores inválidos; fixtures de Conversation completan offsets y valores como lo hace payload preparation; Lock Screen confirma el sentinel null de sus fields forwarded. |
| Enforcement | Validators web requeridos para contracts/direct/projected fields; duration lookup exacto; `asRecord`, `records` y clamps de metadata quedan prohibidos en el timeline. |
| Datos | Sin migración. La base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. Payload current completo conserva cada frame y el null explícito de forwarding; solo dejan de aceptarse fixtures o payloads parciales que nunca debían cruzar la frontera. |

## Slice 1.39 — Política de duración de Screen estricta

| Campo | Resultado |
|---|---|
| Hallazgo | `RuntimeDurationContract.Policy` hacía cast opcional de `animationTimeline`; null/array se convertían en `calculated`. `defaultDurationFrames` usaba `GetValue<int>() ?? 0`, mezclando miembro ausente con tipo incorrecto. |
| Owner | `RuntimeDurationContract` posee `calculated`/`explicit` y el default inicial de una Screen explícita; el timeline común conserva después el cálculo o límite correspondiente. |
| Cambio mínimo | Aplicar default calculated solo por ausencia; exigir timeline objeto, policy string conocida y default explícito entero positivo. Mantener el sentinel null únicamente para fields projected, no para metadata raíz. |
| Rutas eliminadas | Root null/array → calculated y default ausente/wrong scalar → cero genérico. |
| Pruebas | 116/116 escritorio: calculated ausente, explicit 240, policy desconocida, timeline null/array, policy number y default ausente/string/fraccional/cero. 89/89 Preview se conserva. |
| Enforcement | `JsonPath.OptionalObject`/`RequiredInteger` obligatorios; casts opcionales y `GetValue<int>() ?? 0` prohibidos en el owner. |
| Datos | Sin migración. Lock Screen ya declara objeto, `explicit` y 240; base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. Calculated y explicit válidos no cambian; solo deja de reinterpretarse metadata raíz dañada. |

## Slice 1.40 — Identidad temporal explícita en escritorio

| Campo | Resultado |
|---|---|
| Hallazgo | El owner temporal elegía la primera key no vacía mediante conversión tolerante: una `storageCollectionJsonKey` presente pero inválida podía caer en source/json. Collections con la misma key, items con el mismo `targetId` y fields con el mismo id se sobrescribían o se resolvían por primer orden. |
| Owner | El Runtime contract declara las keys y scopes; `RuntimeAnimationFrameOrigin` conserva el índice temporal. Un track solo contiene `fieldId`/`targetId`, por lo que la identidad de target debe ser única en todo el owner. |
| Cambio mínimo | Validar la primera key explícitamente presente; exigir keys efectivas únicas; insertar targets sin overwrite; exigir ids de input y fields únicos, también después de combinar fields directos y proyectados. |
| Rutas eliminadas | Key inválida → siguiente key, key duplicada → doble lectura, target duplicado → último item y field duplicado → primer field por posición. |
| Pruebas | 116/116 escritorio: key ausente/wrong-root/vacía, precedencia inválida, key efectiva duplicada, target duplicado entre collections e ids duplicados de input/field; todos los escenarios temporales current siguen pasando. |
| Enforcement | Key leída con `RequiredString`; `HashSet` para keys, `TryAdd` para targets y validator de ids de field; overwrite directo de `_items[targetId]` prohibido. |
| Datos | Sin migración. La base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia ninguna identidad válida; únicamente deja de depender del orden una dirección temporal contradictoria. |

## Slice 1.41 — Paridad web de identidad temporal

| Campo | Resultado |
|---|---|
| Hallazgo | `RuntimeOwnerTimeline` mantenía los equivalentes web de la selección tolerante y del overwrite: cadena `storage/source/json` por primer string truthy, `Map.set` para targets repetidos y `.find` para fields repetidos. |
| Owner | El payload preparado conserva las identidades declaradas; el timeline web las consume sin reinterpretarlas antes de resolver el frame. Resolver y renderer no participan en este control. |
| Cambio mínimo | Aplicar la misma precedencia presente/estricta; rechazar keys efectivas, targets e ids de field repetidos; validar la combinación de fields directos, embedded y projected. |
| Rutas eliminadas | Wrong key → fallback, target duplicado → último item y field duplicado → primero por orden de concatenación. |
| Pruebas | 89/89 Preview y 116/116 escritorio: misma matriz de keys/ids ambiguos y todos los resolvers, forwarding, Stacks, Conversation y ownership temporal actuales. |
| Enforcement | Set de collection keys, guard antes de `items.set`, `requiredString` en la precedencia y validator de fields; cadena truthy anterior prohibida. |
| Datos | Sin migración. La base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. Preview conserva todas las direcciones válidas y deja de elegir un owner diferente por orden del documento. |

## Slice 1.42 — Referencias de actions temporales en escritorio

| Campo | Resultado |
|---|---|
| Hallazgo | El timeline filtraba `extendsModuleDuration` por igualdad, omitía una action cuyo play field no existía, trataba enable ausente/string como false e ignoraba keyframes de play con value no boolean. Una action dañada aparentaba estar inactiva y dejaba de extender la Screen. |
| Owner | La action declara si participa en duración y sus referencias; el timeline común resuelve el field owner-relative y solo exige la duración condicional cuando el trigger boolean está activo. |
| Cambio mínimo | Validar flags presentes; exigir id y referencias completas para una action extending; respetar `playFieldId` explícito al cruzar forwarding; exigir play field real, enable boolean y values booleanos en el track. Mantener que una action válidamente inactiva no exige duration. |
| Rutas eliminadas | Flag wrong-root → false, field ausente → skip, enable inválido → false y keyframe string → no activo. |
| Pruebas | 116/116 escritorio: flags, id, base duration, referencias, field, enable y keyframe inválidos; action inactiva sin duration válida; media finita, Conversation y ownership temporal current conservados. |
| Enforcement | `ValidateTemporalActions` obligatorio; enable con `RequiredBoolean`; missing play field falla y los dos patrones de skip/coerción quedan prohibidos. |
| Datos | Sin migración. Las actions current extending de Chat ya declaran ids y referencias completas; base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia una action válida ni cuándo requiere duration; solo impide que una declaración activa desaparezca por datos contradictorios. |

## Slice 1.43 — Paridad web de actions temporales

| Campo | Resultado |
|---|---|
| Hallazgo | El timeline web repetía la omisión de flags/referencias inválidas: filtraba solo `extends === true`, aceptaba fallback truthy de `playFieldId`, saltaba fields ausentes y convertía enable/value distinto de `true` en action inactiva. |
| Owner | `RuntimeOwnerTimeline` consume el mismo action owner ya preparado y decide su contribución al frame; no traslada esta semántica al resolver del componente ni al renderer. |
| Cambio mínimo | Replicar el validator y las referencias de escritorio; requerir trigger/value boolean; actualizar fixtures parciales para materializar `isPlaying: false`, igual que el payload completo. |
| Rutas eliminadas | Flag inválido → filtered, `playFieldId` inválido → fallback, field ausente → skip y trigger/keyframe inválido → false. |
| Pruebas | 89/89 Preview y 116/116 escritorio: misma matriz temporal, fixtures Conversation completos, media finita y todos los frames/resolvers current sin regresión. |
| Enforcement | `validateTemporalActions`, `requiredBooleanValue` y missing-field failure obligatorios; filter, skip y comparación tolerante anteriores prohibidos. |
| Datos | Sin migración. Los payloads current ya materializan el booleano Runtime; base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. La action válida conserva su duración; web deja de calcular una Screen distinta del editor ante el mismo contrato dañado. |

## Slice 1.44 — Identidad de tracks transitorios en escritorio

| Campo | Resultado |
|---|---|
| Hallazgo | El documento persistido v2 ya prohíbe targets/frames ambiguos, pero el envelope transitorio del calculator aceptaba dos tracks para el mismo `fieldId`/`targetId`, frames repetidos y frames desordenados. `FirstOrDefault` y `OrderBy` elegían un resultado plausible. |
| Owner | La pareja estable field/target identifica un track; el frame owner-relative identifica un keyframe dentro de él. El timeline calcula, no normaliza esos dos índices. |
| Cambio mínimo | Registrar addresses al validar; exigir frame único y orden estrictamente ascendente antes de resolver. Conservar el target vacío como sentinel Screen, equivalente a target ausente. |
| Rutas eliminadas | Track duplicado → primero; frame duplicado → orden incidental y lista desordenada → sorted silencioso. |
| Pruebas | 116/116 escritorio: duplicado target explícito, target ausente/vacío equivalente, frame repetido y orden descendente; todos los tracks v2 current y escenarios owner-relative pasan. |
| Enforcement | `HashSet<(field,target)>`, `frames.Add` y comparación con frame previo obligatorios en el guard transitorio. |
| Datos | Sin migración. Los documentos persistidos y payloads current ya cumplen; base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No se reordena ni modifica animación válida; solo deja de elegirse por posición una dirección contradictoria. |

## Slice 1.45 — Paridad web de identidad de tracks

| Campo | Resultado |
|---|---|
| Hallazgo | El guard web validaba roots y scalars pero permitía los mismos targets y frames ambiguos; `find` y el sort de keyframes decidían cuál usar. |
| Owner | El timeline web consume el address estable y el frame owner-relative exactos antes de resolver; no normaliza la animación que recibe. |
| Cambio mínimo | Mantener un set de parejas serializadas sin colisión; exigir frames únicos y orden ascendente en cada track. Tratar target ausente y sentinel vacío como la misma dirección Screen. |
| Rutas eliminadas | Track duplicado → primero, frame duplicado → orden incidental y lista desordenada → sorted silencioso. |
| Pruebas | 89/89 Preview y 116/116 escritorio: misma matriz de address/frame y todos los resolvers, actions y tracks current. |
| Enforcement | Set de targets, `frames.has` y comparación con frame previo obligatorios en el guard web. |
| Datos | Sin migración. Los payloads current ya cumplen; base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. Preview mantiene el resultado válido y deja de divergir del editor ante el mismo track contradictorio. |

## Slice 1.46 — Ausencia estructural en actions declarativas

| Campo | Resultado |
|---|---|
| Hallazgo | El owner de Design Preview actions era estricto con tipos no null, pero sus readers opcionales usaban `owner[key] is null`: miembro ausente y `null` explícito activaban el mismo default. `actions: null` e `itemActions: null` también se aceptaban como listas ausentes. Varias strings temporales ni siquiera se comprobaban durante validación startup. |
| Owner | `ComponentPreviewActions` valida el documento declarativo completo; los hosts solo presentan/ejecutan sus definiciones ya validadas. |
| Cambio mínimo | Aplicar defaults solo con `ContainsKey == false`; hacer que null presente llegue al reader exacto y falle; incluir play/enable/prewarm strings en la validación completa. |
| Rutas eliminadas | Null → string vacío, número/boolean default, lista vacía, options ausentes o action array ausente. |
| Pruebas | 116/116 escritorio: roots actions/itemActions null y ocho familias opcionales null; contracts válidos, embedded actions, durations, Motion y startup current conservados. |
| Enforcement | Readers y arrays deben distinguir `ContainsKey`; seis patrones concretos de null-as-absence quedan prohibidos. |
| Datos | Sin migración. Las actions current omiten opcionales y no persisten null; base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia un default declarado por ausencia; solo se rechaza una segunda representación no autorizada. |

## Slice 1.47 — Reader web común de arrays de objetos

| Campo | Resultado |
|---|---|
| Hallazgo | El timeline web ya distinguía ausencia de raíz/entrada inválida, pero conservaba un reader privado. Esa copia hacía que otros payload consumers pudieran repetir o alterar accidentalmente la misma frontera. |
| Owner | `previewJsonHelpers` posee las conversiones estructurales comunes después del payload boundary; cada resolver conserva la semántica de sus documentos. |
| Cambio mínimo | Extraer `optionalObjectArray` sin cambiar su contrato ni sus llamadas y hacer que `RuntimeOwnerTimeline` lo importe. |
| Rutas eliminadas | Copia local del reader exacto y futura divergencia entre arrays opcionales de objetos. |
| Pruebas | 89/89 Preview, typecheck y arquitectura; todos los envelopes temporales válidos e inválidos conservan el mismo resultado. |
| Enforcement | El helper común exportado y su import en el timeline son obligatorios; queda prohibida una nueva definición local. |
| Datos | Sin migración ni cambio de payload. La base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Muy bajo. Es una extracción pura que prepara la adopción gradual del mismo límite por otros consumers. |

## Slice 1.48 — Identidad explícita de fields embebidos

| Campo | Resultado |
|---|---|
| Hallazgo | El resolver común de Collection/Component Stack filtraba definitions inválidas, convertía un mapa de forwarding mal formado en `{}` y trataba cualquier key local no declarada como `fieldId`. También usaba la key JSON como último fallback de identidad. |
| Owner | El contrato Runtime Input publica id y JSON key; forwarding publica únicamente la sustitución estable de id al cruzar una frontera. El resolver embebido consume esas direcciones sin inventar otras. |
| Cambio mínimo | Leer definitions como array exacto, exigir ids/keys únicos y animar solo valores declarados o promovidos por el mapa explícito. El mapa puede ser la autoridad exclusiva al cruzar forwarding, pero su key debe existir como valor local. Las keys auxiliares sin ninguno de esos owners siguen llegando intactas al child resolver. |
| Rutas eliminadas | Wrong definitions/map → vacío, definition incompleta → omitida, id ausente → jsonKey y payload key no declarada → track implícito. |
| Pruebas | 90/90 Preview y 116/116 escritorio: id distinto de jsonKey, forwarding explícito con y sin definition local, key no declarada no animada y nueve envelopes/identidades inválidos. Typecheck, arquitectura y build pasan. |
| Enforcement | Reader común, ids/keys únicos y mapa declarado quedan fijados; los tres fallback/filter anteriores están prohibidos. |
| Datos | Sin migración. Los inputs embebidos canónicos ya contienen definitions completas; la base permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia un track current explícito; impide que metadata auxiliar o dañada adquiera significado temporal por coincidencia de nombre. |

## Slice 1.49 — Envelope e identidad de actions embebidas

| Campo | Resultado |
|---|---|
| Hallazgo | El resolver embebido convertía `actions` mal formado en vacío, filtraba entradas no objeto, omitía actions sin play/time key y usaba una cadena de fallback que terminaba en la key JSON. Un time unit desconocido se ejecutaba como segundos y un play animado no boolean se apagaba. |
| Owner | El documento declarativo posee la forma/vocabulario de la action; definition y forwarding poseen su identidad temporal estable; el host embebido solo resuelve la action preparada. |
| Cambio mínimo | Exigir array/entries completos, ids únicos, time unit y completion cerrados; hacer compatibles pero no contradictorios `playFieldId` y mapa explícito; exigir boolean cuando existe track. Mantener que un play value estructuralmente ausente hace la action no disponible en ese child. |
| Rutas eliminadas | Wrong root/entry → action ausente, miembro obligatorio ausente → skip, jsonKey → fieldId, time unit desconocido → seconds y play string → false. |
| Pruebas | 91/91 Preview y 116/116 escritorio: 16 envelopes, vocabularios e identidades inválidos; action forwarded válida y sus frames finales se conservan. Typecheck, unused, arquitectura y build pasan sin avisos. |
| Enforcement | Reader exacto, required strings, vocabularios, conflicto de ids y booleano quedan fijados; los fallback/filter anteriores están prohibidos. |
| Datos | Sin migración; las actions canónicas ya son documentos completos. La base permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia una action válida. Este slice no decide la semántica pendiente de `durationInputId` id frente a JSON key ni añade fuentes de duración. |

## Slice 1.50 — Items, Slots, States y Overrides exactos en Preview

| Campo | Resultado |
|---|---|
| Hallazgo | Collection Stack y Component Stack comprobaban la raíz de sus listas, pero convertían cada entrada no objeto con `asRecord`. Los Overrides de un Component embebido también convertían null/array/scalar en `{}`, perdiendo silenciosamente el documento local. |
| Owner | El array estructurado posee sus entradas objeto y el embedded item owner posee Inputs/Overrides; el resolver consume esos documentos, no los repara. |
| Cambio mínimo | Añadir el reader común required array-of-objects; aplicarlo a items, slots y States; exigir Overrides objeto antes de mezclarlo con la Variant. |
| Rutas eliminadas | Entrada inválida → `{}` y Overrides wrong-root → ausencia de Override. |
| Pruebas | 93/93 Preview y 116/116 escritorio: raíces null/objeto, entradas null, States inválidos y Overrides array. Typecheck, unused, arquitectura y build pasan sin avisos. |
| Enforcement | Helper requerido y sus tres consumers quedan fijados; los maps con `asRecord` y el Override tolerante quedan prohibidos. |
| Datos | Sin migración. Los items/States canónicos ya contienen objetos explícitos. La base permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Muy bajo. Un documento válido produce exactamente el mismo child payload; solo desaparece la reparación silenciosa. |

## Slice 1.51 — Owner web único de animación transitoria

| Campo | Resultado |
|---|---|
| Hallazgo | `RuntimeOwnerTimeline` poseía un guard estricto, pero `resolveParameterAnimation` mantenía otro reader que convertía tracks/keyframes inválidos en vacíos, clampaba frames y ordenaba el documento. Interpolation ausente/null/desconocida acababa en Hold. |
| Owner | El nuevo `transientAnimationDocument` posee una sola validación estructural web; timeline e interpolador solo consumen su secuencia validada. |
| Cambio mínimo | Extraer el guard existente, compartir también el object reader, añadir valor explícito y vocabulario de interpolation cerrado; retirar maps/clamp/sort del interpolador y el sort redundante del timeline. |
| Rutas eliminadas | Wrong root/entry → vacío, frame inválido → cero, lista desordenada → sorted y interpolation inválida → Hold. |
| Pruebas | 94/94 Preview y 116/116 escritorio: acceso directo al interpolador con trece envelopes inválidos, además de toda la matriz temporal previa. Typecheck, unused, arquitectura y build pasan sin avisos. |
| Enforcement | Owner compartido obligatorio para timeline/interpolator; guard/identidad/orden/vocabulario fijados y readers/sorts tolerantes prohibidos. |
| Datos | Sin migración. La animación canónica usa values explícitos e interpolation `hold`/`writeOn`; base `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. Los documentos válidos conservan cada frame; el WeakSet evita revalidar el mismo objeto inmutable durante sus múltiples resoluciones. |

## Slice 1.52 — Ausencia estructural en la instancia de Preview

| Campo | Resultado |
|---|---|
| Hallazgo | `instanceJson` ya exigía raíz objeto, pero cuatro resolvers convertían `context` o `animation` presentes con raíz `null`, array o scalar en `{}`. Component Stack conservaba además una segunda lectura tolerante durante una transición. |
| Owner | El boundary de documentos Preview conserva la raíz requerida; `previewJsonHelpers.optionalObject` declara la única semántica opcional de sus miembros estructurales. Cada resolver conserva únicamente su cálculo de frame y estado. |
| Cambio mínimo | Usar el reader exacto en Component Collection, Component Stack, Conversation y Notifications; ausencia real mantiene el objeto opcional vacío y cualquier presencia con raíz incorrecta falla. No se cambia ninguna fórmula temporal ni se elige entre relojes potencialmente solapados. |
| Rutas eliminadas | `asRecord(instance.context)`, `asRecord(instance.animation)` y la lectura secundaria `asRecord(parseObject(...).context)` que convertían documento inválido en “sin contexto/sin animación”. |
| Pruebas | 95/95 Preview focales: instancia sin miembros válida y ocho raíces presentes inválidas para `context`/`animation`; toda la matriz de Stacks, collections, actions, timing y Conversation conserva sus resultados. |
| Enforcement | Los cuatro consumers deben usar `optionalObject`; las tres formas tolerantes retiradas quedan prohibidas. |
| Datos | Sin migración. Los payloads current ya contienen objetos válidos o ausencia estructural; base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Muy bajo. Solo deja de confundirse un envelope roto con ausencia legítima. La autoridad pendiente de `localFrame` queda explícitamente fuera de este slice. |

## Slice 1.53 — Proyección exacta de eventos de salida en Component Stack

| Campo | Resultado |
|---|---|
| Hallazgo | La interpolación ya usaba el owner único de animación, pero el recorrido que mantiene States salientes repetía su propio parser: roots incorrectas parecían listas vacías, entries no objeto parecían `{}` y frames inválidos se convertían en cero. |
| Owner | `transientAnimationDocument` valida el documento completo; Component Stack solo proyecta los frames de tracks `active` para decidir qué salida sigue visible. |
| Cambio mínimo | Validar el mismo objeto, leer tracks/keyframes con los readers exactos y exigir el frame numérico ya validado. Se conserva el orden descendente de eventos porque es semántica de evaluación de salidas, no normalización del documento. |
| Rutas eliminadas | `Array.isArray(...)?...:[]`, `map(asRecord)` y `Number(frame) || 0` dentro de la proyección de eventos. |
| Pruebas | 96/96 Preview: roots/entries, frame, enabled, value e interpolation dañados; transiciones Replace/Overlay, actions y salidas válidas conservan sus frames. |
| Enforcement | Owner compartido y readers exactos obligatorios en Component Stack; los tres patrones tolerantes retirados quedan prohibidos. |
| Datos | Sin migración. La base canónica permanece `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Muy bajo. No cambia una animación válida ni el orden de salida; solo se elimina un segundo significado para un documento incorrecto. |

## Slice 1.54 — Documentos de composición de Notifications

| Campo | Resultado |
|---|---|
| Hallazgo | Notifications convertía su config raíz, tres slots, varios Inputs/Overrides y cada item no objeto en `{}`. La raíz de `items` era array requerido, pero sus entries seguían tolerantes. |
| Owner | El resolver Notifications posee su composición explícita; cada slot/Inputs/Overrides es un objeto requerido y el Runtime collection owner posee el array de items estables. |
| Cambio mínimo | Exigir todos los objetos antes de resolver referencias o mezclar Overrides y usar el reader común de array requerido de objetos para `items`. No cambia ninguna referencia Variant ni el payload de los children válidos. |
| Rutas eliminadas | Nueve `asRecord` de fronteras requeridas y el `preview.items.map(asRecord)` que convertía una entry dañada en una Notification incompleta. |
| Pruebas | 98/98 Preview: fixture válida, nueve roots de config dañadas y cuatro formas inválidas de la colección Runtime; 116/116 escritorio conserva la composición seeded completa. |
| Enforcement | `requiredRecord`/`requiredObjectArray` obligatorios en cada frontera y los casts tolerantes concretos quedan prohibidos. |
| Datos | Sin migración. La configuración y los items current ya son objetos completos; base `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia una Notification válida; un documento roto deja de parecer un slot vacío o una colección parcialmente usable. |

## Slice 1.55 — Sobre transitorio de Runtime Transition

| Campo | Resultado |
|---|---|
| Hallazgo | Notifications convertía `__runtimeTransitions` y su miembro `distributionMode` dañados en `{}`; `sourceFrame` aceptaba strings y valores no positivos como ausencia. Un objeto presente sin `previousValue` podía mezclar metadata forwarded incompleta con un valor recalculado. |
| Owner | `runtimeTransitionDocument` define el sobre transitorio genérico producido por la composición embebida; Notifications conserva únicamente el vocabulario `flow`/`stacked` y el cálculo visual. |
| Cambio mínimo | Distinguir raíz/miembro ausente de presencia inválida, exigir frame entero positivo y `previousValue` explícito. Se mantiene la precedencia actual del track local sobre el frame forwarded. |
| Rutas eliminadas | Doble `asRecord`, `Number(sourceFrame)`, no positivo → ausencia y previous ausente → recálculo silencioso. |
| Pruebas | 99/99 Preview: transición completa válida y diez formas de root, miembro, frame o valor previo inválidas; 116/116 escritorio y toda la composición current conservada. |
| Enforcement | Owner común obligatorio, uso en Notifications fijado y parsers tolerantes concretos prohibidos. |
| Datos | Sin migración. El metadata se genera en memoria con ambos miembros completos; base canónica `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia la transición válida ni su precedencia; solo se deja de completar un sobre interno contradictorio. |

## Slice 1.56 — Lookup estricto de Component Variant preparada

| Campo | Resultado |
|---|---|
| Hallazgo | `componentVariantConfig` ya exigía la referencia completa, pero convertía un catálogo `variants` con raíz incorrecta en `{}` y una config referenciada null/array/scalar en base vacía. |
| Owner | El payload preparado posee el catálogo por referencia estable; cada referencia exacta identifica una config objeto completa antes de Overrides locales. |
| Cambio mínimo | Exigir raíz objeto para el catálogo y para el miembro referenciado, conservando el error explícito de referencia ausente, la gramática y el merge recursivo actual. |
| Rutas eliminadas | `asRecord(componentBaseConfigs.variants)` y `asRecord(config)` como reparación de bases inválidas. |
| Pruebas | 99/99 Preview ampliadas con catálogo ausente/null/array y config null/array/scalar; 116/116 escritorio y todas las referencias current conservadas. |
| Enforcement | Los dos `requiredRecord` y la comprobación exacta de presencia quedan fijados; los casts tolerantes de lookup quedan prohibidos. |
| Datos | Sin migración. Todos los catálogos preparados current contienen configs objeto completas; base `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Muy bajo. No cambia ninguna referencia ni Variant válida; solo deja de renderizarse una base vacía inventada. |

## Slice 1.57 — Owner común de slot embebido y adopción en Audio

| Campo | Resultado |
|---|---|
| Hallazgo | Cada resolver repetía referencia+merge y podía convertir Overrides null/array/scalar en `{}`. Audio hacía lo mismo en cuatro slots y convertía también su config/slots requeridos en vacíos. |
| Owner | `embeddedComponentConfig` conserva la referencia completa y los Overrides locales; el parent resolver exige sus slots y decide visibilidad/composición. |
| Cambio mínimo | Extraer el helper común y migrar Surface, Avatar, Badge y duration Label de Audio; exigir config y cuatro slots objeto aun cuando Avatar/Badge estén ocultos. |
| Rutas eliminadas | Cuatro secuencias locales `componentVariantConfig + asRecord(overrides)` y cinco casts de config/slot en Audio. |
| Pruebas | 100/100 Preview: merge válido, referencia incompleta y Overrides ausente/null/array; 116/116 escritorio conserva Audio y su composición current. |
| Enforcement | Helper compartido, cuatro adopciones Audio y roots requeridas fijados; las secuencias tolerantes retiradas quedan prohibidas. |
| Datos | Sin migración. Los cuatro slots Audio current ya declaran referencia completa y Overrides objeto; base `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia el merge ni la visibilidad válida; un slot roto deja de convertirse en child con configuración inventada. |

## Slice 1.58 — Adopción de slots exactos en Avatar y Label

| Campo | Resultado |
|---|---|
| Hallazgo | Avatar convertía su config, style, Label/Badge slots y Overrides en objetos vacíos; Label repetía el patrón con su Surface. Las raíces principales solían fallar más tarde por un scalar requerido, pero un Override dañado seguía desapareciendo. |
| Owner | Cada parent exige sus objetos declarados y `embeddedComponentConfig` conserva referencia completa + Override local antes de componer el child. |
| Cambio mínimo | Migrar Avatar → Label/Badge y Label → Surface al helper compartido; exigir config/style/slots aun cuando Label o Badge estén ocultos. |
| Rutas eliminadas | Siete casts tolerantes de config/slot y tres merges locales de referencias/Overrides. |
| Pruebas | 100/100 Preview y 116/116 escritorio; tests de owner común, Actor/Avatar, Label layout y composición current pasan sin cambios. |
| Enforcement | Roots requeridas y tres adopciones del helper fijadas; casts y merges tolerantes concretos prohibidos. |
| Datos | Sin migración. Todos los documentos current ya son completos; base `1191ea88e5b27b81014e3041e232a8c0c8cbdb40`. |
| Riesgo | Bajo. No cambia visibilidad, estilo ni contenido; solo se rechaza composición incompleta que antes podía perder Overrides. |
