#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;

namespace WaterSortGame.View;

[Tool]
public sealed partial class HomeGardenView : Control
{
	public event Action? StartGameRequested;
	public event Action? LevelSelectRequested;
	public event Action<int>? FlowerSlotPlantRequested;
	public event Action<int, string>? FlowerSlotFlowerSelected;
	public event Action<int>? FlowerSlotShovelAllRequested;
	public event Action<int, string>? FlowerSlotFlowerShovelRequested;
	public event Action? WarehouseRequested;
	public event Action? PlantingRequested;
	public event Action? SettingsRequested;

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
	private const string DefaultFlowerTextureNodeName = "FlowerTexture";
	private const string YellowRoseTextureNodeName = "YellowRoseTexture";
	private const string LavenderTextureNodeName = "LavenderTexture";
	private const string PinkRoseLavenderComboTextureNodeName = "PinkRoseLavenderComboTexture";
	private const string PinkRoseYellowRoseComboTextureNodeName = "PinkRoseYellowRoseComboTexture";
	private const string YellowRoseLavenderComboTextureNodeName = "YellowRoseLavenderComboTexture";
	private const string PinkRoseYellowRoseLavenderComboTextureNodeName = "PinkRoseYellowRoseLavenderComboTexture";
	private const string PlantMarkerButtonNodeName = "PlantMarkerButton";
	private static readonly Color PlantMarkerTextColor = new(0.17f, 0.13f, 0.09f);
	private const string PendingRewardPreviewNodeName = "PendingRewardPreview";
	private const string PlantingFxLayerNodeName = "PlantingFxLayer";
	private const string SeedTextureNodeName = "SeedTexture";
	private const string PotionTextureNodeName = "PotionTexture";
	private const string SeedFxTextureNodeName = "SeedFxTexture";
	private const string PotionFxTextureNodeName = "PotionFxTexture";
	private const string ButtonRootNodeName = "ButtonRoot";
	private const string StartGameButtonNodeName = "StartGameButton";
	private const string PlantingSignButtonNodeName = "PlantingSignButton";
	private const string WarehouseSignButtonNodeName = "WarehouseSignButton";
	private const string LevelSelectButtonNodeName = "LevelSelectButton";
	private const string SettingsButtonNodeName = "SettingsButton";
	private const string PotionSignClickAreaPath = "PotionSignRoot/SignClickArea";
	private const string PlantSignClickAreaPath = "PlantSignRoot/SignClickArea";
	private const string WarehouseSignClickAreaPath = "WarehouseSignRoot/SignClickArea";
	private const string ItemTexturePathFormat = "res://assets/flowers/{0}/items/{0}_{1}.png";
	private const float PlantMarkerDiameter = 54f;
	private const float PlantingFxIconSize = 74f;

	private TextureRect _background = null!;
	private Control _flowerSlotRoot = null!;
	private Control _flowerDisplayRoot = null!;
	private Control _pendingRewardPreview = null!;
	private TextureRect _seedTexture = null!;
	private TextureRect _potionTexture = null!;
	private Control _plantingFxLayer = null!;
	private TextureRect _seedFxTexture = null!;
	private TextureRect _potionFxTexture = null!;
	private PopupPanel _plantingFlowerPopup = null!;
	private VBoxContainer _plantingFlowerList = null!;
	private Area2D _startGameClickArea = null!;
	private Area2D _warehouseClickArea = null!;
	private Area2D _plantingClickArea = null!;
	private CollisionPolygon2D _startGameClickPolygon = null!;
	private CollisionPolygon2D _warehouseClickPolygon = null!;
	private CollisionPolygon2D _plantingClickPolygon = null!;
	private Button _levelSelectButton = null!;
	private Button _settingsButton = null!;
	private Label _statusLabel = null!;
	private Control[] _flowerSlots = Array.Empty<Control>();
	private TextureRect[] _flowerSlotTextures = Array.Empty<TextureRect>();
	private Button[] _plantMarkerButtons = Array.Empty<Button>();
	private bool[] _warehousePlantingSlots = Array.Empty<bool>();
	private readonly TemporaryTipHandle _temporaryTip = new();
	private RunSessionState? _pendingState;
	private IReadOnlyList<HomeGardenPlantingSlotOption>? _pendingWarehousePlantingSlots;
	private bool _inputLocked;
	private bool _isReady;
	private LocalizationManager? _localizationManager;

	public void SetLocalizationManager(LocalizationManager localizationManager)
	{
		_localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
		if (_isReady && !Engine.IsEditorHint())
		{
			ApplyLocalizedText();
		}
	}

