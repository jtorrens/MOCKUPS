using System;

namespace Mockups.DesktopEditorShell.EditorShell;

/// <summary>
/// Authoring owner capability. Reads and writes execute on the session operation
/// worker, never through a Dictionary control or a captured field value.
/// </summary>
internal sealed record ComponentOverrideFieldOwner(
    string Identity,
    Func<string, string> Read,
    Action<ComponentOverrideAddress, string> Write,
    Func<ComponentOverrideAddress, string, ProjectTreeNode>? Promote = null,
    Func<ThemeComponentVariantSource, string>? ThemeVariantReference = null);
