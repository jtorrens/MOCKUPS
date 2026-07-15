# Simplified Editor Projection — implementation proposal

Date: 2026-07-15  
Status: first generic slice implementation-complete; Keypad is the first
configured component and broader use testing remains pending

## Purpose

Keep the full, metadata-driven editor as the single authoring model while
offering a reduced **Simplified** tab for the controls used most often. This is
a presentation projection of the existing field graph: it does not introduce a
second schema, controls, values, commits, resolver route, or preview route.

```text
layout metadata -> FieldDefinition -> ValueKind -> dictionary control
                                      |                    |
                                      +-- simplified projection --+
                                                           |
                                                    same commit path
```

The existing tab remains the complete view. The new Simplified tab displays
only promoted fields through the same registered dictionary controls.

## User interaction

- In the complete editor, a field or compound field group has a promotion
  checkbox. Selecting it includes the complete control in Simplified; a
  compound control is never split into its scalar leaves.
- The Simplified tab groups promoted controls using their declared ownership
  path, not component names, field-label heuristics, or editor-specific rules.
- Fields owned directly by the current class belong to the first-level
  `Component` section.
- Fields owned by an embedded child belong to a first-level vertical tab named
  after that embedded slot/control.
- One nested embedded origin may create a second level. A third navigation
  level is invalid; its controls are shown in the nearest valid second-level
  group rather than creating more navigation.
- The existing shared `verticalCards` surface renders this navigation. Its
  selection and splitter size are session-only, exactly like other internal
  editor navigation.

The complete and Simplified tabs are editor presentation state. Their selected
tab is session-only, and neither it nor card expansion is written to
`window-state.json`.

## Metadata model

Add generic layout metadata rather than adding component-specific editor code.
The exact persistence names are illustrative:

```text
SimplifiedProjection
  ownerContextId            stable variant or embedded-slot context
  entries[]
    fieldPath               stable owner/slot path + FieldDefinition id
    enabled                 shown in Simplified
    source                  local | capturedEmbeddedDefault
    capturedFrom            optional child variant reference + revision marker
    groupingPath            declared ownership path, maximum two levels
    order                   explicit local order
```

`fieldPath` identifies an existing field; it never carries a copied field
definition, ValueKind, label, validation rule, or stored field value. The field
catalog continues to own those things. `groupingPath` is derived from the
embedded ownership metadata when the projection is materialized, not inferred
from UI labels or tree depth.

The renderer receives ordinary generic section metadata plus the filtered field
paths. It resolves each path back through the normal FieldDefinition and
dictionary-control registry. `MainWindow` only hosts the generic tab surface
and delegates projection construction/rendering to shared editor-shell code.

## Embedded defaults are snapshots, never live inheritance

An embedded atom or component can expose its own Simplified projection. When
that component is selected into a parent slot, its currently enabled entries
are **materialized once** into the parent slot's projection as
`capturedEmbeddedDefault` entries. They appear enabled by default in the
parent's Simplified editor.

The parent can then:

- disable any captured entry;
- add other valid fields from the same embedded context;
- reorder its local projection;
- retain the captured selection even if the child later changes its own
  Simplified tab.

This makes authoring convenient without defining a transitive, live UI
inheritance system. Child changes do not silently alter any parent that embeds
it, and parent changes do not modify the child.

### Lock / capture affordance

Each captured entry shows a small lock/capture icon. Its meaning is:

> This entry was copied from the embedded component's Simplified definition and
> is now owned by this parent context.

The icon is provenance, not a read-only field lock. The parent may toggle or
remove the entry. Once locally changed, it may show a `locally adjusted` state,
but it still remains a normal local projection entry.

There is deliberately no automatic refresh. If a future workflow needs it,
provide an explicit **Refresh captured defaults** command scoped to one slot:

1. show the changes it would apply;
2. require confirmation because it changes saved parent presentation data;
3. preserve manually added entries unless the user explicitly chooses reset;
4. write a new snapshot.

It must never run as a background update, resolver behavior, or preview
fallback.

## Ownership and persistence

