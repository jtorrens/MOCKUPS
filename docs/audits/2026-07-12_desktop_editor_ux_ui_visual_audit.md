# Desktop Editor UX/UI Visual Audit

Date: 2026-07-12  
Base: `main`, pulled and confirmed current; desktop build `v0.317.46`  
Status: diagnostic audit closed with final decisions; no production code, seed data, database or architecture checks changed

## 1. Executive summary

The editor has a credible foundation: Design and Production are visibly separate, the three-panel layout is consistent, cards make a large schema scanable, and the web preview is clearly framed as the visual source of truth. The strongest opportunity is not a redesign; it is making scope and state explicit at the moments where the user must decide what an action will affect.

The audit found one P1 and five material P2 issues. The P1 is a repeated full native-surface blackout after navigation actions; it recovered, but destroys confidence and presents an accessibility risk. The P2 cluster is conceptual: selection versus activation is encoded mainly by tiny icons; class/variant/embedded-owner identity is distributed; Test Values and persisted defaults share the same editing language; Production does not expose the Episode → Shot → Screen path or transport scopes strongly enough; and empty preview states consume the largest area without offering a contextual next step.

No P0 was found. The audit was completed without changing production data. The recommended direction is a shared context/state layer that can be driven by existing metadata, plus labelled action groups and explicit scope text. Component-specific shell behavior is neither required nor recommended.

## 2. User mental model and principal workflows

### Design

The intended model is: choose a design owner in the tree, choose a concrete variant, edit design properties, supply temporary runtime examples, and inspect the resolved result. The current interface supports the mechanics but makes the user reconstruct four facts from separate regions: owner, active variant, inheritance/override state and persistence scope.

### Production

The intended model is: choose a real Episode, Shot and Screen, edit the selected Screen's runtime content, and navigate time within explicit Screen and Shot boundaries. The current top-level separation from Design is good, but the operational hierarchy is visually weak until the user already knows which glyph or row action opens the next level.

```text
Design: class → concrete variant → fields/slots → temporary Test Values → resolved preview
Production: episode → shot → screen → runtime values → resolved frame
```

## 3. Tested surface and journey matrix

| Surface / journey | Normal state | Selection / expansion | Empty / disabled | Narrow / contrast | Result |
| --- | --- | --- | --- | --- | --- |
| Design top level and Apps | inspected | inspected | empty preview inspected | dark mode, current 3-panel width | findings D02, D05 |
| Component Classes groups | inspected | Components expanded | group/container preview | dark mode | D02, D05 |
| Bubble class and variants | inspected | class expanded; protected Default and Bubble variant; Bubble selected | protected state visible | dark mode | D02, D03, D04 |
| Bubble editor cards and preview | inspected | major cards and Runtime Inputs affordance inspected | no explicit loading state | dark mode | D01, D03, D04 |
| Production overview | inspected | Episode rows and row actions inspected | no selected Screen; blank preview | dark mode | D02, D05, P01 |
| Cross-workspace switch | inspected repeatedly | Design ↔ Production | transient blank/black recomposition | dark mode | D01 |
| Themes, Palette, Icon Themes, Fonts, Devices, Actors, Render Presets | inventory and entry affordances inspected | top-level rows | deep destructive flows not executed | dark mode | D02, T01 |
| Light appearance, keyboard-only modal traversal, hover/focus visuals | not fully evidenced in this pass | — | — | — | implementation-phase validation dependency |

## 4. Strengths worth preserving

- Design and Production use a stable, immediate workspace switch and do not leak `ModuleInstance` vocabulary.
- Navigation, editing and preview remain spatially stable across contexts.
- Cards provide progressive disclosure without exposing the full schema at once.
- The protected Default variant has a distinct lock treatment.
- The preview identifies itself as the web source of truth and keeps Device, Theme, mode and orientation together.
- Selected tree rows use a clear full-row highlight; nested component groups remain understandable.
- Actual preview content is visually convincing and makes isolated component inspection worthwhile.

## 5. Findings ordered by severity

### P1-D01 — Navigation repeatedly blanks the native editor surface

