#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using WaterSortGame.Core;

namespace WaterSortGame.View;

public sealed partial class FlowerSelectView : Control
{
    private const string SelectTexturePathFormat = "res://assets/flowers/{0}/select/{0}_select.png";

    private static readonly Color[] FlowerColors =
    {
        new(0.96f, 0.31f, 0.39f),
        new(0.94f, 0.72f, 0.24f),
        new(0.26f, 0.48f, 0.86f),
        new(0.96f, 0.76f, 0.48f),
        new(0.3f, 0.52f, 0.98f),
        new(0.78f, 0.43f, 0.7f)
    };

    private GridContainer _optionRoot = null!;
    private Label _hintLabel = null!;
    private IReadOnlyList<FlowerOption>? _pendingOptions;
    private string _defaultHintText = string.Empty;
    private bool _isReady;

    public event Action<string>? TargetFlowerSelected;
    public event Action? BackRequested;

    public override void _Ready()
    {
        _optionRoot = GetNode<GridContainer>("Panel/OptionRoot");
        _hintLabel = GetNode<Label>("Panel/HintLabel");
        _defaultHintText = _hintLabel.Text;
        GetNode<Button>("Panel/BackButton").Pressed += OnBackPressed;
        _isReady = true;

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

    private void RefreshOptions(IReadOnlyList<FlowerOption> options)
    {
        ShowMessage(_defaultHintText);

        foreach (Node child in _optionRoot.GetChildren())
        {
            child.QueueFree();
        }

        foreach (FlowerOption option in options)
        {
            _optionRoot.AddChild(CreateFlowerOptionButton(option));
        }
    }

    private Button CreateFlowerOptionButton(FlowerOption option)
    {
        Color flowerColor = option.IsSelectable ? GetFlowerColor(option.Index) : new Color(0.56f, 0.56f, 0.52f);
        Color background = option.IsSelectable ? new Color(1f, 0.98f, 0.91f, 0.92f) : new Color(0.74f, 0.74f, 0.68f, 0.82f);
        Color hoverBackground = option.IsSelectable ? new Color(1f, 0.96f, 0.84f, 0.98f) : new Color(0.78f, 0.78f, 0.72f, 0.86f);
        StyleBoxFlat normalStyle = CreateOptionStyle(background, flowerColor);
        StyleBoxFlat hoverStyle = CreateOptionStyle(hoverBackground, flowerColor.Lightened(0.08f));

        Button button = new()
        {
            CustomMinimumSize = new Vector2(156f, 184f),
            ThemeTypeVariation = "FlowerSelectButton"
        };
        button.AddThemeStyleboxOverride("normal", normalStyle);
        button.AddThemeStyleboxOverride("hover", hoverStyle);
        button.AddThemeStyleboxOverride("pressed", hoverStyle);

        button.AddChild(CreateFlowerVisual(option, flowerColor));

        Label label = new()
        {
            Name = "FlowerName",
            Text = option.DisplayName,
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        label.OffsetTop = 138f;
        label.OffsetBottom = -8f;
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", option.IsSelectable ? new Color(0.2f, 0.15f, 0.1f) : new Color(0.32f, 0.31f, 0.28f));
        button.AddChild(label);

        string statusText = option.IsOpen ? option.IsFull ? "已种满" : string.Empty : "待开放";
        if (!string.IsNullOrEmpty(statusText))
        {
            Label statusLabel = new()
            {
                Name = "StatusLabel",
                Text = statusText,
                MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusLabel.SetAnchorsPreset(LayoutPreset.TopWide);
            statusLabel.OffsetLeft = 18f;
            statusLabel.OffsetTop = 14f;
            statusLabel.OffsetRight = -18f;
            statusLabel.OffsetBottom = 44f;
            statusLabel.AddThemeFontSizeOverride("font_size", 16);
            statusLabel.AddThemeColorOverride("font_color", new Color(0.32f, 0.3f, 0.27f));
            button.AddChild(statusLabel);
        }

        button.Pressed += () =>
        {
            if (option.IsSelectable)
            {
                TargetFlowerSelected?.Invoke(option.FlowerId);
                return;
            }

            ShowMessage(option.UnavailableMessage);
        };

        return button;
    }

    public void ShowMessage(string message)
    {
        if (!_isReady)
        {
            return;
        }

        _hintLabel.Text = message;
    }

    private static Node CreateFlowerVisual(FlowerOption option, Color flowerColor)
    {
        string texturePath = BuildSelectTexturePath(option.FlowerId);
        if (ResourceLoader.Exists(texturePath))
        {
            TextureRect flowerIcon = new()
            {
                Name = "FlowerIcon",
                Texture = GD.Load<Texture2D>(texturePath),
                MouseFilter = MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            ApplyVisualBounds(flowerIcon);
            return flowerIcon;
        }

        GD.PushWarning($"FlowerSelect select texture not found: {texturePath}. Showing safe placeholder for {option.FlowerId}.");
        return CreateMissingTexturePlaceholder(option.FlowerId, flowerColor);
    }

    private static Control CreateMissingTexturePlaceholder(string flowerId, Color flowerColor)
    {
        Control root = new()
        {
            Name = "MissingSelectPlaceholder",
            MouseFilter = MouseFilterEnum.Ignore
        };
        ApplyVisualBounds(root);

        ColorRect swatch = new()
        {
            Name = "PlaceholderSwatch",
            Color = flowerColor.Lightened(0.45f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        swatch.SetAnchorsPreset(LayoutPreset.FullRect);
        swatch.OffsetLeft = 8f;
        swatch.OffsetTop = 8f;
        swatch.OffsetRight = -8f;
        swatch.OffsetBottom = -8f;
        root.AddChild(swatch);

        Label marker = new()
        {
            Name = "PlaceholderLabel",
            Text = flowerId,
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        marker.SetAnchorsPreset(LayoutPreset.FullRect);
        marker.OffsetLeft = 12f;
        marker.OffsetTop = 12f;
        marker.OffsetRight = -12f;
        marker.OffsetBottom = -12f;
        marker.AddThemeFontSizeOverride("font_size", 15);
        marker.AddThemeColorOverride("font_color", new Color(0.28f, 0.22f, 0.16f));
        root.AddChild(marker);

        return root;
    }

    private static void ApplyVisualBounds(Control control)
    {
        control.SetAnchorsPreset(LayoutPreset.FullRect);
        control.OffsetLeft = 12f;
        control.OffsetTop = 12f;
        control.OffsetRight = -12f;
        control.OffsetBottom = -46f;
    }

    public static Color GetFlowerColor(int flowerId)
    {
        int index = Mathf.PosMod(flowerId, FlowerColors.Length);
        return FlowerColors[index];
    }

    public static string BuildSelectTexturePath(string flowerId)
    {
        return string.Format(SelectTexturePathFormat, flowerId);
    }

    private void OnBackPressed()
    {
        BackRequested?.Invoke();
    }

    private static StyleBoxFlat CreateOptionStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusBottomLeft = 8,
            ContentMarginLeft = 8,
            ContentMarginTop = 8,
            ContentMarginRight = 8,
            ContentMarginBottom = 8
        };
    }
}
