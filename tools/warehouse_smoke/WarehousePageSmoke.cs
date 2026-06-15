#nullable enable

using System;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;
using WaterSortGame.View;

public sealed partial class WarehousePageSmoke : Node
{
    private const string WarehouseSavePath = "user://warehouse_page_smoke.json";

    private MainFlowController _main = null!;

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
            GD.Print("WAREHOUSE_PAGE_SMOKE_OK");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError($"WAREHOUSE_PAGE_SMOKE_FAILED: {ex}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        DeleteUserFile(WarehouseSavePath);
        WriteInitialSave();

        PackedScene packedMain = GD.Load<PackedScene>("res://main.tscn");
        _main = packedMain.Instantiate<MainFlowController>();
        _main.SavePathOverride = WarehouseSavePath;
        AddChild(_main);
        await NextFrame();

        HomeGardenView homeGarden = AssertActiveScene<HomeGardenView>("main.tscn should start in HomeGarden.");
        AssertWoodSignHotspots(homeGarden);
        SaveData saveData = GetSaveData();
        Assert(saveData.WarehouseInventoryByFlower["pink_rose"].SeedCount == 2, "Initial pink_rose seed_count should be 2.");
        Assert(saveData.WarehouseInventoryByFlower["pink_rose"].PotionCount == 3, "Initial pink_rose potion_count should be 3.");

        saveData.WarehouseInventoryByFlower.Remove("yellow_rose");
        PressButton(homeGarden, "ButtonRoot/WarehouseSignButton");
        await NextFrame();

        WarehousePageView warehouse = AssertActiveScene<WarehousePageView>("WarehouseSignButton should open WarehousePage.");
        AssertWarehouseRow(warehouse, "pink_rose", expectedSeedCount: 2, expectedPotionCount: 3);
        AssertWarehouseRow(warehouse, "yellow_rose", expectedSeedCount: 0, expectedPotionCount: 0);
        AssertWarehouseRow(warehouse, "lavender", expectedSeedCount: 0, expectedPotionCount: 0);
        Assert(warehouse.GetNode<Label>("Panel/Content/Header/TitleLabel").Text == "仓库", "WarehousePage title should be 仓库.");
        Assert(warehouse.GetNodeOrNull<Node>("PlantingPage") == null, "WarehousePage smoke must not enter PlantingPage.");
        AssertNoLevelCompletedSideEffects("after opening warehouse");
        Assert(!saveData.WarehouseInventoryByFlower.ContainsKey("yellow_rose"), "WarehousePage should not create missing inventory entries in SaveData.");
        Assert(saveData.WarehouseInventoryByFlower["pink_rose"].SeedCount == 2, "WarehousePage should not modify pink_rose seed_count.");
        Assert(saveData.WarehouseInventoryByFlower["pink_rose"].PotionCount == 3, "WarehousePage should not modify pink_rose potion_count.");

        PressButton(warehouse, "Panel/Content/Header/BackButton");
        await NextFrame();
        AssertActiveScene<HomeGardenView>("Warehouse BackButton should return to HomeGarden.");
        AssertNoLevelCompletedSideEffects("after returning home");

        SaveSystem reloadSystem = new();
        SaveData reloaded = reloadSystem.LoadOrCreate(WarehouseSavePath);
        Assert(reloaded.WarehouseInventoryByFlower["pink_rose"].SeedCount == 2, "Reloaded pink_rose seed_count should remain 2.");
        Assert(reloaded.WarehouseInventoryByFlower["pink_rose"].PotionCount == 3, "Reloaded pink_rose potion_count should remain 3.");
        Assert(reloaded.WarehouseInventoryByFlower["yellow_rose"].SeedCount == 0, "Reloaded yellow_rose seed_count should remain 0.");
        Assert(reloaded.WarehouseInventoryByFlower["lavender"].PotionCount == 0, "Reloaded lavender potion_count should remain 0.");

        DeleteUserFile(WarehouseSavePath);
    }

    private static void WriteInitialSave()
    {
        SaveSystem saveSystem = new();
        SaveData data = saveSystem.LoadOrCreate(WarehouseSavePath);
        data.WarehouseInventoryByFlower["pink_rose"].SeedCount = 2;
        data.WarehouseInventoryByFlower["pink_rose"].PotionCount = 3;
        if (!saveSystem.ImmediateSave())
        {
            throw new InvalidOperationException("Could not write initial warehouse smoke save.");
        }
    }

    private static void AssertWoodSignHotspots(HomeGardenView homeGarden)
    {
        AssertEditableSignButton(homeGarden.GetNode<Button>("ButtonRoot/StartGameButton"), "StartGameButton", "调试药剂");
        AssertEditableSignButton(homeGarden.GetNode<Button>("ButtonRoot/PlantingSignButton"), "PlantingSignButton", "种植页面");
        AssertEditableSignButton(homeGarden.GetNode<Button>("ButtonRoot/WarehouseSignButton"), "WarehouseSignButton", "仓库");
    }

    private static void AssertEditableSignButton(Button button, string label, string expectedText)
    {
        Assert(button.Text == expectedText, $"{label} should render editable text {expectedText}. Actual: {button.Text}.");
        Assert(button.Icon == null, $"{label} should not render an icon.");
        Assert(button.Flat, $"{label} should be flat.");
        Assert(button.GetThemeStylebox("normal") is StyleBoxEmpty, $"{label} normal style should be empty.");
        Assert(button.GetThemeStylebox("hover") is StyleBoxEmpty, $"{label} hover style should be empty.");
        Assert(button.GetThemeStylebox("pressed") is StyleBoxEmpty, $"{label} pressed style should be empty.");
        Assert(button.GetThemeStylebox("focus") is StyleBoxEmpty, $"{label} focus style should be empty.");
        Assert(button.GetThemeColor("font_color").A > 0.9f, $"{label} text should be visible.");
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
        Assert(GetActiveScene().Name != "PlantingPage", $"{label}: active scene should not be PlantingPage.");
    }

    private static void AssertWarehouseRow(WarehousePageView warehouse, string flowerId, int expectedSeedCount, int expectedPotionCount)
    {
        Control row = warehouse.GetNode<Control>($"Panel/Content/Scroll/ItemList/Row_{flowerId}");
        Label flowerName = row.GetNode<Label>("RowRoot/FlowerName");
        Label seedCount = row.GetNode<Label>("RowRoot/SeedGroup/SeedCountLabel");
        Label potionCount = row.GetNode<Label>("RowRoot/PotionGroup/PotionCountLabel");
        Assert(flowerName.Text.Contains(flowerId, StringComparison.Ordinal), $"{flowerId} row should show flower id.");
        Assert(seedCount.Text == $"种子 x{expectedSeedCount}", $"{flowerId} seed count should be {expectedSeedCount}. Actual: {seedCount.Text}.");
        Assert(potionCount.Text == $"药剂 x{expectedPotionCount}", $"{flowerId} potion count should be {expectedPotionCount}. Actual: {potionCount.Text}.");
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
        if (value is not RunSessionState state)
        {
            throw new InvalidOperationException("MainFlowController._runSessionState should be RunSessionState.");
        }

        return state;
    }

    private SaveData GetSaveData()
    {
        FieldInfo? field = typeof(MainFlowController).GetField("_saveData", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException("MainFlowController._saveData field should exist.");
        }

        object? value = field.GetValue(_main);
        if (value is not SaveData saveData)
        {
            throw new InvalidOperationException("MainFlowController._saveData should be SaveData.");
        }

        return saveData;
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
