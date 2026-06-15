#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;
using WaterSortGame.View;

public sealed partial class TutorialSystemSmoke : Node
{
    private const string SavePath = "user://tutorial_system_smoke.json";

    private MainFlowController _main = null!;

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
            GD.Print("TUTORIAL_SYSTEM_SMOKE_OK");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError($"TUTORIAL_SYSTEM_SMOKE_FAILED: {ex}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        DeleteUserFile(SavePath);
        await CreateMainAsync();

        TutorialSystem tutorial = GetTutorialSystem();
        TutorialBubbleView bubble = GetBubble();
        AssertBubble(bubble, TutorialSystem.HomeIntroKey, "调试药剂可以获得种子和药剂。");
        Assert(GetSaveData().TutorialBubblesShown.GetValueOrDefault(TutorialSystem.HomeIntroKey), "home_intro should be recorded when shown.");
        Assert(bubble.MouseFilter == Control.MouseFilterEnum.Ignore, "Tutorial overlay root should ignore page input.");

        bubble.DismissTutorial();
        Assert(!bubble.IsShowing, "Tutorial bubble should support manual close.");
        GetSaveSystem().ImmediateSave();
        SaveData firstReload = new SaveSystem().LoadOrCreate(SavePath);
        Assert(firstReload.TutorialBubblesShown.GetValueOrDefault(TutorialSystem.HomeIntroKey), "Reloaded save should keep home_intro.");

        await RecreateMainAsync();
        tutorial = GetTutorialSystem();
        bubble = GetBubble();
        Assert(!bubble.IsShowing, "Reloaded main should not repeat home_intro.");
        Assert(!tutorial.TryShowTutorial(TutorialSystem.HomeIntroKey), "home_intro should only show once before reset.");

        RunSessionState state = GetState();
        state.SelectTargetFlower("pink_rose");
        Assert(state.TrySelectPlayableLevel("pink_rose", 1), "Tutorial smoke should select pink_rose level 1.");
        InvokePrivate(_main, "OnLevelCompleted");
        await NextFrame();
        AssertBubble(bubble, TutorialSystem.RewardToWarehouseKey, "通关奖励已进入仓库。");
        Assert(state.GetCompletedLevelCount("pink_rose") == 1, "Reward tutorial should not trigger an extra level completion.");
        Assert(GetSaveData().WarehouseInventoryByFlower["pink_rose"].SeedCount == 1, "Reward flow should add one seed only.");
        Assert(GetSaveData().WarehouseInventoryByFlower["pink_rose"].PotionCount == 1, "Reward flow should add one potion only.");
        AssertNoLegacyScene("after reward tutorial");
        bubble.DismissTutorial();

        InvokePrivate(_main, "ShowWarehouse");
        await NextFrame();
        WarehousePageView warehouse = AssertActiveScene<WarehousePageView>("ShowWarehouse should open WarehousePage.");
        AssertBubble(bubble, TutorialSystem.WarehouseIntroKey, "仓库可以查看种子和药剂数量。");
        PressButton(warehouse, "Panel/Content/Header/BackButton");
        await NextFrame();
        AssertActiveScene<HomeGardenView>("Warehouse BackButton should work while the tutorial bubble is visible.");
        bubble.DismissTutorial();

        _main.BeginWarehousePlantingMode();
        await NextFrame();
        HomeGardenView plantingHome = AssertActiveScene<HomeGardenView>("Planting entry should stay in HomeGarden.");
        AssertBubble(bubble, TutorialSystem.PlantingIntroKey, "先点数字圆圈选择位置，再选择可种花。");
        InvokePrivate(plantingHome, "OnStartGamePressed");
        await NextFrame();
        AssertActiveScene<FlowerSelectView>("HomeGarden should still open FlowerSelect while tutorial is visible.");
        AssertNoLegacyScene("after HomeGarden to FlowerSelect navigation");
        bubble.DismissTutorial();

        InvokePrivate(_main, "ShowHomeGarden");
        await NextFrame();
        plantingHome = AssertActiveScene<HomeGardenView>("Tutorial smoke should return to HomeGarden planting mode.");
        Assert(state.TryAddFlowerToSlot(0, "pink_rose") == PlantingResult.Planted, "Tutorial smoke should prepare an existing flower slot.");
        GetSaveData().SetHomeSlot(0, new[] { "pink_rose" });
        InvokePrivate(_main, "OnFlowerSlotPlantRequested", plantingHome, 0);
        await NextFrame();
        AssertBubble(bubble, TutorialSystem.ShovelIntroKey, "这里可以铲除全部或单种花，铲花会返还库存。");
        Assert(plantingHome.GetNode<PopupPanel>("PlantingFlowerPopup").Visible, "Existing flower slot should open its action panel.");
        AssertNoLegacyScene("after shovel tutorial");
        bubble.DismissTutorial();

        InvokePrivate(_main, "ShowSettings");
        await NextFrame();
        SettingsPageView settings = AssertActiveScene<SettingsPageView>("ShowSettings should open SettingsPage.");
        AssertBubble(bubble, TutorialSystem.SettingsIntroKey, "这里可以调整音量、语言和重置进度。");
        PressButton(settings, "Panel/Content/Header/BackButton");
        await NextFrame();
        AssertActiveScene<HomeGardenView>("Settings BackButton should work while tutorial is visible.");
        bubble.DismissTutorial();

        AssertTutorialLanguages(tutorial, bubble);
        AssertMissingKeyFallback(tutorial, bubble);

        InvokePrivate(_main, "ShowSettings");
        await NextFrame();
        settings = AssertActiveScene<SettingsPageView>("ResetProgressOnly smoke should run from SettingsPage.");
        GetSaveData().Settings.Language = "en";
        GetLocalization().ApplySettings(GetSaveData().Settings);
        InvokePrivate(_main, "OnResetProgressConfirmed", settings);
        await NextFrame();
        Assert(GetSaveData().TutorialBubblesShown.Count == 0, "ResetProgressOnly should clear tutorial records.");
        Assert(GetSaveData().Settings.Language == "en", "ResetProgressOnly should preserve language.");
        Assert(!tutorial.HasShown(TutorialSystem.HomeIntroKey), "TutorialSystem should synchronize cleared progress state.");
        InvokePrivate(_main, "ShowHomeGarden");
        await NextFrame();
        AssertBubble(bubble, TutorialSystem.HomeIntroKey, "Complete potion puzzles to earn seeds and potions.");

        InvokePrivate(_main, "ShowSettings");
        await NextFrame();
        settings = AssertActiveScene<SettingsPageView>("ResetAllSettings smoke should run from SettingsPage.");
        GetSaveData().Settings.Language = "en";
        GetLocalization().ApplySettings(GetSaveData().Settings);
        InvokePrivate(_main, "OnResetAllConfirmed", settings);
        await NextFrame();
        Assert(GetSaveData().TutorialBubblesShown.Count == 0, "ResetAllSettings should clear tutorial records.");
        Assert(GetSaveData().Settings.Language == SettingsData.DefaultLanguage, "ResetAllSettings should restore default language.");
        Assert(GetLocalization().CurrentLanguage == SettingsData.DefaultLanguage, "LocalizationManager should use default language after ResetAllSettings.");
        InvokePrivate(_main, "ShowHomeGarden");
        await NextFrame();
        AssertBubble(bubble, TutorialSystem.HomeIntroKey, "调试药剂可以获得种子和药剂。");
        AssertNoLegacyScene("after reset tutorial checks");

        GetSaveSystem().ImmediateSave();
        SaveData finalReload = new SaveSystem().LoadOrCreate(SavePath);
        Assert(finalReload.TutorialBubblesShown.GetValueOrDefault(TutorialSystem.HomeIntroKey), "Final save should persist the newly shown home_intro.");
        DeleteUserFile(SavePath);
    }

