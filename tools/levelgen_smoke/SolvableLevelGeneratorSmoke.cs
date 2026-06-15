#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using WaterSortGame.Config;
using WaterSortGame.Core;
using WaterSortGame.Model;
using WaterSortGame.View;

public sealed partial class SolvableLevelGeneratorSmoke : Node
{
    private const string SavePath = "user://solvable_level_generator_smoke.json";
    private const long MaxSixColorGenerationMilliseconds = 1_500;

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
            GD.Print("LEVELGEN_SMOKE_OK");
            GD.Print("SOLVABLE_LEVEL_GENERATOR_SMOKE_OK");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError($"SOLVABLE_LEVEL_GENERATOR_SMOKE_FAILED: {ex}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        LevelGenerator generator = new();
        AssertQualityEvaluatorRules();
        AssertBagSystemRequiresFullyRevealedBottle();
        AssertSolvabilityStatuses();
        AssertGeneratedQualitySamples(generator);

        SaveData untouchedSaveData = SaveData.CreateDefault();
        untouchedSaveData.SetLevelProgress("yellow_rose", 2);
        untouchedSaveData.GetOrCreateInventory("lavender").SeedCount = 3;
        untouchedSaveData.TutorialBubblesShown["home_intro"] = true;
        untouchedSaveData.SetHomeSlot(0, new[] { "pink_rose" });
        string saveSignature = BuildSaveSignature(untouchedSaveData);

        RunSessionState untouchedRunState = new();
        untouchedRunState.SelectTargetFlower("yellow_rose");
        Assert(untouchedRunState.TrySelectPlayableLevel("yellow_rose", 1), "RunSessionState setup should select yellow_rose level 1.");
        string runSignature = BuildRunSignature(untouchedRunState);

        foreach (string flowerId in RunSessionState.OpenFlowerIds)
        {
            HashSet<string> levelFingerprints = new(StringComparer.Ordinal);
            for (int levelNumber = 1; levelNumber <= RunSessionState.LevelsPerFlower; levelNumber++)
            {
                LevelDifficultyConfig config = LevelDifficultyConfig.ForLevel(levelNumber);
                GameState state = new();
                Stopwatch generationTimer = Stopwatch.StartNew();
                generator.GenerateSolvableLevel(state, flowerId, levelNumber);
                generationTimer.Stop();
                AssertGeneratedLevel(generator, state, config, flowerId, levelNumber);
                AssertSixColorGenerationDuration(flowerId, levelNumber, generationTimer.ElapsedMilliseconds);

                string fingerprint = LevelSolvabilityVerifier.BuildStateKey(state);
                GameState duplicate = new();
                Stopwatch duplicateTimer = Stopwatch.StartNew();
                generator.GenerateSolvableLevel(duplicate, flowerId, levelNumber);
                duplicateTimer.Stop();
                AssertSixColorGenerationDuration(flowerId, levelNumber, duplicateTimer.ElapsedMilliseconds);
                Assert(
                    LevelSolvabilityVerifier.BuildStateKey(duplicate) == fingerprint,
                    $"{flowerId} level {levelNumber} should generate deterministically.");
                Assert(levelFingerprints.Add(fingerprint), $"{flowerId} level {levelNumber} should differ from the other levels for that flower.");

                LevelGenerationStats stats = generator.LastGenerationStats;
                GD.Print(
                    $"LEVELGEN_SMOKE_DONE flower={flowerId} level={levelNumber} seed={stats.Seed} " +
                    $"elapsed_ms={generationTimer.ElapsedMilliseconds} attempts={stats.Attempts} " +
                    $"visited={stats.VerificationVisitedStates} status={stats.VerificationStatus} " +
                    $"fallback={stats.UsedFallback}");
            }
        }

        Assert(BuildSaveSignature(untouchedSaveData) == saveSignature, "Level generation must not modify SaveData.");
        Assert(BuildRunSignature(untouchedRunState) == runSignature, "Level generation must not modify RunSessionState progress.");

        LevelDifficultyConfig forcedFallbackConfig = new(6, 8, 6, 2, 0, 0);
        GameState fallbackState = new();
        Stopwatch fallbackTimer = Stopwatch.StartNew();
        generator.GenerateLevelFromDifficulty(fallbackState, forcedFallbackConfig, 123456);
        fallbackTimer.Stop();
        AssertGeneratedLevel(
            generator,
            fallbackState,
            forcedFallbackConfig,
            "forced_fallback",
            6,
            expectStableFlowerLevelSeed: false,
            expectFallback: true);
        Assert(fallbackTimer.ElapsedMilliseconds <= MaxSixColorGenerationMilliseconds,
            $"Six-color fallback should finish within {MaxSixColorGenerationMilliseconds} ms. Actual: {fallbackTimer.ElapsedMilliseconds} ms.");
        Assert(generator.LastGenerationStats.Mode == "fixed_6_color_template",
            $"Six-color fallback should use the fixed template. Actual: {generator.LastGenerationStats.Mode}.");
        Assert(
            generator.VerifyCurrentState(fallbackState).IsSolvable,
            "Fixed six-color fallback should pass known-solution replay verification.");
        GD.Print(
            $"LEVELGEN_SMOKE_FALLBACK_OK level=6 elapsed_ms={fallbackTimer.ElapsedMilliseconds} " +
            $"mode={generator.LastGenerationStats.Mode} status={generator.LastGenerationStats.VerificationStatus}");
        AssertGameManagerVictoryRequiresTargetColorCount();

        await AssertOuterFlowLevelTransferAsync();
        await AssertOuterFlowAsync();
    }

