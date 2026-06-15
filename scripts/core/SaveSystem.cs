#nullable enable

using System;
using System.Text.Json;
using Godot;
using WaterSortGame.Model;

namespace WaterSortGame.Core;

public sealed partial class SaveSystem : Node
{
    public const string DefaultSavePath = "user://save_data.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private Timer? _saveTimer;
    private bool _saveRequested;
    private bool _hasLoaded;

    public string SavePath { get; private set; } = DefaultSavePath;

    public double SaveDebounceSeconds { get; set; } = 0.25d;

    public SaveData CurrentData { get; private set; } = SaveData.CreateDefault();

    public override void _Ready()
    {
        EnsureSaveTimer();
    }

    public SaveData LoadOrCreate(string? savePath = null)
    {
        if (!string.IsNullOrWhiteSpace(savePath))
        {
            SavePath = savePath;
        }

        if (!Godot.FileAccess.FileExists(SavePath))
        {
            CurrentData = SaveData.CreateDefault();
            _hasLoaded = true;
            return CurrentData;
        }

        try
        {
            string json = Godot.FileAccess.GetFileAsString(SavePath);
            SaveData? data = JsonSerializer.Deserialize<SaveData>(json, SerializerOptions);
            if (data == null)
            {
                throw new InvalidOperationException("Save file deserialized to null.");
            }

            data.Normalize();
            CurrentData = data;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"SaveSystem failed to read {SavePath}; using default save data. {ex.Message}");
            CurrentData = SaveData.CreateDefault();
        }

        _hasLoaded = true;
        return CurrentData;
    }

    public void RequestSave()
    {
        EnsureLoaded();
        _saveRequested = true;

        if (!IsInsideTree())
        {
            ImmediateSave();
            return;
        }

        EnsureSaveTimer();
        _saveTimer!.Stop();
        _saveTimer.Start(SaveDebounceSeconds);
    }

    public bool ImmediateSave()
    {
        EnsureLoaded();
        CurrentData.Normalize();

        try
        {
            string json = JsonSerializer.Serialize(CurrentData, SerializerOptions);
            using Godot.FileAccess? file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushWarning($"SaveSystem failed to open {SavePath} for write. Error: {Godot.FileAccess.GetOpenError()}");
                return false;
            }

            file.StoreString(json);
            _saveRequested = false;
            _saveTimer?.Stop();
            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"SaveSystem failed to write {SavePath}. {ex.Message}");
            return false;
        }
    }

    public void ResetProgressOnly()
    {
        EnsureLoaded();
        SettingsData preservedSettings = CurrentData.Settings.Clone();
        CurrentData = SaveData.CreateDefault();
        CurrentData.Settings = preservedSettings;
        CurrentData.Normalize();
        RequestSave();
    }

    public void ResetAllSettings()
    {
        CurrentData = SaveData.CreateDefault();
        _hasLoaded = true;
        RequestSave();
    }

    private void EnsureSaveTimer()
    {
        if (_saveTimer != null)
        {
            return;
        }

        _saveTimer = new Timer
        {
            OneShot = true,
            Autostart = false
        };
        _saveTimer.Timeout += OnSaveTimerTimeout;
        AddChild(_saveTimer);
    }

    private void OnSaveTimerTimeout()
    {
        if (_saveRequested)
        {
            ImmediateSave();
        }
    }

    private void EnsureLoaded()
    {
        if (!_hasLoaded)
        {
            LoadOrCreate();
        }
    }
}
