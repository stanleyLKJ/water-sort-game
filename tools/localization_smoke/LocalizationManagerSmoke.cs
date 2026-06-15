#nullable enable

using System;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;
using WaterSortGame.View;

public sealed partial class LocalizationManagerSmoke : Node
{
    private const string SavePath = "user://localization_manager_smoke.json";

    private MainFlowController _main = null!;

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
            GD.Print("LOCALIZATION_MANAGER_SMOKE_OK");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError($"LOCALIZATION_MANAGER_SMOKE_FAILED: {ex}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        DeleteUserFile(SavePath);
        AssertStandaloneFallbacks();

        PackedScene packedMain = GD.Load<PackedScene>("res://main.tscn");
        _main = packedMain.Instantiate<MainFlowController>();
        _main.SavePathOverride = SavePath;
        AddChild(_main);
        await NextFrame();

        LocalizationManager localization = _main.GetNode<LocalizationManager>("LocalizationManager");
        Assert(localization.CurrentLanguage == "zh", "Default localization language should be zh.");
        Assert(localization.Tr("settings.title") == "设置", "Default settings title should be Chinese.");
        Assert(localization.Tr("warehouse.title") == "仓库", "Default warehouse title should be Chinese.");

        HomeGardenView home = AssertActiveScene<HomeGardenView>("Main should initialize HomeGarden in zh.");
        Assert(home.GetNode<Button>("ButtonRoot/SettingsButton").Text == "设置", "HomeGarden settings button should initialize in zh.");
        Assert(home.GetNode<Label>("ButtonRoot/WarehouseSignRoot/SignLabel").Text == "仓库", "HomeGarden warehouse sign should initialize in zh.");

        home.GetNode<Button>("ButtonRoot/SettingsButton").EmitSignal(BaseButton.SignalName.Pressed);
        await NextFrame();
        SettingsPageView settings = AssertActiveScene<SettingsPageView>("Settings button should open SettingsPage.");
        AssertSettingsTitle(settings, "设置");

        await SelectLanguageAsync(settings, 1, "en", "Settings");
        await AssertSavedLanguageAsync("en");

        await SelectLanguageAsync(settings, 0, "zh", "设置");
        await AssertSavedLanguageAsync("zh");

        await SelectLanguageAsync(settings, 1, "en", "Settings");
        await AssertSavedLanguageAsync("en");

        PressButton(settings, "Panel/Content/ResetButtons/ResetProgressButton");
        await NextFrame();
        settings.GetNode<ConfirmationDialog>("ResetProgressDialog").EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        await NextFrame();
        Assert(GetSaveData().Settings.Language == "en", "ResetProgressOnly should keep language en.");
        Assert(localization.CurrentLanguage == "en", "LocalizationManager should remain en after ResetProgressOnly.");
        AssertSettingsTitle(settings, "Settings");
        await AssertSavedLanguageAsync("en");

        PressButton(settings, "Panel/Content/ResetButtons/ResetAllSettingsButton");
        await NextFrame();
        settings.GetNode<ConfirmationDialog>("ResetAllSettingsDialog").EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        await NextFrame();
        Assert(GetSaveData().Settings.Language == SettingsData.DefaultLanguage, "ResetAllSettings should restore the default language.");
        Assert(localization.CurrentLanguage == SettingsData.DefaultLanguage, "LocalizationManager should restore the default language after ResetAllSettings.");
        AssertSettingsTitle(settings, "设置");
        await AssertSavedLanguageAsync(SettingsData.DefaultLanguage);

        await AssertMajorPagesInitializeInZhAsync(localization);
        DeleteUserFile(SavePath);
    }

    private static void AssertStandaloneFallbacks()
    {
        LocalizationManager localization = new();
        localization.SetLanguage("bad_value");
        Assert(localization.CurrentLanguage == SettingsData.DefaultLanguage, "Invalid language should normalize to the default language.");
        Assert(localization.Tr("settings.title") == "设置", "Invalid language fallback should still return Chinese text.");
        Assert(localization.Tr("missing.key") == "missing.key", "Missing localization key should safely return the key.");
    }