	public override void _Ready()
	{
		_background = GetNode<TextureRect>("Background");
		_flowerSlotRoot = GetNode<Control>("FlowerSlotRoot");
		_flowerDisplayRoot = GetNode<Control>("FlowerDisplayRoot");
		_pendingRewardPreview = GetNode<Control>(PendingRewardPreviewNodeName);
		_seedTexture = _pendingRewardPreview.GetNode<TextureRect>(SeedTextureNodeName);
		_potionTexture = _pendingRewardPreview.GetNode<TextureRect>(PotionTextureNodeName);
		_plantingFxLayer = GetNode<Control>(PlantingFxLayerNodeName);
		_seedFxTexture = _plantingFxLayer.GetNode<TextureRect>(SeedFxTextureNodeName);
		_potionFxTexture = _plantingFxLayer.GetNode<TextureRect>(PotionFxTextureNodeName);
		CacheFlowerSlots();
		ApplyDebugSlotVisibility();
		HidePendingRewardPreview();
		HidePlantingFxTextures();
		_isReady = true;

		if (!Engine.IsEditorHint())
		{
			_statusLabel = CreateStatusLabel();
			AddChild(_statusLabel);
			Control buttonRoot = GetNode<Control>(ButtonRootNodeName);
			DisableLegacySignButton(buttonRoot, StartGameButtonNodeName);
			DisableLegacySignButton(buttonRoot, PlantingSignButtonNodeName);
			DisableLegacySignButton(buttonRoot, WarehouseSignButtonNodeName);
			BindLegacySignButtonPress(buttonRoot, StartGameButtonNodeName, OnStartGamePressed);
			BindLegacySignButtonPress(buttonRoot, PlantingSignButtonNodeName, OnPlantingPressed);
			BindLegacySignButtonPress(buttonRoot, WarehouseSignButtonNodeName, OnWarehousePressed);

			_startGameClickArea = buttonRoot.GetNode<Area2D>(PotionSignClickAreaPath);
			_startGameClickPolygon = _startGameClickArea.GetNode<CollisionPolygon2D>("CollisionPolygon2D");
			BindSignClickArea(_startGameClickArea, OnStartGamePressed);
			_plantingClickArea = buttonRoot.GetNode<Area2D>(PlantSignClickAreaPath);
			_plantingClickPolygon = _plantingClickArea.GetNode<CollisionPolygon2D>("CollisionPolygon2D");
			BindSignClickArea(_plantingClickArea, OnPlantingPressed);
			_warehouseClickArea = buttonRoot.GetNode<Area2D>(WarehouseSignClickAreaPath);
			_warehouseClickPolygon = _warehouseClickArea.GetNode<CollisionPolygon2D>("CollisionPolygon2D");
			BindSignClickArea(_warehouseClickArea, OnWarehousePressed);

			_levelSelectButton = buttonRoot.GetNode<Button>(LevelSelectButtonNodeName);
			_levelSelectButton.Visible = false;
			_levelSelectButton.Disabled = true;
			_levelSelectButton.MouseFilter = MouseFilterEnum.Ignore;
			_levelSelectButton.Pressed += OnLevelSelectPressed;
			_settingsButton = EnsureSettingsButton(buttonRoot);
			_settingsButton.Pressed += OnSettingsPressed;
			EnsurePlantingFlowerPopup();
			ApplyLocalizedText();
		}

		if (_pendingState != null)
		{
			RefreshFlowers(_pendingState, _pendingWarehousePlantingSlots);
			return;
		}

		if (ShowEditorFlowerPreview)
		{
			ApplyEditorFlowerPreview();
		}
	}

	public void RefreshFlowers(
		RunSessionState state,
		IReadOnlyList<HomeGardenPlantingSlotOption>? warehousePlantingSlots = null)
	{
		_pendingState = state;
		_pendingWarehousePlantingSlots = warehousePlantingSlots;
		CacheWarehousePlantingSlots(warehousePlantingSlots);

		if (!_isReady)
		{
			return;
		}

		// Runtime state is authoritative. Editor preview never writes or overrides planted flowers.
		ClearFlowerDisplays();

		RefreshStatus(state);
		RefreshPlantingButtonText(state);
		HidePendingRewardPreview();

		HideAllFlowerSlotTextures();
		RefreshPlantingMarkers(state);

		for (int i = 0; i < Mathf.Min(state.FlowerSlotBatches.Count, _flowerSlots.Length); i++)
		{
			IReadOnlyList<string> flowerIds = state.FlowerSlotBatches[i];
			if (flowerIds.Count > 0)
			{
				ShowHomeSlotBatch(i, flowerIds);
			}
		}
	}

