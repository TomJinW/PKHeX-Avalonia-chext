using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public sealed class Gen4NameRenderTests
{
    [AvaloniaFact]
    public async Task LoadedGen4SaveDoesNotExposeNoncharactersToTextControls()
    {
        var previousLanguage = LocalizedStrings.Instance.CurrentLanguage;
        if (OperatingSystem.IsMacOS())
        {
            FontManager.Current.TryMatchCharacter(0xFFFF, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, FontFamily.Default, null, out _);
            Assert.True(FontManager.Current.TryMatchCharacter('宝', FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, FontFamily.Default, null, out var chineseFace));
            Assert.Equal("PingFang SC", chineseFace.FontFamily.Name);
        }
        var root = Path.Combine(Path.GetTempPath(), "pkhex-gen4-main-render");
        var paths = new Mock<IAppPaths>();
        paths.SetupGet(x => x.ConfigDirectory).Returns(Path.Combine(root, "config"));
        paths.SetupGet(x => x.DataDirectory).Returns(Path.Combine(root, "data"));
        paths.SetupGet(x => x.ConfigFilePath).Returns(Path.Combine(root, "config.json"));
        paths.SetupGet(x => x.LegacyConfigFilePath).Returns(Path.Combine(root, "legacy.json"));
        using var services = (ServiceProvider)App.BuildServiceProvider(paths.Object, Mock.Of<ISettingsStore>(), new AppSettings { DisplayLanguage = "zh-Hans" }, s =>
        {
            s.AddSingleton(Mock.Of<IDialogService>());
            s.AddSingleton(Mock.Of<IWindowService>());
        });
        var vm = services.GetRequiredService<MainWindowViewModel>();
        LocalizedStrings.Instance.SetLanguage("zh-Hans");
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles/gen4_heartgold.sav"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var file = Environment.GetEnvironmentVariable("PKHEX_GEN4_RENDER_SAVE") ?? Path.Combine(directory.FullName, "Tests/savefiles/gen4_heartgold.sav");
        var before = File.ReadAllBytes(file);
        var save = Assert.IsType<SAV4HGSS>(SaveUtil.GetSaveFile(before.ToArray()));
        services.GetRequiredService<ISaveFileGateway>().OpenLoadedSave(save, "fixture.sav");
        var window = new MainWindow { DataContext = vm, Width = 1200, Height = 850 };
        try
        {
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
            {
                Directory.CreateDirectory(path);
                using var frame = new RenderTargetBitmap(new PixelSize(1200, 850));
                frame.Render(window);
                frame.Save(Path.Combine(path, "gen4-loaded-main.png"));
            }
            var bad = window.GetVisualDescendants().Select(x => (Control: x, Text: x switch { TextBlock t => t.Text, TextBox t => t.Text, _ => null }))
                .Where(x => x.Text?.Contains('\uFFFF') == true).Select(x => x.Control.GetType().Name + ": " + x.Text).ToArray();
            Assert.Empty(bad);
            Assert.Equal(before, File.ReadAllBytes(file));
        }
        finally { window.Close(); LocalizedStrings.Instance.SetLanguage(previousLanguage); }
    }

    [AvaloniaFact]
    public async Task EmptyGen4EditorDoesNotRenderUnicodeNoncharacters()
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        Window? window = null;
        try
        {
            LocalizedStrings.Instance.SetLanguage("zh-Hans");
            var vm = new PokemonEditorViewModel(new PK4(), new SAV4HGSS(), Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>(), Mock.Of<IWindowService>(), settings: new AppSettings());
            var editor = new PokemonEditor { DataContext = vm };
            window = new Window { Content = editor, Width = 420, Height = 800 };
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
            {
                Directory.CreateDirectory(path);
                using var frame = new RenderTargetBitmap(new PixelSize(420, 800));
                frame.Render(window);
                frame.Save(Path.Combine(path, "gen4-empty-editor.png"));
            }
            Assert.Equal(string.Empty, vm.Nickname);
            Assert.Equal(string.Empty, vm.OriginalTrainerName);
        }
        finally { window?.Close(); LocalizedStrings.Instance.SetLanguage(previous); }
    }
}
