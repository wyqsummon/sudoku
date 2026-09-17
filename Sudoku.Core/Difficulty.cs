namespace Sudoku.Core;

/// <summary>游戏难度档位。</summary>
public enum DifficultyLevel
{
    /// <summary>简单：只用唯一候选数即可完成。</summary>
    Easy = 0,

    /// <summary>中等：需要区块摒除或数对等技巧。</summary>
    Medium = 1,

    /// <summary>困难：需要三数组或更高级技巧（含阶段二的链技巧）。</summary>
    Hard = 2,
}

/// <summary>难度判定：由逻辑求解结果推导难度档位。</summary>
public static class Difficulty
{
    /// <summary>难度中文名。</summary>
    public static string Name(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Easy => "简单",
        DifficultyLevel.Medium => "中等",
        DifficultyLevel.Hard => "困难",
        _ => level.ToString(),
    };

    /// <summary>由逻辑求解结果判定难度。</summary>
    public static DifficultyLevel FromSolve(LogicalSolveResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        // 基础技巧无法推导完 → 属于困难档（需要更高级技巧或试数）
        if (!result.Solved)
        {
            return DifficultyLevel.Hard;
        }

        return result.MaxLevel switch
        {
            <= 2 => DifficultyLevel.Easy,
            <= 4 => DifficultyLevel.Medium,
            _ => DifficultyLevel.Hard,
        };
    }

    /// <summary>所有难度档位（按从易到难）。</summary>
    public static IReadOnlyList<DifficultyLevel> All { get; } = new[]
    {
        DifficultyLevel.Easy,
        DifficultyLevel.Medium,
        DifficultyLevel.Hard,
    };
}
