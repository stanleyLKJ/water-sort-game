#nullable enable

namespace WaterSortGame.Model;

public sealed class PourResult
{
    public bool Success { get; init; }
    public string FailReason { get; init; } = string.Empty;
    public PourPlan? Plan { get; init; }

    public static PourResult Fail(string reason)
    {
        return new PourResult
        {
            Success = false,
            FailReason = reason
        };
    }

    public static PourResult Ok(PourPlan plan)
    {
        return new PourResult
        {
            Success = true,
            Plan = plan
        };
    }
}
