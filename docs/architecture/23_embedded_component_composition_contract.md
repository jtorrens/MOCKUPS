# Embedded component composition contract

Date: 2026-07-05

This document defines the desktop editor and web preview contract for recursive
component composition.

The goal is a design system made from reusable component classes. Parent
components may embed child components without copying the child's fields, UI or
render logic.

For the related but distinct contracts of Component Stack slots/States,
Collection Stack items, Module Variants and Shot Module Instances, see
[Structural Stacks, Slots, States and Module Instances](31_structural_stacks_slots_and_module_instances.md).

## Core Model

An embedded component is a slot, not a duplicated class.

```text
parent component class
  -> embedded slot metadata
  -> child component base class
  -> child component active variant
  -> slot-local overrides
  -> component resolver
  -> component renderable
  -> generic preview helpers
  -> generic web renderer
```

The parent owns:

- slot visibility and placement fields;
- slot-level layout such as generic alignment placement;
- an `overrides` object containing only deliberate local edits.

The embedded child owns:

- its normal field catalog;
- its normal dictionary controls;
- its reusable variants;
- its normal resolver contract;
- its normal resolver/renderable/helper path.

The parent must not duplicate the child component's scalar fields. If a child
field is editable inside the parent, the editor must open the embedded editor
context and write into the slot-local `overrides` object.

## Override Identity

An override exists because the user made a local decision.

That decision remains an override until the user explicitly restores/inherits
the field, even if a later parent/base-class edit makes the effective value
numerically or textually equal.

Therefore:

- amber state means "this slot has saved overrides";
- amber state must not be computed by comparing effective values;
- restore/inherit removes the override entry;
- an empty `overrides` object is not an override by itself;
- nested objects count only when they contain a concrete leaf value.

## Variant Layer

Every component class has one or more named variants. A variant is a named config
snapshot stored with the component class metadata. It is not a separate
component class and it must not duplicate resolver/render code.

The effective base for a component instance is:

```text
component class
  -> selected variant config
  -> slot-local overrides
  -> runtime inputs
```

Rules:

- parent component classes are internal system definitions;
- the tree must not expose Add on the Component Classes root;
- the tree must not expose Duplicate/Delete on parent component class nodes;
- user variation happens through variants and embedded overrides;
- every component class must have a protected `Default` variant;
- `Default` cannot be renamed or deleted;
- duplicated variants can be renamed/deleted unless usage checks block deletion;
- parent component classes may be renamed from the tree for identification, but
  they cannot be duplicated, deleted or edited as component configurations;
- selecting a component class in the tree must resolve to a concrete variant;
- if no variant has been selected for that component class in the current
  session, the editor selects `Default`;
- if a variant was selected earlier in the session, navigating back to that class
  selects the last used variant;
- selecting a variant in the tree changes the design-preview base config to that
  variant;
- composition, embedded slots, Theme Status Bar and Theme Navigation Bar must
  reference a concrete variant, never the parent component class as a reusable
  visual value;
- the editor may still show the parent component class layout, but the active
  variant name must be visible and the variant node must be selected in the tree.
- editor fields shown while a variant is selected read and write that variant's
  `config`, even though the visible layout comes from the owning component
  class;
- saving a new variant while another variant is selected copies the active variant
  config, not the component class's mutable config.
- saving a variant is only valid from a selected variant. The component class is
  schema/ownership, not a cloneable visual base.

An embedded slot stores a full child variant reference in its `presetId` field:

```text
componentClassId::preset::presetId
```

The persisted field and delimiter keep `preset` until a dedicated storage
migration renames them. They should be treated as internal compatibility
spelling.

When a field is restored to inherited state, it restores to the selected variant
value, not to the component class's mutable current config.

Changing a base variant does not remove slot-local overrides. Override identity
is explicit and survives coincidental equality with the variant value.

