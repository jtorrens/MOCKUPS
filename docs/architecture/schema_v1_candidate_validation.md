# Schema v1 Cutover Validation Record

Historical validation record for the schema v1 cutover. The candidate was
generated from the committed pre-consolidation desktop database and promoted to
the active desktop database by `e37312c2`.

```text
Source: data/desktop-editor-spike.sqlite
Candidate: data/desktop-editor-spike.schema-v1.sqlite
Schema version: 1
```

## Passed

- Candidate contains exactly the 15 canonical desktop tables.
- Candidate does not contain `screen_instances`, `status_bars` or
  `navigation_bars`.
- Every canonical table preserves source row counts and stable ids.
- All JSON columns in the candidate pass SQLite `json_valid`.
- SQLite foreign-key checks and explicit actor/device/theme/module references
  pass.
- Icon-theme and production-font directories exist under the candidate project
  media root.
- `projects.media_root` was normalized from its Mac-specific absolute path to
  `assets/FOQN_S2/`.
- All current shots inherited the project FPS, so their `fps_override` values
  are null in the candidate.

## Row Counts

| Table | Rows |
| --- | ---: |
| projects | 1 |
| episodes | 3 |
| shots | 5 |
| apps | 2 |
| modules | 1 |
| module_instances | 1 |
| palette_colors | 24 |
| devices | 6 |
| actors | 3 |
| production_fonts | 4 |
| icon_themes | 6 |
| render_presets | 7 |
| component_classes | 15 |
| themes | 2 |
| editor_layouts | 48 |

## Source Data Repair Found By Validation

The source Theme editor layout had a trailing comma and was invalid JSON. The
layout source was corrected and the active DB was refreshed from that canonical
layout before candidate creation. No authored project or component data changed.

## Active Result

The desktop app opens `data/desktop-editor-spike.sqlite`, which is now schema
v1. Startup validates the canonical table set, `PRAGMA user_version = 1`, and
the `shots.fps_override` shape before opening the editor; it does not apply
historical migrations.

`data/desktop-editor-spike.schema-v1.sqlite` remains versioned only as the
validated cutover artifact. It is not a second active runtime database.
