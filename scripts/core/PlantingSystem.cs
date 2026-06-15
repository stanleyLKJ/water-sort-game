#nullable enable

using System;
using System.Collections.Generic;
using WaterSortGame.Model;

namespace WaterSortGame.Core;

public sealed class PlantingSystem
{
    public PlantingPageSnapshot CreateSnapshot(
        SaveData saveData,
        RunSessionState state,
        Func<string, string> displayNameResolver)
    {
        ArgumentNullException.ThrowIfNull(saveData);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(displayNameResolver);

        List<PlantingFlowerOption> flowers = new();
        foreach (string flowerId in RunSessionState.OpenFlowerIds)
        {
            int seedCount = 0;
            int potionCount = 0;
            if (saveData.WarehouseInventoryByFlower.TryGetValue(flowerId, out InventoryItemData? inventory) && inventory != null)
            {
                seedCount = inventory.SeedCount;
                potionCount = inventory.PotionCount;
            }

            flowers.Add(new PlantingFlowerOption(
                flowerId,
                displayNameResolver(flowerId),
                seedCount,
                potionCount,
                seedCount >= 1 && potionCount >= 1));
        }

        return new PlantingPageSnapshot(flowers, Array.Empty<PlantingSlotOption>());
    }

    public IReadOnlyList<HomeGardenPlantingSlotOption> CreateHomeGardenSlotOptions(
        SaveData saveData,
        RunSessionState state,
        Func<string, string> displayNameResolver)
    {
        ArgumentNullException.ThrowIfNull(saveData);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(displayNameResolver);

        List<HomeGardenPlantingSlotOption> slots = new();
        for (int i = 0; i < RunSessionState.MaxFlowerCount; i++)
        {
            bool hasPlantedFlowers = state.FlowerSlotBatches[i].Count > 0;
            bool hasPlantableFlowers = CreateAvailableFlowersForSlot(saveData, state, i, displayNameResolver).Count > 0;
            slots.Add(new HomeGardenPlantingSlotOption(
                i,
                hasPlantedFlowers || hasPlantableFlowers));
        }

        return slots;
    }

    public IReadOnlyList<PlantingFlowerOption> CreateAvailableFlowersForSlot(
        SaveData saveData,
        RunSessionState state,
        int slotIndex,
        Func<string, string> displayNameResolver)
    {
        ArgumentNullException.ThrowIfNull(saveData);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(displayNameResolver);

        if (slotIndex < 0 || slotIndex >= RunSessionState.MaxFlowerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Flower slot index is out of range.");
        }

        List<PlantingFlowerOption> flowers = new();
        foreach (string flowerId in RunSessionState.OpenFlowerIds)
        {
            if (state.SlotContainsFlower(slotIndex, flowerId))
            {
                continue;
            }

            int seedCount = 0;
            int potionCount = 0;
            if (saveData.WarehouseInventoryByFlower.TryGetValue(flowerId, out InventoryItemData? inventory) && inventory != null)
            {
                seedCount = inventory.SeedCount;
                potionCount = inventory.PotionCount;
            }

            if (seedCount < 1 || potionCount < 1)
            {
                continue;
            }

            flowers.Add(new PlantingFlowerOption(
                flowerId,
                displayNameResolver(flowerId),
                seedCount,
                potionCount,
                true));
        }

        return flowers;
    }

    public IReadOnlyList<PlantedFlowerOption> CreatePlantedFlowersForSlot(
        RunSessionState state,
        int slotIndex,
        Func<string, string> displayNameResolver)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(displayNameResolver);

        if (slotIndex < 0 || slotIndex >= RunSessionState.MaxFlowerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Flower slot index is out of range.");
        }

        List<PlantedFlowerOption> flowers = new();
        foreach (string flowerId in state.FlowerSlotBatches[slotIndex])
        {
            flowers.Add(new PlantedFlowerOption(flowerId, displayNameResolver(flowerId)));
        }

