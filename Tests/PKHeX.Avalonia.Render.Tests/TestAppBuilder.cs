using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(PKHeX.Avalonia.Render.Tests.RenderedAnimationAppBuilder))]

namespace PKHeX.Avalonia.Render.Tests;

public static class RenderedAnimationAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<global::PKHeX.Avalonia.App>()
        .UseSkia().WithInterFont()
        .With(global::PKHeX.Avalonia.Services.PlatformFontOptions.Create())
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
