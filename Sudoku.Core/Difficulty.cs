namespace Sudoku.Core;

/// <summary>游戏难度档位。</summary>
public enum DifficultyLevel
{
    /// <summary>简单：只用唯一候选数即可完成。</summary>
    Easy = 0,

    /// <summary>中等：需要区块摒除或数对等技巧。</summary>
    Medium = 1,

    /// <summary>困难：需要三数组或中阶图案技巧。</summary>
    Hard = 2,

    /// <summary>专家：必须用到链级技巧（X 链 / XY 链 / AIC），或纯逻辑解不动。</summary>
    Expert = 3,

    /// <summary>大师：链级技巧不够，必须用到高阶技巧（带鳍鱼 / 唯一矩形 / BUG+1 / 远程数对 / ALS / 连续环）。</summary>
    Master = 4,

    /// <summary>
    /// 十七数：题面恰好只有 17 个提示数（数独的理论下限），唯一解，而且**必须用到高阶技巧**。
    /// 公开的 17 提示数全目录里绝大多数其实靠唯一候选数就能推完（49157 道里有 21932 道如此），
    /// 本作只取其中约 1900 道「纯逻辑可解且必须用高阶技巧」的题目当母题，
    /// 所以这一档既是 17 提示数的题型，又是难度不低于大师档的档位（实测 6.4 ~ 9.2 分）。
    /// </summary>
    Seventeen = 5,
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
        DifficultyLevel.Expert => "专家",
        DifficultyLevel.Master => "大师",
        DifficultyLevel.Seventeen => "十七数",
        _ => level.ToString(),
    };

    /// <summary>难度说明（菜单上显示）。</summary>
    public static string Description(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Easy => "只需唯一候选数即可完成",
        DifficultyLevel.Medium => "需要区块摒除与数对",
        DifficultyLevel.Hard => "需要三数组或中阶图案技巧",
        DifficultyLevel.Expert => "需要链级技巧（X 链 / XY 链 / AIC）",
        DifficultyLevel.Master => "链级技巧不够用，必须靠高阶技巧（带鳍鱼 / 唯一矩形 / BUG+1 / 远程数对 / ALS / 连续环）",
        DifficultyLevel.Seventeen => "恰好 17 个提示数（理论下限），且必须用到高阶技巧（ALS / BUG+1 / 唯一矩形 等）",
        _ => string.Empty,
    };

    /// <summary>
    /// 是否为「大师」档专属的高阶技巧（阶段四新增 + 阶段九新增）。
    /// 只要解题路径上用到其中任何一种，这道题就属于大师档——新技巧都放在这个新难度里。
    /// </summary>
    public static bool IsAdvancedTechnique(Technique technique) => technique is
        Technique.RemotePair or
        Technique.UniqueRectangle or
        Technique.FinnedXWing or
        Technique.FinnedSwordfish or
        Technique.BugPlusOne or
        Technique.AlsXz or
        Technique.SueDeCoq or
        Technique.AlsChain or
        Technique.ContinuousLoop;

    /// <summary>由逻辑求解结果判定难度。</summary>
    public static DifficultyLevel FromSolve(LogicalSolveResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        // 基础技巧无法推导完 → 属于专家档（需要更高级技巧或试数）
        if (!result.Solved)
        {
            return DifficultyLevel.Expert;
        }

        // 用到阶段四高阶技巧（带鳍鱼 / 唯一矩形 / BUG+1 / 远程数对）→ 大师
        // 注意：collectSteps=false 时 Steps 为空，此时用求解器记录的标记判断，避免依赖是否收集了步骤
        bool advanced = result.UsesAdvancedTechnique || result.Steps.Any(s => IsAdvancedTechnique(s.Technique));
        if (advanced)
        {
            return DifficultyLevel.Master;
        }

        return result.MaxLevel switch
        {
            <= 2 => DifficultyLevel.Easy,
            <= 4 => DifficultyLevel.Medium,
            <= 6 => DifficultyLevel.Hard,
            _ => DifficultyLevel.Expert,
        };
    }

    /// <summary>所有难度档位（按从易到难）。</summary>
    public static IReadOnlyList<DifficultyLevel> All { get; } = new[]
    {
        DifficultyLevel.Easy,
        DifficultyLevel.Medium,
        DifficultyLevel.Hard,
        DifficultyLevel.Expert,
        DifficultyLevel.Master,
        DifficultyLevel.Seventeen,
    };
}
