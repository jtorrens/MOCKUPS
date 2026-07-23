# Owner Validation and Preview Document Boundary Contract

Status: normative.

This document governs cleanup phase 1. It extends contracts 24, 33, 34, 35,
51, 56, 57 and 72 without changing valid current data, persisted payloads,
Preview primitives or editor interaction.

## 1. Objective

Every current invariant is checked by the layer that owns its meaning. A
consumer may report the owner's failure, but it must not repair, reinterpret or
reconstruct the rule.

The target route is:

```text
validated current persistence
→ complete prepared payload
→ strict payload-document boundary
→ id-only registry
→ owner resolver/renderable
→ generic renderer
```

Phase 1 moves only verified dispersed validation. It does not require a common
`IValidatable`, a general validation graph, reflection, a new diagnostic
framework or migration of every existing value object.

## 2. Validation ownership

- repositories validate the current row and root shape they store or write;
- current document contracts validate their own envelope and domain
  invariants;
- `VariantEnvelopeContract` owns complete current Variant entries;
- Module runtime-document owners validate their declared Runtime values;
- the common animation/timeline contracts validate owner-relative animation;
- payload preparation validates and resolves the complete desktop boundary;
- `renderablePayloadBoundary` validates serialized object roots before routing
  in the web runtime;
- component and Module resolvers validate their own semantic fields;
- asset resolvers validate final fonts, icons and media they materialize;
- the renderer receives only routed, validated and fully resolved primitives.

The shell may show a validation result or prevent an action whose owner has
rejected it. `MainWindow`, controllers, registries and the generic renderer do
not become domain validators.

## 3. Required Preview payload documents

Every routed `DesignPreviewPayload` requires current JSON object roots for:

- `configJson`;
- `designPreviewJson`;
- `runtimeContractJson`;
- `componentBaseConfigsJson`;
- `appConfigJson`;
- `instanceJson`;
- `themeTokensJson`.

Blank, malformed, absent or wrong-root values fail before registry dispatch.
They are never converted into `{}` by `parseObject`, a resolver, a registry or
the renderer.

`iconMappingJson` remains optional in the web payload type for existing
isolated fixtures. Absence is an explicit empty optional mapping; when present,
it must be a JSON object. Optionality must be declared at the call site or type
boundary and must not be inferred from a failed parse.

The boundary may parse again when a resolver consumes a document, but every
such parse remains strict. A downstream parser is a local type conversion, not
a second policy that can accept data rejected by the boundary.

## 4. Effective Theme mode

`DesignPreviewPayloadFactory` owns the effective Preview Theme mode:

- an explicit Module Variant `appearanceMode: light` resolves to `light`;
- an explicit Module Variant `appearanceMode: dark` resolves to `dark`;
- `inherit` resolves from the selected or inherited session/Shot mode;
- isolated Component payloads without a Module appearance choice use the
  session mode.

Every prepared `DesignPreviewPayload.ThemeMode` is an explicit `light` or
`dark`, and that value is authoritative. `WebDesignPreviewRenderer` transports
it unchanged; it must not accept a second session mode, supply a fallback, let
session `dark` override explicit payload `light`, or inspect Module config
itself.

## 5. Current JSON and value objects

Persisted C# object and array roots continue through
`JsonPath.ParseRequiredObject` and `ParseRequiredArray`. Repository or facade
wrappers may add an owner/path to the error, but must not add a second accepted
shape.

Current Module config is additionally routed by exact `record_class_id` to its
owner contract. The committed Module definition config and every complete
Module Variant config must satisfy the same owner before repository reads or
writes succeed. Module field presentation and commits consume that validated
document; they must not turn an invalid nested object, array, boolean, number
or option into an empty value or an apparent default.

Value objects may define explicit sentinels. In particular, blank typography
and `inherited` mean that no local typography object is authored. Any other
Typography Style value must be a valid JSON object; malformed or wrong-root
input is not another spelling of inherited state.

Runtime Input `defaultValue` is interpreted only by the canonical
`RuntimeInputValueKindContract`. `BehaviorTiming` is a required object with an
explicit supported mode, non-negative fixed frames and pace token. Invalid
defaults never become a plausible false, zero, empty collection or fixed-zero
timing value.

