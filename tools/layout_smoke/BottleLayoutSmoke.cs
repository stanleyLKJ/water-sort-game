#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.View;

public sealed partial class BottleLayoutSmoke : Node
{
    private const float MinimumSafeDistance = 90f;

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
            GD.Print("BOTTLE_LAYOUT_SMOKE_OK");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError($"BOTTLE_LAYOUT_SMOKE_FAILED: {ex}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        AssertFallbackLayout();
        await AssertLevelLayoutAsync(levelNumber: 1, expectedBottleCount: 6);
        await AssertLevelLayoutAsync(levelNumber: 3, expectedBottleCount: 7);
        await AssertLevelLayoutAsync(levelNumber: 6, expectedBottleCount: 8);
        await AssertEightToSixAndRestartAsync();
    }

    private static void AssertFallbackLayout()
    {
        IReadOnlyList<Vector2> positions = BottleLayoutHelper.GetPositions(9);
        Assert(positions.Count == 9, "Fallback layout should return one position per bottle.");

        for (int i = 0; i < positions.Count; i++)
        {
            for (int j = i + 1; j < positions.Count; j++)
            {
                Assert(
                    positions[i].DistanceTo(positions[j]) > MinimumSafeDistance,
                    $"Fallback layout positions {i} and {j} should not overlap.");
            }
        }
    }

    private async Task AssertLevelLayoutAsync(int levelNumber, int expectedBottleCount)
    {
        Node2D gameScene = await CreateGameSceneAsync(levelNumber);
        try
        {
            AssertActiveLayout(gameScene, expectedBottleCount, $"Level {levelNumber}");
            AssertBottleLayerOwnership(gameScene, expectedBottleCount);
        }
        finally
        {
            await RemoveSceneAsync(gameScene);
        }
    }

    private async Task AssertEightToSixAndRestartAsync()
    {
        Node2D gameScene = await CreateGameSceneAsync(levelNumber: 6);
        try
        {
            GameManager gameManager = gameScene.GetNode<GameManager>("Managers/GameManager");
            AssertActiveLayout(gameScene, 8, "Initial level 6");

            int levelCompletedSignals = 0;
            int exitSignals = 0;
            gameManager.LevelCompleted += () => levelCompletedSignals++;
            gameManager.ExitRequested += () => exitSignals++;

            gameManager.SelectedLevelNumber = 1;
            gameScene.GetNode<Button>("CanvasLayer/RestartButton").EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();

            AssertActiveLayout(gameScene, 6, "After switching 8 bottles to 6");
            AssertInactiveExtraBottle(gameScene, 6);
            AssertInactiveExtraBottle(gameScene, 7);

            int childCountAfterSwitch = CountBottleViewChildren(gameScene);
            gameScene.GetNode<Button>("CanvasLayer/RestartButton").EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();

            AssertActiveLayout(gameScene, 6, "After restarting 6-bottle level");
            Assert(
                CountBottleViewChildren(gameScene) == childCountAfterSwitch,
                "Restarting the current level must not create duplicate BottleView nodes.");

            gameScene.GetNode<Button>("CanvasLayer/ExitButton").EmitSignal(BaseButton.SignalName.Pressed);
            await NextFrame();
            Assert(exitSignals == 1, "ExitButton should emit ExitRequested once.");
            Assert(levelCompletedSignals == 0, "Exiting the level must not emit LevelCompleted.");
        }
        finally
        {
            await RemoveSceneAsync(gameScene);
        }
    }

    private async Task<Node2D> CreateGameSceneAsync(int levelNumber)
    {
        PackedScene packedScene = GD.Load<PackedScene>("res://GameScene.tscn");
        Node2D gameScene = packedScene.Instantiate<Node2D>();
        GameManager gameManager = gameScene.GetNode<GameManager>("Managers/GameManager");
        gameManager.IsManagedByMainFlow = true;
        gameManager.SelectedFlowerId = "pink_rose";
        gameManager.SelectedLevelNumber = levelNumber;
        AddChild(gameScene);
        await NextFrame();
        return gameScene;
    }

