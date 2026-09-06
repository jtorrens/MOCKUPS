# Resources and assets

Status: normative.

## Resource ownership

Palette Colors, Themes, Icon Themes, Actors, Devices and Production Fonts are
Project-owned SQLite records. Asset files are referenced by
those current records and resolved through the owning resource service.

There are no cross-Project records and no cross-Project fallback.

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
of authored external image, wallpaper, avatar, media, video and media-directory
paths. It traverses only fields declared with the corresponding media
`ValueKind`, plus the exact App, Actor and Shot media documents owned by their
repositories. It never scans arbitrary JSON text or infers assets from file
extensions.

The index covers every complete Component Variant, every complete Module
Variant, their Design Test Values and defaults, and every Production Screen
payload and local Override. Icons, Icon Themes, fonts and application-internal
assets are outside this inventory. Relative references resolve through the
Project path resolver; absolute references retain their authored workstation
location. Missing targets remain listed and are marked explicitly so stale
authored references can be found without repairing or deleting them.

Each result retains its exact owner, authoring surface, field, nested slot path
and stable structured-item id. The UI can therefore navigate to the owning
editor and focus that exact field or item without matching labels, types or
positions. A file row shows its absolute parent path and filename separately.
A media-directory row shows only the absolute directory path and the indicative
filename text `Media folder`; it never expands the directory into synthetic
file usages. Right-clicking an existing path reveals that exact file or folder
in the workstation file manager.

## Palette and Themes

Palette records provide stable semantic color identities. Themes provide
complete token documents and explicit light/dark values. Alpha is part of the
complete resolved visual value and applies consistently to colors and images
where the owning visual contract declares it.

Theme interpretation stays in common domain services and Preview resolution,
not repositories or shell code.

## Actors

An Actor owns its stable Production identity, Theme choice and associated
visual metadata. A Shot always names one Actor. Component-specific Actor use,
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

An Icon Theme owns one current mapping document plus metadata. Every token maps
explicitly to an asset. Icon selection, mapping validation and asset resolution
live in the resource owner, not `MainWindow`, a generic editor or the renderer.

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

A behavior or Preview change that alters icons, fonts, media, wallpaper or
seeded Theme/Component data commits every required asset and the parity
database together. Validation checks both stored references and filesystem
presence.