- Component/atom class Simplified selections live with the concrete selected
  variant's editor-layout metadata.
- An embedded use has its own slot-local projection metadata alongside its
  existing slot-local presentation/override context. It is not stored in the
  child variant and is not an override of a visual field value.
- A projection is presentation configuration, not runtime component data. It
  must not cross the editor -> resolver -> contract -> renderable -> web
  renderer boundary.
- New data must be introduced with one explicit migration of seeds and the
  committed desktop SQLite parity file. Do not add old-schema aliases or
  compatibility coercion.

The selected child variant continues to own the base values of child fields;
the projection only decides which already-editable controls are visible in a
particular parent Simplified tab. Normal visual field overrides retain their
existing explicit semantics and are not inferred from projection membership.

## Generic implementation boundaries

Shared editor-shell code should own:

- validation of field paths and the two-level maximum;
- capture/materialization of child Simplified defaults;
- filtering and grouping generic layout metadata;
- the promotion checkbox/indicator and explicit refresh workflow;
- generic simplified-tab composition using existing `verticalCards` and
  dictionary controls.

Component-specific code may only declare its regular field catalog, embedded
slot catalog and normal layout metadata. It must not construct Simplified
controls by hand or add component-name branches to the shell.

The existing rules remain binding:

- every editable value is a FieldDefinition rendered by the dictionary route;
- compound value kinds own their own visual layout;
- `MainWindow` is shell/orchestration only;
- no new component knowledge enters preview helpers, bridge or renderer;
- common surfaces are reused before creating new UI.

## Suggested delivery sequence (after the current component)

1. Audit existing Common editor layout and field-path routines; extend an
   equivalent shared abstraction instead of adding a local helper.
2. Define and migrate the projection schema, including seeded variants and the
   committed desktop SQLite database.
3. Build the generic field/group promotion metadata and validation, initially
   for direct class-owned fields only.
4. Add the generic Simplified tab composed through existing vertical tabs and
   registered dictionary controls.
5. Add embedded-default capture and local enable/disable behavior.
6. Add the capture/provenance icon; defer explicit refresh until there is a
   real authoring need.
7. Test a direct field, a compound field, one embedded child and a two-level
   nested child; verify that child changes do not affect existing parent
   snapshots.
8. Run the normal desktop checks and `npm run check:architecture` before the
   migration is closed.

## Acceptance criteria

- Editing a field in either tab produces the same persistence and preview
  result.
- No raw value controls are created for the Simplified tab.
- A child Simplified change after embedding does not alter the parent's
  captured projection.
- Disabling a captured field affects only that parent/slot context.
- Navigation is no deeper than two embedded-origin levels.
- Simplified metadata never reaches resolver or preview payloads.
- A fresh application session starts from the normal closed-card/session-state
  baseline; no presentation state leaks into persisted window state.

## Implemented first slice (2026-07-15)

The generic editor now reads `simplified` metadata from `editor_layouts`, shows
a session-only **Simplified / Complete** segmented selector, decorates eligible
fields in Complete with promotion checkboxes and composes Simplified from the
same dictionary controls and commit paths.

General opens by default when a context first enters Simplified. Its manual
expanded/collapsed state, the selected top-level group, the selected nested
group and scroll position remain session-only and survive conditional-control
rebuilds. Changing a discriminator such as Key 10 Kind therefore refreshes its
dependent fields without navigating away from Keys -> Key 10.

Keypad validates the three required ownership routes:

- direct fields: Sizing and Key size;
- nested embedded field: Label -> Surface -> Corner radius;
- structured collection item: Key 10 -> Kind and Icon.

Embedded defaults are materialized once. Password therefore captures Keypad's
five enabled defaults when its Keypad slot is first projected. A persisted
`capturedSlots` marker prevents later Keypad changes from being applied to that
Password projection. Captured rows show a lock provenance marker but remain
locally removable through the Complete editor.

This closes the planned first slice. Further work is validation through normal
use and adding projection declarations to other components; it is not another
editor-shell mechanism or a live-inheritance phase.
