#nullable enable

using System;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;
using WaterSortGame.View;

public sealed partial class SettingsPageSmoke : Node
{
    private const string SettingsSavePath = "user://settings_page_smoke.json";

    private MainFlowController _main = null!;

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
            GD.Print("SETTINGS_PAGE_SMOKE_OK");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError($"SETTINGS_PAGE_SMOKE_FAILED: {ex}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        DeleteUserFile(SettingsSavePath);
        WriteInitialSave();

        PackedScene packedMain = GD.Load<PackedScene>("res://main.tscn");
        _main = packedMain.Instantiate<MainFlowController>();
        _main.SavePathOverride = SettingsSavePath;
        AddChild(_main);
        await NextFrame();

        HomeGardenView homeGarden = AssertActiveScene<HomeGardenView>("main.tscn should start in HomeGarden.");
        AssertSettingsButton(homeGarden);
        PressButton(homeGarden, "ButtonRoot/SettingsButton");
        await NextFrame();

        SettingsPageView settings = AssertActiveScene<SettingsPageView>("SettingsButton should open SettingsPage.");
        AssertSettingsSnapshot(settings, 0.3f, 0.4f, "en");

        ChangeSlider(settings, "Panel/Content/SettingsList/MusicVolumeRow/MusicVolumeSlider", 0.6d);
        await NextFrame();
        AssertNearly(GetSaveData().Settings.MusicVolume, 0.6f, "music_volume should update in SaveData.");
        await AssertSavedSettingsAsync(0.6f, 0.4f, "en");

        ChangeSlider(settings, "Panel/Content/SettingsList/SfxVolumeRow/SfxVolumeSlider", 0.7d);
        await NextFrame();
        AssertNearly(GetSaveData().Settings.SfxVolume, 0.7f, "sfx_volume should update in SaveData.");
        await AssertSavedSettingsAsync(0.6f, 0.7f, "en");

        OptionButton language = settings.GetNode<OptionButton>("Panel/Content/SettingsList/LanguageRow/LanguageOptionButton");
        language.Select(0);
        language.EmitSignal(OptionButton.SignalName.ItemSelected, 0);
        await NextFrame();
        Assert(GetSaveData().Settings.Language == "zh", "language should update to zh in SaveData.");
        await AssertSavedSettingsAsync(0.6f, 0.7f, "zh");

        SaveData beforeResetProgress = GetSaveData();
        Assert(beforeResetProgress.LevelProgressByFlower["pink_rose"] > 0, "Initial pink_rose progress should be > 0 before reset progress.");
        Assert(beforeResetProgress.WarehouseInventoryByFlower["pink_rose"].SeedCount == 2, "Initial pink_rose seed_count should be 2 before reset progress.");
        Assert(beforeResetProgress.HomeSlotsBySlot["01"].FlowerIds.Count == 1, "Initial slot 01 should contain pink_rose before reset progress.");

        PressButton(settings, "Panel/Content/ResetButtons/ResetProgressButton");
        await NextFrame();
        ConfirmationDialog resetProgressDialog = settings.GetNode<ConfirmationDialog>("ResetProgressDialog");
        Assert(resetProgressDialog.Visible, "ResetProgressButton should show confirmation dialog.");
        Assert(beforeResetProgress.LevelProgressByFlower["pink_rose"] > 0, "Reset progress should not run before confirmation.");
        Assert(beforeResetProgress.WarehouseInventoryByFlower["pink_rose"].SeedCount == 2, "Inventory should not change before reset progress confirmation.");
        Assert(beforeResetProgress.HomeSlotsBySlot["01"].FlowerIds.Count == 1, "Home slot should not change before reset progress confirmation.");

        resetProgressDialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        await NextFrame();
        AssertProgressClearedSettingsKept(0.6f, 0.7f, "zh");
        AssertRunSessionCleared("after reset progress");
        AssertSettingsSnapshot(settings, 0.6f, 0.7f, "zh");
        await AssertSavedResetProgressAsync(0.6f, 0.7f, "zh");

        PrepareDataForResetAll();
        Assert(GetSaveData().LevelProgressByFlower["pink_rose"] == 5, "Reset all setup should add progress.");
        Assert(GetSaveData().WarehouseInventoryByFlower["pink_rose"].SeedCount == 2, "Reset all setup should add inventory.");
        Assert(GetSaveData().HomeSlotsBySlot["01"].FlowerIds.Count == 1, "Reset all setup should add home slot.");

        PressButton(settings, "Panel/Content/ResetButtons/ResetAllSettingsButton");
        await NextFrame();
        ConfirmationDialog resetAllDialog = settings.GetNode<ConfirmationDialog>("ResetAllSettingsDialog");
        Assert(resetAllDialog.Visible, "ResetAllSettingsButton should show confirmation dialog.");
        Assert(GetSaveData().Settings.Language == "en", "Reset all should not run before confirmation.");
        Assert(GetSaveData().LevelProgressByFlower["pink_rose"] == 5, "Progress should not change before reset all confirmation.");

        resetAllDialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        await NextFrame();
        AssertAllSettingsResetToDefault();
        AssertRunSessionCleared("after reset all");
        AssertSettingsSnapshot(settings, SettingsData.DefaultMusicVolume, SettingsData.DefaultSfxVolume, SettingsData.DefaultLanguage);
        await AssertSavedDefaultSettingsAsync();

        PressButton(settings, "Panel/Content/Header/BackButton");
        await NextFrame();
        AssertActiveScene<HomeGardenView>("Settings BackButton should return to HomeGarden.");
        Assert(GetActiveScene().Name != "WarehousePage", "Settings BackButton should not enter WarehousePage.");
        Assert(GetActiveScene().Name != "PlantingPage", "Settings BackButton should not enter PlantingPage.");
        AssertNoLevelCompletedSideEffects("after returning from SettingsPage");

        DeleteUserFile(SettingsSavePath);
    }

