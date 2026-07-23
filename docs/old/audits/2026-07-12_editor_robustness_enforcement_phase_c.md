# Desktop Editor Robustness and Enforcement Audit — Phase C

Date: 2026-07-12

Base commit: `41dd7e80` (`docs: record editor audit phase B`)

Status: reviewed and approved; no fixes applied

## Executive summary

Phase C audited startup/migration idempotence, persisted residue, cache/state
dependence and performance robustness. It found three P2 issues. No P0/P1 issue
was found beyond the already accepted B01 spacing-contract violation.

Logical database content is idempotent across repeated starts, but every no-op
start rewrites SQLite bytes. Video/reference assets can remain stale when a file is
replaced at the same path, and the rendered-frame cache retains the largest
reserved playback capacity for the rest of the process.

The expanded numeric scan found no new component spacing violation beyond B01.
Theme Keyboard `keyGap`/`rowGap` values are Theme-owned numeric token definitions,
not components bypassing Theme spacing.

## Test matrix

| Area | Method | Result |
| --- | --- | --- |
| Startup migrations | three launches against disposable DB | logical dump stable; physical file rewritten |
| Database integrity | `PRAGMA integrity_check` after launches | pass |
| Schema version | `PRAGMA user_version` | stable at 1 |
| Persisted retired vocabulary | recursive SQLite JSON/layout scan | B01/B02 only |
| Numeric spacing equivalents | component/module/instance/Theme JSON scan | no new component finding |
| Preview frame cache | capacity lifecycle inspection | C03 |
| Video and reference caches | key/invalidation inspection | C02 |
| Font/SVG caches | key/invalidation inspection | SVG safe; font process-lifetime limitation noted |
| Architecture enforcement | existing check and committed-DB assertions | passes; B01/B02 gaps documented |
| Collections/actions | stable IDs, applicability, cancellation checks | pass through existing executable guards |
| Event subscriptions | editor/controller lifecycle inspection | no reproducible duplicate subscription found |
| Accessibility surfaces | keyboard/dialog/static inspection | no new reproducible defect |

## Findings

### P2-C01 — A no-op application start rewrites the committed SQLite file

**Reproduction:** copy the committed database, hash it, start and stop the editor
without editing, and hash again. Repeat.

**Evidence:** the physical SHA-256 changed on each of the first three starts. The
canonical `.dump` hash remained identical to the committed database and remained
stable between later starts; integrity and `user_version` also remained valid.
This is therefore write amplification/no-op persistence, not logical corruption.

**Affected area:** startup normalization sequence in `SpikeDatabase.cs`, including
normalizers in `SpikeDatabase.ComponentClassSeeds.cs`,
`SpikeDatabase.RuntimeInputContracts.cs` and `SpikeDatabase.EditorLayouts.cs`.
Several routines serialize and execute `UPDATE` statements even when their
normalized logical document is unchanged.

**Impact:** merely opening the application dirties the tracked parity database,
obscures real data changes, increases the chance of accidental DB commits and
makes byte-level reproducibility depend on whether the editor was launched.

**Recommended correction:** make every startup normalizer compare canonical
before/after values and issue no `UPDATE` when unchanged. Prefer explicit,
versioned migrations over permanent startup normalization.

**Proposed check:** copy the committed DB, initialize it twice, and assert both a
stable logical dump and stable file hash after the first completed migration pass.

### P2-C02 — Media/reference caches ignore same-path file replacement

**Reproduction:** preview a video or reference image/video, replace its file at the
same path, and request the same time/frame without restarting the process.

**Evidence:**

- `previewAssetResolver.ts` keys duration by path and video frames by
  `path + time`; its disk-frame filename uses the same inputs.
- `PreviewReferenceOverlay.cs` keys resolved URIs by `path + time`.
- Neither key includes file size, modification time or a content digest.
- The video `lastVideoFrameByPath` fallback also survives same-path replacement.

**Affected files:** `src/desktop-preview/previewAssetResolver.ts` and
`spikes/desktop-editor-shell/EditorShell/PreviewReferenceOverlay.cs`.

**Impact:** preview parity can depend on warm cache/process state. Reopening the
editor may appear to fix the asset, hiding the invalidation defect.

**Recommended correction:** include stable file identity (at minimum size and
mtime; preferably content identity where already available) in memory and disk
cache keys, and invalidate duration/last-frame entries together.

**Proposed check:** replace a fixture at the same path and assert that the next
frame/reference URI changes without process restart.

### P2-C03 — Frame-cache capacity grows for a long action and never contracts

**Reproduction:** prepare an action exceeding the default 180-frame cache, then
return to short/static previews.

**Evidence:** `ReserveFrameCacheCapacity` raises the static capacity up to 4096.
Requests at or below the default return without resetting `_frameCacheCapacity`,
and no end-of-playback contraction path exists.

**Affected file:**
`spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs`.

**Impact:** one long action can retain thousands of complete HTML frame strings for
the remaining editor process, producing state-dependent memory pressure during
otherwise small previews.

**Recommended correction:** make capacity a scoped reservation owned by playback,
restore the default afterward and evict excess entries deterministically. Preserve
the existing independent interactive/prewarm lanes.

**Proposed check:** reserve a large window, release it, and assert capacity and
entry count return to the default bound.

## Audited areas with no new finding

- Repeated startup preserves the logical database and passes integrity checks.
- The expanded persistence scan found no additional retired radius, time-unit,
  component-reference or icon-path vocabulary in committed active payloads.
- No additional raw component padding/gap/gutter/margin field was found beyond
  accepted B01.
- `SvgIconPreview` invalidates cached SVG content using modification time and size.
- Frame-cache keys include the complete serialized payload digest, so Theme/input
  changes do not reuse a different resolved frame.
- Runtime collection guards reject stale item IDs and keep independent repeated
  children.
- Declarative actions retain applicability, units and finite-duration reset
  semantics under the existing architecture checks.
- No component-specific branch was found in generic cache, renderer or WebView
  update logic.

## Recommended disposition

Phase C was reviewed and approved on 2026-07-12. C01, C02 and C03 remain accepted
as P2 findings. No implementation is authorized in the audit thread.

C01 should be addressed alongside explicit migration cleanup. C02 and C03 belong
to the later robustness/performance fixes phase.

## Recommended implementation order

The approved cross-phase correction order is:

1. B01 — migrate Label/Avatar gaps to semantically mapped `theme.spacing.*`
   tokens across defaults, variants, overrides and committed data.
2. B02 + C01 — remove retired layouts through an explicit migration and make
   post-migration no-op startup byte-stable.
3. C02 — add file identity and joint invalidation to Media/Reference caches.
4. C03 — make frame-cache expansion an owned reservation with deterministic
   release on playback completion or cancellation.
5. A01–A04 — apply the accepted UI-language, shared amber, breadcrumb and
   pluralization corrections.

Implementation must occur in a separate phase/thread after the audit is closed.
