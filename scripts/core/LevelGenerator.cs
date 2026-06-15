#nullable enable

using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WaterSortGame.Config;
using WaterSortGame.Model;

namespace WaterSortGame.Core;

public sealed class LevelGenerationStats
{
    public int Seed { get; init; }
    public int AttemptSeed { get; init; }
    public int Attempts { get; init; }
    public int QualityRejectedAttempts { get; init; }
    public int ScrambleMoves { get; init; }
    public int VerificationVisitedStates { get; init; }
    public int VerificationSolutionDepth { get; init; }
    public int InitialBottomPairDiversityScore { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public string Mode { get; init; } = string.Empty;
    public SolvabilityVerificationStatus VerificationStatus { get; init; }
    public bool VerificationSucceeded { get; init; }
    public bool UsedFallback { get; init; }
}

public sealed partial class LevelGenerator : Node
{
    private const int BottleCapacity = 4;
    private const int LayersPerColor = 4;
    private const int LegacyBottleCount = 6;
    private const int LegacyColorCount = 4;
    private const int MaxLegacyRandomAttempts = 100;
    private const int MaxReverseSearchNodes = 20_000;

    private static readonly WaterColor[] SupportedColors =
    {
        WaterColor.Red,
        WaterColor.Blue,
        WaterColor.Yellow,
        WaterColor.Green,
        WaterColor.Purple,
        WaterColor.Orange
    };

    private readonly RandomNumberGenerator _legacyRandom = new();
    private readonly LevelSolvabilityVerifier _solvabilityVerifier = new();
    private IReadOnlyList<LevelMove> _lastKnownSolution = Array.Empty<LevelMove>();
    private bool _legacyRandomized;

    [Export]
    public bool UseRandomizedLevels { get; set; }

    public LevelGenerationStats LastGenerationStats { get; private set; } = new();
    public IReadOnlyList<LevelMove> LastKnownSolution => _lastKnownSolution;
    public LevelSolvabilityVerifier SolvabilityVerifier => _solvabilityVerifier;

    public void CreateInitialState(GameState state)
    {
        if (UseRandomizedLevels)
        {
            CreateSimpleRandomStateCore(state);
            return;
        }

        CreateFixedTestState(state);
    }

    public void GenerateSolvableLevel(GameState state, string flowerId, int levelNumber)
    {
        ArgumentNullException.ThrowIfNull(state);
        LevelDifficultyConfig config = LevelDifficultyConfig.ForLevel(levelNumber);
        GD.Print(
            $"CAULDRON_DIAG LevelDifficultyConfig.ForLevel " +
            $"levelNumber={levelNumber} BottleCount={config.BottleCount} ColorCount={config.ColorCount} " +
            $"EmptyBottleCount={config.EmptyBottleCount} ShuffleSteps={config.ScrambleSteps}");
        int seed = ComputeStableSeed(flowerId, levelNumber);
        GenerateLevelFromDifficulty(state, config, seed);
    }

    public void GenerateLevelFromDifficulty(GameState state, LevelDifficultyConfig config, int seed)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(config);
        ValidateDifficultyConfig(config);
        Stopwatch stopwatch = Stopwatch.StartNew();
        GD.Print(
            $"LEVELGEN_START level={config.LevelNumber} seed={seed} bottles={config.BottleCount} " +
            $"colors={config.ColorCount} scramble={config.ScrambleSteps} attempts={config.MaxGenerationAttempts}");

        string primaryFailureReason = "template_disabled";
        if (config.ScrambleSteps > 0 && TryBuildVerifiedTemplateState(
                config,
                seed,
                out GameState verifiedCandidate,
                out IReadOnlyList<LevelMove> verifiedKnownSolution,
                out SolvabilityVerificationResult verifiedResult,
                out primaryFailureReason))
        {
            CompleteGeneration(
                state,
                verifiedCandidate,
                config,
                seed,
                seed,
                1,
                0,
                verifiedKnownSolution,
                verifiedResult,
                false,
                "verified_template",
                stopwatch);
            return;
        }

        if (IsSixColorDifficulty(config))
        {
            UseFixedFallbackOrFail(state, config, seed, 0, 0, primaryFailureReason, stopwatch);
            return;
        }

        int qualityRejectedAttempts = 0;
        string lastQualityFailureReason = string.Empty;
        for (int attempt = 1; attempt <= config.MaxGenerationAttempts; attempt++)
        {
            int attemptSeed = MixSeed(seed, attempt);
            if (!TryBuildReverseScrambledState(config, attemptSeed, out GameState candidate, out List<LevelMove> knownSolution))
            {
                continue;
            }

            if (!LevelQualityEvaluator.TryGetFailureReason(candidate, out string qualityFailureReason))
            {
                qualityRejectedAttempts++;
                lastQualityFailureReason = qualityFailureReason;
                continue;
            }

            SolvabilityVerificationResult verification = _solvabilityVerifier.VerifyKnownSolution(candidate, knownSolution);
            if (!verification.IsSolvable)
            {
                GD.PushWarning(
                    $"Solvable level verification failed on attempt {attempt}/{config.MaxGenerationAttempts}. " +
                    $"seed={seed}, attempt_seed={attemptSeed}, visited={verification.VisitedStateCount}, " +
                    $"status={verification.Status}.");
                break;
            }

            CompleteGeneration(
                state,
                candidate,
                config,
                seed,
                attemptSeed,
                attempt,
                qualityRejectedAttempts,
                knownSolution,
                verification,
                false,
                "reverse_scramble",
                stopwatch);
            return;
        }

