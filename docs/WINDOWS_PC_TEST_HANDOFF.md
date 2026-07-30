# Windows PC test handoff

Status: executable handoff for the first Windows validation of MOCKUPS.

## Instruction for Codex on the PC

Work from the root of the `MOCKUPS` repository and perform a read-only product
validation on Windows.

Before acting:

1. Read `AGENTS.md` completely.
2. Read `docs/README.md` and every mandatory active architecture document
   listed in `AGENTS.md`.
3. Do not open, search or use anything under `docs/old`.
4. Do not change application code, committed project data, the parity database
   or this handoff.
5. Do not run database creation, migration, repair, normalization or reset
   commands.

## Required PC software

- Git;
- Node.js LTS and npm;
- .NET 10 SDK;
- Windows 10 or Windows 11 with its current Microsoft Edge WebView2 Runtime.

The published application is self-contained for .NET, but the current Preview
pipeline still starts `node.exe`. Node.js must therefore remain installed when
the published application is tested.

## Prepare the checkout

Use PowerShell:

```powershell
git fetch origin main
git switch main
git pull --ff-only origin main
git status -sb
git log -1 --oneline
```

Stop if:

- `main` does not equal `origin/main`;
- the worktree is not clean before preparation;
- Git proposes a merge or a non-fast-forward update.

Record the starting commit shown by `git log`.

## Verify the toolchain

```powershell
git --version
node --version
npm --version
dotnet --version
```

Stop and report the exact result if Node.js is unavailable or the .NET SDK is
not version 10.

## Install and validate

```powershell
npm ci
npm run desktop:db:validate
npm run desktop:publish:win
```

All three commands must finish successfully. Do not substitute database
creation or repair when validation fails.

The expected executable is:

```text
out\desktop\win-x64\Mockups.Desktop.Host.exe
```

Create an ignored disposable database beside the parity database so Project
asset resolution remains exact while the smoke test cannot modify committed
data:

```powershell
Copy-Item ".\data\mockups.sqlite" ".\data\.windows-smoke.sqlite"
& ".\out\desktop\win-x64\Mockups.Desktop.Host.exe" --db ".\data\.windows-smoke.sqlite"
```

## Manual smoke test

Check and record each result:

1. The startup surface completes and the main editor opens without a recovery
   error.
2. Design and Production navigation load and can change selection.
3. The selected editor opens and its Preview is visible.
4. Editing one field in the disposable database updates Preview.
5. Production Preview navigation, play and timeline interaction respond.
6. Hide the left navigation tree:
   - Preview keeps its exact width;
   - the released width goes to the central Editor panel.
7. Show the tree again:
   - Navigation, Editor and Preview return to their previous widths.
8. Hide the tree, close the application, reopen it and verify that the hidden
   state and retained expanded geometry survive restart.
9. Trigger an explicit navigation from Preview or another routed action and
   verify that the tree opens automatically and selects the destination.
10. Close the application normally and confirm that no process remains.

Take a screenshot of the open application and another showing the tree-hidden
layout. If a failure is visual, also capture the failing state.

## Final repository check

After the application is closed:

```powershell
Remove-Item ".\data\.windows-smoke.sqlite"
git status -sb
git diff --check
git log -1 --oneline
```

Only remove the exact disposable database created by this handoff. If it was
not created by the preparation above, do not remove it.

The worktree must remain clean and the final commit must match the recorded
starting commit. Do not clean, restore or delete unexpected changes merely to
make this check pass; report them.

## Report back

Return:

- Windows edition and version;
- CPU architecture;
- display resolution and scaling percentage;
- Git, Node.js, npm and .NET versions;
- tested commit and branch;
- result of installation, database validation and Windows publication;
- pass/fail for every manual smoke-test item;
- exact error text and command output for every failure;
- relevant screenshots;
- final `git status -sb`;
- whether any MOCKUPS, Node or Preview process remained after closing.

Do not implement fixes during this validation. Report findings so the owning
Mac task can decide the next coherent revision.
