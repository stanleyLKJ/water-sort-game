#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;
using WaterSortGame.View;

public sealed partial class PlantingPageSmoke : Node
{
    private const string PlantingSavePath = "user://planting_page_smoke.json";

    private MainFlowController _main = null!;

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
            GD.Print("PLANTING_PAGE_SMOKE_OK");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError($"PLANTING_PAGE_SMOKE_FAILED: {ex}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        DeleteUserFile(PlantingSavePath);
        WriteInitialSave();

        PackedScene packedMain = GD.Load<PackedScene>("res://main.tscn");
        _main = packedMain.Instantiate<MainFlowController>();
        _main.SavePathOverride = PlantingSavePath;
        AddChild(_main);
        await NextFrame();

        HomeGardenView homeGarden = AssertActiveScene<HomeGardenView>("main.tscn should start in HomeGarden.");
        AssertWoodSignHotspots(homeGarden);
        AssertNoLevelCompletedSideEffects("initial HomeGarden");

        EnterWarehousePlantingMode(homeGarden);
        await NextFrame();
        homeGarden = AssertActiveScene<HomeGardenView>("PlantingSignButton should stay on HomeGarden.");
        Assert(GetActiveScene().Name != "PlantingPage", "Official planting flow must not enter PlantingPage.");
        Assert(GetState().IsWarehousePlantingMode, "PlantingSignButton should enter warehouse planting mode.");
        Assert(!GetState().PendingPlanting && !GetState().HasSeed && !GetState().HasPotion, "Warehouse planting mode should not preselect a flower.");
        Assert(GetState().PendingPlantingFlowerId == null, "Warehouse planting mode should wait for a slot before choosing flower_id.");
        AssertStatus(homeGarden, "请选择种植位置");
        await WaitForHomeGardenStatusToHideAsync(homeGarden, "warehouse planting mode prompt");
        AssertAllPlantMarkersVisible(homeGarden, "initial warehouse planting mode");

        ClickPlantMarkerCorner(homeGarden, 0);
        await NextFrame();
        AssertPlantingPopupVisible(homeGarden, false, "marker rectangle corner should not open popup");
        AssertInventory("pink_rose", 1, 1, "corner click should not deduct pink_rose");
        AssertSlotEmpty(GetState(), 0, "corner click should not modify slot 01");

        ClickPlantMarkerCenter(homeGarden, 0);
        await NextFrame();
        AssertPlantingPopupVisible(homeGarden, true, "slot 01 marker center should open available flower popup");
        AssertPopupHasChoice(homeGarden, "pink_rose", true);
        AssertPopupHasChoice(homeGarden, "lavender", true);
        AssertPopupHasChoice(homeGarden, "yellow_rose", false);

        PressButton(homeGarden, "PlantingFlowerPopup/Panel/FlowerList/PlantingChoice_pink_rose");
        await WaitForPlantingToFinishAsync();
        homeGarden = AssertActiveScene<HomeGardenView>("Successful popup planting should stay on HomeGarden.");
        AssertStatus(homeGarden, "种植成功");
        await WaitForHomeGardenStatusToHideAsync(homeGarden, "successful planting prompt");
        Assert(!GetState().IsWarehousePlantingMode, "Successful planting should clear warehouse planting mode.");
        AssertInventory("pink_rose", 0, 0, "pink_rose after planting");
        AssertSlotContainsExactly(GetState(), 0, "pink_rose");
        AssertSaveSlotContainsExactly(GetSaveData(), 0, "pink_rose");
        AssertHomeGardenSlotVisible(homeGarden, 0, "pink_rose");
        AssertNoPlantMarkers(homeGarden, "after successful planting");
        AssertNoLevelCompletedSideEffects("after successful planting");

        await AssertSavePersistsAsync();

        OpenWarehouseFromHome(homeGarden);
        await NextFrame();
        WarehousePageView warehouse = AssertActiveScene<WarehousePageView>("WarehouseSignButton should open WarehousePage after planting.");
        AssertWarehouseRow(warehouse, "pink_rose", 0, 0);
        AssertWarehouseRow(warehouse, "lavender", 1, 1);
        PressButton(warehouse, "Panel/Content/Header/BackButton");
        await NextFrame();
        homeGarden = AssertActiveScene<HomeGardenView>("Warehouse BackButton should return to HomeGarden.");

        InventoryItemData pinkInventory = GetSaveData().GetOrCreateInventory("pink_rose");
        pinkInventory.SeedCount = 1;
        pinkInventory.PotionCount = 1;
        InventoryItemData yellowInventory = GetSaveData().GetOrCreateInventory("yellow_rose");
        yellowInventory.SeedCount = 1;
        yellowInventory.PotionCount = 1;
        PlantingResult addPinkToSlot02 = GetState().TryAddFlowerToSlot(1, "pink_rose");
        Assert(addPinkToSlot02 == PlantingResult.Planted, $"Setup should add pink_rose to slot 02. Actual: {addPinkToSlot02}.");
        GetSaveData().SetHomeSlot(1, GetState().FlowerSlotBatches[1]);
        homeGarden.RefreshFlowers(GetState());

        EnterWarehousePlantingMode(homeGarden);
        await NextFrame();
        homeGarden = AssertActiveScene<HomeGardenView>("Append check should stay on HomeGarden.");
        ClickPlantMarkerCenter(homeGarden, 1);
        await NextFrame();
        AssertPopupHasChoice(homeGarden, "pink_rose", false);
        AssertPopupHasChoice(homeGarden, "yellow_rose", true);
        PressButton(homeGarden, "PlantingFlowerPopup/Panel/FlowerList/PlantingChoice_yellow_rose");
        await WaitForPlantingToFinishAsync();
        AssertInventory("yellow_rose", 0, 0, "append should deduct yellow_rose inventory");
        AssertSlotContainsExactly(GetState(), 1, "pink_rose", "yellow_rose");
        AssertSaveSlotContainsExactly(GetSaveData(), 1, "pink_rose", "yellow_rose");
        AssertNoLevelCompletedSideEffects("after append planting");

        homeGarden = AssertActiveScene<HomeGardenView>("HomeGarden should stay active after append.");
        InventoryItemData lavenderInventory = GetSaveData().GetOrCreateInventory("lavender");
        lavenderInventory.SeedCount = 1;
        lavenderInventory.PotionCount = 1;
        EnterWarehousePlantingMode(homeGarden);
        await NextFrame();
        _main.CancelWarehousePlanting();
        await NextFrame();
        homeGarden = AssertActiveScene<HomeGardenView>("CancelWarehousePlanting should return to normal HomeGarden.");
        Assert(!GetState().IsWarehousePlantingMode && !GetState().PendingPlanting, "CancelWarehousePlanting should clear planting modes.");
        AssertInventory("lavender", 1, 1, "cancel should not deduct lavender");
        AssertNoPlantMarkers(homeGarden, "after cancel");

        await RunShovelScenarioAsync(homeGarden);

        DeleteUserFile(PlantingSavePath);
    }

