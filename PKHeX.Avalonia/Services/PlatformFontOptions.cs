using Avalonia.Media;

namespace PKHeX.Avalonia.Services;

internal static class PlatformFontOptions
{
    public static FontManagerOptions Create() => new()
    {
        // Keep CJK text away from macOS LastResort fallback, including after a
        // malformed save string caused the platform matcher to cache that face.
        FontFallbacks = OperatingSystem.IsMacOS() ?
        [
            new FontFallback
            {
                FontFamily = new FontFamily("PingFang SC"),
                UnicodeRange = UnicodeRange.Parse("U+2E80-9FFF,U+F900-FAFF,U+FF00-FFEF"),
            },
            new FontFallback
            {
                FontFamily = new FontFamily("Apple SD Gothic Neo"),
                UnicodeRange = UnicodeRange.Parse("U+1100-11FF,U+3130-318F,U+AC00-D7AF"),
            },
        ] : [],
    };
}
