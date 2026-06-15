#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using WaterSortGame.Core;

namespace WaterSortGame.View;

public sealed partial class FlowerSelectView : Control
{
    private const int SlotCount = FlowerSelectSystem.BaseFlowerCount;

    private readonly FlowerSlotNode[] _slots = new FlowerSlotNode[SlotCount];
    private readonly TemporaryTipHandle _temporaryTip = new();

    private Label _titleLabel = null!;
    private Label _hintLabel = null!;
    private Label _temporaryTipLabel = null!;
    private Button _backButton = null!;
    private IReadOnlyList<FlowerOption>? _pendingOptions;
    private string _defaultHintText = string.Empty;
    private bool _isReady;
    private LocalizationManager? _localizationManager;

    public event Action<string>? TargetFlowerSelected;
    public event Action? BackRequested;

    public void SetLocalizationManager(LocalizationManager localizationManager)
    {
        _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
        if (_isReady)
        {
            ApplyLocalizedText();
            if (_pendingOptions != null)
            {
                RefreshOptions(_pendingOptions);
            }
        }
    }

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("Panel/TitleLabel");
        _hintLabel = GetNode<Label>("Panel/HintLabel");
        _temporaryTipLabel = GetNode<Label>("Panel/TemporaryTipLabel");
        _backButton = GetNode<Button>("Panel/BackButton");
        _backButton.Pressed += OnBackPressed;

        Control slotRoot = GetNode<Control>("Panel/FlowerSlots");
        for (int i = 0; i < SlotCount; i++)
        {
            int slotIndex = i;
            Control root = slotRoot.GetNode<Control>($"FlowerSlot_{slotIndex + 1:00}");
            Button hotAreaButton = root.GetNode<Button>("HotAreaButton");
            hotAreaButton.Pressed += () => OnFlowerSlotPressed(slotIndex);

            _slots[slotIndex] = new FlowerSlotNode(
                root,
                root.GetNode<TextureRect>("OpenCardTexture"),
                root.GetNode<TextureRect>("DisabledCardTexture"),
                root.GetNode<Label>("FlowerName"),
                root.GetNode<Label>("StatusLabel"),
                hotAreaButton);
        }

        _isReady = true;
        ApplyLocalizedText();

        if (_pendingOptions != null)
        {
            RefreshOptions(_pendingOptions);
        }
    }

    public void SetFlowerOptions(IReadOnlyList<FlowerOption> options)
    {
        _pendingOptions = options;

        if (_isReady)
        {
            RefreshOptions(options);
        }
    }

    public void ShowMessage(string message)
    {
        if (!_isReady)
        {
            return;
        }

        _temporaryTip.Show(_temporaryTipLabel, message);
    }

    private void RefreshOptions(IReadOnlyList<FlowerOption> options)
    {
        ShowFixedHint(_defaultHintText);

        for (int i = 0; i < SlotCount; i++)
        {
            if (i >= options.Count)
            {
                _slots[i].Root.Visible = false;
                continue;
            }

            ApplyOptionToSlot(_slots[i], options[i]);
        }
    }

    private void ApplyOptionToSlot(FlowerSlotNode slot, FlowerOption option)
    {
        slot.Root.Visible = true;
        slot.OpenCardTexture.Visible = option.IsSelectable;
        slot.DisabledCardTexture.Visible = !option.IsSelectable;
        slot.FlowerNameLabel.Text = option.DisplayName;
        slot.HotAreaButton.TooltipText = option.DisplayName;

        string statusText = option.IsOpen
            ? option.IsFull ? Tr("flower_select.full") : string.Empty
            : Tr("flower_select.coming_soon");

        slot.StatusLabel.Text = statusText;
        slot.StatusLabel.Visible = !string.IsNullOrEmpty(statusText);
    }

    private void OnFlowerSlotPressed(int slotIndex)
    {
        if (_pendingOptions == null || slotIndex < 0 || slotIndex >= _pendingOptions.Count)
        {
            return;
        }

        FlowerOption option = _pendingOptions[slotIndex];
        if (option.IsSelectable)
        {
            TargetFlowerSelected?.Invoke(option.FlowerId);
            return;
        }

        AudioManager.PlayGlobalClick();
        ShowMessage(option.IsFull
            ? Tr("flower_select.full_tip")
            : Tr("flower_select.coming_soon_tip"));
    }

    private void ShowFixedHint(string message)
    {
        _hintLabel.Text = message;
        _hintLabel.Visible = !string.IsNullOrWhiteSpace(message);
    }

    private void OnBackPressed()
    {
        BackRequested?.Invoke();
    }

    private void ApplyLocalizedText()
    {
        _titleLabel.Text = Tr("flower_select.title");
        _defaultHintText = Tr("flower_select.hint");
        ShowFixedHint(_defaultHintText);
        _backButton.Text = Tr("common.back");
        _backButton.TooltipText = Tr("common.back");
    }

    private string Tr(string key)
    {
        return _localizationManager?.Tr(key) ?? LocalizationManager.GetText(key);
    }

    private readonly struct FlowerSlotNode
    {
        public FlowerSlotNode(
            Control root,
            TextureRect openCardTexture,
            TextureRect disabledCardTexture,
            Label flowerNameLabel,
            Label statusLabel,
            Button hotAreaButton)
        {
            Root = root;
            OpenCardTexture = openCardTexture;
            DisabledCardTexture = disabledCardTexture;
            FlowerNameLabel = flowerNameLabel;
            StatusLabel = statusLabel;
            HotAreaButton = hotAreaButton;
        }

        public Control Root { get; }

        public TextureRect OpenCardTexture { get; }

        public TextureRect DisabledCardTexture { get; }

        public Label FlowerNameLabel { get; }

        public Label StatusLabel { get; }

        public Button HotAreaButton { get; }
    }
}