    private async Task RunShovelScenarioAsync(HomeGardenView homeGarden)
    {
        PrepareShovelScenario(homeGarden);

        EnterWarehousePlantingMode(homeGarden);
        await NextFrame();
        homeGarden = AssertActiveScene<HomeGardenView>("Shovel scenario should stay on HomeGarden.");
        Assert(GetState().IsWarehousePlantingMode, "Shovel scenario should enter warehouse planting mode.");
        AssertPlantMarkerVisibility(homeGarden, 0, true, "slot 01 has planted flower and zero inventory");
        AssertPlantMarkerVisibility(homeGarden, 1, true, "slot 02 has planted flowers and zero inventory");
        AssertPlantMarkerVisibility(homeGarden, 2, true, "slot 03 has planted flowers and zero inventory");
        for (int i = 3; i < RunSessionState.MaxFlowerCount; i++)
        {
            AssertPlantMarkerVisibility(homeGarden, i, false, $"empty slot {i + 1} has no plantable inventory");
        }

        ClickPlantMarkerCenter(homeGarden, 0);
        await NextFrame();
        AssertPlantingPopupVisible(homeGarden, true, "slot 01 should open shovel popup even with no plantable inventory");
        AssertPopupHasChoice(homeGarden, "pink_rose", false);
        AssertPopupHasShovelAll(homeGarden, true);
        AssertPopupHasShovelChoice(homeGarden, "pink_rose", true);
        PressButton(homeGarden, "PlantingFlowerPopup/Panel/FlowerList/ShovelAllButton");
        await NextFrame();
        AssertInventory("pink_rose", 1, 1, "shovel all slot 01 should return pink_rose");
        AssertSlotEmpty(GetState(), 0, "slot 01 should be empty after shovel all");
        AssertSaveSlotEmpty(GetSaveData(), 0, "SaveData slot 01 should be empty after shovel all");
        AssertNoPlantMarkers(homeGarden, "after shovel all slot 01");
        AssertNoLevelCompletedSideEffects("after shovel all slot 01");

        EnterWarehousePlantingMode(homeGarden);
        await NextFrame();
        homeGarden = AssertActiveScene<HomeGardenView>("Single shovel should stay on HomeGarden.");
        ClickPlantMarkerCenter(homeGarden, 1);
        await NextFrame();
        AssertPopupHasShovelChoice(homeGarden, "pink_rose", true);
        AssertPopupHasShovelChoice(homeGarden, "yellow_rose", true);
        PressButton(homeGarden, "PlantingFlowerPopup/Panel/FlowerList/ShovelChoice_yellow_rose");
        await NextFrame();
        AssertInventory("yellow_rose", 1, 1, "single shovel should return yellow_rose");
        AssertSlotContainsExactly(GetState(), 1, "pink_rose");
        AssertSaveSlotContainsExactly(GetSaveData(), 1, "pink_rose");
        AssertHomeGardenSlotVisible(homeGarden, 1, "pink_rose");
        AssertNoLevelCompletedSideEffects("after single shovel slot 02");

        EnterWarehousePlantingMode(homeGarden);
        await NextFrame();
        homeGarden = AssertActiveScene<HomeGardenView>("Slot 03 shovel all should stay on HomeGarden.");
        ClickPlantMarkerCenter(homeGarden, 2);
        await NextFrame();
        PressButton(homeGarden, "PlantingFlowerPopup/Panel/FlowerList/ShovelAllButton");
        await NextFrame();
        AssertInventory("pink_rose", 2, 2, "slot 03 shovel all should return another pink_rose");
        AssertInventory("yellow_rose", 2, 2, "slot 03 shovel all should return another yellow_rose");
        AssertInventory("lavender", 1, 1, "slot 03 shovel all should return lavender");
        AssertSlotEmpty(GetState(), 2, "slot 03 should be empty after shovel all");
        AssertSaveSlotEmpty(GetSaveData(), 2, "SaveData slot 03 should be empty after shovel all");
        AssertNoLevelCompletedSideEffects("after shovel all slot 03");

        PlantingSystem plantingSystem = new();
        ShovelAttemptResult emptySlotResult = plantingSystem.TryShovelAll(GetSaveData(), GetState(), 0);
        Assert(!emptySlotResult.IsSuccess, "Shoveling an empty slot should fail.");
        AssertInventory("pink_rose", 2, 2, "empty slot shovel should not change pink_rose inventory");

        ShovelAttemptResult missingFlowerResult = plantingSystem.TryShovelFlower(GetSaveData(), GetState(), 1, "yellow_rose");
        Assert(!missingFlowerResult.IsSuccess, "Shoveling a flower not in the slot should fail.");
        AssertInventory("yellow_rose", 2, 2, "missing flower shovel should not change yellow_rose inventory");
        AssertSlotContainsExactly(GetState(), 1, "pink_rose");

        await AssertShovelSavePersistsAsync();

        OpenWarehouseFromHome(homeGarden);
        await NextFrame();
        WarehousePageView warehouse = AssertActiveScene<WarehousePageView>("Warehouse should show returned inventory after shoveling.");
        AssertWarehouseRow(warehouse, "pink_rose", 2, 2);
        AssertWarehouseRow(warehouse, "yellow_rose", 2, 2);
        AssertWarehouseRow(warehouse, "lavender", 1, 1);
        PressButton(warehouse, "Panel/Content/Header/BackButton");
        await NextFrame();
    }

