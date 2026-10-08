# Resources and assets

Status: normative.

## Resource ownership

Palette Color identities, Icon Themes, Component Classes and complete Component
Variants are System-owned SQLite records. Every
Project owns a complete set of RGB values for the fixed Palette catalog.
Themes, Actors, Devices and Production Fonts remain Project-owned SQLite
records. Asset files are referenced by those current records and resolved
through the owning resource service.

There is no cross-Project fallback. Global Palette identities resolve only
through the exact active Project's required RGB rows.

Each Project stores either no media root or one absolute external directory
path in `projects.media_root`. The database location never owns or implies that
asset location. Asset records store paths relative to that Project media root.
Preview, dictionary controls and resource workflows receive the session path
resolver explicitly; they never copy Project assets into application data or
configure a process-global root. Two contexts keep independent roots even when
they coexist in one process.

A Shot reference video is not a Preview Split reference. The authoring picker
stores a portable relative path when the selected supported video is inside
the current Project root and its absolute workstation path when it is outside
that root. Relative paths may not escape the Project root. A missing file
leaves the authored reference intact and produces the explicit `Sin media`
presentation instead of falling back across Projects or roots.

The desktop reference window exposes the resolved local video only through a
loopback, process-owned, read-only HTTP source with byte-range support. This
keeps large MOV/MP4 files streamable and seekable by the native WebView without
copying them into Preview data, embedding them as data URIs or exposing a
general filesystem server. Closing the editor stops that source.

## External Media inventory

Every Project exposes one permanent **External Media** surface in both Design
and Production. Its focused query is the sole owner of the Project-wide index
of authored external image, wallpaper, avatar, media, video and directory
paths. It traverses only fields declared with the corresponding media
`ValueKind`, plus the exact App, Actor, Shot, Production Font family and Icon
Theme asset-root documents owned by their repositories. It never scans
arbitrary JSON text or infers authored references from file extensions.

The index projects global App relative resource paths through the selected
Project media root and covers every Production Screen payload, local Override
and media-valued animation keyframe. Global Component and Module Design
fixtures live in App Support and remain outside every Project inventory. An animated media
usage retains the exact animation track and keyframe ids so replacement writes
only that authored keyframe through the Module Instance animation owner. Each
Production Font family directory is represented once. System Icon Theme
directories, individual icon files, System Preview fixtures and other
application-internal assets remain
outside the Project inventory. Relative references resolve through the Project
path resolver; absolute references retain their authored workstation location.
Missing targets remain listed and are marked explicitly so stale authored
references can be found without repairing or deleting them.

Missing Runtime media remains authored and visible in this inventory. The
effective Runtime document preserves the exact selected value, including media
keyframes: it never substitutes Design defaults. The shared asset boundary
resolves relative paths only against the exact Project root and absolute paths
at their authored location. It never searches the working directory or removes
a duplicated root prefix. Missing media paints `Media ausente`; an extraction or
read failure paints `Error al leer media`, with a nonblocking diagnostic. A video
frame cache is keyed by asset identity and exact requested time; extraction
failure cannot reuse a frame from another time. Media and image wallpaper use
the same notice primitive. Empty or unavailable Gallery directories produce a
notice, not another directory's contents. These notices remain in exports.

Each result retains its exact owner, authoring surface, field, nested slot path
and stable structured-item id. The UI can therefore navigate to the owning
editor and focus that exact field or item without matching labels, types or
positions. A file row shows its absolute parent path and filename separately.
A directory row shows only the absolute directory path and the indicative File
name text `Media folder` or `Font family folder`; it never
expands the directory into synthetic file usages.

The File name cell exposes `Replace media…`, `Replace media folder…`, `Replace
font family…` according to the declared owner, for both existing and missing
references. Production Font replacement rebuilds the family's exact
file/style/weight document from the selected directory. Contextual menus expose
`Show in Finder` as a separate action when the target exists; right-click never
reveals a target directly. The replacement action uses the
declared field `ValueKind` picker, preserves that picker's relative/absolute
storage policy and commits only the exact owner, nested slot and stable item
identified by the row through the existing editor, Design Test Values or
Production Runtime Input write contract. It never performs a global path
replacement. After a successful commit the focused query reloads the complete
inventory while the current column and sort direction remain unchanged.