Usage checks must inspect both component class config and every variant config.
Deleting a variant is blocked when any component class slot or any other variant
slot references it.
System component selectors, such as Theme Status Bar and Navigation Bar, must
store variant references, not parent component class ids.
Status Bar and Navigation Bar must not be exposed as separate editable tree
sections once their component classes exist. Legacy storage may remain only as a
temporary compatibility layer; all new editing and Theme assignment goes through
component class variants.
Their preview manifest and registry entries must also use explicit component
modules (`statusBarComponent...` and `navigationBarComponent...`). They may
share family helpers internally, but those helpers are not public component
entrypoints and must not be used by the registry as a shortcut.
The editor structure modal must separate class usage from variant usage with a
compact switch:
class usage answers where the component type is embedded structurally, while
variant usage answers where the active variant is referenced. Theme Status Bar and
Navigation Bar references appear only in variant usage, because Theme selects a
concrete variant, not the generic component class.

## Editor Boundary

The Avalonia editor edits structured data. It does not render final visuals.

Allowed editor responsibilities:

- list embedded slots from `EmbeddedComponentSlotCatalog`;
- list component variants under their owning component class;
- remember the last selected variant per component class for the current editor
  session;
- open the child component editor in an embedded context;
- show inherited values from the child selected variant;
- save local edits into the parent slot's `overrides`;
- show visual affordances for embedded context and override state.

Disallowed editor responsibilities:

- copying child component fields into the parent catalog;
- creating a manual UI for a child field outside the dictionary route;
- resolving theme colors, palette colors, SVGs or device pixels for preview;
- generating final renderable geometry for the web renderer;
- adding component-specific logic to `MainWindow` beyond generic embedded-editor
  hosting.
- opening a component class as a special "current class values" design target
  when a concrete variant exists.

The dictionary route remains mandatory:

```text
embedded editor context
  -> child FieldDefinition
  -> ValueKind
  -> dictionary control
  -> generic commit path
  -> parent slot overrides
```

Embedded component slots use a dedicated dictionary value kind for variant
selection. The visible slot row is one `ComponentPreset` control: it selects the
child variant and opens the embedded child editor from the same row. Do not show
`*.presetId` as a separate editor row for slots such as Avatar Label, Button
Icon Label, Audio Avatar or Audio Badge.

The stored value for a selected embedded variant is a full component variant
reference, not a short local id:

```text
componentClassId::preset::presetId
```

The persisted reference still uses `preset` internally until a dedicated storage
migration renames it. In UI and architecture language, this is a component
variant.

This is required because multiple component classes of the same type can define
variants with the same local id. A slot such as Audio Badge must therefore know
both "Button Icon class = Icon Badge" and "variant = Icon Badge" before the
editor or web preview resolves inherited values.

Inside an embedded editor, reset/inherit restores the selected variant for that
slot, not the child component's `Default` variant. Ancestor slot overrides may
affect nested slot selection, but the overrides of the slot currently being
edited are never part of its inherited value.

## Resolver Boundary

Component resolvers consume current-model data and produce a typed design
contract for the bridge.

Allowed resolver responsibilities:

- parse the parent component config;
- merge child base class config with slot-local overrides;
- validate required fields;
- decide component semantics such as whether a child is present;
- normalize child/owned atom visibility, zone/group assignment and ordering;
- keep dimensions in design units;
- keep colors/radius/text-size references as tokens.

Disallowed resolver responsibilities:

- reading editor controls or draft UI state;
- reading SQLite directly;
- resolving palette colors to hex;
- applying neutral tint;
- applying theme mode;
- applying device scale to pixels;
- generating HTML/CSS;
- using plausible development fallbacks for missing current-model data.

Missing required current-model values must fail visibly and reach the editor
message area.

## Component Input Boundary

Component inputs are runtime inputs, not preview-only controls.

They are the values supplied by the surrounding composition process when a
component is resolved for a frame. In isolated design preview those values come
from sample controls. In a real screen they come from the screen/module payload,
shot state, actor selection, playback state, timing, or other declared runtime
data.

Examples:

- a label exposes text/subtext inputs when those strings are supplied by a
  parent module;
