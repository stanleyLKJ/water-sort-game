using Godot;
using System.Collections.Generic;
using System.Linq;
using WaterSortGame.Model;

namespace WaterSortGame.Core;

public sealed partial class BagSystem : Node
{
    // Historical name retained for compatibility: this now feeds the cauldron
    // debug progress collection flow rather than a visible bag/pot UI.
    public List<int> CollectCompletedBottles(GameState state)
    {
        List<int> collectedIds = new();

        foreach (BottleData bottle in state.Bottles)
        {
            if (bottle == null || bottle.IsCollected || !IsCompletedBottle(bottle))
            {
                continue;
            }

            WaterColor color = bottle.Layers[0].Color;
            if (!state.Bags.ContainsKey(color))
            {
                state.Bags[color] = new BagData(color);
            }

            state.Bags[color].CollectedCount++;
            state.CollectedColorOrder.Add(color);
            bottle.IsCollected = true;
            collectedIds.Add(bottle.Id);
        }

        return collectedIds;
    }

    public bool IsCompletedBottle(BottleData bottle)
    {
        if (bottle == null)
        {
            return false;
        }

        if (bottle.IsCollected || bottle.Layers.Count != bottle.Capacity || bottle.Layers.Count == 0)
        {
            return false;
        }

        WaterColor color = bottle.Layers[0].Color;
        return bottle.Layers.All(layer => layer.Color == color && layer.IsRevealed);
    }
}