Presentation readers preserve those complete definitions. Optional
`inputs`/`collections` members may be absent; when present they are arrays of
complete objects and collection fields are complete definitions. Static option
arrays, string lists, nested presentation/component contracts, animation and
transition metadata keep their exact roots and entries. Unknown present
`source`/`uiOrigin` values, incomplete visibility pairs, filtered options or a
hidden malformed definition are invalid. Only structural absence retains the
declared `runtime` and `self` meanings.

Persisted Runtime collections use `RuntimeCollectionDocumentContract` for the
stable item envelope. The effective Runtime contract owns the exact storage
key; current content owns an existing array of object items with unique
non-empty ids. Variant reconciliation may create a newly declared empty array,
but neither reconciliation nor ordinary editor writes may replace a present
wrong root or silently redirect an insert with a missing anchor.

Current top-level Runtime values and structured collection fields are resolved
from the effective contract by exact JSON key. `RuntimeInputValueKindContract`
owns both editor serialization and persisted shape validation. Parent-owned or
undeclared keys do not become Runtime data, and invalid booleans, numbers,
arrays or semantic objects do not become plausible values.

Runtime presentation and session-only Test Values distinguish an absent field
from a present invalid field. Only absence may expose the definition's explicit
`defaultValue`; a present value is validated by its exact `ValueKind` before it
is converted to dictionary storage text. Design Preview action play and time
state follow the same rule: structural absence initializes the session-owned
state to `false` and zero, while a present value must be a JSON boolean or a
finite non-negative JSON number respectively.

Declared dynamic Runtime options also preserve that collection owner. Their
source collection, value key and label key are exact metadata; items remain
objects with stable ids and option values are unique non-empty strings. The UI
does not turn a missing collection into no options, filter malformed entries,
derive a label from position or show a broken Variant reference as its own
display label. Action-target normalization consumes the same option projection
instead of maintaining a second permissive reader.

Explicit forwarding keeps one strict envelope through persistence validation,
desktop payload preparation and the web payload boundary. The optional
`$forwardedInputs` member and every contained definition are objects; Preview
input/collection/action lists and projection metadata keep their declared
array roots; nested projected Runtime contracts are required objects. Invalid
entries are not filtered and wrong roots are not replaced with empty documents.

Dictionary compound controls consume these owners rather than reparsing JSON.
Component Input Bindings use the strict object/forwarding contract;
Structured Collection and Icon Slots use the stable Runtime collection
document contract. A control may create explicit empty Overrides or Inputs only
while creating a new boundary; opening or editing an existing wrong-root
document must fail without replacing it.

Session-only Design Test Values follow the same document ownership. Their
optional envelope is an object; collection values and sourced overrides keep
array roots and stable unique ids. The session may create a missing envelope or
override array only as the direct result of an explicit Test Value edit. It
must not repair a present wrong root, filter malformed items or derive an id
from collection position.

Collection source application consumes the same complete
`RuntimeInputCollectionDefinition` reader used by presentation, including
definitions hidden by current UI conditions. It does not maintain a second raw
metadata reader. Record-reference resolution and action option lookup consume
the effective current collection-item owner without cloning it, so wrong roots
or filtered entries cannot disappear and record-resolution mutations stay in
the prepared document that reaches final Preview.

Collections declaring `componentItems` use one shared embedded-item document
owner. The metadata names three distinct keys and its Variant key identifies
exactly one declared `ComponentVariant` field. Every existing stable item keeps
an explicit Variant string plus object roots for local Overrides and Inputs. A
non-empty Variant string uses the full stable reference grammar. The empty
string remains an explicit sentinel only for a State that intentionally has no
visual Component; it does not authorize a short id, Default inference or an
omitted Inputs/Overrides document.

Module Instance Runtime reconciliation uses the same strict definition, value
and stable-collection owners as ordinary persistence validation. It may
materialize a declared default or a newly declared empty collection only for an
absent key during an explicit Variant/creation workflow. Present nulls, wrong
roots, filtered definitions and missing item ids fail; reconciliation does not
repair them or derive identity from collection position.

Projected collections declaring `itemRuntimeContractJsonKey` also keep one
exact nested object per stable item. Test Values validate it before returning
items; embedded action discovery, the Runtime Inputs editor and animation
target presentation require the same object. Missing/wrong-root nested
contracts do not remove an item's Runtime API from one surface while leaving it
active in another.

