#nullable enable

namespace WaterSortGame.Core;

public readonly struct FlowerOption
{
    public FlowerOption(int index, string flowerId, string displayName)
    {
        Index = index;
        FlowerId = flowerId;
        DisplayName = displayName;
    }

    public int Index { get; }

    public string FlowerId { get; }

    public string DisplayName { get; }
}

public sealed class FlowerSelectSystem
{
    public const int BaseFlowerCount = 6;

    public FlowerOption[] CreateBaseFlowerOptions()
    {
        return new[]
        {
            new FlowerOption(0, "pink_rose", "粉玫瑰"),
            new FlowerOption(1, "yellow_rose", "黄玫瑰"),
            new FlowerOption(2, "lavender", "薰衣草"),
            new FlowerOption(3, "flower_04", "待定花 04"),
            new FlowerOption(4, "flower_05", "待定花 05"),
            new FlowerOption(5, "flower_06", "待定花 06")
        };
    }
}
