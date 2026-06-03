#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using WaterSortGame.Model;

namespace WaterSortGame.View;

public sealed partial class BottleView : Area2D
{
    private const int Capacity = 4;
    private const float BottleTargetHeight = 250f;
    private const float WaterBottomY = 101f;
    private const float WaterTopY = -50f;
    private const float LiquidRectHalfWidth = 56f;
    private const float WaterBottomHalfWidth = 30f;
    private const float WaterBodyHalfWidth = 36f;
    private const float WaterTopHalfWidth = 22f;
    private const float WaterSurfaceWave = 1.4f;
    private const float LayerGap = 0.08f;
    private const float LayerBorderWidth = 1.25f;
    private const float WaterLevelAnimationSeconds = 0.36f;
    private const string BottleTexturePath = "res://assets/bottles/bottle_new.png";
    private const string LiquidMaskTexturePath = "res://assets/bottles/bottle_liquid_mask.png";
    private const int LiquidTextureWidth = 288;
    private const int LiquidTextureHeight = 512;
    private const int LiquidTopPixel = 142;
    private const int LiquidBottomPixel = 470;

    private static readonly Color HiddenLayerColor = new(0.05f, 0.07f, 0.09f, 0.64f);

    private Polygon2D[] _layers = new Polygon2D[Capacity];
    private Line2D[] _layerBorders = new Line2D[Capacity];
    private Polygon2D[] _layerSheens = new Polygon2D[Capacity];
    private Label[] _sparkles = new Label[Capacity];
    private Label[] _questions = new Label[Capacity];
    private readonly List<WaterLayer> _previewLayers = new();
    private List<WaterLayer>? _animationLayers;
    private CollisionShape2D _collisionShape = null!;
    private Line2D? _bottleFrame;
    private Sprite2D? _bottleSprite;
    private Sprite2D? _bottleBackSprite;
    private Sprite2D? _liquidSprite;
    private Polygon2D? _waterClip;
    private Node2D? _waterContainer;
    private Node2D? _pourPoint;
    private Image? _liquidMaskImage;
    private BottleData? _currentData;
    private bool _isCached;
    private bool _hasBasePosition;
    private bool _isSelected;
    private bool _isPourAnimating;
    private float _visualFillLevel;
    private Vector2 _basePosition;
    private Tween? _invalidFeedbackTween;

    public int BottleId { get; private set; }

    public event Action<int>? Clicked;

    public override void _Ready()
    {
        CacheBasePosition();
        CacheNodes();
    }

    public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
    {
        if (@event is not InputEventMouseButton mouseButton)
        {
            return;
        }

        if (mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed)
        {
            return;
        }

        Clicked?.Invoke(BottleId);
    }

    public void Bind(int bottleId)
    {
        CacheBasePosition();
        BottleId = bottleId;
    }

    public void Refresh(BottleData data)
    {
        CacheBasePosition();
        CacheNodes();

        if (data == null || data.IsCollected)
        {
            _invalidFeedbackTween?.Kill();
            _invalidFeedbackTween = null;
            _isSelected = false;
            Position = _basePosition;
            Visible = false;
            _collisionShape.Disabled = true;
            return;
        }

        Visible = true;
        _collisionShape.Disabled = false;
        _currentData = data;
        _previewLayers.Clear();
        _animationLayers = null;
        _visualFillLevel = data.Layers.Count;

        RenderWaterLayers();
    }

    public void SetSelected(bool selected)
    {
        CacheBasePosition();
        _isSelected = selected;

        if (!_isPourAnimating && (_invalidFeedbackTween == null || !_invalidFeedbackTween.IsRunning()))
        {
            Position = GetTargetPosition();
        }
    }

    public Vector2 GetPourPointGlobalPosition()
    {
        CacheNodes();
        return _pourPoint?.GlobalPosition ?? GlobalPosition + new Vector2(36, -120);
    }

    public Vector2 GetPourLipGlobalPosition(bool poursRight)
    {
        return ToGlobal(GetPourLipLocalPosition(poursRight));
    }

    public Vector2 GetReceivingPointGlobalPosition()
    {
        return ToGlobal(new Vector2(0f, -101f));
    }