- an avatar exposes actor input when actor data is supplied externally;
- audio exposes playback state, duration and actor input;
- future bubbles will expose message, sender, status, media and timing inputs.

Rules:

- input declarations belong to the component contract/data, not to the preview
  panel;
- preview may edit sample values, but must not define component-specific
  catalogs;
- animation controls are generic clock/input state and ask the resolver for a
  frame with filled input values;
- screen composition must use the same input declarations and replace preview
  sample values with real module/frame values;
- resolvers decide how input values affect component atoms;
- bridge and web renderer must not infer missing inputs or know which concrete
  component declared them.

## Runtime Input Forwarding

When a parent Variant embeds a child, every child runtime input crosses a new
composition boundary. The parent component/module design may explicitly keep
that binding as Runtime or declare it Calculated. Otherwise it becomes
Variant-owned by default. The designer may explicitly expose a Variant-bound
value to the parent runtime through generic Runtime Input Forwarding.

The editor represents this with an empty/filled upward triangle, distinct from
animation keyframe diamonds. Activating the triangle preserves the current
Variant value as the runtime default and reveals an editable runtime label.
The technical input id and payload key remain stable when that label changes.
Only fields already declared as `Runtime` by the child are eligible. Variant
and Calculated child fields never acquire a forwarding affordance.

Forwarding metadata is stored beside the child input values under
`$forwardedInputs`. The effective parent runtime contract is its declared
runtime contract plus those forwarded definitions. A forwarded input is a
normal runtime input at the next composition boundary and may be forwarded
again without knowing its final owner.

Forwarded fields are readonly at the Variant boundary. Their value is edited
only through the effective parent Runtime Input. Disabling forwarding restores
the retained Variant value; transient test values are never copied back into
the Variant.

Forwarding is recursive but never implicit. At the next parent boundary the
forwarded value is presented as an ordinary child runtime input. The next
parent design may explicitly keep it Runtime; otherwise it becomes Variant
there by default and may be forwarded again deliberately. A
matching id, label, `ValueKind`, `tableId` or record id does not bind two
inputs. This is particularly important for Actor references: the Lock Screen
owner, an Audio Avatar, a Conversation participant and a notification sender
may all be different Actors.

Forwarding preserves the complete reusable runtime contract metadata, not only
the scalar value kind. Animation eligibility, interpolation choices,
`BehaviorTiming` natural-unit metadata, enablement and input transitions are
rebased to the forwarded stable ids. If a promoted field participates in a
declared action, that action is lifted only when every field it requires has
also been promoted. Its play field, action clock, target and duration references
are then rebased generically. An incomplete action is not exposed and no parent
editor invents missing trigger, timing or progress values.

Structured collections use one shared collection shell for Variant authoring,
Runtime Test Values and instance runtime. The context is explicit metadata, not
inferred from a card label or hierarchy depth. Add, duplicate, reorder, delete,
selection and expansion are shared. Storage and field capabilities come from
the context adapter. Runtime Test Values and instance runtime never display
forwarding controls. Variant authoring reuses the normal child input binding
control, so runtime inputs of a component selected inside an item receive the
same forwarding triangle. Forward ids include the stable item id, never the
item index.

Structural collections may declare an explicit runtime projection. In that
case the Variant remains the sole owner of ordered child structure, presets,
Overrides, Placements and Motions. Test Values and Module Instances persist only
the projection fields keyed by stable item id, such as selected State, source
State, transition flag and elapsed action time. Before resolving a requested
frame, the common contract layer joins that minimal runtime array to the Variant
source array. The merged structure is transient and is never written back to
the instance payload.

When a projected structural item owns nested States, the projection may also
declare a second, related runtime collection. That collection contains one
entry per stable State id and only the forwarded runtime contract/value object
for that State. Parent metadata relates every State entry to its slot id, so the
shared editor presents `Slots -> Slot -> State` without copying the Variant's
component reference, Overrides, Placement or Motion into editable instance
structure. The two projected arrays remain flat in storage for stable lookup
and timeline ownership; their parent relation is contract metadata, not an
editor-specific hierarchy rule.