The Path cell additionally exposes `Change source directory…`. Its row's
absolute directory is the exact old prefix. Every declared External Media
usage whose resolved directory is that directory or a descendant is
reassociated through its existing typed owner while preserving the target
suffix below that prefix. The operation does not copy files and does not
require every corresponding target to exist: unavailable targets remain
visible as missing for individual replacement. Media files keep the normal
relative-when-inside-media-root and absolute-when-outside storage policy;
resource directories retain their stricter Project media-root ownership.

## Palette and Themes

System Palette records provide stable semantic color identities and creation
defaults. A Project Palette can edit only the RGB corresponding to each fixed
identity; it cannot add, duplicate, rename or delete identities. Themes provide
complete token documents and explicit light/dark references to those global
identities. Alpha is part of the complete resolved visual value and applies
consistently to colors and images where the owning visual contract declares it.

Every Project Palette is complete. Missing values fail validation and Preview;
the System default is copied only when the Project is explicitly created.

Design exposes a `System Palette Color` editor for the global identity,
creation default and lifecycle. Production Data exposes a distinct
`Production Palette Color` editor whose exact owner is the Project plus System
color id and whose only editable field is RGB. Themes also live in Production
Data. The two Palette record classes never share layout metadata or mutation
actions.

Theme interpretation stays in common domain services and Preview resolution,
not repositories or shell code.

Theme is the sole owner of concrete Production Font ids for text, system and
emoji typography. Component, Module, Variant, Override and Runtime typography
stores only `theme`, `theme.system` or `theme.emoji`; direct Production Font ids
outside Theme are invalid current data.

## Actors

An Actor owns its stable Production identity, default Theme used to initialize
new Screens, and associated visual metadata. Existing Screens retain their own
exact Theme when the Actor default changes. A Shot always names one Actor. Component-specific Actor use,
such as a conversation message owner, remains a separate explicit reference.

Usage actions navigate to Design or Production as required, open the exact tree
branch and select the owning editor. Usage lines in destructive confirmations
are navigable actions that close the dialog before navigating.

## Devices

A Device owns one strict current metrics document:

```json
{
  "canvas": { "width": 1179, "height": 2556 },
  "screen": { "x": 0, "y": 0, "width": 1179, "height": 2556 },
  "cornerRadius": 151,
  "safeArea": { "bottom": 93 },
  "statusBar": { "height": 161 },
  "moduleTransparency": {
    "enabled": false,
    "mode": "fixed",
    "paletteColor": "gray_000",
    "backgroundOpacity": 1,
    "fixedStart": 1278,
    "minimumOpaqueExtent": 1278,
    "gradientHeight": 639,
    "variableOffset": 0
  }
}
```

`frame.cornerRadiusCoefficient` and
`designGuides.safeMarginCoefficient` are the only optional properties. Every
object rejects undeclared properties. Device metrics contain no design-space,
render-size, pixel-ratio, default-scale, viewport, Dynamic Island, source or
unit metadata. The domain owns validation and projection; Preview consumes
only Canvas and Screen geometry plus the declared visual coefficients.
Repository, tree and shell expose the record without embedding
device-specific layout rules.

`moduleTransparency` is the Device-owned global Module wallpaper override and
is required even when disabled. Its Palette token resolves in the same Project
and is tracked as an exact resource reference. Values are authored in Device
units. `fixed` uses `fixedStart`; `variable` resolves its start on every frame
from the last visible pixel of the complete Module foreground before any
substitute background or opacity mask exists, then adds `variableOffset` and
compares that result with `minimumOpaqueExtent`. The larger coordinate is the
gradient start, so the complete Module remains fully opaque from the Device top
through at least that minimum extent.
The original wallpaper is absent. The substitute Palette surface uses only
`backgroundOpacity`; the Module foreground retains its authored alpha and no
additional opacity is applied to it. The foreground is then composed over that
surface, and one separate global mask is applied to the complete result. That
mask remains fully opaque from the Device top through the resolved start and
fades to zero over `gradientHeight`. The variable offset changes the start
before this mask is constructed; it never moves either painted layer. No
legacy `opacity` or fade keys, aliases or missing-object defaults are accepted.
The interactive Device Preview presents an enabled policy over a fixed black
matte regardless of the selected Theme mode. That matte is Preview chrome, not
an authored Dark appearance and not part of the clean raster document.

