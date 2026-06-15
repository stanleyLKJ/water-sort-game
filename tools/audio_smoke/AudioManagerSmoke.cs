#nullable enable

using System;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;
using WaterSortGame.View;

public sealed partial class AudioManagerSmoke : Node
{
    private const string AudioSavePath = "user://audio_manager_smoke.json";

    private MainFlowController _main = null!;

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
            GD.Print("AUDIO_MANAGER_SMOKE_OK");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError($"AUDIO_MANAGER_SMOKE_FAILED: {ex}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        DeleteUserFile(AudioSavePath);
        await AssertAudioManagerNoOpsWithoutResourcesAsync();
        WriteInitialSave();

        PackedScene packedMain = GD.Load<PackedScene>("res://main.tscn");
        _main = packedMain.Instantiate<MainFlowController>();
        _main.SavePathOverride = AudioSavePath;
        AddChild(_main);
        await NextFrame();

        AudioManager audio = GetAudioManager();
        AssertNearly(audio.MusicVolume, 0.3f, "AudioManager should receive initial music_volume from SettingsData.");
        AssertNearly(audio.SfxVolume, 0.4f, "AudioManager should receive initial sfx_volume from SettingsData.");
        Assert(audio.GetBgmPlayerCount() == 1, "AudioManager should create exactly one BGM player.");
        Assert(audio.GetSfxPlayerCount() == 4, "AudioManager should create exactly four SFX players.");
        Assert(CountAudioManagers(_main) == 1, "main.tscn should contain exactly one AudioManager node.");
        Assert(audio.HasBgmStream, "AudioManager should load a formal BGM stream.");
        Assert(audio.CurrentBgmPath == "res://assets/audio/bgm_pure_morning.mp3", $"BGM path should be bgm_pure_morning.mp3. Actual: {audio.CurrentBgmPath}.");
        Assert(audio.HasClickStream && audio.HasPourStream && audio.HasBlockedStream && audio.HasSuccessStream, "AudioManager should load the four formal SFX resources.");

        SettingsPageView settings = await OpenSettingsAsync();
        ChangeSlider(settings, "Panel/Content/SettingsList/MusicVolumeRow/MusicVolumeSlider", 0.55d);
        await NextFrame();
        AssertNearly(audio.MusicVolume, 0.55f, "Changing SettingsPage music_volume should immediately update AudioManager.");
        AssertNearly(GetSaveData().Settings.MusicVolume, 0.55f, "Changing SettingsPage music_volume should still update SaveData.");

        ChangeSlider(settings, "Panel/Content/SettingsList/SfxVolumeRow/SfxVolumeSlider", 0.65d);
        await NextFrame();
        AssertNearly(audio.SfxVolume, 0.65f, "Changing SettingsPage sfx_volume should immediately update AudioManager.");
        AssertNearly(GetSaveData().Settings.SfxVolume, 0.65f, "Changing SettingsPage sfx_volume should still update SaveData.");

        PressButton(settings, "Panel/Content/ResetButtons/ResetProgressButton");
        await NextFrame();
        settings.GetNode<ConfirmationDialog>("ResetProgressDialog").EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        await NextFrame();
        AssertNearly(audio.MusicVolume, 0.55f, "ResetProgressOnly should keep current music volume in AudioManager.");
        AssertNearly(audio.SfxVolume, 0.65f, "ResetProgressOnly should keep current sfx volume in AudioManager.");
        AssertNoLevelCompletedSideEffects("after reset progress from SettingsPage");

        PrepareDataForResetAll();
        PressButton(settings, "Panel/Content/ResetButtons/ResetAllSettingsButton");
        await NextFrame();
        settings.GetNode<ConfirmationDialog>("ResetAllSettingsDialog").EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        await NextFrame();
        AssertNearly(audio.MusicVolume, SettingsData.DefaultMusicVolume, "ResetAllSettings should restore default music volume in AudioManager.");
        AssertNearly(audio.SfxVolume, SettingsData.DefaultSfxVolume, "ResetAllSettings should restore default sfx volume in AudioManager.");
        AssertNoLevelCompletedSideEffects("after reset all from SettingsPage");

        PressButton(settings, "Panel/Content/Header/BackButton");
        await NextFrame();
        AssertActiveScene<HomeGardenView>("Settings BackButton should return to HomeGarden.");
        Assert(audio.GetBgmPlayerCount() == 1, "Returning from SettingsPage should not create another BGM player.");
        Assert(CountAudioManagers(_main) == 1, "Page switching should not create duplicate AudioManager nodes.");

        DeleteUserFile(AudioSavePath);
    }

    private async Task AssertAudioManagerNoOpsWithoutResourcesAsync()
    {
        AudioManager audio = new()
        {
            Name = "AudioManagerSmokeLocal",
            BgmPath = "res://assets/audio/__missing_bgm_a.mp3",
            ClickPath = "res://assets/audio/__missing_click.wav",
            PourPath = "res://assets/audio/__missing_pour.wav",
            BlockedPath = "res://assets/audio/__missing_blocked.wav",
            SuccessPath = "res://assets/audio/__missing_success.wav"
        };
        AddChild(audio);
        await NextFrame();

        audio.ApplySettings(new SettingsData { MusicVolume = 0.3f, SfxVolume = 0.4f, Language = "zh" });
        AssertNearly(audio.MusicVolume, 0.3f, "ApplySettings should store music volume.");
        AssertNearly(audio.SfxVolume, 0.4f, "ApplySettings should store sfx volume.");

        audio.SetMusicVolume(0f);
        AssertNearly(audio.MusicVolumeDb, -80f, "music_volume 0 should map to mute dB without Log(0).");
        audio.SetSfxVolume(0f);
        AssertNearly(audio.SfxVolumeDb, -80f, "sfx_volume 0 should map to mute dB without Log(0).");
        audio.PlayClick();
        audio.PlayPour();
        audio.PlayBlocked();
        audio.PlaySuccess();
        Assert(audio.GetBgmPlayerCount() == 1, "Standalone AudioManager should keep one BGM player.");

        RemoveChild(audio);
        audio.QueueFree();
        await NextFrame();
    }

