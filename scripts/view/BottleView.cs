#nullable enable

using System;
using Godot;
using WaterSortGame.Model;

namespace WaterSortGame.View;

public sealed partial class BottleView : Area2D
{
    private const int Capacity = 4;

    private static readonly Color HiddenLayerColor = new(0f, 0f, 0f, 0.75f);

    private Polygon2D[] _layers = new Polygon2D[Capacity];
    private Label[] _questions = new Label[Capacity];
    private CollisionShape2D _collisionShape = null!;
    private bool _isCached;
    private bool _hasBasePosition;
    private bool _isSelected;
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

        for (int i = 0; i < Capacity; i++)
        {
            Polygon2D layerView = _layers[i];
            Label questionView = _questions[i];

            if (i >= data.Layers.Count)
            {
                layerView.Visible = false;
                questionView.Visible = false;
                continue;
            }

            layerView.Visible = true;
            WaterLayer layer = data.Layers[i];
            if (layer.IsRevealed)
            {
                layerView.Color = GetColor(layer.Color);
                questionView.Visible = false;
            }
            else
            {
                layerView.Color = HiddenLayerColor;
                questionView.Visible = true;
                questionView.Text = "?";
            }
        }
    }

    public void SetSelected(bool selected)
    {
        CacheBasePosition();
        _isSelected = selected;

        if (_invalidFeedbackTween == null || !_invalidFeedbackTween.IsRunning())
        {
            Position = GetTargetPosition();
        }
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

        for (int i = 0; i < Capacity; i++)
        {
            _layers[i] = GetNode<Polygon2D>($"LayerRoot/Layer_{i}");
            _questions[i] = GetNode<Label>($"LayerRoot/Question_{i}");
        }

        _isCached = true;
    }

    private Color GetColor(WaterColor color)
    {
        return color switch
        {
            WaterColor.Red => new Color(0.9f, 0.1f, 0.1f),
            WaterColor.Blue => new Color(0.1f, 0.35f, 1.0f),
            WaterColor.Yellow => new Color(1.0f, 0.85f, 0.1f),
            WaterColor.Green => new Color(0.1f, 0.8f, 0.25f),
            _ => new Color(1f, 1f, 1f)
        };
    }
}
