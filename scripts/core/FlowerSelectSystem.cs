#nullable enable

using WaterSortGame.Model;

namespace WaterSortGame.Core;

public readonly struct FlowerOption
{
    public FlowerOption(int index, string flowerId, string displayName, bool isOpen, bool isFull)
    {
        Index = index;
        FlowerId = flowerId;
        DisplayName = displayName;
        IsOpen = isOpen;
        IsFull = isFull;
    }

    public int Index { get; }

    public string FlowerId { get; }

    public string DisplayName { get; }

    public bool IsOpen { get; }

    public bool IsFull { get; }

    public bool IsSelectable => IsOpen && !IsFull;

    public string UnavailableMessage
    {
        get
        {
            if (!IsOpen)
            {
                return "该花将在后续版本开放";
            }

            if (IsFull)
            {
                return "该花已种满，请选择其他花";
            }

            return string.Empty;
        }
    }
}

public sealed class FlowerSelectSystem
{
    public const int BaseFlowerCount = 6;

    private static readonly FlowerDefinition[] BaseFlowers =
    {
        new(0, "pink_rose", "粉玫瑰"),
        new(1, "yellow_rose", "黄玫瑰"),
        new(2, "lavender", "薰衣草"),
        new(3, "flower_04", "待定花 04"),
        new(4, "flower_05", "待定花 05"),
        new(5, "flower_06", "待定花 06")
    };

    public FlowerOption[] CreateBaseFlowerOptions(RunSessionState? state = null)
    {
        FlowerOption[] options = new FlowerOption[BaseFlowers.Length];
        for (int i = 0; i < BaseFlowers.Length; i++)
        {
            FlowerDefinition definition = BaseFlowers[i];
            bool isOpen = RunSessionState.IsOpenFlowerId(definition.FlowerId);
            bool isFull = isOpen && state?.IsFlowerFull(definition.FlowerId) == true;
            options[i] = new FlowerOption(definition.Index, definition.FlowerId, definition.DisplayName, isOpen, isFull);
        }

        return options;
    }

    public string GetDisplayName(string flowerId)
    {
        foreach (FlowerDefinition definition in BaseFlowers)
        {
            if (definition.FlowerId == flowerId)
            {
                return definition.DisplayName;
            }
        }

        return flowerId;
    }

    private readonly struct FlowerDefinition
    {
        public FlowerDefinition(int index, string flowerId, string displayName)
        {
            Index = index;
            FlowerId = flowerId;
            DisplayName = displayName;
        }

        public int Index { get; }

        public string FlowerId { get; }

        public string DisplayName { get; }
    }
}