    private static void AssertActiveLayout(Node gameScene, int expectedBottleCount, string label)
    {
        Node bottleRoot = gameScene.GetNode<Node>("WorldRoot/BottleRoot");
        List<BottleView> activeViews = new();
        for (int i = 0; i < expectedBottleCount; i++)
        {
            BottleView view = bottleRoot.GetNode<BottleView>($"Bottle_{i}");
            Assert(view.Visible, $"{label}: Bottle_{i} should be visible.");
            CollisionShape2D collision = view.GetNode<CollisionShape2D>("CollisionShape2D");
            Assert(!collision.Disabled, $"{label}: Bottle_{i} should be clickable.");
            activeViews.Add(view);
        }

        Assert(activeViews.Count == expectedBottleCount, $"{label}: active BottleView count mismatch.");
        AssertPositionsAreUniqueAndSeparated(activeViews, label);

        foreach (Node child in bottleRoot.GetChildren())
        {
            if (child is not BottleView view || activeViews.Contains(view))
            {
                continue;
            }

            CollisionShape2D collision = view.GetNode<CollisionShape2D>("CollisionShape2D");
            Assert(!view.Visible, $"{label}: extra {view.Name} should be hidden.");
            Assert(collision.Disabled, $"{label}: extra {view.Name} should not be clickable.");
        }
    }

    private static void AssertPositionsAreUniqueAndSeparated(IReadOnlyList<BottleView> views, string label)
    {
        for (int i = 0; i < views.Count; i++)
        {
            for (int j = i + 1; j < views.Count; j++)
            {
                float distance = views[i].GlobalPosition.DistanceTo(views[j].GlobalPosition);
                Assert(
                    distance > MinimumSafeDistance,
                    $"{label}: {views[i].Name} and {views[j].Name} are too close ({distance:0.##}).");
            }
        }
    }

    private static void AssertBottleLayerOwnership(Node gameScene, int bottleCount)
    {
        Node bottleRoot = gameScene.GetNode<Node>("WorldRoot/BottleRoot");
        for (int i = 0; i < bottleCount; i++)
        {
            BottleView view = bottleRoot.GetNode<BottleView>($"Bottle_{i}");
            Node layerRoot = view.FindChild("LayerRoot", recursive: true, owned: false)
                ?? throw new InvalidOperationException($"Bottle_{i} should contain LayerRoot.");
            Assert(view.IsAncestorOf(layerRoot), $"Bottle_{i} LayerRoot must stay under its own BottleView.");

            for (int layerIndex = 0; layerIndex < 4; layerIndex++)
            {
                Node layer = layerRoot.GetNode($"Layer_{layerIndex}");
                Node question = layerRoot.GetNode($"Question_{layerIndex}");
                Assert(view.IsAncestorOf(layer), $"Bottle_{i} Layer_{layerIndex} must stay under its own BottleView.");
                Assert(view.IsAncestorOf(question), $"Bottle_{i} Question_{layerIndex} must stay under its own BottleView.");
            }
        }
    }

    private static void AssertInactiveExtraBottle(Node gameScene, int bottleIndex)
    {
        BottleView view = gameScene.GetNode<BottleView>($"WorldRoot/BottleRoot/Bottle_{bottleIndex}");
        CollisionShape2D collision = view.GetNode<CollisionShape2D>("CollisionShape2D");
        Assert(!view.Visible, $"Bottle_{bottleIndex} should be hidden after switching to six bottles.");
        Assert(collision.Disabled, $"Bottle_{bottleIndex} should be non-clickable after switching to six bottles.");
    }

    private static int CountBottleViewChildren(Node gameScene)
    {
        return gameScene
            .GetNode<Node>("WorldRoot/BottleRoot")
            .GetChildren()
            .Count(child => child is BottleView);
    }

    private async Task RemoveSceneAsync(Node gameScene)
    {
        RemoveChild(gameScene);
        gameScene.QueueFree();
        await NextFrame();
    }

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