    private async Task AssertMajorPagesInitializeInZhAsync(LocalizationManager localization)
    {
        Assert(localization.CurrentLanguage == "zh", "Major page initialization smoke requires zh.");

        FlowerSelectView flowerSelect = GD.Load<PackedScene>("res://scenes/flower_select/FlowerSelect.tscn").Instantiate<FlowerSelectView>();
        flowerSelect.SetLocalizationManager(localization);
        flowerSelect.SetFlowerOptions(new FlowerSelectSystem().CreateBaseFlowerOptions(null, id => localization.Tr($"flower.{id}.name")));
        AddChild(flowerSelect);
        await NextFrame();
        Assert(flowerSelect.GetNode<Label>("Panel/TitleLabel").Text == "选择一种花", "FlowerSelect title should initialize in zh.");
        RemoveChild(flowerSelect);
        flowerSelect.QueueFree();

        LevelSelectView levelSelect = GD.Load<PackedScene>("res://scenes/level_select/LevelSelect.tscn").Instantiate<LevelSelectView>();
        levelSelect.SetLocalizationManager(localization);
        levelSelect.SetLevelOptions("粉玫瑰 关卡", new[] { new LevelSelectOption(1, "粉玫瑰 第 1 关", FlowerLevelState.Playable) });
        AddChild(levelSelect);
        await NextFrame();
        Assert(levelSelect.GetNode<Label>("CommonTextRoot/MessageLabel").Text == "选择当前可玩关卡", "LevelSelect hint should initialize in zh.");
        RemoveChild(levelSelect);
        levelSelect.QueueFree();

        WarehousePageView warehouse = GD.Load<PackedScene>("res://scenes/warehouse/WarehousePage.tscn").Instantiate<WarehousePageView>();
        warehouse.SetLocalizationManager(localization);
        warehouse.SetInventoryRows(new[] { new WarehouseInventoryRow("pink_rose", "粉玫瑰", 1, 1) });
        AddChild(warehouse);
        await NextFrame();
        Assert(warehouse.GetNode<Label>("Panel/Content/Header/TitleLabel").Text == "仓库", "WarehousePage title should initialize in zh.");
        RemoveChild(warehouse);
        warehouse.QueueFree();

        AssertActiveScene<SettingsPageView>("SettingsPage should remain initialized after reset all.");
    }

    private async Task SelectLanguageAsync(SettingsPageView settings, int optionIndex, string expectedLanguage, string expectedTitle)
    {
        OptionButton language = settings.GetNode<OptionButton>("Panel/Content/SettingsList/LanguageRow/LanguageOptionButton");
        language.Select(optionIndex);
        language.EmitSignal(OptionButton.SignalName.ItemSelected, optionIndex);
        await NextFrame();

        Assert(GetSaveData().Settings.Language == expectedLanguage, $"SaveData language should be {expectedLanguage}.");
        Assert(_main.GetNode<LocalizationManager>("LocalizationManager").CurrentLanguage == expectedLanguage, $"LocalizationManager language should be {expectedLanguage}.");
        AssertSettingsTitle(settings, expectedTitle);
    }

    private async Task AssertSavedLanguageAsync(string expectedLanguage)
    {
        for (int i = 0; i < 300; i++)
        {
            await NextFrame();
            SaveData reloaded = new SaveSystem().LoadOrCreate(SavePath);
            if (reloaded.Settings.Language == expectedLanguage)
            {
                return;
            }
        }

        throw new TimeoutException($"Timed out waiting for saved language {expectedLanguage}.");
    }

    private static void AssertSettingsTitle(SettingsPageView settings, string expected)
    {
        string actual = settings.GetNode<Label>("Panel/Content/Header/TitleLabel").Text;
        Assert(actual == expected, $"SettingsPage title should be '{expected}'. Actual: '{actual}'.");
    }

    private SaveData GetSaveData()
    {
        FieldInfo? field = typeof(MainFlowController).GetField("_saveData", BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(_main) as SaveData ?? throw new InvalidOperationException("MainFlowController._saveData should exist.");
    }

    private T AssertActiveScene<T>(string message) where T : Node
    {
        Node host = _main.GetNode<Node>("SceneHost");
        Assert(host.GetChildCount() == 1, "SceneHost should contain exactly one active scene.");
        Node active = host.GetChild(0);
        Assert(active is T, $"{message} Actual: {active.GetType().Name}.");
        return (T)active;
    }

    private static void PressButton(Node root, string path)
    {
        root.GetNode<Button>(path).EmitSignal(BaseButton.SignalName.Pressed);
    }

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void DeleteUserFile(string path)
    {
        if (FileAccess.FileExists(path))
        {
            Error error = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            if (error != Error.Ok)
            {
                throw new InvalidOperationException($"Could not delete {path}. Error: {error}");
            }
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
