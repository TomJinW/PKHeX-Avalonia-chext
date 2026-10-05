using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class TrainerEditorViewModel
{
    public bool HasGen2Clock => _sav is SAV2;
    public bool HasGen2ClockPassword => _sav is SAV2 { Japanese: false };

    private readonly IDialogService? _trainerDialogs;
    [ObservableProperty] private string _gen2ClockStatus = string.Empty;
    [ObservableProperty] private string _gen2ClockPassword = string.Empty;

    private bool CanCalculateGen2ClockPassword() => HasGen2ClockPassword && IsDisplayIdValid && IsTrainerNameEncodingValid;

    [RelayCommand(CanExecute = nameof(CanCalculateGen2ClockPassword))]
    private void CalculateGen2ClockPassword()
    {
        if (_sav is not SAV2 { Japanese: false } save || !IsDisplayIdValid || !IsTrainerNameEncodingValid)
            return;

        // Include pending edits without touching the loaded save. In particular, preserve
        // unedited raw OT bytes, which can use a third-party Chinese character table.
        var preview = new SAV2(save.Data.ToArray(), (LanguageID)save.Language, save.Version);
        if (TrainerName != DecodedTrainerName)
        {
            if (UsesGbTrainerName)
                Gen12ChineseNames.TryEncode(TrainerName, preview.OriginalTrainerTrash, preview.Japanese, _gbTrainerEncoding);
            else preview.OT = TrainerName;
        }
        preview.SetDisplayID(DisplayTid, DisplaySid);
        preview.Money = Money;
        Gen2ClockPassword = preview.ResetKey.ToString("D5", CultureInfo.InvariantCulture);
    }

    private void InvalidateGen2ClockPassword()
    {
        Gen2ClockPassword = string.Empty;
        CalculateGen2ClockPasswordCommand.NotifyCanExecuteChanged();
    }

    partial void OnTrainerNameChanged(string value)
    {
        InvalidateGen2ClockPassword();
        OnPropertyChanged(nameof(IsTrainerNameEncodingValid));
        OnPropertyChanged(nameof(TrainerNameEncodingError));
        SaveCommand.NotifyCanExecuteChanged();
    }
    partial void OnMoneyChanged(uint value) => InvalidateGen2ClockPassword();

    private bool CanResetGen2Clock() => HasGen2Clock && _trainerDialogs is not null;

    [RelayCommand(CanExecute = nameof(CanResetGen2Clock))]
    private async Task ResetGen2ClockAsync()
    {
        if (_sav is not SAV2 save || _trainerDialogs is null)
            return;
        var strings = LocalizedStrings.Instance;
        if (!await _trainerDialogs.ShowConfirmationAsync(strings["Gen2Clock_Reset"],
                strings["Gen2Clock_Confirm"], strings["Gen2Clock_Reset"], strings["Common_Cancel"]))
            return;
        save.ResetRTC();
        save.State.Edited = true;
        Gen2ClockStatus = strings["Gen2Clock_Done"];
    }
}