    public async Task PlayPourAnimationTo(
        BottleView targetView,
        WaterColor waterColor,
        int amount,
        Action<Vector2, Vector2> onTransferData,
        Action? onPourVisualComplete = null)
    {
        CacheNodes();
        targetView.CacheNodes();

        Vector2 originalGlobalPosition = GlobalPosition;
        float originalRotation = Rotation;
        bool sourceStartsLeft = GlobalPosition.X <= targetView.GlobalPosition.X;
        float pourRotation = sourceStartsLeft ? Mathf.DegToRad(50f) : Mathf.DegToRad(-50f);
        Vector2 sourcePourLocal = GetPourLipLocalPosition(sourceStartsLeft);
        Vector2 targetMouth = targetView.GetReceivingPointGlobalPosition() + new Vector2(0f, 10f);
        Vector2 desiredSpout = targetMouth + (sourceStartsLeft ? new Vector2(-64f, -56f) : new Vector2(64f, -56f));
        Vector2 pourGlobalPosition = desiredSpout - sourcePourLocal.Rotated(pourRotation);

        _isPourAnimating = true;
        _invalidFeedbackTween?.Kill();
        _invalidFeedbackTween = null;

        try
        {
            Tween moveTween = CreateTween();
            moveTween.SetTrans(Tween.TransitionType.Sine);
            moveTween.SetEase(Tween.EaseType.Out);
            moveTween.TweenProperty(this, "global_position", pourGlobalPosition, 0.22);
            moveTween.Parallel().TweenProperty(this, "rotation", pourRotation, 0.22);
            await ToSignal(moveTween, Tween.SignalName.Finished);

            BeginOutgoingPreview();
            float sourceStartFill = _visualFillLevel;
            float sourceEndFill = Mathf.Max(0f, sourceStartFill - amount);
            float targetStartFill = targetView._visualFillLevel;
            float targetEndFill = Mathf.Min(Capacity, targetStartFill + amount);
            targetView.BeginIncomingPreview(waterColor, amount);

            Task sourceLevelTask = AnimateVisualFillLevelAsync(sourceStartFill, sourceEndFill);
            Task targetLevelTask = targetView.AnimateVisualFillLevelAsync(targetStartFill, targetEndFill);

            SceneTreeTimer transferTimer = GetTree().CreateTimer(0.08);
            await ToSignal(transferTimer, SceneTreeTimer.SignalName.Timeout);
            onTransferData?.Invoke(
                GetPourLipGlobalPosition(sourceStartsLeft),
                targetView.GetReceivingPointGlobalPosition() + new Vector2(0f, 10f));

            await Task.WhenAll(sourceLevelTask, targetLevelTask);
            onPourVisualComplete?.Invoke();

            Tween returnTween = CreateTween();
            returnTween.SetTrans(Tween.TransitionType.Sine);
            returnTween.SetEase(Tween.EaseType.InOut);
            returnTween.TweenProperty(this, "rotation", originalRotation, 0.16);
            returnTween.Parallel().TweenProperty(this, "global_position", originalGlobalPosition, 0.2);
            await ToSignal(returnTween, Tween.SignalName.Finished);
        }
        finally
        {
            Rotation = originalRotation;
            GlobalPosition = originalGlobalPosition;
            _isPourAnimating = false;
            Position = GetTargetPosition();
        }
    }

    private static Vector2 GetPourLipLocalPosition(bool poursRight)
    {
        return new Vector2(poursRight ? -14f : 14f, -115f);
    }

    public void PlayInvalidFeedback()
    {
        CacheBasePosition();
        _invalidFeedbackTween?.Kill();

        Vector2 targetPosition = GetTargetPosition();
        Position = targetPosition;

        _invalidFeedbackTween = CreateTween();
        _invalidFeedbackTween.TweenProperty(this, "position", targetPosition + new Vector2(-10, 0), 0.05);
        _invalidFeedbackTween.TweenProperty(this, "position", targetPosition + new Vector2(10, 0), 0.1);
        _invalidFeedbackTween.TweenProperty(this, "position", targetPosition + new Vector2(-6, 0), 0.08);
        _invalidFeedbackTween.TweenProperty(this, "position", targetPosition, 0.06);
        _invalidFeedbackTween.TweenCallback(Callable.From(() => Position = GetTargetPosition()));
    }

