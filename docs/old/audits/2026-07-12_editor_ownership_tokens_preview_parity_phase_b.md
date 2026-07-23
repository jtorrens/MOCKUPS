# Desktop Editor Ownership, Tokens and Preview Parity Audit — Phase B

Date: 2026-07-12

Base commit: `0fbeebe6` (`docs: record editor audit phase A`)

Status: reviewed and approved; no fixes applied

## Executive summary

Phase B traced persisted configuration, runtime inputs, Theme references and
representative preview values across the active desktop component/module route.
The active ownership model is broadly consistent and the architecture guard
protects the principal resolver/renderable boundaries.

The audit found one P1 contract violation and one P2 migration-residue issue:

- Label and Avatar still persist raw numeric visual gaps instead of
  `theme.spacing.*` tokens.
- Four unreachable legacy Status/Navigation layout records remain committed after
  the schema-v1 migration removed their tables and active editor route.

No component-specific bridge/renderer leak, SVG-path icon persistence, retired
radius token, ambiguous short component reference, or Module Instance ownership
leak was found in the active committed payloads.

## Scope and evidence

- SQLite tables, component variants, module configuration, module instances,
  Theme references and all `editor_layouts` records.
- Desktop field catalogs, embedded-slot catalogs, payload factory and Theme field
  value route.
- Active component/module contracts, resolvers, renderables, manifests and
  registries.
- Canonical behavior sheets under `docs/architecture/component_behavior/`.
- Existing executable architecture checks, including payload, manifest, import,
  runtime-collection, reference and retired-vocabulary assertions.

## Ownership and contract matrix

| Owner | Persisted design/configuration | Runtime/calculated input | Parent/Theme authority |
| --- | --- | --- | --- |
| Surface | color/token, alpha, border, radius, shadow, relief, tail | parent box/size | Theme resolves tokens |
| Cursor | color token, width, fade | height and elapsed motion | Theme motion/color |
| Text Box | Surface/Cursor variants, padding, typography, alignment | size, text, placeholder, icon-row content | parent fixes/binds inputs |
| Icon Row | stable Button item variants/settings | item content/state/action | parent owns outer placement |
| Icon Bar | row variants and state composition | active state and row content | parent owns outer frame |
| Label | Surface variant, padding, typography, text/subtext gap | text, subtext and size | owning resolver supplies calculated text |
| Avatar | Label variant, visual size/radius | actor, label and subtitle | parent supplies actor/runtime content |
| Button | state Surface/Label variants, sizing, padding, motion token | content mode, icon/text, state and Push | parent may fix runtime bindings |
| Audio | Surface/Avatar/Button variants and waveform design | playback, duration, actor and state | Bubble/parent supplies media runtime |
| Status Bar | visual variant and stable item collection | resolved status values | active Theme selects concrete variant |
| Navigation Bar | visual variant and stable item collection | resolved navigation state | active Theme selects concrete variant |
| Text Input Bar | Surface/Text Box/Icon Bar variants | text and typing state | Conversation supplies revealed text |
| Keyboard | layout and Icon Bar variants | revealed grapheme/motion | Conversation supplies shared time/text |
| Media | Surface/Icon Bar variants and transition design | source, viewport, playback/full-screen state | Bubble/parent supplies media runtime |
| Bubble | child variants, palette pairs, padding, status appearance | message, actor, status, media and write-on | Conversation supplies message/shared frame |
| Conversation | static composition, selected child variants, header Icon Rows, layout policy | messages, shared frame, runtime actor/subtitle/actions | Theme alone supplies system-bar variants |
| Module Instance | duration/transition plus declared instance payload | shot/app/module context | must not own static Conversation composition |

## Preview parity matrix

| Value/contract | Isolated | Embedded | Module design | Module Instance | Result |
| --- | --- | --- | --- | --- | --- |
| Concrete component variant reference | component payload | parent slot merge | Conversation selected variants | same module config | pass |
| Bubble message text/status | Test Values inputs | Bubble child contract | `messages[]` item | instance `content_json` | pass |
| Avatar actor/subtitle | Test Values record input | parent-provided actor data | header/message actor | shot/message actor | pass |
| Header Icon Rows | isolated row inputs | Conversation-owned fixed composition | module config | inherited from module | pass |
| Status/Navigation variants | isolated component variant | Theme reference | active Theme | instance Theme context | pass |
| Shared animation time | declared action input | parent frame inputs | Conversation frame | shot frame/FPS | pass |
| Icon token resolution | semantic token | unchanged token contract | active Theme Icon Set | active Theme Icon Set | pass |
| Label/Avatar visual gap | raw numeric config | raw numeric merge | same numeric contract | same numeric contract | **B01** |

## Findings

### P1-B01 — Label and Avatar persist raw numeric visual gaps

**Reproduction/data path:** inspect the Label and Avatar component variants in
`component_classes.metadata_json`, then trace Label through
`ComponentClassFieldCatalog` → resolver → renderable.

