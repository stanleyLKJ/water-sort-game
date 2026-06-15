#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using WaterSortGame.Core;

namespace WaterSortGame.View;

public sealed partial class WarehousePageView : Control
{
    public event Action? BackRequested;

    private const string SeedItemKind = "seed";
    private const string PotionItemKind = "potion";
    private const string ItemTexturePathFormat = "res://assets/flowers/{0}/items/{0}_{1}.png";
    private const float ItemIconSize = 56f;

    private VBoxContainer _itemList = null!;
    private Button _backButton = null!;
    private IReadOnlyList<WarehouseInventoryRow> _pendingRows = Array.Empty<WarehouseInventoryRow>();
    private bool _isReady;
    private LocalizationManager? _localizationManager;

    public void SetLocalizationManager(LocalizationManager localizationManager)
    {
        _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
        if (_isReady)
        {
            RefreshLocalizedText();
            RenderRows(_pendingRows);
        }
    }

    public override void _Ready()
    {
        BuildLayout();
        _isReady = true;
        _backButton.Pressed += () => BackRequested?.Invoke();
        RenderRows(_pendingRows);
        RefreshLocalizedText();
    }

    public void SetInventoryRows(IReadOnlyList<WarehouseInventoryRow> rows)
    {
        _pendingRows = rows;
        if (!_isReady)
        {
            return;
        }

        RenderRows(rows);
    }

    private void RenderRows(IReadOnlyList<WarehouseInventoryRow> rows)
    {
        foreach (Node child in _itemList.GetChildren())
        {
            _itemList.RemoveChild(child);
            child.QueueFree();
        }

        foreach (WarehouseInventoryRow row in rows)
        {
            _itemList.AddChild(CreateInventoryCard(row));
        }
    }

