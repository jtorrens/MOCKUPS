# Resources and assets

Status: normative.

## Resource ownership

Palette Colors, Themes, Icon Themes, Actors, Devices and Production Fonts are
Project-owned SQLite records. Asset files are referenced by
those current records and resolved through the owning resource service.

There are no cross-Project records and no cross-Project fallback.

Relative asset paths are resolved by the immutable path resolver associated
with the database context that owns the current desktop session. Preview,
dictionary controls and resource workflows receive that resolver explicitly;
they never read or configure a process-global Project root. Two contexts keep
independent roots even when they coexist in one process.

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

A Device owns its current metrics document. Device metrics are interpreted by
the domain and Preview layers. Repository, tree and shell expose the record
without embedding device-specific layout rules.

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

System UI actions use shared assets under `assets/system/system_icons`. A new
local glyph is not introduced when the shared action already exists.

## Wallpaper

Wallpaper is App configuration with explicit kind, light/dark color or image
references and alpha. Alpha affects the complete wallpaper visual, including
an image. Resolution happens before Preview rendering.

## Render output resources

Output mode and encoding profile are queue-job choices, not Project resources.
Snapshot preparation copies the exact assets needed by its resolved frames into
the queue-owned local store once per content hash. Light/Dark children share
that batch asset store. The worker registers a referenced asset only when its
current frame first needs it and therefore does not reinterpret current font,
icon, media or wallpaper records after enqueue.

## Asset delivery

A behavior or Preview change that alters icons, fonts, media, wallpaper or
seeded Theme/Component data commits every required asset and the parity
database together. Validation checks both stored references and filesystem
presence.