    private static void AssertGeneratedLevel(
        LevelGenerator generator,
        GameState state,
        LevelDifficultyConfig config,
        string flowerId,
        int levelNumber,
        bool expectStableFlowerLevelSeed = true,
        bool expectFallback = false)
    {
        Assert(state.Bottles.Count == config.BottleCount, $"{flowerId} level {levelNumber} bottle count mismatch.");
        Assert(state.Bags.Count == config.ColorCount, $"{flowerId} level {levelNumber} color count mismatch.");
        Assert(state.RequiredColorCount == config.ColorCount, $"{flowerId} level {levelNumber} required color count mismatch.");
        Assert(state.Bottles.Count(bottle => bottle.IsEmpty) >= 1, $"{flowerId} level {levelNumber} needs at least one empty bottle.");
        Assert(!IsWon(state), $"{flowerId} level {levelNumber} must not start completed.");
        Assert(
            LevelQualityEvaluator.Validate(state),
            $"{flowerId} level {levelNumber} should pass initial layout quality checks.");
        Assert(
            !LevelQualityEvaluator.HasBottleSameColorRunAtLeast(state, 3),
            $"{flowerId} level {levelNumber} should not contain a same-color bottle run of three.");
        Assert(
            !LevelQualityEvaluator.HasInitialVisibleTopColorCountAbove(state, 2),
            $"{flowerId} level {levelNumber} should not expose any top color more than twice.");
        Assert(
            !LevelQualityEvaluator.HasSameColorAtLayerPair(
                state,
                LevelQualityEvaluator.RequiredDifferentVisualThirdLayerDataIndex,
                LevelQualityEvaluator.RequiredDifferentVisualFourthLayerDataIndex),
            $"{flowerId} level {levelNumber} should not match visual reveal layers 3 and 4 (data Layer_1 and Layer_0).");

        foreach (BottleData bottle in state.Bottles.Where(bottle => !bottle.IsEmpty))
        {
            if (bottle.Layers.Count >= 2)
            {
                Assert(
                    bottle.Layers[0].Color != bottle.Layers[1].Color,
                    $"{flowerId} level {levelNumber} bottle {bottle.Id} should satisfy Layers[0].Color != Layers[1].Color.");
            }
        }

        Dictionary<WaterColor, int> colorCounts = new();
        foreach (BottleData bottle in state.Bottles)
        {
            Assert(bottle.Capacity == 4, $"{flowerId} level {levelNumber} bottle capacity should be 4.");
            Assert(bottle.Layers.Count <= bottle.Capacity, $"{flowerId} level {levelNumber} bottle overflowed.");
            if (!bottle.IsEmpty)
            {
                Assert(bottle.Layers[^1].IsRevealed, $"{flowerId} level {levelNumber} top layer should be revealed.");
            }

            for (int layerIndex = 0; layerIndex < bottle.Layers.Count; layerIndex++)
            {
                WaterLayer layer = bottle.Layers[layerIndex];
                Assert(
                    layerIndex == bottle.Layers.Count - 1 || !layer.IsRevealed,
                    $"{flowerId} level {levelNumber} non-top layers should start hidden.");
                colorCounts[layer.Color] = colorCounts.GetValueOrDefault(layer.Color) + 1;
            }
        }

        Assert(colorCounts.Count == config.ColorCount, $"{flowerId} level {levelNumber} used color count mismatch.");
        Assert(colorCounts.Values.All(count => count == 4), $"{flowerId} level {levelNumber} every color should have four layers.");
        Assert(
            generator.LastGenerationStats.InitialBottomPairDiversityScore ==
                LevelQualityEvaluator.CalculateInitialBottomPairDiversityScore(state),
            $"{flowerId} level {levelNumber} should publish its Layer_0/Layer_1 diagnostic count.");

        Assert(generator.LastGenerationStats.VerificationSucceeded, $"{flowerId} level {levelNumber} generation stats should record verification success.");
        Assert(
            generator.LastGenerationStats.UsedFallback == expectFallback,
            $"{flowerId} level {levelNumber} fallback mismatch. Expected: {expectFallback}, actual: {generator.LastGenerationStats.UsedFallback}.");
        if (expectStableFlowerLevelSeed)
        {
            SolvabilityVerificationResult verification = generator.VerifyCurrentState(state);
            Assert(verification.IsSolvable, $"{flowerId} level {levelNumber} should pass independent solvability verification.");
            Assert(
                generator.LastGenerationStats.Seed == LevelGenerator.ComputeStableSeed(flowerId, levelNumber),
                $"{flowerId} level {levelNumber} should use its stable seed.");
        }
    }

