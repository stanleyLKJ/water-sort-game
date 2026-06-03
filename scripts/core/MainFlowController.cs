#nullable enable

using Godot;
using WaterSortGame.Model;
using WaterSortGame.View;

namespace WaterSortGame.Core;

public sealed partial class MainFlowController : Node
{
	private const string HomeGardenScenePath = "res://scenes/home/HomeGarden.tscn";
	private const string LevelSelectScenePath = "res://scenes/level_select/LevelSelect.tscn";
	private const string FlowerSelectScenePath = "res://scenes/flower_select/FlowerSelect.tscn";
	private const string GameScenePath = "res://GameScene.tscn";

	private readonly RunSessionState _runSessionState = new();
	private readonly FlowerSelectSystem _flowerSelectSystem = new();

	private Node _sceneHost = null!;
	private Node? _activeScene;

	public override void _Ready()
	{
		_sceneHost = GetNode<Node>("SceneHost");
		ShowHomeGarden();
	}

	private void ShowHomeGarden()
	{
		Node scene = LoadScene(HomeGardenScenePath);

		if (scene is HomeGardenView homeGarden)
		{
			homeGarden.StartGameRequested += ShowFlowerSelect;
			homeGarden.LevelSelectRequested += ShowLevelSelect;
			homeGarden.FlowerSlotPlantRequested += slotIndex => OnFlowerSlotPlantRequested(homeGarden, slotIndex);
			homeGarden.RefreshFlowers(_runSessionState);
		}

		SetActiveScene(scene);
	}

	private void ShowLevelSelect()
	{
		Node scene = LoadScene(LevelSelectScenePath);

		if (scene is LevelSelectView levelSelect)
		{
			levelSelect.LevelOneRequested += ShowFlowerSelect;
			levelSelect.BackRequested += ShowHomeGarden;
		}

		SetActiveScene(scene);
	}

	private void ShowFlowerSelect()
	{
		Node scene = LoadScene(FlowerSelectScenePath);

		if (scene is FlowerSelectView flowerSelect)
		{
			flowerSelect.SetFlowerOptions(_flowerSelectSystem.CreateBaseFlowerOptions());
			flowerSelect.TargetFlowerSelected += OnTargetFlowerSelected;
			flowerSelect.BackRequested += ShowHomeGarden;
		}

		SetActiveScene(scene);
	}

	private void ShowGameScene()
	{
		Node scene = LoadScene(GameScenePath);
		GameManager? gameManager = scene.GetNodeOrNull<GameManager>("Managers/GameManager");

		if (gameManager != null)
		{
			gameManager.IsManagedByMainFlow = true;
			gameManager.LevelCompleted += OnLevelCompleted;
		}
		else
		{
			GD.PushWarning("MainFlowController could not find GameScene Managers/GameManager; reward flow will not run.");
		}

		SetActiveScene(scene);
	}

	private void OnTargetFlowerSelected(string flowerId)
	{
		_runSessionState.SelectTargetFlower(flowerId);
		ShowGameScene();
	}

	private void OnLevelCompleted()
	{
		bool rewardCreated = _runSessionState.CreatePendingPlantingReward();
		if (!rewardCreated)
		{
			GD.Print("Pending planting reward was not created. The garden may be full or no target flower was selected.");
		}

		ShowHomeGarden();
	}

	private void OnFlowerSlotPlantRequested(HomeGardenView homeGarden, int slotIndex)
	{
		PlantingResult result = _runSessionState.TryPlantPendingRewardAt(slotIndex);
		switch (result)
		{
			case PlantingResult.Planted:
				homeGarden.RefreshFlowers(_runSessionState);
				homeGarden.PlayPlantingFeedback(slotIndex);
				break;
			case PlantingResult.SlotOccupied:
				homeGarden.ShowMessage("该花位已有花");
				break;
			case PlantingResult.NoPendingReward:
				homeGarden.ShowMessage(_runSessionState.IsGardenFull ? string.Empty : "先完成药剂调试");
				break;
		}
	}

	private static Node LoadScene(string scenePath)
	{
		PackedScene packedScene = GD.Load<PackedScene>(scenePath);
		return packedScene.Instantiate();
	}

	private void SetActiveScene(Node scene)
	{
		if (_activeScene != null)
		{
			_sceneHost.RemoveChild(_activeScene);
			_activeScene.QueueFree();
		}

		_activeScene = scene;
		_sceneHost.AddChild(scene);
	}
}
