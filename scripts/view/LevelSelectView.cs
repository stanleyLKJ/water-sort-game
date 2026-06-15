#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;

namespace WaterSortGame.View;

public readonly struct LevelSelectOption
{
    public LevelSelectOption(int levelNumber, string title, FlowerLevelState state)
    {
        LevelNumber = levelNumber;
        Title = title;
        State = state;
    }

    public int LevelNumber { get; }

    public string Title { get; }

    public FlowerLevelState State { get; }

    public bool IsPlayable => State == FlowerLevelState.Playable;
}

public sealed partial class LevelSelectView : Control
{
    public event Action<int>? LevelSelected;
    public event Action? BackRequested;

    private static readonly IReadOnlyDictionary<string, string> FlowerRootNames = new Dictionary<string, string>
    {
        ["pink_rose"] = "PinkRosePanelRoot",
        ["yellow_rose"] = "YellowRosePanelRoot",
        ["lavender"] = "LavenderPanelRoot"
    };

    private Label _titleLabel = null!;
    private Label _messageLabel = null!;
    private Label _temporaryTipLabel = null!;
    private Control _flowerPanelsRoot = null!;
    private readonly Dictionary<string, FlowerPanelSlot> _flowerPanels = new();
    private IReadOnlyList<LevelSelectOption>? _pendingOptions;
    private string? _pendingFlowerId;
    private string _pendingTitle = "关卡选择";
    private string? _pendingMessage;
    private readonly TemporaryTipHandle _temporaryTip = new();
    private bool _isReady;
    private LocalizationManager? _localizationManager;
    private FlowerPanelSlot? _activePanel;

    public void SetLocalizationManager(LocalizationManager localizationManager)
    {
        _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
        if (!_isReady)
        {
            return;
        }

        ApplyLocalizedText();
        if (_pendingOptions != null)
        {
            RefreshLevels(_pendingTitle, _pendingOptions);
        }
    }

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("CommonTextRoot/TitleLabel");
        _messageLabel = GetNode<Label>("CommonTextRoot/MessageLabel");
        _temporaryTipLabel = GetNode<Label>("CommonTextRoot/TemporaryTipLabel");
        _flowerPanelsRoot = GetNode<Control>("FlowerPanelsRoot");

        RegisterFlowerPanel("pink_rose", "PinkRosePanelRoot");
        RegisterFlowerPanel("yellow_rose", "YellowRosePanelRoot");
        RegisterFlowerPanel("lavender", "LavenderPanelRoot");

        _isReady = true;
        ApplyLocalizedText();

        if (_pendingOptions != null)
        {
            RefreshLevels(_pendingTitle, _pendingOptions);
        }
        else
        {
            ShowFlowerPanel(_pendingFlowerId);
        }

