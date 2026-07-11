# Behavior Reference Open Items

These are explicit discrepancies between the canonical behavior sheets and the
active desktop-preview implementation as checked on 2026-07-11.

1. **Legacy direct preview message compatibility:** Conversation Test Values
   now use sourced `messages[]` with id-matched overrides. The renderer still
   accepts legacy direct preview message fields as a compatibility route.
   Remove that once saved previews no longer need it.
2. **Generic text fidelity:** Text/emoji wrapping, final-line clipping and
   some write-on preview updates can differ from the Test Values control.
   Resolve this in common typography/measurement/frame code, never with a
   component-specific fallback.
3. **Video preview:** Cold video playback can present late or blank frames.
   The stable solution is deterministic frame preloading/buffering upstream of
   generic Media paint.
