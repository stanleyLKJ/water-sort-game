#nullable enable

using System.Collections.Generic;
using Godot;
using WaterSortGame.Model;
using WaterSortGame.View;

namespace WaterSortGame.Core;

public sealed partial class MainFlowController : Node
{
	private const string HomeGardenScenePath = "res://scenes/home/HomeGarden.tscn";
	private const string LevelSelectScenePath = "res://scenes/level_select/LevelSelect.tscn";
	private const string FlowerSelectScenePath = "res://scenes/flower_select/FlowerSelect.tscn";
	private const string WarehouseScenePath = "res://scenes/warehouse/WarehousePage.tscn";
	private const string PlantingScenePath = "res://scenes/planting/PlantingPage.tscn";
	private const string SettingsScenePath = "res://scenes/settings/SettingsPage.tscn";
	private const string GameScenePath = "res://GameScene.tscn";

	private readonly RunSessionState _runSessionState = new();
	private readonly FlowerSelectSystem _flowerSelectSystem = new();
	private readonly PlantingSystem _plantingSystem = new();

	private Node _sceneHost = null!;
	private Node? _activeScene;
	private SaveSystem _saveSystem = null!;
	private SaveData _saveData = null!;
	private AudioManager _audioManager = null!;
	private LocalizationManager _localizationManager = null!;
	private TutorialSystem _tutorialSystem = null!;
	private TutorialBubbleView _tutorialBubbleView = null!;
	private bool _plantingInputLocked;

	public string? SavePathOverride { get; set; }

	public override void _Ready()
	{
		_sceneHost = GetNode<Node>("SceneHost");
		_audioManager = GetNodeOrNull<AudioManager>("AudioManager") ?? CreateRuntimeAudioManager();
		_localizationManager = GetNodeOrNull<LocalizationManager>("LocalizationManager") ?? CreateRuntimeLocalizationManager();
		_tutorialSystem = GetNodeOrNull<TutorialSystem>("TutorialSystem") ?? CreateRuntimeTutorialSystem();
		_tutorialBubbleView = GetNodeOrNull<TutorialBubbleView>("TutorialLayer/TutorialBubbleView") ?? CreateRuntimeTutorialBubbleView();
		_saveSystem = new SaveSystem();
		AddChild(_saveSystem);
		_saveData = _saveSystem.LoadOrCreate(SavePathOverride);
		_audioManager.ApplySettings(_saveData.Settings);
		_localizationManager.ApplySettings(_saveData.Settings);
		_tutorialSystem.Initialize(_saveData, _localizationManager, _tutorialBubbleView);
		_runSessionState.ApplyLevelProgress(_saveData);
		_runSessionState.ApplyHomeSlots(_saveData);
		ShowHomeGarden();
	}

	private void ShowHomeGarden()
	{
		ShowHomeGarden(null);
	}

	private void ShowHomeGarden(string? message)
	{
		Node scene = LoadScene(HomeGardenScenePath);

		if (scene is HomeGardenView homeGarden)
		{
			homeGarden.SetLocalizationManager(_localizationManager);
			homeGarden.StartGameRequested += () => { PlayClick(); OnStartGameRequested(homeGarden); };
			homeGarden.LevelSelectRequested += () => { PlayClick(); homeGarden.ShowMessage("Please use the main game entry."); };
			homeGarden.WarehouseRequested += () => { PlayClick(); ShowWarehouse(); };
			homeGarden.PlantingRequested += () => { PlayClick(); OnPlantingEntryRequested(); };
			homeGarden.SettingsRequested += () => { PlayClick(); ShowSettings(); };
			homeGarden.FlowerSlotPlantRequested += slotIndex => { PlayClick(); OnFlowerSlotPlantRequested(homeGarden, slotIndex); };
			homeGarden.FlowerSlotFlowerSelected += (slotIndex, flowerId) => { PlayClick(); OnFlowerSelectedForSlot(homeGarden, slotIndex, flowerId); };
			homeGarden.FlowerSlotShovelAllRequested += slotIndex => { PlayClick(); OnFlowerSlotShovelAllRequested(homeGarden, slotIndex); };
			homeGarden.FlowerSlotFlowerShovelRequested += (slotIndex, flowerId) => { PlayClick(); OnFlowerSlotFlowerShovelRequested(homeGarden, slotIndex, flowerId); };
			RefreshHomeGarden(homeGarden);
		}

		SetActiveScene(scene);

		if (scene is HomeGardenView activeHomeGarden && !string.IsNullOrWhiteSpace(message))
		{
			activeHomeGarden.ShowMessage(message);
		}

		TryShowTutorial(TutorialSystem.HomeIntroKey);
	}

