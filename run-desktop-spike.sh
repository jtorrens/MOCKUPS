#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

exec dotnet run --project "$ROOT_DIR/spikes/desktop-editor-shell/Mockups.DesktopEditorShell.csproj"