- **Path:** launch cleanly → switch Design/Production or expand/select Component Classes/Bubble/Episode row.
- **Evidence:** during several actions, navigation and editor panels disappeared into black for roughly 2–4 seconds while the WebView content remained visible, then the native UI returned. Captured during the audit, separate from the steady screenshots below.
- **Expectation / observed:** a navigation action should preserve the existing surface or show a bounded busy state; instead most of the application visually disappears.
- **Impact:** users may interpret it as a crash or lost selection; repeated high-contrast flashing is an accessibility concern and makes expert navigation feel unsafe.
- **Users:** all. **Frequency:** repeated in this session. **Class:** verified usability/functional problem.
- **Recommendation:** preserve the previous composed surface until the next native state is ready; if work exceeds a short threshold, show a panel-local loading indicator and retain context. Never replace unrelated panels with black.
- **Acceptance:** 20 consecutive workspace, tree-expansion and selection actions produce no full-surface black frame; any operation over 200 ms keeps the previous UI and exposes a labelled busy state; focus and selection survive.
- **Scope:** shared shell/render-lifecycle change. Diagnose separately before visual implementation.

### P2-D02 — Tree actions do not reliably predict selection, expansion, navigation or creation

- **Path:** Design → Component Classes → Components → Bubble; Production → Episodes → Episode 1.
- **Evidence:** [Design overview](assets/2026-07-12_desktop_editor_ux_ui_visual_audit/01-design-overview.png) and [Production overview](assets/2026-07-12_desktop_editor_ux_ui_visual_audit/03-production-overview.png).
- **Expectation / observed:** users expect clicking a row to select, a chevron to expand and a labelled action to open another context. Rows combine `+`, lock, duplicate-like, external/open and chevron glyphs in a small cluster; their relative meaning changes with hierarchy.
- **Impact:** likely wrong turns and slower learning; touch targets and tooltip dependence are especially costly for occasional users.
- **Users:** new, occasional; experts pay a smaller recurring cost. **Frequency:** every navigation session. **Class:** verified consistency/usability problem.
- **Recommendation:** adopt the balanced hybrid navigation model: cards represent first-level functional sections and hierarchical rows represent apps, components, modules, variants, Episodes, Shots, Screens and other navigable objects. Rows use a fixed left chevron/icon column, 16 px indentation per depth level, object-specific metadata only, state beside the name and actions at the right. Row click selects; the chevron expands/collapses; `Open` or `Enter` enters a different editing context; `+` creates the named child/object; `…` contains less-frequent or destructive actions. Frequent actions may appear on hover, selection or keyboard focus, but their column reserves space so content never shifts. Protected variants remain selectable and openable; only forbidden mutations are disabled. Every `+` exposes the exact created object in its tooltip and accessible name.
- **Acceptance:** without prior instruction, 4/5 test users correctly predict row click, expand and open behavior; the panel shows approximately twice the current object count without losing readability; every compact action has at least a 36×36 target, tooltip and keyboard equivalent; the same glyph never changes semantic role; hover is never the sole path to an action; protected rows remain selectable/openable; normal, selected, focused and disabled-action states remain distinct without relying on color alone.
- **Scope:** shared metadata-driven hierarchical-row primitive used by Design and Production navigation. The primitive knows no concrete record/component type. **Final decision:** accepted as the balanced hybrid model; the earlier five-action card-row composition is superseded.

### P2-D03 — Owner, variant, inheritance and save scope are split across the screen

- **Path:** Design → Component Classes → Components → Bubble → Bubble variant.
- **Evidence:** [current Bubble variant](assets/2026-07-12_desktop_editor_ux_ui_visual_audit/02-design-bubble-variant.png); [revised compact context strip](assets/2026-07-12_desktop_editor_ux_ui_visual_audit/07-revision-d03-compact-context-strip.svg).
- **Expectation / observed:** before editing, users need to know “what am I editing, what is it based on, and where will Save write?” The tree, small `Variant: Bubble` subtitle, amber title, lock and Restore/Save actions each carry one piece.
- **Impact:** increases risk of modifying the wrong layer and makes amber do too much work.
- **Users:** all; greatest risk to occasional users. **Frequency:** every variant/embedded edit. **Class:** verified comprehension problem.
- **Recommendation:** extend the existing breadcrumb with a compact persistent strip rather than adding a second header. The shared primitive is populated exclusively from generic metadata. The breadcrumb continues to own location; the strip adds active concrete identity, override count and dirty/saved state, expanding scoped Save/Revert actions only when relevant. Use explicit typed labels such as `Componente: Bubble · Variante: Bubble`; never ambiguous shorthand such as `Bubble / Bubble`. Normalize embedded paths as `Owner › Slot › Component / Variant` without repeating the same owner or title twice.
- **Acceptance:** the current owner and concrete variant remain visible while scrolling; dirty and override are distinct; Save and Revert labels name their scope; nested navigation can be reversed in one action.
- **Scope:** shared breadcrumb/context-strip primitive plus metadata; no component-specific shell branches. **Final decision:** accepted.

