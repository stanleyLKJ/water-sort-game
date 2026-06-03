#nullable enable

using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;

namespace WaterSortGame.View;

public sealed partial class PourFxView : Node2D
{
    private const float Epsilon = 0.001f;

    private Line2D _streamLine = null!;
    private Line2D _highlightLine = null!;
    private Line2D _lipLine = null!;
    private Line2D _dropLine = null!;
    private Tween? _hideTween;
    private bool _streamActive;
    private Vector2 _streamStart;
    private Vector2 _streamEnd;
    private Color _streamColor;
    private bool _isGroundPour;
    private float _streamTime;

    public override void _Ready()
    {
        _streamLine = GetNodeOrNull<Line2D>("PourStreamLine") ?? CreateStreamLine("PourStreamLine", 5.8f, 20);
        _highlightLine = GetNodeOrNull<Line2D>("PourHighlightLine") ?? CreateStreamLine("PourHighlightLine", 1.4f, 22);
        _lipLine = GetNodeOrNull<Line2D>("PourLipLine") ?? CreateStreamLine("PourLipLine", 4.8f, 23);
        _dropLine = GetNodeOrNull<Line2D>("PourDropLine") ?? CreateStreamLine("PourDropLine", 2.2f, 21);
        HideStream();

        GameManager? gameManager = GetNode<Node>("../..").GetNodeOrNull<GameManager>("Managers/GameManager");
        if (gameManager == null)
        {
            GD.PushWarning("PourFxView could not find Managers/GameManager; pour stream FX will stay disabled.");
            return;
        }

        gameManager.TransferCommitted += OnTransferCommitted;
        gameManager.PouringStateChanged += OnPouringStateChanged;
    }

    public override void _Process(double delta)
    {
        if (!_streamActive)
        {
            return;
        }

        _streamTime += (float)delta;
        UpdateStreamGeometry();
    }

    private void OnTransferCommitted(
        int sourceBottleId,
        int targetBottleId,
        int moved,
        int color,
        Vector2 streamStartGlobal,
        Vector2 streamEndGlobal,
        bool isGroundPour)
    {
        if (moved <= Epsilon)
        {
            HideStream();
            return;
        }

        ShowStream(streamStartGlobal, streamEndGlobal, BottleView.GetColor((WaterColor)color), isGroundPour);
    }

    private void OnPouringStateChanged(string state, string reason)
    {
        if (state is "Blocked" or "Cancelled")
        {
            HideStream();
            return;
        }

        if (state == "StreamComplete")
        {
            HideStream();
        }
    }

    private Line2D CreateStreamLine(string nodeName, float width, int zIndex)
    {
        Line2D streamLine = new()
        {
            Name = nodeName,
            Visible = false,
            Width = width,
            ZIndex = zIndex,
            Antialiased = true
        };
        AddChild(streamLine);
        return streamLine;
    }

    private void ShowStream(Vector2 startGlobal, Vector2 endGlobal, Color streamColor, bool isGroundPour)
    {
        _hideTween?.Kill();
        _hideTween = null;

        _streamActive = true;
        _streamStart = ToLocal(startGlobal);
        _streamEnd = ToLocal(endGlobal);
        _streamColor = streamColor;
        _isGroundPour = isGroundPour;
        _streamTime = 0f;

        ConfigureStreamLines(streamColor, isGroundPour);
        UpdateStreamGeometry();
    }

    private void ConfigureStreamLines(Color streamColor, bool isGroundPour)
    {
        _streamLine.Antialiased = true;
        _streamLine.WidthCurve = CreateWidthCurve();
        _streamLine.Gradient = CreateBodyGradient(streamColor);
        _streamLine.DefaultColor = streamColor;
        _streamLine.Width = isGroundPour ? 4.2f : 6.4f;
        _streamLine.Modulate = Colors.White;
        _streamLine.Visible = true;

        _highlightLine.Antialiased = true;
        _highlightLine.WidthCurve = CreateHighlightWidthCurve();
        _highlightLine.Gradient = CreateHighlightGradient();
        _highlightLine.DefaultColor = Colors.White;
        _highlightLine.Width = isGroundPour ? 0.9f : 1.4f;
        _highlightLine.Modulate = Colors.White;
        _highlightLine.Visible = true;

        _lipLine.Antialiased = true;
        _lipLine.WidthCurve = CreateLipWidthCurve();
        _lipLine.Gradient = CreateBodyGradient(streamColor);
        _lipLine.DefaultColor = streamColor;
        _lipLine.Width = isGroundPour ? 3.4f : 4.8f;
        _lipLine.Modulate = Colors.White;
        _lipLine.Visible = true;

        _dropLine.Antialiased = true;
        _dropLine.WidthCurve = null;
        _dropLine.Gradient = CreateBodyGradient(streamColor);
        _dropLine.DefaultColor = streamColor;
        _dropLine.Width = isGroundPour ? 1.6f : 2.2f;
        _dropLine.Modulate = Colors.White;
        _dropLine.Visible = true;
    }

