using System.Collections.Generic;

namespace WaterSortGame.Model;

public sealed class BottleData
{
    public int Id { get; set; }
    public int Capacity { get; set; } = 4;
    public List<WaterLayer> Layers { get; } = new();
    public bool IsCollected { get; set; }
    public bool IsEmpty => Layers.Count == 0;
    public bool IsFull => Layers.Count >= Capacity;
    public int EmptySlots => Capacity - Layers.Count;

    public BottleData(int id)
    {
        Id = id;
    }
}