    private Vector2 GetTargetPosition()
    {
        return _isSelected ? _basePosition + new Vector2(0, -30) : _basePosition;
    }

    private void CacheBasePosition()
    {
        if (_hasBasePosition)
        {
            return;
        }

        _basePosition = Position;
        _hasBasePosition = true;
    }

    private void CacheNodes()
    {
        if (_isCached)
        {
            return;
        }

        _collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
        _bottleFrame = GetNodeOrNull<Line2D>("BottleFrame");
        if (_bottleFrame != null)
        {
            _bottleFrame.Visible = false;
        }

        _waterContainer = GetNodeOrNull<Node2D>("WaterContainer") ?? GetNodeOrNull<Node2D>("LayerRoot");
        EnsureWaterClip();
        EnsureBottleSprite();
        EnsureLiquidSprite();
        EnsureLiquidMaskImage();
        EnsurePourPoint();

        for (int i = 0; i < Capacity; i++)
        {
            _layers[i] = _waterContainer!.GetNode<Polygon2D>($"Layer_{i}");
            _questions[i] = _waterContainer.GetNode<Label>($"Question_{i}");
            _layers[i].Position = Vector2.Zero;
            _layers[i].Rotation = 0f;
            _layers[i].Scale = Vector2.One;
            _layers[i].ZIndex = 1;
            _layers[i].Material = null;
            _layers[i].Visible = false;
            _questions[i].Position = Vector2.Zero;
            _questions[i].Rotation = 0f;
            _questions[i].Scale = Vector2.One;
            _questions[i].ZIndex = 6;
            EnsureLayerDecorationNodes(i);
        }

        _isCached = true;
    }

    private void EnsureBottleSprite()
    {
        Texture2D texture = GD.Load<Texture2D>(BottleTexturePath);
        float scale = 1f;
        if (texture != null)
        {
            Vector2 size = texture.GetSize();
            scale = size.Y > 0 ? BottleTargetHeight / size.Y : 1f;
        }

        if (_bottleBackSprite == null)
        {
            _bottleBackSprite = GetNodeOrNull<Sprite2D>("BottleBack");
        }

        if (_bottleBackSprite == null)
        {
            _bottleBackSprite = new Sprite2D
            {
                Name = "BottleBack",
                Centered = true,
                Position = Vector2.Zero,
                ZIndex = 0
            };
            AddChild(_bottleBackSprite);
        }

        _bottleBackSprite.Texture = texture;
        _bottleBackSprite.Scale = new Vector2(scale, scale);
        _bottleBackSprite.Modulate = new Color(1f, 1f, 1f, 0.36f);
        _bottleBackSprite.ZIndex = 0;

        if (_bottleSprite == null)
        {
            _bottleSprite = GetNodeOrNull<Sprite2D>("BottleFront")
                ?? GetNodeOrNull<Sprite2D>("BottleSprite")
                ?? GetNodeOrNull<Sprite2D>("EmptyBottleSprite");
        }

        if (_bottleSprite == null)
        {
            _bottleSprite = new Sprite2D
            {
                Name = "BottleFront",
                Centered = true,
                Position = Vector2.Zero,
                ZIndex = 5
            };
            AddChild(_bottleSprite);
        }

        _bottleSprite.Name = "BottleFront";
        _bottleSprite.ZIndex = 5;
        _bottleSprite.Texture = texture;
        _bottleSprite.Scale = new Vector2(scale, scale);
    }

    private void EnsureLiquidSprite()
    {
        if (_liquidSprite == null)
        {
            _liquidSprite = GetNodeOrNull<Sprite2D>("LiquidSprite");
        }

        if (_liquidSprite == null)
        {
            _liquidSprite = new Sprite2D
            {
                Name = "LiquidSprite",
                Centered = true,
                Position = Vector2.Zero,
                ZIndex = 3
            };
            AddChild(_liquidSprite);
        }

        _liquidSprite.ZIndex = 3;
        _liquidSprite.Scale = Vector2.One * (BottleTargetHeight / LiquidTextureHeight);
    }