### P2-D04 — Test Values and “Save as defaults” are too easy to read as one persistence model

- **Path:** Design → Bubble variant → Runtime Inputs → Test Values / preview actions / Save as defaults.
- **Evidence:** current Bubble screen above and [revised Test Values proposal](assets/2026-07-12_desktop_editor_ux_ui_visual_audit/08-revision-d04-test-values.svg).
- **Expectation / observed:** sample runtime content should feel temporary and separate from variant configuration. The same card/control language is used and the consequential action is phrased as a convenience action rather than a scope conversion.
- **Impact:** users can believe preview samples are already persisted, or save temporary examples as defaults unintentionally.
- **Users:** new and occasional. **Frequency:** recurring in component work. **Class:** verified mental-model problem.
- **Recommendation:** label the region exactly `Valores de prueba · temporales`, with supporting text that it affects only the preview. Use `Restablecer valores de prueba` when clearing/reverting temporary edits, or `Recargar predeterminados` only when the action actually reloads defaults. Label the conversion action `Guardar como valores predeterminados…`; disable it when no differences exist and explain why in a tooltip. Confirmation names the destination variant and affected fields. Playback actions remain temporary and expose running/reset states.
- **Acceptance:** temporary values survive/revert exactly as documented; closing or switching context warns only when appropriate; confirmation lists destination and changed keys; playback cannot be confused with a persistent switch.
- **Scope:** shared runtime-input/state surface and product wording decision. **Final decision:** accepted.

### P2-P01 — Production lacks a strong Episode → Shot → Screen context and transport grammar

- **Path:** Production → Episode 1 → attempt to understand or enter Shot/Screen context.
- **Evidence:** [current Production overview](assets/2026-07-12_desktop_editor_ux_ui_visual_audit/03-production-overview.png); [revised overlay on the current Production interface](assets/2026-07-12_desktop_editor_ux_ui_visual_audit/09-revision-p01-production-overlay.svg).
- **Expectation / observed:** Production users expect a visible hierarchy and controls whose labels reveal whether they cross a frame, Screen or Shot boundary. The overview initially emphasizes Episode scalar editing and small row icons; the selected operational scope is not persistent in the editor/preview header.
- **Impact:** slower recovery from wrong context and high risk of using the wrong previous/next command once transport is visible.
- **Users:** all Production users. **Frequency:** every shot-editing session. **Class:** verified comprehension/efficiency problem.
- **Recommendation:** add only a persistent Production breadcrumb and a read-only inherited-context strip for Device, Theme and display mode. Preserve the implemented transport exactly as the interaction baseline: do not replace or redesign its compact controls, separators, frame units or `Frame`/`Screen`/`Shot` scopes.
- **Acceptance:** current Episode/Shot/Screen is visible in editor and preview; every boundary command includes scope in accessible name and tooltip; inherited values cannot be mistaken for editable Design selectors; empty Shots present a clear creation/selection route.
- **Scope:** shared Production breadcrumb and inherited-context presentation. **Final decision:** partially accepted; transport redesign is explicitly out of scope.

### P2-D05 — The largest visual region becomes an unhelpful blank preview

- **Path:** select App/Episode/group/container rather than a renderable component/Screen.
- **Evidence:** both overview screenshots show a phone canvas with a tiny centered message while most of the preview panel remains empty.
- **Expectation / observed:** a non-renderable selection should explain why there is no preview and offer the next useful action. Instead the UI preserves a full device canvas with low-salience microcopy.
- **Impact:** wastes space and makes selection appear broken; it also competes with the editor even though no result exists.
- **Users:** all. **Frequency:** common during navigation. **Class:** reasonable but material improvement.
- **Recommendation:** use three distinct contextual states: non-renderable object (selected object and reason; show `Ver elementos renderizables` only when a deterministic destination/action exists), loading (retain the previous resolved preview underneath a local progress layer and identify the pending context/frame) and error (plain-language cause, retained editor state, retry and expandable details). Retain device chrome only when a renderable target exists.
- **Acceptance:** every non-renderable selection has a specific empty state; no instruction is smaller than normal secondary text; errors, loading and “not renderable” are visually distinct.
- **Scope:** shared preview state system. **Final decision:** accepted. Loading must not introduce a global spinner or conceal D01.

### P3-V01 — Secondary text and icon contrast is marginal at realistic scale

