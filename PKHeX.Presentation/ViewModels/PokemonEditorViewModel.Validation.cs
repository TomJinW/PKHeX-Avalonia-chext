
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class PokemonEditorViewModel
{
    public bool ShowLegalityStatus => Species != 0 && !IsHaXMode;
    [ObservableProperty]
    private bool _isLegal;

    [ObservableProperty]
    private string _legalityReport = string.Empty;

    private void Validate()
    {
        if (!CheckNameEncoding())
        {
            IsLegal = false;
            LegalityReport = NameEncodingError;
            return;
        }
        var pk = PreparePKM();
        var la = SaveLegalityCheck.Evaluate(pk, _sav, _nameEncoding, _nameSettings.IgnoreGen3NameTrashErrors, _nameSettings.Gen5ChineseNamesEnabled, _nameSettings.Gen6ChineseNamesEnabled, _nameSettings.Gen4NameEncoding, _nameSettings.Gen3NgcChineseNamesEnabled);
        IsLegal = _haXMode || la.Valid;
        LegalityReport = la.ReportText;
    }

    [RelayCommand]
    private async Task ShowLegalityAsync()
    {
        Validate();
        await _windowService.ShowDialogAsync(new LegalityViewModel(LegalityReport), LocalizedStrings.Instance["PokemonEditor_LegalityAnalysisTitle"]);
    }
}
