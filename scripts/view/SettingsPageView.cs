#nullable enable

using System;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;

namespace WaterSortGame.View;

public sealed partial class SettingsPageView : Control
{
    public event Action<float>? MusicVolumeChanged;
    public event Action<float>? SfxVolumeChanged;
    public event Action<string>? LanguageChanged;
    public event Action? ResetProgressRequested;
    public event Action? ResetProgressConfirmed;
    public event Action? ResetAllRequested;
    public event Action? ResetAllConfirmed;
    public event Action? BackRequested;

    private VBoxContainer _content = null!;
    private HSlider _musicSlider = null!;
    private Label _musicValueLabel = null!;
    private HSlider _sfxSlider = null!;
    private Label _sfxValueLabel = null!;
    private OptionButton _languageOption = null!;
    private Label _messageLabel = null!;
    private ConfirmationDialog _resetProgressDialog = null!;
    private ConfirmationDialog _resetAllDialog = null!;
    private SettingsPageSnapshot _pendingSnapshot = SettingsPageSnapshot.From(SettingsData.CreateDefault());
    private bool _isReady;
    private bool _applyingSnapshot;
    private LocalizationManager? _localizationManager;

    public void SetLocalizationManager(LocalizationManager localizationManager)
    {
        _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
        RefreshLocalizedText();
    }

    public override void _Ready()
    {
        BuildLayout();
        _isReady = true;
        ApplySnapshot(_pendingSnapshot);
        RefreshLocalizedText();
    }

    public void SetSnapshot(SettingsPageSnapshot snapshot)
    {
        _pendingSnapshot = snapshot.Normalized();
        if (!_isReady)
        {
            return;
        }

        ApplySnapshot(_pendingSnapshot);
    }

    public void ShowMessage(string message)
    {
        if (!_isReady)
        {
            return;
        }

        _messageLabel.Text = message;
        _messageLabel.Visible = !string.IsNullOrWhiteSpace(message);
    }

    public void RefreshLocalizedText()
    {
        if (!_isReady)
        {
            return;
        }

        GetNode<Label>("Panel/Content/Header/TitleLabel").Text = Tr("settings.title");
        GetNode<Button>("Panel/Content/Header/BackButton").Text = Tr("common.back");
        GetNode<Label>("Panel/Content/SettingsList/MusicVolumeRow/MusicVolumeLabel").Text = Tr("settings.music_volume");
        GetNode<Label>("Panel/Content/SettingsList/SfxVolumeRow/SfxVolumeLabel").Text = Tr("settings.sfx_volume");
        GetNode<Label>("Panel/Content/SettingsList/LanguageRow/LanguageLabel").Text = Tr("settings.language");
        _languageOption.SetItemText(0, Tr("settings.language_zh"));
        _languageOption.SetItemText(1, Tr("settings.language_en"));
        GetNode<Button>("Panel/Content/ResetButtons/ResetProgressButton").Text = Tr("settings.reset_progress");
        GetNode<Button>("Panel/Content/ResetButtons/ResetAllSettingsButton").Text = Tr("settings.reset_all");

        _resetProgressDialog.Title = Tr("settings.reset_progress_title");
        _resetProgressDialog.DialogText = Tr("settings.reset_progress_prompt");
        _resetProgressDialog.GetOkButton().Text = Tr("common.confirm");
        _resetProgressDialog.GetCancelButton().Text = Tr("common.cancel");
        _resetAllDialog.Title = Tr("settings.reset_all_title");
        _resetAllDialog.DialogText = Tr("settings.reset_all_prompt");
        _resetAllDialog.GetOkButton().Text = Tr("common.confirm");
        _resetAllDialog.GetCancelButton().Text = Tr("common.cancel");
    }

    private void ApplySnapshot(SettingsPageSnapshot snapshot)
    {
        _applyingSnapshot = true;

        _musicSlider.Value = snapshot.MusicVolume;
        _sfxSlider.Value = snapshot.SfxVolume;
        UpdateVolumeLabel(_musicValueLabel, snapshot.MusicVolume);
        UpdateVolumeLabel(_sfxValueLabel, snapshot.SfxVolume);
        _languageOption.Select(snapshot.Language == "en" ? 1 : 0);

        _applyingSnapshot = false;
    }

    private void BuildLayout()
    {
        ColorRect background = new()
        {
            Name = "Background",
            Color = new Color(0.88f, 0.92f, 0.84f)
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        background.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(background);

        PanelContainer panel = new()
        {
            Name = "Panel"
        };
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        panel.OffsetLeft = 44f;
        panel.OffsetTop = 64f;
        panel.OffsetRight = -44f;
        panel.OffsetBottom = -64f;
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(1f, 0.98f, 0.9f, 0.97f), new Color(0.32f, 0.25f, 0.14f, 0.46f)));
        AddChild(panel);