    private void UpdateStreamGeometry()
    {
        Vector2[] streamPoints = CreateBezierStreamPoints(_streamStart, _streamEnd, _isGroundPour, _streamTime);
        SetLinePoints(_streamLine, streamPoints);
        SetLinePoints(_highlightLine, CreateHighlightPoints(streamPoints));
        SetLinePoints(_lipLine, CreateLipPoints(_streamStart, _streamEnd, _isGroundPour, _streamTime));
        SetLinePoints(_dropLine, CreateDropPoints(streamPoints, _streamTime));
    }

    private void HideStream()
    {
        if (_streamLine == null)
        {
            return;
        }

        _hideTween?.Kill();
        _hideTween = null;
        _streamActive = false;
        ClearAndHide(_streamLine);
        ClearAndHide(_highlightLine);
        ClearAndHide(_lipLine);
        ClearAndHide(_dropLine);
    }

    private void FadeOutStream()
    {
        if (_streamLine == null || !_streamLine.Visible)
        {
            return;
        }

        _streamActive = false;
        _hideTween?.Kill();
        _hideTween = CreateTween();
        _hideTween.SetTrans(Tween.TransitionType.Sine);
        _hideTween.SetEase(Tween.EaseType.Out);
        _hideTween.TweenProperty(_streamLine, "width", 0.1f, 0.12);
        _hideTween.Parallel().TweenProperty(_streamLine, "modulate", new Color(1f, 1f, 1f, 0f), 0.12);
        _hideTween.Parallel().TweenProperty(_highlightLine, "width", 0.1f, 0.1);
        _hideTween.Parallel().TweenProperty(_highlightLine, "modulate", new Color(1f, 1f, 1f, 0f), 0.1);
        _hideTween.Parallel().TweenProperty(_lipLine, "width", 0.1f, 0.1);
        _hideTween.Parallel().TweenProperty(_lipLine, "modulate", new Color(1f, 1f, 1f, 0f), 0.1);
        _hideTween.Parallel().TweenProperty(_dropLine, "width", 0.1f, 0.14);
        _hideTween.Parallel().TweenProperty(_dropLine, "modulate", new Color(1f, 1f, 1f, 0f), 0.14);
        _hideTween.TweenCallback(Callable.From(HideStream));
    }

    private static Vector2[] CreateBezierStreamPoints(Vector2 start, Vector2 end, bool isGroundPour, float time)
    {
        Vector2 direction = end - start;
        float length = Mathf.Max(direction.Length(), 1f);
        Vector2 horizontal = new(Mathf.Sign(direction.X == 0f ? 1f : direction.X), 0f);
        Vector2 tangent = new Vector2(horizontal.X * 0.84f, 0.54f).Normalized();
        Vector2 control1 = start + tangent * Mathf.Clamp(length * 0.34f, 30f, 58f);
        Vector2 control2 = end - horizontal * Mathf.Clamp(length * 0.22f, 24f, 46f) + new Vector2(0f, -Mathf.Clamp(length * 0.32f, 34f, 58f));

        Vector2[] points = new Vector2[14];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)(points.Length - 1);
            Vector2 point = CubicBezier(start, control1, control2, end, t);
            if (i > 1 && i < points.Length - 2)
            {
                float wobble = Mathf.Sin(time * 16f + t * 8f) * (isGroundPour ? 0.4f : 0.9f);
                point += new Vector2(0f, wobble * Mathf.Sin(t * Mathf.Pi));
            }

