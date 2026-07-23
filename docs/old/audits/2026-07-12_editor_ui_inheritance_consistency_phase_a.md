# Desktop Editor UI, Inheritance and Navigation Audit — Phase A

Date: 2026-07-12

Base commit: `22c6077a` (`refactor: complete conversation component composition`)

Status: Phase A reviewed and approved; no fixes applied

## Executive summary

Phase A inventories the complete desktop editor surface and audits UI language,
inheritance presentation and embedded navigation. Static inspection, the
committed SQLite inventory and representative interactive verification are
complete. macOS Accessibility and screen-capture access were enabled during the
audit, allowing real input events, pixel inspection and restart verification.

No committed database or user preset was modified. The app was started directly
from the base commit. `artifacts/` and `data/window-state.json` were left intact.

The phase found four P3 UI-language/chrome inconsistencies and no P0, P1 or P2
finding. The critical inheritance sample passed: an explicit local value remained
an amber override even when equal to its parent, restore removed the persisted
entry, and the removal survived restart. Concrete-variant navigation and recursive
Bubble → Avatar → Label navigation also passed.

## Scope and evidence

- Repository and database base: `main`, `HEAD = origin/main = 22c6077a`.
- Required architecture documents read: editor shell non-negotiables,
  modernization roadmap, desktop preview component architecture, component
  migration status, desktop preview pipeline and time-units contract.
- Database inspected read-only: `data/desktop-editor-spike.sqlite`.
- Write/restart checks used `data/desktop-editor-spike.phase-a-audit.sqlite`, a
  disposable byte-identical copy removed after testing.
- Source inspected: layout metadata, field catalogs, dictionary embedded control,
  embedded editor controller/context, runtime-input collection editor and shell
  wiring.
- Application restart and interaction: successful; real CoreGraphics pointer
  events, screenshots and SQLite state queries were used as cross-evidence.
- A first disposable copy under `/private/tmp` made relative assets resolve under
  `/private/tmp/assets`; that artificial setup was discarded and is not classified
  as a product finding.

## Exhaustive editor matrix

This matrix is the Phase A reference inventory derived from `editor_layouts`,
record tables and active component/module records.

### Active record editors

| Area | Record class/editor | Seeded records | Phase A surface |
| --- | --- | ---: | --- |
| Production | `project` | 1 | scalar cards |
| Production | `episode` | 3 | scalar cards |
| Production | `shot` | 5 | scalar cards + ordered Module Instances |
| Production | `module_instance` | 3 | scalar cards |
| Production | `device` | 8 | scalar cards/import |
| Production | `actor` | 3 | scalar cards + avatar preview |
| Production | `palette_color` | 24 | scalar cards |
| Production | `production_font` | 4 | scalar cards/font files |
| Production | `icon_theme` | 6 | scalar cards + token collection |
| Production | `theme` | 2 | 11 token/reference cards |
| Production | `render_preset` | database-backed | scalar cards |
| App | `app.core.chat` | 1 app record class | scalar cards |
| App | `app.generic` | 1 app record class | scalar cards |
| Module | `module.core.chat` | 1 | composition, messages and runtime/test values |
| Module | `module.generic` | 0 | generic fallback layout |

### Active component editors

Every row has one seeded component class and one protected Default variant;
additional variants are children of the same class.

| Category | Record class | Embedded/navigation-sensitive surface |
| --- | --- | --- |
| Atom | `component.surface` | none |
| Atom | `component.cursor` | none |
| Atom | `component.textBox` | Surface, Cursor and internal icon-row slots |
| Atom | `component.iconRow` | stable Button item collection |
| Atom | `component.iconBar` | left/center/right Icon Row slots by state |
| Atom | `component.label` | Surface slot |
| Component | `component.avatar` | Label slot |
| Atom | `component.button` | Surface and Label slots for Normal, Active, Pushed and Disabled |
| Component | `component.audio` | Surface, Avatar, duration Label and Button badge |
| System | `component.status_bar` | status item collection |
| System | `component.navigation_bar` | navigation item collection |
| System | `component.textInputBar` | Surface, Text Box and Icon Bar |
| System | `component.keyboard` | Icon Bar and keyboard composition |
| Component | `component.media` | Surface and inline/full-screen Icon Bars |
| Component | `component.bubble` | Surface, Text Box, image/video Media, Audio, actor Label and Avatar |

