# Embedded component composition contract

Date: 2026-07-05

This document defines the desktop editor and web preview contract for recursive
component composition.

The goal is a design system made from reusable component classes. Parent
components may embed child components without copying the child's fields, UI or
render logic.

## Core Model

An embedded component is a slot, not a duplicated class.

```text
parent component class
  -> embedded slot metadata
  -> child component base class
  -> slot-local overrides
  -> component resolver
  -> web bridge
  -> web renderer
```

The parent owns:

- slot visibility and placement fields;
- slot-level layout such as position and gap;
- an `overrides` object containing only deliberate local edits.

The embedded child owns:

- its normal field catalog;
- its normal dictionary controls;
- its normal resolver contract;
- its normal bridge/render path.

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

## Editor Boundary

The Avalonia editor edits structured data. It does not render final visuals.

Allowed editor responsibilities:

- list embedded slots from `EmbeddedComponentSlotCatalog`;
- open the child component editor in an embedded context;
- show inherited values from the child base class;
- save local edits into the parent slot's `overrides`;
- show visual affordances for embedded context and override state.

Disallowed editor responsibilities:

- copying child component fields into the parent catalog;
- creating a manual UI for a child field outside the dictionary route;
- resolving theme colors, palette colors, SVGs or device pixels for preview;
- generating final renderable geometry for the web renderer;
- adding component-specific logic to `MainWindow` beyond generic embedded-editor
  hosting.

The dictionary route remains mandatory:

```text
embedded editor context
  -> child FieldDefinition
  -> ValueKind
  -> dictionary control
  -> generic commit path
  -> parent slot overrides
```

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

## Bridge Boundary

The web bridge converts validated component contracts into final web renderable
nodes.

Allowed bridge responsibilities:

- resolve theme tokens for the selected theme mode;
- resolve all available color variants for mode switching;
- resolve palette colors and neutral tint;
- apply device design-to-pixel scale;
- resolve icon/SVG file references into web-consumable assets;
- compose renderable nodes with explicit boxes and styles.

Disallowed bridge responsibilities:

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

Current storage shape:

```json
{
  "avatar": {
    "labelSlot": {
      "showLabel": true,
      "showSubtext": true,
      "position": "right",
      "gap": 4,
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
  -> RenderableReactAdapter
```

This is the reference path for future embedded components.

## Migration Rule

When a composite component is migrated, migrate its owned subcomponents through
this same route. Do not partially migrate the parent while leaving child
geometry, colors, text, icons or media on a legacy preview branch.

For message bubbles, the bubble and its owned avatar, labels, media, audio,
video, icon button, tail/chrome and status subcomponents must move together or
remain explicitly unsupported in component-class preview.
