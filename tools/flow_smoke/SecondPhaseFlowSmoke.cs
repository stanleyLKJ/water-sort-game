#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;
using WaterSortGame.View;

public sealed partial class SecondPhaseFlowSmoke : Node
{
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

        HomeGardenView homeGarden = AssertActiveScene<HomeGardenView>("main.tscn should start in HomeGarden.");
        AssertLevelSelectButtonHidden(homeGarden);
        AssertPlantMarkers(homeGarden, GetState(), "Initial HomeGarden");

        FlowerSelectView flowerSelect = await OpenFlowerSelectFromHomeAsync();
        AssertFlowerSelectOptions(flowerSelect);

        PressFlowerOption(flowerSelect, 3);
        await NextFrame();
        flowerSelect = AssertActiveScene<FlowerSelectView>("Pending flower slots must not enter LevelSelect.");
        AssertHint(flowerSelect, "该花将在后续版本开放");

        LevelSelectView yellowLevelSelect = await SelectFlowerAsync(flowerSelect, 1, "yellow_rose");
        AssertLevelButtons(yellowLevelSelect, FlowerLevelState.Playable, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked);

        PressLevelButton(yellowLevelSelect, 2);
        await NextFrame();
        yellowLevelSelect = AssertActiveScene<LevelSelectView>("Locked level should not enter GameScene.");
        AssertLevelMessage(yellowLevelSelect, "该关卡尚未解锁");

        PressLevelButton(yellowLevelSelect, 1);
        await NextFrame();
        AssertActiveScene<Node2D>("Playable yellow_rose level 1 should enter GameScene.");
        Assert(GetState().SelectedLevelNumber == 1, "SelectedLevelNumber should be 1 after entering yellow_rose level 1.");

        homeGarden = await CompleteGameAndReturnHomeAsync("yellow_rose", expectedCompletedCount: 1);
        PressActiveButton("ButtonRoot/StartGameButton");
        await NextFrame();
        AssertActiveScene<HomeGardenView>("PendingPlanting should block StartGame.");
        AssertStatus(homeGarden, "请先完成本次种植或追加");

        ClickSlotPanelArea(homeGarden, 0);
        await NextFrame();
        AssertSlotEmpty(GetState(), 0, "Clicking the slot panel outside the marker should not plant.");
        Assert(GetState().PendingPlanting, "Clicking outside marker should not clear PendingPlanting.");

        ClickPlantMarkerOutsideCircle(homeGarden, 0);
        await NextFrame();
        AssertSlotEmpty(GetState(), 0, "Clicking inside marker bounds but outside the circle should not plant.");
        Assert(GetState().PendingPlanting, "Clicking outside marker circle should not clear PendingPlanting.");

        await PlantSlotAsync(0);
        AssertSlotContainsExactly(GetState(), 0, "yellow_rose");
        homeGarden = AssertActiveScene<HomeGardenView>("Planting should keep HomeGarden active.");
        AssertSingleFlowerTexture(homeGarden, 0, "yellow_rose");

        homeGarden = await CompleteRunAndReturnHomeAsync(1, 2, "yellow_rose", expectedCompletedCount: 2);
        Assert(!GetPlantMarker(homeGarden, 0).Visible, "Slot containing yellow_rose should not show marker for yellow_rose reward.");
        Assert(GetPlantMarker(homeGarden, 1).Visible, "Empty slot should show marker for yellow_rose reward.");
        await PlantSlotAsync(1);
        AssertSlotContainsExactly(GetState(), 1, "yellow_rose");

        homeGarden = await CompleteRunAndReturnHomeAsync(0, 1, "pink_rose", expectedCompletedCount: 1);
        Assert(GetPlantMarker(homeGarden, 0).Visible, "Slot with yellow_rose should allow appending pink_rose.");
        await PlantSlotAsync(0);
        AssertSlotContainsExactly(GetState(), 0, "pink_rose", "yellow_rose");
        homeGarden = AssertActiveScene<HomeGardenView>("Appending should keep HomeGarden active.");
        AssertComboPlaceholder(homeGarden, 0, "pink_rose+yellow_rose");

        homeGarden = await CompleteRunAndReturnHomeAsync(0, 2, "pink_rose", expectedCompletedCount: 2);
        Assert(!GetPlantMarker(homeGarden, 0).Visible, "Slot already containing pink_rose should not show marker for another pink_rose reward.");
        Assert(GetPlantMarker(homeGarden, 2).Visible, "Slot without pink_rose should allow pink_rose append/plant.");
        await PlantSlotAsync(2);
        AssertSlotContainsExactly(GetState(), 2, "pink_rose");