### Navigation-only layouts

The database also contains shell/navigation layouts for:
`navigation.actors`, `navigation.apps`, `navigation.component_classes`,
`navigation.devices`, `navigation.episodes`, `navigation.icon_themes`,
`navigation.navigation_bars`, `navigation.palette`,
`navigation.production_data`, `navigation.production_fonts`,
`navigation.render_presets`, `navigation.status_bars`,
`navigation.system_data` and `navigation.themes`.

The database retains layouts named `status_bar` and `navigation_bar`, while the
active editors use `component.status_bar` and `component.navigation_bar` and no
legacy physical status/navigation tables exist. These two layouts are classified
as inactive layout residue for later migration auditing; they are not counted as
active editor surfaces in Phase A.

## Tested matrix

| Check | Static | Interactive | Result |
| --- | --- | --- | --- |
| Card/group/field inventory | complete | not required | covered |
| User-visible terminology in source/layouts | complete | representative visual confirmation | 3 findings |
| Shared card/control chrome | complete | representative Light/Dark inspection | 1 finding |
| Override-entry semantics | complete | explicit-equals-parent sample | pass |
| Restore removes override | complete | disposable DB + UI | pass |
| Parent refreshes child | resolution/refresh paths inspected | representative preview refresh | pass |
| Reopen/restart persistence | complete | disposable DB restart | pass |
| Direct embedded `···` navigation | complete | Bubble → Surface and Bubble → Avatar | pass |
| Parent-variant icon navigation | complete | Bubble Surface → `Surface Bubble` | pass |
| Recursive nested navigation | complete | Bubble → Avatar → Label | pass |
| Breadcrumb names and return state | complete | direct and nested paths | 1 finding |
| Unsupported/no-op buttons absent | complete static inventory | representative interaction | pass |

## Findings

### P3-A01 — Runtime API rows expose storage-oriented JSON terminology

**Reproduction path:** select a component/module with Runtime Inputs, open
`Runtime Inputs` → `Runtime API`, then inspect input and collection rows.

**Evidence:**

- `RuntimeInputsCollectionEditor.cs` renders
  `Runtime · {ValueKind} · {JsonKey}`.
- Collection headers render `Runtime array · {JsonKey}[]`.
- Item cards render `{collection.JsonKey}[{itemIndex}]`.

**Affected file:**
`spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs`
(around lines 194–218 and 378).

**Violated audit rule:** meaningful labels should be preferred over internal
JSON/schema terminology, and terminology should consistently distinguish Runtime
inputs from implementation storage keys.

**Recommended correction:** keep the API tab if its purpose is diagnostic, but
label the technical key explicitly as `Payload key`/`Collection key`; use the
declared logical input/collection label as the primary text. Avoid presenting a
raw JSON expression as the item subtitle unless a developer-details mode is
active.

**Disposition:** safe mechanical wording/layout change after product confirms
whether `Runtime API` is intentionally developer-facing.

**Proposed check:** a focused UI-string check can reject bare `JsonKey` as the
primary label outside explicitly named diagnostic/developer surfaces.

### P3-A02 — Embedded-component action chrome hardcodes its own amber colors

**Reproduction path:** open any editor card containing an embedded component
field and compare its `···` button/override indication with sibling dictionary
controls and shared editor buttons in Light and Dark mode.

**Evidence:** `DictionaryEmbeddedComponentControl` constructs the `···` button
with local literals `#24D6A638` and `#D6A638`, and separately constructs its
highlight label brush with `#D6A638`.

**Affected file:**
`spikes/desktop-editor-shell/EditorShell/DictionaryEmbeddedComponentControl.cs`
(constructor and `LabelBrush`).

**Violated boundary:** common UI surfaces and override state should use the
shared dictionary/editor chrome; control colors must be centralized, especially
for Light/Dark readability and consistent override communication.

**Recommended correction:** route the embedded action button and override marker
through the same shared editor brush/resource and structure-button treatment used
by other dictionary controls. Preserve amber semantics; remove local color
literals.

