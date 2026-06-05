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
			homeGarden.StartGameRequested += () => OnStartGameRequested(homeGarden);
			homeGarden.LevelSelectRequested += () => homeGarden.ShowMessage("请点击开始游戏");
			homeGarden.FlowerSlotPlantRequested += slotIndex => OnFlowerSlotPlantRequested(homeGarden, slotIndex);
			homeGarden.RefreshFlowers(_runSessionState);
		}

		SetActiveScene(scene);
	}

	private void OnStartGameRequested(HomeGardenView homeGarden)
	{
		if (_runSessionState.PendingPlanting)
		{
			homeGarden.ShowMessage("请先完成本次种植或追加");
			return;
		}

		ShowFlowerSelect();
	}

	private void ShowLevelSelect(string? message = null)
	{
		if (!_runSessionState.HasSelectedFlower)
		{
			ShowFlowerSelect();
			return;
		}

		string flowerId = _runSessionState.SelectedFlowerId!;
		Node scene = LoadScene(LevelSelectScenePath);

		if (scene is LevelSelectView levelSelect)
		{
			string displayName = _flowerSelectSystem.GetDisplayName(flowerId);
			levelSelect.SetLevelOptions($"{displayName} 关卡", BuildLevelOptions(flowerId, displayName));
			levelSelect.LevelSelected += OnLevelSelected;
			levelSelect.BackRequested += ShowFlowerSelect;
			if (!string.IsNullOrWhiteSpace(message))
			{
				levelSelect.ShowMessage(message);
			}
		}

		SetActiveScene(scene);
	}

	private void ShowFlowerSelect()
	{
		Node scene = LoadScene(FlowerSelectScenePath);

		if (scene is FlowerSelectView flowerSelect)
		{
			flowerSelect.SetFlowerOptions(_flowerSelectSystem.CreateBaseFlowerOptions(_runSessionState));
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
		if (!RunSessionState.IsOpenFlowerId(flowerId))
		{
			return;
		}

		if (_runSessionState.IsFlowerFull(flowerId))
		{
			ShowFlowerSelect();
			return;
		}

		_runSessionState.SelectTargetFlower(flowerId);
		ShowLevelSelect();
	}

	private void OnLevelSelected(int levelNumber)
	{
		if (!_runSessionState.HasSelectedFlower)
		{
			ShowFlowerSelect();
			return;
		}

		string flowerId = _runSessionState.SelectedFlowerId!;
		if (!_runSessionState.TrySelectPlayableLevel(flowerId, levelNumber))
		{
			ShowLevelSelect("该关卡暂不可进入");
			return;
		}

		ShowGameScene();
	}

	private void OnLevelCompleted()
	{
		bool rewardCreated = _runSessionState.CompleteSelectedLevelAndCreatePendingPlantingReward();
		if (!rewardCreated)
		{
			GD.Print("Pending planting reward was not created. The flower may be full, the selected level may be invalid, or no target flower was selected.");
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
			case PlantingResult.FlowerAlreadyInSlot:
				homeGarden.ShowMessage("该花位已有本次奖励花");
				break;
			case PlantingResult.FlowerAlreadyFull:
				homeGarden.ShowMessage("该花已种满，请选择其他花");
				break;
			case PlantingResult.NoPendingReward:
				homeGarden.ShowMessage("先完成药剂调试");
				break;
		}
	}

	private LevelSelectOption[] BuildLevelOptions(string flowerId, string displayName)
	{
		LevelSelectOption[] options = new LevelSelectOption[RunSessionState.LevelsPerFlower];
		for (int i = 0; i < options.Length; i++)
		{
			int levelNumber = i + 1;
			options[i] = new LevelSelectOption(
				levelNumber,
				$"{displayName} 第 {levelNumber} 关",
				_runSessionState.GetLevelState(flowerId, levelNumber));
		}

		return options;
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
