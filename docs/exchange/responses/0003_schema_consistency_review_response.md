# Codex Response 0003 — Schema consistency review

## Summary

Reviewed the initial schema and all six examples against accepted decisions D001–D009. Corrected raw JSON column naming, completed the minimal screen-instance shape, aligned references, made frame-coordinate semantics consistent, and clarified stable conversation participant relationships.

## Files changed

- `docs/architecture/04_shot_builder.md`
- `docs/architecture/07_initial_data_schema.md`
- `docs/examples/production_minimal.json`
- `docs/examples/shot_lock_to_chat.json`
- `docs/examples/theme_ios_light.json`
- `docs/examples/device_iphone_generic.json`
- `docs/examples/resolved_props_chat_screen.json`
- `docs/examples/resolved_props_message_bubble.json`
- `PROJECT_STATUS.md`
- `docs/exchange/responses/0003_schema_consistency_review_response.md`

## Questions / conflicts

None. The corrections resolve documentation/example drift and preserve accepted decisions D001–D009.

## Tests

- Parsed all example files as JSON.
- Checked database-style examples for snake_case and resolved-props examples for camelCase.
- Verified required screen-instance and event fields.
- Verified IDs and references in the lock-to-chat sequence.
- Verified resolved props contain device state, resolved asset URIs, visual tokens, timing, and animation data without requiring database access.

## Notes

Frame coordinates are now explicit: screen-instance placement is shot-relative; events, message timing, and module input frames are screen-instance-local. Conversation participant membership is documented as a stable SQL relationship rather than canonical JSON. No application code, dependencies, migrations, TypeScript/Zod schemas, or build configuration were added or modified.