    private static void AssertSixColorGenerationDuration(string flowerId, int levelNumber, long elapsedMilliseconds)
    {
        if (levelNumber < 6)
        {
            return;
        }

        Assert(
            elapsedMilliseconds <= MaxSixColorGenerationMilliseconds,
            $"{flowerId} level {levelNumber} should generate within {MaxSixColorGenerationMilliseconds} ms. " +
            $"Actual: {elapsedMilliseconds} ms.");
    }

    private static void AssertSolvabilityStatuses()
    {
        GameState unsolvableState = CreateState(new[]
        {
            new[] { WaterColor.Red }
        });
        SolvabilityVerificationResult unsolvable = new LevelSolvabilityVerifier().Verify(unsolvableState);
        Assert(
            unsolvable.Status == SolvabilityVerificationStatus.Unsolvable,
            $"A fully explored dead-end should be Unsolvable. Actual: {unsolvable.Status}.");

        SolvabilityVerificationResult budgetExhausted = new LevelSolvabilityVerifier(
            maxVisitedStates: 1,
            maxSearchDepth: 1).Verify(unsolvableState);
        Assert(
            budgetExhausted.Status == SolvabilityVerificationStatus.BudgetExhausted,
            $"A verifier stopped by its state budget should be BudgetExhausted. Actual: {budgetExhausted.Status}.");
        Assert(budgetExhausted.HitStateLimit, "BudgetExhausted result should identify the state limit.");

        SolvabilityVerificationResult invalidKnownSolution = new LevelSolvabilityVerifier().VerifyKnownSolution(
            unsolvableState,
            Array.Empty<LevelMove>());
        Assert(
            invalidKnownSolution.Status == SolvabilityVerificationStatus.KnownSolutionInvalid,
            $"Invalid known-solution replay should be explicit. Actual: {invalidKnownSolution.Status}.");
    }

