#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;

public sealed partial class SaveSystemSmoke : Node
{
    private const string SavePath = "user://save_system_smoke.json";
    private const string BadSavePath = "user://save_system_bad.json";

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
            GD.Print("SAVE_SYSTEM_SMOKE_OK");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError($"SAVE_SYSTEM_SMOKE_FAILED: {ex}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        DeleteUserFile(SavePath);
        DeleteUserFile(BadSavePath);

        SaveSystem saveSystem = CreateSaveSystem();
        SaveData data = saveSystem.LoadOrCreate(SavePath);
        AssertDefaultSaveData(data);

        data.WarehouseInventoryByFlower["pink_rose"].SeedCount = 3;
        data.WarehouseInventoryByFlower["pink_rose"].PotionCount = 2;
        data.TutorialBubblesShown[TutorialSystem.HomeIntroKey] = true;
        saveSystem.RequestSave();
        await WaitForFileAsync(SavePath);

        SaveSystem reloadSystem = CreateSaveSystem();
        SaveData reloaded = reloadSystem.LoadOrCreate(SavePath);
        Assert(reloaded.WarehouseInventoryByFlower["pink_rose"].SeedCount == 3, "Reloaded seed_count should persist after RequestSave.");
        Assert(reloaded.WarehouseInventoryByFlower["pink_rose"].PotionCount == 2, "Reloaded potion_count should persist after RequestSave.");
        Assert(reloaded.TutorialBubblesShown.GetValueOrDefault(TutorialSystem.HomeIntroKey), "Reloaded tutorial_bubbles_shown should persist after RequestSave.");

        WriteText(BadSavePath, "{ bad json");
        SaveSystem badReadSystem = CreateSaveSystem();
        SaveData badRead = badReadSystem.LoadOrCreate(BadSavePath);
        AssertDefaultSaveData(badRead);

        reloaded.Settings.MusicVolume = 0.25f;
        reloaded.Settings.SfxVolume = 0.5f;
        reloaded.Settings.Language = "en";
        reloaded.LevelProgressByFlower["yellow_rose"] = 4;
        reloaded.WarehouseInventoryByFlower["yellow_rose"].SeedCount = 9;
        reloaded.HomeSlotsBySlot[SaveData.BuildSlotKey(0)] = HomeSlotSaveData.Create(0, new[] { "pink_rose", "yellow_rose" });
        reloadSystem.ResetProgressOnly();
        await WaitForFileAsync(SavePath);
        Assert(reloadSystem.CurrentData.Settings.MusicVolume == 0.25f, "ResetProgressOnly should keep music volume.");
        Assert(reloadSystem.CurrentData.Settings.SfxVolume == 0.5f, "ResetProgressOnly should keep sfx volume.");
        Assert(reloadSystem.CurrentData.Settings.Language == "en", "ResetProgressOnly should keep language.");
        Assert(reloadSystem.CurrentData.LevelProgressByFlower["yellow_rose"] == 0, "ResetProgressOnly should clear level progress.");
        Assert(reloadSystem.CurrentData.WarehouseInventoryByFlower["yellow_rose"].SeedCount == 0, "ResetProgressOnly should clear warehouse inventory.");
        Assert(reloadSystem.CurrentData.HomeSlotsBySlot[SaveData.BuildSlotKey(0)].FlowerIds.Count == 0, "ResetProgressOnly should clear home slot batches.");
        Assert(reloadSystem.CurrentData.TutorialBubblesShown.Count == 0, "ResetProgressOnly should clear tutorial records.");

        reloadSystem.CurrentData.Settings.MusicVolume = 0.1f;
        reloadSystem.CurrentData.Settings.SfxVolume = 0.2f;
        reloadSystem.CurrentData.Settings.Language = "en";
        reloadSystem.CurrentData.TutorialBubblesShown[TutorialSystem.SettingsIntroKey] = true;
        reloadSystem.ResetAllSettings();
        await WaitForFileAsync(SavePath);
        Assert(reloadSystem.CurrentData.Settings.MusicVolume == SettingsData.DefaultMusicVolume, "ResetAllSettings should restore default music volume.");
        Assert(reloadSystem.CurrentData.Settings.SfxVolume == SettingsData.DefaultSfxVolume, "ResetAllSettings should restore default sfx volume.");
        Assert(reloadSystem.CurrentData.Settings.Language == SettingsData.DefaultLanguage, "ResetAllSettings should restore default language.");
        Assert(reloadSystem.CurrentData.TutorialBubblesShown.Count == 0, "ResetAllSettings should clear tutorial records.");

        DeleteUserFile(SavePath);
        DeleteUserFile(BadSavePath);
    }

    private SaveSystem CreateSaveSystem()
    {
        SaveSystem saveSystem = new()
        {
            SaveDebounceSeconds = 0.03d
        };
        AddChild(saveSystem);
        return saveSystem;
    }

    private async Task WaitForFileAsync(string path)
    {
        for (int i = 0; i < 60; i++)
        {
            if (Godot.FileAccess.FileExists(path))
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                return;
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        throw new TimeoutException($"Timed out waiting for save file: {path}");
    }

    private static void AssertDefaultSaveData(SaveData data)
    {
        foreach (string flowerId in RunSessionState.OpenFlowerIds)
        {
            Assert(data.LevelProgressByFlower[flowerId] == 0, $"{flowerId} level progress should default to 0.");
            Assert(data.WarehouseInventoryByFlower[flowerId].SeedCount == 0, $"{flowerId} seed_count should default to 0.");
            Assert(data.WarehouseInventoryByFlower[flowerId].PotionCount == 0, $"{flowerId} potion_count should default to 0.");
        }

        for (int i = 0; i < RunSessionState.MaxFlowerCount; i++)
        {
            HomeSlotSaveData slot = data.HomeSlotsBySlot[SaveData.BuildSlotKey(i)];
            Assert(slot.SlotIndex == i, $"Slot {i + 1} should keep its zero-based index.");
            Assert(slot.FlowerIds.Count == 0, $"Slot {i + 1} should default to an empty batch.");
            Assert(slot.Batch == string.Empty, $"Slot {i + 1} should default to an empty batch key.");
        }

        Assert(data.Settings.MusicVolume == SettingsData.DefaultMusicVolume, "music_volume should default.");
        Assert(data.Settings.SfxVolume == SettingsData.DefaultSfxVolume, "sfx_volume should default.");
        Assert(data.Settings.Language == SettingsData.DefaultLanguage, "language should default.");
    }

    private static void WriteText(string path, string text)
    {
        using Godot.FileAccess? file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
        if (file == null)
        {
            throw new InvalidOperationException($"Could not open {path} for writing. Error: {Godot.FileAccess.GetOpenError()}");
        }

        file.StoreString(text);
    }

    private static void DeleteUserFile(string path)
    {
        if (!Godot.FileAccess.FileExists(path))
        {
            return;
        }

        Error error = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        if (error != Error.Ok)
        {
            throw new InvalidOperationException($"Could not delete {path}. Error: {error}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
