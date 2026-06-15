#nullable enable

using System;
using Godot;
using WaterSortGame.Core;

namespace WaterSortGame.View;

public sealed partial class PlantingPageView : Control
{
    public event Action? BackRequested;
    public event Action<string>? FlowerSelected;

    private VBoxContainer _flowerList = null!;
    private Button _backButton = null!;
    private Label _messageLabel = null!;
    private PlantingPageSnapshot _pendingSnapshot = new(Array.Empty<PlantingFlowerOption>(), Array.Empty<PlantingSlotOption>());
    private readonly TemporaryTipHandle _temporaryTip = new();
    private bool _isReady;

    public override void _Ready()
    {
        BuildLayout();
        _isReady = true;
        _backButton.Pressed += () => BackRequested?.Invoke();
        RenderSnapshot(_pendingSnapshot);
    }

    public void SetSnapshot(PlantingPageSnapshot snapshot)
    {
        _pendingSnapshot = snapshot;
        if (!_isReady)
        {
            return;
        }

        RenderSnapshot(snapshot);
    }

    public void ShowMessage(string message)
    {
        if (!_isReady)
        {
            return;
        }

        _temporaryTip.Show(_messageLabel, message);
    }

    private void RenderSnapshot(PlantingPageSnapshot snapshot)
    {
        ClearChildren(_flowerList);

        foreach (PlantingFlowerOption flower in snapshot.Flowers)
        {
            _flowerList.AddChild(CreateFlowerButton(flower));
        }
    }

    private Button CreateFlowerButton(PlantingFlowerOption flower)
    {
        Button button = new()
        {
            Name = $"Flower_{flower.FlowerId}",
            Text = BuildFlowerButtonText(flower),
            CustomMinimumSize = new Vector2(0f, 104f),
            FocusMode = FocusModeEnum.None,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        ApplyButtonTheme(button, isSelected: false, flower.CanPlant);
        button.Pressed += () => OnFlowerPressed(flower);
        return button;
    }

    private void OnFlowerPressed(PlantingFlowerOption flower)
    {
        if (!flower.CanPlant)
        {
            ShowMessage("缺少种子或药剂");
            return;
        }

        FlowerSelected?.Invoke(flower.FlowerId);
    }

    private static string BuildFlowerButtonText(PlantingFlowerOption flower)
    {
        string status = flower.CanPlant ? "可种植" : "不可种植";
        string displayName = string.IsNullOrWhiteSpace(flower.DisplayName) ? flower.FlowerId : flower.DisplayName;
        return $"{displayName} / {flower.FlowerId}\n种子 x{Math.Max(0, flower.SeedCount)}  药剂 x{Math.Max(0, flower.PotionCount)}\n{status}";
    }

    private void BuildLayout()
    {
        ColorRect background = new()
        {
            Name = "Background",
            Color = new Color(0.9f, 0.93f, 0.8f)
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        background.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(background);

        PanelContainer panel = new()
        {
            Name = "Panel"
        };
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        panel.OffsetLeft = 36f;
        panel.OffsetTop = 54f;
        panel.OffsetRight = -36f;
        panel.OffsetBottom = -54f;
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(1f, 0.98f, 0.9f, 0.96f), new Color(0.32f, 0.24f, 0.13f, 0.46f)));
        AddChild(panel);

        VBoxContainer content = new()
        {
            Name = "Content",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 18);
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
            Text = "种植页面",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 42);
        title.AddThemeColorOverride("font_color", new Color(0.16f, 0.11f, 0.07f));
        header.AddChild(title);

        _backButton = new Button
        {
            Name = "BackButton",
            Text = "返回",
            CustomMinimumSize = new Vector2(128f, 58f),
            FocusMode = FocusModeEnum.None
        };
        ApplyHeaderButtonTheme(_backButton);
        header.AddChild(_backButton);

        VBoxContainer body = new()
        {
            Name = "Body",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("separation", 18);
        content.AddChild(body);

        VBoxContainer flowerColumn = CreateColumn("FlowerColumn", "库存花");
        body.AddChild(flowerColumn);
        _flowerList = new VBoxContainer
        {
            Name = "FlowerList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _flowerList.AddThemeConstantOverride("separation", 10);
        flowerColumn.AddChild(_flowerList);

        _messageLabel = new Label
        {
            Name = "MessageLabel",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(0f, 44f)
        };
        _messageLabel.AddThemeFontSizeOverride("font_size", 24);
        _messageLabel.AddThemeColorOverride("font_color", new Color(0.18f, 0.1f, 0.06f));
        content.AddChild(_messageLabel);
    }

    private static VBoxContainer CreateColumn(string name, string titleText)
    {
        VBoxContainer column = new()
        {
            Name = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        column.AddThemeConstantOverride("separation", 10);

        Label title = new()
        {
            Name = "ColumnTitle",
            Text = titleText,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(0f, 34f)
        };
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", new Color(0.18f, 0.12f, 0.08f));
        column.AddChild(title);

        return column;
    }

    private static void ApplyButtonTheme(Button button, bool isSelected, bool isAvailable)
    {
        Color textColor = isAvailable ? new Color(0.18f, 0.12f, 0.07f) : new Color(0.36f, 0.35f, 0.32f);
        Color normal = isSelected
            ? new Color(0.8f, 0.95f, 0.72f, 0.98f)
            : isAvailable ? new Color(0.96f, 0.88f, 0.62f, 0.95f) : new Color(0.72f, 0.72f, 0.67f, 0.78f);
        button.AddThemeFontSizeOverride("font_size", 22);
        button.AddThemeColorOverride("font_color", textColor);
        button.AddThemeColorOverride("font_hover_color", textColor);
        button.AddThemeColorOverride("font_pressed_color", textColor);
        button.AddThemeColorOverride("font_focus_color", textColor);
        button.AddThemeStyleboxOverride("normal", CreatePanelStyle(normal, new Color(0.34f, 0.24f, 0.12f, 0.42f)));
        button.AddThemeStyleboxOverride("hover", CreatePanelStyle(Lighten(normal, 0.08f), new Color(0.42f, 0.28f, 0.13f, 0.62f)));
        button.AddThemeStyleboxOverride("pressed", CreatePanelStyle(Darken(normal, 0.1f), new Color(0.28f, 0.18f, 0.08f, 0.7f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static void ApplyHeaderButtonTheme(Button button)
    {
        Color textColor = new(0.18f, 0.12f, 0.07f);
        button.AddThemeFontSizeOverride("font_size", 24);
        button.AddThemeColorOverride("font_color", textColor);
        button.AddThemeColorOverride("font_hover_color", textColor);
        button.AddThemeColorOverride("font_pressed_color", textColor);
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
            ContentMarginLeft = 16f,
            ContentMarginTop = 12f,
            ContentMarginRight = 16f,
            ContentMarginBottom = 12f
        };
    }

    private static Color Lighten(Color color, float amount)
    {
        return new Color(
            Mathf.Min(1f, color.R + amount),
            Mathf.Min(1f, color.G + amount),
            Mathf.Min(1f, color.B + amount),
            color.A);
    }

    private static Color Darken(Color color, float amount)
    {
        return new Color(
            Mathf.Max(0f, color.R - amount),
            Mathf.Max(0f, color.G - amount),
            Mathf.Max(0f, color.B - amount),
            color.A);
    }

    private static void ClearChildren(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
