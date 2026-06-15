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
	private const string FlowSavePath = "user://second_phase_flow_smoke.json";

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
		DeleteUserFile(FlowSavePath);

		PackedScene packedMain = GD.Load<PackedScene>("res://main.tscn");
		_main = packedMain.Instantiate<MainFlowController>();
		_main.SavePathOverride = FlowSavePath;
		AddChild(_main);
		await NextFrame();

		HomeGardenView homeGarden = AssertActiveScene<HomeGardenView>("main.tscn should start in HomeGarden.");
		AssertLevelSelectButtonHidden(homeGarden);
		AssertPendingRewardPreviewHidden(homeGarden, "Initial HomeGarden");
		AssertPlantingFxHidden(homeGarden, "Initial HomeGarden");
		AssertRewardItemResourcesExist();
		AssertRunSessionStateDoesNotStoreImagePaths(GetState());
		AssertPlantMarkers(homeGarden, GetState(), "Initial HomeGarden");
		await AssertHomeGardenSceneNodeDisplayAsync();
		await AssertMissingCombinationNodeDoesNotCreateFallbackAsync();

		FlowerSelectView flowerSelect = await OpenFlowerSelectFromHomeAsync();
		AssertFlowerSelectOptions(flowerSelect);

		PressFlowerOption(flowerSelect, 3);
		await NextFrame();
		flowerSelect = AssertActiveScene<FlowerSelectView>("Pending flower slots must not enter LevelSelect.");
		AssertFlowerFixedHint(flowerSelect, "选择 1 种花，完成药剂调试后回花园种植");
		AssertFlowerTemporaryTip(flowerSelect, "该花将在后续版本开放");
		await WaitForFlowerSelectTemporaryTipToHideAsync(flowerSelect);

		LevelSelectView yellowLevelSelect = await SelectFlowerAsync(flowerSelect, 1, "yellow_rose");
		AssertLevelButtons(yellowLevelSelect, FlowerLevelState.Playable, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked);

		PressLevelButton(yellowLevelSelect, 2);
		await NextFrame();
		yellowLevelSelect = AssertActiveScene<LevelSelectView>("Locked level should not enter GameScene.");
		AssertLevelFixedMessage(yellowLevelSelect, "选择当前可玩关卡");
		AssertLevelTemporaryTip(yellowLevelSelect, "该关卡尚未解锁");
		await WaitForLevelTemporaryTipToHideAsync(yellowLevelSelect);

		PressLevelButton(yellowLevelSelect, 1);
		await NextFrame();
		Node2D yellowGameScene = AssertActiveScene<Node2D>("Playable yellow_rose level 1 should enter GameScene.");
		AssertGameSceneCauldron(yellowGameScene);
		AssertCauldronProgressCollectionRefresh(yellowGameScene);
		AssertGameSceneCanvasControls(yellowGameScene, expectExitVisible: true);
		Assert(GetState().SelectedLevelNumber == 1, "SelectedLevelNumber should be 1 after entering yellow_rose level 1.");
		await NextFrame();
		await AssertRestartButtonKeepsGameSceneAsync();
		yellowLevelSelect = await ExitGameSceneToLevelSelectAsync("yellow_rose");
		PressLevelButton(yellowLevelSelect, 1);
		await NextFrame();
		yellowGameScene = AssertActiveScene<Node2D>("Playable yellow_rose level 1 should still enter GameScene again after ExitButton.");
		AssertGameSceneCauldron(yellowGameScene);
		AssertGameSceneCanvasControls(yellowGameScene, expectExitVisible: true);
		await AssertCauldronRewardPanelBehaviorAsync(yellowGameScene);

		homeGarden = await CompleteGameAndReturnHomeAsync("yellow_rose", expectedCompletedCount: 1);
		AssertStatus(homeGarden, "新的种子和药剂已存入仓库");
		await WaitForHomeGardenStatusToHideAsync(homeGarden);
		AssertHomeGardenStatusHidden(homeGarden, "Warehouse reward status should auto-hide after 3 seconds.");
		homeGarden.ShowMessage("库存不足");
		await WaitSecondsAsync(1.0);
		homeGarden.ShowMessage("缺少种子或药剂");
		await WaitSecondsAsync(2.1);
		AssertStatus(homeGarden, "缺少种子或药剂");
		await WaitSecondsAsync(1.1);
		AssertHomeGardenStatusHidden(homeGarden, "The second HomeGarden status should own the 3 second timer.");
		AssertNoPendingRewardFlags("After yellow_rose level 1 completion");
		AssertNoPlantMarkers(homeGarden, "After warehouse reward");
		await AssertSavedRewardAndProgressAsync("yellow_rose", expectedCompletedCount: 1, expectedSeedCount: 1, expectedPotionCount: 1);
		WarehousePageView warehouse = await OpenWarehouseFromHomeAsync();
		AssertWarehouseRow(warehouse, "yellow_rose", expectedSeedCount: 1, expectedPotionCount: 1);
		AssertWarehouseRow(warehouse, "pink_rose", expectedSeedCount: 0, expectedPotionCount: 0);
		AssertWarehouseRow(warehouse, "lavender", expectedSeedCount: 0, expectedPotionCount: 0);
		PressActiveButton("Panel/Content/Header/BackButton");
		await NextFrame();
		homeGarden = AssertActiveScene<HomeGardenView>("Warehouse BackButton should return to HomeGarden.");
		homeGarden = await EnterPlantingModeFromHomeAsync();
		Assert(GetState().IsWarehousePlantingMode, "PlantingSignButton should enter HomeGarden warehouse planting mode.");
		Assert(!GetState().PendingPlanting && !GetState().HasSeed && !GetState().HasPotion, "Warehouse planting mode should not preselect a flower.");
		AssertStatus(homeGarden, "请选择种植位置");
		Assert(GetPlantMarker(homeGarden, 0).Visible, "Warehouse planting mode should show slot markers when inventory can be planted.");
		Assert(GetActiveScene().Name != "PlantingPage", "PlantingSignButton should not open PlantingPage in the official flow.");
		PressActiveButton("ButtonRoot/PlantingSignButton");
		await NextFrame();
		homeGarden = AssertActiveScene<HomeGardenView>("PlantingButton should cancel warehouse planting mode from HomeGarden.");
		Assert(!GetState().IsWarehousePlantingMode, "Cancel planting should clear warehouse planting mode.");
		AssertNoPlantMarkers(homeGarden, "After canceling warehouse planting mode");

		PressActiveButton("ButtonRoot/StartGameButton");
		await NextFrame();
		flowerSelect = AssertActiveScene<FlowerSelectView>("StartGame should not be blocked after warehouse reward.");
		yellowLevelSelect = await SelectFlowerAsync(flowerSelect, 1, "yellow_rose");
		AssertLevelButtons(yellowLevelSelect, FlowerLevelState.Completed, FlowerLevelState.Playable, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked);

		PressLevelButton(yellowLevelSelect, 2);
		await NextFrame();
		yellowGameScene = AssertActiveScene<Node2D>("Playable yellow_rose level 2 should enter GameScene.");
		AssertGameSceneCauldron(yellowGameScene);
		homeGarden = await CompleteGameAndReturnHomeAsync("yellow_rose", expectedCompletedCount: 2);
		AssertNoPendingRewardFlags("After yellow_rose level 2 completion");
		await AssertSavedRewardAndProgressAsync("yellow_rose", expectedCompletedCount: 2, expectedSeedCount: 2, expectedPotionCount: 2);

		homeGarden = await CompleteRunAndReturnHomeAsync(0, 1, "pink_rose", expectedCompletedCount: 1);
		AssertNoPendingRewardFlags("After pink_rose level 1 completion");
		await AssertSavedRewardAndProgressAsync("pink_rose", expectedCompletedCount: 1, expectedSeedCount: 1, expectedPotionCount: 1);

		flowerSelect = await OpenFlowerSelectFromHomeAsync();
		LevelSelectView lavenderLevelSelect = await SelectFlowerAsync(flowerSelect, 2, "lavender");
		AssertLevelButtons(lavenderLevelSelect, FlowerLevelState.Playable, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked);
		Assert(GetState().GetCompletedLevelCount("yellow_rose") == 2, "yellow_rose progress should remain independent.");
		Assert(GetState().GetCompletedLevelCount("lavender") == 0, "lavender progress should start independently at level 1.");

		AssertNoRewardFlower();
		AssertMainFlowDoesNotReferenceRewardFlower();
		AssertRunSessionStateDoesNotStoreImagePaths(GetState());
		DeleteUserFile(FlowSavePath);
	}

	private async System.Threading.Tasks.Task<FlowerSelectView> OpenFlowerSelectFromHomeAsync()
	{
		AssertActiveScene<HomeGardenView>("Expected HomeGarden before opening FlowerSelect.");
		PressActiveButton("ButtonRoot/StartGameButton");
		await NextFrame();
		return AssertActiveScene<FlowerSelectView>("StartGame should open FlowerSelect when no pending planting exists.");
	}

	private async System.Threading.Tasks.Task<WarehousePageView> OpenWarehouseFromHomeAsync()
	{
		AssertActiveScene<HomeGardenView>("Expected HomeGarden before opening WarehousePage.");
		PressActiveButton("ButtonRoot/WarehouseSignButton");
		await NextFrame();
		return AssertActiveScene<WarehousePageView>("WarehouseSignButton should open WarehousePage.");
	}

	private async System.Threading.Tasks.Task<HomeGardenView> EnterPlantingModeFromHomeAsync()
	{
		AssertActiveScene<HomeGardenView>("Expected HomeGarden before entering warehouse planting mode.");
		PressActiveButton("ButtonRoot/PlantingSignButton");
		await NextFrame();
		return AssertActiveScene<HomeGardenView>("PlantingSignButton should stay in HomeGarden for warehouse planting mode.");
	}

	private async System.Threading.Tasks.Task<LevelSelectView> SelectFlowerAsync(FlowerSelectView flowerSelect, int optionIndex, string expectedFlowerId)
	{
		PressFlowerOption(flowerSelect, optionIndex);
		await NextFrame();
		Assert(GetState().SelectedFlowerId == expectedFlowerId, $"SelectedFlowerId should be {expectedFlowerId}.");
		LevelSelectView levelSelect = AssertActiveScene<LevelSelectView>("Selectable flower should open LevelSelect.");
		AssertLevelSelectArt(levelSelect, expectedFlowerId);
		return levelSelect;
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
		Node2D gameScene = AssertActiveScene<Node2D>($"{expectedFlowerId} level {levelNumber} should enter GameScene.");
		AssertGameSceneCauldron(gameScene);
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
		Assert(!state.PendingPlanting && !state.HasSeed && !state.HasPotion, "LevelCompleted should store rewards in warehouse instead of creating PendingPlanting.");
		Assert(state.PendingPlantingFlowerId == null, "Pending reward flower id should stay null after warehouse reward.");
		AssertPendingRewardPreviewHidden(homeGarden, $"Pending planting for {expectedFlowerId}");
		AssertPlantingFxHidden(homeGarden, $"Pending planting for {expectedFlowerId}");
		AssertPlantMarkers(homeGarden, state, $"Pending planting for {expectedFlowerId}");
		return homeGarden;
	}

	private async System.Threading.Tasks.Task AssertRestartButtonKeepsGameSceneAsync()
	{
		PressActiveButton("CanvasLayer/RestartButton");
		await NextFrame();

		Node2D gameScene = AssertActiveScene<Node2D>("RestartButton should keep the current GameScene active.");
		AssertGameSceneCauldron(gameScene);
		Assert(!GetState().PendingPlanting && !GetState().HasSeed && !GetState().HasPotion, "RestartButton should not create pending rewards.");
		Assert(GetState().GetCompletedLevelCount("yellow_rose") == 0, "RestartButton should not record level completion.");
	}

	private async System.Threading.Tasks.Task<LevelSelectView> ExitGameSceneToLevelSelectAsync(string expectedFlowerId)
	{
		PressActiveButton("CanvasLayer/ExitButton");
		await NextFrame();

		LevelSelectView levelSelect = AssertActiveScene<LevelSelectView>("ExitButton should return from GameScene to LevelSelect.");
		RunSessionState state = GetState();
		Assert(state.SelectedFlowerId == expectedFlowerId, "ExitButton should preserve SelectedFlowerId.");
		Assert(state.SelectedLevelNumber == 1, "ExitButton should preserve the current outer level selection.");
		Assert(!state.PendingPlanting && !state.HasSeed && !state.HasPotion, "ExitButton should not create PendingPlanting or reward flags.");
		Assert(state.GetCompletedLevelCount(expectedFlowerId) == 0, "ExitButton should not record level completion.");
		AssertLevelButtons(levelSelect, FlowerLevelState.Playable, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked, FlowerLevelState.Locked);
		return levelSelect;
	}

	private async System.Threading.Tasks.Task PlantSlotAsync(int slotIndex)
	{
		HomeGardenView homeGarden = AssertActiveScene<HomeGardenView>("Expected HomeGarden before planting.");
		PlantSlot(homeGarden, slotIndex);
		await NextFrame();
		Assert(GetState().PendingPlanting && GetState().HasSeed && GetState().HasPotion, "Pending flags should remain until planting animation finishes.");
		AssertPlantingFxActive(homeGarden, "During planting animation");
		Assert(GetPlantMarker(homeGarden, slotIndex).Disabled, "Plant marker should be disabled while planting animation is running.");

		PlantSlot(homeGarden, slotIndex);
		await NextFrame();
		Assert(GetState().PendingPlanting, "Second click during planting animation should not plant immediately or clear pending state.");

		await WaitForPlantingToFinishAsync();
		Assert(!GetState().PendingPlanting && !GetState().HasSeed && !GetState().HasPotion, "Planting should clear pending reward flags.");
		HomeGardenView homeGardenAfterPlanting = AssertActiveScene<HomeGardenView>("HomeGarden should stay active after planting.");
		AssertPendingRewardPreviewHidden(homeGardenAfterPlanting, "After planting");
		AssertPlantingFxHidden(homeGardenAfterPlanting, "After planting");
		AssertPlantMarkers(homeGardenAfterPlanting, GetState(), "After planting");
	}

	private async System.Threading.Tasks.Task AssertHomeGardenSceneNodeDisplayAsync()
	{
		PackedScene packedHome = GD.Load<PackedScene>("res://scenes/home/HomeGarden.tscn");
		HomeGardenView comboHomeGarden = packedHome.Instantiate<HomeGardenView>();
		AddChild(comboHomeGarden);
		await NextFrame();

		try
		{
			RunSessionState comboState = new();
			AddSlotFlowers(comboState, 0, "lavender", "pink_rose");
			AddSlotFlowers(comboState, 1, "yellow_rose", "pink_rose");
			AddSlotFlowers(comboState, 2, "lavender", "yellow_rose");
			AddSlotFlowers(comboState, 3, "lavender", "yellow_rose", "pink_rose");

			Assert(comboState.GetSlotComboKey(0) == "pink_rose+lavender", "Combo key should sort pink_rose before lavender.");
			Assert(comboState.GetSlotComboKey(1) == "pink_rose+yellow_rose", "Combo key should sort pink_rose before yellow_rose.");
			Assert(comboState.GetSlotComboKey(2) == "yellow_rose+lavender", "Combo key should sort yellow_rose before lavender.");
			Assert(comboState.GetSlotComboKey(3) == "pink_rose+yellow_rose+lavender", "Combo key should use stable open-flower order for three-flower combos.");

			comboHomeGarden.RefreshFlowers(comboState);
			await NextFrame();

			AssertComboTexture(comboHomeGarden, 0, "pink_rose+lavender");
			AssertComboTexture(comboHomeGarden, 1, "pink_rose+yellow_rose");
			AssertComboTexture(comboHomeGarden, 2, "yellow_rose+lavender");
			AssertComboTexture(comboHomeGarden, 3, "pink_rose+yellow_rose+lavender");
		}
		finally
		{
			RemoveChild(comboHomeGarden);
			comboHomeGarden.QueueFree();
		}
	}

	private async System.Threading.Tasks.Task AssertMissingCombinationNodeDoesNotCreateFallbackAsync()
	{
		PackedScene packedHome = GD.Load<PackedScene>("res://scenes/home/HomeGarden.tscn");
		HomeGardenView comboHomeGarden = packedHome.Instantiate<HomeGardenView>();
		AddChild(comboHomeGarden);
		await NextFrame();

		try
		{
			Control slot = comboHomeGarden.GetNode<Control>("FlowerSlotRoot/PinkRoseSlot_01");
			TextureRect comboNode = slot.GetNode<TextureRect>("PinkRoseYellowRoseComboTexture");
			slot.RemoveChild(comboNode);
			comboNode.QueueFree();

			RunSessionState comboState = new();
			AddSlotFlowers(comboState, 0, "pink_rose", "yellow_rose");

			comboHomeGarden.RefreshFlowers(comboState);
			await NextFrame();

			Assert(slot.GetNodeOrNull<TextureRect>("PinkRoseYellowRoseComboTexture") == null, "Missing combo node should remain missing.");
			Assert(slot.GetNodeOrNull<Control>("ComboAssetPlaceholder") == null, "Missing combo node should not create a dynamic placeholder.");
			Assert(!GetSlotTexture(comboHomeGarden, 0, "pink_rose").Visible, "Missing combo should not fall back to pink_rose single texture.");
			Assert(!GetSlotTexture(comboHomeGarden, 0, "yellow_rose").Visible, "Missing combo should not fall back to yellow_rose single texture.");
			Assert(!GetSlotTexture(comboHomeGarden, 0, "lavender").Visible, "Missing combo should not fall back to lavender single texture.");
		}
		finally
		{
			RemoveChild(comboHomeGarden);
			comboHomeGarden.QueueFree();
		}
	}

	private void CompleteGameScene()
	{
		GameManager gameManager = GetActiveScene().GetNode<GameManager>("Managers/GameManager");
		gameManager.EmitSignal(GameManager.SignalName.LevelCompleted);
	}

	private static void AssertGameSceneCauldron(Node gameScene)
	{
		Node2D? cauldronRoot = gameScene.GetNodeOrNull<Node2D>("WorldRoot/CauldronRoot");
		CauldronView? cauldronView = gameScene.GetNodeOrNull<CauldronView>("WorldRoot/CauldronRoot/CauldronView");
		Sprite2D? cauldronSprite = gameScene.GetNodeOrNull<Sprite2D>("WorldRoot/CauldronRoot/CauldronView/CauldronSprite");
		Label? progressLabel = gameScene.GetNodeOrNull<Label>("WorldRoot/CauldronRoot/CauldronView/CauldronProgressRoot/ProgressLabel");
		Control? rewardPanel = gameScene.GetNodeOrNull<Control>("WorldRoot/CauldronRoot/CauldronView/RewardPanel");
		TextureRect? seedTexture = gameScene.GetNodeOrNull<TextureRect>("WorldRoot/CauldronRoot/CauldronView/RewardPanel/SeedTexture");
		TextureRect? potionTexture = gameScene.GetNodeOrNull<TextureRect>("WorldRoot/CauldronRoot/CauldronView/RewardPanel/PotionTexture");
		Label? seedLabel = gameScene.GetNodeOrNull<Label>("WorldRoot/CauldronRoot/CauldronView/RewardPanel/SeedLabel");
		Label? potionLabel = gameScene.GetNodeOrNull<Label>("WorldRoot/CauldronRoot/CauldronView/RewardPanel/PotionLabel");
		Button? goPlantButton = gameScene.GetNodeOrNull<Button>("WorldRoot/CauldronRoot/CauldronView/RewardPanel/GoPlantButton");
		Node2D? bagRoot = gameScene.GetNodeOrNull<Node2D>("WorldRoot/BagRoot");

		Assert(cauldronRoot != null, "GameScene should contain CauldronRoot.");
		Assert(cauldronView != null, "GameScene should contain CauldronView.");
		Assert(cauldronSprite is { Texture: not null }, "GameScene should contain a Sprite2D CauldronSprite with the formal cauldron texture.");
		string? cauldronTexturePath = cauldronSprite?.Texture?.ResourcePath;
		Assert(cauldronTexturePath == "res://assets/cauldron/cauldron_reward.png", "CauldronSprite should only reference the formal cauldron asset path.");
		Assert(progressLabel != null && progressLabel.Text == "0/4", "Cauldron progress should start at 0/4.");
		AssertCauldronInitialBubblesAreEmpty(gameScene);
		Assert(rewardPanel != null && !rewardPanel.Visible, "Cauldron reward panel should start hidden.");
		Assert(seedTexture != null, "Cauldron reward panel should contain SeedTexture.");
		Assert(potionTexture != null, "Cauldron reward panel should contain PotionTexture.");
		Assert(seedLabel != null && seedLabel.Text == "种子 x1", "Cauldron reward panel should contain SeedLabel text.");
		Assert(potionLabel != null && potionLabel.Text == "药剂 x1", "Cauldron reward panel should contain PotionLabel text.");
		Assert(goPlantButton != null && goPlantButton.Text == "种植", "Cauldron reward panel should contain GoPlantButton.");
		Assert(bagRoot != null && !bagRoot.Visible, "Legacy BagRoot should be hidden in GameScene.");
	}

	private static void AssertCauldronInitialBubblesAreEmpty(Node gameScene)
	{
		AssertCauldronVisibleBubbleCount(gameScene, 4);
		for (int i = 0; i < 6; i++)
		{
			ColorRect bubble = gameScene.GetNode<ColorRect>($"WorldRoot/CauldronRoot/CauldronView/CauldronProgressRoot/Bubble_{i}");
			Assert(ColorNear(bubble.Color, new Color(0.58f, 0.58f, 0.58f, 0.34f)), $"Cauldron bubble {i} should start as a gray empty slot. Actual: {bubble.Color}.");
		}
	}

	private static void AssertCauldronProgressCollectionRefresh(Node gameScene)
	{
		CauldronView cauldronView = gameScene.GetNode<CauldronView>("WorldRoot/CauldronRoot/CauldronView");
		Label progressLabel = gameScene.GetNode<Label>("WorldRoot/CauldronRoot/CauldronView/CauldronProgressRoot/ProgressLabel");

		foreach (int requiredColorCount in new[] { 4, 5, 6 })
		{
			cauldronView.SetTargetColorCount(requiredColorCount);
			AssertCauldronVisibleBubbleCount(gameScene, requiredColorCount);

			cauldronView.RefreshProgress(new[] { WaterColor.Yellow, WaterColor.Green, WaterColor.Blue });

			Assert(progressLabel.Text == $"3/{requiredColorCount}", $"Cauldron progress label should support {requiredColorCount} target colors.");
			Assert(ColorNear(GetCauldronBubble(gameScene, 0).Color, new Color(0.96f, 0.78f, 0.34f, 0.95f)), "First collected yellow bottle should light Bubble_0 yellow.");
			Assert(ColorNear(GetCauldronBubble(gameScene, 1).Color, new Color(0.52f, 0.78f, 0.48f, 0.95f)), "Second collected green bottle should light Bubble_1 green.");
			Assert(ColorNear(GetCauldronBubble(gameScene, 2).Color, new Color(0.48f, 0.68f, 0.95f, 0.95f)), "Third collected blue bottle should light Bubble_2 blue.");
			for (int i = 3; i < requiredColorCount; i++)
			{
				Assert(ColorNear(GetCauldronBubble(gameScene, i).Color, new Color(0.58f, 0.58f, 0.58f, 0.34f)), $"Bubble_{i} should remain gray until collected.");
			}
		}

		cauldronView.SetTargetColorCount(4);
		cauldronView.RefreshProgress(Array.Empty<WaterColor>());
		Assert(progressLabel.Text == "0/4", "Cauldron progress should reset to the level target after dynamic slot checks.");
	}

	private static ColorRect GetCauldronBubble(Node gameScene, int index)
	{
		return gameScene.GetNode<ColorRect>($"WorldRoot/CauldronRoot/CauldronView/CauldronProgressRoot/Bubble_{index}");
	}

	private static void AssertCauldronVisibleBubbleCount(Node gameScene, int expectedVisibleCount)
	{
		for (int i = 0; i < 6; i++)
		{
			ColorRect bubble = GetCauldronBubble(gameScene, i);
			Assert(bubble.Visible == i < expectedVisibleCount, $"Bubble_{i} visibility should match target count {expectedVisibleCount}.");
		}
	}

	private static void AssertGameSceneCanvasControls(Node gameScene, bool expectExitVisible)
	{
		Button restartButton = gameScene.GetNode<Button>("CanvasLayer/RestartButton");
		Button exitButton = gameScene.GetNode<Button>("CanvasLayer/ExitButton");
		Label tipLabel = gameScene.GetNode<Label>("CanvasLayer/TipLabel");
		PopupPanel victoryPopup = gameScene.GetNode<PopupPanel>("CanvasLayer/VictoryPopup");
		Label victoryLabel = victoryPopup.GetNode<Label>("VictoryLabel");
		Button popupRestartButton = victoryPopup.GetNode<Button>("PopupRestartButton");

		Assert(restartButton.Visible && !restartButton.Disabled, "RestartButton should stay available in GameScene.");
		Assert(string.IsNullOrEmpty(exitButton.Text), "ExitButton should not draw extra text over the return button art.");
		Assert(exitButton.Icon != null, "ExitButton should use the return button texture.");
		Assert(exitButton.ExpandIcon, "ExitButton should expand the return button texture to its control bounds.");
		Assert(exitButton.Visible == expectExitVisible, $"ExitButton visible should be {expectExitVisible}.");
		Assert(exitButton.Disabled != expectExitVisible, $"ExitButton disabled should be {!expectExitVisible}.");
		Assert(tipLabel.LabelSettings != null && tipLabel.LabelSettings.OutlineSize >= 3, "TipLabel should use readable outline settings.");
		Assert(victoryLabel.LabelSettings != null && victoryLabel.LabelSettings.OutlineSize >= 3, "VictoryLabel should use readable outline settings.");
		Assert(popupRestartButton.GetThemeConstant("outline_size") >= 3, "PopupRestartButton text should have a readable outline.");
	}

	private async System.Threading.Tasks.Task AssertCauldronRewardPanelBehaviorAsync(Node2D gameScene)
	{
		CauldronView cauldronView = gameScene.GetNode<CauldronView>("WorldRoot/CauldronRoot/CauldronView");
		Control rewardPanel = gameScene.GetNode<Control>("WorldRoot/CauldronRoot/CauldronView/RewardPanel");
		TextureRect seedTexture = rewardPanel.GetNode<TextureRect>("SeedTexture");
		TextureRect potionTexture = rewardPanel.GetNode<TextureRect>("PotionTexture");
		Label titleLabel = rewardPanel.GetNode<Label>("RewardTitleLabel");
		Label flowerLabel = rewardPanel.GetNode<Label>("FlowerIdLabel");
		Button goPlantButton = rewardPanel.GetNode<Button>("GoPlantButton");

		int completedSignals = 0;
		GameManager gameManager = gameScene.GetNode<GameManager>("Managers/GameManager");
		gameManager.LevelCompleted += () => completedSignals++;

		System.Threading.Tasks.Task rewardTask = cauldronView.ShowRewardsAsync("pink_rose");
		await NextFrame();

		Assert(rewardPanel.Visible, "RewardPanel should be visible while rewards are being shown.");
		Assert(titleLabel.Text == "获得奖励", "RewardPanel title should be Chinese reward text.");
		Assert(flowerLabel.Text.Contains("pink_rose", StringComparison.Ordinal), "RewardPanel should show the target flower id.");
		Assert(seedTexture.Visible && seedTexture.Texture != null, "RewardPanel should load pink_rose seed texture.");
		Assert(potionTexture.Visible && potionTexture.Texture != null, "RewardPanel should load pink_rose potion texture.");

		goPlantButton.EmitSignal(BaseButton.SignalName.Pressed);
		goPlantButton.EmitSignal(BaseButton.SignalName.Pressed);
		await rewardTask;
		await NextFrame();

		Assert(!rewardPanel.Visible, "Clicking GoPlantButton should finish and hide RewardPanel immediately.");
		Assert(completedSignals == 0, "CauldronView button should not emit LevelCompleted directly.");
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
		Control options = flowerSelect.GetNode<Control>("Panel/FlowerSlots");
		Button button = options.GetNode<Button>($"FlowerSlot_{optionIndex + 1:00}/HotAreaButton");
		button.EmitSignal(BaseButton.SignalName.Pressed);
	}

	private static void PressLevelButton(LevelSelectView levelSelect, int levelNumber)
	{
		Control panelRoot = GetVisibleLevelPanelRoot(levelSelect);
		Button button = panelRoot.GetNode<Button>($"LevelSlots/LevelSlot_{levelNumber:00}/HotAreaButton");
		button.EmitSignal(BaseButton.SignalName.Pressed);
	}

	private static void AssertFlowerSelectOptions(FlowerSelectView flowerSelect)
	{
		Assert(flowerSelect.GetNode<TextureRect>("Background").Texture?.ResourcePath == "res://assets/reward/reward_background.png",
			"FlowerSelect outer background should keep the shared watercolor background.");
		Assert(flowerSelect.GetNode<TextureRect>("Panel/PanelTexture").Texture?.ResourcePath == "res://assets/ui/flower_select/panel_base.png",
			"FlowerSelect PanelTexture should use the PSD formal panel base.");
		Button backButton = flowerSelect.GetNode<Button>("Panel/BackButton");
		Assert(backButton.Visible, "FlowerSelect BackButton should be visible.");
		Assert(backButton.Text == "返回", $"FlowerSelect BackButton should show 返回. Actual: {backButton.Text}.");
		Assert(backButton.GetThemeStylebox("normal") is StyleBoxTexture, "FlowerSelect BackButton should use a visible PSD texture style.");

		Control options = flowerSelect.GetNode<Control>("Panel/FlowerSlots");
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
			Control slot = options.GetNode<Control>($"FlowerSlot_{i + 1:00}");
			Assert(slot.GetNode<TextureRect>("OpenCardTexture") != null, $"Flower option {i} should expose OpenCardTexture.");
			Assert(slot.GetNode<TextureRect>("DisabledCardTexture") != null, $"Flower option {i} should expose DisabledCardTexture.");
			Assert(slot.GetNode<Button>("HotAreaButton") != null, $"Flower option {i} should expose HotAreaButton.");
			Assert(slot.GetNode<Label>("FlowerName").Text == expectedNames[i], $"Flower option {i} should show {expectedNames[i]}.");
		}

		AssertFlowerCardVisibility(flowerSelect, 0, expectedOpen: true);
		AssertFlowerCardVisibility(flowerSelect, 1, expectedOpen: true);
		AssertFlowerCardVisibility(flowerSelect, 2, expectedOpen: true);
		AssertFlowerCardVisibility(flowerSelect, 3, expectedOpen: false);
		AssertFlowerCardVisibility(flowerSelect, 4, expectedOpen: false);
		AssertFlowerCardVisibility(flowerSelect, 5, expectedOpen: false);

		AssertFlowerOptionStatus(flowerSelect, 0, string.Empty);
		AssertFlowerOptionStatus(flowerSelect, 1, string.Empty);
		AssertFlowerOptionStatus(flowerSelect, 2, string.Empty);
		AssertFlowerOptionStatus(flowerSelect, 3, "待开放");
		AssertFlowerOptionStatus(flowerSelect, 4, "待开放");
		AssertFlowerOptionStatus(flowerSelect, 5, "待开放");
	}

	private static void AssertFlowerCardVisibility(FlowerSelectView flowerSelect, int optionIndex, bool expectedOpen)
	{
		Control slot = flowerSelect.GetNode<Control>($"Panel/FlowerSlots/FlowerSlot_{optionIndex + 1:00}");
		Assert(slot.GetNode<TextureRect>("OpenCardTexture").Visible == expectedOpen, $"Flower option {optionIndex} open card visibility mismatch.");
		Assert(slot.GetNode<TextureRect>("DisabledCardTexture").Visible == !expectedOpen, $"Flower option {optionIndex} disabled card visibility mismatch.");
	}

	private static void AssertFlowerOptionStatus(FlowerSelectView flowerSelect, int optionIndex, string expectedStatus)
	{
		Control options = flowerSelect.GetNode<Control>("Panel/FlowerSlots");
		Control slot = options.GetNode<Control>($"FlowerSlot_{optionIndex + 1:00}");
		Label? statusLabel = slot.GetNodeOrNull<Label>("StatusLabel");

		if (string.IsNullOrEmpty(expectedStatus))
		{
			Assert(statusLabel == null || !statusLabel.Visible || string.IsNullOrEmpty(statusLabel.Text), $"Flower option {optionIndex} should not show a status label.");
			return;
		}

		Assert(statusLabel != null, $"Flower option {optionIndex} should show status {expectedStatus}.");
		Assert(statusLabel!.Visible, $"Flower option {optionIndex} status should be visible.");
		Assert(statusLabel.Text == expectedStatus, $"Flower option {optionIndex} status should be {expectedStatus}. Actual: {statusLabel.Text}.");
	}

	private static void AssertLevelButtons(LevelSelectView levelSelect, params FlowerLevelState[] expectedStates)
	{
		Control panelRoot = GetVisibleLevelPanelRoot(levelSelect);
		Control options = panelRoot.GetNode<Control>("LevelSlots");
		Assert(options.GetChildCount() == 7, $"LevelSelect should show 7 levels. Actual: {options.GetChildCount()}.");

		for (int i = 0; i < expectedStates.Length; i++)
		{
			Control slot = options.GetNode<Control>($"LevelSlot_{i + 1:00}");
			Button button = slot.GetNode<Button>("HotAreaButton");
			Label statusLabel = slot.GetNode<Label>("TextRoot/StatusLabel");
			string expectedStatus = GetExpectedLevelStatusText(expectedStates[i]);
			Assert(!string.IsNullOrWhiteSpace(statusLabel.Text) && statusLabel.Visible, $"Level {i + 1} should show a visible status label. Actual: {statusLabel.Text} / {statusLabel.Visible}.");
			Assert(statusLabel.Text == expectedStatus, $"Level {i + 1} status text should be {expectedStatus}. Actual: {statusLabel.Text}.");
			Assert(!button.Disabled, $"Level {i + 1} hot area should stay enabled for playable/tip handling.");
			Assert(slot.GetNode<TextureRect>("AvailableTexture").Visible == (expectedStates[i] == FlowerLevelState.Playable), $"Level {i + 1} available texture visibility mismatch.");
			Assert(slot.GetNode<TextureRect>("CompletedTexture").Visible == (expectedStates[i] == FlowerLevelState.Completed), $"Level {i + 1} completed texture visibility mismatch.");
			Assert(slot.GetNode<TextureRect>("LockedTexture").Visible == (expectedStates[i] == FlowerLevelState.Locked), $"Level {i + 1} locked texture visibility mismatch.");
		}
	}

	private static void AssertLevelSelectArt(LevelSelectView levelSelect, string flowerId)
	{
		TextureRect background = levelSelect.GetNode<TextureRect>("Background");
		Assert(background.Texture != null, $"LevelSelect {flowerId} should keep the watercolor garden background.");
		Assert(
			background.Texture!.ResourcePath == "res://assets/reward/reward_background.png",
			$"LevelSelect {flowerId} background should match FlowerSelect. Actual: {background.Texture.ResourcePath}");

		Control panelRoot = levelSelect.GetNode<Control>($"FlowerPanelsRoot/{GetPanelRootName(flowerId)}");
		Assert(panelRoot.Visible, $"LevelSelect {flowerId} should show its own PanelRoot.");
		AssertOnlyExpectedPanelRootVisible(levelSelect, flowerId);

		TextureRect panelTexture = panelRoot.GetNode<TextureRect>("PanelTexture");
		Assert(panelTexture.Texture != null, $"LevelSelect {flowerId} should load panel art.");
		Assert(
			panelTexture.Texture!.ResourcePath == $"res://assets/flowers/{flowerId}/level_select/panel.png",
			$"LevelSelect {flowerId} panel should use formal art. Actual: {panelTexture.Texture.ResourcePath}");

		Control options = panelRoot.GetNode<Control>("LevelSlots");
		Assert(options.GetChildCount() == 7, $"LevelSelect {flowerId} should have 7 editable level slots.");
		for (int i = 0; i < options.GetChildCount(); i++)
		{
			Control slot = options.GetNode<Control>($"LevelSlot_{i + 1:00}");
			Assert(slot.GetNodeOrNull<Label>("TextRoot/LevelNameNumberLabel") != null, $"LevelSelect {flowerId} level {i + 1} should expose LevelNameNumberLabel.");
			Assert(slot.GetNodeOrNull<Label>("TextRoot/FlowerNameLabel") == null, $"LevelSelect {flowerId} level {i + 1} should not keep separate FlowerNameLabel.");
			Assert(slot.GetNodeOrNull<Label>("TextRoot/LevelNumberLabel") == null, $"LevelSelect {flowerId} level {i + 1} should not keep separate LevelNumberLabel.");
			Assert(slot.GetNodeOrNull<Label>("TextRoot/StatusLabel") != null, $"LevelSelect {flowerId} level {i + 1} should expose StatusLabel.");
			Assert(slot.GetNodeOrNull<Button>("HotAreaButton") != null, $"LevelSelect {flowerId} level {i + 1} should expose HotAreaButton.");
			AssertLevelStateTexture(slot, flowerId, i + 1, "AvailableTexture", "available");
			AssertLevelStateTexture(slot, flowerId, i + 1, "CompletedTexture", "completed");
			AssertLevelStateTexture(slot, flowerId, i + 1, "LockedTexture", "locked");
		}

		Button backButton = panelRoot.GetNode<Button>("BackButton");
		Assert(backButton.Text.Contains("\u8fd4\u56de", StringComparison.Ordinal) && backButton.Visible, $"LevelSelect BackButton should be visible and show return text. Actual text: {backButton.Text} / visible: {backButton.Visible}.");
		Assert(backButton.Flat, "LevelSelect BackButton should only draw text over the panel art.");
		Assert(backButton.GetThemeStylebox("normal") is StyleBoxEmpty, "LevelSelect BackButton normal style should be empty.");
		Assert(backButton.GetThemeStylebox("hover") is StyleBoxEmpty, "LevelSelect BackButton hover style should be empty.");
		Assert(backButton.GetThemeStylebox("pressed") is StyleBoxEmpty, "LevelSelect BackButton pressed style should be empty.");
	}

	private static string GetExpectedLevelStatusText(FlowerLevelState state)
	{
		return state switch
		{
			FlowerLevelState.Completed => "\u5df2\u5b8c\u6210",
			FlowerLevelState.Playable => "\u53ef\u8fdb\u5165",
			_ => "\u672a\u89e3\u9501"
		};
	}

	private static void AssertLevelStateTexture(Control slot, string flowerId, int levelNumber, string nodeName, string stateKey)
	{
		TextureRect textureNode = slot.GetNode<TextureRect>(nodeName);
		Assert(textureNode.Texture != null, $"LevelSelect {flowerId} level {levelNumber} {nodeName} should have a texture.");
		Assert(
			textureNode.Texture!.ResourcePath == $"res://assets/flowers/{flowerId}/level_select/level_{levelNumber:00}_{stateKey}.png",
			$"LevelSelect {flowerId} level {levelNumber} {nodeName} should use formal {stateKey} art. Actual: {textureNode.Texture.ResourcePath}");
	}

	private static Control GetVisibleLevelPanelRoot(LevelSelectView levelSelect)
	{
		Control root = levelSelect.GetNode<Control>("FlowerPanelsRoot");
		for (int i = 0; i < root.GetChildCount(); i++)
		{
			if (root.GetChild(i) is Control child && child.Visible)
			{
				return child;
			}
		}

		throw new InvalidOperationException("LevelSelect should have one visible PanelRoot.");
	}

	private static string GetPanelRootName(string flowerId)
	{
		return flowerId switch
		{
			"yellow_rose" => "YellowRosePanelRoot",
			"lavender" => "LavenderPanelRoot",
			_ => "PinkRosePanelRoot"
		};
	}

	private static void AssertOnlyExpectedPanelRootVisible(LevelSelectView levelSelect, string flowerId)
	{
		Control root = levelSelect.GetNode<Control>("FlowerPanelsRoot");
		string expected = GetPanelRootName(flowerId);
		for (int i = 0; i < root.GetChildCount(); i++)
		{
			Control child = root.GetChild<Control>(i);
			Assert(child.Visible == (child.Name == expected), $"Only {expected} should be visible. {child.Name} visible: {child.Visible}.");
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

	private static void AssertComboTexture(HomeGardenView homeGarden, int slotIndex, string comboKey)
	{
		Control? placeholder = homeGarden.GetNodeOrNull<Control>($"FlowerSlotRoot/PinkRoseSlot_{slotIndex + 1:00}/ComboAssetPlaceholder");
		Assert(placeholder == null, $"Slot {slotIndex + 1} should use scene combo node instead of dynamic placeholder for {comboKey}.");

		TextureRect texture = GetSlotTexture(homeGarden, slotIndex, comboKey);
		Assert(texture.Visible, $"Slot {slotIndex + 1} combo texture should be visible for {comboKey}.");
		Assert(texture.Texture != null, $"Slot {slotIndex + 1} combo texture should come from the scene node for {comboKey}.");
		Assert(!GetSlotTexture(homeGarden, slotIndex, "pink_rose").Visible, "Combo texture should not stack pink_rose single texture.");
		Assert(!GetSlotTexture(homeGarden, slotIndex, "yellow_rose").Visible, "Combo texture should not stack yellow_rose single texture.");
		Assert(!GetSlotTexture(homeGarden, slotIndex, "lavender").Visible, "Combo texture should not stack lavender single texture.");
	}

	private static void AssertPendingRewardPreviewHidden(HomeGardenView homeGarden, string label)
	{
		Control preview = homeGarden.GetNode<Control>("PendingRewardPreview");
		TextureRect seedTexture = preview.GetNode<TextureRect>("SeedTexture");
		TextureRect potionTexture = preview.GetNode<TextureRect>("PotionTexture");
		Assert(!preview.Visible, $"{label}: PendingRewardPreview should be hidden.");
		Assert(!seedTexture.Visible, $"{label}: SeedTexture should be hidden.");
		Assert(!potionTexture.Visible, $"{label}: PotionTexture should be hidden.");
	}

	private static void AssertPlantingFxActive(HomeGardenView homeGarden, string label)
	{
		Control layer = homeGarden.GetNode<Control>("PlantingFxLayer");
		TextureRect seedTexture = layer.GetNode<TextureRect>("SeedFxTexture");
		TextureRect potionTexture = layer.GetNode<TextureRect>("PotionFxTexture");
		Assert(layer.Visible, $"{label}: PlantingFxLayer should be visible.");
		Assert(seedTexture.Visible, $"{label}: SeedFxTexture should be visible only during animation.");
		Assert(potionTexture.Visible, $"{label}: PotionFxTexture should be visible only during animation.");
	}

	private static void AssertPlantingFxHidden(HomeGardenView homeGarden, string label)
	{
		Control layer = homeGarden.GetNode<Control>("PlantingFxLayer");
		TextureRect seedTexture = layer.GetNode<TextureRect>("SeedFxTexture");
		TextureRect potionTexture = layer.GetNode<TextureRect>("PotionFxTexture");
		Assert(!layer.Visible, $"{label}: PlantingFxLayer should be hidden.");
		Assert(!seedTexture.Visible, $"{label}: SeedFxTexture should be hidden.");
		Assert(!potionTexture.Visible, $"{label}: PotionFxTexture should be hidden.");
	}

	private static void AssertRewardItemResourcesExist()
	{
		AssertRewardItemResourceExists("pink_rose", "seed");
		AssertRewardItemResourceExists("pink_rose", "potion");
		AssertRewardItemResourceExists("yellow_rose", "seed");
		AssertRewardItemResourceExists("yellow_rose", "potion");
		AssertRewardItemResourceExists("lavender", "seed");
		AssertRewardItemResourceExists("lavender", "potion");
	}

	private static void AssertRewardItemResourceExists(string flowerId, string itemKind)
	{
		string path = BuildRewardItemPath(flowerId, itemKind);
		Assert(ResourceLoader.Exists(path) || FileAccess.FileExists(path), $"Reward item resource should exist: {path}");
	}

	private static string BuildRewardItemPath(string flowerId, string itemKind)
	{
		return CauldronView.BuildRewardItemPath(flowerId, itemKind);
	}

	private static void AssertRunSessionStateDoesNotStoreImagePaths(RunSessionState state)
	{
		const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

		foreach (FieldInfo field in typeof(RunSessionState).GetFields(flags))
		{
			Assert(!field.Name.Contains("Texture", StringComparison.OrdinalIgnoreCase), "RunSessionState should not define texture fields.");
			Assert(!field.Name.Contains("Path", StringComparison.OrdinalIgnoreCase), "RunSessionState should not define path fields.");

			object? value = field.IsStatic ? field.GetValue(null) : field.GetValue(state);
			if (value is string text)
			{
				AssertStringIsNotImagePath(text, $"RunSessionState field {field.Name}");
			}
			else if (value is IEnumerable<string> strings)
			{
				foreach (string item in strings)
				{
					AssertStringIsNotImagePath(item, $"RunSessionState field {field.Name}");
				}
			}
		}

		foreach (PropertyInfo property in typeof(RunSessionState).GetProperties(flags))
		{
			Assert(!property.Name.Contains("Texture", StringComparison.OrdinalIgnoreCase), "RunSessionState should not expose texture properties.");
			Assert(!property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase), "RunSessionState should not expose path properties.");
		}
	}

	private static void AssertStringIsNotImagePath(string value, string label)
	{
		Assert(!value.Contains("res://", StringComparison.OrdinalIgnoreCase), $"{label} should not store a res:// resource path.");
		Assert(!value.Contains(".png", StringComparison.OrdinalIgnoreCase), $"{label} should not store a PNG path.");
		Assert(!value.Contains("assets/items", StringComparison.OrdinalIgnoreCase), $"{label} should not store an item asset path.");
		Assert(!value.Contains("assets/flowers", StringComparison.OrdinalIgnoreCase), $"{label} should not store a flower asset path.");
	}

	private static void AddSlotFlowers(RunSessionState state, int slotIndex, params string[] flowerIds)
	{
		foreach (string flowerId in flowerIds)
		{
			PlantingResult result = state.TryAddFlowerToSlot(slotIndex, flowerId);
			Assert(result == PlantingResult.Planted, $"Expected to add {flowerId} to slot {slotIndex + 1}. Actual: {result}.");
		}
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

	private static void AssertFlowerFixedHint(FlowerSelectView flowerSelect, string expected)
	{
		Label hint = flowerSelect.GetNode<Label>("Panel/HintLabel");
		Assert(hint.Text == expected && hint.Visible, $"FlowerSelect fixed hint should stay visible as {expected}. Actual: {hint.Text} / {hint.Visible}.");
	}

	private static void AssertFlowerTemporaryTip(FlowerSelectView flowerSelect, string expected)
	{
		Label tip = flowerSelect.GetNode<Label>("Panel/TemporaryTipLabel");
		Assert(tip.Text == expected && tip.Visible, $"FlowerSelect temporary tip should be {expected}. Actual: {tip.Text} / {tip.Visible}.");
	}

	private static void AssertLevelFixedMessage(LevelSelectView levelSelect, string expected)
	{
		Label message = levelSelect.GetNode<Label>("CommonTextRoot/MessageLabel");
		Assert(message.Text == expected && message.Visible, $"LevelSelect fixed message should stay visible as {expected}. Actual: {message.Text} / {message.Visible}.");
	}

	private static void AssertLevelTemporaryTip(LevelSelectView levelSelect, string expected)
	{
		Label tip = levelSelect.GetNode<Label>("CommonTextRoot/TemporaryTipLabel");
		Assert(tip.Text == expected && tip.Visible, $"LevelSelect temporary tip should be {expected}. Actual: {tip.Text} / {tip.Visible}.");
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
			"pink_rose+lavender" => "PinkRoseLavenderComboTexture",
			"pink_rose+yellow_rose" => "PinkRoseYellowRoseComboTexture",
			"yellow_rose+lavender" => "YellowRoseLavenderComboTexture",
			"pink_rose+yellow_rose+lavender" => "PinkRoseYellowRoseLavenderComboTexture",
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

	private async System.Threading.Tasks.Task AssertSavedRewardAndProgressAsync(
		string flowerId,
		int expectedCompletedCount,
		int expectedSeedCount,
		int expectedPotionCount)
	{
		AssertSaveDataRewardAndProgress(
			GetSaveData(),
			flowerId,
			expectedCompletedCount,
			expectedSeedCount,
			expectedPotionCount,
			"MainFlowController current SaveData");

		for (int i = 0; i < 90; i++)
		{
			await NextFrame();
			if (!FileAccess.FileExists(FlowSavePath))
			{
				continue;
			}

			SaveSystem reloadSystem = new();
			SaveData reloaded = reloadSystem.LoadOrCreate(FlowSavePath);
			if (SaveDataHasRewardAndProgress(reloaded, flowerId, expectedCompletedCount, expectedSeedCount, expectedPotionCount))
			{
				return;
			}
		}

		SaveSystem finalReloadSystem = new();
		SaveData finalReload = finalReloadSystem.LoadOrCreate(FlowSavePath);
		AssertSaveDataRewardAndProgress(
			finalReload,
			flowerId,
			expectedCompletedCount,
			expectedSeedCount,
			expectedPotionCount,
			"Reloaded SaveData");
	}

	private static bool SaveDataHasRewardAndProgress(
		SaveData saveData,
		string flowerId,
		int expectedCompletedCount,
		int expectedSeedCount,
		int expectedPotionCount)
	{
		saveData.Normalize();
		return saveData.LevelProgressByFlower[flowerId] == expectedCompletedCount
			&& saveData.WarehouseInventoryByFlower[flowerId].SeedCount == expectedSeedCount
			&& saveData.WarehouseInventoryByFlower[flowerId].PotionCount == expectedPotionCount;
	}

	private static void AssertSaveDataRewardAndProgress(
		SaveData saveData,
		string flowerId,
		int expectedCompletedCount,
		int expectedSeedCount,
		int expectedPotionCount,
		string label)
	{
		saveData.Normalize();
		Assert(saveData.LevelProgressByFlower[flowerId] == expectedCompletedCount, $"{label}: {flowerId} level progress should be {expectedCompletedCount}. Actual: {saveData.LevelProgressByFlower[flowerId]}.");
		Assert(saveData.WarehouseInventoryByFlower[flowerId].SeedCount == expectedSeedCount, $"{label}: {flowerId} seed_count should be {expectedSeedCount}. Actual: {saveData.WarehouseInventoryByFlower[flowerId].SeedCount}.");
		Assert(saveData.WarehouseInventoryByFlower[flowerId].PotionCount == expectedPotionCount, $"{label}: {flowerId} potion_count should be {expectedPotionCount}. Actual: {saveData.WarehouseInventoryByFlower[flowerId].PotionCount}.");
	}

	private void AssertNoPendingRewardFlags(string label)
	{
		RunSessionState state = GetState();
		Assert(!state.PendingPlanting, $"{label}: PendingPlanting should be false.");
		Assert(!state.HasSeed, $"{label}: HasSeed should be false.");
		Assert(!state.HasPotion, $"{label}: HasPotion should be false.");
		Assert(state.PendingPlantingFlowerId == null, $"{label}: PendingPlantingFlowerId should be null.");
	}

	private static void AssertNoPlantMarkers(HomeGardenView homeGarden, string label)
	{
		for (int i = 0; i < RunSessionState.MaxFlowerCount; i++)
		{
			Button marker = GetPlantMarker(homeGarden, i);
			Assert(!marker.Visible, $"{label}: marker {i + 1} should stay hidden when rewards go to warehouse.");
			Assert(marker.Disabled, $"{label}: marker {i + 1} should stay disabled when rewards go to warehouse.");
		}
	}

	private static void AssertWarehouseRow(WarehousePageView warehouse, string flowerId, int expectedSeedCount, int expectedPotionCount)
	{
		Control row = warehouse.GetNode<Control>($"Panel/Content/Scroll/ItemList/Row_{flowerId}");
		Label seedCount = row.GetNode<Label>("RowRoot/SeedGroup/SeedCountLabel");
		Label potionCount = row.GetNode<Label>("RowRoot/PotionGroup/PotionCountLabel");
		Assert(seedCount.Text == $"种子 x{expectedSeedCount}", $"{flowerId} seed count should be {expectedSeedCount}. Actual: {seedCount.Text}.");
		Assert(potionCount.Text == $"药剂 x{expectedPotionCount}", $"{flowerId} potion count should be {expectedPotionCount}. Actual: {potionCount.Text}.");
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

	private async System.Threading.Tasks.Task WaitForHomeGardenStatusToHideAsync(HomeGardenView homeGarden)
	{
		await WaitSecondsAsync(3.1);
		AssertHomeGardenStatusHidden(homeGarden, "HomeGarden status");
	}

	private async System.Threading.Tasks.Task WaitForFlowerSelectTemporaryTipToHideAsync(FlowerSelectView flowerSelect)
	{
		await WaitSecondsAsync(3.1);
		Label fixedHint = flowerSelect.GetNode<Label>("Panel/HintLabel");
		Label tip = flowerSelect.GetNode<Label>("Panel/TemporaryTipLabel");
		Assert(fixedHint.Text == "选择 1 种花，完成药剂调试后回花园种植" && fixedHint.Visible, "FlowerSelect fixed hint should not auto-hide.");
		Assert(string.IsNullOrEmpty(tip.Text) && !tip.Visible, "FlowerSelect temporary tip should auto-hide after 3 seconds.");
	}

	private async System.Threading.Tasks.Task WaitForLevelTemporaryTipToHideAsync(LevelSelectView levelSelect)
	{
		await WaitSecondsAsync(3.1);
		Label fixedMessage = levelSelect.GetNode<Label>("CommonTextRoot/MessageLabel");
		Label tip = levelSelect.GetNode<Label>("CommonTextRoot/TemporaryTipLabel");
		Assert(fixedMessage.Text == "选择当前可玩关卡" && fixedMessage.Visible, "LevelSelect fixed message should not auto-hide.");
		Assert(string.IsNullOrEmpty(tip.Text) && !tip.Visible, "LevelSelect temporary tip should auto-hide after 3 seconds.");
	}

	private static void AssertHomeGardenStatusHidden(HomeGardenView homeGarden, string label)
	{
		Label status = homeGarden.GetNode<Label>("PlantingStatusLabel");
		Assert(string.IsNullOrEmpty(status.Text) && !status.Visible, $"{label} should be hidden.");
	}

	private async System.Threading.Tasks.Task WaitSecondsAsync(double seconds)
	{
		SceneTreeTimer timer = GetTree().CreateTimer(seconds);
		await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
	}

	private async System.Threading.Tasks.Task NextFrame()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private static bool ColorNear(Color actual, Color expected, float tolerance = 0.01f)
	{
		return Mathf.Abs(actual.R - expected.R) <= tolerance
			&& Mathf.Abs(actual.G - expected.G) <= tolerance
			&& Mathf.Abs(actual.B - expected.B) <= tolerance
			&& Mathf.Abs(actual.A - expected.A) <= tolerance;
	}

	private async System.Threading.Tasks.Task WaitForPlantingToFinishAsync()
	{
		for (int i = 0; i < 90; i++)
		{
			await NextFrame();
			if (!GetState().PendingPlanting)
			{
				return;
			}
		}

		throw new TimeoutException("Planting animation did not finish and commit within 90 frames.");
	}

	private static void Assert(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
