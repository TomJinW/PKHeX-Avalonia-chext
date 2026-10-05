using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class PokemonEditorViewModel
{
    private Gen12NameEncoding _nameEncoding;
    private Gen4NameEncoding _gen4NameEncoding;
    private bool UsesGen4ChineseNames => Gen4ChineseNames.Applies(_pk, _sav, _gen4NameEncoding);
    private bool _useGen3ChineseNames;
    public bool HasNameEncodingChoice => (_sav is SAV4 && _pk is PK4) || HasLegacyNames || (_sav is SAV3 { Japanese: false } && _pk is PK3 { Japanese: false });
    private bool UsesGen3ChineseNames => Gen3ChineseNames.Applies(_pk, _sav, _useGen3ChineseNames);
    public bool HasLegacyNames => _sav.Generation is 1 or 2 && _pk is GBPKML;
    public bool UsesChineseNames => UsesGen4ChineseNames || UsesGen3ChineseNames || (HasLegacyNames && Gen12ChineseNames.Applies(_pk, _nameEncoding));
    public string NameEncodingDescription => LocalizedStrings.Instance[
        _sav.Generation == 4 ? (_gen4NameEncoding switch { Gen4NameEncoding.YYJOY => "Gen4Names_YYJOY", Gen4NameEncoding.Xzonn => "Gen4Names_Xzonn", _ => "Gen12Names_Official" })
            : _sav.Generation == 3 ? (UsesGen3ChineseNames ? "Gen3Names_ChineseActive" : "Gen3Names_OfficialActive")
            : UsesChineseNames ? (_nameEncoding == Gen12NameEncoding.CknGb2312 ? "Gen1Names_CKN" : _nameEncoding == Gen12NameEncoding.KoreanChinese ? "Gen2Names_Korean" : _sav.Generation == 1 ? "Gen1Names_US" : "Gen2Names_US") : "Gen12Names_OfficialActive"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNameEncodingError))]
    private string _nameEncodingError = string.Empty;
    public bool HasNameEncodingError => NameEncodingError.Length != 0;

    /// <summary>Reinterprets raw names after Settings closes, without transcoding either buffer.</summary>
    public void RefreshNameEncoding()
    {
        var encoding = _sav.Generation is 1 or 2 ? _nameSettings.GetNameEncoding(_sav.Generation) : Gen12NameEncoding.Official;
        if (_gen4NameEncoding == _nameSettings.Gen4NameEncoding && _nameEncoding == encoding && _useGen3ChineseNames == _nameSettings.Gen3ChineseNamesEnabled)
        {
            RefreshNameLegality();
            return;
        }
        _gen4NameEncoding = _nameSettings.Gen4NameEncoding;
        _useGen3ChineseNames = _nameSettings.Gen3ChineseNamesEnabled;
        _nameEncoding = encoding;
        bool loading = _isLoading;
        _isLoading = true;
        Nickname = DecodedNickname;
        OriginalTrainerName = DecodedTrainerName;
        _isLoading = loading;
        NameEncodingError = string.Empty;
        NotifyNameEncoding();
        RefreshNameLegality();
    }

    private void RefreshNameLegality()
    {
        var result = SaveLegalityCheck.Evaluate(_pk, _sav, _nameEncoding, _nameSettings.IgnoreGen3NameTrashErrors, _nameSettings.Gen5ChineseNamesEnabled, _nameSettings.Gen6ChineseNamesEnabled, _nameSettings.Gen4NameEncoding, _nameSettings.Gen3NgcChineseNamesEnabled);
        IsLegal = _haXMode || result.Valid;
        LegalityReport = result.ReportText;
    }

    private void NotifyNameEncoding()
    {
        OnPropertyChanged(nameof(HasLegacyNames));
        OnPropertyChanged(nameof(HasNameEncodingChoice));
        OnPropertyChanged(nameof(UsesChineseNames));
        OnPropertyChanged(nameof(NameEncodingDescription));
    }

    public bool CheckNameEncoding()
    {
        // Unedited unknown bytes and bytes after the terminator must round-trip exactly.
        bool valid = !UsesChineseNames ||
            (CanEncodeName(Nickname, _pk.NicknameTrash) && CanEncodeName(OriginalTrainerName, _pk.OriginalTrainerTrash));
        NameEncodingError = valid ? string.Empty : LocalizedStrings.Instance[UsesGen4ChineseNames ? "Gen4Names_Invalid" : UsesGen3ChineseNames ? "Gen3Names_Invalid" : "Gen12Names_Invalid"];
        return valid;
    }

    private string DecodedNickname => UsesGen4ChineseNames ? DecodeName(_pk.NicknameTrash) : UsesGen3ChineseNames ? Gen3ChineseNames.Decode(_pk.NicknameTrash) : Gen12ChineseNames.Nickname(_pk, _nameEncoding);
    private string DecodedTrainerName => UsesGen4ChineseNames ? DecodeName(_pk.OriginalTrainerTrash) : UsesGen3ChineseNames ? Gen3ChineseNames.Decode(_pk.OriginalTrainerTrash) : Gen12ChineseNames.OriginalTrainer(_pk, _nameEncoding);
    private string DecodeName(ReadOnlySpan<byte> bytes) => UsesGen4ChineseNames ? Gen4ChineseNames.Decode(bytes, _gen4NameEncoding) : UsesGen3ChineseNames ? Gen3ChineseNames.Decode(bytes) : Gen12ChineseNames.Decode(bytes, _pk.Japanese, _nameEncoding);
    private bool EncodeName(string value, Span<byte> bytes) => UsesGen4ChineseNames ? Gen4ChineseNames.TryEncode(value, bytes, _gen4NameEncoding, bytes.Length == _pk.NicknameTrash.Length ? Legal.GetMaxLengthNickname(4, (LanguageID)_pk.Language) : Legal.GetMaxLengthOT(4, (LanguageID)_pk.Language)) : UsesGen3ChineseNames ? Gen3ChineseNames.TryEncode(value, bytes) : Gen12ChineseNames.TryEncode(value, bytes, _pk.Japanese, _nameEncoding);

    private bool CanEncodeName(string value, ReadOnlySpan<byte> original)
        => value == DecodeName(original) ||
           EncodeName(value, new byte[original.Length]);

    private void ApplyChineseNames()
    {
        if (UsesGen4ChineseNames) _pk.IsNicknamed = IsNicknamed;
        WriteChangedName(Nickname, _pk.NicknameTrash);
        WriteChangedName(OriginalTrainerName, _pk.OriginalTrainerTrash);
    }

    private void WriteChangedName(string value, Span<byte> original)
    {
        if (value != DecodeName(original))
            EncodeName(value, original);
    }

    partial void OnOriginalTrainerNameChanged(string value)
    {
        if (!_isLoading && UsesChineseNames) Validate();
    }
}