        if (config.MaxGenerationAttempts > 0 &&
            TryBuildStructuredReverseState(config, seed, out GameState structuredCandidate, out List<LevelMove> structuredKnownSolution))
        {
            if (LevelQualityEvaluator.TryGetFailureReason(structuredCandidate, out string structuredQualityFailureReason))
            {
                SolvabilityVerificationResult structuredVerification = _solvabilityVerifier.VerifyKnownSolution(
                    structuredCandidate,
                    structuredKnownSolution);
                if (structuredVerification.IsSolvable)
                {
                    CompleteGeneration(
                        state,
                        structuredCandidate,
                        config,
                        seed,
                        seed,
                        config.MaxGenerationAttempts,
                        qualityRejectedAttempts,
                        structuredKnownSolution,
                        structuredVerification,
                        false,
                        "structured_reverse",
                        stopwatch);
                    return;
                }

                GD.PushWarning(
                    $"Structured reverse level failed solvability verification. seed={seed}, " +
                    $"visited={structuredVerification.VisitedStateCount}, status={structuredVerification.Status}.");
            }
            else
            {
                GD.PushWarning($"Structured reverse level failed quality validation: {structuredQualityFailureReason}");
            }
        }
        else if (config.MaxGenerationAttempts > 0)
        {
            GD.PushWarning($"Structured reverse level could not be built. seed={seed}.");
        }

        string fallbackReason = string.IsNullOrWhiteSpace(lastQualityFailureReason)
            ? primaryFailureReason
            : lastQualityFailureReason;
        UseFixedFallbackOrFail(
            state,
            config,
            seed,
            config.MaxGenerationAttempts,
            qualityRejectedAttempts,
            fallbackReason,
            stopwatch);
    }

    private bool TryBuildVerifiedTemplateState(
        LevelDifficultyConfig config,
        int seed,
        out GameState candidate,
        out IReadOnlyList<LevelMove> knownSolution,
        out SolvabilityVerificationResult verification,
        out string failureReason)
    {
        List<WaterColor> colors = SupportedColors.Take(config.ColorCount).ToList();
        candidate = new GameState();
        ResetState(candidate, colors);
        for (int bottleId = 0; bottleId < config.BottleCount; bottleId++)
        {
            BottleData bottle = new() { Id = bottleId, Capacity = BottleCapacity };
            if (bottleId < config.ColorCount)
            {
                for (int layerIndex = 0; layerIndex < BottleCapacity; layerIndex++)
                {
                    int colorIndex = layerIndex switch
                    {
                        0 => (bottleId + 1) % config.ColorCount,
                        1 or 2 => bottleId,
                        _ => (bottleId + config.ColorCount - 1) % config.ColorCount
                    };
                    bottle.Layers.Add(new WaterLayer(colors[colorIndex], false));
                }
            }

            candidate.Bottles.Add(bottle);
        }

        ApplyInitialRevealRules(candidate);
        if (!ValidateGeneratedState(candidate, config))
        {
            knownSolution = Array.Empty<LevelMove>();
            verification = new SolvabilityVerificationResult
            {
                Status = SolvabilityVerificationStatus.KnownSolutionInvalid
            };
            failureReason = "template_structure_invalid";
            return false;
        }

        if (!LevelQualityEvaluator.TryGetFailureReason(candidate, out string qualityFailureReason))
        {
            knownSolution = Array.Empty<LevelMove>();
            verification = new SolvabilityVerificationResult
            {
                Status = SolvabilityVerificationStatus.KnownSolutionInvalid
            };
            failureReason = $"template_quality_invalid:{qualityFailureReason}";
            return false;
        }

        knownSolution = BuildRingKnownSolution(config.ColorCount);
        verification = _solvabilityVerifier.VerifyKnownSolution(candidate, knownSolution);
        if (!verification.IsSolvable)
        {
            failureReason = $"template_known_solution_{verification.Status}";
            return false;
        }

        ApplyColorPermutation(candidate, config.ColorCount, seed);
        failureReason = string.Empty;
        return true;
    }

