# Behavior Reference Open Items

These are explicit discrepancies between the canonical behavior sheets and the
active desktop-preview implementation as checked on 2026-07-11.

1. **Generic text fidelity:** Text/emoji wrapping, final-line clipping and
   some write-on preview updates can differ from the Test Values control.
   Resolve this in common typography/measurement/frame code, never with a
   component-specific fallback.
2. **Video preview:** Cold video playback can present late or blank frames.
   The stable solution is deterministic frame preloading/buffering upstream of
   generic Media paint.