## Production Fonts

A Production Font owns:

- its current record and metadata;
- a strict array of declared font files;
- the Project-relative asset references used by Preview.

Font lookup resolves from the Project asset root. Temporary payload folders do
not become the authority for source font files. Missing declared files fail
with the owning font and path identified.

## Icon Themes

Discovery/refresh is non-destructive. A collection directory carries one
`manifest.json` import document with `schemaVersion: 1`, stable `id`, explicit
`name`, complete `mapping` and `metadata` containing the provider `iconSet`.
The shared document contract validates the input without inferring provider,
style, weight, token identity or file references from names. Existing records
retain their SQLite-authored mapping, name and metadata; the manifest is only
an initial import snapshot, not a second live authority. Its exact id permits
an unambiguous directory relocation. Duplicate ids/names/paths and malformed
manifests fail the refresh before committing any records. No current reader
accepts the retired provider-report manifest format.

Refresh never computes a common filesystem intersection or invents `token.svg`
references. Missing directories and SVGs remain authored mappings and are
reported as unavailable, preserving the existing red-square Preview behavior.
Unlisted files are not implicitly imported. New tokens enter through explicit
token import, and deleting tokens remains a separate operation. New collection
imports supply their complete explicit mapping in the manifest; discovery does
not rewrite other collections to make their token sets match.

An Icon Theme owns one global current mapping document plus metadata. Every
token maps explicitly to one asset under `assets/system/icon-themes`. The full
set catalog is shared by every Production: stable Icon Theme ids and token ids
are never copied or remapped when a Production is created. Editing a mapped SVG
therefore changes every Production that resolves that Icon Theme and token.
Icon selection, mapping validation and System asset resolution live in the
resource owner, not `MainWindow`, a generic editor or the renderer.

Design exposes the one System Icon Themes editor. Theme remains
Production-owned and selects a System Icon Theme by its stable global id.
Preview receives the resolved System directory explicitly and never probes a
Project media root or a hardcoded Production directory for icon assets.
Every Preview icon resolves only through that selected Theme's exact mapping,
including tokens beginning with `system_`; that prefix is not a routing rule.
A missing token or unavailable mapped SVG becomes a red square at the authored
icon box, prepared by the shared icon helper before generic painting. Explicit
absence (`null` or empty, where declared) creates no icon. Startup reports
missing SVG assets without blocking the Project. Invalid mapping documents
remain contract errors. The placeholder remains in exports and logs a warning.

The shared SVG transformation workflow emits a filled icon as direct filled
geometry. It does not encode that geometry through a background mask.
Re-editing a previously transformed SVG recovers its authored inner geometry
before applying the next transform, so transformations do not nest or recolor
mask semantics. Lightweight Icon Theme previews inherit presentation attributes
through the SVG element hierarchy and never paint definition-only geometry.

System UI actions use shared assets under `assets/system/system_icons`. A new
local glyph is not introduced when the shared action already exists.

The provisional desktop application identity is separate from those in-product
actions. Its 1024 px master and derived macOS bundle icon live under
`assets/system/application`; the macOS packaging owner copies the `.icns` into
the application bundle and declares that exact resource in `Info.plist`.

## Wallpaper

Wallpaper is App configuration with explicit kind, light/dark color or image
references and alpha. Alpha affects the complete wallpaper visual, including
an image. Resolution happens before Preview rendering.

An enabled Device `moduleTransparency` policy supersedes this App or Actor
wallpaper for every Module. Components do not interpret that policy.