The common desktop owner timeline consumes those same complete envelopes.
Optional `collections`, `inputs`, `actions`, collection `fields` and
`itemActions` may be absent; when present they are arrays of objects. A present
Runtime collection is an array of object items with non-empty stable ids.
Animation timeline metadata and its pre/post field-id lists retain their exact
object, array and non-empty string shapes. The calculator does not filter a
malformed entry, convert a wrong root to an empty owner or omit a declared
projected Runtime contract.

The web transient-animation envelope has one shared document owner used by
`RuntimeOwnerTimeline` and `resolveParameterAnimation`. It validates track and
keyframe object arrays, unique addresses/frames, authored order, exact frame and
enabled scalars, explicit values, closed interpolation vocabulary and positive
retime durations. Timeline and interpolation then consume the same validated
sequence without a second filter, clamp, default or sort.

The required `instanceJson` root contains optional `context` and `animation`
members. Structural absence means that the prepared isolated Preview has no
additional context or animation and may be represented to consumers by an
empty optional object. A present member must be an object; `null`, arrays and
scalars are invalid and must fail before frame-state resolution. Component
collection, Component Stack, Conversation and Notifications resolvers consume
this same distinction and must not use a tolerant object cast for either
member. This structural rule does not choose between two clock fields or alter
the owner-relative timing formulas; clock authority remains a separate
contract decision.

Component Stack transition-exit discovery is a projection of that same
validated transient animation document. It may order distinct event frames
from newest to oldest to calculate currently exiting States, but it must not
filter malformed tracks/keyframes, coerce their frames or maintain a second
animation grammar. Its event traversal therefore consumes the shared owner and
the exact object-array readers before applying Component Stack semantics.

Notifications composition owns a required current `notifications` config
object, three embedded slot objects, their local Overrides, the declared
notification/badge Inputs and a required Runtime `items` array of objects. Its
resolver must require those roots before it constructs Collection Stack,
Notification or Badge child payloads. A malformed child boundary is not an
empty slot, no local Override or an empty Notifications collection.

Embedded Runtime transition metadata is a transient prepared-document
boundary. Its root may be structurally absent or an object. Each present field
transition is an object with a positive integer `sourceFrame` and an explicit
`previousValue`; a present empty/incomplete/wrong-root transition is not
equivalent to no transition. Components consume this shared document owner and
then apply their own closed value vocabulary. The owner does not choose between
an explicit local animation track and forwarded metadata or change transition
precedence.

Prepared Component base configs expose one required `variants` object keyed by
complete stable Component Variant reference. A referenced member must exist and
must itself be a complete config object before local Overrides are merged.
Missing/wrong-root catalogs or Variant configs do not become an empty base
config. Lookup preserves the exact full-reference grammar and never selects a
class default or short id.

Every embedded Component slot crosses that lookup through one shared prepared
slot owner: a complete stable `variantReference` and a required local
`overrides` object. The helper merges only those two validated documents. The
parent Component remains responsible for requiring its slot object and for
declaring whether the embedded child is visually enabled. Audio applies this
owner to its Surface, Avatar, Badge and duration Label slots; hidden children
do not make their slot document optional.

Avatar and Label use the same rule through their explicit composition chain.
Avatar requires its own config, shared style, Label slot and Badge slot; Label
requires its config and Surface slot. Child visibility affects only whether a
validated child is included in the renderable result. It never turns the
parent's current slot/reference/Overrides document into optional data.

Notification likewise requires its own config, Avatar Inputs and its Avatar,
Surface, summary Label and detail Label slots before selecting the active
display mode. Both Label slots remain current composition even when only one is
visible. Its Theme motion root and the Badge config root are required objects;
wrong-root configuration is never treated as an empty visual style. The shared
embedded owner replaces local copies of reference/Override merging.

Button requires its config, Badge slot and state catalog. The selected state is
an exact object with Surface and Label slots, and all three children cross the
shared embedded owner. Content mode may omit the resolved Label from the final
renderable, and `showBadge` may omit Badge, but neither flag makes the authored
slot/reference/Overrides document optional. State selection remains explicit
and uses the existing closed state vocabulary.

