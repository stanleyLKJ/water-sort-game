#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using WaterSortGame.Model;

namespace WaterSortGame.View;

[Tool]
public sealed partial class HomeGardenView : Control
{
    public event Action? StartGameRequested;
    public event Action? LevelSelectRequested;
    public event Action<int>? FlowerSlotPlantRequested;

    [Export]
    public bool ShowDebugSlots { get; set; } = false;

    private string _previewFlowerId = "pink_rose";
    private bool _showEditorFlowerPreview = false;

    [Export]
    public string PreviewFlowerId
    {
        get => _previewFlowerId;
        set
        {
            _previewFlowerId = value;
            ApplyEditorFlowerPreviewState();
        }
    }

    [Export]
    public bool ShowEditorFlowerPreview
    {
        get => _showEditorFlowerPreview;
        set
        {
            _showEditorFlowerPreview = value;
            ApplyEditorFlowerPreviewState();
        }
    }

    private const int FlowerSlotCount = RunSessionState.MaxFlowerCount;
    private const string GardenBackgroundPath = "res://assets/home/backgrounds/home_garden_bg_v1.png";
    private const string HomeSlotPathFormat = "res://assets/flowers/{0}/home_slots/{0}_slot_{1:00}.png";
    private const string ComboHomeSlotPathFormat = "res://assets/flowers/combinations/{0}/home_slots/{0}_slot_{1:00}.png";
    private const string LegacyPinkRoseSlotPathFormat = "res://assets/flowers/pink_rose/slots/pink_rose_slot_{0:00}.png";
    private const string DefaultFlowerTextureNodeName = "FlowerTexture";
    private const string YellowRoseTextureNodeName = "YellowRoseTexture";
    private const string LavenderTextureNodeName = "LavenderTexture";
    private const string PlantMarkerButtonNodeName = "PlantMarkerButton";
    private const string ComboPlaceholderNodeName = "ComboAssetPlaceholder";
    private const float PlantMarkerDiameter = 54f;

    private TextureRect _background = null!;
    private Control _flowerSlotRoot = null!;
    private Control _flowerDisplayRoot = null!;
    private Label _statusLabel = null!;
    private Control[] _flowerSlots = Array.Empty<Control>();
    private TextureRect[] _flowerSlotTextures = Array.Empty<TextureRect>();
    private Button[] _plantMarkerButtons = Array.Empty<Button>();
    private RunSessionState? _pendingState;
    private bool _isReady;

    public override void _Ready()
    {
        _background = GetNode<TextureRect>("Background");
        _flowerSlotRoot = GetNode<Control>("FlowerSlotRoot");
        _flowerDisplayRoot = GetNode<Control>("FlowerDisplayRoot");
        CacheFlowerSlots();
        ApplyDebugSlotVisibility();
        _isReady = true;

        if (!Engine.IsEditorHint())
        {
            _statusLabel = CreateStatusLabel();
            AddChild(_statusLabel);
            GetNode<Button>("ButtonRoot/StartGameButton").Pressed += OnStartGamePressed;
            Button levelSelectButton = GetNode<Button>("ButtonRoot/LevelSelectButton");
            levelSelectButton.Visible = false;
            levelSelectButton.Disabled = true;
            levelSelectButton.Pressed += OnLevelSelectPressed;
        }

        if (_pendingState != null)
        {
            RefreshFlowers(_pendingState);
            return;
        }

        if (ShowEditorFlowerPreview)
        {
            ApplyEditorFlowerPreview();
        }
    }

    public void RefreshFlowers(RunSessionState state)
    {
        _pendingState = state;

        if (!_isReady)
        {
            return;
        }

        // Runtime state is authoritative. Editor preview never writes or overrides planted flowers.
        ClearFlowerDisplays();

        _background.Texture = GD.Load<Texture2D>(GardenBackgroundPath);
        RefreshStatus(state);

        HideAllFlowerSlotTextures();
        RefreshPlantingMarkers(state);

        for (int i = 0; i < Mathf.Min(state.FlowerSlotBatches.Count, _flowerSlots.Length); i++)
        {
            IReadOnlyList<string> flowerIds = state.FlowerSlotBatches[i];
            if (flowerIds.Count > 0)
            {
                LoadHomeSlotBatch(i, flowerIds);
            }
        }
    }