    private static IReadOnlyList<LevelMove> BuildRingKnownSolution(int colorCount)
    {
        int firstEmptyBottleId = colorCount;
        int secondEmptyBottleId = colorCount + 1;
        List<LevelMove> moves = new()
        {
            new LevelMove(0, firstEmptyBottleId),
            new LevelMove(1, secondEmptyBottleId),
            new LevelMove(0, secondEmptyBottleId),
            new LevelMove(0, secondEmptyBottleId)
        };

        for (int colorIndex = 1; colorIndex <= colorCount - 2; colorIndex++)
        {
            moves.Add(new LevelMove(colorIndex, colorIndex - 1));
            moves.Add(new LevelMove(colorIndex, colorIndex - 1));
            moves.Add(new LevelMove(colorIndex + 1, colorIndex - 1));
        }

        moves.Add(new LevelMove(colorCount - 1, colorCount - 2));
        moves.Add(new LevelMove(colorCount - 1, colorCount - 2));
        moves.Add(new LevelMove(firstEmptyBottleId, colorCount - 2));
        moves.Add(new LevelMove(colorCount - 1, secondEmptyBottleId));
        return moves;
    }

    private static bool IsSixColorDifficulty(LevelDifficultyConfig config)
    {
        return config.BottleCount == 8 &&
            config.ColorCount == 6 &&
            config.EmptyBottleCount == 2;
    }

    private void UseFixedFallbackOrFail(
        GameState state,
        LevelDifficultyConfig config,
        int seed,
        int attempts,
        int qualityRejectedAttempts,
        string reason,
        Stopwatch stopwatch)
    {
        GD.Print(
            $"LEVELGEN_FALLBACK level={config.LevelNumber} seed={seed} reason={reason} " +
            $"bottles={config.BottleCount} colors={config.ColorCount}");

        if (TryBuildFixedFallbackState(
                config,
                seed,
                out GameState fallbackState,
                out IReadOnlyList<LevelMove> fallbackKnownSolution,
                out SolvabilityVerificationResult fallbackVerification,
                out string fallbackMode,
                out string fallbackFailureReason))
        {
            CompleteGeneration(
                state,
                fallbackState,
                config,
                seed,
                seed,
                attempts,
                qualityRejectedAttempts,
                fallbackKnownSolution,
                fallbackVerification,
                true,
                fallbackMode,
                stopwatch);
            return;
        }

        stopwatch.Stop();
        _lastKnownSolution = Array.Empty<LevelMove>();
        LastGenerationStats = new LevelGenerationStats
        {
            Seed = seed,
            AttemptSeed = seed,
            Attempts = attempts,
            QualityRejectedAttempts = qualityRejectedAttempts,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            Mode = "failed",
            VerificationStatus = fallbackVerification.Status,
            VerificationSucceeded = false,
            UsedFallback = true
        };
        string message =
            $"LEVELGEN_FAILED level={config.LevelNumber} seed={seed} elapsed_ms={stopwatch.ElapsedMilliseconds} " +
            $"reason={fallbackFailureReason} status={fallbackVerification.Status}";
        GD.PushError(message);
        throw new InvalidOperationException(message);
    }

    private bool TryBuildFixedFallbackState(
        LevelDifficultyConfig config,
        int seed,
        out GameState candidate,
        out IReadOnlyList<LevelMove> knownSolution,
        out SolvabilityVerificationResult verification,
        out string mode,
        out string failureReason)
    {
        if (!IsSixColorDifficulty(config))
        {
            bool built = TryBuildVerifiedTemplateState(
                config,
                seed,
                out candidate,
                out knownSolution,
                out verification,
                out failureReason);
            mode = "fixed_template";
            return built;
        }

        candidate = new GameState();
        CreateFixedSixColorFallbackState(candidate);
        knownSolution = BuildRingKnownSolution(6);
        mode = "fixed_6_color_template";

        if (!ValidateGeneratedState(candidate, config))
        {
            verification = new SolvabilityVerificationResult
            {
                Status = SolvabilityVerificationStatus.KnownSolutionInvalid
            };
            failureReason = "fixed_6_color_structure_invalid";
            return false;
        }

        if (!LevelQualityEvaluator.TryGetFailureReason(candidate, out string qualityFailureReason))
        {
            verification = new SolvabilityVerificationResult
            {
                Status = SolvabilityVerificationStatus.KnownSolutionInvalid
            };
            failureReason = $"fixed_6_color_quality_invalid:{qualityFailureReason}";
            return false;
        }

        verification = _solvabilityVerifier.VerifyKnownSolution(candidate, knownSolution);
        if (!verification.IsSolvable)
        {
            failureReason = $"fixed_6_color_known_solution_{verification.Status}";
            return false;
        }

        ApplyColorPermutation(candidate, config.ColorCount, seed);
        failureReason = string.Empty;
        return true;
    }