    private void EnsureLiquidMaskImage()
    {
        if (_liquidMaskImage != null)
        {
            return;
        }

        string absolutePath = ProjectSettings.GlobalizePath(LiquidMaskTexturePath);
        Image? image = FileAccess.FileExists(absolutePath)
            ? Image.LoadFromFile(absolutePath)
            : null;

        if (image == null || image.IsEmpty())
        {
            _liquidMaskImage = CreateFallbackLiquidMaskImage();
            return;
        }

        image.Convert(Image.Format.Rgba8);
        image.Resize(LiquidTextureWidth, LiquidTextureHeight, Image.Interpolation.Bilinear);
        _liquidMaskImage = image;
    }

    private static Image CreateFallbackLiquidMaskImage()
    {
        Image image = Image.CreateEmpty(LiquidTextureWidth, LiquidTextureHeight, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);

        int centerX = LiquidTextureWidth / 2;
        int halfWidth = 33;
        for (int y = LiquidTopPixel; y <= LiquidBottomPixel; y++)
        {
            float t = Mathf.InverseLerp(LiquidTopPixel, LiquidBottomPixel, y);
            int rowHalfWidth = Mathf.RoundToInt(Mathf.Lerp(22f, halfWidth, Mathf.Sin(t * Mathf.Pi)));
            for (int x = centerX - rowHalfWidth; x <= centerX + rowHalfWidth; x++)
            {
                image.SetPixel(x, y, Colors.White);
            }
        }

        return image;
    }

    private void EnsureWaterClip()
    {
        if (_waterContainer == null)
        {
            return;
        }

        _waterClip = GetNodeOrNull<Polygon2D>("WaterClip");
        if (_waterClip == null)
        {
            _waterClip = new Polygon2D
            {
                Name = "WaterClip",
                Color = Colors.White,
                ZIndex = 0,
                ClipChildren = CanvasItem.ClipChildrenMode.Only,
                Antialiased = true
            };
            AddChild(_waterClip);
            MoveChild(_waterClip, 0);
        }

        _waterClip.Polygon = CreateClipPolygon();
        _waterClip.ClipChildren = CanvasItem.ClipChildrenMode.Only;
        _waterClip.ZIndex = 2;
        _waterClip.Color = new Color(1f, 1f, 1f, 0.001f);

        if (_waterContainer.GetParent() != _waterClip)
        {
            _waterContainer.GetParent()?.RemoveChild(_waterContainer);
            _waterClip.AddChild(_waterContainer);
            _waterContainer.Position = Vector2.Zero;
        }

        _waterContainer.ZIndex = 1;
    }

    private void EnsureLayerDecorationNodes(int index)
    {
        _layerBorders[index] = _waterContainer!.GetNodeOrNull<Line2D>($"LayerBorder_{index}") ?? new Line2D
        {
            Name = $"LayerBorder_{index}"
        };
        if (_layerBorders[index].GetParent() == null)
        {
            _waterContainer.AddChild(_layerBorders[index]);
        }

        _layerBorders[index].ZIndex = 4;
        _layerBorders[index].Width = LayerBorderWidth;
        _layerBorders[index].Material = null;
        _layerBorders[index].Antialiased = true;
        _layerBorders[index].JointMode = Line2D.LineJointMode.Round;
        _layerBorders[index].BeginCapMode = Line2D.LineCapMode.Round;
        _layerBorders[index].EndCapMode = Line2D.LineCapMode.Round;

        _layerSheens[index] = _waterContainer.GetNodeOrNull<Polygon2D>($"LayerSheen_{index}") ?? new Polygon2D
        {
            Name = $"LayerSheen_{index}"
        };
        if (_layerSheens[index].GetParent() == null)
        {
            _waterContainer.AddChild(_layerSheens[index]);
        }

        _layerSheens[index].ZIndex = 3;
        _layerSheens[index].Color = new Color(1f, 1f, 1f, 0.16f);
        _layerSheens[index].Material = null;
        _layerSheens[index].Antialiased = true;

        _sparkles[index] = _waterContainer.GetNodeOrNull<Label>($"Sparkle_{index}") ?? new Label
        {
            Name = $"Sparkle_{index}"
        };
        if (_sparkles[index].GetParent() == null)
        {
            _waterContainer.AddChild(_sparkles[index]);
        }

        _sparkles[index].ZIndex = 5;
        _sparkles[index].Text = string.Empty;
        _sparkles[index].HorizontalAlignment = HorizontalAlignment.Center;
        _sparkles[index].VerticalAlignment = VerticalAlignment.Center;
        _sparkles[index].Modulate = new Color(1f, 1f, 1f, 0.9f);
    }