        return flowers;
    }

    public PlantingAttemptResult ValidatePlant(
        SaveData saveData,
        RunSessionState state,
        string flowerId,
        int slotIndex)
    {
        ArgumentNullException.ThrowIfNull(saveData);
        ArgumentNullException.ThrowIfNull(state);

        if (!RunSessionState.IsOpenFlowerId(flowerId))
        {
            return new PlantingAttemptResult(PlantingAttemptResultKind.InvalidFlower, "该花尚未开放");
        }

        if (slotIndex < 0 || slotIndex >= RunSessionState.MaxFlowerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Flower slot index is out of range.");
        }

        int seedCount = 0;
        int potionCount = 0;
        if (saveData.WarehouseInventoryByFlower.TryGetValue(flowerId, out InventoryItemData? existingInventory) && existingInventory != null)
        {
            seedCount = existingInventory.SeedCount;
            potionCount = existingInventory.PotionCount;
        }

        if (seedCount < 1 || potionCount < 1)
        {
            return new PlantingAttemptResult(PlantingAttemptResultKind.InsufficientInventory, "缺少种子或药剂");
        }

        if (state.SlotContainsFlower(slotIndex, flowerId))
        {
            return new PlantingAttemptResult(PlantingAttemptResultKind.FlowerAlreadyInSlot, "该花位已有这种花");
        }

        if (state.IsFlowerFull(flowerId))
        {
            return new PlantingAttemptResult(PlantingAttemptResultKind.FlowerAlreadyFull, "该花已种满，请选择其他花");
        }

        return new PlantingAttemptResult(PlantingAttemptResultKind.Ready, string.Empty);
    }

    public PlantingAttemptResult TryPlant(
        SaveData saveData,
        RunSessionState state,
        string flowerId,
        int slotIndex)
    {
        PlantingAttemptResult validation = ValidatePlant(saveData, state, flowerId, slotIndex);
        if (!validation.IsReady)
        {
            return validation;
        }

        PlantingResult addResult = state.TryAddFlowerToSlot(slotIndex, flowerId);
        if (addResult == PlantingResult.FlowerAlreadyFull)
        {
            return new PlantingAttemptResult(PlantingAttemptResultKind.FlowerAlreadyFull, "该花已种满，请选择其他花");
        }

        if (addResult == PlantingResult.FlowerAlreadyInSlot)
        {
            return new PlantingAttemptResult(PlantingAttemptResultKind.FlowerAlreadyInSlot, "该花位已有这种花");
        }

        if (addResult != PlantingResult.Planted)
        {
            return new PlantingAttemptResult(PlantingAttemptResultKind.Failed, "种植失败");
        }

        InventoryItemData inventory = saveData.GetOrCreateInventory(flowerId);
        inventory.SeedCount -= 1;
        inventory.PotionCount -= 1;
        inventory.Normalize();
        saveData.SetHomeSlot(slotIndex, state.FlowerSlotBatches[slotIndex]);
        saveData.Normalize();

        return new PlantingAttemptResult(PlantingAttemptResultKind.Planted, "种植成功");
    }

    public ShovelAttemptResult ValidateShovelAll(RunSessionState state, int slotIndex)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (slotIndex < 0 || slotIndex >= RunSessionState.MaxFlowerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Flower slot index is out of range.");
        }

        return state.FlowerSlotBatches[slotIndex].Count == 0
            ? new ShovelAttemptResult(ShovelAttemptResultKind.SlotEmpty, "该花位没有可铲除的花")
            : new ShovelAttemptResult(ShovelAttemptResultKind.Ready, string.Empty);
    }

    public ShovelAttemptResult ValidateShovelFlower(RunSessionState state, int slotIndex, string flowerId)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!RunSessionState.IsOpenFlowerId(flowerId))
        {
            return new ShovelAttemptResult(ShovelAttemptResultKind.InvalidFlower, "该花尚未开放");
        }

        if (slotIndex < 0 || slotIndex >= RunSessionState.MaxFlowerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Flower slot index is out of range.");
        }

        if (state.FlowerSlotBatches[slotIndex].Count == 0)
        {
            return new ShovelAttemptResult(ShovelAttemptResultKind.SlotEmpty, "该花位没有可铲除的花");
        }

        if (!state.SlotContainsFlower(slotIndex, flowerId))
        {
            return new ShovelAttemptResult(ShovelAttemptResultKind.FlowerNotInSlot, "该花位没有这种花");
        }

        return new ShovelAttemptResult(ShovelAttemptResultKind.Ready, string.Empty);
    }

    public ShovelAttemptResult TryShovelAll(SaveData saveData, RunSessionState state, int slotIndex)
    {
        ArgumentNullException.ThrowIfNull(saveData);
        ArgumentNullException.ThrowIfNull(state);

        ShovelAttemptResult validation = ValidateShovelAll(state, slotIndex);
        if (!validation.IsReady)
        {
            return validation;
        }

        if (!state.TryRemoveAllFlowersFromSlot(slotIndex, out string[] removedFlowerIds))
        {
            return new ShovelAttemptResult(ShovelAttemptResultKind.SlotEmpty, "该花位没有可铲除的花");
        }

        foreach (string flowerId in removedFlowerIds)
        {
            ReturnInventory(saveData, flowerId);
        }

        saveData.SetHomeSlot(slotIndex, state.FlowerSlotBatches[slotIndex]);
        saveData.Normalize();
        return new ShovelAttemptResult(ShovelAttemptResultKind.Shoveled, "铲花成功");
    }

    public ShovelAttemptResult TryShovelFlower(SaveData saveData, RunSessionState state, int slotIndex, string flowerId)
    {
        ArgumentNullException.ThrowIfNull(saveData);
        ArgumentNullException.ThrowIfNull(state);

        ShovelAttemptResult validation = ValidateShovelFlower(state, slotIndex, flowerId);
        if (!validation.IsReady)
        {
            return validation;
        }

        if (!state.TryRemoveFlowerFromSlot(slotIndex, flowerId))
        {
            return new ShovelAttemptResult(ShovelAttemptResultKind.FlowerNotInSlot, "该花位没有这种花");
        }

        ReturnInventory(saveData, flowerId);
        saveData.SetHomeSlot(slotIndex, state.FlowerSlotBatches[slotIndex]);
        saveData.Normalize();
        return new ShovelAttemptResult(ShovelAttemptResultKind.Shoveled, "铲花成功");
    }

    private static void ReturnInventory(SaveData saveData, string flowerId)
    {
        InventoryItemData inventory = saveData.GetOrCreateInventory(flowerId);
        inventory.SeedCount += 1;
        inventory.PotionCount += 1;
        inventory.Normalize();
    }
}

