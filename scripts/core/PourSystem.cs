using Godot;
using System;
using System.Linq;
using WaterSortGame.Model;

namespace WaterSortGame.Core;

public sealed partial class PourSystem : Node
{
    public PourResult TryCreatePourPlan(BottleData source, BottleData target)
    {
        if (source == null)
        {
            return PourResult.Fail("Source bottle is null.");
        }

        if (target == null)
        {
            return PourResult.Fail("Target bottle is null.");
        }

        if (source.Id == target.Id)
        {
            return PourResult.Fail("Cannot pour into the same bottle.");
        }

        if (source.IsCollected || target.IsCollected)
        {
            return PourResult.Fail("Collected bottles cannot pour or receive water.");
        }

        if (source.IsEmpty)
        {
            return PourResult.Fail("Source bottle is empty.");
        }

        if (target.IsFull)
        {
            return PourResult.Fail("Target bottle is full.");
        }

        WaterLayer sourceTopLayer = source.Layers[^1];
        if (!sourceTopLayer.IsRevealed)
        {
            return PourResult.Fail("Source top layer is hidden.");
        }

        WaterColor color = sourceTopLayer.Color;
        if (!target.IsEmpty && target.Layers[^1].Color != color)
        {
            return PourResult.Fail("Target top color does not match source top color.");
        }

        int pourableAmount = CountRevealedTopSameColorLayers(source, color);
        int amount = Math.Min(pourableAmount, target.EmptySlots);
        if (amount <= 0)
        {
            return PourResult.Fail("No water can be poured.");
        }

        return PourResult.Ok(new PourPlan
        {
            SourceBottleId = source.Id,
            TargetBottleId = target.Id,
            Color = color,
            Amount = amount
        });
    }

    public void ExecutePour(PourPlan plan, GameState state)
    {
        BottleData source = state.Bottles.First(bottle => bottle.Id == plan.SourceBottleId);
        BottleData target = state.Bottles.First(bottle => bottle.Id == plan.TargetBottleId);

        for (int i = 0; i < plan.Amount; i++)
        {
            source.Layers.RemoveAt(source.Layers.Count - 1);
            target.Layers.Add(new WaterLayer(plan.Color, true));
        }

        if (!source.IsEmpty && !source.Layers[^1].IsRevealed)
        {
            source.Layers[^1].IsRevealed = true;
        }
    }

    private static int CountRevealedTopSameColorLayers(BottleData bottle, WaterColor color)
    {
        int count = 0;
        for (int i = bottle.Layers.Count - 1; i >= 0; i--)
        {
            WaterLayer layer = bottle.Layers[i];
            if (!layer.IsRevealed || layer.Color != color)
            {
                break;
            }

            count++;
        }

        return count;
    }
}