    private void LoadHomeSlotBatch(int zeroBasedSlotIndex, IReadOnlyList<string> flowerIds)
    {
        if (flowerIds.Count == 1)
        {
            LoadHomeSlotTexture(zeroBasedSlotIndex, flowerIds[0]);
            return;
        }

        string comboKey = RunSessionState.BuildComboKey(flowerIds);
        int slotIndex = zeroBasedSlotIndex + 1;
        string texturePath = ResolveComboHomeSlotTexturePath(comboKey, slotIndex);
        Texture2D? texture = LoadTexture(texturePath);

        if (texture == null)
        {
            GD.PushWarning($"Home garden combo texture not found: {texturePath}. Showing placeholder for combo {comboKey}.");
            ShowMissingComboPlaceholder(zeroBasedSlotIndex, comboKey);
            return;
        }

        TextureRect textureRect = GetFlowerSlotTexture(zeroBasedSlotIndex, comboKey, out _);
        textureRect.Texture = texture;
        textureRect.Visible = true;
    }

    private void LoadHomeSlotTexture(int zeroBasedSlotIndex, string flowerId)
    {
        int slotIndex = zeroBasedSlotIndex + 1;
        TextureRect textureRect = GetFlowerSlotTexture(zeroBasedSlotIndex, flowerId, out bool hasDedicatedNode);

        if (!hasDedicatedNode)
        {
            GD.PushWarning($"Home garden flower node missing for {flowerId} slot {slotIndex:00}; falling back to {ResolveHomeSlotTexturePath(flowerId, slotIndex)}. Expected node: {GetFlowerTextureNodeName(flowerId)}.");
        }

        if (hasDedicatedNode && textureRect.Texture != null)
        {
            textureRect.Visible = true;
            return;
        }

        string texturePath = ResolveHomeSlotTexturePath(flowerId, slotIndex);

        Texture2D? texture = LoadTexture(texturePath);
        if (texture == null)
        {
            GD.PushWarning($"Home garden flower slot texture not found: {texturePath}");
            textureRect.Texture = null;
            textureRect.Visible = false;
            return;
        }

        textureRect.Texture = texture;
        textureRect.Visible = textureRect.Texture != null;
    }

    public static string ResolveHomeSlotTexturePath(string flowerId, int slotIndex)
    {
        string texturePath = string.Format(HomeSlotPathFormat, flowerId, slotIndex);
        if (ResourceLoader.Exists(texturePath) || FileAccess.FileExists(texturePath))
        {
            return texturePath;
        }

        if (flowerId == "pink_rose")
        {
            string legacyTexturePath = string.Format(LegacyPinkRoseSlotPathFormat, slotIndex);
            if (ResourceLoader.Exists(legacyTexturePath) || FileAccess.FileExists(legacyTexturePath))
            {
                return legacyTexturePath;
            }
        }

        return texturePath;
    }

    public static string ResolveComboHomeSlotTexturePath(string comboKey, int slotIndex)
    {
        return string.Format(ComboHomeSlotPathFormat, comboKey, slotIndex);
    }

    private static Texture2D? LoadTexture(string texturePath)
    {
        if (ResourceLoader.Exists(texturePath))
        {
            return GD.Load<Texture2D>(texturePath);
        }

        if (FileAccess.FileExists(texturePath))
        {
            Image image = Image.LoadFromFile(texturePath);
            if (image != null && !image.IsEmpty())
            {
                ImageTexture texture = ImageTexture.CreateFromImage(image);
                return texture;
            }
        }

        return null;
    }

    private void CacheFlowerSlots()
    {
        _flowerSlots = new Control[FlowerSlotCount];
        _flowerSlotTextures = new TextureRect[FlowerSlotCount];
        _plantMarkerButtons = new Button[FlowerSlotCount];
        for (int i = 0; i < FlowerSlotCount; i++)
        {
            _flowerSlots[i] = GetNode<Control>($"FlowerSlotRoot/PinkRoseSlot_{i + 1:00}");
            _flowerSlotTextures[i] = GetFlowerSlotTexture(i, "pink_rose", out _);
            _plantMarkerButtons[i] = EnsurePlantMarkerButton(_flowerSlots[i], i);
            // Keep scene textures visible for editor placement; runtime hides empty slots here.
            if (!Engine.IsEditorHint() || ShowEditorFlowerPreview)
            {
                HideFlowerSlotTextures(_flowerSlots[i]);
            }
            if (!Engine.IsEditorHint())
            {
                _flowerSlots[i].MouseFilter = MouseFilterEnum.Ignore;
            }
        }
    }

