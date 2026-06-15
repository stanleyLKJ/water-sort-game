#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using WaterSortGame.Model;

namespace WaterSortGame.Core;

public readonly record struct LevelMove(int SourceBottleId, int TargetBottleId);

public enum SolvabilityVerificationStatus
{
    Unsolvable,
    Solvable,
    BudgetExhausted,
    KnownSolutionInvalid
}

public sealed class SolvabilityVerificationResult
{
    public SolvabilityVerificationStatus Status { get; init; }
    public bool IsSolvable => Status == SolvabilityVerificationStatus.Solvable;
    public bool IsBudgetExhausted => Status == SolvabilityVerificationStatus.BudgetExhausted;
    public int VisitedStateCount { get; init; }
    public int SolutionDepth { get; init; }
    public bool UsedKnownSolution { get; init; }
    public bool HitStateLimit { get; init; }
    public bool HitDepthLimit { get; init; }
}

public sealed class LevelSolvabilityVerifier
{
    public const int DefaultMaxVisitedStates = 50_000;
    public const int DefaultMaxSearchDepth = 96;

    public LevelSolvabilityVerifier(
        int maxVisitedStates = DefaultMaxVisitedStates,
        int maxSearchDepth = DefaultMaxSearchDepth)
    {
        MaxVisitedStates = Math.Max(1, maxVisitedStates);
        MaxSearchDepth = Math.Max(1, maxSearchDepth);
    }

    public int MaxVisitedStates { get; }
    public int MaxSearchDepth { get; }

    public SolvabilityVerificationResult Verify(
        GameState state,
        IReadOnlyList<LevelMove>? knownSolution = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        SolverState initial = SolverState.FromGameState(state);
        if (IsWon(initial))
        {
            return new SolvabilityVerificationResult
            {
                Status = SolvabilityVerificationStatus.Solvable,
                VisitedStateCount = 1,
                SolutionDepth = 0,
                UsedKnownSolution = knownSolution != null
            };
        }

        if (knownSolution != null)
        {
            SolvabilityVerificationResult knownSolutionResult = VerifyKnownSolution(initial, knownSolution);
            if (knownSolutionResult.IsSolvable)
            {
                return knownSolutionResult;
            }
        }

        return Search(initial);
    }

