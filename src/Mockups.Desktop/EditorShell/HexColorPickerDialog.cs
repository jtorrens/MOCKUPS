using Avalonia.Controls;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class HexColorPickerDialog
{
    private const uint ChooseColorRgbInit = 0x00000001;
    private const uint ChooseColorFullOpen = 0x00000002;
    private const uint ChooseColorSolidColor = 0x00000080;
    private const int CustomColorCount = 16;

    public static bool IsSupported =>
        OperatingSystem.IsMacOS()
        || OperatingSystem.IsWindows();

    public static Task<string?> Show(
        Window owner,
        string currentValue)
    {
        var color = ColorValue.Parse(currentValue);
        if (OperatingSystem.IsMacOS())
        {
            return ShowMacOs(color);
        }
        if (OperatingSystem.IsWindows())
        {
            return Task.FromResult(
                ShowWindows(owner, color));
        }
        throw new PlatformNotSupportedException(
            "The system Hex color picker supports macOS and Windows.");
    }

    internal static string MacOsColorOutputToHex(
        string output)
    {
        var values = output
            .Trim()
            .Split(
                ',',
                StringSplitOptions.TrimEntries
                | StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != 3)
        {
            throw new InvalidOperationException(
                "The macOS color picker returned an invalid RGB value.");
        }

        return ColorValue.ToHex(
            Color.FromRgb(
                MacOsComponent(values[0]),
                MacOsComponent(values[1]),
                MacOsComponent(values[2])));
    }

    internal static uint WindowsColorReference(
        Color color) =>
        (uint)(
            color.R
            | (color.G << 8)
            | (color.B << 16));

    internal static string WindowsColorReferenceToHex(
        uint colorReference) =>
        ColorValue.ToHex(
            Color.FromRgb(
                (byte)(colorReference & 0xFF),
                (byte)((colorReference >> 8) & 0xFF),
                (byte)((colorReference >> 16) & 0xFF)));

    private static async Task<string?> ShowMacOs(
        Color color)
    {
        var processStart = new ProcessStartInfo
        {
            FileName = "/usr/bin/osascript",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        processStart.ArgumentList.Add("-e");
        processStart.ArgumentList.Add(
            $"set pickedColor to choose color default color "
            + $"{{{MacOsComponent(color.R)}, "
            + $"{MacOsComponent(color.G)}, "
            + $"{MacOsComponent(color.B)}}}");
        processStart.ArgumentList.Add("-e");
        processStart.ArgumentList.Add(
            "return (item 1 of pickedColor as text) & \",\" & "
            + "(item 2 of pickedColor as text) & \",\" & "
            + "(item 3 of pickedColor as text)");

        using var process = Process.Start(processStart)
            ?? throw new InvalidOperationException(
                "The macOS color picker could not be started.");
        var outputTask =
            process.StandardOutput.ReadToEndAsync();
        var errorTask =
            process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode == 0)
        {
            return MacOsColorOutputToHex(output);
        }
        if (error.Contains(
                "(-128)",
                StringComparison.Ordinal))
        {
            return null;
        }
        throw new InvalidOperationException(
            $"The macOS color picker failed: {error.Trim()}");
    }

    private static string? ShowWindows(
        Window owner,
        Color color)
    {
        var customColors = Marshal.AllocHGlobal(
            CustomColorCount * sizeof(uint));
        try
        {
            for (var index = 0;
                 index < CustomColorCount;
                 index++)
            {
                Marshal.WriteInt32(
                    customColors,
                    index * sizeof(uint),
                    unchecked((int)0x00FFFFFF));
            }

            var request = new ChooseColorRequest
            {
                StructSize =
                    Marshal.SizeOf<ChooseColorRequest>(),
                OwnerHandle =
                    owner.TryGetPlatformHandle()?.Handle
                    ?? nint.Zero,
                Result = WindowsColorReference(color),
                CustomColors = customColors,
                Flags =
                    ChooseColorRgbInit
                    | ChooseColorFullOpen
                    | ChooseColorSolidColor,
            };
            if (ChooseColor(ref request))
            {
                return WindowsColorReferenceToHex(
                    request.Result);
            }

            var error = CommonDialogExtendedError();
            if (error == 0)
            {
                return null;
            }
            throw new InvalidOperationException(
                $"The Windows color picker failed with error {error}.");
        }
        finally
        {
            Marshal.FreeHGlobal(customColors);
        }
    }

    private static int MacOsComponent(
        byte component) =>
        component * 257;

    private static byte MacOsComponent(
        string component)
    {
        if (!int.TryParse(
                component,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value)
            || value is < 0 or > 65535)
        {
            throw new InvalidOperationException(
                "The macOS color picker returned an invalid RGB component.");
        }
        return (byte)Math.Clamp(
            (value + 128) / 257,
            0,
            255);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ChooseColorRequest
    {
        public int StructSize;
        public nint OwnerHandle;
        public nint InstanceHandle;
        public uint Result;
        public nint CustomColors;
        public uint Flags;
        public nint CustomData;
        public nint Hook;
        public nint TemplateName;
    }

    [DllImport(
        "comdlg32.dll",
        EntryPoint = "ChooseColorW",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChooseColor(
        ref ChooseColorRequest request);

    [DllImport("comdlg32.dll")]
    private static extern uint CommonDialogExtendedError();
}
