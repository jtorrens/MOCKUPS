# Codex Response 0007 — Visual tokens and layout contract review

## Summary

Defined the canonical visual token/layout contract across theme tokens, device metrics/state, screen-instance props, resolved props, and diagnostic renderable metadata. Added accepted decisions D010–D014, aligned examples and Zod contracts, propagated the new values through resolvers, and replaced the visual stubs' principal layout constants with resolved tokens.

## Files changed

- `docs/architecture/05_decisions_log.md`
- `docs/architecture/07_initial_data_schema.md`
- `docs/architecture/08_visual_tokens_layout_contract.md`
- `docs/examples/theme_ios_light.json`
- `docs/examples/device_iphone_generic.json`
- `docs/examples/resolved_props_chat_screen.json`
- `docs/examples/resolved_props_message_bubble.json`
- `src/domain/schemas/resolvedProps.ts`
- `src/domain/repository/fixtures/exampleDataset.ts`
- `src/domain/resolvers/resolveChatScreen.ts`
- `src/domain/resolvers/resolveMessageBubble.ts`
- `src/visual/renderable/helpers.ts`
- `src/visual/modules/atomic/MessageBubbleModule.ts`
- `src/visual/modules/atomic/StatusBarModule.ts`
- `src/visual/modules/atomic/ChatHeaderModule.ts`
- `src/visual/modules/screens/ChatScreenModule.ts`
- `src/visual/validation/validateVisualModules.ts`
- `PROJECT_STATUS.md`
- `docs/exchange/responses/0007_visual_tokens_layout_contract_response.md`

## Questions / conflicts

None. The refined contract applies D004 to visual configuration and preserves decisions D001–D009; D010–D014 make the existing boundaries explicit.

## Tests

- `npm test` — passed.
- `npm run validate:examples` — all six examples passed.
- `npm run validate:resolver` — resolved chat/message props passed with the expanded contract.
- `npm run validate:visual` — renderable tree passed and now asserts resolved header, gutter, spacing, bubble-tail, and Wi-Fi values.
- `npm run typecheck` — passed with strict TypeScript.

## Notes

Resolved by this task: screen gutter; header height/background/separator; message and group spacing; bubble maximum-width ratio, tail tokens, and shadow; font line height; avatar size/gap; status icon scale; cursor tokens; device default screen scale; and explicit Wi-Fi state/icon state.

Still intentionally approximate: renderer-independent text measurement uses average glyph width; exact grapheme shaping/font metrics, bubble-tail path construction, avatar placement/collision behavior, status icon glyph assets, overflow/scroll policy, and final vertical layout remain future layout/renderer concerns. Visual modules retain defensive fallback numbers for legacy or malformed inputs, but validated resolved props now supply canonical values. Theme subgroups remain permissive JSON records; a future versioned token schema may tighten them once more screen types establish shared requirements.

No React, Remotion, Electron, DOM, Canvas, SVG, renderer backend, image/video export, SQLite, migration, persistence, UI, or asset pipeline was added.