        if (!string.IsNullOrWhiteSpace(_pendingMessage))
        {
            ShowMessage(_pendingMessage);
        }
    }

    public void SetLevelOptions(string title, IReadOnlyList<LevelSelectOption> options)
    {
        SetLevelOptions(null, title, options);
    }

    public void SetLevelOptions(string? flowerId, string title, IReadOnlyList<LevelSelectOption> options)
    {
        _pendingFlowerId = flowerId;
        _pendingTitle = title;
        _pendingOptions = options;

        if (_isReady)
        {
            RefreshLevels(title, options);
        }
    }

    public void ShowMessage(string message)
    {
        if (!_isReady)
        {
            _pendingMessage = message;
            return;
        }

        _temporaryTip.Show(_temporaryTipLabel, string.IsNullOrWhiteSpace(message) ? Tr("level_select.hint") : message);
    }

    private void RegisterFlowerPanel(string flowerId, string nodeName)
    {
        Control panelRoot = _flowerPanelsRoot.GetNode<Control>(nodeName);
        Control levelSlotsRoot = panelRoot.GetNode<Control>("LevelSlots");
        Button backButton = panelRoot.GetNode<Button>("BackButton");
        backButton.Pressed += OnBackPressed;

        LevelVisualSlot[] levelSlots = new LevelVisualSlot[RunSessionState.LevelsPerFlower];
        for (int i = 0; i < levelSlots.Length; i++)
        {
            int levelNumber = i + 1;
            Control slotRoot = levelSlotsRoot.GetNode<Control>($"LevelSlot_{levelNumber:00}");
            Button hotAreaButton = slotRoot.GetNode<Button>("HotAreaButton");
            hotAreaButton.Pressed += () => OnLevelPressed(levelNumber);
            levelSlots[i] = new LevelVisualSlot(
                slotRoot,
                slotRoot.GetNode<TextureRect>("AvailableTexture"),
                slotRoot.GetNode<TextureRect>("CompletedTexture"),
                slotRoot.GetNode<TextureRect>("LockedTexture"),
                hotAreaButton,
                slotRoot.GetNode<Label>("TextRoot/LevelNameNumberLabel"),
                slotRoot.GetNode<Label>("TextRoot/StatusLabel"));
        }

        _flowerPanels[flowerId] = new FlowerPanelSlot(panelRoot, levelSlots, backButton);
    }

    private void RefreshLevels(string title, IReadOnlyList<LevelSelectOption> options)
    {
        _titleLabel.Text = title;
        ShowFixedMessage(Tr("level_select.hint"));
        FlowerPanelSlot? maybePanel = ShowFlowerPanel(_pendingFlowerId);
        if (!maybePanel.HasValue)
        {
            return;
        }

        FlowerPanelSlot panel = maybePanel.Value;
        string flowerName = ResolveFlowerName(_pendingFlowerId, title);
        for (int i = 0; i < panel.LevelSlots.Length; i++)
        {
            LevelVisualSlot slot = panel.LevelSlots[i];
            if (i >= options.Count)
            {
                HideUnusedSlot(slot);
                continue;
            }

            ApplyLevelOption(slot, options[i], flowerName);
        }
    }

    private FlowerPanelSlot? ShowFlowerPanel(string? flowerId)
    {
        string normalizedFlowerId = !string.IsNullOrWhiteSpace(flowerId) && FlowerRootNames.ContainsKey(flowerId)
            ? flowerId
            : "pink_rose";

        FlowerPanelSlot? selected = null;
        foreach ((string id, FlowerPanelSlot panel) in _flowerPanels)
        {
            bool isSelected = id == normalizedFlowerId;
            panel.PanelRoot.Visible = isSelected;
            if (isSelected)
            {
                selected = panel;
            }
        }

        _activePanel = selected;
        return selected;
    }

    private void ApplyLevelOption(LevelVisualSlot slot, LevelSelectOption option, string flowerName)
    {
        slot.SlotRoot.Visible = true;
        slot.HotAreaButton.Visible = true;
        slot.HotAreaButton.Disabled = false;
        slot.HotAreaButton.MouseFilter = Control.MouseFilterEnum.Stop;
        string stateText = GetStateLabel(option.State);
        string levelText = Tr("level_select.level_number").Replace("{0}", option.LevelNumber.ToString());

        slot.HotAreaButton.Text = string.Empty;
        slot.HotAreaButton.TooltipText = $"{flowerName} {levelText} {stateText}";
        slot.AvailableTexture.Visible = option.State == FlowerLevelState.Playable;
        slot.CompletedTexture.Visible = option.State == FlowerLevelState.Completed;
        slot.LockedTexture.Visible = option.State == FlowerLevelState.Locked;
        slot.LevelNameNumberLabel.Text = $"{flowerName} {levelText}";
        slot.StatusLabel.Text = stateText;
        slot.LevelNameNumberLabel.Visible = true;
        slot.StatusLabel.Visible = true;
    }

    private static void HideUnusedSlot(LevelVisualSlot slot)
    {
        slot.SlotRoot.Visible = false;
        slot.HotAreaButton.Disabled = true;
        slot.HotAreaButton.MouseFilter = Control.MouseFilterEnum.Ignore;
        slot.HotAreaButton.Text = string.Empty;
        slot.HotAreaButton.TooltipText = string.Empty;
        slot.AvailableTexture.Visible = false;
        slot.CompletedTexture.Visible = false;
        slot.LockedTexture.Visible = false;
        slot.LevelNameNumberLabel.Text = string.Empty;
        slot.StatusLabel.Text = string.Empty;
    }

    private void OnLevelPressed(int levelNumber)
    {
        if (_pendingOptions == null || levelNumber < 1 || levelNumber > _pendingOptions.Count)
        {
            return;
        }

        LevelSelectOption option = _pendingOptions[levelNumber - 1];
        if (option.IsPlayable)
        {
            LevelSelected?.Invoke(option.LevelNumber);
            return;
        }

        AudioManager.PlayGlobalClick();
        ShowMessage(option.State == FlowerLevelState.Completed
            ? Tr("level_select.completed_tip")
            : Tr("level_select.locked_tip"));
    }

    private void ShowFixedMessage(string message)
    {
        _messageLabel.Text = string.IsNullOrWhiteSpace(message) ? Tr("level_select.hint") : message;
        _messageLabel.Visible = true;
    }

    private void OnBackPressed()
    {
        BackRequested?.Invoke();
    }

    private void ApplyLocalizedText()
    {
        _titleLabel.Text = _pendingOptions == null ? Tr("level_select.title") : _pendingTitle;
        ShowFixedMessage(Tr("level_select.hint"));
        foreach (FlowerPanelSlot panel in _flowerPanels.Values)
        {
            panel.BackButton.TooltipText = Tr("common.back");
        }
    }

    private string GetStateLabel(FlowerLevelState state)
    {
        return state switch
        {
            FlowerLevelState.Completed => Tr("level_select.completed"),
            FlowerLevelState.Playable => Tr("level_select.playable"),
            _ => Tr("level_select.locked")
        };
    }

    private string ResolveFlowerName(string? flowerId, string title)
    {
        if (!string.IsNullOrWhiteSpace(flowerId))
        {
            string key = $"flower.{flowerId}.name";
            string translated = Tr(key);
            if (translated != key)
            {
                return translated;
            }
        }

        const string zhSuffix = " 关卡";
        const string enSuffix = " Levels";
        if (title.EndsWith(zhSuffix, StringComparison.Ordinal))
        {
            return title[..^zhSuffix.Length];
        }

        if (title.EndsWith(enSuffix, StringComparison.Ordinal))
        {
            return title[..^enSuffix.Length];
        }

        return title;
    }

    private string Tr(string key)
    {
        return _localizationManager?.Tr(key) ?? LocalizationManager.GetText(key);
    }

    private readonly record struct FlowerPanelSlot(Control PanelRoot, LevelVisualSlot[] LevelSlots, Button BackButton);

    private readonly record struct LevelVisualSlot(
        Control SlotRoot,
        TextureRect AvailableTexture,
        TextureRect CompletedTexture,
        TextureRect LockedTexture,
        Button HotAreaButton,
        Label LevelNameNumberLabel,
        Label StatusLabel);
}
