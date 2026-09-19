namespace Sudoku.Core;

/// <summary>一局题目的评分结果。</summary>
/// <param name="Score">SE 风格评分（取解题路径上最难的一步，与 Sudoku Explainer 的做法一致）。</param>
/// <param name="Level">难度档位（由最难技巧的等级推出，与出题器口径一致）。</param>
/// <param name="Hardest">最难的一步所用技巧。</param>
/// <param name="StepCount">逻辑求解用掉的步数。</param>
/// <param name="Solved">是否能纯逻辑解完（不靠试数）。</param>
public sealed record RatingReport(double Score, DifficultyLevel Level, Technique Hardest, int StepCount, bool Solved)
{
    /// <summary>形如「困难 · 4.2」的显示文本。</summary>
    public string Display => $"{Difficulty.Name(Level)} · {Score:0.0}";

    /// <summary>最难一步的技巧中文名。</summary>
    public string HardestName => TechniqueInfo.Name(Hardest);
}

/// <summary>
/// SER 风格（Sudoku Explainer Rating）难度评分：给每种技巧一个基础分，
/// 再按链长/删除数量做小幅加成；整题取「最难的一步」作为评分——
/// 这正是 Sudoku Explainer 的核心口径（而不是把各步分数相加）。
/// </summary>
public static class DifficultyRating
{
    /// <summary>逻辑解不动（需要试数）时的保底评分。</summary>
    public const double UnsolvedScore = 9.0;

    /// <summary>技巧基础分（数值风格参照 Sudoku Explainer 的公开评分习惯）。</summary>
    public static double BaseScore(Technique technique) => technique switch
    {
        Technique.HiddenSingle => 1.5,
        Technique.NakedSingle => 2.3,
        Technique.LockedCandidatesPointing => 2.6,
        Technique.LockedCandidatesClaiming => 2.8,
        Technique.NakedPair => 3.0,
        Technique.XWing => 3.2,
        Technique.HiddenPair => 3.4,
        Technique.NakedTriple => 3.6,
        Technique.Swordfish => 3.8,
        Technique.HiddenTriple => 4.0,
        Technique.XYWing => 4.2,
        Technique.XChain => 5.0,
        Technique.XYChain => 6.5,
        Technique.Aic => 7.5,
        Technique.RemotePair => 5.5,
        Technique.FinnedXWing => 6.0,
        Technique.UniqueRectangle => 6.2,
        Technique.FinnedSwordfish => 7.0,
        Technique.BugPlusOne => 8.5,
        Technique.AlsXz => 8.6,
        Technique.SueDeCoq => 8.8,
        Technique.AlsChain => 9.0,
        Technique.ContinuousLoop => 9.2,
        _ => 2.0,
    };

    /// <summary>单步评分：基础分 + 链长加成 + 删除数量加成。</summary>
    public static double StepScore(TechniqueStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        double score = BaseScore(step.Technique);

        // 链越长越难：每多一条链接 +0.1，最多 +1.0
        if (step.LinkList.Count > 1)
        {
            score += Math.Min(1.0, 0.1 * (step.LinkList.Count - 1));
        }

        // 一次删掉多个候选数略难一点
        if (step.Eliminations.Count > 1)
        {
            score += 0.1 * (Math.Min(step.Eliminations.Count, 5) - 1);
        }

        return Math.Round(score, 1);
    }

    /// <summary>对一道题面评分。</summary>
    public static RatingReport Rate(Board puzzle, int maxLevel = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(puzzle);

        RatingReport report = Rate(LogicalSolver.Solve(puzzle, collectSteps: true, maxLevel));

        // 「十七数」这一档有两道门槛：题面恰好 17 个提示数（理论下限），**而且**必须用到高阶技巧。
        // 公开的 17 提示数全目录里绝大多数其实靠唯一候选数就能推完，如果只看提示数个数，
        // 这一档就会出现「十七数 · 2.3」这种名不副实的评分；所以这里要求难度已经到大师档才升级档位，
        // 否则按实际技巧评级（简单 / 中等 / …）。
        if (puzzle.FilledCount == SeventeenClues.ClueCount
            && report.Level == DifficultyLevel.Master
            && Solver.HasUniqueSolution(puzzle))
        {
            return report with { Level = DifficultyLevel.Seventeen };
        }

        return report;
    }

    /// <summary>用已有的逻辑求解结果评分（出题器复用求解结果，避免重复计算）。</summary>
    public static RatingReport Rate(LogicalSolveResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        double score = 1.0;
        Technique hardest = Technique.HiddenSingle;
        int steps = 0;

        foreach (TechniqueStep step in result.Steps)
        {
            steps++;

            double stepScore = StepScore(step);
            if (stepScore > score)
            {
                score = stepScore;
                hardest = step.Technique;
            }
        }

        if (!result.Solved)
        {
            score = Math.Max(score, UnsolvedScore);
            hardest = result.MaxLevel > 0 ? hardest : Technique.None;
        }

        return new RatingReport(score, Difficulty.FromSolve(result), hardest, steps, result.Solved);
    }

    /// <summary>
    /// 评分的星级（1~5 星，用于界面展示）。
    /// 注意：档位一律以 <see cref="RatingReport.Level"/>（由最难技巧等级推出）为准，
    /// 不从这个分数反推，避免两套口径互相打架。
    /// </summary>
    public static int Stars(double score) => score switch
    {
        < 2.0 => 1,
        < 3.0 => 2,
        < 4.4 => 3,
        < 6.5 => 4,
        _ => 5,
    };
}
