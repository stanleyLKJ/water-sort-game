#nullable enable

using System;
using System.Collections.Generic;

namespace WaterSortGame.Model;

public sealed class RunSessionState
{
    public const int MaxFlowerCount = 7;
    public const int LevelsPerFlower = 7;

    private static readonly string[] OpenFlowerIdOrder =
    {
        "pink_rose",
        "yellow_rose",
        "lavender"
    };

    private readonly List<string>[] _flowerSlotBatches = new List<string>[MaxFlowerCount];
    private readonly IReadOnlyList<string>[] _flowerSlotBatchViews = new IReadOnlyList<string>[MaxFlowerCount];
    private readonly Dictionary<string, int> _completedLevelCounts = new(StringComparer.Ordinal);

    public RunSessionState()
    {
        for (int i = 0; i < MaxFlowerCount; i++)
        {
            _flowerSlotBatches[i] = new List<string>();
            _flowerSlotBatchViews[i] = _flowerSlotBatches[i];
        }

        foreach (string flowerId in OpenFlowerIdOrder)
        {
            _completedLevelCounts[flowerId] = 0;
        }
    }

    public static IReadOnlyList<string> OpenFlowerIds => OpenFlowerIdOrder;

    public IReadOnlyList<IReadOnlyList<string>> FlowerSlotBatches => _flowerSlotBatchViews;

    public string? SelectedFlowerId { get; private set; }

    public bool HasSelectedFlower => !string.IsNullOrEmpty(SelectedFlowerId);

    public int? SelectedLevelNumber { get; private set; }

    public bool HasSeed { get; private set; }

    public bool HasPotion { get; private set; }

    public bool PendingPlanting { get; private set; }

    public string? PendingPlantingFlowerId { get; private set; }