Icon Row requires its config and a Runtime `items` array of objects. Every item
keeps its stable id, full Button Variant reference and explicit
`buttonOverrides` object; invalid entries are not filtered or converted to
empty Buttons. Icon Bar requires its config and, for each explicit zone in the
selected state, the named Icon Row slot plus its named Inputs object. The slot
uses the shared embedded owner. Zone/state keys remain the declared closed
Icon Bar contract and are never derived from item position.

Keyboard requires its config and explicit Icon Bar slot, routed through the
same embedded owner. Its keyboard rows remain module-owned standard layout
data; the slot is not inferred from that layout. Keypad requires its config,
exact `keys` object array, state catalog and Label slot before resolving keys.
Each key keeps its explicit stable id/kind/value and duplicate checks. The
selected visual state is an exact state object and the Label slot keeps its
full Variant reference and local Overrides even when a key is a spacer.

Password requires its config, exact Runtime Input definition array and its
complete composition set: three state Label slots plus Indicator, Icon Bar,
Fingerprint, Face Recognition, Draw Password and Keypad slots. All slots are
validated before the active password mode/state is selected, then each selected
child crosses the shared embedded owner. An inactive method is still complete
current Variant configuration; it is not optional data and no slot is derived
from its display label, list position or payload type.

Code Indicator, Fingerprint, Face Recognition and Draw Password each require
their config root, state catalog and selected state object after validating the
closed state vocabulary. Code Indicator additionally requires its explicitly
named empty/filled Surface slots and routes both through the shared embedded
owner. A missing state/slot cannot become an empty style whose later fields
happen to fail or a Surface with discarded local Overrides.

Surface requires its config, Tail and shared style objects even when the Tail
is visually disabled. Cursor requires its config object. These atomic resolvers
consume exact authored configuration; a wrong root is not a blank Tail, default
style or Cursor awaiting scalar fallbacks.

Status Bar and Navigation Bar require their layout objects and exact `items`
arrays of objects. Navigation Bar additionally requires its gesture object.
Every item keeps its stable id, explicit kind, zone and order before zone
projection/sorting; non-object entries do not become partial items and zone
order never supplies identity. Existing hidden `off` items remain complete
authored items rather than being filtered before validation.

Collection Stack Reflow requires the current Theme `motion` object in both
explicit distribution transitions and automatic item reflow. A missing or
wrong-root motion document is not an empty Theme awaiting duration/easing
fallbacks. Collection Stack continues to own only Reflow selection; Theme
timing vocabulary remains in the shared motion helpers/contracts.

Lock Screen requires its Module config `lockScreen` object before reading its
Status Bar, Navigation Bar and Component Stack slots/Inputs. Those three slots
retain their already-required full references and local Overrides, and the
renderable uses the same shared embedded owner as Components when preparing
child payloads. Visibility flags omit only final system-bar renderables; they do
not make the slot documents optional or add a default Variant fallback.

Temporal lookup identity is never selected by a permissive fallback. The first
declared collection key in the explicit storage/source/json precedence must be
valid, collection storage keys are unique, stable target ids are unique across
the owner's collections, and field ids are unique within their exact owner
scope. A later member, item or projected field cannot silently overwrite an
earlier temporal owner.

The web `RuntimeOwnerTimeline` mirrors this envelope ownership after payload
preparation. Its optional object/array readers distinguish absence from invalid
presence for direct, embedded and projected Runtime data; a malformed Preview
document cannot therefore produce a different plausible duration from the
desktop editor.

The strict optional array-of-objects reader is a shared Preview JSON boundary,
not a timeline-local convention. Every web payload consumer that adopts this
shape must preserve structural absence as an empty optional collection while
rejecting a present wrong root or any non-object entry. Consumers must not
reintroduce local filtering readers with subtly different acceptance rules.

Required arrays of objects use the same shared reader after first requiring the
member's structural presence. Collection Stack items, Component Stack slots and
nested States use that boundary. Their visual Component Inputs and Overrides
remain required objects; neither an invalid collection entry nor a wrong-root
Overrides member is converted to `{}` before Variant resolution.