A Component Stack adds one more stable nested identity level: the outer item is
the slot and its nested item is a State. State actions and animation tracks use
the State id, while flow and gap ownership use the slot id/order. Collection
Stack has only item identity and must not be interpreted as this two-level State
model.

JSON-valued inputs retain their declared shape while forwarded. For example,
`IconSlots` is exposed through the generic `iconList` runtime kind and remains
an array; it must not be serialized as an opaque text value. Record references
retain `tableId` and `resolvedJsonKey`. Design and Production resolve those
references recursively from the contract before preview, but never persist the
resolved record object beside the authored record id.

Before a component resolver runs, the generic forwarding pass writes resolved
parent runtime values into their declared child input locations. Component
resolvers continue to own composition; bridge and renderer never see forwarding
metadata or implement component-specific routing. The pass traverses nested
objects and collections and applies every forwarding definition owned by the
current runtime boundary. A complete forwarding block is consumed only when
that boundary supplies all of its declared keys; a block owned by a deeper
embedded component remains intact for that component's resolver pass. Partial
ownership is an error. State-owned values are found by stable State id, never by
array position. The consumed `$forwardedInputs` marker is removed from the
transient resolved config, so each child resolver receives only its final values
without the module pass stealing contracts that belong to a deeper component.

Stopping forwarding retains the current Variant value. If downstream bindings
use the effective input id, the operation is blocked and the usage modal must
offer direct navigation links to those editors. It must never silently cascade
through parent Variants, Screen payloads or animation tracks.

The downstream-usage blocker and navigation list are the required completion
rule for disabling a used Forward. Until that usage graph is implemented for a
particular owner, the editor must at minimum require confirmation and must not
silently remove data from any parent.

## Shared structured editor surface

`StructuredCollectionEditor` is the common collection shell. Its editing
context is one of Variant authoring, Runtime Test Values, instance runtime or
Runtime API. The context controls storage and available affordances; component
or module names do not select a different editor.

For component items, `ComponentInputBindings` embeds the same grouped Runtime
Inputs surface used by Test Values:

- sections such as Layout, Playback and Actor come from input UI metadata;
- Variant authoring adds Forward triangles and runtime-label editing;
- inputs explicitly declared Runtime by the parent design remain ordinary
  Runtime inputs and do not need Forward at that boundary;
- Test Values and instances use the same dictionary controls without Forward;
- a forwarded field is readonly in the Variant surface;
- adding an item expands and reveals the new stable item;
- changing Component constrains the second selector to that Component's
  Variants, replaces the item-local overrides and input values, and invalidates
  the parent runtime contract immediately; Forward fields from the previous
  component must disappear from Runtime Inputs in the same operation;
- changing only the Variant inside the same Component preserves the compatible
  item inputs, overrides and Forward contract because the Component class owns
  that schema;
- changing Component or deleting an item that contains Forward fields requires
  one `Accept` / `Cancel` confirmation listing the fields that will disappear;
  accepting performs the destructive contract change and cancelling restores
  the previous selection or leaves the item untouched;
- Override opens the ordinary embedded editor and the breadcrumb returns to the
  previous tab, expansion and scroll position.

Complex dictionary values (`ComponentInputBindings` and
`StructuredCollection`) use block layout: a full-width separator opens the
field, its label occupies the next row when present, and the value editor uses
the complete following row. No closing separator is added; the next complex
field opens its own boundary and the containing card closes the final block.

`verticalCards` uses two real panels while enough width is available. The left
navigation width is session-only and resizable with an always-visible vertical
splitter. The vertical layout remains active while this fits:

```text
selected navigation width + splitter width + minimum content width
```

If the content would fall below its minimum, navigation becomes horizontal.
It returns to vertical with a small hysteresis margin and restores the last
vertical width. The block height is natural—the maximum of navigation and
selected content—not the remaining card height. Navigation widths, selected
tabs, card expansion and scroll restoration are session-only and are never
written to window state. The draggable splitter and visual divider are arranged
to that resolved natural height but are excluded from desired-height
measurement, so a stretch-aligned splitter cannot make the block consume the
remaining editor viewport.

