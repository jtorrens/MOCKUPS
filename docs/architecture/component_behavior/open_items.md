# Behavior Reference Open Items

These are explicit discrepancies between the canonical behavior sheets and the
active desktop-preview implementation as checked on 2026-07-11.

1. **Generic text fidelity:** Text/emoji wrapping now uses a conservative
   grapheme-safe estimator and has an architecture guardrail for latin text
   with emoji/accented characters. Remaining work is exact parity between
   browser font measurement and Test Values controls, plus any write-on preview
   update discrepancies. Resolve this in common typography/measurement/frame
   code, never with a component-specific fallback.
2. **Video preview:** Cold video playback can present late or blank frames.
   The stable solution is deterministic frame preloading/buffering upstream of
   generic Media paint.
3. **General text-keyframe animation:** Before exposing the animation editor,
   implement the documented resolver algorithm for `writeOn` interpolation:
   compare consecutive text keyframes by grapheme cluster, delete to their
   common prefix, then type the new suffix over the interval. Do not represent
   this with per-character persisted keyframes or renderer-side timers.