- **Path:** scan navigation descriptions, card subtitles, preview labels and tiny tree action icons in dark mode.
- **Impact:** reduces scan speed and increases reliance on proximity/color; likely worse on lower-quality displays.
- **Recommendation:** raise secondary text contrast, use a minimum 12 px equivalent for persistent explanatory copy, and verify icon states against WCAG contrast expectations.
- **Acceptance:** text and interactive indicators meet the agreed AA targets in Light/Dark; 200% zoom and narrow panels do not clip state labels.
- **Scope:** joint V01/T01 pass across shared tokens and controls. **Final decision:** accepted with measured token/contrast verification.

### P3-T01 — Terminology is mostly sound, but action words need scope

- **Observed:** `Design`, `Production`, `Episode`, `Shot`, `Screen`, `Runtime Inputs`, `Test Values` and `Web source of truth` form a coherent vocabulary. `Restore…`, `Save variant`, `Preview`, `Browse`, `Source`, `Pick`, `+` and external/open glyphs are ambiguous without the affected object.
- **Recommendation:** apply noun-scoped visible labels where space permits (`Revert Bubble variant…`, `Open parent variant`, `Pick icon`, `Browse media`, `Use source path`). In compact controls, retain the icon and provide a contextual tooltip/accessibility name that includes object and result. Do not force verbose labels into every toolbar.
- **Scope:** joint V01/T01 pass across shared tokens and controls. **Final decision:** accepted under the visible-label/compact-tooltip rule.

## 6. Annotated evidence and visual proposals

The revised SVG proposals are medium-fidelity compositions using the current dark cards, amber accent and actual three-panel layout. They are not canonical redesigns. Superseded first-pass proposals remain as audit history but are not the recommended treatment.

1. [D02 — final balanced hybrid navigation](assets/2026-07-12_desktop_editor_ux_ui_visual_audit/11-final-d02-hybrid-navigation.svg): cards identify first-level sections; compact hierarchical rows identify objects, variants and Production entities. The previous `06` proposal remains only as superseded audit history.
2. [D03 — compact context strip](assets/2026-07-12_desktop_editor_ux_ui_visual_audit/07-revision-d03-compact-context-strip.svg): integrated with the existing breadcrumb, including saved/dirty and reduced-width states without duplicating owner/title.
3. [D04 — Test Values](assets/2026-07-12_desktop_editor_ux_ui_visual_audit/08-revision-d04-test-values.svg): `Valores de prueba · temporales`, explicit conversion to defaults, confirmation and disabled rule.
4. [P01 — Production overlay](assets/2026-07-12_desktop_editor_ux_ui_visual_audit/09-revision-p01-production-overlay.svg): annotation over the current Production capture; preserves implemented transport, frames and Frame/Screen/Shot scopes.
5. [D05 — preview states](assets/2026-07-12_desktop_editor_ux_ui_visual_audit/10-revision-d05-preview-states.svg): non-renderable, loading and error states plus reduced-width behavior.

The current screenshots retain enough surrounding UI to judge hierarchy. No production assets were modified.

## 7. Terminology and metaphor review

| Metaphor / term | Assessment | Recommendation |
| --- | --- | --- |
| Design / Production | strong, preserve | keep globally visible and mutually exclusive |
| Amber | overloaded across workspace accent, selection and override | reserve amber for actionable state/override; pair with text/icon |
| Parent edit icon vs `…` override | not self-explanatory | label parent navigation and call overflow “Overrides” when that is its purpose |
| `+` | ambiguous object and insertion position | expose object name in label/menu; collections use “Add item” |
| Lock | understandable for protected Default, but tiny | retain with `Protected default` accessible label |
| History/context dropdown | visually similar to ordinary selector | prefix with `Recent:` or use a back/history affordance with destination preview |
| Cards/subordinate cards | sound | strengthen indentation/path label; avoid equal chrome for owner and child |
| Browse / Source / Pick | overlapping verbs | Browse = filesystem, Pick = catalog, Source = read-only provenance |
| Test Values / Save as defaults | conceptually risky | explicitly mark temporary and treat save as a confirmed conversion |
| Play/action vs switch | currently too similar in compact areas | actions show momentary/running/reset state; switches show persistent on/off |
| Shot / Screen transport | must be explicit | group and label commands by boundary, not by icon alone |

## 8. Prioritized recommendations

### Quick wins

1. Scope action labels and tooltips; increase hit areas and secondary contrast.
2. Replace generic blank preview with contextual empty/loading/error states.
3. Add persistent “Temporary preview content” language to Test Values.
4. Normalize breadcrumbs and zero/one/many labels, aligning with accepted Phase A findings.

### Shared structural improvements