Embedded Component collection inputs use that exact boundary for their Runtime
Input definitions. Every definition has a unique non-empty id and JSON key.
Only a declared value or a value explicitly named by forwarding is eligible for
parameter-animation resolution; extra payload keys pass through without
becoming fields. A present `__runtimeFieldIds` map is an object of non-empty
stable ids whose keys name existing local values. It may promote a value without
a duplicated child definition. It is explicit forwarding metadata, not
permission to infer identity from a JSON key.

Embedded action arrays use the same exact object-array boundary. Every action
entry keeps a unique non-empty id plus its required label, play key, time key,
closed time unit and completion behavior. A present forwarded/action field id
must be a non-empty string and two explicit authorities may not disagree. When
the play value is present it must resolve through one of those explicit ids or
its complete Runtime Input definition; its JSON key is never the fallback id.
An animated play value is a boolean. Structural absence may leave an action
unavailable, but null, wrong roots, incomplete entries and unknown vocabularies
do not become an empty or inactive action.

An animation owner with no authored data may still supply an empty transient
object. If tracks, keyframes or retime are present, the desktop timeline
validates their calculation envelope before using it: no wrong array/object
root, filtered entry, empty track field id, invalid target-id scalar, invalid
frame/enabled scalar or non-positive retime duration may be interpreted as “no
animation” or “Retime off”. A target id is a stable string or the explicit
empty Screen-owner sentinel. Track addresses are unique and their frame lists
are strictly ordered without duplicates. Persisted Module Instance animation
retains the stricter complete v2 document owner. The web timeline enforces the
same transient calculation envelope.

The temporal metadata itself is also current owner data. Collection sequencing,
pre/post duration ids and `firstMatchingValue` origin use their closed declared
vocabulary. Field origin is `ownerStart` or a complete `fieldCompletion` with a
non-negative integer offset; completion names a real sibling duration field,
uses only the supported track override and never clamps an invalid minimum.
Referenced pre/post/completion Runtime values must exist as non-negative JSON
numbers or their declared `BehaviorTiming` owner. Missing fields, strings in
place of numbers and negative values do not become frame zero.

Finite action participation is equally explicit. `definesModuleDuration` and
`extendsModuleDuration` are booleans when present. An extending item action has
complete play/duration/enable references, resolves a real play field and
consumes boolean owner and keyframe states. The calculator does not skip a
missing field or reinterpret an invalid trigger as an inactive action.

Design Preview action documents likewise preserve structural absence only.
Present null action arrays or optional action members are invalid; defaults for
prewarm, duration, targeting and auxiliary lists are not null-coercion paths.

Web frame resolution mirrors that same metadata and duration-value contract.
Prepared fixtures and payloads contain every declared Runtime duration value;
an explicit embedded Runtime object is resolved only through its complete
input-definition list and exact JSON key. The timeline does not use `asRecord`,
array filtering, a name-based lookup or zero to conceal a missing value.

The forwarding projection explicitly serializes `animationTimeline: null` for
a field without authored animation metadata. This is a field-level absence
sentinel shared by the desktop and web timelines; a collection timeline null or
any other present wrong root remains invalid.

`RuntimeDurationContract` likewise distinguishes structural absence from an
invalid root Screen timeline. Only absence defaults to calculated duration;
present policy and explicit default frames use their exact string and positive
integer shapes. Root Screen metadata never consumes the forwarded-field null
sentinel.

Component Class and Component Variant dictionary fields also use the exact
`ValueKind` owner for both editor serialization and current-node validation. A
field that is absent may still expose its explicitly declared descriptor
default; a field that is present with a wrong scalar, object or array shape is
invalid and must not be presented as that default. The same rule applies to
local Overrides. An embedded slot or `overrides` member may be created only
while explicitly authoring a new boundary; an existing member with a wrong root
must fail without replacement.

Record-resource scalar writes follow the same failure rule at their declared
field boundary. Boolean text must be explicitly true or false and numeric text
must be a finite number before a prepared Palette, Device, Actor, Theme or App
document/row is written. Invalid input never means false or zero, and rejection
must leave the stored row unchanged.

Their current reads preserve the same exact JSON scalar shapes. A present
numeric path is a finite JSON number, a present boolean path is a JSON boolean,
and a required text path is a string. Numeric strings, integer substitutes for
booleans and wrong-root nested documents are invalid. Optionality is declared
per exact path: Device `dynamicIsland` may be absent and then exposes zero
editor geometry without modifying persistence; when present, it must be a
complete numeric object. Theme defaults are not reconstructed from missing or
wrong-root current token paths.