    private static void WriteInitialSave()
    {
        SaveSystem saveSystem = new();
        SaveData data = saveSystem.LoadOrCreate(SettingsSavePath);
        data.Settings.MusicVolume = 0.3f;
        data.Settings.SfxVolume = 0.4f;
        data.Settings.Language = "en";
        data.LevelProgressByFlower["pink_rose"] = 3;
        data.WarehouseInventoryByFlower["pink_rose"].SeedCount = 2;
        data.WarehouseInventoryByFlower["pink_rose"].PotionCount = 2;
        data.SetHomeSlot(0, new[] { "pink_rose" });
        data.Normalize();
        if (!saveSystem.ImmediateSave())
        {
            throw new InvalidOperationException("Could not write initial settings smoke save.");
        }
    }

    private void PrepareDataForResetAll()
    {
        SaveData data = GetSaveData();
        data.Settings.MusicVolume = 0.2f;
        data.Settings.SfxVolume = 0.5f;
        data.Settings.Language = "en";
        data.LevelProgressByFlower["pink_rose"] = 5;
        data.WarehouseInventoryByFlower["pink_rose"].SeedCount = 2;
        data.WarehouseInventoryByFlower["pink_rose"].PotionCount = 2;
        data.SetHomeSlot(0, new[] { "pink_rose" });
        data.Normalize();
        GetState().ApplySaveData(data);
    }

    private static void AssertSettingsButton(HomeGardenView homeGarden)
    {
        Button button = homeGarden.GetNode<Button>("ButtonRoot/SettingsButton");
        Assert(button.Visible, "HomeGarden SettingsButton should be visible.");
        Assert(!button.Disabled, "HomeGarden SettingsButton should be enabled.");
    }

    private static void AssertSettingsSnapshot(SettingsPageView settings, float expectedMusic, float expectedSfx, string expectedLanguage)
    {
        HSlider music = settings.GetNode<HSlider>("Panel/Content/SettingsList/MusicVolumeRow/MusicVolumeSlider");
        HSlider sfx = settings.GetNode<HSlider>("Panel/Content/SettingsList/SfxVolumeRow/SfxVolumeSlider");
        OptionButton language = settings.GetNode<OptionButton>("Panel/Content/SettingsList/LanguageRow/LanguageOptionButton");

        AssertNearly((float)music.Value, expectedMusic, "SettingsPage music slider should show expected value.");
        AssertNearly((float)sfx.Value, expectedSfx, "SettingsPage sfx slider should show expected value.");
        Assert((expectedLanguage == "en" && language.Selected == 1) || (expectedLanguage == "zh" && language.Selected == 0), $"SettingsPage language should be {expectedLanguage}. Actual selected: {language.Selected}.");
    }

