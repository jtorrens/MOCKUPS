# Behavior Reference Open Items

These are explicit discrepancies between the canonical behavior sheets and the
active desktop-preview implementation as checked on 2026-07-11.

1. **Conversation to Keyboard shared time:** Conversation now passes the final
   revealed grapheme index, but does not yet pass shared module
   `motionTimeSeconds` into Keyboard when its motion belongs to the module
   timeline.
2. **Runtime message schema:** Message arrays preserve ordering today, but do
   not yet explicitly carry hierarchy and editor-group metadata required by the
   canonical collection model.
3. **Test Values parity:** The active path uses the same resolver contract, but
   the module still has a compatibility adaptation for direct preview message
   fields. Remove it once the message collection editor owns all test/instance
   input construction.
4. **Generic text fidelity:** Text/emoji wrapping, final-line clipping and
   some write-on preview updates can differ from the Test Values control.
   Resolve this in common typography/measurement/frame code, never with a
   component-specific fallback.
5. **Video preview:** Cold video playback can present late or blank frames.
   The stable solution is deterministic frame preloading/buffering upstream of
   generic Media paint.