	private void ShowHomeSlotBatch(int zeroBasedSlotIndex, IReadOnlyList<string> flowerIds)
	{
		if (flowerIds.Count == 1)
		{
			ShowHomeSlotNode(zeroBasedSlotIndex, flowerIds[0]);
			return;
		}

		string comboKey = RunSessionState.BuildComboKey(flowerIds);
		ShowHomeSlotNode(zeroBasedSlotIndex, comboKey);
	}

	private void ShowHomeSlotNode(int zeroBasedSlotIndex, string flowerId)
	{
		int slotIndex = zeroBasedSlotIndex + 1;
		TextureRect? textureRect = GetFlowerSlotTextureOrNull(zeroBasedSlotIndex, flowerId);
		if (textureRect == null)
		{
			GD.PushWarning($"Home garden visual node missing for {flowerId} slot {slotIndex:00}. Expected node: {GetFlowerTextureNodeName(flowerId)}.");
			return;
		}

		if (textureRect.Texture == null)
		{
			GD.PushWarning($"Home garden visual node has no scene texture for {flowerId} slot {slotIndex:00}. Node: {GetFlowerTextureNodeName(flowerId)}.");
			return;
		}

		textureRect.Visible = true;
	}

	private void CacheFlowerSlots()
	{
		_flowerSlots = new Control[FlowerSlotCount];
		_flowerSlotTextures = new TextureRect[FlowerSlotCount];
		_plantMarkerButtons = new Button[FlowerSlotCount];
		for (int i = 0; i < FlowerSlotCount; i++)
		{
			_flowerSlots[i] = GetNode<Control>($"FlowerSlotRoot/PinkRoseSlot_{i + 1:00}");
			_flowerSlotTextures[i] = GetNode<TextureRect>($"FlowerSlotRoot/PinkRoseSlot_{i + 1:00}/{DefaultFlowerTextureNodeName}");
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
			if (flowerId.Contains('+', StringComparison.Ordinal))
			{
				ShowHomeSlotBatch(i, flowerId.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
			}
			else
			{
				ShowHomeSlotNode(i, flowerId);
			}
		}
	}

	private void HideAllFlowerSlotTextures()
	{
		foreach (TextureRect textureRect in _flowerSlotTextures)
		{
			textureRect.Visible = false;
		}

		foreach (Control slot in _flowerSlots)
		{
			HideFlowerSlotTextures(slot);
		}
	}

	private void RefreshPlantingMarkers(RunSessionState state)
	{
		for (int i = 0; i < _plantMarkerButtons.Length; i++)
		{
			Button marker = _plantMarkerButtons[i];
			bool shouldShow = state.PendingPlanting
				? state.CanPlantPendingRewardAt(i)
				: state.IsWarehousePlantingMode && i < _warehousePlantingSlots.Length && _warehousePlantingSlots[i];
			marker.Visible = shouldShow;
			marker.Disabled = !shouldShow || _inputLocked;
		}
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (Engine.IsEditorHint()
			|| _inputLocked
			|| (_plantingFlowerPopup != null && _plantingFlowerPopup.Visible)
			|| inputEvent is not InputEventMouseButton mouseButton
			|| mouseButton.ButtonIndex != MouseButton.Left
			|| !mouseButton.Pressed)
		{
			return;
		}

		Vector2 globalMousePosition = GetGlobalMousePosition();
		if (IsPointInsideClickPolygon(_warehouseClickPolygon, globalMousePosition))
		{
			OnWarehousePressed();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (IsPointInsideClickPolygon(_plantingClickPolygon, globalMousePosition))
		{
			OnPlantingPressed();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (IsPointInsideClickPolygon(_startGameClickPolygon, globalMousePosition))
		{
			OnStartGamePressed();
			GetViewport().SetInputAsHandled();
		}
	}

	private void CacheWarehousePlantingSlots(IReadOnlyList<HomeGardenPlantingSlotOption>? slots)
	{
		_warehousePlantingSlots = new bool[FlowerSlotCount];
		if (slots == null)
		{
			return;
		}

		foreach (HomeGardenPlantingSlotOption slot in slots)
		{
			if (slot.SlotIndex < 0 || slot.SlotIndex >= _warehousePlantingSlots.Length)
			{
				continue;
			}

			_warehousePlantingSlots[slot.SlotIndex] = slot.CanOpenFlowerList;
		}
	}

	private static bool TrySetItemTexture(TextureRect textureRect, string flowerId, string itemKind)
	{
		string texturePath = BuildPendingRewardItemPath(flowerId, itemKind);
		Texture2D? texture = LoadItemTexture(texturePath);
		if (texture == null)
		{
			GD.PushWarning($"Planting {itemKind} texture missing for {flowerId}: {texturePath}");
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

	public static string BuildPendingRewardItemPath(string flowerId, string itemKind)
	{
		return string.Format(ItemTexturePathFormat, flowerId, itemKind);
	}

	private void HidePendingRewardPreview()
	{
		_pendingRewardPreview.Visible = false;
		_seedTexture.Texture = null;
		_seedTexture.Visible = false;
		_potionTexture.Texture = null;
		_potionTexture.Visible = false;
	}

	private void HidePlantingFxTextures()
	{
		_plantingFxLayer.Visible = false;
		_seedFxTexture.Visible = false;
		_seedFxTexture.Texture = null;
		_seedFxTexture.Modulate = Colors.White;
		_seedFxTexture.Rotation = 0f;
		_potionFxTexture.Visible = false;
		_potionFxTexture.Texture = null;
		_potionFxTexture.Modulate = Colors.White;
		_potionFxTexture.Rotation = 0f;
	}

	private TextureRect? GetFlowerSlotTextureOrNull(int slotIndex, string flowerId)
	{
		string textureNodeName = GetFlowerTextureNodeName(flowerId);
		return _flowerSlots[slotIndex].GetNodeOrNull<TextureRect>(textureNodeName);
	}

	private static string GetFlowerTextureNodeName(string flowerId)
	{
		return flowerId switch
		{
			"pink_rose" => DefaultFlowerTextureNodeName,
			"yellow_rose" => YellowRoseTextureNodeName,
			"lavender" => LavenderTextureNodeName,
			"pink_rose+lavender" => PinkRoseLavenderComboTextureNodeName,
			"pink_rose+yellow_rose" => PinkRoseYellowRoseComboTextureNodeName,
			"yellow_rose+lavender" => YellowRoseLavenderComboTextureNodeName,
			"pink_rose+yellow_rose+lavender" => PinkRoseYellowRoseLavenderComboTextureNodeName,
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
		ApplyPlantMarkerNumberTheme(numberLabel);

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
		marker.AddThemeColorOverride("font_color", PlantMarkerTextColor);
		marker.AddThemeColorOverride("font_hover_color", PlantMarkerTextColor);
		marker.AddThemeColorOverride("font_pressed_color", PlantMarkerTextColor);
		marker.AddThemeColorOverride("font_focus_color", PlantMarkerTextColor);
		marker.AddThemeColorOverride("font_disabled_color", PlantMarkerTextColor);
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
		ApplyPlantMarkerNumberTheme(label);
		return label;
	}

	private static void ApplyPlantMarkerNumberTheme(Label label)
	{
		label.AddThemeFontSizeOverride("font_size", 26);
		label.AddThemeColorOverride("font_color", PlantMarkerTextColor);
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

	}

	private static void ClearChildren(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			node.RemoveChild(child);
			child.QueueFree();
		}
	}

	public async Task PlayPlantingRewardAnimationAsync(int slotIndex, string flowerId)
	{
		if (!_isReady || slotIndex < 0 || slotIndex >= _flowerSlots.Length || string.IsNullOrWhiteSpace(flowerId))
		{
			return;
		}

		TextureRect[] fxTextures = { _seedFxTexture, _potionFxTexture };
		string[] itemKinds = { "seed", "potion" };
		bool hasAnyTexture = false;
		for (int i = 0; i < fxTextures.Length; i++)
		{
			hasAnyTexture |= TrySetItemTexture(fxTextures[i], flowerId, itemKinds[i]);
		}

		if (!hasAnyTexture)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			return;
		}

		_plantingFxLayer.Visible = true;
		Vector2 layerOrigin = _plantingFxLayer.GetGlobalRect().Position;
		Vector2 slotCenter = _flowerSlots[slotIndex].GetGlobalRect().GetCenter() - layerOrigin;
		Vector2 startBase = new(118f, 168f);
		Vector2 targetBase = slotCenter + new Vector2(-20f, -58f);

		PreparePlantingFxTexture(_seedFxTexture, startBase, true);
		PreparePlantingFxTexture(_potionFxTexture, startBase + new Vector2(84f, 8f), true);

		Tween tween = CreateTween();
		tween.SetParallel();
		if (_seedFxTexture.Visible)
		{
			tween.TweenProperty(_seedFxTexture, "position", targetBase + new Vector2(-26f, -6f), 0.42)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(_seedFxTexture, "rotation", Mathf.DegToRad(45f), 0.42)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(_seedFxTexture, "modulate:a", 0f, 0.18).SetDelay(0.34);
		}

		if (_potionFxTexture.Visible)
		{
			tween.TweenProperty(_potionFxTexture, "position", targetBase + new Vector2(28f, 4f), 0.5)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(_potionFxTexture, "rotation", Mathf.DegToRad(45f), 0.5)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(_potionFxTexture, "modulate:a", 0f, 0.2).SetDelay(0.42);
		}

		for (int i = 0; i < 90 && tween.IsRunning(); i++)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		if (tween.IsRunning())
		{
			tween.Kill();
		}

		HidePlantingFxTextures();
	}

	private static void PreparePlantingFxTexture(TextureRect textureRect, Vector2 position, bool visible)
	{
		textureRect.Visible = visible && textureRect.Texture != null;
		textureRect.Size = new Vector2(PlantingFxIconSize, PlantingFxIconSize);
		textureRect.PivotOffset = textureRect.Size * 0.5f;
		textureRect.Position = position;
		textureRect.Rotation = 0f;
		textureRect.Modulate = Colors.White;
	}

	public void SetPlantingInputLocked(bool locked)
	{
		if (!_isReady)
		{
			return;
		}

		_inputLocked = locked;
		foreach (Button marker in _plantMarkerButtons)
		{
			if (marker.Visible)
			{
				marker.Disabled = locked;
			}
		}

		if (_startGameClickArea != null)
		{
			_startGameClickArea.InputPickable = !locked;
		}

		if (_levelSelectButton != null)
		{
			_levelSelectButton.Disabled = true;
		}

		if (_warehouseClickArea != null)
		{
			_warehouseClickArea.InputPickable = !locked;
		}

		if (_plantingClickArea != null)
		{
			_plantingClickArea.InputPickable = !locked;
		}

		if (_settingsButton != null)
		{
			_settingsButton.Disabled = locked;
		}
	}

	public void ShowMessage(string message)
	{
		if (!_isReady)
		{
			return;
		}

		_temporaryTip.Show(_statusLabel, message);
	}

	public void ShowPlantingFlowerPopup(
		int slotIndex,
		IReadOnlyList<PlantingFlowerOption> flowers,
		IReadOnlyList<PlantedFlowerOption> plantedFlowers)
	{
		if (!_isReady)
		{
			return;
		}

		if (slotIndex < 0 || slotIndex >= FlowerSlotCount)
		{
			throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Flower slot index is out of range.");
		}

		ClearChildren(_plantingFlowerList);

		Label title = new()
		{
			Name = "PopupTitleLabel",
			Text = TrFormat("home.slot_actions", slotIndex + 1),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(0f, 42f)
		};
		title.AddThemeFontSizeOverride("font_size", 26);
		title.AddThemeColorOverride("font_color", new Color(0.18f, 0.12f, 0.07f));
		_plantingFlowerList.AddChild(title);

		if (flowers.Count > 0)
		{
			_plantingFlowerList.AddChild(CreatePopupSectionLabel(Tr("home.plantable_section")));
			foreach (PlantingFlowerOption flower in flowers)
			{
				_plantingFlowerList.AddChild(CreatePlantingFlowerChoiceButton(slotIndex, flower));
			}
		}

		if (plantedFlowers.Count > 0)
		{
			_plantingFlowerList.AddChild(CreatePopupSectionLabel(Tr("home.shovel_section")));
			Button shovelAllButton = new()
			{
				Name = "ShovelAllButton",
				Text = Tr("home.shovel_all"),
				CustomMinimumSize = new Vector2(0f, 58f),
				FocusMode = FocusModeEnum.None
			};
			ApplyWarehouseButtonTheme(shovelAllButton);
			shovelAllButton.Pressed += () =>
			{
				_plantingFlowerPopup.Hide();
				FlowerSlotShovelAllRequested?.Invoke(slotIndex);
			};
			_plantingFlowerList.AddChild(shovelAllButton);

			foreach (PlantedFlowerOption flower in plantedFlowers)
			{
				_plantingFlowerList.AddChild(CreateShovelFlowerChoiceButton(slotIndex, flower));
			}
		}

		if (flowers.Count == 0 && plantedFlowers.Count == 0)
		{
			Label empty = new()
			{
				Name = "NoSlotActionLabel",
				Text = Tr("home.no_action"),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				CustomMinimumSize = new Vector2(0f, 58f)
			};
			empty.AddThemeFontSizeOverride("font_size", 22);
			empty.AddThemeColorOverride("font_color", new Color(0.28f, 0.18f, 0.1f));
			_plantingFlowerList.AddChild(empty);
		}

		Button cancelButton = new()
		{
			Name = "PopupCloseButton",
			Text = Tr("common.close"),
			CustomMinimumSize = new Vector2(0f, 52f),
			FocusMode = FocusModeEnum.None
		};
		ApplyWarehouseButtonTheme(cancelButton);
		cancelButton.Pressed += () => _plantingFlowerPopup.Hide();
		_plantingFlowerList.AddChild(cancelButton);

		int actionCount = flowers.Count + plantedFlowers.Count + (plantedFlowers.Count > 0 ? 1 : 0);
		_plantingFlowerPopup.PopupCentered(new Vector2I(440, Math.Min(760, actionCount == 0 ? 230 : 260 + actionCount * 82)));
	}

	private static Label CreatePopupSectionLabel(string text)
	{
		Label label = new()
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(0f, 34f)
		};
		label.AddThemeFontSizeOverride("font_size", 22);
		label.AddThemeColorOverride("font_color", new Color(0.2f, 0.13f, 0.08f));
		return label;
	}

	private Button CreatePlantingFlowerChoiceButton(int slotIndex, PlantingFlowerOption flower)
	{
		string displayName = string.IsNullOrWhiteSpace(flower.DisplayName) ? flower.FlowerId : flower.DisplayName;
		Button button = new()
		{
			Name = $"PlantingChoice_{flower.FlowerId}",
			Text = TrFormat("home.plant_choice", displayName, flower.FlowerId, Math.Max(0, flower.SeedCount), Math.Max(0, flower.PotionCount)),
			CustomMinimumSize = new Vector2(0f, 84f),
			FocusMode = FocusModeEnum.None,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		ApplyWarehouseButtonTheme(button);
		button.Pressed += () =>
		{
			_plantingFlowerPopup.Hide();
			FlowerSlotFlowerSelected?.Invoke(slotIndex, flower.FlowerId);
		};
		return button;
	}

	private Button CreateShovelFlowerChoiceButton(int slotIndex, PlantedFlowerOption flower)
	{
		string displayName = string.IsNullOrWhiteSpace(flower.DisplayName) ? flower.FlowerId : flower.DisplayName;
		Button button = new()
		{
			Name = $"ShovelChoice_{flower.FlowerId}",
			Text = TrFormat("home.shovel_flower", displayName, flower.FlowerId),
			CustomMinimumSize = new Vector2(0f, 62f),
			FocusMode = FocusModeEnum.None,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		ApplyWarehouseButtonTheme(button);
		button.Pressed += () =>
		{
			_plantingFlowerPopup.Hide();
			FlowerSlotFlowerShovelRequested?.Invoke(slotIndex, flower.FlowerId);
		};
		return button;
	}

	private void RefreshStatus(RunSessionState state)
	{
		if (state.PendingPlanting)
		{
			ShowMessage(HasPlantablePendingRewardSlot(state)
				? Tr("home.select_available_slot")
				: Tr("home.no_append_slot"));
			return;
		}

		if (state.IsWarehousePlantingMode)
		{
			ShowMessage(HasWarehousePlantingSlot()
				? Tr("home.select_planting_slot")
				: Tr("home.no_plantable_flower"));
			return;
		}

		ShowMessage(string.Empty);
	}

	private bool HasWarehousePlantingSlot()
	{
		foreach (bool canOpen in _warehousePlantingSlots)
		{
			if (canOpen)
			{
				return true;
			}
		}

		return false;
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
		if (_inputLocked)
		{
			GetViewport().SetInputAsHandled();
			return;
		}

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

	private void RefreshPlantingButtonText(RunSessionState state)
	{
		_ = state;
	}

	private void EnsurePlantingFlowerPopup()
	{
		_plantingFlowerPopup = GetNodeOrNull<PopupPanel>("PlantingFlowerPopup") ?? new PopupPanel
		{
			Name = "PlantingFlowerPopup"
		};

		if (_plantingFlowerPopup.GetParent() == null)
		{
			AddChild(_plantingFlowerPopup);
		}

		PanelContainer panel = _plantingFlowerPopup.GetNodeOrNull<PanelContainer>("Panel") ?? new PanelContainer
		{
			Name = "Panel"
		};
		if (panel.GetParent() == null)
		{
			_plantingFlowerPopup.AddChild(panel);
		}

		panel.AddThemeStyleboxOverride("panel", CreateWarehouseButtonStyle(new Color(1f, 0.97f, 0.86f, 0.98f), new Color(0.36f, 0.22f, 0.1f, 0.72f)));
		_plantingFlowerList = panel.GetNodeOrNull<VBoxContainer>("FlowerList") ?? new VBoxContainer
		{
			Name = "FlowerList",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		_plantingFlowerList.AddThemeConstantOverride("separation", 10);
		if (_plantingFlowerList.GetParent() == null)
		{
			panel.AddChild(_plantingFlowerList);
		}
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

	private void BindSignClickArea(Area2D clickArea, Action onPressed)
	{
		clickArea.InputPickable = true;
		if (clickArea.GetParent() is Node signRoot)
		{
			Label? signLabel = signRoot.GetNodeOrNull<Label>("SignLabel");
			if (signLabel != null)
			{
				signLabel.MouseFilter = MouseFilterEnum.Ignore;
			}
		}

		clickArea.InputEvent += (_, inputEvent, _) => OnSignClickAreaInput(inputEvent, onPressed);
	}

	private void OnSignClickAreaInput(InputEvent inputEvent, Action onPressed)
	{
		if (inputEvent is not InputEventMouseButton mouseButton
			|| mouseButton.ButtonIndex != MouseButton.Left
			|| !mouseButton.Pressed)
		{
			return;
		}

		if (_inputLocked)
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		onPressed();
		GetViewport().SetInputAsHandled();
	}

	private static bool IsPointInsideClickPolygon(CollisionPolygon2D? polygonNode, Vector2 globalPoint)
	{
		if (polygonNode == null || polygonNode.Disabled)
		{
			return false;
		}

		Vector2[] polygon = polygonNode.Polygon;
		if (polygon.Length < 3)
		{
			return false;
		}

		Vector2 localPoint = polygonNode.GlobalTransform.AffineInverse() * globalPoint;
		bool inside = false;
		for (int current = 0, previous = polygon.Length - 1; current < polygon.Length; previous = current++)
		{
			Vector2 currentPoint = polygon[current];
			Vector2 previousPoint = polygon[previous];
			bool crossesHorizontalRay = currentPoint.Y > localPoint.Y != previousPoint.Y > localPoint.Y;
			if (!crossesHorizontalRay)
			{
				continue;
			}

			float intersectionX = (previousPoint.X - currentPoint.X)
				* (localPoint.Y - currentPoint.Y)
				/ (previousPoint.Y - currentPoint.Y)
				+ currentPoint.X;
			if (localPoint.X < intersectionX)
			{
				inside = !inside;
			}
		}

		return inside;
	}

	private static void DisableLegacySignButton(Control buttonRoot, string buttonName)
	{
		Button? button = buttonRoot.GetNodeOrNull<Button>(buttonName);
		if (button == null)
		{
			return;
		}

		button.Visible = false;
		button.Disabled = true;
		button.MouseFilter = MouseFilterEnum.Ignore;
		button.FocusMode = FocusModeEnum.None;
	}

	private static void BindLegacySignButtonPress(Control buttonRoot, string buttonName, Action onPressed)
	{
		Button? button = buttonRoot.GetNodeOrNull<Button>(buttonName);
		if (button == null)
		{
			return;
		}

		button.Pressed += onPressed;
	}

	private void OnStartGamePressed()
	{
		if (_inputLocked)
		{
			return;
		}

		StartGameRequested?.Invoke();
	}

	private void OnLevelSelectPressed()
	{
		if (_inputLocked)
		{
			return;
		}

		LevelSelectRequested?.Invoke();
	}

	private void OnWarehousePressed()
	{
		if (_inputLocked)
		{
			return;
		}

		WarehouseRequested?.Invoke();
	}

	private void OnPlantingPressed()
	{
		if (_inputLocked)
		{
			return;
		}

		PlantingRequested?.Invoke();
	}

	private void OnSettingsPressed()
	{
		if (_inputLocked)
		{
			return;
		}

		SettingsRequested?.Invoke();
	}

	private Button EnsureSettingsButton(Control buttonRoot)
	{
		Button? button = buttonRoot.GetNodeOrNull<Button>(SettingsButtonNodeName);
		if (button == null)
		{
			GD.PushWarning($"HomeGarden ButtonRoot is missing {SettingsButtonNodeName}. Creating runtime fallback.");
			button = new Button
			{
				Name = SettingsButtonNodeName
			};
			buttonRoot.AddChild(button);
			button.SetAnchorsPreset(LayoutPreset.TopLeft);
			button.OffsetLeft = 608f;
			button.OffsetTop = 24f;
			button.OffsetRight = 696f;
			button.OffsetBottom = 76f;
		}

		button.Text = Tr("home.settings");
		button.Visible = true;
		button.Disabled = false;
		button.Flat = false;
		button.FocusMode = FocusModeEnum.None;
		button.CustomMinimumSize = new Vector2(88f, 52f);
		ApplyWarehouseButtonTheme(button);
		return button;
	}

	private void ApplyLocalizedText()
	{
		Control buttonRoot = GetNode<Control>(ButtonRootNodeName);
		buttonRoot.GetNode<Label>("PotionSignRoot/SignLabel").Text = Tr("home.start_game");
		buttonRoot.GetNode<Label>("PlantSignRoot/SignLabel").Text = Tr("home.planting");
		buttonRoot.GetNode<Label>("WarehouseSignRoot/SignLabel").Text = Tr("home.warehouse");
		buttonRoot.GetNode<Button>(StartGameButtonNodeName).Text = Tr("home.start_game");
		buttonRoot.GetNode<Button>(PlantingSignButtonNodeName).Text = Tr("home.planting_page");
		buttonRoot.GetNode<Button>(WarehouseSignButtonNodeName).Text = Tr("home.warehouse");
		_settingsButton.Text = Tr("home.settings");
	}

	private string Tr(string key)
	{
		return _localizationManager?.Tr(key) ?? LocalizationManager.GetText(key);
	}

	private string TrFormat(string key, params object[] args)
	{
		return _localizationManager?.TrFormat(key, args)
			?? string.Format(System.Globalization.CultureInfo.InvariantCulture, LocalizationManager.GetText(key), args);
	}

	private static void ApplySignButtonTheme(Button button)
	{
		Color textColor = new(0.18f, 0.1f, 0.04f);
		button.Icon = null;
		button.Visible = true;
		button.Flat = true;
		button.FocusMode = FocusModeEnum.None;
		button.AddThemeFontSizeOverride("font_size", 24);
		button.AddThemeColorOverride("font_color", textColor);
		button.AddThemeColorOverride("font_hover_color", textColor.Lightened(0.1f));
		button.AddThemeColorOverride("font_pressed_color", textColor.Darkened(0.1f));
		button.AddThemeColorOverride("font_focus_color", textColor);
		button.AddThemeColorOverride("font_disabled_color", new Color(textColor, 0.5f));
		button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("disabled", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
	}
	private static void ApplyWarehouseButtonTheme(Button button)
	{
		Color textColor = new(0.18f, 0.12f, 0.07f);
		button.AddThemeFontSizeOverride("font_size", 26);
		button.AddThemeColorOverride("font_color", textColor);
		button.AddThemeColorOverride("font_hover_color", textColor);
		button.AddThemeColorOverride("font_pressed_color", textColor);
		button.AddThemeColorOverride("font_focus_color", textColor);
		button.AddThemeColorOverride("font_disabled_color", new Color(textColor, 0.55f));
		button.AddThemeStyleboxOverride("normal", CreateWarehouseButtonStyle(new Color(0.96f, 0.82f, 0.48f, 0.94f), new Color(0.35f, 0.2f, 0.09f, 0.72f)));
		button.AddThemeStyleboxOverride("hover", CreateWarehouseButtonStyle(new Color(1f, 0.89f, 0.58f, 0.98f), new Color(0.42f, 0.25f, 0.1f, 0.82f)));
		button.AddThemeStyleboxOverride("pressed", CreateWarehouseButtonStyle(new Color(0.86f, 0.68f, 0.35f, 0.98f), new Color(0.3f, 0.17f, 0.07f, 0.86f)));
		button.AddThemeStyleboxOverride("disabled", CreateWarehouseButtonStyle(new Color(0.86f, 0.78f, 0.62f, 0.62f), new Color(0.32f, 0.22f, 0.12f, 0.4f)));
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
	}

	private static StyleBoxFlat CreateWarehouseButtonStyle(Color backgroundColor, Color borderColor)
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
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ShadowColor = new Color(0.18f, 0.11f, 0.06f, 0.2f),
			ShadowSize = 4,
			ShadowOffset = new Vector2(0f, 2f)
		};
	}
}
