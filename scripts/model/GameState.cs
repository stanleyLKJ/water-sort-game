using System.Collections.Generic;

namespace WaterSortGame.Model;

public sealed class GameState
{
    public List<BottleData> Bottles { get; } = new();
    public Dictionary<WaterColor, BagData> Bags { get; } = new();
    public int? SelectedBottleId { get; set; }
    public bool IsGameOver { get; set; }
}
