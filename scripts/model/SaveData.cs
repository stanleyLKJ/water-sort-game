#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WaterSortGame.Model;

public sealed class SaveData
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("level_progress_by_flower")]
    public Dictionary<string, int> LevelProgressByFlower { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("warehouse_inventory_by_flower")]
    public Dictionary<string, InventoryItemData> WarehouseInventoryByFlower { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("home_slots_by_slot")]
    public Dictionary<string, HomeSlotSaveData> HomeSlotsBySlot { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("tutorial_bubbles_shown")]
    public Dictionary<string, bool> TutorialBubblesShown { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("settings")]
    public SettingsData Settings { get; set; } = SettingsData.CreateDefault();

    public static SaveData CreateDefault()
    {
        SaveData data = new();
        data.Normalize();
        return data;
    }

    public static SaveData FromRunSessionState(RunSessionState state, SettingsData? settings = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        SaveData data = CreateDefault();
        data.Settings = settings?.Clone() ?? SettingsData.CreateDefault();

        foreach (string flowerId in RunSessionState.OpenFlowerIds)
        {
            data.LevelProgressByFlower[flowerId] = state.GetCompletedLevelCount(flowerId);
        }

        for (int i = 0; i < RunSessionState.MaxFlowerCount; i++)
        {
            string slotKey = BuildSlotKey(i);
            data.HomeSlotsBySlot[slotKey] = HomeSlotSaveData.Create(i, state.FlowerSlotBatches[i]);
        }

        data.Normalize();
        return data;
    }

    public void Normalize()
    {
        SchemaVersion = CurrentSchemaVersion;
        LevelProgressByFlower ??= new Dictionary<string, int>(StringComparer.Ordinal);
        WarehouseInventoryByFlower ??= new Dictionary<string, InventoryItemData>(StringComparer.Ordinal);
        HomeSlotsBySlot ??= new Dictionary<string, HomeSlotSaveData>(StringComparer.Ordinal);
        TutorialBubblesShown ??= new Dictionary<string, bool>(StringComparer.Ordinal);
        Settings ??= SettingsData.CreateDefault();
        Settings.Normalize();

        foreach (string flowerId in RunSessionState.OpenFlowerIds)
        {
            LevelProgressByFlower[flowerId] = ClampLevelProgress(LevelProgressByFlower.GetValueOrDefault(flowerId));

            if (!WarehouseInventoryByFlower.TryGetValue(flowerId, out InventoryItemData? inventory) || inventory == null)
            {
                inventory = new InventoryItemData();
                WarehouseInventoryByFlower[flowerId] = inventory;
            }

            inventory.Normalize();
        }

        for (int i = 0; i < RunSessionState.MaxFlowerCount; i++)
        {
            string slotKey = BuildSlotKey(i);
            if (!HomeSlotsBySlot.TryGetValue(slotKey, out HomeSlotSaveData? slot) || slot == null)
            {
                slot = HomeSlotSaveData.Create(i, Array.Empty<string>());
                HomeSlotsBySlot[slotKey] = slot;
            }

            slot.SlotIndex = i;
            slot.Normalize();
        }
    }

    public void SetLevelProgress(string flowerId, int completedCount)
    {
        if (string.IsNullOrWhiteSpace(flowerId))
        {
            throw new ArgumentException("Flower id cannot be empty.", nameof(flowerId));
        }

        LevelProgressByFlower ??= new Dictionary<string, int>(StringComparer.Ordinal);
        LevelProgressByFlower[flowerId] = ClampLevelProgress(completedCount);
    }

    public InventoryItemData GetOrCreateInventory(string flowerId)
    {
        if (string.IsNullOrWhiteSpace(flowerId))
        {
            throw new ArgumentException("Flower id cannot be empty.", nameof(flowerId));
        }

        WarehouseInventoryByFlower ??= new Dictionary<string, InventoryItemData>(StringComparer.Ordinal);
        if (!WarehouseInventoryByFlower.TryGetValue(flowerId, out InventoryItemData? inventory) || inventory == null)
        {
            inventory = new InventoryItemData();
            WarehouseInventoryByFlower[flowerId] = inventory;
        }

        inventory.Normalize();
        return inventory;
    }

    public void SetHomeSlot(int zeroBasedSlotIndex, IReadOnlyList<string> flowerIds)
    {
        if (zeroBasedSlotIndex < 0 || zeroBasedSlotIndex >= RunSessionState.MaxFlowerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(zeroBasedSlotIndex), zeroBasedSlotIndex, "Flower slot index is out of range.");
        }

        HomeSlotsBySlot ??= new Dictionary<string, HomeSlotSaveData>(StringComparer.Ordinal);
        string slotKey = BuildSlotKey(zeroBasedSlotIndex);
        HomeSlotSaveData slot = HomeSlotSaveData.Create(zeroBasedSlotIndex, flowerIds);
        slot.Normalize();
        HomeSlotsBySlot[slotKey] = slot;
    }

    public static string BuildSlotKey(int zeroBasedSlotIndex)
    {
        return (zeroBasedSlotIndex + 1).ToString("00");
    }

    private static int ClampLevelProgress(int completedCount)
    {
        return Math.Clamp(completedCount, 0, RunSessionState.LevelsPerFlower);
    }
}

public sealed class InventoryItemData
{
    [JsonPropertyName("seed_count")]
    public int SeedCount { get; set; }

    [JsonPropertyName("potion_count")]
    public int PotionCount { get; set; }

    public void Normalize()
    {
        SeedCount = Math.Max(0, SeedCount);
        PotionCount = Math.Max(0, PotionCount);
    }
}

public sealed class HomeSlotSaveData
{
    [JsonPropertyName("slot")]
    public int SlotIndex { get; set; }

    [JsonPropertyName("flower_ids")]
    public List<string> FlowerIds { get; set; } = new();

    [JsonPropertyName("batch")]
    public string Batch { get; set; } = string.Empty;

    public static HomeSlotSaveData Create(int slotIndex, IReadOnlyList<string> flowerIds)
    {
        return new HomeSlotSaveData
        {
            SlotIndex = slotIndex,
            FlowerIds = new List<string>(flowerIds)
        };
    }

    public void Normalize()
    {
        FlowerIds ??= new List<string>();

        List<string> cleanFlowerIds = new();
        HashSet<string> seenFlowerIds = new(StringComparer.Ordinal);
        foreach (string flowerId in FlowerIds)
        {
            if (string.IsNullOrWhiteSpace(flowerId) || !seenFlowerIds.Add(flowerId))
            {
                continue;
            }

            cleanFlowerIds.Add(flowerId);
        }

        Batch = RunSessionState.BuildComboKey(cleanFlowerIds);
        FlowerIds = Batch.Length == 0
            ? new List<string>()
            : new List<string>(Batch.Split('+', StringSplitOptions.RemoveEmptyEntries));
    }
}