    private static void AssertTutorialLanguages(TutorialSystem tutorial, TutorialBubbleView bubble)
    {
        SaveData data = GetSystemSaveData(tutorial);
        LocalizationManager localization = GetSystemLocalization(tutorial);

        data.TutorialBubblesShown.Remove(TutorialSystem.HomeIntroKey);
        localization.SetLanguage("en");
        Assert(tutorial.TryShowTutorial(TutorialSystem.HomeIntroKey), "English home_intro should show after test record removal.");
        AssertBubble(bubble, TutorialSystem.HomeIntroKey, "Complete potion puzzles to earn seeds and potions.");
        bubble.DismissTutorial();

        data.TutorialBubblesShown.Remove(TutorialSystem.HomeIntroKey);
        localization.SetLanguage("zh");
        Assert(tutorial.TryShowTutorial(TutorialSystem.HomeIntroKey), "Chinese home_intro should show after test record removal.");
        AssertBubble(bubble, TutorialSystem.HomeIntroKey, "调试药剂可以获得种子和药剂。");
        bubble.DismissTutorial();
    }

    private static void AssertMissingKeyFallback(TutorialSystem tutorial, TutorialBubbleView bubble)
    {
        Assert(tutorial.TryShowTutorial("missing_smoke_key"), "Missing tutorial key should use a safe fallback instead of failing.");
        AssertBubble(bubble, "missing_smoke_key", "tutorial.missing_smoke_key");
        bubble.DismissTutorial();
    }