String-backed pair values remain owned by `ValueKind`, not by individual
controls. Integer, Theme-token, Palette-color and Palette-color-alpha pairs
require their complete two-member grammar; alpha and hue values retain their
intrinsic ranges. Pair, boolean, alpha, hue and icon-list controls consume these
owners on current value assignment and do not catch invalid current data to
manufacture an empty or default presentation.

Integer and Decimal dictionary controls validate assigned current values with
the shared `ValueKind` owner and the field's declared numeric range. Invalid or
incomplete interactive text may remain visible only as a transient draft; it
does not replace, clamp or commit the last valid value.

Pair presentation labels are also required owner metadata. Every pair field
declares both non-empty labels; current Runtime definitions without them fail.
The dictionary must not infer labels from field ids, JSON keys, type, hierarchy
or position, and must not provide generic `W`/`H`, `X`/`Y`, `Light`/`Dark` or
`A`/`B` fallbacks.

Visual fallback policy remains separate from current-document validation. A
declared missing-media placeholder or unsupported inline preview does not
authorize an empty current config, Theme, Variant, Runtime contract or
animation document.

Design Preview actions are strict declarative documents. `actions` and
`itemActions` preserve array/object roots, unique stable ids, explicit labels,
time unit, completion and finite duration ownership. Present optional scalar,
list, target and visibility metadata keeps its exact type and complete group;
the editor does not omit an invalid action or invent id, label, time unit,
boolean, number or list members.

Their Theme duration ids are exact entries in the common numeric token catalog.
The shared numeric Theme-value owner resolves the catalog path and requires a
finite number with the range declared by the consumer: positive action/pace
duration or non-negative Motion delay/duration. Missing paths, numeric strings,
unknown tokens and invalid ranges never become zero or a one-frame action.
State-action source and destination ids are transient session values and may be
absent before a concrete transition; once present, each must resolve to one
exact authored State whose referenced Motion document remains strict.
An action-owned `durationMotionConfigPath` likewise resolves to one complete
Motion in the owner config and uses that same timing owner. Payload preparation
must materialize a positive duration or fail; downstream playback hosts do not
reconstruct or default the missing result.

Action runtime values remain strict after declaration validation. A direct
duration is a positive finite JSON number on the action's exact owner; a
collection-derived duration requires its declared array, stable object items
and non-negative numeric contributors. Action time is a non-negative JSON
number and action state is a JSON boolean. Session storage may serialize those
values as text internally, but parses them strictly before use. Missing,
wrong-root, numeric/boolean-string or non-positive current duration values do
not become zero, one frame or one second. Design frame preparation and the
Production owner timeline enforce the same numeric contract. An inactive
conditional finite action contributes no endpoint until its activating value
or keyframe is true.

Production Font file lists are also current typed documents. Every entry keeps
its required file name, normalized safe relative path, explicit normal/italic
style and integer CSS weight. Startup, repository access, editor summaries and
Preview-face preparation share one document owner; none may filter a malformed
entry, skip a missing path or infer style/weight defaults. Physical asset
existence remains a separate Preview/resource diagnostic and is never repaired
while reading persistence.

`BehaviorTiming` values and their declaring Runtime metadata remain one
temporal contract across editor, owner timeline and web Preview. A value keeps
its explicit mode, non-negative integer fixed duration and declared natural
pace token. A natural definition keeps one exact sibling string source,
supported semantic unit and positive numeric base rate. Invalid or incomplete
values/metadata do not become duration zero, a missing definition or a
calculated UI fallback. The dictionary control requires the same resolver when
it is constructed and rejects a missing owner or negative result instead of
showing calculated zero.

## 6. Failure and diagnostics

The current application uses fail-fast exceptions at internal payload and
persistence boundaries. Phase 1 keeps that model unless a real consumer needs
independent diagnostic aggregation.

Errors must identify the document, owner or field being rejected. They must be
reported through the established Messages/context surfaces and must not be
caught to produce plausible empty data.

