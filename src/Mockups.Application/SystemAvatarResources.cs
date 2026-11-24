using System.Collections.Frozen;

namespace Mockups.DesktopEditorShell.Common;

public static class SystemAvatarResources
{
    public const string DefaultAvatar = "sample-one.svg";

    private static readonly FrozenDictionary<string, string> Images =
        typeof(SystemAvatarResources).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Mockups.Avatars.", StringComparison.Ordinal))
            .ToFrozenDictionary(name => name["Mockups.Avatars.".Length..], ReadImage);

    public static string ImageUri(string name) => Images[name];

    private static string ReadImage(string name)
    {
        using var source = typeof(SystemAvatarResources).Assembly.GetManifestResourceStream(name)!;
        using var bytes = new MemoryStream();
        source.CopyTo(bytes);
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(bytes.ToArray())}";
    }
}