	private void OnStartGameRequested(HomeGardenView homeGarden)
	{
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
			levelSelect.SetLocalizationManager(_localizationManager);
			string displayName = GetFlowerDisplayName(flowerId);
			levelSelect.SetLevelOptions(flowerId, _localizationManager.TrFormat("level_select.flower_title", displayName), BuildLevelOptions(flowerId, displayName));
			levelSelect.LevelSelected += levelNumber => { PlayClick(); OnLevelSelected(levelNumber); };
			levelSelect.BackRequested += () => { PlayClick(); ShowFlowerSelect(); };
			if (!string.IsNullOrWhiteSpace(message))
			{
				levelSelect.ShowMessage(message);
			}
		}

		SetActiveScene(scene);
	}

	private void ShowPlanting(string? message = null)
	{
		Node scene = LoadScene(PlantingScenePath);

		if (scene is PlantingPageView plantingPage)
		{
			plantingPage.SetSnapshot(BuildPlantingSnapshot());
			plantingPage.FlowerSelected += flowerId => { PlayClick(); OnPlantingFlowerSelected(plantingPage, flowerId); };
			plantingPage.BackRequested += () => { PlayClick(); ShowHomeGarden(); };
			if (!string.IsNullOrWhiteSpace(message))
			{
				plantingPage.ShowMessage(message);
			}
		}

		SetActiveScene(scene);
	}

	private void ShowFlowerSelect()
	{
		Node scene = LoadScene(FlowerSelectScenePath);

		if (scene is FlowerSelectView flowerSelect)
		{
			flowerSelect.SetLocalizationManager(_localizationManager);
			flowerSelect.SetFlowerOptions(_flowerSelectSystem.CreateBaseFlowerOptions(_runSessionState, GetFlowerDisplayName));
			flowerSelect.TargetFlowerSelected += flowerId => { PlayClick(); OnTargetFlowerSelected(flowerId); };
			flowerSelect.BackRequested += () => { PlayClick(); ShowHomeGarden(); };
		}

		SetActiveScene(scene);
	}

	private void ShowWarehouse()
	{
		Node scene = LoadScene(WarehouseScenePath);

		if (scene is WarehousePageView warehouse)
		{
			warehouse.SetLocalizationManager(_localizationManager);
			warehouse.SetInventoryRows(BuildWarehouseRows());
			warehouse.BackRequested += () => { PlayClick(); ShowHomeGarden(); };
		}

		SetActiveScene(scene);
		TryShowTutorial(TutorialSystem.WarehouseIntroKey);
	}

	private void ShowSettings()
	{
		ShowSettings(null);
	}

	private void ShowSettings(string? message)
	{
		Node scene = LoadScene(SettingsScenePath);

		if (scene is SettingsPageView settingsPage)
		{
			settingsPage.SetLocalizationManager(_localizationManager);
			settingsPage.SetSnapshot(BuildSettingsSnapshot());
			settingsPage.MusicVolumeChanged += volume => { OnMusicVolumeChanged(settingsPage, volume); PlayClick(); };
			settingsPage.SfxVolumeChanged += volume => { OnSfxVolumeChanged(settingsPage, volume); PlayClick(); };
			settingsPage.LanguageChanged += language => { PlayClick(); OnLanguageChanged(settingsPage, language); };
			settingsPage.ResetProgressRequested += PlayClick;
			settingsPage.ResetAllRequested += PlayClick;
			settingsPage.ResetProgressConfirmed += () => { PlayClick(); OnResetProgressConfirmed(settingsPage); };
			settingsPage.ResetAllConfirmed += () => { PlayClick(); OnResetAllConfirmed(settingsPage); };
			settingsPage.BackRequested += () => { PlayClick(); ShowHomeGarden(); };
			if (!string.IsNullOrWhiteSpace(message))
			{
				settingsPage.ShowMessage(message);
			}
		}

		SetActiveScene(scene);
		TryShowTutorial(TutorialSystem.SettingsIntroKey);
	}

	private void OnMusicVolumeChanged(SettingsPageView settingsPage, float volume)
	{
		_saveData.Settings.MusicVolume = Clamp01(volume);
		_saveData.Settings.Normalize();
		_saveSystem.RequestSave();
		_audioManager.SetMusicVolume(_saveData.Settings.MusicVolume);
		settingsPage.SetSnapshot(BuildSettingsSnapshot());
	}

	private void OnSfxVolumeChanged(SettingsPageView settingsPage, float volume)
	{
		_saveData.Settings.SfxVolume = Clamp01(volume);
		_saveData.Settings.Normalize();
		_saveSystem.RequestSave();
		_audioManager.SetSfxVolume(_saveData.Settings.SfxVolume);
		settingsPage.SetSnapshot(BuildSettingsSnapshot());
	}

	private void OnLanguageChanged(SettingsPageView settingsPage, string language)
	{
		_saveData.Settings.Language = LocalizationManager.NormalizeLanguage(language);
		_saveData.Settings.Normalize();
		_saveSystem.RequestSave();
		_localizationManager.ApplySettings(_saveData.Settings);
		settingsPage.SetSnapshot(BuildSettingsSnapshot());
		settingsPage.RefreshLocalizedText();
	}

	private void OnResetProgressConfirmed(SettingsPageView settingsPage)
	{
		_saveSystem.ResetProgressOnly();
		_saveData = _saveSystem.CurrentData;
		_runSessionState.ApplySaveData(_saveData);
		_audioManager.ApplySettings(_saveData.Settings);
		_localizationManager.ApplySettings(_saveData.Settings);
		_tutorialSystem.SynchronizeSaveData(_saveData);
		settingsPage.SetSnapshot(BuildSettingsSnapshot());
		settingsPage.RefreshLocalizedText();
		settingsPage.ShowMessage(_localizationManager.Tr("settings.progress_reset"));
	}

	private void OnResetAllConfirmed(SettingsPageView settingsPage)
	{
		_saveSystem.ResetAllSettings();
		_saveData = _saveSystem.CurrentData;
		_runSessionState.ApplySaveData(_saveData);
		_audioManager.ApplySettings(_saveData.Settings);
		_localizationManager.ApplySettings(_saveData.Settings);
		_tutorialSystem.SynchronizeSaveData(_saveData);
		settingsPage.SetSnapshot(BuildSettingsSnapshot());
		settingsPage.RefreshLocalizedText();
		settingsPage.ShowMessage(_localizationManager.Tr("settings.all_reset"));
	}

	private void ShowGameScene()
	{
		Node scene = LoadScene(GameScenePath);
		GameManager? gameManager = scene.GetNodeOrNull<GameManager>("Managers/GameManager");
		GD.Print(
			$"CAULDRON_DIAG MainFlowController.ShowGameScene " +
			$"SelectedFlowerId={_runSessionState.SelectedFlowerId ?? "<null>"} " +
			$"SelectedLevelNumber={(_runSessionState.SelectedLevelNumber?.ToString() ?? "<null>")}");

		if (gameManager != null)
		{
			gameManager.IsManagedByMainFlow = true;
			gameManager.SelectedFlowerId = _runSessionState.SelectedFlowerId ?? string.Empty;
			gameManager.SelectedLevelNumber = _runSessionState.SelectedLevelNumber ?? 1;
			gameManager.LevelCompleted += OnLevelCompleted;
			gameManager.ExitRequested += OnGameExitRequested;
		}
		else
		{
			GD.PushWarning("MainFlowController could not find GameScene Managers/GameManager; reward flow will not run.");
		}

		scene.GetNodeOrNull<UIManager>("Managers/UIManager")?.SetLocalizationManager(_localizationManager);
		scene.GetNodeOrNull<CauldronView>("WorldRoot/CauldronRoot/CauldronView")?.SetLocalizationManager(_localizationManager);
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
			ShowLevelSelect(_localizationManager.Tr("level_select.unavailable_tip"));
			return;
		}

		ShowGameScene();
	}

	private void OnLevelCompleted()
	{
		string? completedFlowerId = _runSessionState.SelectedFlowerId;
		bool levelCompleted = _runSessionState.CompleteSelectedLevel();
		if (!levelCompleted || string.IsNullOrWhiteSpace(completedFlowerId))
		{
			GD.Print("Level completion was ignored. The selected level may be invalid, or no target flower was selected.");
			ShowHomeGarden();
			return;
		}

		_saveData.SetLevelProgress(completedFlowerId, _runSessionState.GetCompletedLevelCount(completedFlowerId));
		InventoryItemData inventory = _saveData.GetOrCreateInventory(completedFlowerId);
		inventory.SeedCount += 1;
		inventory.PotionCount += 1;
		_saveData.Normalize();
		_saveSystem.RequestSave();

		ShowHomeGarden(_localizationManager.Tr("home.reward_stored"));
		TryShowTutorial(TutorialSystem.RewardToWarehouseKey);
	}

	private void OnPlantingEntryRequested()
	{
		if (_runSessionState.PendingPlanting || _runSessionState.IsWarehousePlantingMode)
		{
			CancelWarehousePlanting();
			return;
		}

		BeginWarehousePlantingMode();
	}

	private void OnPlantingFlowerSelected(PlantingPageView plantingPage, string flowerId)
	{
		PlantingAttemptResult inventoryCheck = _plantingSystem.ValidatePlant(_saveData, _runSessionState, flowerId, 0);
		if (inventoryCheck.Kind == PlantingAttemptResultKind.InsufficientInventory)
		{
			plantingPage.ShowMessage(inventoryCheck.Message);
			return;
		}

		if (!RunSessionState.IsOpenFlowerId(flowerId))
		{
			plantingPage.ShowMessage("该花尚未开放");
			return;
		}

		BeginWarehousePlanting(flowerId);
	}

	public void BeginWarehousePlanting(string flowerId)
	{
		_runSessionState.BeginWarehousePlanting(flowerId);
		ShowHomeGarden(_localizationManager.Tr("home.select_planting_slot"));
	}

	public void BeginWarehousePlantingMode()
	{
		_runSessionState.BeginWarehousePlantingMode();
		ShowHomeGarden();
		TryShowTutorial(TutorialSystem.PlantingIntroKey);
	}

	public void CancelWarehousePlanting()
	{
		_runSessionState.CancelWarehousePlanting();
		ShowHomeGarden("已取消种植");
	}

	private void OnGameExitRequested()
	{
		ShowLevelSelect();
	}

	private async void OnFlowerSlotPlantRequested(HomeGardenView homeGarden, int slotIndex)
	{
		if (_plantingInputLocked)
		{
			return;
		}

		if (!_runSessionState.CanPlantPendingRewardAt(slotIndex))
		{
			if (_runSessionState.IsWarehousePlantingMode && string.IsNullOrEmpty(_runSessionState.PendingPlantingFlowerId))
			{
				IReadOnlyList<PlantingFlowerOption> flowers = _plantingSystem.CreateAvailableFlowersForSlot(
					_saveData,
					_runSessionState,
					slotIndex,
					GetFlowerDisplayName);
				IReadOnlyList<PlantedFlowerOption> plantedFlowers = _plantingSystem.CreatePlantedFlowersForSlot(
					_runSessionState,
					slotIndex,
					GetFlowerDisplayName);
				if (flowers.Count == 0 && plantedFlowers.Count == 0)
				{
					RefreshHomeGarden(homeGarden);
					homeGarden.ShowMessage(_localizationManager.Tr("home.no_action"));
					return;
				}

				homeGarden.ShowPlantingFlowerPopup(slotIndex, flowers, plantedFlowers);
				if (plantedFlowers.Count > 0)
				{
					TryShowTutorial(TutorialSystem.ShovelIntroKey);
				}
				return;
			}

			homeGarden.ShowMessage(_runSessionState.PendingPlanting
				? "没有可追加的位置，请选择其他花"
				: "先完成药剂调试");
			return;
		}

		string pendingFlowerId = _runSessionState.PendingPlantingFlowerId!;
		PlantingAttemptResult validation = _plantingSystem.ValidatePlant(_saveData, _runSessionState, pendingFlowerId, slotIndex);
		if (!validation.IsReady)
		{
			RefreshHomeGarden(homeGarden);
			homeGarden.ShowMessage(validation.Message);
			return;
		}

		_plantingInputLocked = true;
		homeGarden.SetPlantingInputLocked(true);

		try
		{
			await homeGarden.PlayPlantingRewardAnimationAsync(slotIndex, pendingFlowerId);
			PlantingAttemptResult result = _plantingSystem.TryPlant(_saveData, _runSessionState, pendingFlowerId, slotIndex);
			if (result.IsSuccess)
			{
				_runSessionState.CancelWarehousePlanting();
				_saveSystem.RequestSave();
				RefreshHomeGarden(homeGarden);
				homeGarden.ShowMessage(_localizationManager.Tr("home.plant_success"));
			}
			else
			{
				homeGarden.ShowMessage(result.Message);
			}
		}
		finally
		{
			_plantingInputLocked = false;
			homeGarden.SetPlantingInputLocked(false);
		}
	}

	private async void OnFlowerSelectedForSlot(HomeGardenView homeGarden, int slotIndex, string flowerId)
	{
		if (_plantingInputLocked)
		{
			return;
		}

		if (!_runSessionState.IsWarehousePlantingMode || !string.IsNullOrEmpty(_runSessionState.PendingPlantingFlowerId))
		{
			return;
		}

		PlantingAttemptResult validation = _plantingSystem.ValidatePlant(_saveData, _runSessionState, flowerId, slotIndex);
		if (!validation.IsReady)
		{
			RefreshHomeGarden(homeGarden);
			homeGarden.ShowMessage(validation.Message);
			return;
		}

		_plantingInputLocked = true;
		homeGarden.SetPlantingInputLocked(true);

		try
		{
			await homeGarden.PlayPlantingRewardAnimationAsync(slotIndex, flowerId);
			PlantingAttemptResult result = _plantingSystem.TryPlant(_saveData, _runSessionState, flowerId, slotIndex);
			if (result.IsSuccess)
			{
				_runSessionState.CancelWarehousePlanting();
				_saveSystem.RequestSave();
				RefreshHomeGarden(homeGarden);
				homeGarden.ShowMessage(_localizationManager.Tr("home.plant_success"));
			}
			else
			{
				homeGarden.ShowMessage(result.Message);
				RefreshHomeGarden(homeGarden);
			}
		}
		finally
		{
			_plantingInputLocked = false;
			homeGarden.SetPlantingInputLocked(false);
		}
	}

	private void OnFlowerSlotShovelAllRequested(HomeGardenView homeGarden, int slotIndex)
	{
		if (_plantingInputLocked)
		{
			return;
		}

		if (!_runSessionState.IsWarehousePlantingMode || !string.IsNullOrEmpty(_runSessionState.PendingPlantingFlowerId))
		{
			return;
		}

		ShovelAttemptResult result = _plantingSystem.TryShovelAll(_saveData, _runSessionState, slotIndex);
		if (result.IsSuccess)
		{
			_runSessionState.CancelWarehousePlanting();
			_saveSystem.RequestSave();
			RefreshHomeGarden(homeGarden);
			homeGarden.ShowMessage(_localizationManager.Tr("home.shovel_success"));
			return;
		}

		RefreshHomeGarden(homeGarden);
		homeGarden.ShowMessage(result.Message);
	}

	private void OnFlowerSlotFlowerShovelRequested(HomeGardenView homeGarden, int slotIndex, string flowerId)
	{
		if (_plantingInputLocked)
		{
			return;
		}

		if (!_runSessionState.IsWarehousePlantingMode || !string.IsNullOrEmpty(_runSessionState.PendingPlantingFlowerId))
		{
			return;
		}

		ShovelAttemptResult result = _plantingSystem.TryShovelFlower(_saveData, _runSessionState, slotIndex, flowerId);
		if (result.IsSuccess)
		{
			_runSessionState.CancelWarehousePlanting();
			_saveSystem.RequestSave();
			RefreshHomeGarden(homeGarden);
			homeGarden.ShowMessage(_localizationManager.Tr("home.shovel_success"));
			return;
		}

		RefreshHomeGarden(homeGarden);
		homeGarden.ShowMessage(result.Message);
	}

	private LevelSelectOption[] BuildLevelOptions(string flowerId, string displayName)
	{
		LevelSelectOption[] options = new LevelSelectOption[RunSessionState.LevelsPerFlower];
		for (int i = 0; i < options.Length; i++)
		{
			int levelNumber = i + 1;
			options[i] = new LevelSelectOption(
				levelNumber,
				_localizationManager.TrFormat("level_select.level_title", displayName, levelNumber),
				_runSessionState.GetLevelState(flowerId, levelNumber));
		}

		return options;
	}

	private WarehouseInventoryRow[] BuildWarehouseRows()
	{
		WarehouseInventoryRow[] rows = new WarehouseInventoryRow[RunSessionState.OpenFlowerIds.Count];
		for (int i = 0; i < RunSessionState.OpenFlowerIds.Count; i++)
		{
			string flowerId = RunSessionState.OpenFlowerIds[i];
			string displayName = GetFlowerDisplayName(flowerId);
			int seedCount = 0;
			int potionCount = 0;
			if (_saveData.WarehouseInventoryByFlower.TryGetValue(flowerId, out InventoryItemData? inventory) && inventory != null)
			{
				seedCount = inventory.SeedCount;
				potionCount = inventory.PotionCount;
			}

			rows[i] = new WarehouseInventoryRow(flowerId, displayName, seedCount, potionCount);
		}

		return rows;
	}

	private PlantingPageSnapshot BuildPlantingSnapshot()
	{
		return _plantingSystem.CreateSnapshot(_saveData, _runSessionState, GetFlowerDisplayName);
	}

	private SettingsPageSnapshot BuildSettingsSnapshot()
	{
		_saveData.Settings.Normalize();
		return SettingsPageSnapshot.From(_saveData.Settings);
	}

	private static float Clamp01(float value)
	{
		return float.IsNaN(value) ? 0f : float.Clamp(value, 0f, 1f);
	}

	private AudioManager CreateRuntimeAudioManager()
	{
		AudioManager audioManager = new()
		{
			Name = "AudioManager"
		};
		AddChild(audioManager);
		return audioManager;
	}

	private LocalizationManager CreateRuntimeLocalizationManager()
	{
		LocalizationManager localizationManager = new()
		{
			Name = "LocalizationManager"
		};
		AddChild(localizationManager);
		return localizationManager;
	}

	private TutorialSystem CreateRuntimeTutorialSystem()
	{
		TutorialSystem tutorialSystem = new()
		{
			Name = "TutorialSystem"
		};
		AddChild(tutorialSystem);
		return tutorialSystem;
	}

	private TutorialBubbleView CreateRuntimeTutorialBubbleView()
	{
		CanvasLayer tutorialLayer = GetNodeOrNull<CanvasLayer>("TutorialLayer") ?? new CanvasLayer
		{
			Name = "TutorialLayer",
			Layer = 100
		};
		if (tutorialLayer.GetParent() == null)
		{
			AddChild(tutorialLayer);
		}

		TutorialBubbleView bubbleView = new()
		{
			Name = "TutorialBubbleView"
		};
		tutorialLayer.AddChild(bubbleView);
		return bubbleView;
	}

	private void TryShowTutorial(string tutorialKey)
	{
		if (_tutorialSystem.TryShowTutorial(tutorialKey))
		{
			_saveSystem.RequestSave();
		}
	}

	private string GetFlowerDisplayName(string flowerId)
	{
		return _localizationManager.Tr($"flower.{flowerId}.name");
	}

	private void PlayClick()
	{
		_audioManager.PlayClick();
	}

	private void RefreshHomeGarden(HomeGardenView homeGarden)
	{
		IReadOnlyList<HomeGardenPlantingSlotOption>? slots = _runSessionState.IsWarehousePlantingMode
			? _plantingSystem.CreateHomeGardenSlotOptions(_saveData, _runSessionState, GetFlowerDisplayName)
			: null;
		homeGarden.RefreshFlowers(
			_runSessionState,
			slots);
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