    private static void CreateFixedSixColorFallbackState(GameState state)
    {
        ResetState(state, SupportedColors.Take(6));
        state.Bottles.Add(CreateBottle(0,
            (WaterColor.Blue, false),
            (WaterColor.Red, false),
            (WaterColor.Red, false),
            (WaterColor.Orange, false)));
        state.Bottles.Add(CreateBottle(1,
            (WaterColor.Yellow, false),
            (WaterColor.Blue, false),
            (WaterColor.Blue, false),
            (WaterColor.Red, false)));
        state.Bottles.Add(CreateBottle(2,
            (WaterColor.Green, false),
            (WaterColor.Yellow, false),
            (WaterColor.Yellow, false),
            (WaterColor.Blue, false)));
        state.Bottles.Add(CreateBottle(3,
            (WaterColor.Purple, false),
            (WaterColor.Green, false),
            (WaterColor.Green, false),
            (WaterColor.Yellow, false)));
        state.Bottles.Add(CreateBottle(4,
            (WaterColor.Orange, false),
            (WaterColor.Purple, false),
            (WaterColor.Purple, false),
            (WaterColor.Green, false)));
        state.Bottles.Add(CreateBottle(5,
            (WaterColor.Red, false),
            (WaterColor.Orange, false),
            (WaterColor.Orange, false),
            (WaterColor.Purple, false)));
        state.Bottles.Add(new BottleData { Id = 6, Capacity = BottleCapacity });
        state.Bottles.Add(new BottleData { Id = 7, Capacity = BottleCapacity });
        ApplyInitialRevealRules(state);
    }

    private void CompleteGeneration(
        GameState state,
        GameState candidate,
        LevelDifficultyConfig config,
        int seed,
        int attemptSeed,
        int attempts,
        int qualityRejectedAttempts,
        IReadOnlyList<LevelMove> knownSolution,
        SolvabilityVerificationResult verification,
        bool usedFallback,
        string mode,
        Stopwatch stopwatch)
    {
        CopyState(candidate, state);
        _lastKnownSolution = knownSolution.ToArray();
        stopwatch.Stop();
        LastGenerationStats = new LevelGenerationStats
        {
            Seed = seed,
            AttemptSeed = attemptSeed,
            Attempts = attempts,
            QualityRejectedAttempts = qualityRejectedAttempts,
            ScrambleMoves = knownSolution.Count,
            VerificationVisitedStates = verification.VisitedStateCount,
            VerificationSolutionDepth = verification.SolutionDepth,
            InitialBottomPairDiversityScore = LevelQualityEvaluator.CalculateInitialBottomPairDiversityScore(candidate),
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            Mode = mode,
            VerificationStatus = verification.Status,
            VerificationSucceeded = verification.IsSolvable,
            UsedFallback = usedFallback
        };
        GD.Print(
            $"LEVELGEN_DONE level={config.LevelNumber} seed={seed} elapsed_ms={stopwatch.ElapsedMilliseconds} " +
            $"status={verification.Status} visited={verification.VisitedStateCount} attempts={attempts} " +
            $"fallback={usedFallback.ToString().ToLowerInvariant()} mode={mode}");
        PrintGeneratedStateDiagnostics(state, mode);
    }

    private static void ApplyColorPermutation(GameState state, int colorCount, int seed)
    {
        List<WaterColor> sourceColors = SupportedColors.Take(colorCount).ToList();
        List<WaterColor> targetColors = new(sourceColors);
        Shuffle(targetColors, new Random(MixSeed(seed, 20_000)));
        Dictionary<WaterColor, WaterColor> mapping = new();
        for (int i = 0; i < sourceColors.Count; i++)
        {
            mapping[sourceColors[i]] = targetColors[i];
        }

        foreach (BottleData bottle in state.Bottles)
        {
            foreach (WaterLayer layer in bottle.Layers)
            {
                layer.Color = mapping[layer.Color];
            }
        }

        Dictionary<WaterColor, BagData> remappedBags = state.Bags
            .ToDictionary(pair => mapping[pair.Key], pair => new BagData(mapping[pair.Key]));
        state.Bags.Clear();
        foreach (KeyValuePair<WaterColor, BagData> pair in remappedBags)
        {
            state.Bags[pair.Key] = pair.Value;
        }
    }