**Evidence:**

- Label variants persist `label.textGap = 10`.
- `component.label.textGap` is an editable `ValueKind.Decimal` with numeric range
  and step metadata.
- Avatar variants persist `avatar.labelSlot.gap = 4`.
- Avatar label overrides persist `label.textGap = 0`.
- `labelComponentResolver` requires a number and the renderable multiplies it by
  device scale directly.

**Affected files/data:**

- `data/desktop-editor-spike.sqlite`, Label and Avatar component variant configs;
- `spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs`;
- `spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassDefaults.cs`;
- `src/desktop-preview/labelComponentContract.ts`;
- `src/desktop-preview/labelComponentResolver.ts`;
- `src/desktop-preview/labelComponentRenderable.ts`.

**Violated boundary:** all reusable component padding and gap values must use
`theme.spacing.*`; raw numeric spacing fields are explicitly forbidden.

**Recommended correction:** define the Label text/subtext gap as a spacing-token
field and resolve it through the generic Theme spacing path. Define the Avatar
label-slot gap through the same token vocabulary or remove it if placement fully
owns the separation. Migrate every persisted variant/override and committed DB
record in one explicit migration; do not keep numeric compatibility coercion.

**Disposition:** requires an explicit data migration and preview-parity update;
not safe as a UI-only mechanical fix.

**Proposed check:** scan committed component configs and field catalogs so keys
representing visual padding/gap accept only `theme.spacing.*` or declared
spacing-token pairs.

### P2-B02 — Retired Status/Navigation editor layouts remain committed

**Reproduction/data path:** query `editor_layouts` for `status_bar`,
`navigation_bar`, `navigation.status_bars` and `navigation.navigation_bars`.

**Evidence:** all four records remain in the committed database. The first pair
contains fields from the retired record model (`statusBar.family`,
`navigationBar.family`, etc.); the navigation pair represents tree roots that no
longer exist. No physical `status_bars` or `navigation_bars` tables/records exist,
and active records correctly use `component.status_bar` and
`component.navigation_bar`.

**Affected data:** `data/desktop-editor-spike.sqlite`, table `editor_layouts`.

**Violated boundary:** schema/vocabulary changes require one explicit migration
that updates all references and removes retired identifiers; compatibility residue
must not remain in seeds, layouts or the committed database.

**Recommended correction:** confirm no external layout consumer depends on these
four IDs, delete them in the next explicit data migration, and add a committed-DB
assertion that rejects them. Do not alter the active component layouts or Theme
component-variant references.

**Disposition:** deletion is mechanically simple but requires migration review
because it changes committed parity data.

**Proposed check:** extend `checkDesktopPreviewArchitecture.ts` to reject these
four `editor_layouts.record_class_id` values, not only legacy tables/code paths.

## Audited areas with no finding

- Active Theme columns reference full concrete Status/Navigation component variant
  IDs or an intentionally empty optional reference; they do not reference removed
  tables.
- Conversation owns fixed header Icon Row composition; Module Instances do not
  duplicate it as runtime input.
- Avatar subtitle remains a declared runtime value and reaches the owned Label
  contract through the generic input route.
- Calculated/final text is supplied by owning resolvers; Label and the generic
  renderer do not inspect calculation sources.
- Icon Row items retain stable IDs, Button variant, content mode, state and local
  overrides independently.
- Active icon consumers persist semantic tokens; no SVG path/file was found in an
  icon-token field.
- Active padding fields other than B01 use Theme spacing tokens/pairs.
- Canonical radius vocabulary is present; retired radius token values were absent
  from committed component/module/Theme data.
- Production typography uses explicit Theme/theme-system roles and declared emoji
  fonts; no host-system font fallback was found in the active route.
- Component references in active configs use full
  `componentClassId::preset::presetId` values; no short reference was found.
- Runtime/action units follow frames/seconds/milliseconds by domain.
- Final renderables use the generic primitive allowlist and existing architecture
  checks found no component knowledge in common helpers or the renderer.

## Known limitations not reclassified as new findings

- Cold video preview may present late/blank frames; it is already recorded in the
  canonical behavior open-items list and preview-pipeline performance section.
- General text-keyframe interpolation is intentionally deferred before the future
  animation editor exposes that contract.

## Recommended disposition

Phase B was reviewed and approved on 2026-07-12. B01 remains accepted as P1 and
B02 as P2. Neither fix belongs in this audit commit; implementation is deferred to
a dedicated fix/migration phase.

Before selecting the replacement tokens for B01, document the semantic mapping
between every current numeric gap and the actual `theme.spacing.*` values in both
committed Themes. The later migration must update defaults, every variant, every
override and the committed database in one change, with no numeric coercion or
fallback.

B02 must be removed through an explicit data migration. The same future change
must extend architecture checks against the committed database so all four retired
layout IDs fail validation if reintroduced.
