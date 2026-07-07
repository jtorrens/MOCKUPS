# PC parity validation

This document defines the minimum Windows/PC setup and validation flow for keeping the desktop editor behavior aligned with the Mac development state.

## Versioned state that must travel with commits

The desktop editor parity state is code plus data plus assets. Commits that affect editor behavior should include these paths when they changed:

- `data/desktop-editor-spike.sqlite`
- `assets/FOQN_S2`
- `assets/system/system_icons`
- relevant source/docs changes

The canonical desktop editor database is:

```text
data/desktop-editor-spike.sqlite
```

Do not rely on a locally generated empty database when validating PC parity. The app should open the committed desktop editor database and then run its non-destructive schema/default migrations on startup.

## Required Windows tools

Install:

- Git for Windows.
- Node.js 24 LTS or newer compatible LTS.
- .NET SDK 10.x, because the desktop spike targets `net10.0`.
- Microsoft Edge WebView2 Runtime if the preview panel opens blank.
- Visual Studio is optional. If used, install the .NET desktop workload.

Optional but useful:

- SQLite command line tools for direct DB spot checks.

## First checkout

From PowerShell:

```powershell
git clone <repo-url> MOCKUPS
cd MOCKUPS
git checkout codex/editor-modernization-rules
npm install
```

Confirm the committed parity files exist:

```powershell
Test-Path data/desktop-editor-spike.sqlite
Test-Path assets/FOQN_S2
Test-Path assets/system/system_icons
```

## Build and static checks

Run:

```powershell
npm run typecheck
npm run check:architecture
npm run validate:sqlite
npm run app:check
```

Expected result:

- TypeScript passes.
- Desktop preview architecture boundaries pass.
- SQLite validation passes using its isolated validation database.
- The Avalonia/Suki desktop editor project builds.

Known warning:

- `SQLitePCLRaw.lib.e_sqlite3` may report `NU1903` during .NET restore/build. That warning is currently known and not a PC parity failure by itself.

## Run the desktop editor

Run:

```powershell
npm run desktop
```

The app should open with the branch/version marker in the title and should load the same project data as on Mac.

## Manual parity checklist

Check these areas after startup:

- Navigation tree shows `Component Classes` grouped as `Components`, `Atoms`, and `System`.
- `Avatar`, `Label`, and `Button Icon` are under `Atoms`.
- `Text Input Bar`, `Keyboard`, `Status Bar`, and `Navigation Bar` are under `System`.
- Preview setup card shows `Scale` and `Marks` in the card header.
- Preview setup row shows `Device`, `Theme`, `Mode`, and `Orientation`.
- `Orientation: Landscape` swaps the preview frame width/height without modifying the selected device record.
- `Text Input Bar` design preview renders idle and typing icon rows.
- Text box icons are centered for a single line and bottom-aligned when the text grows to multiple lines.
- Icon picker displays icons from `assets/FOQN_S2/icon-themes`.
- Production fonts load from `assets/FOQN_S2/fonts`; changing to Oswald should visibly change text.

If SQLite CLI is installed, spot-check the committed DB:

```powershell
sqlite3 data/desktop-editor-spike.sqlite "select count(*) from component_classes; select count(*) from themes; select count(*) from icon_themes; select count(*) from production_fonts;"
```

Current expected counts at the time of this document:

```text
13
2
6
4
```

## What not to do during parity testing

- Do not run reset scripts unless intentionally rebuilding the development fixture database.
- Do not replace `data/desktop-editor-spike.sqlite` with a local empty database.
- Do not test with missing `assets/FOQN_S2`; many previews resolve icons, fonts, wallpapers, and avatars from that folder.
- Do not judge emoji font parity yet. Emoji rendering can still vary by OS and is tracked as a known follow-up.

## If PC behavior differs

Capture:

- the branch and commit hash;
- `dotnet --list-sdks`;
- `node --version`;
- whether WebView2 Runtime is installed;
- a screenshot of the failing editor/preview area;
- any message shown in the app message panel.

Then compare the DB and assets against the committed versions before changing component code.
