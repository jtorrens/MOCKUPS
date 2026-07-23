# Behavior Reference Open Items

These are explicit discrepancies between the canonical behavior sheets and the
active desktop-preview implementation as checked on 2026-07-11.

1. **Video preview:** Cold video playback can present late or blank frames.
   The stable solution is deterministic frame preloading/buffering upstream of
   generic Media paint.
2. **General text-keyframe animation:** Before exposing the animation editor,
   implement the documented resolver algorithm for `writeOn` interpolation:
   compare consecutive text keyframes by grapheme cluster, delete to their
   common prefix, then type the new suffix over the interval. Do not represent
   this with per-character persisted keyframes or renderer-side timers.

Closed on 2026-07-11: generic text measurement and wrapping now shape the
declared production text and emoji font files, preserve manual line breaks and
split only at grapheme boundaries. The parsed faces, glyph-font selection and
shaped advances are cached independently of any component.
