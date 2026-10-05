using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public sealed class Gen2ClockRenderTests
{
    [AvaloniaFact]
    public async Task ChineseClockSectionRendersAndDisplaysPassword()
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        Window? window = null;
        try
        {
            LocalizedStrings.Instance.SetLanguage("zh-Hans");
            var vm = new TrainerEditorViewModel(new SAV2 { OT = "GOLD", TID16 = 12345, Money = 123456 }, Mock.Of<IDialogService>());
            vm.CalculateGen2ClockPasswordCommand.Execute(null);
            var view = new TrainerEditor { DataContext = vm };
            window = new Window { Content = view, Width = 900, Height = 700 };
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            view.FindControl<Border>("Gen2ClockSection")!.BringIntoView();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var section = view.FindControl<Border>("Gen2ClockSection")!;
            Assert.True(section.Bounds.Height < 70);
            var parent = Assert.IsType<StackPanel>(section.Parent);
            Assert.Same(section, parent.Children.Last());
            Assert.Empty(section.GetVisualDescendants().OfType<CheckBox>());
            Assert.True(view.FindControl<Button>("ResetGen2ClockButton")!.IsEnabled);
            var field = view.FindControl<TextBox>("Gen2ClockPasswordField")!;
            Assert.True(field.IsEffectivelyVisible);
            Assert.True(field.IsReadOnly);
            Assert.Equal(5, field.Text!.Length);
            Assert.True(field.Bounds.Width >= 75);
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
            {
                Directory.CreateDirectory(path);
                using var frame = new RenderTargetBitmap(new PixelSize(900, 700));
                frame.Render(window);
                frame.Save(Path.Combine(path, "gen2-clock.png"));
            }
        }
        finally { window?.Close(); LocalizedStrings.Instance.SetLanguage(previous); }
    }
}