        _content = new VBoxContainer
        {
            Name = "Content",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _content.AddThemeConstantOverride("separation", 22);
        panel.AddChild(_content);

        _content.AddChild(CreateHeader());

        VBoxContainer settingsList = new()
        {
            Name = "SettingsList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        settingsList.AddThemeConstantOverride("separation", 18);
        _content.AddChild(settingsList);

        settingsList.AddChild(CreateVolumeRow(
            "MusicVolumeRow",
            "MusicVolumeLabel",
            Tr("settings.music_volume"),
            "MusicVolumeSlider",
            "MusicVolumeValueLabel",
            out _musicSlider,
            out _musicValueLabel));
        settingsList.AddChild(CreateVolumeRow(
            "SfxVolumeRow",
            "SfxVolumeLabel",
            Tr("settings.sfx_volume"),
            "SfxVolumeSlider",
            "SfxVolumeValueLabel",
            out _sfxSlider,
            out _sfxValueLabel));
        settingsList.AddChild(CreateLanguageRow());

        _messageLabel = new Label
        {
            Name = "MessageLabel",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(0f, 44f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _messageLabel.AddThemeFontSizeOverride("font_size", 22);
        _messageLabel.AddThemeColorOverride("font_color", new Color(0.22f, 0.14f, 0.08f));
        _content.AddChild(_messageLabel);

        HBoxContainer resetButtons = new()
        {
            Name = "ResetButtons",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        resetButtons.AddThemeConstantOverride("separation", 16);
        _content.AddChild(resetButtons);

        Button resetProgressButton = CreateCommandButton("ResetProgressButton", Tr("settings.reset_progress"));
        resetProgressButton.Pressed += () =>
        {
            ResetProgressRequested?.Invoke();
            _resetProgressDialog.PopupCentered();
        };
        resetButtons.AddChild(resetProgressButton);

        Button resetAllButton = CreateCommandButton("ResetAllSettingsButton", Tr("settings.reset_all"));
        resetAllButton.Pressed += () =>
        {
            ResetAllRequested?.Invoke();
            _resetAllDialog.PopupCentered();
        };
        resetButtons.AddChild(resetAllButton);

        CreateDialogs();

        _musicSlider.ValueChanged += value =>
        {
            float volume = Clamp01((float)value);
            UpdateVolumeLabel(_musicValueLabel, volume);
            if (!_applyingSnapshot)
            {
                MusicVolumeChanged?.Invoke(volume);
            }
        };

        _sfxSlider.ValueChanged += value =>
        {
            float volume = Clamp01((float)value);
            UpdateVolumeLabel(_sfxValueLabel, volume);
            if (!_applyingSnapshot)
            {
                SfxVolumeChanged?.Invoke(volume);
            }
        };

        _languageOption.ItemSelected += index =>
        {
            if (_applyingSnapshot)
            {
                return;
            }

            LanguageChanged?.Invoke(index == 1 ? "en" : "zh");
        };
    }

    private Control CreateHeader()
    {
        HBoxContainer header = new()
        {
            Name = "Header",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        Label title = new()
        {
            Name = "TitleLabel",
            Text = Tr("settings.title"),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 42);
        title.AddThemeColorOverride("font_color", new Color(0.18f, 0.11f, 0.07f));
        header.AddChild(title);

        Button backButton = CreateCommandButton("BackButton", Tr("common.back"));
        backButton.CustomMinimumSize = new Vector2(128f, 58f);
        backButton.Pressed += () => BackRequested?.Invoke();
        header.AddChild(backButton);

        return header;
    }

    private static Control CreateVolumeRow(
        string rowName,
        string labelName,
        string labelText,
        string sliderName,
        string valueLabelName,
        out HSlider slider,
        out Label valueLabel)
    {
        HBoxContainer row = new()
        {
            Name = rowName,
            CustomMinimumSize = new Vector2(0f, 76f),
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 14);

        Label label = CreateRowLabel(labelName, labelText);
        row.AddChild(label);

        slider = new HSlider
        {
            Name = sliderName,
            MinValue = 0d,
            MaxValue = 1d,
            Step = 0.05d,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(260f, 0f)
        };
        row.AddChild(slider);

        valueLabel = new Label
        {
            Name = valueLabelName,
            CustomMinimumSize = new Vector2(68f, 0f),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        valueLabel.AddThemeFontSizeOverride("font_size", 24);
        valueLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.13f, 0.08f));
        row.AddChild(valueLabel);

        return row;
    }

    private Control CreateLanguageRow()
    {
        HBoxContainer row = new()
        {
            Name = "LanguageRow",
            CustomMinimumSize = new Vector2(0f, 76f),
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 14);

        row.AddChild(CreateRowLabel("LanguageLabel", Tr("settings.language")));

        _languageOption = new OptionButton
        {
            Name = "LanguageOptionButton",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(260f, 54f),
            FocusMode = FocusModeEnum.None
        };
        _languageOption.AddItem(Tr("settings.language_zh"), 0);
        _languageOption.AddItem(Tr("settings.language_en"), 1);
        ApplyButtonTheme(_languageOption);
        row.AddChild(_languageOption);

        return row;
    }

    private static Label CreateRowLabel(string name, string text)
    {
        Label label = new()
        {
            Name = name,
            Text = text,
            CustomMinimumSize = new Vector2(150f, 0f),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 26);
        label.AddThemeColorOverride("font_color", new Color(0.18f, 0.12f, 0.07f));
        return label;
    }

    private Button CreateCommandButton(string name, string text)
    {
        Button button = new()
        {
            Name = name,
            Text = text,
            CustomMinimumSize = new Vector2(0f, 58f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None
        };
        ApplyButtonTheme(button);
        return button;
    }

    private void CreateDialogs()
    {
        _resetProgressDialog = new ConfirmationDialog
        {
            Name = "ResetProgressDialog",
            Title = Tr("settings.reset_progress_title"),
            DialogText = Tr("settings.reset_progress_prompt")
        };
        _resetProgressDialog.Confirmed += () =>
        {
            _resetProgressDialog.Hide();
            ResetProgressConfirmed?.Invoke();
        };
        AddChild(_resetProgressDialog);

        _resetAllDialog = new ConfirmationDialog
        {
            Name = "ResetAllSettingsDialog",
            Title = Tr("settings.reset_all_title"),
            DialogText = Tr("settings.reset_all_prompt")
        };
        _resetAllDialog.Confirmed += () =>
        {
            _resetAllDialog.Hide();
            ResetAllConfirmed?.Invoke();
        };
        AddChild(_resetAllDialog);
    }

    private static void UpdateVolumeLabel(Label label, float value)
    {
        label.Text = Clamp01(value).ToString("0.00");
    }

    private static float Clamp01(float value)
    {
        return float.IsNaN(value) ? 0f : float.Clamp(value, 0f, 1f);
    }

    private string Tr(string key)
    {
        return _localizationManager?.Tr(key) ?? LocalizationManager.GetText(key);
    }

    private static void ApplyButtonTheme(Button button)
    {
        Color textColor = new(0.18f, 0.12f, 0.07f);
        button.AddThemeFontSizeOverride("font_size", 24);
        button.AddThemeColorOverride("font_color", textColor);
        button.AddThemeColorOverride("font_hover_color", textColor);
        button.AddThemeColorOverride("font_pressed_color", textColor);
        button.AddThemeColorOverride("font_focus_color", textColor);
        button.AddThemeStyleboxOverride("normal", CreatePanelStyle(new Color(0.94f, 0.8f, 0.47f, 0.98f), new Color(0.36f, 0.22f, 0.1f, 0.7f)));
        button.AddThemeStyleboxOverride("hover", CreatePanelStyle(new Color(1f, 0.87f, 0.55f, 1f), new Color(0.42f, 0.25f, 0.11f, 0.8f)));
        button.AddThemeStyleboxOverride("pressed", CreatePanelStyle(new Color(0.84f, 0.67f, 0.34f, 1f), new Color(0.32f, 0.18f, 0.08f, 0.85f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static StyleBoxFlat CreatePanelStyle(Color backgroundColor, Color borderColor)
    {
        return new StyleBoxFlat
        {
            BgColor = backgroundColor,
            BorderColor = borderColor,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 20f,
            ContentMarginTop = 20f,
            ContentMarginRight = 20f,
            ContentMarginBottom = 20f
        };
    }
}

public readonly record struct SettingsPageSnapshot(float MusicVolume, float SfxVolume, string Language)
{
    public static SettingsPageSnapshot From(SettingsData settings)
    {
        settings.Normalize();
        return new SettingsPageSnapshot(settings.MusicVolume, settings.SfxVolume, settings.Language);
    }

    public SettingsPageSnapshot Normalized()
    {
        SettingsData settings = new()
        {
            MusicVolume = MusicVolume,
            SfxVolume = SfxVolume,
            Language = Language
        };
        settings.Normalize();
        return From(settings);
    }
}
