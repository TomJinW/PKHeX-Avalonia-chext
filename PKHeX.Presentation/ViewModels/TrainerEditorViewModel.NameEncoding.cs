using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class TrainerEditorViewModel
{
    private readonly AppSettings _nameSettings;
    private bool _useChineseTrainerName;
    private Gen12NameEncoding _gbTrainerEncoding;
    private bool UsesGbTrainerName => _sav is SAV1 or SAV2 && _gbTrainerEncoding != Gen12NameEncoding.Official;
    private bool GbJapanese => _sav is SAV1 { Japanese: true } or SAV2 { Japanese: true };
    private Gen4NameEncoding _gen4TrainerEncoding;
    private bool UsesGen4TrainerName => _sav is SAV4 && Gen4ChineseNames.Enabled(_gen4TrainerEncoding);
    private bool UsesChineseTrainerName => UsesGbTrainerName || UsesGen4TrainerName || _useChineseTrainerName && _sav is SAV3 { Japanese: false };
    private string DecodedTrainerName => UsesGbTrainerName ? Gen12ChineseNames.Decode(TranslatedSaveNames.TrainerBytes(_sav), GbJapanese, _gbTrainerEncoding) : UsesGen4TrainerName && _sav is SAV4 ds ? Gen4ChineseNames.Decode(ds.OriginalTrainerTrash, _gen4TrainerEncoding) : UsesChineseTrainerName && _sav is SAV3 save
        ? Gen3ChineseNames.Decode(save.SmallBlock.OriginalTrainerTrash[..7]) : _sav.OT;

    public bool IsTrainerNameEncodingValid => !UsesChineseTrainerName || TrainerName == DecodedTrainerName
        || (UsesGbTrainerName ? Gen12ChineseNames.TryEncode(TrainerName, new byte[TranslatedSaveNames.TrainerBytes(_sav).Length], GbJapanese, _gbTrainerEncoding) : UsesGen4TrainerName ? Gen4ChineseNames.TryEncode(TrainerName, new byte[16], _gen4TrainerEncoding, _sav.MaxStringLengthTrainer) : Gen3ChineseNames.TryEncode(TrainerName, new byte[7]));
    public string TrainerNameEncodingError => IsTrainerNameEncodingValid ? string.Empty
        : LocalizedStrings.Instance[UsesGbTrainerName ? "Gen12Names_Invalid" : UsesGen4TrainerName ? "Gen4Names_Invalid" : "Gen3Names_Invalid"];

    public void RefreshNameEncoding()
    {
        if (_gbTrainerEncoding == _nameSettings.GetNameEncoding(_sav.Generation) && _gen4TrainerEncoding == _nameSettings.Gen4NameEncoding && _useChineseTrainerName == _nameSettings.Gen3ChineseNamesEnabled) return;
        bool unchanged = TrainerName == DecodedTrainerName;
        _gbTrainerEncoding = _nameSettings.GetNameEncoding(_sav.Generation);
        _gen4TrainerEncoding = _nameSettings.Gen4NameEncoding;
        _useChineseTrainerName = _nameSettings.Gen3ChineseNamesEnabled;
        if (unchanged) TrainerName = DecodedTrainerName;
        OnPropertyChanged(nameof(IsTrainerNameEncodingValid));
        OnPropertyChanged(nameof(TrainerNameEncodingError));
        SaveCommand.NotifyCanExecuteChanged();
    }
}
