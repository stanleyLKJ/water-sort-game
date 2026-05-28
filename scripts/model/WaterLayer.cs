namespace WaterSortGame.Model;

public sealed class WaterLayer
{
    public WaterColor Color { get; set; }
    public bool IsRevealed { get; set; }

    public WaterLayer(WaterColor color, bool isRevealed)
    {
        Color = color;
        IsRevealed = isRevealed;
    }
}