    private void ApplyEditorFlowerPreviewState()
    {
        if (!_isReady || _pendingState != null)
        {
            return;
        }

        if (!ShowEditorFlowerPreview)
        {
            HideAllFlowerSlotTextures();
            return;
        }

        ApplyEditorFlowerPreview();
    }

    private void ApplyEditorFlowerPreview()
    {
        if (!_isReady)
        {
            return;
        }

        string flowerId = string.IsNullOrWhiteSpace(PreviewFlowerId) ? "pink_rose" : PreviewFlowerId.Trim();
        HideAllFlowerSlotTextures();
        for (int i = 0; i < _flowerSlotTextures.Length; i++)
        {
            LoadHomeSlotTexture(i, flowerId);
        }
    }

    private void HideAllFlowerSlotTextures()
    {
        foreach (TextureRect textureRect in _flowerSlotTextures)
        {
            textureRect.Texture = null;
            textureRect.Visible = false;
        }

        foreach (Control slot in _flowerSlots)
        {
            HideFlowerSlotTextures(slot);
            ClearComboPlaceholder(slot);
        }
    }

    private void RefreshPlantingMarkers(RunSessionState state)
    {
        for (int i = 0; i < _plantMarkerButtons.Length; i++)
        {
            Button marker = _plantMarkerButtons[i];
            bool shouldShow = state.CanPlantPendingRewardAt(i);
            marker.Visible = shouldShow;
            marker.Disabled = !shouldShow;
        }
    }

    private TextureRect GetFlowerSlotTexture(int slotIndex, string flowerId, out bool hasDedicatedNode)
    {
        string textureNodeName = GetFlowerTextureNodeName(flowerId);
        TextureRect? textureRect = _flowerSlots[slotIndex].GetNodeOrNull<TextureRect>(textureNodeName);
        if (textureRect != null)
        {
            hasDedicatedNode = true;
            return textureRect;
        }

        hasDedicatedNode = false;
        return _flowerSlots[slotIndex].GetNode<TextureRect>(DefaultFlowerTextureNodeName);
    }

    private static string GetFlowerTextureNodeName(string flowerId)
    {
        return flowerId switch
        {
            "pink_rose" => DefaultFlowerTextureNodeName,
            "yellow_rose" => YellowRoseTextureNodeName,
            "lavender" => LavenderTextureNodeName,
            _ => $"{ToPascalCase(flowerId)}Texture"
        };
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Flower";
        }