    private static void ChangeSlider(Node settings, string path, double value)
    {
        HSlider slider = settings.GetNode<HSlider>(path);
        slider.Value = value;
        slider.EmitSignal(Godot.Range.SignalName.ValueChanged, value);
    }

    private void AssertProgressClearedSettingsKept(float expectedMusic, float expectedSfx, string expectedLanguage)
    {
        SaveData data = GetSaveData();
        AssertNearly(data.Settings.MusicVolume, expectedMusic, "ResetProgressOnly should keep music_volume.");
        AssertNearly(data.Settings.SfxVolume, expectedSfx, "ResetProgressOnly should keep sfx_volume.");
        Assert(data.Settings.Language == expectedLanguage, "ResetProgressOnly should keep language.");
        Assert(data.LevelProgressByFlower["pink_rose"] == 0, "ResetProgressOnly should clear pink_rose progress.");
        Assert(data.WarehouseInventoryByFlower["pink_rose"].SeedCount == 0, "ResetProgressOnly should clear inventory seed_count.");
        Assert(data.WarehouseInventoryByFlower["pink_rose"].PotionCount == 0, "ResetProgressOnly should clear inventory potion_count.");
        Assert(data.HomeSlotsBySlot["01"].FlowerIds.Count == 0, "ResetProgressOnly should clear home_slots.");
    }

    private void AssertAllSettingsResetToDefault()
    {
        SaveData data = GetSaveData();
        AssertNearly(data.Settings.MusicVolume, SettingsData.DefaultMusicVolume, "ResetAllSettings should restore default music_volume.");
        AssertNearly(data.Settings.SfxVolume, SettingsData.DefaultSfxVolume, "ResetAllSettings should restore default sfx_volume.");
        Assert(data.Settings.Language == SettingsData.DefaultLanguage, "ResetAllSettings should restore default language.");
        Assert(data.LevelProgressByFlower["pink_rose"] == 0, "ResetAllSettings should clear pink_rose progress.");
        Assert(data.WarehouseInventoryByFlower["pink_rose"].SeedCount == 0, "ResetAllSettings should clear inventory seed_count.");
        Assert(data.HomeSlotsBySlot["01"].FlowerIds.Count == 0, "ResetAllSettings should clear home_slots.");
    }

    private void AssertRunSessionCleared(string label)
    {
        RunSessionState state = GetState();
        foreach (string flowerId in RunSessionState.OpenFlowerIds)
        {
            Assert(state.GetCompletedLevelCount(flowerId) == 0, $"{label}: runtime progress for {flowerId} should be 0.");
        }

        for (int i = 0; i < RunSessionState.MaxFlowerCount; i++)
        {
            Assert(state.FlowerSlotBatches[i].Count == 0, $"{label}: runtime flower slot {i + 1} should be empty.");
        }

        Assert(!state.PendingPlanting, $"{label}: PendingPlanting should be false.");
        Assert(!state.HasSeed, $"{label}: HasSeed should be false.");
        Assert(!state.HasPotion, $"{label}: HasPotion should be false.");
        Assert(!state.IsWarehousePlantingMode, $"{label}: IsWarehousePlantingMode should be false.");
    }

    private void AssertNoLevelCompletedSideEffects(string label)
    {
        RunSessionState state = GetState();
        foreach (string flowerId in RunSessionState.OpenFlowerIds)
        {
            Assert(state.GetCompletedLevelCount(flowerId) == 0, $"{label}: {flowerId} progress should stay 0.");
        }

        Assert(!state.PendingPlanting, $"{label}: PendingPlanting should stay false.");
        Assert(!state.HasSeed, $"{label}: HasSeed should stay false.");
        Assert(!state.HasPotion, $"{label}: HasPotion should stay false.");
    }