## Render output resources

Output mode and encoding profile are queue-job choices, not Project resources.
At each job start, transient preparation resolves the latest Shot and Screens
and copies the exact assets needed by those frames into that job's temporary
store once per content hash. The worker registers a referenced asset only when
its current frame first needs it. The complete temporary asset store is deleted
after the job; enqueue persists no font, icon, media or wallpaper state.
Before publication, RGB is multiplied by the raster alpha against black while
the alpha channel remains unchanged. PNG, EXR and ProRes 4444 therefore carry
premultiplied alpha; non-alpha MOV profiles retain the corresponding black
composite when they discard alpha.

## Asset delivery

### Resource imports and replacements

Production Font imports and Icon Theme token imports/replacements use the same
recoverable resource file-write owner. Originals remain in a durable undo
image until SQLite confirms all authored metadata together. Failed writes
restore the original files; failed/interrupted recovery remains in Settings →
Review resource cleanup for explicit retry, never automatic startup repair.
Filesystem swaps are per-file, not a filesystem-wide atomic transaction.

Icon Theme rename and duplication use the same resource directory-transfer
owner, not direct directory moves or local copy/rollback helpers. The source
stays intact until the destination and authored row commit; rename retires the
source through the shared cleanup outbox in that same transaction. A failed
retirement remains in Settings for explicit retry. Empty directories are
preserved. The stable id is retained on rename and new on duplicate; mappings
and explicit provider metadata are preserved, with only name fields changed.
A required identity manifest must be valid and match the source id before any
write. Rename and duplication export a current import snapshot from SQLite;
duplication assigns its new id to that snapshot. Invalid directory names and occupied or overlapping destinations
fail explicitly. Case-only aliases require a distinct intermediate name; they
are never implemented with an unjournaled temporary move.

Icon search requires at least one selected valid source, not a pair. When both
providers have equivalents, each collection receives its native SVG. When a
collection has no equivalent, import explicitly copies an available selected
SVG using the same semantic token. The canonical copy sources are Lucide
stroke 2 and Material rounded weight 400; a missing style uses its selected
provider's canonical source, then the other selected provider. No valid source
fails preparation; invalid SVG/read/download errors fail rather than being
treated as absence. The provider script returns prepared SVGs keyed by exact
Icon Theme id and never writes collection files. Resources validates all ids
and SVGs, prepares exact mappings and commits them with the file transaction.
Each mapping records actual provider, source name and whether it was copied;
refresh preserves that provenance. Preview reads the resulting ordinary files
and exact mappings, with no provider substitution or alternate rendering path.

### Resource deletion

Production Font families, Icon Themes and Icon Theme tokens use one shared
recoverable file-deletion owner in Resources. The record/mapping deletion and
its captured file manifest are committed atomically before file cleanup.
If SQLite rejects the write, both records and files remain unchanged. A file
cleanup failure does not undo or report failure of the already committed
record deletion; its exact task remains visible under Settings → Review
resource cleanup for explicit retry. There is no automatic retry on startup
and no implicit undo or Trash contract.

Cleanup refuses resource roots themselves, escaped paths, symbolic links,
changed file fingerprints and targets that current resources reference again.
It removes only captured files and empty captured directories, never newly
discovered contents through recursive deletion. Discovery/import cannot reuse
paths with pending cleanup. A missing captured file counts as already cleaned;
an unavailable root remains pending rather than being treated as completion.
The repository owns SQL, the resource service owns file policy, and a narrow
cleanup port exposes pending jobs and explicit retries to the editor. The shell
only wires the shared settings action.

All resource file operations share the same pending-recovery notification, not
delete-only warnings. A committed change with pending cleanup is a warning,
not a failed save; an uncommitted write explicitly reports pending restoration.
The workflow queues UI notices independently of persistence and detaches at
window close. Its Settings action remains the only explicit retry route.

A behavior or Preview change that alters icons, fonts, media, wallpaper or
seeded Theme/Component data commits every required asset and the parity
database together. Validation checks both stored references and filesystem
presence.