A future collect-all preflight may introduce a minimal serializable diagnostic
DTO only when editor and export consumers exist. It must not carry controls,
delegates, windows, view models or navigation state.

## 7. Preserved boundaries

This phase preserves:

- stable ids and complete Variant references;
- explicit Forward and local Overrides;
- Design Test Values versus persisted Production payload;
- exact Shot Actor/Theme/Device context;
- owner-relative keyframes and stable target ids;
- complete frame resolution before Preview;
- generic bridge and renderer;
- shell-only `MainWindow`;
- dictionary-owned editable fields;
- read-only startup and explicit migrations only.

Valid current data must produce the same payload and Preview result. A newly
visible error is permitted only where invalid current input was previously
coerced or ignored.

## 8. Enforcement and tests

Architecture enforcement must verify:

- this contract is required by `AGENTS.md` and the architecture index;
- required Preview object roots are listed at the payload boundary;
- `previewJsonHelpers.parseObject` contains no blank-to-`{}` or wrong-root-to-
  `{}` fallback;
- registries remain id-only and do not validate concrete component fields;
- the renderer does not parse `appearanceMode` or override explicit payload
  `ThemeMode`;
- typography parsing keeps only its declared blank/`inherited` sentinel.
- Module definitions and every complete Module Variant use the exact
  record-class-owned config contract, with no empty object/array write fallback.
- Runtime Input defaults are validated and materialized by the exact
  `ValueKind` owner; no parallel reconciliation parser remains.
- Runtime Input presentation readers preserve complete input/collection
  definition arrays and exact optional metadata without filtering hidden,
  non-Runtime or malformed entries.
- current Runtime collections and every mutation preserve declared storage
  ownership, array roots and unique stable item ids.
- dynamic Runtime option presentation and action normalization share one
  strict source projection without filtering or positional labels.
- persisted Runtime scalars, collection fields, Test Values and keyframe values
  share exact `ValueKind` serialization and validation.
- explicit Module Instance reconciliation shares complete definition readers
  and never repairs a present invalid value or missing stable item id.
- Runtime presentation and Test Values use the shared current-value serializer;
  only an absent field may use its explicit definition default.
- forwarding envelopes and projected child Runtime documents reject wrong
  roots before registry dispatch.
- compound dictionary controls reuse the exact Runtime value and stable
  collection owners without local empty-document parsers.
- transient Test Values preserve strict roots and stable collection ids without
  becoming persisted Production payload.
- collection source application, record resolution and action lookup reuse the
  complete collection-definition and effective-item owners without raw
  metadata or `OfType` filtering.
- embedded component collection items share one metadata/item document owner
  across startup, Test Values, dictionary authoring, Usage and Preview action
  discovery.
- projected item Runtime contract objects remain required and shared by Test
  Values, actions, Runtime API presentation and animation targets.
- the common desktop owner timeline consumes complete Runtime contract arrays,
  stable items and timeline metadata without `OfType` filtering or wrong-root
  empty fallbacks.
- the web owner timeline preserves the same complete envelopes and rejects
  `asRecord`/array filtering fallbacks before frame resolution.
- Preview frame-state resolvers distinguish absent instance `context` and
  `animation` members from present wrong-root values without changing clock
  authority or owner-relative formulas.
- Component Stack exit-event projection consumes the shared transient
  animation owner and exact keyframe scalars; its descending event traversal is
  not a document-normalization fallback.
- Notifications requires exact config, embedded slot/Input/Override and Runtime
  item documents before composing its child Components.
- transient forwarded Runtime transitions distinguish structural absence from
  a present incomplete object and retain a positive integer source frame plus
  explicit previous value.
- Component Variant base lookup requires an exact variants object and exact
  referenced config object before applying local Overrides.
- embedded Component config uses one shared full-reference plus required local
  Overrides owner; each parent still requires its declared slot objects.
- Avatar and Label require their declared config/style/slot objects and route
  every child through the shared embedded Component owner.
- Notification requires both mode slots plus Avatar/Surface composition and
  uses no local embedded-config parser; Badge requires its config object.
- Button requires its selected State and declared Label/Surface/Badge slots and
  routes each child through the shared embedded Component owner.
- Icon Row preserves exact item objects, stable ids, full Button references and
  local Overrides; Icon Bar requires each selected state/zone slot and Inputs.
