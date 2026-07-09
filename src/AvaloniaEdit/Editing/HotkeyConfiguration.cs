using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace AvaloniaEditCore.Editing;

public static class HotkeyConfiguration
{
    public static KeyModifiers BoxSelectionModifiers { get; private set; }

    public static PlatformHotkeyConfiguration Keymap
    {
        get { return Application.Current.PlatformSettings.HotkeyConfiguration; }
    }

    static HotkeyConfiguration()
    {
        BoxSelectionModifiers = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
                KeyModifiers.Control : KeyModifiers.Alt;
    }
}