    private void EnsurePourPoint()
    {
        if (_pourPoint == null)
        {
            _pourPoint = GetNodeOrNull<Node2D>("PourPoint");
        }

        if (_pourPoint != null)
        {
            return;
        }

        _pourPoint = new Marker2D
        {
            Name = "PourPoint",
            Position = new Vector2(28, -118),
            Visible = false
        };
        AddChild(_pourPoint);
    }

    private void BeginIncomingPreview(WaterColor color, int amount)
    {
        _previewLayers.Clear();
        _animationLayers = CopyCurrentLayers();
        for (int i = 0; i < amount; i++)
        {
            WaterLayer incomingLayer = new(color, true);
            _previewLayers.Add(incomingLayer);
            _animationLayers.Add(incomingLayer);
        }

        RenderWaterLayers();
    }

    private void BeginOutgoingPreview()
    {
        _previewLayers.Clear();
        _animationLayers = CopyCurrentLayers();
        RenderWaterLayers();
    }

    private async Task AnimateVisualFillLevelAsync(float startFill, float endFill)
    {
        double elapsed = 0d;

        while (elapsed < WaterLevelAnimationSeconds)
        {
            float progress = Mathf.Clamp((float)(elapsed / WaterLevelAnimationSeconds), 0f, 1f);
            progress = EaseInOutSine(progress);
            _visualFillLevel = Mathf.Lerp(startFill, endFill, progress);
            RenderWaterLayers();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += GetProcessDeltaTime();
        }

        _visualFillLevel = endFill;
        RenderWaterLayers();
    }

    private void RenderWaterLayers()
    {
        if (_currentData == null)
        {
            HideAllWaterLayers();
            return;
        }

        List<WaterLayer> renderLayers = GetRenderLayers();
        float fillLevel = Mathf.Clamp(_visualFillLevel, 0f, Capacity);
        RenderLiquidTexture(renderLayers, fillLevel);
        int topVisibleIndex = Mathf.Clamp(Mathf.CeilToInt(fillLevel) - 1, 0, Capacity - 1);

        for (int i = 0; i < Capacity; i++)
        {
            Polygon2D layerView = _layers[i];
            Label questionView = _questions[i];

            float visiblePart = Mathf.Clamp(fillLevel - i, 0f, 1f);
            if (i >= renderLayers.Count || visiblePart <= 0.001f)
            {
                layerView.Visible = false;
                questionView.Visible = false;
                _layerBorders[i].Visible = false;
                _layerSheens[i].Visible = false;
                _sparkles[i].Visible = false;
                continue;
            }

            WaterLayer layer = renderLayers[i];
            bool isTopSurface = i == topVisibleIndex;
            layerView.Visible = false;
            Color fillColor = layer.IsRevealed ? GetColor(layer.Color) : HiddenLayerColor;
            layerView.Color = layer.IsRevealed
                ? new Color(fillColor.R, fillColor.G, fillColor.B, 0.82f)
                : HiddenLayerColor;
            Vector2[] layerPolygon = CreateWaterPolygon(i, visiblePart, isTopSurface);
            layerView.Polygon = layerPolygon;
            layerView.Antialiased = true;
            RenderLayerDecorations(i, layer, layerPolygon, visiblePart, isTopSurface);
            _layerBorders[i].Visible = false;
            _layerSheens[i].Visible = false;

            questionView.Visible = !layer.IsRevealed && visiblePart > 0.35f;
            if (questionView.Visible)
            {
                PositionQuestionLabel(questionView, i, visiblePart);
            }
        }
    }

    private List<WaterLayer> GetRenderLayers()
    {
        List<WaterLayer> renderLayers = _animationLayers != null
            ? new List<WaterLayer>(_animationLayers)
            : _currentData == null
            ? new List<WaterLayer>()
            : new List<WaterLayer>(_currentData.Layers);
        if (_animationLayers == null)
        {
            renderLayers.AddRange(_previewLayers);
        }

        if (renderLayers.Count > Capacity)
        {
            renderLayers.RemoveRange(Capacity, renderLayers.Count - Capacity);
        }

        return renderLayers;
    }

