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

public sealed class Gen3NameRenderTests
{
    [AvaloniaFact]
    public async Task SettingPersistsIndependentlyAndNamesRender()
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        Window? window = null;
        try
        {
            LocalizedStrings.Instance.SetLanguage("zh-Hans");
            var settings = new AppSettings { Gen3ChineseNamesEnabled = false, Gen5ChineseNamesEnabled = false, Gen6ChineseNamesEnabled = false };
            var store = new Mock<ISettingsStore>();
            var coordinator = new UpdateCheckCoordinator(Mock.Of<IUpdateCheckService>(), Mock.Of<IWindowService>(), Mock.Of<IUpdateInstaller>(), Mock.Of<IAppLifetime>(), settings, store.Object);
            var vm = new SettingsViewModel(settings, store.Object, Mock.Of<IThemeService>(), Mock.Of<IUiDensityService>(), new LanguageService(), coordinator);
            var view = new SettingsView { DataContext = vm };
            window = new Window { Content = view, Width = 640, Height = 700 };
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var picker = view.FindControl<ComboBox>("Gen3NameEncodingPicker")!;
            Assert.Equal(0, picker.SelectedIndex);
            picker.SelectedIndex = 1;
            var ignoreTrash = view.FindControl<ToggleSwitch>("IgnoreGen3NameTrashErrorsToggle")!;
            Assert.False(ignoreTrash.IsChecked);
            ignoreTrash.IsChecked = true;
            var ngcPicker = view.FindControl<ComboBox>("Gen3NgcNameEncodingPicker")!;
            Assert.Equal(1, ngcPicker.SelectedIndex);
            Assert.Equal(2, ngcPicker.Items.Count);
            ngcPicker.SelectedIndex = 0;
            var gen4Picker = view.FindControl<ComboBox>("Gen4NameEncodingPicker")!;
            Assert.Equal(2, gen4Picker.SelectedIndex);
            Assert.Equal(3, gen4Picker.Items.Count);
            gen4Picker.SelectedIndex = 1;
            var gen5Picker = view.FindControl<ComboBox>("Gen5NameEncodingPicker")!;
            var gen6Picker = view.FindControl<ComboBox>("Gen6NameEncodingPicker")!;
            Assert.Equal(0, gen5Picker.SelectedIndex);
            Assert.Equal(0, gen6Picker.SelectedIndex);
            gen5Picker.SelectedIndex = 1;
            gen6Picker.SelectedIndex = 1;
            vm.SaveCommand.Execute(null);
            Assert.False(settings.Gen3NgcChineseNamesEnabled);
            Assert.True(settings.Gen3ChineseNamesEnabled);
            Assert.True(settings.IgnoreGen3NameTrashErrors);
            Assert.Equal(Gen4NameEncoding.YYJOY, settings.Gen4NameEncoding);
            Assert.True(settings.Gen5ChineseNamesEnabled);
            Assert.True(settings.Gen6ChineseNamesEnabled);
            Assert.Equal(Gen12NameEncoding.SimplifiedChinese, settings.Gen12NameEncoding);
            store.Verify(s => s.Save(settings), Times.Once);
            picker.BringIntoView();
            await Capture(window, "gen3-name-settings");
            gen6Picker.BringIntoView();
            await Capture(window, "gen56-name-settings");
            gen4Picker.BringIntoView();
            gen4Picker.IsDropDownOpen = true;
            await Capture(window, "gen4-name-settings");
            gen4Picker.IsDropDownOpen = false;
            var pk = new PK3 { Species = 25, Language = (int)LanguageID.English, CurrentLevel = 20, Version = GameVersion.E };
            Gen3ChineseNames.TryEncode("皮卡丘", pk.NicknameTrash);
            Gen3ChineseNames.TryEncode("小智", pk.OriginalTrainerTrash);
            var editorVm = new PokemonEditorViewModel(pk, new SAV3E(), Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>(), Mock.Of<IWindowService>(), settings: settings);
            var editor = new PokemonEditor { DataContext = editorVm };
            window.Content = editor;
            window.Width = 360;
            window.Height = 640;
            await Capture(window, "gen3-name-editor");
            Assert.Equal("皮卡丘", editor.FindControl<TextBox>("NicknameField")!.Text);
            editor.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex = 4;
            await Capture(window, "gen3-name-ot");
            Assert.Contains(editor.GetVisualDescendants().OfType<TextBox>(), field => field.IsEffectivelyVisible && field.Text == "小智");
            var trainerSave = new SAV3E();
            Gen3ChineseNames.TryEncode("佑树", trainerSave.SmallBlock.OriginalTrainerTrash[..7]);
            var trainer = new TrainerEditor { DataContext = new TrainerEditorViewModel(trainerSave, settings: settings) };
            window.Content = trainer;
            window.Width = 800;
            await Capture(window, "gen3-trainer-name");
            Assert.Contains(trainer.GetVisualDescendants().OfType<TextBox>(), field => field.IsEffectivelyVisible && field.Text == "佑树");
        }
        finally { window?.Close(); LocalizedStrings.Instance.SetLanguage(previous); }
    }

    private static async Task Capture(Window window, string name)
    {
        await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is not { } path) return;
        Directory.CreateDirectory(path);
        using var frame = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
        frame.Render(window);
        frame.Save(Path.Combine(path, name + ".png"));
    }
}