    private static void AssertQualityEvaluatorRules()
    {
        Assert(
            !LevelQualityEvaluator.Validate(CreateState(new[]
            {
                new[] { WaterColor.Red, WaterColor.Red, WaterColor.Red, WaterColor.Blue },
                new[] { WaterColor.Blue, WaterColor.Green },
                Array.Empty<WaterColor>()
            })),
            "Quality check should reject a bottle with a same-color run of three.");

        Assert(
            !LevelQualityEvaluator.Validate(CreateState(new[]
            {
                new[] { WaterColor.Yellow, WaterColor.Yellow, WaterColor.Yellow, WaterColor.Yellow },
                new[] { WaterColor.Blue, WaterColor.Green },
                Array.Empty<WaterColor>()
            })),
            "Quality check should reject a bottle with a same-color run of four.");

        Assert(
            LevelQualityEvaluator.Validate(CreateState(new[]
            {
                new[] { WaterColor.Red, WaterColor.Blue, WaterColor.Blue, WaterColor.Green },
                new[] { WaterColor.Green, WaterColor.Yellow, WaterColor.Yellow, WaterColor.Red },
                Array.Empty<WaterColor>()
            })),
            "Quality check should allow same-color runs of two outside data Layer_0/Layer_1.");

        Assert(
            !LevelQualityEvaluator.Validate(CreateState(new[]
            {
                new[] { WaterColor.Blue, WaterColor.Green },
                new[] { WaterColor.Red, WaterColor.Green },
                new[] { WaterColor.Yellow, WaterColor.Green },
                Array.Empty<WaterColor>()
            })),
            "Quality check should reject an initial top color exposed three times.");

        Assert(
            !LevelQualityEvaluator.Validate(CreateState(new[]
            {
                new[] { WaterColor.Blue, WaterColor.Green },
                new[] { WaterColor.Red, WaterColor.Green },
                new[] { WaterColor.Yellow, WaterColor.Green },
                new[] { WaterColor.Purple, WaterColor.Green },
                Array.Empty<WaterColor>()
            })),
            "Quality check should reject an initial top color exposed four times.");

        Assert(
            LevelQualityEvaluator.Validate(CreateState(new[]
            {
                new[] { WaterColor.Red, WaterColor.Green },
                new[] { WaterColor.Blue, WaterColor.Green },
                new[] { WaterColor.Yellow, WaterColor.Blue },
                new[] { WaterColor.Purple, WaterColor.Blue },
                Array.Empty<WaterColor>()
            })),
            "Quality check should allow initial top colors exposed at most twice.");

        Assert(
            !LevelQualityEvaluator.Validate(CreateState(new[]
            {
                new[] { WaterColor.Green, WaterColor.Green, WaterColor.Red, WaterColor.Blue },
                new[] { WaterColor.Yellow, WaterColor.Purple },
                Array.Empty<WaterColor>()
            })),
            "Quality check should reject matching visual reveal layers 3 and 4 (data Layer_1 and Layer_0).");

        Assert(
            LevelQualityEvaluator.Validate(CreateState(new[]
            {
                new[] { WaterColor.Red, WaterColor.Green, WaterColor.Green, WaterColor.Blue },
                new[] { WaterColor.Yellow, WaterColor.Purple },
                Array.Empty<WaterColor>()
            })),
            "Matching data Layer_1 and Layer_2 must not be mistaken for the visual reveal layer 3/4 pair.");

        Assert(
            !LevelQualityEvaluator.Validate(CreateState(new[]
            {
                new[] { WaterColor.Red, WaterColor.Red, WaterColor.Green, WaterColor.Blue },
                new[] { WaterColor.Yellow, WaterColor.Purple },
                Array.Empty<WaterColor>()
            })),
            "Matching data Layer_0 and Layer_1 must fail because they are visual reveal layers 4 and 3.");
    }

    private static void AssertBagSystemRequiresFullyRevealedBottle()
    {
        GameState state = new();
        state.Bags[WaterColor.Purple] = new BagData(WaterColor.Purple);
        BottleData bottle = new() { Id = 0, Capacity = 4 };
        bottle.Layers.Add(new WaterLayer(WaterColor.Purple, true));
        bottle.Layers.Add(new WaterLayer(WaterColor.Purple, true));
        bottle.Layers.Add(new WaterLayer(WaterColor.Purple, true));
        bottle.Layers.Add(new WaterLayer(WaterColor.Purple, false));
        state.Bottles.Add(bottle);

        BagSystem bagSystem = new();
        Assert(!bagSystem.IsCompletedBottle(bottle), "A same-color full bottle with a hidden layer must not be completed.");
        Assert(bagSystem.CollectCompletedBottles(state).Count == 0, "A same-color full bottle with a hidden layer must not auto-collect.");
        Assert(!bottle.IsCollected, "A hidden-layer bottle must remain available after the collection check.");
        Assert(state.Bags[WaterColor.Purple].CollectedCount == 0, "A hidden-layer bottle must not increment cauldron progress.");

        foreach (WaterLayer layer in bottle.Layers)
        {
            layer.IsRevealed = true;
        }

        Assert(bagSystem.IsCompletedBottle(bottle), "A fully revealed same-color full bottle should be completed.");
        Assert(bagSystem.CollectCompletedBottles(state).SequenceEqual(new[] { bottle.Id }), "A fully revealed same-color full bottle should auto-collect once.");
        Assert(bottle.IsCollected, "A fully revealed completed bottle should be marked collected.");
        Assert(state.Bags[WaterColor.Purple].CollectedCount == 1, "A fully revealed completed bottle should increment cauldron progress once.");
    }