    private void RenderLiquidTexture(List<WaterLayer> renderLayers, float fillLevel)
    {
        EnsureLiquidMaskImage();
        if (_liquidSprite == null || _liquidMaskImage == null)
        {
            return;
        }

        Image liquidImage = Image.CreateEmpty(LiquidTextureWidth, LiquidTextureHeight, false, Image.Format.Rgba8);
        liquidImage.Fill(Colors.Transparent);

        float layerHeight = (LiquidBottomPixel - LiquidTopPixel) / (float)Capacity;
        for (int i = 0; i < Capacity; i++)
        {
            float visiblePart = Mathf.Clamp(fillLevel - i, 0f, 1f);
            if (i >= renderLayers.Count || visiblePart <= 0.001f)
            {
                continue;
            }

            WaterLayer layer = renderLayers[i];
            Color color = layer.IsRevealed
                ? new Color(GetColor(layer.Color), 0.82f)
                : HiddenLayerColor;

            int yBottom = Mathf.RoundToInt(LiquidBottomPixel - i * layerHeight);
            int yTop = Mathf.RoundToInt(LiquidBottomPixel - (i + visiblePart) * layerHeight);
            yTop = Mathf.Clamp(yTop, 0, LiquidTextureHeight - 1);
            yBottom = Mathf.Clamp(yBottom, 0, LiquidTextureHeight - 1);
            FillLiquidBand(liquidImage, yTop, yBottom, color);
        }

        ApplyLiquidMask(liquidImage, _liquidMaskImage);
        _liquidSprite.Texture = ImageTexture.CreateFromImage(liquidImage);
        _liquidSprite.Visible = true;
    }

    private static void FillLiquidBand(Image image, int yTop, int yBottom, Color color)
    {
        for (int y = yTop; y <= yBottom; y++)
        {
            if (y < 0 || y >= LiquidTextureHeight)
            {
                continue;
            }

            for (int x = 0; x < LiquidTextureWidth; x++)
            {
                image.SetPixel(x, y, color);
            }
        }
    }

    private static void DrawLiquidSurface(Image image, int y, Color color, bool revealed)
    {
        if (y < 2 || y >= LiquidTextureHeight - 2)
        {
            return;
        }

        if (!revealed)
        {
            return;
        }

        Color highlightBase = new(
            Mathf.Lerp(color.R, 1f, 0.36f),
            Mathf.Lerp(color.G, 1f, 0.36f),
            Mathf.Lerp(color.B, 1f, 0.36f),
            0.1f);
        int centerX = LiquidTextureWidth / 2;
        int halfWidth = 54;
        for (int dx = -halfWidth; dx <= halfWidth; dx++)
        {
            int x = centerX + dx;
            if (x < 0 || x >= LiquidTextureWidth)
            {
                continue;
            }

            float edgeFade = 1f - Mathf.Pow(Mathf.Abs(dx) / (float)halfWidth, 2.2f);
            Color highlight = new(highlightBase.R, highlightBase.G, highlightBase.B, highlightBase.A * edgeFade);
            int waveY = y + Mathf.RoundToInt(Mathf.Sin(dx * 0.062f) * 0.55f);
            if (waveY >= 0 && waveY < LiquidTextureHeight)
            {
                image.SetPixel(x, waveY, highlight);
            }
        }
    }

    private static void ApplyLiquidMask(Image liquidImage, Image maskImage)
    {
        for (int y = 0; y < LiquidTextureHeight; y++)
        {
            for (int x = 0; x < LiquidTextureWidth; x++)
            {
                Color liquid = liquidImage.GetPixel(x, y);
                if (liquid.A <= 0.001f)
                {
                    continue;
                }

                float maskAlpha = maskImage.GetPixel(x, y).A;
                if (maskAlpha <= 0.001f)
                {
                    liquidImage.SetPixel(x, y, Colors.Transparent);
                    continue;
                }

                liquid.A *= maskAlpha;
                liquidImage.SetPixel(x, y, liquid);
            }
        }
    }

