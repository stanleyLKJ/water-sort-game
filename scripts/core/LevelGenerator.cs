using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WaterSortGame.Model;

namespace WaterSortGame.Core;

public sealed partial class LevelGenerator : Node
{
    private const int BottleCount = 6;
    private const int BottleCapacity = 4;
    private const int LayersPerColor = 4;
    private const int MaxRandomAttempts = 100;

    private readonly RandomNumberGenerator _random = new();
    private bool _isRandomized;

    [Export]
    public bool UseRandomizedLevels { get; set; }

    public void CreateInitialState(GameState state)
    {
        if (UseRandomizedLevels)
        {
            CreateSimpleRandomState(state);
            return;
        }

        CreateFixedTestState(state);
    }

    public void CreateFixedTestState(GameState state)
    {
        ResetState(state);

        state.Bottles.Add(CreateBottle(0,
            (WaterColor.Green, false),
            (WaterColor.Blue, false),
            (WaterColor.Yellow, false),
            (WaterColor.Red, true)));

        state.Bottles.Add(CreateBottle(1,
            (WaterColor.Red, false),
            (WaterColor.Green, false),
            (WaterColor.Yellow, false),
            (WaterColor.Blue, true)));

        state.Bottles.Add(CreateBottle(2,
            (WaterColor.Blue, false),
            (WaterColor.Red, false),
            (WaterColor.Green, false),
            (WaterColor.Yellow, true)));

        state.Bottles.Add(CreateBottle(3,
            (WaterColor.Yellow, false),
            (WaterColor.Blue, false),
            (WaterColor.Red, false),
            (WaterColor.Green, true)));

        state.Bottles.Add(new BottleData { Id = 4 });
        state.Bottles.Add(new BottleData { Id = 5 });

        ValidateFixedTestState(state);
    }

    public void CreateSimpleRandomState(GameState state)
    {
        EnsureRandomized();
        ResetState(state);

        List<BottleData> generatedBottles = new();
        for (int attempt = 0; attempt < MaxRandomAttempts; attempt++)
        {
            generatedBottles = BuildSimpleRandomBottles();
            if (!generatedBottles.Any(IsCompletedBottle))
            {
                break;
            }
        }

        state.Bottles.AddRange(generatedBottles);
        ValidateRandomState(state);
    }

    private static void ResetState(GameState state)
    {
        state.Bottles.Clear();
        state.Bags.Clear();
        state.SelectedBottleId = null;

        state.Bags[WaterColor.Red] = new BagData(WaterColor.Red);
        state.Bags[WaterColor.Blue] = new BagData(WaterColor.Blue);
        state.Bags[WaterColor.Yellow] = new BagData(WaterColor.Yellow);
        state.Bags[WaterColor.Green] = new BagData(WaterColor.Green);
    }

    private static BottleData CreateBottle(int id, params (WaterColor Color, bool IsRevealed)[] layers)
    {
        BottleData bottle = new() { Id = id };
        foreach ((WaterColor color, bool isRevealed) in layers)
        {
            bottle.Layers.Add(new WaterLayer(color, isRevealed));
        }

        return bottle;
    }

    private List<BottleData> BuildSimpleRandomBottles()
    {
        int emptyBottleCount = _random.RandiRange(1, 2);
        int nonEmptyBottleCount = BottleCount - emptyBottleCount;
        List<int> layerCounts = CreateRandomLayerCounts(nonEmptyBottleCount);
        List<WaterColor> colors = CreateShuffledColorPool();
        List<BottleData> bottles = new();
        int colorIndex = 0;

        for (int bottleId = 0; bottleId < BottleCount; bottleId++)
        {
            BottleData bottle = new() { Id = bottleId, Capacity = BottleCapacity };
            if (bottleId < nonEmptyBottleCount)
            {
                int layerCount = layerCounts[bottleId];
                for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
                {
                    bool isTopLayer = layerIndex == layerCount - 1;
                    bottle.Layers.Add(new WaterLayer(colors[colorIndex++], isTopLayer));
                }
            }

            bottles.Add(bottle);
        }

        Shuffle(bottles);
        for (int i = 0; i < bottles.Count; i++)
        {
            bottles[i].Id = i;
        }

        return bottles;
    }