        int[] remainingPinkSlots = { 1, 3, 4, 5, 6 };
        for (int i = 0; i < remainingPinkSlots.Length; i++)
        {
            int levelNumber = i + 3;
            await CompleteRunAndReturnHomeAsync(0, levelNumber, "pink_rose", expectedCompletedCount: levelNumber);
            await PlantSlotAsync(remainingPinkSlots[i]);
        }

        RunSessionState state = GetState();
        Assert(state.IsFlowerFull("pink_rose"), "pink_rose should be full after appearing in all 7 slots.");
        Assert(state.GetCompletedLevelCount("pink_rose") == 7, "pink_rose should have all 7 shell levels completed.");

        flowerSelect = await OpenFlowerSelectFromHomeAsync();
        AssertFlowerOptionStatus(flowerSelect, 0, "已种满");
        PressFlowerOption(flowerSelect, 0);
        await NextFrame();
        flowerSelect = AssertActiveScene<FlowerSelectView>("Full pink_rose option should stay in FlowerSelect.");
        AssertHint(flowerSelect, "该花已种满，请选择其他花");

        LevelSelectView lavenderLevelSelect = await SelectFlowerAsync(flowerSelect, 2, "lavender");
        AssertLevelButtons(lavenderLevelSelect, FlowerLevelState.Playable, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked);
        Assert(GetState().GetCompletedLevelCount("yellow_rose") == 2, "yellow_rose progress should remain independent.");
        Assert(GetState().GetCompletedLevelCount("lavender") == 0, "lavender progress should start independently at level 1.");