**Disposition:** mechanically safe after confirming the intended shared resource.

**Proposed check:** extend the architecture/UI-token check to reject color literals
inside dictionary controls except named diagnostic sentinels.

### P3-A03 — Embedded breadcrumbs repeat indistinguishable slot/class names

**Reproduction path:** Bubble variant → Bubble card → Surface `···`; then Bubble
variant → Avatar card → Avatar `···` → Label `···`.

**Evidence:** the direct breadcrumb renders
`Bubble > Surface · Surface · Surface Bubble`. The nested breadcrumb renders
`Bubble > Avatar · Avatar · Default > Label · Label · Default`.

All required identities are technically present (owner, slot, class and concrete
variant), but identical slot and class names are concatenated without role labels.
The result is difficult to parse and appears accidentally duplicated.

**Affected files:**
`spikes/desktop-editor-shell/EditorShell/EditorBreadcrumbBar.cs` and the embedded
context label construction used by `EditorContentController`.

**Violated audit rule:** breadcrumbs must name owner, slot, component class and
variant clearly; UI language should not require knowledge of the internal order.

**Recommended correction:** retain every identity but make roles explicit, for
example `Bubble › Surface slot › Surface / Surface Bubble`, with the same structure
at every recursive level.

**Disposition:** requires a small product-language decision; implementation is
otherwise mechanical.

**Proposed check:** add a breadcrumb formatting unit test covering direct and
two-level nested paths where slot and component names are equal.

### P3-A04 — Runtime Input counts use placeholder plural grammar

**Reproduction path:** open Runtime Inputs on components with one input and on
components with multiple inputs.

**Evidence:** card subtitles are built with literal `input(s)` and `collection(s)`;
collection cards similarly use `instance(s)`. The UI therefore exposes developer
placeholder grammar rather than correct singular/plural text.

**Affected file:**
`spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs`
(card header and collection header construction).

**Violated audit rule:** singular/plural consistency across cards and empty states.

**Recommended correction:** use the shared count-label routine, or add one in
common UI text helpers, producing `1 input`, `2 inputs`, `1 collection`, and so on.

**Disposition:** safe mechanical correction.

**Proposed check:** unit-test count labels for 0, 1 and 2.

## Audited areas with no finding

- Active component names and categories use current terminology in layout labels.
- Component-facing warning and empty-state strings inspected use `variant`, not
  the persisted `preset` storage term.
- The embedded editor context stores the complete owner-to-nested-slot path rather
  than replacing it with only the leaf slot.
- The parent-variant navigation callback selects a concrete stored reference and
  reports a missing variant instead of silently opening the class.
- Component fields visible in the sampled layouts use logical labels rather than
  their field IDs; field IDs remain metadata.
- Units are supplied through field definitions by the shared dictionary route;
  no Phase A component-specific unit label was found in the inspected surfaces.
- An explicit Bubble → Surface override stored `backgroundAlpha = 1`, equal to the
  selected Surface variant's value, and remained amber as required.
- Restoring that field removed its JSON member, removed the amber label and restore
  affordance, retained the sibling `borderAlpha` override and survived restart.
- The parent-variant icon opened the selected concrete `Surface Bubble` variant and
  selected it in the tree.
- `···` opened the owner-local Surface and Avatar contexts; nested `···` continued
  recursively from Avatar to Label.
- Returning through the owner breadcrumb restored the Bubble editor and preview.
- No unsupported/no-op embedded navigation button was found in the catalog/control
  cross-check.

## Recommended disposition

Phase A was reviewed and approved on 2026-07-12. All four P3 findings are accepted
and deferred to a later fixes phase; none is implemented in this audit commit:

- A01: keep Runtime API as a technical surface, with the logical label as primary
  text and `Payload key` / `Collection key` as secondary information.
- A02: centralize override amber in shared brushes/resources.
- A03: normalize breadcrumbs as `Owner › Slot › Component / Variant`.
- A04: use shared pluralization for zero, one and multiple items.

None requires a data migration and none blocks Phase B analysis. The inactive
`status_bar` and `navigation_bar` layouts remain unclassified residue to investigate
in Phase B; this approval does not authorize their removal or migration.