    private static void AssertGeneratedQualitySamples(LevelGenerator generator)
    {
        (int LevelNumber, int ExpectedColors)[] samples =
        {
            (1, 4),
            (3, 5),
            (6, 6)
        };

        foreach ((int levelNumber, int expectedColors) in samples)
        {
            LevelDifficultyConfig config = LevelDifficultyConfig.ForLevel(levelNumber);
            for (int i = 0; i < 100; i++)
            {
                GameState state = new();
                int seed = 1_000_000 + (expectedColors * 10_000) + i;
                generator.GenerateLevelFromDifficulty(state, config, seed);
                AssertGeneratedLevel(generator, state, config, $"sample_{expectedColors}_color", levelNumber, expectStableFlowerLevelSeed: false);
            }
        }
    }

    private static void AssertGameManagerVictoryRequiresTargetColorCount()
    {
        WaterColor[] colors =
        {
            WaterColor.Red,
            WaterColor.Blue,
            WaterColor.Yellow,
            WaterColor.Green,
            WaterColor.Purple,
            WaterColor.Orange
        };

        foreach (int requiredColorCount in new[] { 4, 5, 6 })
        {
            GameManager gameManager = new();
            GameState state = GetPrivateField<GameState>(gameManager, "_state");
            state.Bags.Clear();
            state.CollectedColorOrder.Clear();
            state.RequiredColorCount = requiredColorCount;
            SetPrivateField(gameManager, "_targetColorCount", requiredColorCount);
            for (int colorIndex = 0; colorIndex < requiredColorCount; colorIndex++)
            {
                state.Bags[colors[colorIndex]] = new BagData(colors[colorIndex]);
            }

            for (int collectedCount = 0; collectedCount < requiredColorCount; collectedCount++)
            {
                state.CollectedColorOrder.Clear();
                for (int colorIndex = 0; colorIndex < collectedCount; colorIndex++)
                {
                    state.CollectedColorOrder.Add(colors[colorIndex]);
                }

                Assert(!InvokeIsWin(gameManager), $"{requiredColorCount}-color level should not win after collecting {collectedCount} colors.");
            }

            state.CollectedColorOrder.Clear();
            for (int colorIndex = 0; colorIndex < requiredColorCount; colorIndex++)
            {
                state.CollectedColorOrder.Add(colors[colorIndex]);
            }

            Assert(InvokeIsWin(gameManager), $"{requiredColorCount}-color level should win only after collecting all required colors.");
        }
    }

    private static bool InvokeIsWin(GameManager gameManager)
    {
        MethodInfo method = typeof(GameManager).GetMethod("IsWin", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(GameManager).FullName, "IsWin");
        return (bool)(method.Invoke(gameManager, Array.Empty<object>()) ?? false);
    }

