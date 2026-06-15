#nullable enable

using System;
using Godot;
using WaterSortGame.Model;
using WaterSortGame.View;

namespace WaterSortGame.Core;

public sealed partial class TutorialSystem : Node
{
    public const string HomeIntroKey = "home_intro";
    public const string RewardToWarehouseKey = "reward_to_warehouse";
    public const string WarehouseIntroKey = "warehouse_intro";
    public const string PlantingIntroKey = "planting_intro";
    public const string ShovelIntroKey = "shovel_intro";
    public const string SettingsIntroKey = "settings_intro";

    private SaveData? _saveData;
    private LocalizationManager? _localizationManager;
    private TutorialBubbleView? _bubbleView;

    public void Initialize(
        SaveData saveData,
        LocalizationManager localizationManager,
        TutorialBubbleView bubbleView)
    {
        _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
        _bubbleView = bubbleView ?? throw new ArgumentNullException(nameof(bubbleView));
        SynchronizeSaveData(saveData);
    }

    public void SynchronizeSaveData(SaveData saveData)
    {
        _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
        _saveData.Normalize();
        _bubbleView?.DismissTutorial();
    }

    public bool HasShown(string tutorialKey)
    {
        return _saveData?.TutorialBubblesShown.TryGetValue(tutorialKey, out bool shown) == true && shown;
    }

    public bool TryShowTutorial(string tutorialKey)
    {
        if (string.IsNullOrWhiteSpace(tutorialKey))
        {
            GD.PushWarning("TutorialSystem received an empty tutorial key.");
            return false;
        }

        if (_saveData == null || _localizationManager == null || _bubbleView == null)
        {
            GD.PushWarning("TutorialSystem is not initialized.");
            return false;
        }

        if (HasShown(tutorialKey))
        {
            return false;
        }

        string text = _localizationManager.Tr($"tutorial.{tutorialKey}");
        _bubbleView.ShowTutorial(tutorialKey, text);
        _saveData.TutorialBubblesShown[tutorialKey] = true;
        return true;
    }
}