1. Diagnose and eliminate the native-surface blackout before animation increases update frequency.
2. Introduce the metadata-driven context header for owner, concrete variant, overrides and dirty/saved state.
3. Introduce the balanced hybrid navigation model: section cards containing compact hierarchical object rows with shared select, expand, open, add and overflow semantics.
4. Add the Production breadcrumb, inherited-context strip and labelled transport groups.

### Future explorations

1. Expert command/search palette for direct navigation and actions.
2. Optional compact density once focus targets and labels remain accessible.
3. Reference-comparison layouts that use the preview area more economically at wide widths.

## 9. Dependencies and implementation risks

- The context header must consume generic layout/selection metadata; it must not introduce editor-specific logic into `MainWindow`.
- Editable state remains dictionary-backed. New labels/badges may describe fields but must not create a parallel edit path.
- Production context cannot reuse Design selectors as editable controls; Device/Theme/mode are inherited/read-only there.
- Preview loading/error changes must stay generic and must not add component knowledge across resolver/renderable boundaries.
- D01 needs lifecycle/performance diagnosis before animation work; hiding it with an unconditional spinner would not meet acceptance.
- Accessibility requires a dedicated keyboard and screen-reader pass on both macOS and Windows; this audit did not claim complete assistive-technology coverage.
- Light mode and narrow-width proposals should be visually validated before implementation acceptance.

## 10. Inspected areas with no finding

- The global Design/Production distinction is clear and consistently placed.
- No user-facing `ModuleInstance` term appeared in inspected Production surfaces.
- Three-panel resizing affordance exists and panel ownership remains understandable at the tested width.
- The Bubble preview matched the selected component context and used actual media/text content.
- Protected Default and selected non-default variant are distinguishable in the tree.
- Device, Theme, mode and orientation are grouped coherently in Design preview setup.
- Top-level Theme, Device, Actor, Icon Theme, Font and palette entries use the same shared card language; no isolated custom chrome was observed.
- No destructive operation was required to complete the audited journeys.

## 11. Proposed implementation sequence (no implementation authorized)

1. Diagnose D01 independently and establish a no-black-frame invariant before animation work.
2. Implement D05 contextual preview states, preserving the previous preview during local loading.
3. Implement D04 temporary Test Values and conversion-to-defaults semantics.
4. Implement D03 as a metadata-driven shared breadcrumb/context strip.
5. Implement D02 progressively: create the shared hierarchical row, validate it in Component Classes, then migrate Apps/resources and Production navigation.
6. Implement only the accepted P01 breadcrumb and inherited read-only context; leave transport unchanged.
7. Complete the joint V01/T01 token, contrast, visible-label and contextual-tooltip pass.

## Decision table

| ID | Summary | Severity | Type | Proposed disposition |
| --- | --- | --- | --- | --- |
| D01 | Native editor surface blacks out after navigation | P1 | shared lifecycle defect | **final: accepted** for independent diagnosis before animation; no global-spinner workaround |
| D02 | Tree hierarchy and action grammar are ambiguous and too card-heavy | P2 | shared-system change | **final: balanced hybrid accepted**; cards for sections, hierarchical rows for objects, protected rows remain selectable/openable |
| D03 | Owner, variant and save scope are distributed | P2 | shared-system change | **final: accepted**; metadata-driven shared strip with typed identities |
| D04 | Test Values vs defaults is insufficiently separated | P2 | product + shared UI | **final: accepted**; action wording follows actual semantics and Save disables without differences |
| P01 | Production context and transport scopes are weak | P2 | product + shared UI | **final: partially accepted**; breadcrumb/context only, transport unchanged |
| D05 | Blank preview lacks contextual next action | P2 | quick/shared UI | **final: accepted**; previous preview retained under local loading layer |
| V01 | Secondary contrast and tiny actions | P3 | shared token/control pass | **final: accepted jointly with T01** |
| T01 | Several actions lack noun/scope | P3 | shared token/control pass | **final: accepted jointly with V01** |

All rows above are final for this audit. Any later change requires an explicit
product decision or a new audit iteration.

## Implementation handoff

The accepted work is divided into independently verifiable phases and commits in:

[`2026-07-12_desktop_editor_ux_ui_implementation_handoff.md`](../exchange/codex_handoffs/2026-07-12_desktop_editor_ux_ui_implementation_handoff.md)

The required order is D01 diagnosis, D05, D04, D03, D02, the accepted part of
P01, then the joint V01/T01 pass. This audit does not authorize implementation by
itself; a future thread must receive an explicit execution instruction.
