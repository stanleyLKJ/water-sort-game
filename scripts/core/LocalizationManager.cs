#nullable enable

using System;
using System.Globalization;
using Godot;
using WaterSortGame.Model;

namespace WaterSortGame.Core;

public sealed partial class LocalizationManager : Node
{
    public event Action<string>? LanguageChanged;

    public string CurrentLanguage { get; private set; } = SettingsData.DefaultLanguage;

    public void ApplySettings(SettingsData? settings)
    {
        SetLanguage(settings?.Language);
    }

    public void SetLanguage(string? language)
    {
        string normalized = NormalizeLanguage(language);
        if (CurrentLanguage == normalized)
        {
            return;
        }

        CurrentLanguage = normalized;
        LanguageChanged?.Invoke(CurrentLanguage);
    }

    public string Tr(string key)
    {
        return GetText(key, CurrentLanguage);
    }

    public static string GetText(string key, string? language = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            GD.PushWarning("LocalizationManager received an empty text key.");
            return string.Empty;
        }

        if (!LocalizedTextTable.Entries.TryGetValue(key, out var translations))
        {
            GD.PushWarning($"Localization key is missing: {key}");
            return key;
        }

        string normalizedLanguage = NormalizeLanguage(language);
        if (translations.TryGetValue(normalizedLanguage, out string? localized) && !string.IsNullOrEmpty(localized))
        {
            return localized;
        }

        if (translations.TryGetValue(SettingsData.DefaultLanguage, out string? fallback) && !string.IsNullOrEmpty(fallback))
        {
            GD.PushWarning($"Localization language '{normalizedLanguage}' is missing for key '{key}'. Falling back to {SettingsData.DefaultLanguage}.");
            return fallback;
        }

        GD.PushWarning($"Localization text is missing for key '{key}'.");
        return key;
    }

    public string TrFormat(string key, params object[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, Tr(key), args);
    }

    public static string NormalizeLanguage(string? language)
    {
        return language == "en" ? "en" : SettingsData.DefaultLanguage;
    }
}
