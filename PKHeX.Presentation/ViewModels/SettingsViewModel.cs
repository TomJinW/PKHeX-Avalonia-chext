using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Application.Services;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class SettingsViewModel : ViewModelBase, ICloseableDialog
{
    private readonly AppSettings _settings;
    private readonly ISettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly IUiDensityService _uiDensityService;
    private readonly LanguageService _languageService;
    private readonly UpdateCheckCoordinator _updateCoordinator;
    private bool _isLoading;

    public Action? CloseRequested { get; set; }

    /// <summary>Exposed so the settings screen can host a UI-language selector that switches live.</summary>
    public LanguageService LanguageService => _languageService;

    public SettingsViewModel(
        AppSettings settings,
        ISettingsStore settingsStore,
        IThemeService themeService,
        IUiDensityService uiDensityService,
        LanguageService languageService,
        UpdateCheckCoordinator updateCoordinator)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _themeService = themeService;
        _uiDensityService = uiDensityService;
        _languageService = languageService;
        _updateCoordinator = updateCoordinator;
        Load();
    }

    /// <summary>Status line for the manual "Check for Updates" button; empty until a check runs.</summary>
    [ObservableProperty] private string _updateCheckStatus = string.Empty;

    /// <summary>Disables the button and shows the "checking…" state while a manual check is in flight.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
    private bool _isCheckingForUpdates;

    private bool CanCheckForUpdates => !IsCheckingForUpdates;

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        UpdateCheckStatus = LocalizedStrings.Instance["Update_Checking"];
        try
        {
            var result = await _updateCoordinator.CheckNowAsync();
            UpdateCheckStatus = result.Message;
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    // Startup
    [ObservableProperty] private GameVersion _defaultSaveVersion;
    public IReadOnlyList<GameVersion> GameVersions { get; } = Enum.GetValues<GameVersion>();

    [ObservableProperty] private SaveFileLoadSetting _autoLoadMode;
    public IReadOnlyList<SaveFileLoadSetting> LoadModes { get; } = Enum.GetValues<SaveFileLoadSetting>();

    [ObservableProperty] private bool _forceHaX;
    [ObservableProperty] private bool _showChangelog;
    [ObservableProperty] private bool _checkForUpdatesOnStartup;

    [ObservableProperty] private int _gen4NameEncoding;
    [ObservableProperty] private int _gen3NgcNameEncoding;
    [ObservableProperty] private int _gen5NameEncoding;
    [ObservableProperty] private int _gen6NameEncoding;
    [ObservableProperty] private bool _ignoreGen3NameTrashErrors;
    [ObservableProperty] private int _gen1NameEncoding;
    [ObservableProperty] private int _gen2NameEncoding;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Gen3NameEncoding))]
    private bool _gen3ChineseNamesEnabled;

    public int Gen3NameEncoding
    {
        get => Gen3ChineseNamesEnabled ? 1 : 0;
        set => Gen3ChineseNamesEnabled = value == 1;
    }

    // Backup
    [ObservableProperty] private bool _bakEnabled;
    [ObservableProperty] private bool _bakPrompt;

    // SlotWrite
    [ObservableProperty] private bool _setUpdateDex;
    [ObservableProperty] private bool _setUpdatePKM;
    [ObservableProperty] private bool _setUpdateRecords;
    [ObservableProperty] private bool _modifyUnset;
    [ObservableProperty] private bool _warnClosingModified;
    [ObservableProperty] private bool _allowBattleTowerTeamSwap;

    // Sprites
    [ObservableProperty] private SpritePreference _spritePreference;
    public IReadOnlyList<SpritePreference> SpritePreferences { get; } = Enum.GetValues<SpritePreference>();

    // Appearance
    [ObservableProperty] private AppTheme _selectedTheme;
    // Keep the picker intentionally small. HighContrast/System remain readable legacy enum values
    // for old settings files, but are not product-facing appearance choices anymore.
    public IReadOnlyList<AppTheme> Themes { get; } = [AppTheme.Dark, AppTheme.Light];

    [ObservableProperty] private AppDensity _selectedDensity;
    public IReadOnlyList<AppDensity> Densities { get; } = Enum.GetValues<AppDensity>();

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        // Apply (and persist) immediately so the picker previews live, without needing Save.
        // Skip during Load(), which sets the initial value from the already-applied preference.
        if (!_isLoading)
            _themeService.ApplyTheme(value);
    }

    partial void OnSelectedDensityChanged(AppDensity value)
    {
        // Apply (and persist) immediately so the picker previews live, matching the theme
        // preference. Skip during Load(), which reads the already-applied startup value.
        if (!_isLoading)
            _uiDensityService.ApplyDensity(value);
    }

    private void Load()
    {
        _isLoading = true;

        DefaultSaveVersion = _settings.Startup.DefaultSaveVersion;
        AutoLoadMode = _settings.Startup.AutoLoadSaveOnStartup;
        ForceHaX = _settings.Startup.ForceHaXOnLaunch;
        ShowChangelog = _settings.Startup.ShowChangelogOnUpdate;
        CheckForUpdatesOnStartup = _settings.Startup.CheckForUpdatesOnStartup;

        Gen1NameEncoding = _settings.GetNameEncoding(1) switch { PKHeX.Application.Services.Gen12NameEncoding.CknGb2312 => 1, PKHeX.Application.Services.Gen12NameEncoding.SimplifiedChinese => 2, _ => 0 };
        Gen2NameEncoding = _settings.GetNameEncoding(2) switch { PKHeX.Application.Services.Gen12NameEncoding.KoreanChinese => 1, PKHeX.Application.Services.Gen12NameEncoding.SimplifiedChinese => 2, _ => 0 };
        Gen3ChineseNamesEnabled = _settings.Gen3ChineseNamesEnabled;
        IgnoreGen3NameTrashErrors = _settings.IgnoreGen3NameTrashErrors;
        Gen4NameEncoding = (int)_settings.Gen4NameEncoding;
        Gen3NgcNameEncoding = _settings.Gen3NgcChineseNamesEnabled ? 1 : 0;
        Gen5NameEncoding = _settings.Gen5ChineseNamesEnabled ? 1 : 0;
        Gen6NameEncoding = _settings.Gen6ChineseNamesEnabled ? 1 : 0;

        BakEnabled = _settings.Backup.BAKEnabled;
        BakPrompt = _settings.Backup.BAKPrompt;

        SetUpdateDex = _settings.SlotWrite.SetUpdateDex;
        SetUpdatePKM = _settings.SlotWrite.SetUpdatePKM;
        SetUpdateRecords = _settings.SlotWrite.SetUpdateRecords;
        ModifyUnset = _settings.SlotWrite.ModifyUnset;
        WarnClosingModified = _settings.EditorBehavior.WarnClosingModified;
        AllowBattleTowerTeamSwap = _settings.Legality.Game.Gen3.AllowBattleTowerTeamSwap;

        SpritePreference = _settings.Sprite.SpritePreference;
        SelectedTheme = Themes.Contains(_themeService.CurrentTheme)
            ? _themeService.CurrentTheme
            : AppTheme.Dark;
        SelectedDensity = _uiDensityService.CurrentDensity;

        _isLoading = false;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.Startup.DefaultSaveVersion = DefaultSaveVersion;
        _settings.Startup.AutoLoadSaveOnStartup = AutoLoadMode;
        _settings.Startup.ForceHaXOnLaunch = ForceHaX;
        _settings.Startup.ShowChangelogOnUpdate = ShowChangelog;
        _settings.Startup.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;

        _settings.Gen3ChineseNamesEnabled = Gen3ChineseNamesEnabled;
        _settings.IgnoreGen3NameTrashErrors = IgnoreGen3NameTrashErrors;
        _settings.Gen4NameEncoding = Gen4NameEncoding is >= 0 and <= 2 ? (Gen4NameEncoding)Gen4NameEncoding : PKHeX.Application.Services.Gen4NameEncoding.Xzonn;
        _settings.Gen3NgcChineseNamesEnabled = Gen3NgcNameEncoding == 1;
        _settings.Gen5ChineseNamesEnabled = Gen5NameEncoding == 1;
        _settings.Gen6ChineseNamesEnabled = Gen6NameEncoding == 1;
        _settings.Gen1NameEncoding = Gen1NameEncoding switch { 1 => PKHeX.Application.Services.Gen12NameEncoding.CknGb2312, 2 => PKHeX.Application.Services.Gen12NameEncoding.SimplifiedChinese, _ => PKHeX.Application.Services.Gen12NameEncoding.Official };
        _settings.Gen2NameEncoding = Gen2NameEncoding switch { 1 => PKHeX.Application.Services.Gen12NameEncoding.KoreanChinese, 2 => PKHeX.Application.Services.Gen12NameEncoding.SimplifiedChinese, _ => PKHeX.Application.Services.Gen12NameEncoding.Official };

        _settings.Backup.BAKEnabled = BakEnabled;
        _settings.Backup.BAKPrompt = BakPrompt;

        _settings.SlotWrite.SetUpdateDex = SetUpdateDex;
        _settings.SlotWrite.SetUpdatePKM = SetUpdatePKM;
        _settings.SlotWrite.SetUpdateRecords = SetUpdateRecords;
        _settings.SlotWrite.ModifyUnset = ModifyUnset;
        _settings.EditorBehavior.WarnClosingModified = WarnClosingModified;
        _settings.Legality.Game.Gen3.AllowBattleTowerTeamSwap = AllowBattleTowerTeamSwap;

        _settings.Sprite.SpritePreference = SpritePreference;

        _settingsStore.Save(_settings);
        _settings.InitializeCore();

        CloseRequested?.Invoke();
    }
}