    private Control CreateInventoryCard(WarehouseInventoryRow row)
    {
        PanelContainer card = new()
        {
            Name = $"Row_{row.FlowerId}",
            CustomMinimumSize = new Vector2(0f, 112f)
        };
        card.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(1f, 0.96f, 0.84f, 0.92f), new Color(0.42f, 0.29f, 0.15f, 0.32f)));

        HBoxContainer rowRoot = new()
        {
            Name = "RowRoot",
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rowRoot.AddThemeConstantOverride("separation", 18);
        card.AddChild(rowRoot);

        Label nameLabel = new()
        {
            Name = "FlowerName",
            Text = string.IsNullOrWhiteSpace(row.DisplayName) ? row.FlowerId : $"{row.DisplayName}\n{row.FlowerId}",
            CustomMinimumSize = new Vector2(170f, 0f),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 24);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.13f, 0.08f));
        rowRoot.AddChild(nameLabel);

        rowRoot.AddChild(CreateCountGroup(row.FlowerId, SeedItemKind, Tr("warehouse.seed"), row.SeedCount));
        rowRoot.AddChild(CreateCountGroup(row.FlowerId, PotionItemKind, Tr("warehouse.potion"), row.PotionCount));

        return card;
    }

    private static HBoxContainer CreateCountGroup(string flowerId, string itemKind, string label, int count)
    {
        HBoxContainer group = new()
        {
            Name = itemKind == SeedItemKind ? "SeedGroup" : "PotionGroup",
            Alignment = BoxContainer.AlignmentMode.Center,
            CustomMinimumSize = new Vector2(180f, 0f)
        };
        group.AddThemeConstantOverride("separation", 10);

        TextureRect icon = new()
        {
            Name = itemKind == SeedItemKind ? "SeedIcon" : "PotionIcon",
            CustomMinimumSize = new Vector2(ItemIconSize, ItemIconSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        TrySetItemTexture(icon, flowerId, itemKind);
        group.AddChild(icon);

        Label countLabel = new()
        {
            Name = itemKind == SeedItemKind ? "SeedCountLabel" : "PotionCountLabel",
            Text = $"{label} x{Math.Max(0, count)}",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(100f, 0f)
        };
        countLabel.AddThemeFontSizeOverride("font_size", 24);
        countLabel.AddThemeColorOverride("font_color", new Color(0.22f, 0.14f, 0.08f));
        group.AddChild(countLabel);

        return group;
    }

    private static bool TrySetItemTexture(TextureRect textureRect, string flowerId, string itemKind)
    {
        string texturePath = BuildItemTexturePath(flowerId, itemKind);
        Texture2D? texture = LoadItemTexture(texturePath);
        if (texture == null)
        {
            GD.PushWarning($"Warehouse item texture missing for {flowerId} {itemKind}: {texturePath}");
            textureRect.Texture = null;
            textureRect.Visible = false;
            return false;
        }

        textureRect.Texture = texture;
        textureRect.Visible = true;
        return true;
    }

    private static Texture2D? LoadItemTexture(string texturePath)
    {
        if (ResourceLoader.Exists(texturePath))
        {
            return GD.Load<Texture2D>(texturePath);
        }

        if (!FileAccess.FileExists(texturePath))
        {
            return null;
        }

        Image image = Image.LoadFromFile(ProjectSettings.GlobalizePath(texturePath));
        if (image == null || image.IsEmpty())
        {
            return null;
        }

        ImageTexture texture = ImageTexture.CreateFromImage(image);
        texture.ResourcePath = texturePath;
        return texture;
    }

    public static string BuildItemTexturePath(string flowerId, string itemKind)
    {
        return string.Format(ItemTexturePathFormat, flowerId, itemKind);
    }

    private void BuildLayout()
    {
        ColorRect background = new()
        {
            Name = "Background",
            Color = new Color(0.93f, 0.9f, 0.76f)
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        background.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(background);

        PanelContainer panel = new()
        {
            Name = "Panel"
        };
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        panel.OffsetLeft = 44f;
        panel.OffsetTop = 64f;
        panel.OffsetRight = -44f;
        panel.OffsetBottom = -64f;
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(1f, 0.98f, 0.9f, 0.96f), new Color(0.36f, 0.24f, 0.13f, 0.46f)));
        AddChild(panel);

        VBoxContainer content = new()
        {
            Name = "Content",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 24);
        panel.AddChild(content);

        HBoxContainer header = new()
        {
            Name = "Header",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddChild(header);

        Label title = new()
        {
            Name = "TitleLabel",
            Text = Tr("warehouse.title"),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 42);
        title.AddThemeColorOverride("font_color", new Color(0.18f, 0.11f, 0.07f));
        header.AddChild(title);

        _backButton = new Button
        {
            Name = "BackButton",
            Text = Tr("common.back"),
            CustomMinimumSize = new Vector2(128f, 58f)
        };
        ApplyButtonTheme(_backButton);
        header.AddChild(_backButton);

        ScrollContainer scroll = new()
        {
            Name = "Scroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddChild(scroll);

        _itemList = new VBoxContainer
        {
            Name = "ItemList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _itemList.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(_itemList);
    }

    private void RefreshLocalizedText()
    {
        GetNode<Label>("Panel/Content/Header/TitleLabel").Text = Tr("warehouse.title");
        _backButton.Text = Tr("common.back");
    }

    private string Tr(string key)
    {
        return _localizationManager?.Tr(key) ?? LocalizationManager.GetText(key);
    }

    private static void ApplyButtonTheme(Button button)
    {
        button.AddThemeFontSizeOverride("font_size", 24);
        button.AddThemeColorOverride("font_color", new Color(0.19f, 0.12f, 0.07f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.19f, 0.12f, 0.07f));
        button.AddThemeColorOverride("font_pressed_color", new Color(0.19f, 0.12f, 0.07f));
        button.AddThemeStyleboxOverride("normal", CreatePanelStyle(new Color(0.94f, 0.8f, 0.47f, 0.98f), new Color(0.36f, 0.22f, 0.1f, 0.7f)));
        button.AddThemeStyleboxOverride("hover", CreatePanelStyle(new Color(1f, 0.87f, 0.55f, 1f), new Color(0.42f, 0.25f, 0.11f, 0.8f)));
        button.AddThemeStyleboxOverride("pressed", CreatePanelStyle(new Color(0.84f, 0.67f, 0.34f, 1f), new Color(0.32f, 0.18f, 0.08f, 0.85f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static StyleBoxFlat CreatePanelStyle(Color backgroundColor, Color borderColor)
    {
        return new StyleBoxFlat
        {
            BgColor = backgroundColor,
            BorderColor = borderColor,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 20f,
            ContentMarginTop = 20f,
            ContentMarginRight = 20f,
            ContentMarginBottom = 20f
        };
    }
}

public sealed record WarehouseInventoryRow(string FlowerId, string DisplayName, int SeedCount, int PotionCount);
