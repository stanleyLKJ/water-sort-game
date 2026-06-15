#nullable enable

using System.Text.Json.Serialization;

namespace WaterSortGame.Model;

public sealed class SettingsData
{
    public const float DefaultMusicVolume = 0.8f;
    public const float DefaultSfxVolume = 0.8f;
    public const string DefaultLanguage = "zh";

    [JsonPropertyName("music_volume")]
    public float MusicVolume { get; set; } = DefaultMusicVolume;

    [JsonPropertyName("sfx_volume")]
    public float SfxVolume { get; set; } = DefaultSfxVolume;

    [JsonPropertyName("language")]
    public string Language { get; set; } = DefaultLanguage;

    public static SettingsData CreateDefault()
    {
        return new SettingsData();
    }

    public SettingsData Clone()
    {
        return new SettingsData
        {
            MusicVolume = MusicVolume,
            SfxVolume = SfxVolume,
            Language = Language
        };
    }

    public void Normalize()
    {
        MusicVolume = Clamp01(MusicVolume);
        SfxVolume = Clamp01(SfxVolume);

        if (string.IsNullOrWhiteSpace(Language))
        {
            Language = DefaultLanguage;
        }
        else if (Language != "zh" && Language != "en")
        {
            Language = DefaultLanguage;
        }
    }

    private static float Clamp01(float value)
    {
        if (float.IsNaN(value))
        {
            return 0f;
        }

        return float.Clamp(value, 0f, 1f);
    }
}
