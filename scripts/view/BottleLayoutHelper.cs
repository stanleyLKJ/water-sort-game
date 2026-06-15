#nullable enable

using System.Collections.Generic;
using Godot;

namespace WaterSortGame.View;

public static class BottleLayoutHelper
{
    private const float FourColumnStartX = 105f;
    private const float FourColumnSpacing = 170f;
    private const float ThreeColumnStartX = 160f;
    private const float ThreeColumnSpacing = 200f;
    private const float TopRowY = 520f;
    private const float BottomRowY = 830f;

    public static IReadOnlyList<Vector2> GetPositions(int bottleCount)
    {
        return bottleCount switch
        {
            6 => new[]
            {
                new Vector2(160f, TopRowY),
                new Vector2(360f, TopRowY),
                new Vector2(560f, TopRowY),
                new Vector2(160f, BottomRowY),
                new Vector2(360f, BottomRowY),
                new Vector2(560f, BottomRowY)
            },
            7 => new[]
            {
                new Vector2(105f, TopRowY),
                new Vector2(275f, TopRowY),
                new Vector2(445f, TopRowY),
                new Vector2(615f, TopRowY),
                new Vector2(190f, BottomRowY),
                new Vector2(360f, BottomRowY),
                new Vector2(530f, BottomRowY)
            },
            8 => new[]
            {
                new Vector2(105f, TopRowY),
                new Vector2(275f, TopRowY),
                new Vector2(445f, TopRowY),
                new Vector2(615f, TopRowY),
                new Vector2(105f, BottomRowY),
                new Vector2(275f, BottomRowY),
                new Vector2(445f, BottomRowY),
                new Vector2(615f, BottomRowY)
            },
            _ => CreateFallbackGrid(bottleCount)
        };
    }

    private static IReadOnlyList<Vector2> CreateFallbackGrid(int bottleCount)
    {
        int safeCount = Mathf.Max(0, bottleCount);
        GD.PushWarning($"No explicit bottle layout exists for {bottleCount} bottles. Using a safe four-column grid.");

        List<Vector2> positions = new(safeCount);
        const int maxColumns = 4;
        int rowCount = Mathf.CeilToInt(safeCount / (float)maxColumns);
        for (int row = 0; row < rowCount; row++)
        {
            int rowStartIndex = row * maxColumns;
            int rowItemCount = Mathf.Min(maxColumns, safeCount - rowStartIndex);
            float rowStartX = rowItemCount == 3
                ? ThreeColumnStartX
                : FourColumnStartX + ((maxColumns - rowItemCount) * FourColumnSpacing * 0.5f);

            for (int column = 0; column < rowItemCount; column++)
            {
                float spacing = rowItemCount == 3 ? ThreeColumnSpacing : FourColumnSpacing;
                positions.Add(new Vector2(rowStartX + (column * spacing), TopRowY + (row * (BottomRowY - TopRowY))));
            }
        }

        return positions;
    }
}
