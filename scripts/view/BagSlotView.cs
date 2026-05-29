using Godot;
using WaterSortGame.Model;

namespace WaterSortGame.View;

public sealed partial class BagSlotView : Node2D
{
    public WaterColor BagColor { get; private set; }

    private Label _countLabel;

    public override void _Ready()
    {
        CacheNodes();
        TryApplyBagColor();
    }

    public void Bind(WaterColor color)
    {
        BagColor = color;
        TryApplyBagColor();
    }

    public void Refresh(BagData data)
    {
        if (data == null)
        {
            return;
        }

        CacheNodes();
        if (_countLabel == null)
        {
            GD.PushWarning($"{Name} cannot refresh bag count because no Label child was found.");
            return;
        }

        _countLabel.Text = data.CollectedCount.ToString();
    }

    private void CacheNodes()
    {
        if (_countLabel != null)
        {
            return;
        }

        _countLabel = GetNodeOrNull<Label>("CountLabel");
        if (_countLabel != null)
        {
            return;
        }

        _countLabel = GetNodeOrNull<Label>("Label");
        if (_countLabel != null)
        {
            return;
        }

        foreach (Node child in GetChildren())
        {
            if (child is Label label)
            {
                _countLabel = label;
                return;
            }
        }

        GD.PushWarning($"{Name} has no Label child for bag count.");
    }

    private void TryApplyBagColor()
    {
        foreach (Node child in GetChildren())
        {
            if (child is Polygon2D polygon)
            {
                polygon.Color = GetColor(BagColor);
                return;
            }
        }
    }

    private static Color GetColor(WaterColor color)
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
