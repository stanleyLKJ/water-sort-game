#nullable enable

using System;
using System.Collections.Generic;

namespace WaterSortGame.Model;

public sealed class RunSessionState
{
    public const int MaxFlowerCount = 7;

    private readonly string?[] _plantedFlowerIds = new string?[MaxFlowerCount];

    public IReadOnlyList<string?> PlantedFlowerIds => _plantedFlowerIds;

    public string? SelectedFlowerId { get; private set; }

    public bool HasSelectedFlower => !string.IsNullOrEmpty(SelectedFlowerId);

    public bool HasSeed { get; private set; }

    public bool HasPotion { get; private set; }

    public bool PendingPlanting { get; private set; }

    public bool IsGardenFull
    {
        get
        {
            foreach (string? flowerId in _plantedFlowerIds)
            {
                if (string.IsNullOrEmpty(flowerId))
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

        SelectedFlowerId = flowerId;
    }

    public bool CreatePendingPlantingReward()
    {
        if (!HasSelectedFlower || IsGardenFull)
        {
            return false;
        }

        HasSeed = true;
        HasPotion = true;
        PendingPlanting = true;
        return true;
    }

    public PlantingResult TryPlantPendingRewardAt(int slotIndex)
    {
        if (!PendingPlanting || !HasSeed || !HasPotion || !HasSelectedFlower)
        {
            return PlantingResult.NoPendingReward;
        }

        if (slotIndex < 0 || slotIndex >= MaxFlowerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Flower slot index is out of range.");
        }

        if (!string.IsNullOrEmpty(_plantedFlowerIds[slotIndex]))
        {
            return PlantingResult.SlotOccupied;
        }

        _plantedFlowerIds[slotIndex] = SelectedFlowerId;
        HasSeed = false;
        HasPotion = false;
        PendingPlanting = false;
        return PlantingResult.Planted;
    }

    public bool AddFlower(string flowerId)
    {
        if (string.IsNullOrWhiteSpace(flowerId))
        {
            throw new ArgumentException("Flower id cannot be empty.", nameof(flowerId));
        }

        if (IsGardenFull)
        {
            return false;
        }

        for (int i = 0; i < _plantedFlowerIds.Length; i++)
        {
            if (string.IsNullOrEmpty(_plantedFlowerIds[i]))
            {
                _plantedFlowerIds[i] = flowerId;
                return true;
            }
        }

        return false;
    }
}

public enum PlantingResult
{
    Planted,
    NoPendingReward,
    SlotOccupied
}