    private async Task AssertOuterFlowAsync()
    {
        DeleteUserFile(SavePath);
        PackedScene packedMain = GD.Load<PackedScene>("res://main.tscn");
        MainFlowController main = packedMain.Instantiate<MainFlowController>();
        main.SavePathOverride = SavePath;
        AddChild(main);
        await NextFrame();

        RunSessionState state = GetPrivateField<RunSessionState>(main, "_runSessionState");
        SaveData saveData = GetPrivateField<SaveData>(main, "_saveData");
        state.SelectTargetFlower("pink_rose");
        InvokePrivate(main, "OnLevelSelected", 1);
        await NextFrame();

        Node gameScene = GetPrivateField<Node>(main, "_activeScene");
        GameManager gameManager = gameScene.GetNode<GameManager>("Managers/GameManager");
        Assert(gameManager.SelectedFlowerId == "pink_rose", "GameManager should receive the selected flower id.");
        Assert(gameManager.SelectedLevelNumber == 1, "GameManager should receive the selected level number.");

        int levelCompletedSignals = 0;
        gameManager.LevelCompleted += () => levelCompletedSignals++;
        InvokePrivate(gameManager, "RequestExit");
        await NextFrame();
        Assert(levelCompletedSignals == 0, "Exiting a level must not emit LevelCompleted.");
        Assert(state.GetCompletedLevelCount("pink_rose") == 0, "Exiting a level must not change progress.");
        Assert(saveData.WarehouseInventoryByFlower["pink_rose"].SeedCount == 0, "Exiting a level must not grant seed rewards.");
        Assert(saveData.WarehouseInventoryByFlower["pink_rose"].PotionCount == 0, "Exiting a level must not grant potion rewards.");
        Assert(GetPrivateField<Node>(main, "_activeScene") is LevelSelectView, "Exiting should return to LevelSelect.");

        InvokePrivate(main, "OnLevelCompleted");
        await NextFrame();
        Assert(state.GetCompletedLevelCount("pink_rose") == 1, "Completion should still update outer progress once.");
        Assert(saveData.WarehouseInventoryByFlower["pink_rose"].SeedCount == 1, "Completion should still add one seed.");
        Assert(saveData.WarehouseInventoryByFlower["pink_rose"].PotionCount == 1, "Completion should still add one potion.");
        Assert(!state.PendingPlanting, "Completion must not enter the old pending planting flow.");
        Node activeScene = GetPrivateField<Node>(main, "_activeScene");
        Assert(activeScene is HomeGardenView, "Completion should return to HomeGarden.");
        Assert(activeScene is not PlantingPageView, "Completion must not enter PlantingPage.");
        Assert(!activeScene.Name.ToString().Contains("RewardFlower", StringComparison.Ordinal), "Completion must not enter old RewardFlower.");

        main.QueueFree();
        await NextFrame();
        DeleteUserFile(SavePath);
    }

    private async Task AssertOuterFlowLevelTransferAsync()
    {
        DeleteUserFile(SavePath);
        PackedScene packedMain = GD.Load<PackedScene>("res://main.tscn");
        MainFlowController main = packedMain.Instantiate<MainFlowController>();
        main.SavePathOverride = SavePath;
        AddChild(main);
        await NextFrame();

        RunSessionState state = GetPrivateField<RunSessionState>(main, "_runSessionState");
        SaveData saveData = GetPrivateField<SaveData>(main, "_saveData");
        await AssertLevelSelectButtonStartsLevelAsync(main, state, saveData, "pink_rose", 1, 4);
        await AssertLevelSelectButtonStartsLevelAsync(main, state, saveData, "pink_rose", 3, 5);
        await AssertLevelSelectButtonStartsLevelAsync(main, state, saveData, "pink_rose", 6, 6);
        await AssertLevelSelectButtonStartsLevelAsync(main, state, saveData, "pink_rose", 7, 6);

        main.QueueFree();
        await NextFrame();
        DeleteUserFile(SavePath);
    }

