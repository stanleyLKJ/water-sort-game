#nullable enable

using System;
using Godot;

namespace WaterSortGame.Core;

public sealed class RewardFlowerSystem
{
    public const int BaseFlowerCount = 6;
    public const int RewardOptionCount = 4;

    private readonly RandomNumberGenerator _random = new();

    public RewardFlowerSystem()
    {
        _random.Randomize();
    }

    public int[] CreateRewardOptions()
    {
        int[] flowerIds = new int[BaseFlowerCount];
        for (int i = 0; i < flowerIds.Length; i++)
        {
            flowerIds[i] = i;
        }

        for (int i = flowerIds.Length - 1; i > 0; i--)
        {
            int swapIndex = _random.RandiRange(0, i);
            (flowerIds[i], flowerIds[swapIndex]) = (flowerIds[swapIndex], flowerIds[i]);
        }

        int[] options = new int[RewardOptionCount];
        Array.Copy(flowerIds, options, RewardOptionCount);
        return options;
    }
}