            points[i] = point;
        }

        return points;
    }

    private static Vector2[] CreateLipPoints(Vector2 start, Vector2 end, bool isGroundPour, float time)
    {
        Vector2 direction = end - start;
        Vector2 horizontal = new(Mathf.Sign(direction.X == 0f ? 1f : direction.X), 0f);
        Vector2 tangent = new Vector2(horizontal.X * 0.86f, 0.5f).Normalized();
        float pulse = Mathf.Sin(time * 18f) * (isGroundPour ? 0.4f : 0.8f);
        return new[]
        {
            start,
            start + tangent * 8f + new Vector2(0f, pulse),
            start + tangent * 16f + new Vector2(0f, 2f + pulse * 0.4f)
        };
    }

    private static Vector2[] CreateHighlightPoints(Vector2[] streamPoints)
    {
        int pointCount = Mathf.Max(streamPoints.Length - 3, 2);
        Vector2[] highlightPoints = new Vector2[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            highlightPoints[i] = streamPoints[i + 1] + new Vector2(0f, -1.2f);
        }

        return highlightPoints;
    }

    private static Vector2[] CreateDropPoints(Vector2[] streamPoints, float time)
    {
        if (streamPoints.Length < 8)
        {
            return System.Array.Empty<Vector2>();
        }

        Vector2 basePoint = streamPoints[^4];
        float offset = Mathf.Sin(time * 11f) * 2f;
        return new[]
        {
            basePoint + new Vector2(5f, 7f + offset),
            basePoint + new Vector2(6.5f, 9.5f + offset)
        };
    }

    private static void SetLinePoints(Line2D line, Vector2[] points)
    {
        line.ClearPoints();
        foreach (Vector2 point in points)
        {
            line.AddPoint(point);
        }
    }

    private static void ClearAndHide(Line2D line)
    {
        line.Visible = false;
        line.ClearPoints();
        line.Modulate = Colors.White;
    }

    private static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float inv = 1f - t;
        return inv * inv * inv * p0
            + 3f * inv * inv * t * p1
            + 3f * inv * t * t * p2
            + t * t * t * p3;
    }

    private static Curve CreateWidthCurve()
    {
        Curve widthCurve = new();
        widthCurve.AddPoint(new Vector2(0f, 0.55f));
        widthCurve.AddPoint(new Vector2(0.16f, 1f));
        widthCurve.AddPoint(new Vector2(0.76f, 0.86f));
        widthCurve.AddPoint(new Vector2(1f, 0.2f));
        return widthCurve;
    }

    private static Curve CreateHighlightWidthCurve()
    {
        Curve widthCurve = new();
        widthCurve.AddPoint(new Vector2(0f, 0.2f));
        widthCurve.AddPoint(new Vector2(0.2f, 1f));
        widthCurve.AddPoint(new Vector2(0.82f, 0.65f));
        widthCurve.AddPoint(new Vector2(1f, 0.05f));
        return widthCurve;
    }

    private static Curve CreateLipWidthCurve()
    {
        Curve widthCurve = new();
        widthCurve.AddPoint(new Vector2(0f, 0.25f));
        widthCurve.AddPoint(new Vector2(0.45f, 1f));
        widthCurve.AddPoint(new Vector2(1f, 0.55f));
        return widthCurve;
    }

    private static Gradient CreateBodyGradient(Color streamColor)
    {
        Gradient gradient = new();
        gradient.SetColor(0, new Color(streamColor.R, streamColor.G, streamColor.B, 0.18f));
        gradient.SetColor(1, new Color(streamColor.R, streamColor.G, streamColor.B, 0.12f));
        gradient.AddPoint(0.14f, new Color(streamColor.R, streamColor.G, streamColor.B, 0.92f));
        gradient.AddPoint(0.76f, new Color(streamColor.R, streamColor.G, streamColor.B, 0.82f));
        return gradient;
    }

    private static Gradient CreateHighlightGradient()
    {
        Gradient gradient = new();
        gradient.SetColor(0, new Color(1f, 1f, 1f, 0.02f));
        gradient.SetColor(1, new Color(1f, 1f, 1f, 0.02f));
        gradient.AddPoint(0.18f, new Color(1f, 1f, 1f, 0.5f));
        gradient.AddPoint(0.68f, new Color(1f, 1f, 1f, 0.32f));
        return gradient;
    }
}
