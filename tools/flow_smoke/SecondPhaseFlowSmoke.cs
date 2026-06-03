#nullable enable

using System;
using System.Reflection;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;
using WaterSortGame.View;

public sealed partial class SecondPhaseFlowSmoke : Node
{
    private static readonly string[] ExpectedFlowerIds =
    {
        "pink_rose",
        "yellow_rose",
        "lavender",
        "flower_04",
        "flower_05",
        "flower_06"
    };

    private MainFlowController _main = null!;

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
            GD.Print("SECOND_PHASE_FLOW_SMOKE_OK");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError($"SECOND_PHASE_FLOW_SMOKE_FAILED: {ex}");
            GetTree().Quit(1);
        }
    }

    private async System.Threading.Tasks.Task RunAsync()
    {
        PackedScene packedMain = GD.Load<PackedScene>("res://main.tscn");
        _main = packedMain.Instantiate<MainFlowController>();
        AddChild(_main);
        await NextFrame();

        AssertPlantMarkersHidden(AssertActiveScene<HomeGardenView>("main.tscn should start in HomeGarden."), "Initial HomeGarden");

        PressActiveButton("ButtonRoot/StartGameButton");
        await NextFrame();
        FlowerSelectView flowerSelect = AssertActiveScene<FlowerSelectView>("StartGame should open FlowerSelect.");
        AssertOptionCount(flowerSelect, 6, "FlowerSelect should show 6 base flowers.");
        AssertFlowerSelectLabelsAndPlaceholders(flowerSelect);

        PressFlowerOption(flowerSelect, 1);
        await NextFrame();
        AssertActiveScene<Node2D>("Selecting a flower should enter GameScene.");
        RunSessionState state = GetState();
        Assert(state.SelectedFlowerId == "yellow_rose", "SelectedFlowerId should be written as yellow_rose.");

        CompleteGameScene();
        await NextFrame();
        HomeGardenView homeGarden = AssertActiveScene<HomeGardenView>("LevelCompleted should return to HomeGarden.");
        AssertNoRewardFlower();
        Assert(state.PendingPlanting && state.HasSeed && state.HasPotion, "LevelCompleted should create pending planting reward.");
        AssertPlantMarkers(homeGarden, state, "Pending planting HomeGarden");

        ClickSlotPanelArea(homeGarden, 0);
        await NextFrame();
        Assert(state.PlantedFlowerIds[0] == null, "Clicking the slot panel outside the marker should not plant.");
        Assert(state.PendingPlanting && state.HasSeed && state.HasPotion, "Clicking outside marker should not clear pending planting.");
        AssertPlantMarkers(homeGarden, state, "Pending planting after non-marker click");

        ClickPlantMarkerOutsideCircle(homeGarden, 0);
        await NextFrame();
        Assert(state.PlantedFlowerIds[0] == null, "Clicking inside marker bounding box but outside the circle should not plant.");
        Assert(state.PendingPlanting && state.HasSeed && state.HasPotion, "Clicking outside marker circle should not clear pending planting.");

        PlantSlot(homeGarden, 0);
        await NextFrame();
        Assert(state.PlantedFlowerIds[0] == "yellow_rose", "Pending yellow_rose reward should be planted in clicked empty slot.");
        AssertHomeSlotTexture(homeGarden, 0, "yellow_rose");
        AssertExclusiveSlotTextures(homeGarden, 0, "yellow_rose");
        Assert(!state.PendingPlanting && !state.HasSeed && !state.HasPotion, "Planting should clear pendingPlanting, seed, and potion.");
        AssertPlantMarkersHidden(homeGarden, "HomeGarden after planting");

        string? plantedBeforeOccupiedClick = state.PlantedFlowerIds[0];
        PlantSlot(homeGarden, 0);
        await NextFrame();
        Assert(state.PlantedFlowerIds[0] == plantedBeforeOccupiedClick, "Occupied flower slot should not be overwritten.");

        StartNewRunAndPlant(2, 1);
        await NextFrame();
        Assert(state.SelectedFlowerId == "lavender", "Selecting option 2 should write lavender to RunSessionState.");
        Assert(state.PlantedFlowerIds[1] == "lavender", "Pending lavender reward should be planted in clicked empty slot.");
        HomeGardenView lavenderGarden = AssertActiveScene<HomeGardenView>("HomeGarden should stay active after lavender planting.");
        AssertHomeSlotTexture(lavenderGarden, 1, "lavender");
        AssertExclusiveSlotTextures(lavenderGarden, 1, "lavender");

        StartNewRunAndPlant(0, 2);
        await NextFrame();
        Assert(state.SelectedFlowerId == "pink_rose", "Selecting option 0 should write pink_rose to RunSessionState.");
        Assert(state.PlantedFlowerIds[2] == "pink_rose", "Slot 2 should store pink_rose after planting.");
        HomeGardenView pinkGarden = AssertActiveScene<HomeGardenView>("HomeGarden should stay active after pink_rose planting.");
        AssertHomeSlotTexture(pinkGarden, 2, "pink_rose");
        AssertExclusiveSlotTextures(pinkGarden, 2, "pink_rose");

        for (int optionIndex = 3; optionIndex < ExpectedFlowerIds.Length; optionIndex++)
        {
            StartNewRunAndPlant(optionIndex, optionIndex);
            await NextFrame();
            Assert(state.SelectedFlowerId == ExpectedFlowerIds[optionIndex], $"Selecting option {optionIndex} should write {ExpectedFlowerIds[optionIndex]} to RunSessionState.");
            Assert(state.PlantedFlowerIds[optionIndex] == ExpectedFlowerIds[optionIndex], $"Slot {optionIndex} should store {ExpectedFlowerIds[optionIndex]} after planting.");
        }

        StartNewRunAndPlant(1, 6);
        await NextFrame();
        HomeGardenView repeatYellowGarden = AssertActiveScene<HomeGardenView>("HomeGarden should stay active after repeat yellow_rose planting.");
        Assert(state.PlantedFlowerIds[0] == "yellow_rose" && state.PlantedFlowerIds[6] == "yellow_rose", "yellow_rose should remain repeatable in another empty slot.");
        AssertHomeSlotTexture(repeatYellowGarden, 6, "yellow_rose");
        AssertExclusiveSlotTextures(repeatYellowGarden, 6, "yellow_rose");

        StartRunAndComplete(flowerId: 3);
        await NextFrame();
        HomeGardenView fullGarden = AssertActiveScene<HomeGardenView>("Full garden reward attempt should still return HomeGarden.");
        Assert(state.IsGardenFull, "Garden should be full after filling 7 slots.");
        Assert(!state.PendingPlanting, "Full garden should block creation of a new pending planting reward.");
        Label fullGardenStatus = fullGarden.GetNode<Label>("PlantingStatusLabel");
        Assert(!fullGardenStatus.Visible && string.IsNullOrEmpty(fullGardenStatus.Text), "Full garden should not show a full-garden message.");

        PressActiveButton("ButtonRoot/LevelSelectButton");
        await NextFrame();
        AssertActiveScene<LevelSelectView>("LevelSelect button should open LevelSelect.");
        PressActiveButton("Panel/LevelOneButton");
        await NextFrame();
        AssertActiveScene<FlowerSelectView>("Level 1 entry should open FlowerSelect before GameScene.");

        AssertMainFlowDoesNotReferenceRewardFlower();
        await AssertHomeGardenPreviewCanLoadFormalFlowerNodesAsync();
        await AssertAllFlowerOptionsRemainSelectableAsync();
    }

    private void StartNewRunAndPlant(int flowerId, int slotIndex)
    {
        StartRunAndComplete(flowerId);
        HomeGardenView homeGarden = AssertActiveScene<HomeGardenView>("Completed run should return HomeGarden for planting.");
        PlantSlot(homeGarden, slotIndex);
    }

    private void StartRunAndComplete(int flowerId)
    {
        PressActiveButton("ButtonRoot/StartGameButton");
        AssertActiveScene<FlowerSelectView>("StartGame should open FlowerSelect.");
        PressFlowerOption(AssertActiveScene<FlowerSelectView>("FlowerSelect should stay active before flower pick."), flowerId);
        AssertActiveScene<Node2D>("Flower pick should enter GameScene.");
        CompleteGameScene();
    }

    private void CompleteGameScene()
    {
        GameManager gameManager = GetActiveScene().GetNode<GameManager>("Managers/GameManager");
        gameManager.EmitSignal(GameManager.SignalName.LevelCompleted);
    }

    private void PlantSlot(HomeGardenView homeGarden, int slotIndex)
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

    private static void ClickSlotPanelArea(HomeGardenView homeGarden, int slotIndex)
    {
        Control slot = homeGarden.GetNode<Control>($"FlowerSlotRoot/PinkRoseSlot_{slotIndex + 1:00}");
        InputEventMouseButton click = new()
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = Vector2.Zero
        };
        slot.EmitSignal(Control.SignalName.GuiInput, click);
    }

    private static void ClickPlantMarkerOutsideCircle(HomeGardenView homeGarden, int slotIndex)
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

    private static void AssertHomeSlotTexture(HomeGardenView homeGarden, int slotIndex, string flowerId)
    {
        TextureRect texture = GetSlotTexture(homeGarden, slotIndex, flowerId);
        Assert(texture.Visible, $"PinkRoseSlot_{slotIndex + 1:00} texture should be visible after planting.");
        string expectedPath = ResolveExpectedVisibleHomeSlotTexturePath(flowerId, slotIndex);
        AssertTextureLoadedFromExpectedPath(texture, expectedPath, $"PinkRoseSlot_{slotIndex + 1:00}");
    }

    private static void AssertExclusiveSlotTextures(HomeGardenView homeGarden, int slotIndex, string visibleFlowerId)
    {
        string[] flowerIds = { "pink_rose", "yellow_rose", "lavender" };
        foreach (string flowerId in flowerIds)
        {
            TextureRect texture = GetSlotTexture(homeGarden, slotIndex, flowerId);
            bool shouldBeVisible = flowerId == visibleFlowerId;
            Assert(texture.Visible == shouldBeVisible, $"PinkRoseSlot_{slotIndex + 1:00} {flowerId} visibility should be {shouldBeVisible}. Actual: {texture.Visible}.");
        }
    }

    private void PressActiveButton(string relativePath)
    {
        Button button = GetActiveScene().GetNode<Button>(relativePath);
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private void PressFlowerOption(FlowerSelectView flowerSelect, int optionIndex)
    {
        GridContainer options = flowerSelect.GetNode<GridContainer>("Panel/OptionRoot");
        Button button = options.GetChild<Button>(optionIndex);
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private void AssertOptionCount(FlowerSelectView flowerSelect, int expected, string message)
    {
        GridContainer options = flowerSelect.GetNode<GridContainer>("Panel/OptionRoot");
        Assert(options.GetChildCount() == expected, $"{message} Actual: {options.GetChildCount()}.");
    }

    private static void AssertFlowerSelectLabelsAndPlaceholders(FlowerSelectView flowerSelect)
    {
        string[] expectedNames =
        {
            "粉玫瑰",
            "黄玫瑰",
            "薰衣草",
            "待定花 04",
            "待定花 05",
            "待定花 06"
        };

        GridContainer options = flowerSelect.GetNode<GridContainer>("Panel/OptionRoot");
        for (int i = 0; i < expectedNames.Length; i++)
        {
            Button button = options.GetChild<Button>(i);
            Label label = button.GetNode<Label>("FlowerName");
            Assert(label.Text == expectedNames[i], $"Flower option {i} should show {expectedNames[i]}. Actual: {label.Text}.");

            bool hasSelectTexture = button.GetNodeOrNull<TextureRect>("FlowerIcon") != null;
            bool hasMissingPlaceholder = button.GetNodeOrNull<Control>("MissingSelectPlaceholder") != null;
            Assert(hasSelectTexture || hasMissingPlaceholder, $"Flower option {i} should show select art or a missing-art placeholder.");
        }
    }

    private async System.Threading.Tasks.Task AssertAllFlowerOptionsRemainSelectableAsync()
    {
        PackedScene packedFlowerSelect = GD.Load<PackedScene>("res://scenes/flower_select/FlowerSelect.tscn");
        FlowerSelectView flowerSelect = packedFlowerSelect.Instantiate<FlowerSelectView>();
        flowerSelect.SetFlowerOptions(new FlowerSelectSystem().CreateBaseFlowerOptions());

        string? selectedFlowerId = null;
        flowerSelect.TargetFlowerSelected += id => selectedFlowerId = id;

        AddChild(flowerSelect);
        await NextFrame();

        GridContainer options = flowerSelect.GetNode<GridContainer>("Panel/OptionRoot");
        Assert(options.GetChildCount() == 6, "Standalone FlowerSelect should show 6 options.");

        for (int i = 0; i < 6; i++)
        {
            selectedFlowerId = null;
            Button button = options.GetChild<Button>(i);
            button.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(selectedFlowerId == ExpectedFlowerIds[i], $"Flower option {i} should emit TargetFlowerSelected({ExpectedFlowerIds[i]}). Actual: {selectedFlowerId}.");
        }

        flowerSelect.QueueFree();
        await NextFrame();
    }

    private async System.Threading.Tasks.Task AssertHomeGardenPreviewCanLoadFormalFlowerNodesAsync()
    {
        PackedScene packedHomeGarden = GD.Load<PackedScene>("res://scenes/home/HomeGarden.tscn");
        HomeGardenView homeGarden = packedHomeGarden.Instantiate<HomeGardenView>();
        homeGarden.PreviewFlowerId = "lavender";
        homeGarden.ShowEditorFlowerPreview = true;

        AddChild(homeGarden);
        await NextFrame();

        for (int i = 0; i < 7; i++)
        {
            TextureRect texture = GetSlotTexture(homeGarden, i, "lavender");
            string expectedPath = ResolveExpectedVisibleHomeSlotTexturePath("lavender", i);
            Assert(texture.Visible, $"Preview PinkRoseSlot_{i + 1:00} texture should be visible for lavender.");
            AssertTextureLoadedFromExpectedPath(texture, expectedPath, $"Preview PinkRoseSlot_{i + 1:00}");
            AssertExclusiveSlotTextures(homeGarden, i, "lavender");
        }

        homeGarden.PreviewFlowerId = "yellow_rose";
        for (int i = 0; i < 7; i++)
        {
            TextureRect texture = GetSlotTexture(homeGarden, i, "yellow_rose");
            string expectedPath = ResolveExpectedVisibleHomeSlotTexturePath("yellow_rose", i);
            Assert(texture.Visible, $"Preview PinkRoseSlot_{i + 1:00} texture should be visible for yellow_rose.");
            AssertTextureLoadedFromExpectedPath(texture, expectedPath, $"Preview PinkRoseSlot_{i + 1:00}");
            AssertExclusiveSlotTextures(homeGarden, i, "yellow_rose");
        }

        homeGarden.ShowEditorFlowerPreview = false;
        for (int i = 0; i < 7; i++)
        {
            TextureRect pinkTexture = GetSlotTexture(homeGarden, i, "pink_rose");
            TextureRect yellowTexture = GetSlotTexture(homeGarden, i, "yellow_rose");
            TextureRect lavenderTexture = GetSlotTexture(homeGarden, i, "lavender");
            Assert(!pinkTexture.Visible, $"Preview PinkRoseSlot_{i + 1:00} pink texture should be hidden after disabling preview.");
            Assert(!yellowTexture.Visible, $"Preview PinkRoseSlot_{i + 1:00} yellow texture should be hidden after disabling preview.");
            Assert(!lavenderTexture.Visible, $"Preview PinkRoseSlot_{i + 1:00} lavender texture should be hidden after disabling preview.");
        }

        homeGarden.QueueFree();
        await NextFrame();
    }

    private static TextureRect GetSlotTexture(HomeGardenView homeGarden, int slotIndex, string flowerId)
    {
        string textureNodeName = flowerId switch
        {
            "yellow_rose" => "YellowRoseTexture",
            "lavender" => "LavenderTexture",
            _ => "FlowerTexture"
        };
        return homeGarden.GetNode<TextureRect>($"FlowerSlotRoot/PinkRoseSlot_{slotIndex + 1:00}/{textureNodeName}");
    }

    private static Button GetPlantMarker(HomeGardenView homeGarden, int slotIndex)
    {
        return homeGarden.GetNode<Button>($"FlowerSlotRoot/PinkRoseSlot_{slotIndex + 1:00}/PlantMarkerButton");
    }

    private static void AssertPlantMarkers(HomeGardenView homeGarden, RunSessionState state, string label)
    {
        for (int i = 0; i < RunSessionState.MaxFlowerCount; i++)
        {
            Button marker = GetPlantMarker(homeGarden, i);
            bool slotIsEmpty = string.IsNullOrEmpty(state.PlantedFlowerIds[i]);
            bool shouldShow = state.PendingPlanting && slotIsEmpty;
            Assert(marker.Visible == shouldShow, $"{label}: marker {i + 1} visible should be {shouldShow}. Actual: {marker.Visible}.");
            Assert(marker.Disabled != shouldShow, $"{label}: marker {i + 1} disabled should be {!shouldShow}. Actual: {marker.Disabled}.");
            Assert(marker.GetNode<Label>("NumberLabel").Text == (i + 1).ToString(), $"{label}: marker {i + 1} should show its slot number.");
        }
    }

    private static void AssertPlantMarkersHidden(HomeGardenView homeGarden, string label)
    {
        for (int i = 0; i < RunSessionState.MaxFlowerCount; i++)
        {
            Button marker = GetPlantMarker(homeGarden, i);
            Assert(!marker.Visible, $"{label}: marker {i + 1} should be hidden.");
            Assert(marker.Disabled, $"{label}: marker {i + 1} should be disabled.");
        }
    }

    private static string ResolveExpectedVisibleHomeSlotTexturePath(string flowerId, int zeroBasedSlotIndex)
    {
        if (flowerId == "yellow_rose")
        {
            int visibleSlotIndex = zeroBasedSlotIndex switch
            {
                3 => 7,
                6 => 4,
                _ => zeroBasedSlotIndex + 1
            };
            return HomeGardenView.ResolveHomeSlotTexturePath(flowerId, visibleSlotIndex);
        }

        return HomeGardenView.ResolveHomeSlotTexturePath(flowerId, zeroBasedSlotIndex + 1);
    }

    private static void AssertTextureLoadedFromExpectedPath(TextureRect texture, string expectedPath, string label)
    {
        Assert(texture.Texture != null, $"{label} texture should be loaded.");
        Assert(FileAccess.FileExists(expectedPath) || ResourceLoader.Exists(expectedPath), $"{label} expected texture path should exist: {expectedPath}.");

        string actualPath = texture.Texture!.ResourcePath;
        if (!string.IsNullOrEmpty(actualPath))
        {
            Assert(actualPath == expectedPath, $"{label} should use {expectedPath}. Actual: {actualPath}.");
        }
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

    private void AssertNoRewardFlower()
    {
        Node activeScene = GetActiveScene();
        Assert(activeScene.Name != "RewardFlower", "Official flow should not enter old RewardFlower scene.");
    }

    private static void AssertMainFlowDoesNotReferenceRewardFlower()
    {
        FieldInfo[] fields = typeof(MainFlowController).GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (FieldInfo field in fields)
        {
            object? value = field.IsStatic ? field.GetValue(null) : null;
            Assert(value?.ToString()?.Contains("RewardFlower", StringComparison.OrdinalIgnoreCase) != true, "MainFlowController should not hold RewardFlower scene path.");
            Assert(!field.FieldType.Name.Contains("RewardFlower", StringComparison.OrdinalIgnoreCase), "MainFlowController should not hold RewardFlower system fields.");
        }

        MethodInfo[] methods = typeof(MainFlowController).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (MethodInfo method in methods)
        {
            Assert(!method.Name.Contains("RewardFlower", StringComparison.OrdinalIgnoreCase), "MainFlowController should not expose RewardFlower flow methods.");
        }
    }

    private async System.Threading.Tasks.Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
