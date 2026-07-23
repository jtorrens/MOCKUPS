# Desktop editor accessibility token and navigation icon inventory

Date: 2026-07-13

Scope: accepted V01/T01 pass for the shared desktop editor shell. This report
does not change component contracts, preview behavior, Production transport,
animation, seed data or assets.

## Contrast method and target

Ratios use WCAG 2 relative luminance. Persistent normal-size text targets
4.5:1. Focus indicators target 3:1. Tree connectors are decorative and use a
subdued scrollbar gray by explicit visual direction. The measured reference
surfaces are the current dark content/card surface `#2C2E33` and light content
surface `#FFFFFF`.

The Suki theme remains the base theme. The editor-owned shared tokens now live
in `Common/EditorUiVisuals.cs`; controls no longer need to repeat local colors
for these states.

| Shared role | Dark before | Dark after | Light before | Light after | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| Primary text | 12.40:1 | 12.40:1 | 14.68:1 | 14.68:1 | unchanged, AA |
| Secondary text | 7.42:1 | 8.73:1 | 4.97:1 | 5.90:1 | raised, AA |
| Disabled row text | 3.52:1 | 5.65:1 | 2.47:1 | 4.66:1 | raised to AA |
| Tree connectors | 1.29:1 | 1.78:1 | 1.51:1 | 1.89:1 | deliberately subdued scrollbar gray |
| Selected text/background | — | 6.52:1 | — | 13.16:1 | explicit, AA |
| Focus ring/background | implicit | 5.34:1 | implicit | 5.55:1 | explicit, above 3:1 |

Persistent tree descriptions now use 12 px instead of 10 px. Disabled rows
also expose the visible word `Unavailable`, set the actual disabled semantic
state, and keep the reason as accessible help text. Selection has a filled
background, hover has a separate surface, and keyboard focus has a 2 px ring;
none depends on color alone.

Usage circles remain shape-coded: unused leaves are empty gray outlines, while
used leaves are solid high-intensity amber without an outline.

## Shared action language and accessible names

- Tree actions name their target: `Rename {name}`, `Duplicate {name}`, `Delete
  {name}`, `Lock {name} variant editing` and `Unlock {name} variant editing`.
- Expand/collapse controls name their target, for example `Expand Episode 1`.
- Breadcrumb ancestors announce `Open {name}`.
- Compact restore controls announce the affected field and inherited result.
- Path, color and icon dictionary controls use noun-scoped visible labels:
  `Browse file…`, `Browse folder…`, `Pick color`, `Pick icon…` and `Pick
  icons…`.
- Compact component-variant and override controls expose contextual tooltips and
  automation names.

## Navigation icon inventory

Levels below are relative to the content inside each first-level section card.
Section cards always have their own section icon. The generic rule for object
rows is: a row with children has a semantic icon; the final leaf level has no
icon and uses only its usage circle. Palette leaves are the sole visual-value
exception: they show a color swatch, not a semantic icon.

### Section-card labels with icons

| Workspace | Section label | Section icon meaning |
| --- | --- | --- |
| Design | Apps | apps |
| Design | Component Classes | component classes |
| Design | Themes | theme |
| Design | Palette Colors | palette/color |
| Design | Icon Themes | icon theme |
| Design | Production Fonts | typography |
| Design | Devices | device |
| Production | Episodes | episode |
| Production | Actors | actor |
| Production | Render Presets | render output |

### Tree levels with icons

| Section | Tree level | Labels/types that have an icon | Next/final level |
| --- | ---: | --- | --- |
| Apps | 0 | every App record | Module at level 1 has no icon |
| Component Classes | 0 | `Components`, `Atoms`, `System` | component classes at level 1 |
| Component Classes | 1 | `Audio`, `Avatar`, `Bubble`, `Button`, `Cursor`, `Icon Bar`, `Icon Row`, `Keyboard`, `Label`, `Media`, `Navigation Bar`, `Status Bar`, `Surface`, `Text Box`, `Text Input Bar` | variant at level 2 has no icon |
| Episodes | 0 | every Episode record | Shot at level 1 |
| Episodes | 1 | every Shot record | Screen at level 2 has no icon |

### Leaf-only sections without row icons

The child rows in `Themes`, `Icon Themes`, `Production Fonts`, `Devices`,
`Actors` and `Render Presets` are final levels, so they have no row icon. The
same applies to Modules, component variants and Screens. `Palette Colors` uses
the stored color swatch in the leaf position and does not add a second icon.

This inventory reflects the shared renderer and the component classes present
in the desktop database on 2026-07-13. New tree types inherit the same generic
children-versus-leaf rule; component-specific icon selection remains confined
to navigation metadata, not the editor or preview bridge.