        string[] parts = value.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "Flower";
        }

        string result = string.Empty;
        foreach (string part in parts)
        {
            result += char.ToUpperInvariant(part[0]) + part[1..];
        }

        return result;
    }

    private static void HideFlowerSlotTextures(Control slot)
    {
        foreach (Node child in slot.GetChildren())
        {
            if (child is TextureRect textureRect)
            {
                textureRect.Visible = false;
            }
        }
    }

    private Button EnsurePlantMarkerButton(Control slot, int slotIndex)
    {
        Button? marker = slot.GetNodeOrNull<Button>(PlantMarkerButtonNodeName);
        if (marker == null)
        {
            marker = CreatePlantMarkerButton(slotIndex);
            slot.AddChild(marker);
        }

        marker.Name = PlantMarkerButtonNodeName;
        marker.Text = string.Empty;
        marker.Visible = false;
        marker.Disabled = true;
        marker.MouseFilter = MouseFilterEnum.Stop;
        marker.ZIndex = 20;
        ApplyPlantMarkerTheme(marker);
        marker.SetAnchorsPreset(LayoutPreset.Center);
        marker.OffsetLeft = -PlantMarkerDiameter * 0.5f;
        marker.OffsetTop = -PlantMarkerDiameter * 0.5f;
        marker.OffsetRight = PlantMarkerDiameter * 0.5f;
        marker.OffsetBottom = PlantMarkerDiameter * 0.5f;

        Label? numberLabel = marker.GetNodeOrNull<Label>("NumberLabel");
        if (numberLabel == null)
        {
            numberLabel = CreatePlantMarkerNumberLabel(slotIndex);
            marker.AddChild(numberLabel);
        }

        numberLabel.Text = (slotIndex + 1).ToString();

        if (!Engine.IsEditorHint())
        {
            marker.GuiInput += inputEvent => OnPlantMarkerGuiInput(slotIndex, marker, inputEvent);
        }

        return marker;
    }

    private static Button CreatePlantMarkerButton(int slotIndex)
    {
        Button marker = new()
        {
            Name = PlantMarkerButtonNodeName,
            FocusMode = FocusModeEnum.None,
            Flat = false,
            TooltipText = $"Plant slot {slotIndex + 1}"
        };

        ApplyPlantMarkerTheme(marker);
        return marker;
    }

    private static void ApplyPlantMarkerTheme(Button marker)
    {
        StyleBoxFlat normal = CreatePlantMarkerStyle(new Color(1f, 0.94f, 0.68f, 0.9f), new Color(0.44f, 0.28f, 0.12f, 0.86f));
        StyleBoxFlat hover = CreatePlantMarkerStyle(new Color(1f, 0.98f, 0.78f, 0.96f), new Color(0.5f, 0.32f, 0.14f, 0.92f));
        StyleBoxFlat pressed = CreatePlantMarkerStyle(new Color(0.92f, 0.78f, 0.46f, 0.96f), new Color(0.36f, 0.22f, 0.1f, 0.96f));
        marker.AddThemeStyleboxOverride("normal", normal);
        marker.AddThemeStyleboxOverride("hover", hover);
        marker.AddThemeStyleboxOverride("pressed", pressed);
        marker.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        marker.AddThemeStyleboxOverride("disabled", normal);
    }

    private static StyleBoxFlat CreatePlantMarkerStyle(Color bgColor, Color borderColor)
    {
        int radius = Mathf.RoundToInt(PlantMarkerDiameter * 0.5f);
        return new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = borderColor,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusBottomLeft = radius,
            ShadowColor = new Color(0.18f, 0.11f, 0.06f, 0.22f),
            ShadowSize = 5,
            ShadowOffset = new Vector2(0f, 2f)
        };
    }

    private static Label CreatePlantMarkerNumberLabel(int slotIndex)
    {
        Label label = new()
        {
            Name = "NumberLabel",
            Text = (slotIndex + 1).ToString(),
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        label.AddThemeFontSizeOverride("font_size", 26);
        label.AddThemeColorOverride("font_color", new Color(0.2f, 0.12f, 0.06f));
        return label;
    }

    private void ApplyDebugSlotVisibility()
    {
        _flowerSlotRoot.Visible = true;

        foreach (Control slot in _flowerSlots)
        {
            slot.SelfModulate = ShowDebugSlots ? Colors.White : new Color(1f, 1f, 1f, 0f);
        }
    }

    private void ClearFlowerDisplays()
    {
        foreach (Node child in _flowerDisplayRoot.GetChildren())
        {
            _flowerDisplayRoot.RemoveChild(child);
            child.QueueFree();
        }

        foreach (Control slot in _flowerSlots)
        {
            ClearComboPlaceholder(slot);
        }
    }

    private static void ClearComboPlaceholder(Control slot)
    {
        Node? placeholder = slot.GetNodeOrNull(ComboPlaceholderNodeName);
        if (placeholder != null)
        {
            slot.RemoveChild(placeholder);
            placeholder.QueueFree();
        }
    }

    private void ShowMissingComboPlaceholder(int zeroBasedSlotIndex, string comboKey)
    {
        Control slot = _flowerSlots[zeroBasedSlotIndex];
        ClearComboPlaceholder(slot);

        Control placeholder = new()
        {
            Name = ComboPlaceholderNodeName,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 12
        };
        placeholder.SetAnchorsPreset(LayoutPreset.Center);
        placeholder.OffsetLeft = -74f;
        placeholder.OffsetTop = -46f;
        placeholder.OffsetRight = 74f;
        placeholder.OffsetBottom = 46f;

        ColorRect background = new()
        {
            Name = "PlaceholderBackground",
            Color = new Color(0.52f, 0.42f, 0.28f, 0.76f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        placeholder.AddChild(background);

        Label label = new()
        {
            Name = "PlaceholderLabel",
            Text = $"{comboKey}\n组合素材待补",
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        label.OffsetLeft = 8f;
        label.OffsetTop = 8f;
        label.OffsetRight = -8f;
        label.OffsetBottom = -8f;
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color(1f, 0.96f, 0.82f));
        placeholder.AddChild(label);

        slot.AddChild(placeholder);
    }

    public void PlayPlantingFeedback(int slotIndex)
    {
        if (!_isReady || slotIndex < 0 || slotIndex >= _flowerSlots.Length)
        {
            return;
        }

        Control slot = _flowerSlots[slotIndex];
        Vector2 displayRootPosition = _flowerDisplayRoot.GetGlobalRect().Position;
        Vector2 slotCenter = slot.GetGlobalRect().GetCenter() - displayRootPosition;

        ColorRect potionFlash = new()
        {
            Name = "PotionPourFlash",
            MouseFilter = MouseFilterEnum.Ignore,
            Color = new Color(0.52f, 0.92f, 0.82f, 0.72f),
            Position = slotCenter + new Vector2(-24f, -74f),
            Size = new Vector2(48f, 86f),
            Rotation = -0.28f
        };
        _flowerDisplayRoot.AddChild(potionFlash);

        Tween tween = CreateTween();
        tween.TweenProperty(potionFlash, "position:y", slotCenter.Y - 18f, 0.32);
        tween.Parallel().TweenProperty(potionFlash, "modulate:a", 0f, 0.32);
        tween.TweenCallback(Callable.From(() => potionFlash.QueueFree()));
    }

    public void ShowMessage(string message)
    {
        if (!_isReady)
        {
            return;
        }

        _statusLabel.Text = message;
        _statusLabel.Visible = !string.IsNullOrEmpty(message);
    }

    private void RefreshStatus(RunSessionState state)
    {
        if (state.PendingPlanting)
        {
            ShowMessage(HasPlantablePendingRewardSlot(state)
                ? "请选择可操作花位种植或追加"
                : "没有可追加的位置，请选择其他花");
            return;
        }

        ShowMessage(string.Empty);
    }

    private static bool HasPlantablePendingRewardSlot(RunSessionState state)
    {
        for (int i = 0; i < state.FlowerSlotBatches.Count; i++)
        {
            if (state.CanPlantPendingRewardAt(i))
            {
                return true;
            }
        }

        return false;
    }

    private void OnPlantMarkerGuiInput(int slotIndex, Button marker, InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton mouseButton || !mouseButton.Pressed || mouseButton.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        Vector2 center = marker.Size * 0.5f;
        if (mouseButton.Position.DistanceTo(center) > PlantMarkerDiameter * 0.5f)
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        FlowerSlotPlantRequested?.Invoke(slotIndex);
        GetViewport().SetInputAsHandled();
    }

    private static Label CreateStatusLabel()
    {
        Label label = new()
        {
            Name = "PlantingStatusLabel",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetAnchorsPreset(LayoutPreset.TopWide);
        label.OffsetLeft = 132f;
        label.OffsetTop = 128f;
        label.OffsetRight = -132f;
        label.OffsetBottom = 184f;
        label.AddThemeFontSizeOverride("font_size", 26);
        label.AddThemeColorOverride("font_color", new Color(0.18f, 0.12f, 0.08f));

        StyleBoxFlat style = new()
        {
            BgColor = new Color(1f, 0.96f, 0.82f, 0.88f),
            BorderColor = new Color(0.38f, 0.25f, 0.12f, 0.36f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusBottomLeft = 8
        };
        label.AddThemeStyleboxOverride("normal", style);
        return label;
    }

    private void OnStartGamePressed()
    {
        StartGameRequested?.Invoke();
    }

    private void OnLevelSelectPressed()
    {
        LevelSelectRequested?.Invoke();
    }
}
