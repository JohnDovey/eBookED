using Avalonia;
using Avalonia.Native;

namespace eBookEditor.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .With(new AvaloniaNativePlatformOptions
        {
            // Falls back to software rendering when the native GPU/render-timer path is
            // unavailable (observed under this sandboxed dev session's WindowServer session).
            RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software]
        })
        .WithInterFont()
        .LogToTrace();
}
