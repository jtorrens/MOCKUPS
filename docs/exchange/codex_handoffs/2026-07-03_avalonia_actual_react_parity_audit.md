# Avalonia parity audit against current React source

Date: 2026-07-03

This audit supersedes the earlier handoff-derived parity notes where they conflict with the current React source.

## Source of truth checked

- `src/domain/schemas/*.ts`
- `src/domain/fields/*Fields.ts`
- `src/debug-ui/editors/*Fields.tsx`
- `src/debug-ui/editors/GenericFieldDispatcher.tsx`
- `src/debug-ui/components/ProjectTree.tsx`
- `src/domain/resolvers/*`

## Important correction

`RenderPresetSchema` does define `width`, `height`, and `fps`, and `RENDER_PRESET_COLUMN_BINDINGS` binds them to columns.

However, the React editor explicitly hides these columns in `src/debug-ui/editors/RenderPresetFields.tsx`:

```ts
function hiddenRenderPresetField(column: string) {
  return [
    "id",
    "production_id",
    "width",
    "height",
    "fps",
    "color_json",
    "quality_json",
  ].includes(column);
}
```

So for Avalonia parity:

- keep `render_presets.width`, `height`, and `fps` as structural/schema fields;
- do not expose them as normal editable fields in the default editor;
- render size still comes from device metrics in preview/runtime flows;
- shot FPS is separate and follows production default/override behavior.

## Render presets

Schema columns:

- `id`
- `production_id`
- `name`
- `width`
- `height`
- `fps`
- `format`
- `codec_json`
- `color_json`
- `quality_json`
- `export_json`

React editor visible behavior:

- hidden: `id`, `production_id`, `width`, `height`, `fps`, `color_json`, `quality_json`
- visible: `name`, `format`, custom codec selector from `codec_json`, `export_json.ffmpegArgs`
- format options: `mov`, `image`
- codec updates also rewrite `codec_json`, `color_json`, `quality_json`, and `export_json`

Seed/source behavior:

- React seeds seven render presets in `src/persistence/sqlite/seedExampleDataset.ts`.
- The seeded `width`, `height`, and `fps` values are `1`, `1`, and `1`.
- `docs/architecture/05_decisions_log.md` states these are internal SQL placeholders and final values come from shot/render context.
- Avalonia should recreate the seven seed presets, not invent a device-sized preset.

Expected seed presets:

- `MOV ProRes 422 HQ`
- `MOV ProRes 4444 Alpha`
- `MOV H.264 Low`
- `MOV H.264 Medium`
- `MOV H.264 High`
- `PNG Image Sequence`
- `EXR Image Sequence`

Avalonia rule:

- default layout should show only `core.name`, `renderPreset.format`, `renderPreset.codec`, and `renderPreset.export.ffmpegArgs` until a richer render preset editor exists.

## Shots

Schema columns:

- `id`
- `production_id`
- `episode_id`
- `owner_actor_id`
- `name`
- `slug`
- `version`
- `sort_order`
- `duration_frames`
- `fps`
- `render_preset_id`
- `canvas_json`
- `metadata_json`

React editor behavior:

- hidden: `production_id`, `sort_order`, `render_preset_id`, `id`
- `fps`: visible as default/override against production `default_fps`
- `duration_frames`: visible read-only, calculated from screen instances when possible
- `owner_actor_id`: visible; also shows read-only owner device derived from actor default device
- `version`: visible; also shows read-only render name
- `metadata_json.note`: shown as shot notes

Avalonia rule for this phase:

- expose `slug`, `version`, `durationFrames` read-only, `fps`, `ownerActorId`, `ownerDevice` read-only, `renderName` read-only, and notes;
- keep `sortOrder`, `renderPresetId`, raw `canvas`, and raw `metadata` out of the default editor until a dedicated shot/screen phase.

## Apps

Schema columns:

- `id`
- `production_id`
- `name`
- `bundle_key`
- `app_type`
- `config_json`
- `metadata_json`

React editor behavior:

- base columns are bound;
- app-specific UI expands `config_json` and `metadata_json` into wallpaper/icon/note fields.

Avalonia rule for this phase:

- structural columns are correct;
- avoid a bespoke app editor until dictionary value kinds can express wallpaper/icon fields cleanly.

## Exclusions still intentional

- `screen_instances`, `module_instances`, screen payloads, and animation/keyframe structures remain out of this phase.
- Web preview implementation remains separate.