All ordinary and embedded Component/Variant selectors use the shared selector
contraction contract. They have no dictionary-field minimum width, keep the
selected label on one line with ellipsis and clip it inside their assigned grid
column. Parent/open/Override and Forward action columns remain outside that
column and cannot be covered by selector content.

## Generic Preview Helper Boundary

Generic preview helpers convert reusable values inside validated component
contracts into final web renderable node data. They replace the useful generic
parts of the old bridge without becoming a component catalog.

Allowed helper responsibilities:

- resolve theme tokens for the selected theme mode;
- resolve all available color variants for mode switching;
- resolve palette colors and neutral tint;
- apply device design-to-pixel scale;
- resolve icon/SVG file references into web-consumable assets;
- compose renderable nodes with explicit boxes and styles.

Disallowed helper responsibilities:

- reading or merging component config JSON directly;
- deciding whether an embedded child exists from raw config;
- deciding which owned atoms exist or which zone/group they belong to;
- applying editor inheritance or override rules;
- reaching into SQLite or desktop editor services;
- hiding missing data behind plausible colors, sizes or spacing.

## Web Renderer Boundary

The web renderer is the visual truth.

Allowed renderer responsibilities:

- render final renderable nodes;
- apply CSS effects described by the renderable contract;
- show debug marks when requested;
- provide visually obvious unsupported placeholders.

Disallowed renderer responsibilities:

- reading component class config JSON;
- resolving theme tokens;
- applying inheritance;
- keeping old component-specific preview branches for migrated components;
- making missing data look plausible.

## Current Implemented Pattern

`component.avatar` embeds `component.label` through
`component.avatar.label.editor`.

`component.buttonIcon` embeds `component.label` through
`component.buttonIcon.label.editor`.

Avatar label placement uses the generic alignment placement value:

```json
{
  "mode": "outsideEdge",
  "alignX": 1,
  "alignY": 0.5,
  "offsetX": 4,
  "offsetY": 0
}
```

`mode: "center"` places the child center on a normalized parent point.
`mode: "insideEdge"` keeps the child inside the supplied parent box and
interpolates from aligned start edges, through center, to aligned end edges.
`mode: "outsideEdge"` interpolates from the child end edge touching the parent
start edge, through center, to the child start edge touching the parent end
edge. Offsets are design pixels and are scaled by generic preview helpers. A
parent that owns padding supplies its already-padded inner box to placement.

Current storage shape:

```json
{
  "avatar": {
    "labelSlot": {
      "showLabel": true,
      "showSubtext": true,
      "placement": {
        "mode": "outsideEdge",
        "alignX": 1,
        "alignY": 0.5,
        "offsetX": 4,
        "offsetY": 0
      },
      "overrides": {
        "label": {
          "textSizeToken": "theme.textSize.xl"
        }
      }
    }
  }
}
```

The preview path is:

```text
DesignPreviewPayloadFactory
  -> renderDesignPreviewHtml.tsx
  -> resolveAvatarComponent
  -> mergeComponentDefaults(base label, labelSlot.overrides)
  -> resolveLabelComponentFromRecords
  -> avatarComponentToRenderable
  -> labelComponentToRenderableAt
  -> DesktopRenderableHtmlAdapter
```

This is the reference path for future embedded components.

`component.buttonIcon` follows the same route with
`resolveButtonIconComponent` and `buttonIconComponentToRenderable`. Its own
fields are limited to icon surface concerns such as icon padding, background
token, background alpha and icon color token. Label text styling remains owned
by the embedded label component and is overridden through `labelSlot.overrides`.

## Migration Rule

When a composite component is migrated, migrate its owned subcomponents through
this same route. Do not partially migrate the parent while leaving child
geometry, colors, text, icons or media on a legacy preview branch.

For message bubbles, the bubble and its owned avatar, labels, media, audio,
video, icon button, tail/chrome and status subcomponents must move together or
remain explicitly unsupported in component-class preview.
