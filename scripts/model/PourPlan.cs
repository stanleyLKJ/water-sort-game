namespace WaterSortGame.Model;

public sealed class PourPlan
{
    public int SourceBottleId { get; init; }
    public int TargetBottleId { get; init; }
    public WaterColor Color { get; init; }
    public int Amount { get; init; }
}
