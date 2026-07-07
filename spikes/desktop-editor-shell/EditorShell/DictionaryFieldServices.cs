using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record DictionaryFieldServices(
    Func<string, ValueKind, Task<string?>>? BrowsePath = null,
    Func<string, bool, Task<string?>>? ShowIconTokenPicker = null,
    Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? ShowThemeTokenPicker = null,
    Func<string, Control>? CreateIconPreview = null,
    Func<string, string?>? ResolveImagePath = null,
    Func<string, string>? GetFieldValue = null,
    Func<IReadOnlyList<FieldOption>>? GetPaletteColorOptions = null,
    Func<string, IReadOnlyList<FieldOption>>? GetComponentPresetOptions = null,
    Func<string, Task>? OpenEmbeddedComponent = null);
