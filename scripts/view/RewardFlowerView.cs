#nullable enable

using System;
using System.Collections.Generic;
using Godot;

namespace WaterSortGame.View;

public sealed partial class RewardFlowerView : Control
{
    private static readonly string[] FlowerTexturePaths =
    {
        "res://assets/reward/flowers/flower_pink_rose.png",
        "res://assets/reward/flowers/flower_red_hydrangea.png",
        "res://assets/reward/flowers/flower_blue_hydrangea.png",
        "res://assets/reward/flowers/flower_champagne_rose.png",
        "res://assets/reward/flowers/flower_blue_rose.png",
        "res://assets/reward/flowers/flower_mixed_border.png"
    };

    private static readonly Color[] FlowerColors =
    {
        new(0.96f, 0.31f, 0.39f),
        new(0.82f, 0.24f, 0.3f),
        new(0.26f, 0.48f, 0.86f),
        new(0.96f, 0.76f, 0.48f),
        new(0.3f, 0.52f, 0.98f),
        new(0.78f, 0.43f, 0.7f)
    };

    private static readonly string[] FlowerNames =
    {
        "Pink Rose",
        "Red Hydrangea",
        "Blue Hydrangea",
        "Champagne Rose",
        "Blue Rose",
        "Mixed Border"
    };

    private GridContainer _optionRoot = null!;
    private IReadOnlyList<int>? _pendingFlowerIds;
    private bool _isReady;

    public event Action<int>? RewardFlowerSelected;

    public override void _Ready()
    {
        _optionRoot = GetNode<GridContainer>("Panel/OptionRoot");
        _isReady = true;

        if (_pendingFlowerIds != null)
        {
            RefreshOptions(_pendingFlowerIds);
        }
    }

    public void SetRewardOptions(IReadOnlyList<int> flowerIds)
    {
        _pendingFlowerIds = flowerIds;

        if (_isReady)
        {
            RefreshOptions(flowerIds);
        }
    }

    private void RefreshOptions(IReadOnlyList<int> flowerIds)
    {
        foreach (Node child in _optionRoot.GetChildren())
        {
            child.QueueFree();
        }

        foreach (int flowerId in flowerIds)
        {
            _optionRoot.AddChild(CreateFlowerOptionButton(flowerId));
        }
    }

    private Button CreateFlowerOptionButton(int flowerId)
    {
        Color flowerColor = GetFlowerColor(flowerId);
        StyleBoxFlat normalStyle = CreateOptionStyle(new Color(1f, 0.98f, 0.91f, 0.9f), flowerColor);
        StyleBoxFlat hoverStyle = CreateOptionStyle(new Color(1f, 0.96f, 0.84f, 0.96f), flowerColor.Lightened(0.12f));

        Button button = new()
        {
            CustomMinimumSize = new Vector2(220f, 220f),
            ThemeTypeVariation = "RewardFlowerButton"
        };
        button.AddThemeStyleboxOverride("normal", normalStyle);
        button.AddThemeStyleboxOverride("hover", hoverStyle);
        button.AddThemeStyleboxOverride("pressed", hoverStyle);

        TextureRect flowerIcon = new()
        {
            Name = "FlowerIcon",
            Texture = GetFlowerTexture(flowerId),
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        flowerIcon.SetAnchorsPreset(LayoutPreset.FullRect);
        flowerIcon.OffsetLeft = 18f;
        flowerIcon.OffsetTop = 16f;
        flowerIcon.OffsetRight = -18f;
        flowerIcon.OffsetBottom = -48f;
        button.AddChild(flowerIcon);

        Label label = new()
        {
            Name = "FlowerName",
            Text = GetFlowerName(flowerId),
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        label.OffsetTop = 166f;
        label.OffsetBottom = -10f;
        label.AddThemeFontSizeOverride("font_size", 17);
        label.AddThemeColorOverride("font_color", new Color(0.2f, 0.15f, 0.1f));
        button.AddChild(label);

        button.Pressed += () => RewardFlowerSelected?.Invoke(flowerId);
        return button;
    }

    public static Color GetFlowerColor(int flowerId)
    {
        int index = Mathf.PosMod(flowerId, FlowerColors.Length);
        return FlowerColors[index];
    }

    public static string GetFlowerName(int flowerId)
    {
        int index = Mathf.PosMod(flowerId, FlowerNames.Length);
        return FlowerNames[index];
    }

    public static Texture2D? GetFlowerTexture(int flowerId)
    {
        int index = Mathf.PosMod(flowerId, FlowerTexturePaths.Length);
        return GD.Load<Texture2D>(FlowerTexturePaths[index]);
    }

    private static StyleBoxFlat CreateOptionStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 4,
            BorderWidthTop = 4,
            BorderWidthRight = 4,
            BorderWidthBottom = 4,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusBottomLeft = 8,
            ContentMarginLeft = 12,
            ContentMarginTop = 12,
            ContentMarginRight = 12,
            ContentMarginBottom = 12
        };
    }
}
