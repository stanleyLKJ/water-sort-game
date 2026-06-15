using System;

namespace WaterSortGame.Config;

public sealed class LevelDifficultyConfig
{
    public LevelDifficultyConfig(
        int levelNumber,
        int bottleCount,
        int colorCount,
        int emptyBottleCount,
        int scrambleSteps,
        int maxGenerationAttempts = 24)
    {
        LevelNumber = levelNumber;
        BottleCount = bottleCount;
        ColorCount = colorCount;
        EmptyBottleCount = emptyBottleCount;
        ScrambleSteps = scrambleSteps;
        MaxGenerationAttempts = maxGenerationAttempts;
    }

    public int LevelNumber { get; }
    public int BottleCount { get; }
    public int ColorCount { get; }
    public int EmptyBottleCount { get; }
    public int ScrambleSteps { get; }
    public int MaxGenerationAttempts { get; }

    public static LevelDifficultyConfig ForLevel(int levelNumber)
    {
        return levelNumber switch
        {
            1 or 2 => new LevelDifficultyConfig(levelNumber, 6, 4, 2, 16),
            3 or 4 or 5 => new LevelDifficultyConfig(levelNumber, 7, 5, 2, 20),
            6 or 7 => new LevelDifficultyConfig(levelNumber, 8, 6, 2, 24),
            _ => throw new ArgumentOutOfRangeException(
                nameof(levelNumber),
                levelNumber,
                "Level number must be between 1 and 7.")
        };
    }
}
