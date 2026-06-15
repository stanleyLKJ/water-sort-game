#nullable enable

using System;
using System.Collections.Generic;
using WaterSortGame.Model;

namespace WaterSortGame.Core;

public static class LevelQualityEvaluator
{
    public const int MaxBottleSameColorRun = 2;
    public const int MaxInitialVisibleTopCountPerColor = 2;
    public const int RequiredDifferentVisualThirdLayerDataIndex = 1;
    public const int RequiredDifferentVisualFourthLayerDataIndex = 0;

    public static bool Validate(GameState state)
    {
        return TryGetFailureReason(state, out _);
    }

    public static bool TryGetFailureReason(GameState state, out string failureReason)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (HasBottleSameColorRunAtLeast(state, MaxBottleSameColorRun + 1))
        {
            failureReason = $"A bottle contains a same-color run longer than {MaxBottleSameColorRun}.";
            return false;
        }

        if (HasSameColorAtLayerPair(
                state,
                RequiredDifferentVisualThirdLayerDataIndex,
                RequiredDifferentVisualFourthLayerDataIndex))
        {
            failureReason = "A bottle has matching real colors at visual reveal layers 3 and 4 (data Layer_1 and Layer_0).";
            return false;
        }

        if (HasInitialVisibleTopColorCountAbove(state, MaxInitialVisibleTopCountPerColor))
        {
            failureReason = $"An initial top color appears more than {MaxInitialVisibleTopCountPerColor} times.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    public static bool HasBottleSameColorRunAtLeast(GameState state, int runLength)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (runLength <= 1)
        {
            return state.Bottles.Exists(bottle => bottle.Layers.Count > 0);
        }

        foreach (BottleData bottle in state.Bottles)
        {
            int currentRun = 0;
            WaterColor? previousColor = null;
            foreach (WaterLayer layer in bottle.Layers)
            {
                if (previousColor == layer.Color)
                {
                    currentRun++;
                }
                else
                {
                    previousColor = layer.Color;
                    currentRun = 1;
                }

                if (currentRun >= runLength)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool HasInitialVisibleTopColorCountAbove(GameState state, int maxCount)
    {
        ArgumentNullException.ThrowIfNull(state);
        Dictionary<WaterColor, int> topCounts = new();

        foreach (BottleData bottle in state.Bottles)
        {
            if (bottle.IsEmpty)
            {
                continue;
            }

            WaterColor topColor = bottle.Layers[^1].Color;
            int count = topCounts.GetValueOrDefault(topColor) + 1;
            if (count > maxCount)
            {
                return true;
            }

            topCounts[topColor] = count;
        }

        return false;
    }

    public static bool HasSameColorAtLayerPair(GameState state, int firstLayerIndex, int secondLayerIndex)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (firstLayerIndex < 0 || secondLayerIndex < 0 || firstLayerIndex == secondLayerIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(firstLayerIndex), "Layer indexes must be distinct and non-negative.");
        }

        int requiredLayerCount = Math.Max(firstLayerIndex, secondLayerIndex) + 1;
        foreach (BottleData bottle in state.Bottles)
        {
            if (bottle.Layers.Count >= requiredLayerCount &&
                bottle.Layers[firstLayerIndex].Color == bottle.Layers[secondLayerIndex].Color)
            {
                return true;
            }
        }

        return false;
    }

    public static int CalculateInitialBottomPairDiversityScore(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        int score = 0;
        foreach (BottleData bottle in state.Bottles)
        {
            if (bottle.Layers.Count > RequiredDifferentVisualThirdLayerDataIndex &&
                bottle.Layers[RequiredDifferentVisualFourthLayerDataIndex].Color !=
                    bottle.Layers[RequiredDifferentVisualThirdLayerDataIndex].Color)
            {
                score++;
            }
        }

        return score;
    }
}
