using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public sealed class Gen12NameRenderTests
{
    [AvaloniaTheory]
    [InlineData("zh-Hans")]
    [InlineData("ko")]
    public async Task NameFieldsAndEncodingPickerRender(string language)
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        Window? window = null;
        try
        {
            LocalizedStrings.Instance.SetLanguage(language);
            var settings = new AppSettings { Gen12NameEncoding = Gen12NameEncoding.SimplifiedChinese };
            var pk = new PK2 { Species = 25, CurrentLevel = 20 };
            Gen12ChineseNames.TryEncode("皮卡丘", pk.NicknameTrash);
            Gen12ChineseNames.TryEncode("小智", pk.OriginalTrainerTrash);
            var vm = new PokemonEditorViewModel(pk, new SAV2(), Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>(), Mock.Of<IWindowService>(), settings: settings);
            var view = new PokemonEditor { DataContext = vm };
            window = new Window { Content = view, Width = 360, Height = 600 };
            window.Show();
            await Capture(window, $"gen12-editor-{language}");
            var nickname = view.FindControl<TextBox>("NicknameField")!;
            Assert.True(nickname.IsEffectivelyVisible);
            Assert.Equal("皮卡丘", nickname.Text);
            Assert.True(nickname.Bounds.Width > 80);
            view.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex = 4;
            await Capture(window, $"gen12-ot-{language}");
            Assert.Contains(view.GetVisualDescendants().OfType<TextBox>(), box => box.IsEffectivelyVisible && box.Text == "小智");
            var coordinator = new UpdateCheckCoordinator(Mock.Of<IUpdateCheckService>(), Mock.Of<IWindowService>(), Mock.Of<IUpdateInstaller>(), Mock.Of<IAppLifetime>(), settings, Mock.Of<ISettingsStore>());
            var settingsVm = new SettingsViewModel(settings, Mock.Of<ISettingsStore>(), Mock.Of<IThemeService>(), Mock.Of<IUiDensityService>(), new LanguageService(), coordinator);
            var settingsView = new SettingsView { DataContext = settingsVm };
            window.Content = settingsView;
            window.Width = 620;
            await Capture(window, $"gen12-settings-{language}");
            var picker = settingsView.FindControl<ComboBox>("Gen1NameEncodingPicker")!;
            picker.BringIntoView();
            await Capture(window, $"gen12-settings-picker-{language}");
            Assert.Equal(2, picker.SelectedIndex);
            Assert.True(picker.Bounds.Width > 200);
            var secondPicker = settingsView.FindControl<ComboBox>("Gen2NameEncodingPicker")!;
            Assert.Equal(2, secondPicker.SelectedIndex);
            Assert.Equal(3, secondPicker.Items.Count);
            secondPicker.BringIntoView();
            await Capture(window, $"gen2-settings-picker-{language}");
        }
        finally { window?.Close(); LocalizedStrings.Instance.SetLanguage(previous); }
    }

    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(9)]
    public async Task GenerationSpecificStatsAndNicknameButton(int generation)
    {
        var previous = GameInfo.CurrentLanguage;
        var previousUi = LocalizedStrings.Instance.CurrentLanguage;
        Window? window = null;
        try
        {
            new LanguageService().SetLanguage("zh-Hans");
            LocalizedStrings.Instance.SetLanguage("zh-Hans");
            SaveFile save = generation switch { 1 => new SAV1(), 2 => new SAV2(), _ => new SAV9SV() };
            PKM pk = generation switch { 1 => new PK1(), 2 => new PK2(), _ => new PK9() };
            pk.Species = 103;
            pk.CurrentLevel = 30;
            var vm = new PokemonEditorViewModel(pk, save, Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>(), Mock.Of<IWindowService>());
            var view = new PokemonEditor { DataContext = vm };
            window = new Window { Content = view, Width = 360, Height = 640 };
            window.Show();
            await Capture(window, $"gen{generation}-main");
            var shiny = view.FindControl<Button>("ShinyToggle")!;
            Assert.True(shiny.IsEffectivelyVisible);
            if (generation == 1)
            {
                shiny.Command!.Execute(null);
                Assert.True(vm.PreparePKM().IsShiny);
                shiny.Command.Execute(null);
                Assert.False(vm.PreparePKM().IsShiny);
            }
            var reset = view.FindControl<Button>("ResetNicknameButton")!;
            reset.Command!.Execute(null);
            Assert.Equal("椰蛋树", vm.Nickname);
            Assert.Equal("椰蛋树", generation < 3 ? Gen12ChineseNames.Decode(vm.PreparePKM().NicknameTrash) : vm.PreparePKM().Nickname);
            Assert.Equal(generation != 1, view.FindControl<Button>("MetTabButton")!.IsVisible);
            var tabs = view.GetVisualDescendants().OfType<TabControl>().Single();
            tabs.SelectedIndex = 1;
            vm.EvHP = generation < 3 ? 65535 : 252;
            await Capture(window, $"gen{generation}-stats");
            var fields = view.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
            NumericUpDown Find(string name) => fields.Single(c => global::Avalonia.Automation.AutomationProperties.GetName(c) == name);
            var hpDv = Find(vm.HpIvAutomationName);
            var spdDv = Find(vm.SpdIvAutomationName);
            var hpEv = Find(vm.HpEvAutomationName);
            Assert.Equal(generation >= 3, hpDv.IsEnabled);
            Assert.Equal(generation >= 3, spdDv.IsEnabled);
            Assert.Equal(generation < 3 ? 15 : 31, hpDv.Maximum);
            Assert.Equal(generation < 3 ? 65535 : 252, hpEv.Maximum);
            var width = view.FindControl<Grid>("StatsGrid")!.ColumnDefinitions[3].Width;
            Assert.True(width.IsAbsolute);
            Assert.Equal(generation < 3 ? 64 : 42, width.Value);
            if (generation < 3)
            {
                Assert.InRange(hpEv.Bounds.Width, 58, 62);
                var text = hpEv.GetVisualDescendants().OfType<TextBox>().Single();
                Assert.Equal("65535", text.Text);
                Assert.True(text.Bounds.Width - text.Padding.Left - text.Padding.Right >= 35);
            }
            tabs.SelectedIndex = 4;
            await Capture(window, $"gen{generation}-ot-fields");
        }
        finally
        {
            window?.Close();
            new LanguageService().SetLanguage(previous);
            LocalizedStrings.Instance.SetLanguage(previousUi);
        }
    }

    private static async Task Capture(Window window, string name)
    {
        await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
        using var frame = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
        frame.Render(window);
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
        {
            Directory.CreateDirectory(path);
            frame.Save(Path.Combine(path, name + ".png"));
        }
    }
}
