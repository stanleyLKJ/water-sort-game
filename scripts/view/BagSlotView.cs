using Godot;
using WaterSortGame.Model;

namespace WaterSortGame.View;

public sealed partial class BagSlotView : Node2D
{
    private const float PotTargetHeight = 120f;

    public WaterColor BagColor { get; private set; }

    private Label _countLabel;
    private Sprite2D _potSprite;

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
                break;
            }
        }

        if (_countLabel == null)
        {
            GD.PushWarning($"{Name} has no Label child for bag count.");
        }
    }

    private void TryApplyBagColor()
    {
        EnsurePotSprite();
        Texture2D texture = GD.Load<Texture2D>(GetPotTexturePath(BagColor));
        _potSprite.Texture = texture;

        if (texture != null)
        {
            Vector2 size = texture.GetSize();
            float scale = size.Y > 0 ? PotTargetHeight / size.Y : 1f;
            _potSprite.Scale = new Vector2(scale, scale);
        }

        foreach (Node child in GetChildren())
        {
            if (child is Polygon2D polygon)
            {
                polygon.Visible = false;
            }
        }
    }

    private void EnsurePotSprite()
    {
        if (_potSprite != null)
        {
            return;
        }

        _potSprite = GetNodeOrNull<Sprite2D>("PotSprite");
        if (_potSprite == null)
        {
            _potSprite = new Sprite2D
            {
                Name = "PotSprite",
                Centered = true,
                Position = new Vector2(0, -12),
                ZIndex = -1
            };
            AddChild(_potSprite);
        }
    }

    private static string GetPotTexturePath(WaterColor color)
    {
        return color switch
        {
            WaterColor.Red => "res://art/pots/pot_red.png",
            WaterColor.Blue => "res://art/pots/pot_blue.png",
            WaterColor.Yellow => "res://art/pots/pot_yellow.png",
            WaterColor.Green => "res://art/pots/pot_green.png",
            _ => "res://art/pots/pot_red.png"
        };
    }
}