    private void PrepareShovelScenario(HomeGardenView homeGarden)
    {
        RunSessionState state = GetState();
        SaveData saveData = GetSaveData();

        for (int i = 0; i < RunSessionState.MaxFlowerCount; i++)
        {
            state.TryRemoveAllFlowersFromSlot(i, out _);
            saveData.SetHomeSlot(i, state.FlowerSlotBatches[i]);
        }

        foreach (string flowerId in RunSessionState.OpenFlowerIds)
        {
            InventoryItemData inventory = saveData.GetOrCreateInventory(flowerId);
            inventory.SeedCount = 0;
            inventory.PotionCount = 0;
        }

        AddSlotFlowers(state, 0, "pink_rose");
        AddSlotFlowers(state, 1, "pink_rose", "yellow_rose");
        AddSlotFlowers(state, 2, "pink_rose", "yellow_rose", "lavender");
        saveData.SetHomeSlot(0, state.FlowerSlotBatches[0]);
        saveData.SetHomeSlot(1, state.FlowerSlotBatches[1]);
        saveData.SetHomeSlot(2, state.FlowerSlotBatches[2]);
        saveData.Normalize();
        homeGarden.RefreshFlowers(state);

        AssertInventory("pink_rose", 0, 0, "shovel scenario setup pink_rose");
        AssertInventory("yellow_rose", 0, 0, "shovel scenario setup yellow_rose");
        AssertInventory("lavender", 0, 0, "shovel scenario setup lavender");
        AssertSlotContainsExactly(state, 0, "pink_rose");
        AssertSlotContainsExactly(state, 1, "pink_rose", "yellow_rose");
        AssertSlotContainsExactly(state, 2, "pink_rose", "yellow_rose", "lavender");
        AssertSaveSlotContainsExactly(saveData, 0, "pink_rose");
        AssertSaveSlotContainsExactly(saveData, 1, "pink_rose", "yellow_rose");
        AssertSaveSlotContainsExactly(saveData, 2, "pink_rose", "yellow_rose", "lavender");
    }