    public bool IsGardenFull
    {
        get
        {
            foreach (List<string> flowerIds in _flowerSlotBatches)
            {
                if (flowerIds.Count == 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public void SelectTargetFlower(string flowerId)
    {
        if (string.IsNullOrWhiteSpace(flowerId))
        {
            throw new ArgumentException("Flower id cannot be empty.", nameof(flowerId));
        }

        if (!IsOpenFlowerId(flowerId))
        {
            throw new ArgumentException($"Flower id is not open for play: {flowerId}", nameof(flowerId));
        }

        SelectedFlowerId = flowerId;
        SelectedLevelNumber = null;
    }

    public bool TrySelectPlayableLevel(string flowerId, int levelNumber)
    {
        if (!CanPlayLevel(flowerId, levelNumber))
        {
            return false;
        }

        SelectedFlowerId = flowerId;
        SelectedLevelNumber = levelNumber;
        return true;
    }

    public bool CompleteSelectedLevelAndCreatePendingPlantingReward()
    {
        if (!HasSelectedFlower || SelectedLevelNumber == null)
        {
            return false;
        }

        string flowerId = SelectedFlowerId!;
        int levelNumber = SelectedLevelNumber.Value;

        if (GetLevelState(flowerId, levelNumber) != FlowerLevelState.Playable)
        {
            return false;
        }

        _completedLevelCounts[flowerId] = Math.Max(GetCompletedLevelCount(flowerId), levelNumber);
        SelectedLevelNumber = null;

        if (IsFlowerFull(flowerId))
        {
            return false;
        }

        HasSeed = true;
        HasPotion = true;
        PendingPlanting = true;
        PendingPlantingFlowerId = flowerId;
        return true;
    }

    public PlantingResult TryPlantPendingRewardAt(int slotIndex)
    {
        if (!PendingPlanting || !HasSeed || !HasPotion || string.IsNullOrEmpty(PendingPlantingFlowerId))
        {
            return PlantingResult.NoPendingReward;
        }

        if (slotIndex < 0 || slotIndex >= MaxFlowerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Flower slot index is out of range.");
        }

        string flowerId = PendingPlantingFlowerId;
        PlantingResult result = TryAddFlowerToSlot(slotIndex, flowerId);
        if (result != PlantingResult.Planted)
        {
            return result;
        }

        HasSeed = false;
        HasPotion = false;
        PendingPlanting = false;
        PendingPlantingFlowerId = null;
        return PlantingResult.Planted;
    }

    public bool AddFlower(string flowerId)
    {
        if (string.IsNullOrWhiteSpace(flowerId))
        {
            throw new ArgumentException("Flower id cannot be empty.", nameof(flowerId));
        }

        if (IsFlowerFull(flowerId))
        {
            return false;
        }

        for (int i = 0; i < _flowerSlotBatches.Length; i++)
        {
            if (!SlotContainsFlower(i, flowerId))
            {
                return TryAddFlowerToSlot(i, flowerId) == PlantingResult.Planted;
            }
        }

        return false;
    }

    public PlantingResult TryAddFlowerToSlot(int slotIndex, string flowerId)
    {
        if (string.IsNullOrWhiteSpace(flowerId))
        {
            throw new ArgumentException("Flower id cannot be empty.", nameof(flowerId));
        }

        if (slotIndex < 0 || slotIndex >= MaxFlowerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Flower slot index is out of range.");
        }

        List<string> slotFlowers = _flowerSlotBatches[slotIndex];
        if (slotFlowers.Contains(flowerId))
        {
            return PlantingResult.FlowerAlreadyInSlot;
        }

        if (IsFlowerFull(flowerId))
        {
            return PlantingResult.FlowerAlreadyFull;
        }

        slotFlowers.Add(flowerId);
        SortFlowerIds(slotFlowers);
        return PlantingResult.Planted;
    }

    public bool CanPlantPendingRewardAt(int slotIndex)
    {
        if (!PendingPlanting || !HasSeed || !HasPotion || string.IsNullOrEmpty(PendingPlantingFlowerId))
        {
            return false;
        }

        if (slotIndex < 0 || slotIndex >= MaxFlowerCount)
        {
            return false;
        }

        return !SlotContainsFlower(slotIndex, PendingPlantingFlowerId) && !IsFlowerFull(PendingPlantingFlowerId);
    }

    public bool SlotContainsFlower(int slotIndex, string flowerId)
    {
        if (slotIndex < 0 || slotIndex >= MaxFlowerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Flower slot index is out of range.");
        }

        return _flowerSlotBatches[slotIndex].Contains(flowerId);
    }

    public bool IsSlotEmpty(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxFlowerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Flower slot index is out of range.");
        }

        return _flowerSlotBatches[slotIndex].Count == 0;
    }

    public string GetSlotComboKey(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxFlowerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Flower slot index is out of range.");
        }

        return BuildComboKey(_flowerSlotBatches[slotIndex]);
    }

    public int GetCompletedLevelCount(string flowerId)
    {
        EnsureOpenFlowerId(flowerId);
        return _completedLevelCounts.TryGetValue(flowerId, out int completedCount) ? completedCount : 0;
    }

    public int? GetCurrentPlayableLevel(string flowerId)
    {
        int completedCount = GetCompletedLevelCount(flowerId);
        return completedCount >= LevelsPerFlower ? null : completedCount + 1;
    }

    public FlowerLevelState GetLevelState(string flowerId, int levelNumber)
    {
        EnsureLevelNumber(levelNumber);
        int completedCount = GetCompletedLevelCount(flowerId);

        if (levelNumber <= completedCount)
        {
            return FlowerLevelState.Completed;
        }

        if (levelNumber == completedCount + 1 && completedCount < LevelsPerFlower)
        {
            return FlowerLevelState.Playable;
        }

        return FlowerLevelState.Locked;
    }

    public bool CanPlayLevel(string flowerId, int levelNumber)
    {
        if (!IsOpenFlowerId(flowerId) || IsFlowerFull(flowerId))
        {
            return false;
        }

        return GetLevelState(flowerId, levelNumber) == FlowerLevelState.Playable;
    }

    public int GetFlowerPresenceCount(string flowerId)
    {
        int count = 0;
        foreach (List<string> slotFlowers in _flowerSlotBatches)
        {
            if (slotFlowers.Contains(flowerId))
            {
                count++;
            }
        }

        return count;
    }

    public bool IsFlowerFull(string flowerId)
    {
        return GetFlowerPresenceCount(flowerId) >= MaxFlowerCount;
    }

    public static bool IsOpenFlowerId(string flowerId)
    {
        return GetOpenFlowerOrderIndex(flowerId) >= 0;
    }

    public static string BuildComboKey(IReadOnlyList<string> flowerIds)
    {
        List<string> orderedUniqueFlowerIds = new();
        HashSet<string> seenFlowerIds = new(StringComparer.Ordinal);

        foreach (string flowerId in flowerIds)
        {
            if (string.IsNullOrWhiteSpace(flowerId) || !seenFlowerIds.Add(flowerId))
            {
                continue;
            }

            orderedUniqueFlowerIds.Add(flowerId);
        }

        SortFlowerIds(orderedUniqueFlowerIds);
        return string.Join("+", orderedUniqueFlowerIds);
    }

    private static void SortFlowerIds(List<string> flowerIds)
    {
        flowerIds.Sort(CompareFlowerIds);
    }

    private static int CompareFlowerIds(string left, string right)
    {
        int leftOrder = GetOpenFlowerOrderIndex(left);
        int rightOrder = GetOpenFlowerOrderIndex(right);

        if (leftOrder >= 0 && rightOrder >= 0)
        {
            return leftOrder.CompareTo(rightOrder);
        }

        if (leftOrder >= 0)
        {
            return -1;
        }

        if (rightOrder >= 0)
        {
            return 1;
        }

        return string.CompareOrdinal(left, right);
    }

    private static int GetOpenFlowerOrderIndex(string flowerId)
    {
        for (int i = 0; i < OpenFlowerIdOrder.Length; i++)
        {
            if (string.Equals(OpenFlowerIdOrder[i], flowerId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static void EnsureOpenFlowerId(string flowerId)
    {
        if (!IsOpenFlowerId(flowerId))
        {
            throw new ArgumentException($"Flower id is not open for play: {flowerId}", nameof(flowerId));
        }
    }

    private static void EnsureLevelNumber(int levelNumber)
    {
        if (levelNumber < 1 || levelNumber > LevelsPerFlower)
        {
            throw new ArgumentOutOfRangeException(nameof(levelNumber), levelNumber, "Level number is out of range.");
        }
    }
}

public enum PlantingResult
{
    Planted,
    NoPendingReward,
    FlowerAlreadyInSlot,
    FlowerAlreadyFull
}

public enum FlowerLevelState
{
    Completed,
    Playable,
    Locked
}