    private async Task<SettingsPageView> OpenSettingsAsync()
    {
        HomeGardenView homeGarden = AssertActiveScene<HomeGardenView>("main.tscn should start in HomeGarden.");
        PressButton(homeGarden, "ButtonRoot/SettingsButton");
        await NextFrame();
        return AssertActiveScene<SettingsPageView>("SettingsButton should open SettingsPage.");
    }

    private static void WriteInitialSave()
    {
        SaveSystem saveSystem = new();
        SaveData data = saveSystem.LoadOrCreate(AudioSavePath);
        data.Settings.MusicVolume = 0.3f;
        data.Settings.SfxVolume = 0.4f;
        data.Settings.Language = "zh";
        data.LevelProgressByFlower["pink_rose"] = 2;
        data.WarehouseInventoryByFlower["pink_rose"].SeedCount = 1;
        data.WarehouseInventoryByFlower["pink_rose"].PotionCount = 1;
        data.SetHomeSlot(0, new[] { "pink_rose" });
        data.Normalize();
        if (!saveSystem.ImmediateSave())
        {
            throw new InvalidOperationException("Could not write initial audio smoke save.");
        }
    }

    private void PrepareDataForResetAll()
    {
        SaveData data = GetSaveData();
        data.Settings.MusicVolume = 0.2f;
        data.Settings.SfxVolume = 0.25f;
        data.Settings.Language = "en";
        data.LevelProgressByFlower["pink_rose"] = 3;
        data.WarehouseInventoryByFlower["pink_rose"].SeedCount = 2;
        data.WarehouseInventoryByFlower["pink_rose"].PotionCount = 2;
        data.SetHomeSlot(0, new[] { "pink_rose" });
        data.Normalize();
        GetState().ApplySaveData(data);
    }

    private void AssertNoLevelCompletedSideEffects(string label)
    {
        RunSessionState state = GetState();
        foreach (string flowerId in RunSessionState.OpenFlowerIds)
        {
            Assert(state.GetCompletedLevelCount(flowerId) == 0, $"{label}: {flowerId} progress should stay 0 after reset.");
        }

        Assert(!state.PendingPlanting, $"{label}: PendingPlanting should stay false.");
        Assert(!state.HasSeed, $"{label}: HasSeed should stay false.");
        Assert(!state.HasPotion, $"{label}: HasPotion should stay false.");
    }

    private AudioManager GetAudioManager()
    {
        return _main.GetNode<AudioManager>("AudioManager");
    }

    private RunSessionState GetState()
    {
        FieldInfo? field = typeof(MainFlowController).GetField("_runSessionState", BindingFlags.Instance | BindingFlags.NonPublic);
        object? value = field?.GetValue(_main);
        return value as RunSessionState ?? throw new InvalidOperationException("MainFlowController._runSessionState should be RunSessionState.");
    }

    private SaveData GetSaveData()
    {
        FieldInfo? field = typeof(MainFlowController).GetField("_saveData", BindingFlags.Instance | BindingFlags.NonPublic);
        object? value = field?.GetValue(_main);
        return value as SaveData ?? throw new InvalidOperationException("MainFlowController._saveData should be SaveData.");
    }

    private T AssertActiveScene<T>(string message) where T : Node
    {
        Node sceneHost = _main.GetNode<Node>("SceneHost");
        Assert(sceneHost.GetChildCount() == 1, $"SceneHost should have exactly one active child. Actual: {sceneHost.GetChildCount()}.");
        Node activeScene = sceneHost.GetChild(0);
        Assert(activeScene is T, $"{message} Actual active scene: {activeScene.GetType().Name} / {activeScene.Name}.");
        return (T)activeScene;
    }

    private static int CountAudioManagers(Node root)
    {
        int count = root is AudioManager ? 1 : 0;
        foreach (Node child in root.GetChildren())
        {
            count += CountAudioManagers(child);
        }

        return count;
    }

    private static void ChangeSlider(Node settings, string path, double value)
    {
        HSlider slider = settings.GetNode<HSlider>(path);
        slider.Value = value;
        slider.EmitSignal(Godot.Range.SignalName.ValueChanged, value);
    }

    private static void PressButton(Node scene, string relativePath)
    {
        Button button = scene.GetNode<Button>(relativePath);
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void DeleteUserFile(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            return;
        }

        Error error = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        if (error != Error.Ok)
        {
            throw new InvalidOperationException($"Could not delete {path}. Error: {error}");
        }
    }

    private static void AssertNearly(float actual, float expected, string message)
    {
        Assert(Math.Abs(actual - expected) <= 0.001f, $"{message} Expected: {expected:0.00}. Actual: {actual:0.00}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