    private async Task AssertSavedSettingsAsync(float expectedMusic, float expectedSfx, string expectedLanguage)
    {
        for (int i = 0; i < 300; i++)
        {
            await NextFrame();
            SaveData reloaded = new SaveSystem().LoadOrCreate(SettingsSavePath);
            if (NearlyEqual(reloaded.Settings.MusicVolume, expectedMusic)
                && NearlyEqual(reloaded.Settings.SfxVolume, expectedSfx)
                && reloaded.Settings.Language == expectedLanguage)
            {
                return;
            }
        }

        SaveData finalReload = new SaveSystem().LoadOrCreate(SettingsSavePath);
        AssertNearly(finalReload.Settings.MusicVolume, expectedMusic, "Reloaded music_volume should match.");
        AssertNearly(finalReload.Settings.SfxVolume, expectedSfx, "Reloaded sfx_volume should match.");
        Assert(finalReload.Settings.Language == expectedLanguage, "Reloaded language should match.");
    }

    private async Task AssertSavedResetProgressAsync(float expectedMusic, float expectedSfx, string expectedLanguage)
    {
        for (int i = 0; i < 300; i++)
        {
            await NextFrame();
            SaveData reloaded = new SaveSystem().LoadOrCreate(SettingsSavePath);
            if (NearlyEqual(reloaded.Settings.MusicVolume, expectedMusic)
                && NearlyEqual(reloaded.Settings.SfxVolume, expectedSfx)
                && reloaded.Settings.Language == expectedLanguage
                && reloaded.LevelProgressByFlower["pink_rose"] == 0
                && reloaded.WarehouseInventoryByFlower["pink_rose"].SeedCount == 0
                && reloaded.HomeSlotsBySlot["01"].FlowerIds.Count == 0)
            {
                return;
            }
        }

        throw new TimeoutException("Timed out waiting for ResetProgressOnly save.");
    }

    private async Task AssertSavedDefaultSettingsAsync()
    {
        for (int i = 0; i < 300; i++)
        {
            await NextFrame();
            SaveData reloaded = new SaveSystem().LoadOrCreate(SettingsSavePath);
            if (NearlyEqual(reloaded.Settings.MusicVolume, SettingsData.DefaultMusicVolume)
                && NearlyEqual(reloaded.Settings.SfxVolume, SettingsData.DefaultSfxVolume)
                && reloaded.Settings.Language == SettingsData.DefaultLanguage
                && reloaded.LevelProgressByFlower["pink_rose"] == 0
                && reloaded.HomeSlotsBySlot["01"].FlowerIds.Count == 0)
            {
                return;
            }
        }

        throw new TimeoutException("Timed out waiting for ResetAllSettings save.");
    }

    private static void PressButton(Node scene, string relativePath)
    {
        Button button = scene.GetNode<Button>(relativePath);
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private T AssertActiveScene<T>(string message) where T : Node
    {
        Node activeScene = GetActiveScene();
        Assert(activeScene is T, $"{message} Actual active scene: {activeScene.GetType().Name} / {activeScene.Name}.");
        return (T)activeScene;
    }

    private Node GetActiveScene()
    {
        Node sceneHost = _main.GetNode<Node>("SceneHost");
        int childCount = sceneHost.GetChildCount();
        Assert(childCount == 1, $"SceneHost should have exactly one active child scene. Actual: {childCount}.");
        return sceneHost.GetChild(0);
    }

    private RunSessionState GetState()
    {
        FieldInfo? field = typeof(MainFlowController).GetField("_runSessionState", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException("MainFlowController._runSessionState field should exist.");
        }

        object? value = field.GetValue(_main);
        return value as RunSessionState ?? throw new InvalidOperationException("MainFlowController._runSessionState should be RunSessionState.");
    }

    private SaveData GetSaveData()
    {
        FieldInfo? field = typeof(MainFlowController).GetField("_saveData", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException("MainFlowController._saveData field should exist.");
        }

        object? value = field.GetValue(_main);
        return value as SaveData ?? throw new InvalidOperationException("MainFlowController._saveData should be SaveData.");
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
        Assert(NearlyEqual(actual, expected), $"{message} Expected: {expected:0.00}. Actual: {actual:0.00}.");
    }

    private static bool NearlyEqual(float actual, float expected)
    {
        return Math.Abs(actual - expected) <= 0.001f;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
