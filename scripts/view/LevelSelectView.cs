#nullable enable

using System;
using System.Collections.Generic;
using Godot;
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

    public string StateLabel
    {
        get
        {
            return State switch
            {
                FlowerLevelState.Completed => "已完成",
                FlowerLevelState.Playable => "可进入",
                _ => "未解锁"
            };
        }
    }

    public string UnavailableMessage
    {
        get
        {
            return State switch
            {
                FlowerLevelState.Completed => "该关卡已完成",
                FlowerLevelState.Locked => "该关卡尚未解锁",
                _ => string.Empty
            };
        }
    }
}

public sealed partial class LevelSelectView : Control
{
    public event Action<int>? LevelSelected;
    public event Action? BackRequested;

    private const string DefaultMessage = "选择当前可玩关卡";

    private Panel _panel = null!;
    private Label _titleLabel = null!;
    private Label _messageLabel = null!;
    private GridContainer _levelButtonRoot = null!;
    private Button _backButton = null!;
    private IReadOnlyList<LevelSelectOption>? _pendingOptions;
    private string _pendingTitle = "关卡选择";
    private string? _pendingMessage;
    private bool _isReady;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleLabel = GetNode<Label>("Panel/TitleLabel");
        _backButton = GetNode<Button>("Panel/BackButton");
        _backButton.Pressed += OnBackPressed;

        Button legacyLevelButton = GetNode<Button>("Panel/LevelOneButton");
        legacyLevelButton.Visible = false;
        legacyLevelButton.Disabled = true;

        PreparePanelLayout();
        _messageLabel = EnsureMessageLabel();
        _levelButtonRoot = EnsureLevelButtonRoot();
        _isReady = true;

        if (_pendingOptions != null)
        {
            RefreshLevels(_pendingTitle, _pendingOptions);
        }

        if (!string.IsNullOrWhiteSpace(_pendingMessage))
        {
            ShowMessage(_pendingMessage);
        }
    }

    public void SetLevelOptions(string title, IReadOnlyList<LevelSelectOption> options)
    {
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

        _messageLabel.Text = string.IsNullOrWhiteSpace(message) ? DefaultMessage : message;
        _messageLabel.Visible = true;
    }

    private void RefreshLevels(string title, IReadOnlyList<LevelSelectOption> options)
    {
        _titleLabel.Text = title;
        ShowMessage(DefaultMessage);

        foreach (Node child in _levelButtonRoot.GetChildren())
        {
            child.QueueFree();
        }

        foreach (LevelSelectOption option in options)
        {
            _levelButtonRoot.AddChild(CreateLevelButton(option));
        }
    }

    private Button CreateLevelButton(LevelSelectOption option)
    {
        Button button = new()
        {
            CustomMinimumSize = new Vector2(238f, 64f),
            Text = $"{option.Title}\n{option.StateLabel}",
            ThemeTypeVariation = "LevelSelectButton"
        };

        if (option.IsPlayable)
        {
            StyleBoxFlat normal = CreateLevelStyle(new Color(0.97f, 0.91f, 0.72f, 0.96f), new Color(0.48f, 0.31f, 0.14f, 0.72f));
            StyleBoxFlat hover = CreateLevelStyle(new Color(1f, 0.96f, 0.8f, 0.98f), new Color(0.56f, 0.36f, 0.16f, 0.84f));
            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("pressed", hover);
            button.AddThemeColorOverride("font_color", new Color(0.2f, 0.12f, 0.05f));
        }
        else
        {
            StyleBoxFlat locked = CreateLevelStyle(new Color(0.72f, 0.72f, 0.68f, 0.76f), new Color(0.34f, 0.34f, 0.32f, 0.42f));
            button.AddThemeStyleboxOverride("normal", locked);
            button.AddThemeStyleboxOverride("hover", locked);
            button.AddThemeStyleboxOverride("pressed", locked);
            button.AddThemeColorOverride("font_color", new Color(0.3f, 0.29f, 0.26f));
        }

        button.AddThemeFontSizeOverride("font_size", 20);
        button.Pressed += () =>
        {
            if (option.IsPlayable)
            {
                LevelSelected?.Invoke(option.LevelNumber);
                return;
            }

            ShowMessage(option.UnavailableMessage);
        };

        return button;
    }

    private void PreparePanelLayout()
    {
        _panel.OffsetLeft = 62f;
        _panel.OffsetTop = 120f;
        _panel.OffsetRight = 658f;
        _panel.OffsetBottom = 970f;

        _titleLabel.OffsetLeft = 42f;
        _titleLabel.OffsetTop = 40f;
        _titleLabel.OffsetRight = 554f;
        _titleLabel.OffsetBottom = 96f;

        _backButton.OffsetLeft = 200f;
        _backButton.OffsetTop = 716f;
        _backButton.OffsetRight = 396f;
        _backButton.OffsetBottom = 778f;
    }

    private Label EnsureMessageLabel()
    {
        Label? label = _panel.GetNodeOrNull<Label>("MessageLabel");
        if (label == null)
        {
            label = new Label
            {
                Name = "MessageLabel",
                Text = DefaultMessage,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _panel.AddChild(label);
        }

        label.SetAnchorsPreset(LayoutPreset.TopWide);
        label.OffsetLeft = 48f;
        label.OffsetTop = 100f;
        label.OffsetRight = -48f;
        label.OffsetBottom = 146f;
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", new Color(0.22f, 0.15f, 0.08f));
        return label;
    }

    private GridContainer EnsureLevelButtonRoot()
    {
        GridContainer? root = _panel.GetNodeOrNull<GridContainer>("LevelButtonRoot");
        if (root == null)
        {
            root = new GridContainer
            {
                Name = "LevelButtonRoot",
                Columns = 2
            };
            _panel.AddChild(root);
        }

        root.SetAnchorsPreset(LayoutPreset.TopWide);
        root.OffsetLeft = 42f;
        root.OffsetTop = 170f;
        root.OffsetRight = -42f;
        root.OffsetBottom = 680f;
        root.AddThemeConstantOverride("h_separation", 18);
        root.AddThemeConstantOverride("v_separation", 18);
        return root;
    }

    private static StyleBoxFlat CreateLevelStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusBottomLeft = 8
        };
    }

    private void OnBackPressed()
    {
        BackRequested?.Invoke();
    }
}