    private List<WaterLayer> CopyCurrentLayers()
    {
        List<WaterLayer> layers = new();
        if (_currentData == null)
        {
            return layers;
        }

        foreach (WaterLayer layer in _currentData.Layers)
        {
            layers.Add(new WaterLayer(layer.Color, layer.IsRevealed));
        }

        return layers;
    }

    private Vector2[] CreateWaterPolygon(int layerIndex, float visiblePart, bool isTopSurface)
    {
        float gapFill = LayerGap / (WaterBottomY - WaterTopY);
        float lowerFill = Mathf.Max(0f, layerIndex + gapFill);
        float upperFill = Mathf.Min(Capacity, Mathf.Max(lowerFill + 0.05f, layerIndex + visiblePart - gapFill));
        float bottomY = FillLevelToY(lowerFill);
        float topY = FillLevelToY(upperFill);

        if (!isTopSurface || visiblePart < 0.2f)
        {
            return new[]
            {
                new Vector2(-LiquidRectHalfWidth, bottomY),
                new Vector2(LiquidRectHalfWidth, bottomY),
                new Vector2(LiquidRectHalfWidth, topY),
                new Vector2(-LiquidRectHalfWidth, topY)
            };
        }

        float wave = WaterSurfaceWave * Mathf.Min(1f, visiblePart * 2f);
        return new[]
        {
            new Vector2(-LiquidRectHalfWidth, bottomY),
            new Vector2(LiquidRectHalfWidth, bottomY),
            new Vector2(LiquidRectHalfWidth, topY + 0.5f),
            new Vector2(LiquidRectHalfWidth * 0.35f, topY - wave * 0.45f),
            new Vector2(-LiquidRectHalfWidth * 0.35f, topY + wave * 0.45f),
            new Vector2(-LiquidRectHalfWidth, topY + 0.5f)
        };
    }

    private static Vector2[] CreateSheenPolygon(int layerIndex, float visiblePart, bool isTopSurface)
    {
        float gapFill = LayerGap / (WaterBottomY - WaterTopY);
        float lowerFill = layerIndex + gapFill;
        float upperFill = Mathf.Max(lowerFill + 0.05f, layerIndex + visiblePart - gapFill);
        float topY = FillLevelToY(upperFill);
        float bottomY = Mathf.Lerp(topY, FillLevelToY(lowerFill), 0.22f);
        float width = HalfWidthAtFillLevel(upperFill) * 0.62f;
        float wave = isTopSurface ? WaterSurfaceWave : 0f;

        return new[]
        {
            new Vector2(-width, bottomY),
            new Vector2(width, bottomY - 1f),
            new Vector2(width * 0.88f, topY + 2.4f),
            new Vector2(width * 0.25f, topY - wave * 0.35f),
            new Vector2(-width * 0.35f, topY + wave * 0.25f),
            new Vector2(-width * 0.88f, topY + 2.4f)
        };
    }

    private static Vector2[] CreateClipPolygon()
    {
        return new[]
        {
            new Vector2(-72f, -126f),
            new Vector2(72f, -126f),
            new Vector2(72f, 126f),
            new Vector2(-72f, 126f)
        };
    }

    private void PositionQuestionLabel(Label questionView, int layerIndex, float visiblePart)
    {
        float gapFill = LayerGap / (WaterBottomY - WaterTopY);
        float lowerFill = layerIndex + gapFill;
        float upperFill = Mathf.Max(lowerFill + 0.05f, layerIndex + visiblePart - gapFill);
        float centerY = (FillLevelToY(lowerFill) + FillLevelToY(upperFill)) * 0.5f;

        questionView.Text = "?";
        questionView.HorizontalAlignment = HorizontalAlignment.Center;
        questionView.VerticalAlignment = VerticalAlignment.Center;
        questionView.OffsetLeft = -18f;
        questionView.OffsetRight = 18f;
        questionView.OffsetTop = centerY - 14f;
        questionView.OffsetBottom = centerY + 14f;
        questionView.AddThemeColorOverride("font_color", Colors.White);
        questionView.AddThemeFontSizeOverride("font_size", 24);
    }