public sealed record PlantingPageSnapshot(
    IReadOnlyList<PlantingFlowerOption> Flowers,
    IReadOnlyList<PlantingSlotOption> Slots);

public sealed record PlantingFlowerOption(
    string FlowerId,
    string DisplayName,
    int SeedCount,
    int PotionCount,
    bool CanPlant);

public sealed record PlantedFlowerOption(
    string FlowerId,
    string DisplayName);

public sealed record PlantingSlotOption(
    int SlotIndex,
    string SlotLabel,
    IReadOnlyList<string> FlowerIds,
    string Batch);

public sealed record HomeGardenPlantingSlotOption(
    int SlotIndex,
    bool CanOpenFlowerList);

public sealed record PlantingAttemptResult(PlantingAttemptResultKind Kind, string Message)
{
    public bool IsSuccess => Kind == PlantingAttemptResultKind.Planted;
    public bool IsReady => Kind == PlantingAttemptResultKind.Ready;
}

public sealed record ShovelAttemptResult(ShovelAttemptResultKind Kind, string Message)
{
    public bool IsSuccess => Kind == ShovelAttemptResultKind.Shoveled;
    public bool IsReady => Kind == ShovelAttemptResultKind.Ready;
}

public enum PlantingAttemptResultKind
{
    Ready,
    Planted,
    InvalidFlower,
    InsufficientInventory,
    FlowerAlreadyInSlot,
    FlowerAlreadyFull,
    Failed
}

public enum ShovelAttemptResultKind
{
    Ready,
    Shoveled,
    InvalidFlower,
    SlotEmpty,
    FlowerNotInSlot,
    Failed
}