- Keyboard and Keypad require their config/slot documents; Keypad keeps exact
  key entries and selected state objects without positional identity.
- Password validates its complete explicitly named state/method slot set and
  exact Runtime definitions before resolving the selected mode.
- Password state children require exact config/state objects; Code Indicator
  also requires and resolves both Surface slots through the shared owner.
- Surface requires config/Tail/style roots and Cursor requires its config root
  before resolving atomic visuals.
- Status/Navigation Bar require exact layout/gesture/item documents and retain
  stable item identity before visibility and zone ordering.
- Collection Stack requires the exact Theme motion object for every Reflow
  calculation without changing its owner-relative item timing.
- Lock Screen requires its Module config root and prepares all three child
  configs through the shared embedded owner.
- present desktop track, keyframe and retime envelopes are validated before the
  common timeline calculates duration or frame origins.
- the web timeline mirrors the exact transient track/keyframe/retime envelope
  and empty Screen target sentinel.
- desktop collection/field temporal metadata and every referenced duration
  value are validated without unknown-kind or frame-zero fallbacks.
- web collection/field temporal metadata and direct/embedded duration values
  follow the same exact contract before frame resolution.
- the explicit forwarded field `animationTimeline: null` sentinel remains
  distinct from an invalid collection timeline or wrong-root field value.
- root Screen duration policy defaults to calculated only by absence and keeps
  explicit duration as a positive integer contract.
- Component Class, Component Variant and local Override field reads/writes use
  their exact dictionary `ValueKind`, and existing embedded slot/Override roots
  are never repaired during an edit.
- record-resource boolean and numeric commits reject invalid text before
  persistence rather than coercing it to false or zero.
- resource field and Preview reads reject wrong current scalar/nested shapes;
  only the declared absent Device `dynamicIsland` keeps explicit optional
  zero geometry.
- primitive/compound dictionary controls reuse exact pair, range, boolean and
  icon-list owners instead of local fallback parsers.
- Integer/Decimal controls reject invalid or out-of-range current values and do
  not convert an invalid interactive draft into zero or a clamped commit.
- pair controls and Runtime definitions require explicit labels without
  identifier- or position-based inference.
- Design Preview actions are validated at startup and by their shared reader;
  incomplete entries, wrong roots and scalar/list coercions are rejected.
- declarative action Theme durations and Behavior Timing pace use one strict
  catalog/path value owner; undeclared, absent, wrong-type or invalid-range
  timing cannot be interpreted as zero/default.
- action Motion-path durations and State Motion durations share one strict
  Motion/Theme timing owner before playback.
- direct and collection-derived action duration, time and state values use one
  strict runtime owner in both Design hosts and the Production owner timeline;
  malformed values never become zero, one frame or one second.
- session initialization distinguishes absent action play/time state from a
  present wrong scalar; only the former initializes to false/zero.
- Production Font file entries share one startup/read/projection contract;
  malformed entries, unsafe/duplicate paths and inferred style/weight defaults
  are rejected.
- `BehaviorTiming` values, sibling-source metadata and both desktop/web
  resolvers reject missing members, wrong scalar shapes and zero-rate/default
  inference.
- the `BehaviorTiming` dictionary control requires its owner resolver and never
  replaces an unavailable calculation with frame zero.

Tests cover every required payload root with valid, malformed and wrong-root
input; optional icon mapping absence and invalid presence; explicit light,
explicit dark and inherited mode; strict typography object parsing; and the
existing read-only database proof. Module config tests cover both current
Module classes, definition and Variant documents, invalid nested roots and
rejected field commits without persistence changes.

## 9. Out of scope

This phase does not redesign UX, add export or Render Mode, change payload
storage, migrate the database, add a general validator framework, change Theme
selection, redesign visual placeholders or introduce new Component/Module
semantics.

## 10. Forbidden shortcuts

- returning `{}`, `[]`, `null`, the first record or frame zero after invalid
  required data;
- validating component-specific fields in a registry, bridge or renderer;
- parsing Module `appearanceMode` in the renderer;
- catching a required payload parse error to retain a plausible render;
- treating malformed typography as inherited;
- adding validation that mutates or normalizes current persistence;
- keeping an old permissive parser beside a new strict parser.