    private void HideAllWaterLayers()
    {
        if (_liquidSprite != null)
        {
            _liquidSprite.Visible = false;
            _liquidSprite.Texture = null;
        }

        for (int i = 0; i < Capacity; i++)
        {
            _layers[i].Visible = false;
            _questions[i].Visible = false;
            _layerBorders[i].Visible = false;
            _layerSheens[i].Visible = false;
            _sparkles[i].Visible = false;
        }
    }

    private void RenderLayerDecorations(
        int index,
        WaterLayer layer,
        Vector2[] layerPolygon,
        float visiblePart,
        bool isTopSurface)
    {
        float gapFill = LayerGap / (WaterBottomY - WaterTopY);
        float lowerFill = index + gapFill;
        float upperFill = Mathf.Max(lowerFill + 0.05f, index + visiblePart - gapFill);
        float centerY = (FillLevelToY(lowerFill) + FillLevelToY(upperFill)) * 0.5f;
        float halfWidth = HalfWidthAtFillLevel(index + visiblePart * 0.5f);
        float topWidth = HalfWidthAtFillLevel(upperFill);
        float topY = FillLevelToY(upperFill);

        _layerBorders[index].Visible = true;
        _layerBorders[index].DefaultColor = layer.IsRevealed
            ? new Color(1f, 1f, 1f, isTopSurface ? 0.48f : 0.22f)
            : new Color(0.75f, 0.84f, 0.88f, 0.24f);
        _layerBorders[index].ClearPoints();
        _layerBorders[index].AddPoint(new Vector2(-topWidth, topY + 0.6f));
        _layerBorders[index].AddPoint(new Vector2(-topWidth * 0.35f, topY + (isTopSurface ? WaterSurfaceWave * 0.25f : 0.2f)));
        _layerBorders[index].AddPoint(new Vector2(topWidth * 0.35f, topY - (isTopSurface ? WaterSurfaceWave * 0.2f : 0.2f)));
        _layerBorders[index].AddPoint(new Vector2(topWidth, topY + 0.6f));

        _layerSheens[index].Visible = layer.IsRevealed && visiblePart > 0.45f && isTopSurface;
        if (_layerSheens[index].Visible)
        {
            _layerSheens[index].Polygon = CreateSheenPolygon(index, visiblePart, isTopSurface);
        }

        _sparkles[index].Visible = false;
        if (_sparkles[index].Visible)
        {
            _sparkles[index].OffsetLeft = -halfWidth * 0.55f;
            _sparkles[index].OffsetRight = -halfWidth * 0.55f + 16f;
            _sparkles[index].OffsetTop = centerY - 11f;
            _sparkles[index].OffsetBottom = centerY + 11f;
            _sparkles[index].Scale = Vector2.One * (index % 2 == 0 ? 1.05f : 0.82f);
        }
    }

    private static float FillLevelToY(float fillLevel)
    {
        float normalized = Mathf.Clamp(fillLevel / Capacity, 0f, 1f);
        return Mathf.Lerp(WaterBottomY, WaterTopY, normalized);
    }

    private static float HalfWidthAtFillLevel(float fillLevel)
    {
        float normalized = Mathf.Clamp(fillLevel / Capacity, 0f, 1f);
        if (normalized < 0.18f)
        {
            return Mathf.Lerp(WaterBottomHalfWidth, WaterBodyHalfWidth, normalized / 0.18f);
        }

        if (normalized < 0.72f)
        {
            return WaterBodyHalfWidth;
        }

        return Mathf.Lerp(WaterBodyHalfWidth, WaterTopHalfWidth, (normalized - 0.72f) / 0.28f);
    }

    private static float EaseInOutSine(float value)
    {
        return -(Mathf.Cos(Mathf.Pi * value) - 1f) * 0.5f;
    }

    public static Color GetColor(WaterColor color)
    {
        return color switch
        {
            WaterColor.Red => new Color(0.9f, 0.24f, 0.32f),
            WaterColor.Blue => new Color(0.2f, 0.52f, 0.95f),
            WaterColor.Yellow => new Color(1.0f, 0.76f, 0.22f),
            WaterColor.Green => new Color(0.38f, 0.75f, 0.32f),
            _ => new Color(1f, 1f, 1f)
        };
    }
}