    private List<int> CreateRandomLayerCounts(int bottleCount)
    {
        List<int> counts = Enumerable.Repeat(1, bottleCount).ToList();
        int remainingLayers = (LayersPerColor * 4) - bottleCount;

        while (remainingLayers > 0)
        {
            int index = _random.RandiRange(0, bottleCount - 1);
            if (counts[index] >= BottleCapacity)
            {
                continue;
            }

            counts[index]++;
            remainingLayers--;
        }

        Shuffle(counts);
        return counts;
    }

    private List<WaterColor> CreateShuffledColorPool()
    {
        List<WaterColor> colors = new();
        foreach (WaterColor color in Enum.GetValues<WaterColor>())
        {
            for (int i = 0; i < LayersPerColor; i++)
            {
                colors.Add(color);
            }
        }

        Shuffle(colors);
        return colors;
    }

    private void Shuffle<T>(IList<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = _random.RandiRange(0, i);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }

    private void EnsureRandomized()
    {
        if (_isRandomized)
        {
            return;
        }

        _random.Randomize();
        _isRandomized = true;
    }

    private static bool IsCompletedBottle(BottleData bottle)
    {
        if (bottle.Layers.Count != BottleCapacity)
        {
            return false;
        }

        WaterColor color = bottle.Layers[0].Color;
        return bottle.Layers.All(layer => layer.Color == color);
    }

    private static void ValidateFixedTestState(GameState state)
    {
        Dictionary<WaterColor, int> layerCounts = new()
        {
            [WaterColor.Red] = 0,
            [WaterColor.Blue] = 0,
            [WaterColor.Yellow] = 0,
            [WaterColor.Green] = 0
        };

        foreach (BottleData bottle in state.Bottles)
        {
            foreach (WaterLayer layer in bottle.Layers)
            {
                layerCounts[layer.Color]++;
            }

            if (!bottle.IsEmpty && !bottle.Layers[^1].IsRevealed)
            {
                GD.PushWarning($"Bottle {bottle.Id} top layer should be revealed in the initial fixed test state.");
            }
        }

        foreach (KeyValuePair<WaterColor, int> pair in layerCounts)
        {
            if (pair.Value != 4)
            {
                GD.PushWarning($"Initial fixed test state has {pair.Value} {pair.Key} layers; expected 4.");
            }
        }
    }

    private static void ValidateRandomState(GameState state)
    {
        if (state.Bottles.Count != BottleCount)
        {
            GD.PushWarning($"Random state has {state.Bottles.Count} bottles; expected {BottleCount}.");
        }

        int emptyBottleCount = state.Bottles.Count(bottle => bottle.IsEmpty);
        if (emptyBottleCount < 1 || emptyBottleCount > 2)
        {
            GD.PushWarning($"Random state has {emptyBottleCount} empty bottles; expected 1 or 2.");
        }

        Dictionary<WaterColor, int> layerCounts = new()
        {
            [WaterColor.Red] = 0,
            [WaterColor.Blue] = 0,
            [WaterColor.Yellow] = 0,
            [WaterColor.Green] = 0
        };

        foreach (BottleData bottle in state.Bottles)
        {
            foreach (WaterLayer layer in bottle.Layers)
            {
                layerCounts[layer.Color]++;
            }

            if (!bottle.IsEmpty && !bottle.Layers[^1].IsRevealed)
            {
                GD.PushWarning($"Bottle {bottle.Id} top layer should be revealed in the initial random state.");
            }
        }

        foreach (KeyValuePair<WaterColor, int> pair in layerCounts)
        {
            if (pair.Value != LayersPerColor)
            {
                GD.PushWarning($"Random state has {pair.Value} {pair.Key} layers; expected {LayersPerColor}.");
            }
        }
    }
}