    private async Task CreateMainAsync()
    {
        PackedScene packedMain = GD.Load<PackedScene>("res://main.tscn");
        _main = packedMain.Instantiate<MainFlowController>();
        _main.SavePathOverride = SavePath;
        AddChild(_main);
        await NextFrame();
    }

    private async Task RecreateMainAsync()
    {
        RemoveChild(_main);
        _main.QueueFree();
        await NextFrame();
        await CreateMainAsync();
    }

    private TutorialSystem GetTutorialSystem()
    {
        return _main.GetNode<TutorialSystem>("TutorialSystem");
    }

    private TutorialBubbleView GetBubble()
    {
        return _main.GetNode<TutorialBubbleView>("TutorialLayer/TutorialBubbleView");
    }

    private LocalizationManager GetLocalization()
    {
        return _main.GetNode<LocalizationManager>("LocalizationManager");
    }

    private SaveSystem GetSaveSystem()
    {
        return GetPrivateField<SaveSystem>(_main, "_saveSystem");
    }

    private SaveData GetSaveData()
    {
        return GetPrivateField<SaveData>(_main, "_saveData");
    }

    private RunSessionState GetState()
    {
        return GetPrivateField<RunSessionState>(_main, "_runSessionState");
    }

    private static SaveData GetSystemSaveData(TutorialSystem tutorial)
    {
        return GetPrivateField<SaveData>(tutorial, "_saveData");
    }

    private static LocalizationManager GetSystemLocalization(TutorialSystem tutorial)
    {
        return GetPrivateField<LocalizationManager>(tutorial, "_localizationManager");
    }

    private T AssertActiveScene<T>(string message) where T : Node
    {
        Node active = GetActiveScene();
        Assert(active is T, $"{message} Actual: {active.GetType().Name} / {active.Name}.");
        return (T)active;
    }

    private Node GetActiveScene()
    {
        Node host = _main.GetNode<Node>("SceneHost");
        Assert(host.GetChildCount() == 1, $"SceneHost should contain one page. Actual: {host.GetChildCount()}.");
        return host.GetChild(0);
    }

    private void AssertNoLegacyScene(string label)
    {
        string sceneName = GetActiveScene().Name;
        Assert(sceneName != "RewardFlower", $"{label}: official flow should not enter RewardFlower.");
        Assert(sceneName != "PlantingPage", $"{label}: official flow should not enter PlantingPage.");
    }

    private static void AssertBubble(TutorialBubbleView bubble, string expectedKey, string expectedText)
    {
        Assert(bubble.IsShowing, $"Tutorial bubble {expectedKey} should be visible.");
        Assert(bubble.CurrentTutorialKey == expectedKey, $"Tutorial key should be {expectedKey}. Actual: {bubble.CurrentTutorialKey}.");
        Assert(bubble.CurrentText == expectedText, $"Tutorial text should be '{expectedText}'. Actual: '{bubble.CurrentText}'.");
    }

    private static void PressButton(Node page, string path)
    {
        page.GetNode<Button>(path).EmitSignal(BaseButton.SignalName.Pressed);
    }

    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo? method = null;
        foreach (MethodInfo candidate in target.GetType().GetMethods(flags))
        {
            if (candidate.Name == methodName && candidate.GetParameters().Length == args.Length)
            {
                method = candidate;
                break;
            }
        }

        if (method == null)
        {
            throw new MissingMethodException(target.GetType().FullName, methodName);
        }

        method.Invoke(target, args);
    }

    private static T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        object? value = field?.GetValue(target);
        return value as T ?? throw new InvalidOperationException($"{target.GetType().Name}.{fieldName} should contain {typeof(T).Name}.");
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

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