        AssertNoRewardFlower();
        AssertMainFlowDoesNotReferenceRewardFlower();
    }

    private async System.Threading.Tasks.Task<FlowerSelectView> OpenFlowerSelectFromHomeAsync()
    {
        AssertActiveScene<HomeGardenView>("Expected HomeGarden before opening FlowerSelect.");
        PressActiveButton("ButtonRoot/StartGameButton");
        await NextFrame();
        return AssertActiveScene<FlowerSelectView>("StartGame should open FlowerSelect when no pending planting exists.");
    }

    private async System.Threading.Tasks.Task<LevelSelectView> SelectFlowerAsync(FlowerSelectView flowerSelect, int optionIndex, string expectedFlowerId)
    {
        PressFlowerOption(flowerSelect, optionIndex);
        await NextFrame();
        Assert(GetState().SelectedFlowerId == expectedFlowerId, $"SelectedFlowerId should be {expectedFlowerId}.");
        return AssertActiveScene<LevelSelectView>("Selectable flower should open LevelSelect.");
    }

    private async System.Threading.Tasks.Task<HomeGardenView> CompleteRunAndReturnHomeAsync(
        int flowerOptionIndex,
        int levelNumber,
        string expectedFlowerId,
        int expectedCompletedCount)
    {
        FlowerSelectView flowerSelect = await OpenFlowerSelectFromHomeAsync();
        LevelSelectView levelSelect = await SelectFlowerAsync(flowerSelect, flowerOptionIndex, expectedFlowerId);
        Assert(GetState().GetLevelState(expectedFlowerId, levelNumber) == FlowerLevelState.Playable, $"{expectedFlowerId} level {levelNumber} should be playable.");
        PressLevelButton(levelSelect, levelNumber);
        await NextFrame();
        AssertActiveScene<Node2D>($"{expectedFlowerId} level {levelNumber} should enter GameScene.");
        return await CompleteGameAndReturnHomeAsync(expectedFlowerId, expectedCompletedCount);
    }

    private async System.Threading.Tasks.Task<HomeGardenView> CompleteGameAndReturnHomeAsync(string expectedFlowerId, int expectedCompletedCount)
    {
        CompleteGameScene();
        await NextFrame();
        HomeGardenView homeGarden = AssertActiveScene<HomeGardenView>("LevelCompleted should return to HomeGarden.");
        RunSessionState state = GetState();
        AssertNoRewardFlower();
        Assert(state.GetCompletedLevelCount(expectedFlowerId) == expectedCompletedCount, $"{expectedFlowerId} completed level count should be {expectedCompletedCount}.");
        Assert(state.PendingPlanting && state.HasSeed && state.HasPotion, "LevelCompleted should create pending planting reward.");
        Assert(state.PendingPlantingFlowerId == expectedFlowerId, $"Pending reward should be for {expectedFlowerId}.");
        AssertPlantMarkers(homeGarden, state, $"Pending planting for {expectedFlowerId}");
        return homeGarden;
    }

    private async System.Threading.Tasks.Task PlantSlotAsync(int slotIndex)
    {
        HomeGardenView homeGarden = AssertActiveScene<HomeGardenView>("Expected HomeGarden before planting.");
        PlantSlot(homeGarden, slotIndex);
        await NextFrame();
        Assert(!GetState().PendingPlanting && !GetState().HasSeed && !GetState().HasPotion, "Planting should clear pending reward flags.");
        AssertPlantMarkers(AssertActiveScene<HomeGardenView>("HomeGarden should stay active after planting."), GetState(), "After planting");
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

    private void PressActiveButton(string relativePath)
    {
        Button button = GetActiveScene().GetNode<Button>(relativePath);
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private static void PressFlowerOption(FlowerSelectView flowerSelect, int optionIndex)
    {
        GridContainer options = flowerSelect.GetNode<GridContainer>("Panel/OptionRoot");
        Button button = options.GetChild<Button>(optionIndex);
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private static void PressLevelButton(LevelSelectView levelSelect, int levelNumber)
    {
        GridContainer options = levelSelect.GetNode<GridContainer>("Panel/LevelButtonRoot");
        Button button = options.GetChild<Button>(levelNumber - 1);
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private static void AssertFlowerSelectOptions(FlowerSelectView flowerSelect)
    {
        GridContainer options = flowerSelect.GetNode<GridContainer>("Panel/OptionRoot");
        Assert(options.GetChildCount() == 6, $"FlowerSelect should show 6 slots. Actual: {options.GetChildCount()}.");

        string[] expectedNames =
        {
            "粉玫瑰",
            "黄玫瑰",
            "薰衣草",
            "待定花 04",
            "待定花 05",
            "待定花 06"
        };

        for (int i = 0; i < expectedNames.Length; i++)
        {
            Button button = options.GetChild<Button>(i);
            Assert(button.GetNode<Label>("FlowerName").Text == expectedNames[i], $"Flower option {i} should show {expectedNames[i]}.");
            bool hasSelectTexture = button.GetNodeOrNull<TextureRect>("FlowerIcon") != null;
            bool hasPlaceholder = button.GetNodeOrNull<Control>("MissingSelectPlaceholder") != null;
            Assert(hasSelectTexture || hasPlaceholder, $"Flower option {i} should show select art or a placeholder.");
        }

        AssertFlowerOptionStatus(flowerSelect, 0, string.Empty);
        AssertFlowerOptionStatus(flowerSelect, 1, string.Empty);
        AssertFlowerOptionStatus(flowerSelect, 2, string.Empty);
        AssertFlowerOptionStatus(flowerSelect, 3, "待开放");
        AssertFlowerOptionStatus(flowerSelect, 4, "待开放");
        AssertFlowerOptionStatus(flowerSelect, 5, "待开放");
    }

    private static void AssertFlowerOptionStatus(FlowerSelectView flowerSelect, int optionIndex, string expectedStatus)
    {
        GridContainer options = flowerSelect.GetNode<GridContainer>("Panel/OptionRoot");
        Button button = options.GetChild<Button>(optionIndex);
        Label? statusLabel = button.GetNodeOrNull<Label>("StatusLabel");

        if (string.IsNullOrEmpty(expectedStatus))
        {
            Assert(statusLabel == null, $"Flower option {optionIndex} should not show a status label.");
            return;
        }

        Assert(statusLabel != null, $"Flower option {optionIndex} should show status {expectedStatus}.");
        Assert(statusLabel!.Text == expectedStatus, $"Flower option {optionIndex} status should be {expectedStatus}. Actual: {statusLabel.Text}.");
    }

    private static void AssertLevelButtons(LevelSelectView levelSelect, params FlowerLevelState[] expectedStates)
    {
        GridContainer options = levelSelect.GetNode<GridContainer>("Panel/LevelButtonRoot");
        Assert(options.GetChildCount() == 7, $"LevelSelect should show 7 levels. Actual: {options.GetChildCount()}.");

        for (int i = 0; i < expectedStates.Length; i++)
        {
            Button button = options.GetChild<Button>(i);
            string expectedLabel = expectedStates[i] switch
            {
                FlowerLevelState.Completed => "已完成",
                FlowerLevelState.Playable => "可进入",
                _ => "未解锁"
            };
            Assert(button.Text.Contains(expectedLabel, StringComparison.Ordinal), $"Level {i + 1} should show {expectedLabel}. Actual: {button.Text}.");
        }
    }

    private static void AssertLevelSelectButtonHidden(HomeGardenView homeGarden)
    {
        Button levelSelectButton = homeGarden.GetNode<Button>("ButtonRoot/LevelSelectButton");
        Assert(!levelSelectButton.Visible, "HomeGarden LevelSelectButton should be hidden.");
        Assert(levelSelectButton.Disabled, "HomeGarden LevelSelectButton should be disabled.");
    }

    private static void AssertPlantMarkers(HomeGardenView homeGarden, RunSessionState state, string label)
    {
        for (int i = 0; i < RunSessionState.MaxFlowerCount; i++)
        {
            Button marker = GetPlantMarker(homeGarden, i);
            bool shouldShow = state.CanPlantPendingRewardAt(i);
            Assert(marker.Visible == shouldShow, $"{label}: marker {i + 1} visible should be {shouldShow}. Actual: {marker.Visible}.");
            Assert(marker.Disabled != shouldShow, $"{label}: marker {i + 1} disabled should be {!shouldShow}. Actual: {marker.Disabled}.");
            Assert(marker.GetNode<Label>("NumberLabel").Text == (i + 1).ToString(), $"{label}: marker {i + 1} should show its slot number.");
        }
    }

    private static void AssertSingleFlowerTexture(HomeGardenView homeGarden, int slotIndex, string flowerId)
    {
        TextureRect texture = GetSlotTexture(homeGarden, slotIndex, flowerId);
        Assert(texture.Visible, $"Slot {slotIndex + 1} {flowerId} texture should be visible.");
        Assert(texture.Texture != null, $"Slot {slotIndex + 1} {flowerId} texture should be loaded.");
    }

    private static void AssertComboPlaceholder(HomeGardenView homeGarden, int slotIndex, string comboKey)
    {
        Control? placeholder = homeGarden.GetNodeOrNull<Control>($"FlowerSlotRoot/PinkRoseSlot_{slotIndex + 1:00}/ComboAssetPlaceholder");
        Assert(placeholder != null, $"Slot {slotIndex + 1} should show missing combo placeholder for {comboKey}.");
        Label label = placeholder!.GetNode<Label>("PlaceholderLabel");
        Assert(label.Text.Contains(comboKey, StringComparison.Ordinal), $"Combo placeholder should name {comboKey}. Actual: {label.Text}.");
        Assert(!GetSlotTexture(homeGarden, slotIndex, "pink_rose").Visible, "Combo placeholder should not stack pink_rose single texture.");
        Assert(!GetSlotTexture(homeGarden, slotIndex, "yellow_rose").Visible, "Combo placeholder should not stack yellow_rose single texture.");
        Assert(!GetSlotTexture(homeGarden, slotIndex, "lavender").Visible, "Combo placeholder should not stack lavender single texture.");
    }

    private static void AssertSlotEmpty(RunSessionState state, int slotIndex, string message)
    {
        Assert(state.FlowerSlotBatches[slotIndex].Count == 0, message);
    }

    private static void AssertSlotContainsExactly(RunSessionState state, int slotIndex, params string[] expectedFlowerIds)
    {
        IReadOnlyList<string> actual = state.FlowerSlotBatches[slotIndex];
        Assert(actual.Count == expectedFlowerIds.Length, $"Slot {slotIndex + 1} should contain {expectedFlowerIds.Length} flowers. Actual: {actual.Count}.");

        for (int i = 0; i < expectedFlowerIds.Length; i++)
        {
            Assert(actual[i] == expectedFlowerIds[i], $"Slot {slotIndex + 1} flower {i} should be {expectedFlowerIds[i]}. Actual: {actual[i]}.");
        }

        string expectedComboKey = string.Join("+", expectedFlowerIds);
        Assert(state.GetSlotComboKey(slotIndex) == expectedComboKey, $"Slot {slotIndex + 1} combo key should be {expectedComboKey}. Actual: {state.GetSlotComboKey(slotIndex)}.");
    }

    private static void AssertHint(FlowerSelectView flowerSelect, string expected)
    {
        Label hint = flowerSelect.GetNode<Label>("Panel/HintLabel");
        Assert(hint.Text == expected, $"FlowerSelect hint should be {expected}. Actual: {hint.Text}.");
    }

    private static void AssertLevelMessage(LevelSelectView levelSelect, string expected)
    {
        Label message = levelSelect.GetNode<Label>("Panel/MessageLabel");
        Assert(message.Text == expected, $"LevelSelect message should be {expected}. Actual: {message.Text}.");
    }

    private static void AssertStatus(HomeGardenView homeGarden, string expected)
    {
        Label status = homeGarden.GetNode<Label>("PlantingStatusLabel");
        Assert(status.Text == expected, $"HomeGarden status should be {expected}. Actual: {status.Text}.");
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