    private async Task AssertLevelSelectButtonStartsLevelAsync(
        MainFlowController main,
        RunSessionState state,
        SaveData saveData,
        string flowerId,
        int levelNumber,
        int expectedColorCount)
    {
        saveData.SetLevelProgress(flowerId, levelNumber - 1);
        saveData.Normalize();
        state.ApplySaveData(saveData);
        state.SelectTargetFlower(flowerId);
        InvokePrivate(main, "ShowLevelSelect", (object?)null!);
        await NextFrame();

        LevelSelectView levelSelect = GetPrivateField<Node>(main, "_activeScene") as LevelSelectView
            ?? throw new InvalidOperationException("Expected LevelSelectView before selecting a level.");
        Control panelRoot = levelSelect.GetNode<Control>("FlowerPanelsRoot/PinkRosePanelRoot");
        Button levelButton = panelRoot.GetNode<Button>($"LevelSlots/LevelSlot_{levelNumber:00}/HotAreaButton");
        Stopwatch flowTimer = Stopwatch.StartNew();
        levelButton.EmitSignal(Button.SignalName.Pressed);
        await NextFrame();
        flowTimer.Stop();
        AssertSixColorGenerationDuration(flowerId, levelNumber, flowTimer.ElapsedMilliseconds);

        Node gameScene = GetPrivateField<Node>(main, "_activeScene");
        GameManager gameManager = gameScene.GetNode<GameManager>("Managers/GameManager");
        GameState gameState = GetPrivateField<GameState>(gameManager, "_state");
        Assert(gameManager.SelectedFlowerId == flowerId, $"GameManager should receive {flowerId} from LevelSelect level {levelNumber}.");
        Assert(gameManager.SelectedLevelNumber == levelNumber, $"GameManager should receive level {levelNumber} from LevelSelect.");
        Assert(gameState.RequiredColorCount == expectedColorCount, $"Level {levelNumber} should publish {expectedColorCount} required colors.");
        Assert(
            CountVisibleCauldronBubbles(gameScene) == expectedColorCount,
            $"Level {levelNumber} should show {expectedColorCount} visible cauldron progress bubbles.");
        GD.Print(
            $"LEVELGEN_FLOW_SMOKE_DONE flower={flowerId} level={levelNumber} " +
            $"elapsed_ms={flowTimer.ElapsedMilliseconds} colors={gameState.RequiredColorCount}");

        InvokePrivate(gameManager, "RequestExit");
        await NextFrame();
        Assert(GetPrivateField<Node>(main, "_activeScene") is LevelSelectView, $"Exiting level {levelNumber} should return to LevelSelect.");
    }

    private static int CountVisibleCauldronBubbles(Node gameScene)
    {
        int visibleCount = 0;
        Node progressRoot = gameScene.GetNode<Node>("WorldRoot/CauldronRoot/CauldronView/CauldronProgressRoot");
        for (int i = 0; i < 6; i++)
        {
            ColorRect? bubble = progressRoot.GetNodeOrNull<ColorRect>($"Bubble_{i}");
            if (bubble != null && bubble.Visible)
            {
                visibleCount++;
            }
        }

        return visibleCount;
    }

    private static bool IsWon(GameState state)
    {
        return state.Bottles.All(bottle =>
            bottle.IsEmpty ||
            (bottle.Layers.Count == bottle.Capacity && bottle.Layers.All(layer => layer.Color == bottle.Layers[0].Color)));
    }

    private static GameState CreateState(IEnumerable<IReadOnlyList<WaterColor>> bottles)
    {
        GameState state = new();
        int bottleId = 0;
        foreach (IReadOnlyList<WaterColor> colors in bottles)
        {
            BottleData bottle = new() { Id = bottleId++ };
            for (int layerIndex = 0; layerIndex < colors.Count; layerIndex++)
            {
                bottle.Layers.Add(new WaterLayer(colors[layerIndex], layerIndex == colors.Count - 1));
            }

            state.Bottles.Add(bottle);
        }

        return state;
    }

    private static string BuildSaveSignature(SaveData data)
    {
        data.Normalize();
        string progress = string.Join(",", data.LevelProgressByFlower.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
        string inventory = string.Join(",", data.WarehouseInventoryByFlower.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.SeedCount}:{pair.Value.PotionCount}"));
        string slots = string.Join(",", data.HomeSlotsBySlot.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.Batch}"));
        string tutorials = string.Join(",", data.TutorialBubblesShown.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
        return $"{progress}|{inventory}|{slots}|{tutorials}|{data.Settings.Language}:{data.Settings.MusicVolume}:{data.Settings.SfxVolume}";
    }

    private static string BuildRunSignature(RunSessionState state)
    {
        string progress = string.Join(",", RunSessionState.OpenFlowerIds.Select(id => $"{id}:{state.GetCompletedLevelCount(id)}"));
        return $"{state.SelectedFlowerId}:{state.SelectedLevelNumber}:{progress}:{state.PendingPlanting}:{state.IsWarehousePlantingMode}";
    }

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().FullName, methodName);
        method.Invoke(target, args);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().FullName, fieldName);
        return (T)(field.GetValue(target) ?? throw new InvalidOperationException($"Field {fieldName} was null."));
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().FullName, fieldName);
        field.SetValue(target, value);
    }

    private static void DeleteUserFile(string path)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        if (FileAccess.FileExists(path))
        {
            DirAccess.RemoveAbsolute(absolutePath);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