    private static void WriteInitialSave()
    {
        SaveSystem saveSystem = new();
        SaveData data = saveSystem.LoadOrCreate(PlantingSavePath);
        data.GetOrCreateInventory("pink_rose").SeedCount = 1;
        data.GetOrCreateInventory("pink_rose").PotionCount = 1;
        data.GetOrCreateInventory("yellow_rose").SeedCount = 0;
        data.GetOrCreateInventory("yellow_rose").PotionCount = 1;
        data.GetOrCreateInventory("lavender").SeedCount = 1;
        data.GetOrCreateInventory("lavender").PotionCount = 1;
        data.Normalize();
        if (!saveSystem.ImmediateSave())
        {
            throw new InvalidOperationException("Could not write initial planting smoke save.");
        }
    }

    private static void EnterWarehousePlantingMode(HomeGardenView homeGarden)
    {
        PressButton(homeGarden, "ButtonRoot/PlantingSignButton");
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

    private static void OpenWarehouseFromHome(HomeGardenView homeGarden)
    {
        PressButton(homeGarden, "ButtonRoot/WarehouseSignButton");
    }

    private void AssertNoLevelCompletedSideEffects(string label)
    {
        AssertLevelProgressUnchanged(label);
        Assert(!GetState().PendingPlanting, $"{label}: PendingPlanting should stay false.");
        Assert(!GetState().HasSeed, $"{label}: HasSeed should stay false.");
        Assert(!GetState().HasPotion, $"{label}: HasPotion should stay false.");
        Assert(GetActiveScene().Name != "RewardFlower", $"{label}: active scene should not be RewardFlower.");
    }

    private void AssertLevelProgressUnchanged(string label)
    {
        RunSessionState state = GetState();
        foreach (string flowerId in RunSessionState.OpenFlowerIds)
        {
            Assert(state.GetCompletedLevelCount(flowerId) == 0, $"{label}: {flowerId} progress should stay 0.");
        }
    }

    private static void AssertAllPlantMarkersVisible(HomeGardenView homeGarden, string label)
    {
        for (int i = 0; i < RunSessionState.MaxFlowerCount; i++)
        {
            Button marker = GetPlantMarker(homeGarden, i);
            Assert(marker.Visible, $"{label}: marker {i + 1} should be visible.");
            Assert(!marker.Disabled, $"{label}: marker {i + 1} should be enabled.");
            Assert(marker.GetNode<Label>("NumberLabel").Text == (i + 1).ToString(), $"{label}: marker {i + 1} should show its slot number.");
        }
    }

    private static void AssertNoPlantMarkers(HomeGardenView homeGarden, string label)
    {
        for (int i = 0; i < RunSessionState.MaxFlowerCount; i++)
        {
            Button marker = GetPlantMarker(homeGarden, i);
            Assert(!marker.Visible, $"{label}: marker {i + 1} should be hidden.");
            Assert(marker.Disabled, $"{label}: marker {i + 1} should be disabled.");
        }
    }

    private static void AssertPlantMarkerVisibility(HomeGardenView homeGarden, int slotIndex, bool expectedVisible, string label)
    {
        Button marker = GetPlantMarker(homeGarden, slotIndex);
        Assert(marker.Visible == expectedVisible, $"{label}: marker {slotIndex + 1} visibility should be {expectedVisible}. Actual: {marker.Visible}.");
        Assert(marker.Disabled != expectedVisible, $"{label}: marker {slotIndex + 1} enabled state should follow visibility.");
    }

    private static void AssertPlantingPopupVisible(HomeGardenView homeGarden, bool expectedVisible, string label)
    {
        PopupPanel popup = homeGarden.GetNode<PopupPanel>("PlantingFlowerPopup");
        Assert(popup.Visible == expectedVisible, $"{label}: popup visible should be {expectedVisible}. Actual: {popup.Visible}.");
    }

    private static void AssertPopupHasChoice(HomeGardenView homeGarden, string flowerId, bool expected)
    {
        Button? choice = homeGarden.GetNodeOrNull<Button>($"PlantingFlowerPopup/Panel/FlowerList/PlantingChoice_{flowerId}");
        Assert((choice != null) == expected, $"Popup choice {flowerId} expected {expected}. Actual: {choice != null}.");
    }

    private static void AssertPopupHasShovelAll(HomeGardenView homeGarden, bool expected)
    {
        Button? choice = homeGarden.GetNodeOrNull<Button>("PlantingFlowerPopup/Panel/FlowerList/ShovelAllButton");
        Assert((choice != null) == expected, $"Popup shovel all expected {expected}. Actual: {choice != null}.");
    }

    private static void AssertPopupHasShovelChoice(HomeGardenView homeGarden, string flowerId, bool expected)
    {
        Button? choice = homeGarden.GetNodeOrNull<Button>($"PlantingFlowerPopup/Panel/FlowerList/ShovelChoice_{flowerId}");
        Assert((choice != null) == expected, $"Popup shovel choice {flowerId} expected {expected}. Actual: {choice != null}.");
    }

    private void AssertInventory(string flowerId, int expectedSeedCount, int expectedPotionCount, string label)
    {
        InventoryItemData inventory = GetSaveData().GetOrCreateInventory(flowerId);
        Assert(inventory.SeedCount == expectedSeedCount, $"{label}: {flowerId} seed_count should be {expectedSeedCount}. Actual: {inventory.SeedCount}.");
        Assert(inventory.PotionCount == expectedPotionCount, $"{label}: {flowerId} potion_count should be {expectedPotionCount}. Actual: {inventory.PotionCount}.");
    }

    private static void AssertWarehouseRow(WarehousePageView warehouse, string flowerId, int expectedSeedCount, int expectedPotionCount)
    {
        Control row = warehouse.GetNode<Control>($"Panel/Content/Scroll/ItemList/Row_{flowerId}");
        Label seedCount = row.GetNode<Label>("RowRoot/SeedGroup/SeedCountLabel");
        Label potionCount = row.GetNode<Label>("RowRoot/PotionGroup/PotionCountLabel");
        Assert(seedCount.Text == $"种子 x{expectedSeedCount}", $"{flowerId} seed count should be {expectedSeedCount}. Actual: {seedCount.Text}.");
        Assert(potionCount.Text == $"药剂 x{expectedPotionCount}", $"{flowerId} potion count should be {expectedPotionCount}. Actual: {potionCount.Text}.");
    }

    private static void AssertSlotEmpty(RunSessionState state, int slotIndex, string message)
    {
        Assert(state.FlowerSlotBatches[slotIndex].Count == 0, message);
    }

    private static void AssertSlotContainsExactly(RunSessionState state, int slotIndex, params string[] expectedFlowerIds)
    {
        IReadOnlyList<string> actual = state.FlowerSlotBatches[slotIndex];
        Assert(actual.Count == expectedFlowerIds.Length, $"Runtime slot {slotIndex + 1} should contain {expectedFlowerIds.Length} flowers. Actual: {actual.Count}.");
        for (int i = 0; i < expectedFlowerIds.Length; i++)
        {
            Assert(actual[i] == expectedFlowerIds[i], $"Runtime slot {slotIndex + 1} flower {i} should be {expectedFlowerIds[i]}. Actual: {actual[i]}.");
        }
    }

    private static void AssertSaveSlotContainsExactly(SaveData saveData, int slotIndex, params string[] expectedFlowerIds)
    {
        string slotKey = SaveData.BuildSlotKey(slotIndex);
        if (!saveData.HomeSlotsBySlot.TryGetValue(slotKey, out HomeSlotSaveData? slot) || slot == null)
        {
            throw new InvalidOperationException($"SaveData should contain slot {slotKey}.");
        }

        slot.Normalize();
        Assert(slot.FlowerIds.Count == expectedFlowerIds.Length, $"SaveData slot {slotKey} should contain {expectedFlowerIds.Length} flowers. Actual: {slot.FlowerIds.Count}.");
        for (int i = 0; i < expectedFlowerIds.Length; i++)
        {
            Assert(slot.FlowerIds[i] == expectedFlowerIds[i], $"SaveData slot {slotKey} flower {i} should be {expectedFlowerIds[i]}. Actual: {slot.FlowerIds[i]}.");
        }

        string expectedBatch = string.Join("+", expectedFlowerIds);
        Assert(slot.Batch == expectedBatch, $"SaveData slot {slotKey} batch should be {expectedBatch}. Actual: {slot.Batch}.");
    }

    private static void AssertSaveSlotEmpty(SaveData saveData, int slotIndex, string message)
    {
        string slotKey = SaveData.BuildSlotKey(slotIndex);
        if (!saveData.HomeSlotsBySlot.TryGetValue(slotKey, out HomeSlotSaveData? slot) || slot == null)
        {
            throw new InvalidOperationException($"SaveData should contain slot {slotKey}.");
        }

        slot.Normalize();
        Assert(slot.FlowerIds.Count == 0 && slot.Batch == string.Empty, message);
    }

    private static void AddSlotFlowers(RunSessionState state, int slotIndex, params string[] flowerIds)
    {
        foreach (string flowerId in flowerIds)
        {
            PlantingResult result = state.TryAddFlowerToSlot(slotIndex, flowerId);
            Assert(result == PlantingResult.Planted, $"Expected setup to add {flowerId} to slot {slotIndex + 1}. Actual: {result}.");
        }
    }

    private static void AssertHomeGardenSlotVisible(HomeGardenView homeGarden, int slotIndex, string flowerId)
    {
        string textureNodeName = flowerId switch
        {
            "yellow_rose" => "YellowRoseTexture",
            "lavender" => "LavenderTexture",
            _ => "FlowerTexture"
        };
        TextureRect texture = homeGarden.GetNode<TextureRect>($"FlowerSlotRoot/PinkRoseSlot_{slotIndex + 1:00}/{textureNodeName}");
        Assert(texture.Visible, $"HomeGarden slot {slotIndex + 1} should show {flowerId} scene texture.");
        Assert(texture.Texture != null, $"HomeGarden slot {slotIndex + 1} {flowerId} texture should be set in scene.");
    }

    private static void AssertStatus(HomeGardenView homeGarden, string expected)
    {
        Label status = homeGarden.GetNode<Label>("PlantingStatusLabel");
        Assert(status.Text == expected, $"HomeGarden status should be {expected}. Actual: {status.Text}.");
    }

    private async Task AssertSavePersistsAsync()
    {
        for (int i = 0; i < 300; i++)
        {
            await NextFrame();
            SaveSystem reloadSystem = new();
            SaveData reloaded = reloadSystem.LoadOrCreate(PlantingSavePath);
            if (reloaded.WarehouseInventoryByFlower["pink_rose"].SeedCount == 0
                && reloaded.WarehouseInventoryByFlower["pink_rose"].PotionCount == 0
                && reloaded.HomeSlotsBySlot["01"].FlowerIds.Count == 1
                && reloaded.HomeSlotsBySlot["01"].FlowerIds[0] == "pink_rose"
                && reloaded.HomeSlotsBySlot["01"].Batch == "pink_rose")
            {
                return;
            }
        }

        SaveSystem finalReloadSystem = new();
        SaveData finalReload = finalReloadSystem.LoadOrCreate(PlantingSavePath);
        Assert(finalReload.WarehouseInventoryByFlower["pink_rose"].SeedCount == 0, "Reloaded pink_rose seed_count should be 0 after planting.");
        Assert(finalReload.WarehouseInventoryByFlower["pink_rose"].PotionCount == 0, "Reloaded pink_rose potion_count should be 0 after planting.");
        AssertSaveSlotContainsExactly(finalReload, 0, "pink_rose");
    }

    private async Task AssertShovelSavePersistsAsync()
    {
        for (int i = 0; i < 300; i++)
        {
            await NextFrame();
            SaveSystem reloadSystem = new();
            SaveData reloaded = reloadSystem.LoadOrCreate(PlantingSavePath);
            if (reloaded.WarehouseInventoryByFlower["pink_rose"].SeedCount == 2
                && reloaded.WarehouseInventoryByFlower["pink_rose"].PotionCount == 2
                && reloaded.WarehouseInventoryByFlower["yellow_rose"].SeedCount == 2
                && reloaded.WarehouseInventoryByFlower["yellow_rose"].PotionCount == 2
                && reloaded.WarehouseInventoryByFlower["lavender"].SeedCount == 1
                && reloaded.WarehouseInventoryByFlower["lavender"].PotionCount == 1
                && reloaded.HomeSlotsBySlot["01"].FlowerIds.Count == 0
                && reloaded.HomeSlotsBySlot["02"].FlowerIds.Count == 1
                && reloaded.HomeSlotsBySlot["02"].FlowerIds[0] == "pink_rose"
                && reloaded.HomeSlotsBySlot["03"].FlowerIds.Count == 0)
            {
                return;
            }
        }

        SaveSystem finalReloadSystem = new();
        SaveData finalReload = finalReloadSystem.LoadOrCreate(PlantingSavePath);
        Assert(finalReload.WarehouseInventoryByFlower["pink_rose"].SeedCount == 2, "Reloaded pink_rose seed_count should be 2 after shoveling.");
        Assert(finalReload.WarehouseInventoryByFlower["pink_rose"].PotionCount == 2, "Reloaded pink_rose potion_count should be 2 after shoveling.");
        Assert(finalReload.WarehouseInventoryByFlower["yellow_rose"].SeedCount == 2, "Reloaded yellow_rose seed_count should be 2 after shoveling.");
        Assert(finalReload.WarehouseInventoryByFlower["yellow_rose"].PotionCount == 2, "Reloaded yellow_rose potion_count should be 2 after shoveling.");
        Assert(finalReload.WarehouseInventoryByFlower["lavender"].SeedCount == 1, "Reloaded lavender seed_count should be 1 after shoveling.");
        Assert(finalReload.WarehouseInventoryByFlower["lavender"].PotionCount == 1, "Reloaded lavender potion_count should be 1 after shoveling.");
        AssertSaveSlotEmpty(finalReload, 0, "Reloaded slot 01 should be empty after shoveling.");
        AssertSaveSlotContainsExactly(finalReload, 1, "pink_rose");
        AssertSaveSlotEmpty(finalReload, 2, "Reloaded slot 03 should be empty after shoveling.");
    }

    private static void PressButton(Node scene, string relativePath)
    {
        Button button = scene.GetNode<Button>(relativePath);
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private static void ClickPlantMarkerCenter(HomeGardenView homeGarden, int slotIndex)
    {
        Button marker = GetPlantMarker(homeGarden, slotIndex);
        InputEventMouseButton click = new()
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = marker.Size * 0.5f
        };
        marker.EmitSignal(Control.SignalName.GuiInput, click);
    }

    private static void ClickPlantMarkerCorner(HomeGardenView homeGarden, int slotIndex)
    {
        Button marker = GetPlantMarker(homeGarden, slotIndex);
        InputEventMouseButton click = new()
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = Vector2.Zero
        };
        marker.EmitSignal(Control.SignalName.GuiInput, click);
    }

    private static Button GetPlantMarker(HomeGardenView homeGarden, int slotIndex)
    {
        return homeGarden.GetNode<Button>($"FlowerSlotRoot/PinkRoseSlot_{slotIndex + 1:00}/PlantMarkerButton");
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

    private async Task WaitForHomeGardenStatusToHideAsync(HomeGardenView homeGarden, string label)
    {
        SceneTreeTimer timer = GetTree().CreateTimer(3.1);
        await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        Label status = homeGarden.GetNode<Label>("PlantingStatusLabel");
        Assert(string.IsNullOrEmpty(status.Text) && !status.Visible, $"{label} should auto-hide after 3 seconds.");
    }

    private async Task WaitForPlantingToFinishAsync()
    {
        for (int i = 0; i < 90; i++)
        {
            await NextFrame();
            if (!GetState().IsWarehousePlantingMode && !GetState().PendingPlanting)
            {
                return;
            }
        }

        throw new TimeoutException("Planting marker commit did not finish within 90 frames.");
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