    public void CreateFixedTestState(GameState state)
    {
        ResetState(state, SupportedColors.Take(LegacyColorCount));

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

    [Obsolete("Simple random levels are not guaranteed solvable. Use GenerateSolvableLevel instead.")]
    public void CreateSimpleRandomState(GameState state)
    {
        CreateSimpleRandomStateCore(state);
    }

    private void CreateSimpleRandomStateCore(GameState state)
    {
        EnsureLegacyRandomized();
        ResetState(state, SupportedColors.Take(LegacyColorCount));

        List<BottleData> generatedBottles = new();
        for (int attempt = 0; attempt < MaxLegacyRandomAttempts; attempt++)
        {
            generatedBottles = BuildSimpleRandomBottles();
            if (!generatedBottles.Any(IsCompletedBottle))
            {
                break;
            }
        }

        state.Bottles.AddRange(generatedBottles);
        ValidateLegacyRandomState(state);
    }

    public SolvabilityVerificationResult VerifyCurrentState(GameState state)
    {
        return _solvabilityVerifier.Verify(state, _lastKnownSolution);
    }

    public static int ComputeStableSeed(string flowerId, int levelNumber)
    {
        string normalizedFlowerId = string.IsNullOrWhiteSpace(flowerId)
            ? "pink_rose"
            : flowerId.Trim().ToLowerInvariant();
        string input = $"{normalizedFlowerId}:{levelNumber}";
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in input)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return (int)hash;
        }
    }

    private static bool TryBuildReverseScrambledState(
        LevelDifficultyConfig config,
        int seed,
        out GameState state,
        out List<LevelMove> knownSolution)
    {
        Random random = new(seed);
        List<WaterColor> colors = SupportedColors.Take(config.ColorCount).ToList();
        Shuffle(colors, random);

        state = new GameState();
        ResetState(state, colors);
        for (int bottleId = 0; bottleId < config.BottleCount; bottleId++)
        {
            BottleData bottle = new() { Id = bottleId, Capacity = BottleCapacity };
            if (bottleId < colors.Count)
            {
                for (int layerIndex = 0; layerIndex < LayersPerColor; layerIndex++)
                {
                    bottle.Layers.Add(new WaterLayer(colors[bottleId], true));
                }
            }

            state.Bottles.Add(bottle);
        }

        int reservedEmptyBottleId = -1;
        List<LevelMove> inverseMoves = new(config.ScrambleSteps);
        HashSet<string> visitedDepthStates = new(StringComparer.Ordinal);
        int visitedNodes = 0;
        if (!TryBuildReverseMovePath(
                state,
                reservedEmptyBottleId,
                config.ScrambleSteps,
                random,
                inverseMoves,
                visitedDepthStates,
                ref visitedNodes))
        {
            knownSolution = new List<LevelMove>();
            return false;
        }

        knownSolution = inverseMoves.AsEnumerable().Reverse().ToList();
        PermuteBottlesAndKnownSolution(state, knownSolution, seed);
        ApplyInitialRevealRules(state);
        return ValidateGeneratedState(state, config);
    }

    private static void PermuteBottlesAndKnownSolution(GameState state, List<LevelMove> knownSolution, int seed)
    {
        int bottleCount = state.Bottles.Count;
        List<int> oldOrder = Enumerable.Range(0, bottleCount).ToList();
        Random random = new(MixSeed(seed, 97));
        Shuffle(oldOrder, random);

        int rotation = Math.Abs(seed % bottleCount);
        if (rotation > 0)
        {
            oldOrder = oldOrder.Skip(rotation).Concat(oldOrder.Take(rotation)).ToList();
        }

        if (oldOrder.SequenceEqual(Enumerable.Range(0, bottleCount)))
        {
            oldOrder.Reverse();
        }

        int[] oldToNew = new int[bottleCount];
        List<BottleData> reordered = new(bottleCount);
        for (int newIndex = 0; newIndex < bottleCount; newIndex++)
        {
            int oldIndex = oldOrder[newIndex];
            oldToNew[oldIndex] = newIndex;
            BottleData bottle = state.Bottles[oldIndex];
            bottle.Id = newIndex;
            reordered.Add(bottle);
        }

        state.Bottles.Clear();
        state.Bottles.AddRange(reordered);
        for (int moveIndex = 0; moveIndex < knownSolution.Count; moveIndex++)
        {
            LevelMove move = knownSolution[moveIndex];
            knownSolution[moveIndex] = new LevelMove(oldToNew[move.SourceBottleId], oldToNew[move.TargetBottleId]);
        }
    }

    private static bool TryBuildStructuredReverseState(
        LevelDifficultyConfig config,
        int seed,
        out GameState state,
        out List<LevelMove> knownSolution)
    {
        state = new GameState();
        knownSolution = new List<LevelMove>();
        if (config.EmptyBottleCount < 2 || config.BottleCount < config.ColorCount + 2)
        {
            return false;
        }

        Random random = new(MixSeed(seed, 31));
        List<WaterColor> colors = SupportedColors.Take(config.ColorCount).ToList();
        Shuffle(colors, random);
        ResetState(state, colors);

        for (int bottleId = 0; bottleId < config.BottleCount; bottleId++)
        {
            BottleData bottle = new() { Id = bottleId, Capacity = BottleCapacity };
            if (bottleId < colors.Count)
            {
                for (int layerIndex = 0; layerIndex < LayersPerColor; layerIndex++)
                {
                    bottle.Layers.Add(new WaterLayer(colors[bottleId], true));
                }
            }

            state.Bottles.Add(bottle);
        }

        int firstEmptyBottleId = config.ColorCount;
        int secondEmptyBottleId = config.ColorCount + 1;
        List<LevelMove> inverseMoves = new(config.ColorCount * 2 + 1);
        for (int colorIndex = 0; colorIndex < config.ColorCount; colorIndex++)
        {
            int firstTarget = colorIndex == 0 ? firstEmptyBottleId : colorIndex - 1;
            int secondTarget = colorIndex == 0
                ? secondEmptyBottleId
                : colorIndex == 1
                    ? firstEmptyBottleId
                    : colorIndex - 2;

            if (!TryApplyReverseMove(state, colorIndex, firstTarget, inverseMoves) ||
                !TryApplyReverseMove(state, colorIndex, secondTarget, inverseMoves))
            {
                return false;
            }
        }

        if (!TryApplyReverseMove(state, secondEmptyBottleId, config.ColorCount - 1, inverseMoves))
        {
            return false;
        }

        knownSolution = inverseMoves.AsEnumerable().Reverse().ToList();
        PermuteBottlesAndKnownSolution(state, knownSolution, seed);
        ApplyInitialRevealRules(state);
        return ValidateGeneratedState(state, config);
    }

    private static bool TryApplyReverseMove(
        GameState state,
        int sourceBottleId,
        int targetBottleId,
        List<LevelMove> inverseMoves)
    {
        if (sourceBottleId < 0 || sourceBottleId >= state.Bottles.Count ||
            targetBottleId < 0 || targetBottleId >= state.Bottles.Count ||
            sourceBottleId == targetBottleId)
        {
            return false;
        }

        BottleData source = state.Bottles[sourceBottleId];
        BottleData target = state.Bottles[targetBottleId];
        if (source.IsEmpty || target.IsFull)
        {
            return false;
        }

        WaterColor color = source.Layers[^1].Color;
        bool inverseDestinationWillAccept = source.Layers.Count == 1 || source.Layers[^2].Color == color;
        if (!inverseDestinationWillAccept || (!target.IsEmpty && target.Layers[^1].Color == color))
        {
            return false;
        }

        WaterLayer layer = source.Layers[^1];
        source.Layers.RemoveAt(source.Layers.Count - 1);
        target.Layers.Add(layer);
        inverseMoves.Add(new LevelMove(targetBottleId, sourceBottleId));
        return true;
    }

    private static List<ReverseMoveCandidate> BuildReverseMoveCandidates(
        GameState state,
        int reservedEmptyBottleId)
    {
        List<ReverseMoveCandidate> candidates = new();
        for (int sourceId = 0; sourceId < state.Bottles.Count; sourceId++)
        {
            if (sourceId == reservedEmptyBottleId)
            {
                continue;
            }

            BottleData source = state.Bottles[sourceId];
            if (source.IsEmpty)
            {
                continue;
            }

            WaterColor color = source.Layers[^1].Color;
            bool inverseDestinationWillAccept = source.Layers.Count == 1 || source.Layers[^2].Color == color;
            if (!inverseDestinationWillAccept)
            {
                continue;
            }

            for (int targetId = 0; targetId < state.Bottles.Count; targetId++)
            {
                if (targetId == sourceId || targetId == reservedEmptyBottleId)
                {
                    continue;
                }

                BottleData target = state.Bottles[targetId];
                if (target.IsFull || (!target.IsEmpty && target.Layers[^1].Color == color))
                {
                    continue;
                }

                candidates.Add(new ReverseMoveCandidate(sourceId, targetId));
            }
        }

        return candidates;
    }

    private static bool TryBuildReverseMovePath(
        GameState state,
        int reservedEmptyBottleId,
        int remainingSteps,
        Random random,
        List<LevelMove> inverseMoves,
        HashSet<string> visitedDepthStates,
        ref int visitedNodes)
    {
        if (remainingSteps == 0)
        {
            return !state.Bottles.All(bottle => bottle.IsEmpty || IsCompletedBottle(bottle))
                && state.Bottles.Any(bottle => bottle.IsEmpty)
                && LevelQualityEvaluator.Validate(state);
        }

        if (++visitedNodes > MaxReverseSearchNodes)
        {
            return false;
        }

        string depthKey = $"{remainingSteps}:{BuildColorLayoutKey(state)}";
        if (!visitedDepthStates.Add(depthKey))
        {
            return false;
        }

        int requiredRunBreakMoves = CountRequiredTopRunBreakMoves(state);
        if (requiredRunBreakMoves > remainingSteps)
        {
            return false;
        }

        List<ReverseMoveCandidate> candidates = BuildReverseMoveCandidates(state, reservedEmptyBottleId);
        if (requiredRunBreakMoves == remainingSteps)
        {
            candidates = candidates
                .Where(candidate => CountTopSameColorRun(state.Bottles[candidate.SourceBottleId]) > LevelQualityEvaluator.MaxBottleSameColorRun)
                .ToList();
        }

        Shuffle(candidates, random);
        candidates = candidates
            .OrderByDescending(candidate => CountTopSameColorRun(state.Bottles[candidate.SourceBottleId]))
            .ThenByDescending(candidate => CalculateSoftScoreAfterReverseMove(state, candidate))
            .ToList();
        foreach (ReverseMoveCandidate move in candidates)
        {
            BottleData source = state.Bottles[move.SourceBottleId];
            BottleData target = state.Bottles[move.TargetBottleId];
            WaterLayer layer = source.Layers[^1];
            source.Layers.RemoveAt(source.Layers.Count - 1);
            target.Layers.Add(layer);
            inverseMoves.Add(new LevelMove(move.TargetBottleId, move.SourceBottleId));

            if (TryBuildReverseMovePath(
                    state,
                    reservedEmptyBottleId,
                    remainingSteps - 1,
                    random,
                    inverseMoves,
                    visitedDepthStates,
                    ref visitedNodes))
            {
                return true;
            }

            inverseMoves.RemoveAt(inverseMoves.Count - 1);
            target.Layers.RemoveAt(target.Layers.Count - 1);
            source.Layers.Add(layer);
        }

        return false;
    }

    private static int CalculateSoftScoreAfterReverseMove(GameState state, ReverseMoveCandidate candidate)
    {
        BottleData source = state.Bottles[candidate.SourceBottleId];
        BottleData target = state.Bottles[candidate.TargetBottleId];
        WaterLayer layer = source.Layers[^1];
        source.Layers.RemoveAt(source.Layers.Count - 1);
        target.Layers.Add(layer);

        int score = LevelQualityEvaluator.CalculateInitialBottomPairDiversityScore(state);

        target.Layers.RemoveAt(target.Layers.Count - 1);
        source.Layers.Add(layer);
        return score;
    }

    private static string BuildColorLayoutKey(GameState state)
    {
        return string.Join(
            '|',
            state.Bottles.Select(bottle => string.Join(',', bottle.Layers.Select(layer => (int)layer.Color))));
    }

    private static void ApplyInitialRevealRules(GameState state)
    {
        foreach (BottleData bottle in state.Bottles)
        {
            for (int layerIndex = 0; layerIndex < bottle.Layers.Count; layerIndex++)
            {
                bottle.Layers[layerIndex].IsRevealed = layerIndex == bottle.Layers.Count - 1;
            }
        }
    }

    private static bool ValidateGeneratedState(GameState state, LevelDifficultyConfig config)
    {
        if (state.Bottles.Count != config.BottleCount || state.Bottles.Count(bottle => bottle.IsEmpty) < 1)
        {
            return false;
        }

        Dictionary<WaterColor, int> counts = new();
        foreach (BottleData bottle in state.Bottles)
        {
            if (bottle.Layers.Count > bottle.Capacity)
            {
                return false;
            }

            if (!bottle.IsEmpty && !bottle.Layers[^1].IsRevealed)
            {
                return false;
            }

            for (int i = 0; i < bottle.Layers.Count; i++)
            {
                WaterLayer layer = bottle.Layers[i];
                if (i < bottle.Layers.Count - 1 && layer.IsRevealed)
                {
                    return false;
                }

                counts[layer.Color] = counts.GetValueOrDefault(layer.Color) + 1;
            }
        }

        if (counts.Count != config.ColorCount || counts.Values.Any(count => count != LayersPerColor))
        {
            return false;
        }

        return !state.Bottles.All(bottle => bottle.IsEmpty || IsCompletedBottle(bottle));
    }

    private static void ValidateDifficultyConfig(LevelDifficultyConfig config)
    {
        if (config.ColorCount < 1 || config.ColorCount > SupportedColors.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(config), "Unsupported color count.");
        }

        if (config.EmptyBottleCount < 1 || config.BottleCount != config.ColorCount + config.EmptyBottleCount)
        {
            throw new ArgumentException("Bottle count must equal color count plus empty bottle count.", nameof(config));
        }

        if (config.ScrambleSteps < 0 || config.MaxGenerationAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(config), "Scramble steps and attempts cannot be negative.");
        }
    }

    private static int MixSeed(int seed, int attempt)
    {
        unchecked
        {
            uint mixed = (uint)seed;
            mixed ^= (uint)attempt * 0x9E3779B9u;
            mixed ^= mixed >> 16;
            mixed *= 0x85EBCA6Bu;
            mixed ^= mixed >> 13;
            return (int)mixed;
        }
    }

    private static void CopyState(GameState source, GameState target)
    {
        ResetState(target, source.Bags.Keys);
        foreach (BottleData sourceBottle in source.Bottles)
        {
            BottleData targetBottle = new()
            {
                Id = sourceBottle.Id,
                Capacity = sourceBottle.Capacity,
                IsCollected = sourceBottle.IsCollected
            };
            foreach (WaterLayer layer in sourceBottle.Layers)
            {
                targetBottle.Layers.Add(new WaterLayer(layer.Color, layer.IsRevealed));
            }

            target.Bottles.Add(targetBottle);
        }
    }

    private static void ResetState(GameState state, IEnumerable<WaterColor> colors)
    {
        List<WaterColor> distinctColors = colors.Distinct().ToList();
        state.Bottles.Clear();
        state.Bags.Clear();
        state.CollectedColorOrder.Clear();
        state.RequiredColorCount = distinctColors.Count;
        state.SelectedBottleId = null;
        state.IsGameOver = false;

        foreach (WaterColor color in distinctColors)
        {
            state.Bags[color] = new BagData(color);
        }
    }

    private static void PrintGeneratedStateDiagnostics(GameState state, string result)
    {
        GD.Print(
            $"CAULDRON_DIAG LevelGenerator.GenerateComplete result={result} " +
            $"bottles={state.Bottles.Count} RequiredColorCount={state.RequiredColorCount} " +
            $"bags={state.Bags.Count} actualColorCount={CountDistinctLayerColors(state)}");
    }

    private static int CountDistinctLayerColors(GameState state)
    {
        HashSet<WaterColor> colors = new();
        foreach (BottleData bottle in state.Bottles)
        {
            foreach (WaterLayer layer in bottle.Layers)
            {
                colors.Add(layer.Color);
            }
        }

        return colors.Count;
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
        int emptyBottleCount = _legacyRandom.RandiRange(1, 2);
        int nonEmptyBottleCount = LegacyBottleCount - emptyBottleCount;
        List<int> layerCounts = CreateLegacyRandomLayerCounts(nonEmptyBottleCount);
        List<WaterColor> colors = CreateLegacyShuffledColorPool();
        List<BottleData> bottles = new();
        int colorIndex = 0;

        for (int bottleId = 0; bottleId < LegacyBottleCount; bottleId++)
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

        ShuffleLegacy(bottles);
        for (int i = 0; i < bottles.Count; i++)
        {
            bottles[i].Id = i;
        }

        return bottles;
    }

    private List<int> CreateLegacyRandomLayerCounts(int bottleCount)
    {
        List<int> counts = Enumerable.Repeat(1, bottleCount).ToList();
        int remainingLayers = (LayersPerColor * LegacyColorCount) - bottleCount;

        while (remainingLayers > 0)
        {
            int index = _legacyRandom.RandiRange(0, bottleCount - 1);
            if (counts[index] >= BottleCapacity)
            {
                continue;
            }

            counts[index]++;
            remainingLayers--;
        }

        ShuffleLegacy(counts);
        return counts;
    }

    private List<WaterColor> CreateLegacyShuffledColorPool()
    {
        List<WaterColor> colors = new();
        foreach (WaterColor color in SupportedColors.Take(LegacyColorCount))
        {
            for (int i = 0; i < LayersPerColor; i++)
            {
                colors.Add(color);
            }
        }

        ShuffleLegacy(colors);
        return colors;
    }

    private void ShuffleLegacy<T>(IList<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = _legacyRandom.RandiRange(0, i);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }

    private static void Shuffle<T>(IList<T> items, Random random)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }

    private void EnsureLegacyRandomized()
    {
        if (_legacyRandomized)
        {
            return;
        }

        _legacyRandom.Randomize();
        _legacyRandomized = true;
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

    private static int CountTopSameColorRun(BottleData bottle)
    {
        if (bottle.Layers.Count == 0)
        {
            return 0;
        }

        WaterColor color = bottle.Layers[^1].Color;
        int count = 0;
        for (int i = bottle.Layers.Count - 1; i >= 0; i--)
        {
            if (bottle.Layers[i].Color != color)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static int CountRequiredTopRunBreakMoves(GameState state)
    {
        int moves = 0;
        foreach (BottleData bottle in state.Bottles)
        {
            moves += Math.Max(0, CountTopSameColorRun(bottle) - LevelQualityEvaluator.MaxBottleSameColorRun);
        }

        return moves;
    }

    private static void ValidateFixedTestState(GameState state)
    {
        Dictionary<WaterColor, int> layerCounts = SupportedColors
            .Take(LegacyColorCount)
            .ToDictionary(color => color, _ => 0);

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
            if (pair.Value != LayersPerColor)
            {
                GD.PushWarning($"Initial fixed test state has {pair.Value} {pair.Key} layers; expected {LayersPerColor}.");
            }
        }
    }

    private static void ValidateLegacyRandomState(GameState state)
    {
        if (state.Bottles.Count != LegacyBottleCount)
        {
            GD.PushWarning($"Legacy random state has {state.Bottles.Count} bottles; expected {LegacyBottleCount}.");
        }

        int emptyBottleCount = state.Bottles.Count(bottle => bottle.IsEmpty);
        if (emptyBottleCount < 1 || emptyBottleCount > 2)
        {
            GD.PushWarning($"Legacy random state has {emptyBottleCount} empty bottles; expected 1 or 2.");
        }
    }

    private readonly record struct ReverseMoveCandidate(int SourceBottleId, int TargetBottleId);
}
