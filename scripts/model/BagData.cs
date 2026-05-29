namespace WaterSortGame.Model;

public sealed class BagData
{
    public WaterColor Color { get; set; }

    public int CollectedCount { get; set; }

    public BagData(WaterColor color)
    {
        Color = color;
    }
}