    public SolvabilityVerificationResult VerifyKnownSolution(
        GameState state,
        IReadOnlyList<LevelMove> knownSolution)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(knownSolution);
        return VerifyKnownSolution(SolverState.FromGameState(state), knownSolution);
    }

    public static string BuildStateKey(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return SolverState.FromGameState(state).BuildKey();
    }

    private static bool TryKnownSolution(
        SolverState initial,
        IReadOnlyList<LevelMove> knownSolution,
        out int visitedStateCount)
    {
        SolverState current = initial.Clone();
        visitedStateCount = 1;

        foreach (LevelMove move in knownSolution)
        {
            if (!TryApplyMove(current, move.SourceBottleId, move.TargetBottleId, out SolverState next))
            {
                return false;
            }

            current = next;
            visitedStateCount++;
        }

        return IsWon(current);
    }

    private static SolvabilityVerificationResult VerifyKnownSolution(
        SolverState initial,
        IReadOnlyList<LevelMove> knownSolution)
    {
        if (IsWon(initial))
        {
            return new SolvabilityVerificationResult
            {
                Status = SolvabilityVerificationStatus.Solvable,
                VisitedStateCount = 1,
                SolutionDepth = 0,
                UsedKnownSolution = true
            };
        }

        bool isSolvable = TryKnownSolution(initial, knownSolution, out int visitedStateCount);
        return new SolvabilityVerificationResult
        {
            Status = isSolvable
                ? SolvabilityVerificationStatus.Solvable
                : SolvabilityVerificationStatus.KnownSolutionInvalid,
            VisitedStateCount = visitedStateCount,
            SolutionDepth = isSolvable ? knownSolution.Count : 0,
            UsedKnownSolution = true
        };
    }

    private SolvabilityVerificationResult Search(SolverState initial)
    {
        Queue<SearchNode> queue = new();
        HashSet<string> visited = new(StringComparer.Ordinal);
        queue.Enqueue(new SearchNode(initial, 0));
        visited.Add(initial.BuildKey());
        bool hitDepthLimit = false;

        while (queue.Count > 0 && visited.Count < MaxVisitedStates)
        {
            SearchNode node = queue.Dequeue();
            if (node.Depth >= MaxSearchDepth)
            {
                hitDepthLimit = true;
                continue;
            }

            int firstEmptyTarget = FindFirstEmptyBottle(node.State);
            for (int sourceIndex = 0; sourceIndex < node.State.Bottles.Length; sourceIndex++)
            {
                SolverBottle source = node.State.Bottles[sourceIndex];
                if (source.Layers.Count == 0 || IsCompletedBottle(source))
                {
                    continue;
                }

                for (int targetIndex = 0; targetIndex < node.State.Bottles.Length; targetIndex++)
                {
                    if (sourceIndex == targetIndex)
                    {
                        continue;
                    }

                    SolverBottle target = node.State.Bottles[targetIndex];
                    if (target.Layers.Count == 0 && targetIndex != firstEmptyTarget)
                    {
                        continue;
                    }

                    if (!TryApplyMove(node.State, sourceIndex, targetIndex, out SolverState next))
                    {
                        continue;
                    }

                    string key = next.BuildKey();
                    if (!visited.Add(key))
                    {
                        continue;
                    }

                    int nextDepth = node.Depth + 1;
                    if (IsWon(next))
                    {
                        return new SolvabilityVerificationResult
                        {
                            Status = SolvabilityVerificationStatus.Solvable,
                            VisitedStateCount = visited.Count,
                            SolutionDepth = nextDepth
                        };
                    }

                    queue.Enqueue(new SearchNode(next, nextDepth));
                    if (visited.Count >= MaxVisitedStates)
                    {
                        break;
                    }
                }

                if (visited.Count >= MaxVisitedStates)
                {
                    break;
                }
            }
        }

        bool hitStateLimit = visited.Count >= MaxVisitedStates;
        return new SolvabilityVerificationResult
        {
            Status = hitStateLimit || hitDepthLimit
                ? SolvabilityVerificationStatus.BudgetExhausted
                : SolvabilityVerificationStatus.Unsolvable,
            VisitedStateCount = visited.Count,
            HitStateLimit = hitStateLimit,
            HitDepthLimit = hitDepthLimit
        };
    }

    private static int FindFirstEmptyBottle(SolverState state)
    {
        for (int i = 0; i < state.Bottles.Length; i++)
        {
            if (state.Bottles[i].Layers.Count == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryApplyMove(
        SolverState state,
        int sourceIndex,
        int targetIndex,
        out SolverState next)
    {
        next = state;
        if (sourceIndex < 0 || sourceIndex >= state.Bottles.Length ||
            targetIndex < 0 || targetIndex >= state.Bottles.Length ||
            sourceIndex == targetIndex)
        {
            return false;
        }

        SolverBottle source = state.Bottles[sourceIndex];
        SolverBottle target = state.Bottles[targetIndex];
        if (source.Layers.Count == 0 || target.Layers.Count >= target.Capacity)
        {
            return false;
        }

        SolverLayer sourceTop = source.Layers[^1];
        if (!sourceTop.IsRevealed)
        {
            return false;
        }

        if (target.Layers.Count > 0 && target.Layers[^1].Color != sourceTop.Color)
        {
            return false;
        }

        int pourableAmount = 0;
        for (int i = source.Layers.Count - 1; i >= 0; i--)
        {
            SolverLayer layer = source.Layers[i];
            if (!layer.IsRevealed || layer.Color != sourceTop.Color)
            {
                break;
            }

            pourableAmount++;
        }

        int amount = Math.Min(pourableAmount, target.Capacity - target.Layers.Count);
        if (amount <= 0)
        {
            return false;
        }

        next = state.Clone();
        SolverBottle nextSource = next.Bottles[sourceIndex];
        SolverBottle nextTarget = next.Bottles[targetIndex];
        for (int i = 0; i < amount; i++)
        {
            nextSource.Layers.RemoveAt(nextSource.Layers.Count - 1);
            nextTarget.Layers.Add(new SolverLayer(sourceTop.Color, true));
        }

        if (nextSource.Layers.Count > 0)
        {
            SolverLayer revealed = nextSource.Layers[^1];
            nextSource.Layers[^1] = revealed with { IsRevealed = true };
        }

        return true;
    }

    private static bool IsWon(SolverState state)
    {
        foreach (SolverBottle bottle in state.Bottles)
        {
            if (bottle.Layers.Count == 0)
            {
                continue;
            }

            if (!IsCompletedBottle(bottle))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCompletedBottle(SolverBottle bottle)
    {
        if (bottle.Layers.Count != bottle.Capacity || bottle.Layers.Count == 0)
        {
            return false;
        }

        WaterColor color = bottle.Layers[0].Color;
        for (int i = 1; i < bottle.Layers.Count; i++)
        {
            if (bottle.Layers[i].Color != color)
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct SearchNode(SolverState State, int Depth);

    private readonly record struct SolverLayer(WaterColor Color, bool IsRevealed);

    private sealed class SolverBottle
    {
        public SolverBottle(int capacity)
        {
            Capacity = capacity;
        }

        public int Capacity { get; }
        public List<SolverLayer> Layers { get; } = new();

        public SolverBottle Clone()
        {
            SolverBottle clone = new(Capacity);
            clone.Layers.AddRange(Layers);
            return clone;
        }
    }

    private sealed class SolverState
    {
        private SolverState(SolverBottle[] bottles)
        {
            Bottles = bottles;
        }

        public SolverBottle[] Bottles { get; }

        public static SolverState FromGameState(GameState state)
        {
            SolverBottle[] bottles = new SolverBottle[state.Bottles.Count];
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                BottleData source = state.Bottles[i];
                SolverBottle bottle = new(source.Capacity);
                foreach (WaterLayer layer in source.Layers)
                {
                    bottle.Layers.Add(new SolverLayer(layer.Color, layer.IsRevealed));
                }

                bottles[i] = bottle;
            }

            return new SolverState(bottles);
        }

        public SolverState Clone()
        {
            SolverBottle[] bottles = new SolverBottle[Bottles.Length];
            for (int i = 0; i < Bottles.Length; i++)
            {
                bottles[i] = Bottles[i].Clone();
            }

            return new SolverState(bottles);
        }

        public string BuildKey()
        {
            StringBuilder builder = new(Bottles.Length * 24);
            for (int bottleIndex = 0; bottleIndex < Bottles.Length; bottleIndex++)
            {
                SolverBottle bottle = Bottles[bottleIndex];
                if (bottleIndex > 0)
                {
                    builder.Append('|');
                }

                builder.Append(bottle.Capacity - bottle.Layers.Count).Append(':');
                foreach (SolverLayer layer in bottle.Layers)
                {
                    builder.Append((int)layer.Color)
                        .Append(layer.IsRevealed ? 'R' : 'H')
                        .Append(',');
                }
            }

            return builder.ToString();
        }
    }
}
