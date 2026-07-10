# Schema v1 Candidate Validation

Candidate generated from the committed pre-consolidation desktop database.

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

## Not Yet Active

The desktop app still opens `data/desktop-editor-spike.sqlite`. The candidate
remains parallel until the next phase changes the startup path, field services
and seed behavior to schema v1.
