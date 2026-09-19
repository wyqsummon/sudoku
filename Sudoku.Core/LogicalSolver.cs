namespace Sudoku.Core;

/// <summary>人工解题技巧：基础集（阶段一）+ 链级技巧（阶段二）。</summary>
public enum Technique
{
    None = 0,
    NakedSingle = 1,
    HiddenSingle = 2,
    LockedCandidatesPointing = 3,
    LockedCandidatesClaiming = 4,
    NakedPair = 5,
    HiddenPair = 6,
    NakedTriple = 7,
    HiddenTriple = 8,

    // ===== 阶段二：链级技巧 =====
    XWing = 9,
    Swordfish = 10,
    XYWing = 11,
    XChain = 12,
    XYChain = 13,
    Aic = 14,

    // ===== 阶段四：高阶技巧 =====
    RemotePair = 15,
    UniqueRectangle = 16,
    FinnedXWing = 17,
    FinnedSwordfish = 18,
    BugPlusOne = 19,

    // ===== 阶段九：集合类与环类高阶技巧 =====
    AlsXz = 20,
    SueDeCoq = 21,
    AlsChain = 22,
    ContinuousLoop = 23,
}

/// <summary>技巧的难度等级与中文名。</summary>
public static class TechniqueInfo
{
    /// <summary>难度等级：数值越大越难（1 最简单）。</summary>
    public static int Level(Technique technique) => technique switch
    {
        Technique.None => 0,
        Technique.NakedSingle => 1,
        Technique.HiddenSingle => 2,
        Technique.LockedCandidatesPointing or Technique.LockedCandidatesClaiming => 3,
        Technique.NakedPair or Technique.HiddenPair => 4,
        Technique.NakedTriple or Technique.HiddenTriple => 5,
        Technique.XWing or Technique.Swordfish or Technique.XYWing => 6,
        Technique.XChain or Technique.RemotePair => 7,
        Technique.XYChain or Technique.UniqueRectangle or Technique.FinnedXWing => 8,
        Technique.Aic or Technique.FinnedSwordfish => 9,
        Technique.BugPlusOne => 10,
        Technique.AlsXz => 11,
        Technique.SueDeCoq => 12,
        Technique.AlsChain or Technique.ContinuousLoop => 13,
        _ => 13,
    };

    /// <summary>技巧中文名。</summary>
    public static string Name(Technique technique) => technique switch
    {
        Technique.None => "无",
        Technique.NakedSingle => "唯一候选数",
        Technique.HiddenSingle => "隐藏唯一候选数",
        Technique.LockedCandidatesPointing => "区块摒除（宫内指向）",
        Technique.LockedCandidatesClaiming => "区块摒除（线内归属）",
        Technique.NakedPair => "显性数对",
        Technique.HiddenPair => "隐性数对",
        Technique.NakedTriple => "显性三数组",
        Technique.HiddenTriple => "隐性三数组",
        Technique.XWing => "X 翼（矩形对角线）",
        Technique.Swordfish => "剑鱼（三行三列）",
        Technique.XYWing => "XY 翼",
        Technique.XChain => "X 链（单数字链）",
        Technique.XYChain => "XY 链（双值格链）",
        Technique.Aic => "交替推导链（AIC）",
        Technique.RemotePair => "远程数对",
        Technique.UniqueRectangle => "唯一矩形",
        Technique.FinnedXWing => "带鳍 X 翼",
        Technique.FinnedSwordfish => "带鳍剑鱼",
        Technique.BugPlusOne => "BUG+1（双值通用坟墓）",
        Technique.AlsXz => "ALS-XZ（准锁定集）",
        Technique.SueDeCoq => "Sue de Coq（双翼锁定集）",
        Technique.AlsChain => "ALS 链（ALS-XY-Wing 等）",
        Technique.ContinuousLoop => "连续环（Continuous Nice Loop）",
        _ => technique.ToString(),
    };

    /// <summary>是否为链级技巧（会在棋盘上画出强弱链）。</summary>
    public static bool IsChain(Technique technique) =>
        technique is Technique.XChain or Technique.XYChain or Technique.Aic or Technique.RemotePair
            or Technique.ContinuousLoop;

    /// <summary>一句话技巧说明（用于提示面板的标题下方）。</summary>
    public static string Summary(Technique technique) => technique switch
    {
        Technique.NakedSingle => "某格只剩一个候选数，直接填入。",
        Technique.HiddenSingle => "某数字在一个行/列/宫内只剩一格能放，直接填入。",
        Technique.LockedCandidatesPointing => "某数字在一个宫内只出现在同一行（列），该行（列）的其他格就不能再放它。",
        Technique.LockedCandidatesClaiming => "某数字在一行（列）内只出现在同一宫，该宫的其他格就不能再放它。",
        Technique.NakedPair => "同单元内两格的候选数恰好是同样的两个数字，可删除该单元其他格的这两个数字。",
        Technique.HiddenPair => "同单元内两个数字只出现在同样的两格，这两格就只保留这两个数字。",
        Technique.NakedTriple => "同单元内三格的候选数只由三个数字组成，可删除该单元其他格的这三个数字。",
        Technique.HiddenTriple => "同单元内三个数字只出现在同样的三格，这三格就只保留这三个数字。",
        Technique.XWing => "某数字在两行中只出现在相同的两列，则可从这两列的其他格删除该数字。",
        Technique.Swordfish => "某数字在三行中只出现在相同的三列，则可从这三列的其他格删除该数字。",
        Technique.XYWing => "以双值格为枢纽，两个翼格共享第三个数字，可删除同时看见两翼的该数字。",
        Technique.XChain => "同一数字的强链与弱链交替成链，链两端的共同可见格可删除该数字。",
        Technique.XYChain => "以双值格串联成链，起点与终点共同可见的格可删除相关数字。",
        Technique.Aic => "候选数之间强弱交替推断，链两端共同可见的候选数可删除。",
        Technique.RemotePair => "一串只含同样两个数字的双值格用共轭对串起来，两端一个填 a、一个填 b，共同可见处不能填这两个数字。",
        Technique.UniqueRectangle => "四格在两行两列两宫内都只能填同样两个数字会形成两个解，据此删除多余候选数。",
        Technique.FinnedXWing => "X 翼的形状多出几个「鳍格」（同一宫内），覆盖列上能看见鳍格的位置可删除该数字。",
        Technique.FinnedSwordfish => "剑鱼的形状多出几个「鳍格」（同一宫内），覆盖行上能看见鳍格的位置可删除该数字。",
        Technique.BugPlusOne => "除一格以外全是双值格且每个数字在单元内恰好出现两次时，那一格里出现三次的候选数就是答案。",
        Technique.AlsXz => "两个「只差一个候选数就填满」的格组（ALS）之间若存在受限公共候选数，则它们的另一个公共候选数至少出现在其中一组里，可据此删除。",
        Technique.SueDeCoq => "行/列与宫交叉处，两侧各取几格合起来格数与候选数一样多时，只在宫侧出现的候选数只能落在宫侧那几格，只在线侧出现的只能落在线侧那几格。",
        Technique.AlsChain => "把多个 ALS 用受限公共候选数串成链，链首链尾共同含有的候选数必出现在其一，可据此删除。",
        Technique.ContinuousLoop => "强弱链交替成环且每个候选数正好一强一弱时，环上候选数按交替的两组取真/假，可删掉单元里的同类候选数与双值格的多余候选数。",
        _ => string.Empty,
    };
}

/// <summary>某个格中的某个候选数。</summary>
public readonly record struct CandidateRef(int Cell, int Digit)
{
    /// <summary>如 R3C5(7)。</summary>
    public override string ToString() => $"R{SudokuGrid.Row(Cell) + 1}C{SudokuGrid.Col(Cell) + 1}({Digit})";
}

/// <summary>
/// 链上的一条推断关系：两个候选数之间的强链或弱链。
/// 强链（IsStrong=true）：两者必有一个成立；弱链：两者不能同时成立。
/// </summary>
public readonly record struct TechniqueLink(CandidateRef From, CandidateRef To, bool IsStrong, string Reason)
{
    /// <summary>关系中文名。</summary>
    public string KindName => IsStrong ? "强链" : "弱链";

    /// <summary>形如 R1C2(3) == R1C8(3) 的表达式（== 强链，-- 弱链）。</summary>
    public override string ToString() => $"{From} {(IsStrong ? "==" : "--")} {To}";
}

/// <summary>逻辑求解的一步：落子或删除候选数。</summary>
public sealed record TechniqueStep(
    Technique Technique,
    IReadOnlyList<int> HighlightCells,
    int PlaceIndex,
    int PlaceDigit,
    IReadOnlyList<CandidateRef> Eliminations,
    string Description,
    IReadOnlyList<TechniqueLink>? Links = null,
    IReadOnlyList<string>? Derivation = null,
    IReadOnlyList<HintMark>? Marks = null)
{
    /// <summary>本步技巧的难度等级。</summary>
    public int Level => TechniqueInfo.Level(Technique);

    /// <summary>是否为落子步骤。</summary>
    public bool IsPlacement => PlaceIndex >= 0;

    /// <summary>本步涉及的强弱链（可能为空）。</summary>
    public IReadOnlyList<TechniqueLink> LinkList => Links ?? Array.Empty<TechniqueLink>();

    /// <summary>分段式提示用的逐条推导说明（可能为空）。</summary>
    public IReadOnlyList<string> DerivationSteps => Derivation ?? Array.Empty<string>();

    /// <summary>技巧自己给出的棋盘高亮标记（可能为空，界面会兜底推导）。</summary>
    public IReadOnlyList<HintMark> MarkList => Marks ?? Array.Empty<HintMark>();

    /// <summary>该步骤真正要画的全部标记：技巧给了就用，没给就按结构 + 结论兜底推导。</summary>
    public IReadOnlyList<HintMark> VisualMarks =>
        Marks is { Count: > 0 } ? Marks : HintMarks.Synthesize(this);

    /// <summary>按推导进度（0 基）取应当显示的标记；传负数表示全部显示。</summary>
    public IReadOnlyList<HintMark> MarksAt(int stage) => HintMarks.UpTo(VisualMarks, stage);

    /// <summary>最后一个标记出现的推导进度。</summary>
    public int LastMarkStage => HintMarks.LastStage(VisualMarks);
}

/// <summary>逻辑求解结果。</summary>
public sealed record LogicalSolveResult(
    bool Solved,
    bool Stuck,
    IReadOnlyList<TechniqueStep> Steps,
    Technique MaxTechnique,
    int MaxLevel,
    Board Result,
    bool UsesAdvancedTechnique = false)
{
    /// <summary>本次求解用到的最高难度技巧的中文名。</summary>
    public string MaxTechniqueName => TechniqueInfo.Name(MaxTechnique);
}

/// <summary>
/// 用人类技巧推导盘面的求解器：驱动难度评级（阶段一）与分段式提示系统（阶段二）。
/// 不使用试数，只按技巧难度从低到高逐步推进。
/// </summary>
public static class LogicalSolver
{
    /// <summary>
    /// 用逻辑技巧求解。若卡住（需要更高级技巧）则 <see cref="LogicalSolveResult.Stuck"/> 为 true。
    /// </summary>
    /// <param name="board">待解盘面。</param>
    /// <param name="collectSteps">是否收集每一步的详细过程（提示系统需要）。</param>
    /// <param name="maxLevel">允许使用的最高技巧等级。</param>
    public static LogicalSolveResult Solve(Board board, bool collectSteps = false, int maxLevel = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(board);

        var state = new State(board);
        var steps = new List<TechniqueStep>();
        var maxTechnique = Technique.None;
        int maxLevelUsed = 0;
        bool usesAdvanced = false;

        while (!state.IsComplete)
        {
            if (state.HasContradiction)
            {
                return new LogicalSolveResult(false, true, steps, maxTechnique, maxLevelUsed, state.ToBoard(), usesAdvanced);
            }

            TechniqueStep? step = FindStep(state, maxLevel);
            if (step is null)
            {
                return new LogicalSolveResult(false, true, steps, maxTechnique, maxLevelUsed, state.ToBoard(), usesAdvanced);
            }

            state.Apply(step);
            if (collectSteps)
            {
                steps.Add(step);
            }

            if (Difficulty.IsAdvancedTechnique(step.Technique))
            {
                usesAdvanced = true;
            }

            if (step.Level > maxLevelUsed)
            {
                maxLevelUsed = step.Level;
                maxTechnique = step.Technique;
            }
        }

        return new LogicalSolveResult(true, false, steps, maxTechnique, maxLevelUsed, state.ToBoard(), usesAdvanced);
    }

    /// <summary>找出当前盘面可用的一步（按难度从低到高，返回第一个可用技巧）。</summary>
    public static TechniqueStep? FindStep(Board board, int maxLevel = int.MaxValue, int[]? candidateOverride = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        return FindStep(new State(board, candidateOverride), maxLevel);
    }

    /// <summary>列出当前盘面所有可用的技巧步骤（同一步棋可能被多个技巧命中，各自独立列出）。</summary>
    public static IReadOnlyList<TechniqueStep> FindAllSteps(
        Board board,
        int maxLevel = int.MaxValue,
        int[]? candidateOverride = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        var state = new State(board, candidateOverride);
        var results = new List<TechniqueStep>();

        if (maxLevel >= 1)
        {
            AddIfNotNull(results, FindNakedSingle(state));
        }

        if (maxLevel >= 2)
        {
            AddAll(results, FindHiddenSingles(state));
        }

        if (maxLevel >= 3)
        {
            AddAll(results, FindLockedCandidates(state));
        }

        if (maxLevel >= 4)
        {
            AddAll(results, FindNakedSubsets(state, 2));
            AddAll(results, FindHiddenSubsets(state, 2));
        }

        if (maxLevel >= 5)
        {
            AddAll(results, FindNakedSubsets(state, 3));
            AddAll(results, FindHiddenSubsets(state, 3));
        }

        if (maxLevel >= 6)
        {
            int[] masks = state.SnapshotCandidates();
            AddAll(results, ChainTechniques.FindXWings(masks));
            AddAll(results, ChainTechniques.FindSwordfishes(masks));
            AddAll(results, ChainTechniques.FindXYWings(masks));
        }

        if (maxLevel >= 7)
        {
            int[] masks = state.SnapshotCandidates();
            AddAll(results, ChainSearch.FindXChains(masks));
            AddAll(results, RemotePair.FindRemotePairs(masks));

            if (maxLevel >= 8)
            {
                AddAll(results, ChainSearch.FindXYChains(masks));
                AddAll(results, UniqueRectangle.FindUniqueRectangles(masks));
                AddAll(results, FinnedFish.FindFinnedFish(masks, 2));
            }

            if (maxLevel >= 9)
            {
                AddAll(results, ChainSearch.FindAics(masks));
                AddAll(results, FinnedFish.FindFinnedFish(masks, 3));
            }

            if (maxLevel >= 10)
            {
                AddAll(results, BugPlusOne.FindBugPlusOne(masks));
            }

            if (maxLevel >= 11)
            {
                AddAll(results, AlsTechniques.FindAlsXz(masks));
            }

            if (maxLevel >= 12)
            {
                AddAll(results, SueDeCoq.FindSueDeCoq(masks));
            }

            if (maxLevel >= 13)
            {
                AddAll(results, AlsTechniques.FindAlsChains(masks));
                AddAll(results, ContinuousLoop.FindContinuousLoops(masks));
            }
        }

        return results;
    }

    private static void AddIfNotNull(List<TechniqueStep> list, TechniqueStep? step)
    {
        if (step is not null)
        {
            list.Add(step);
        }
    }

    private static void AddAll(List<TechniqueStep> list, IEnumerable<TechniqueStep> steps) => list.AddRange(steps);

    private static TechniqueStep? FindStep(State state, int maxLevel)
    {
        TechniqueStep? step = null;

        if (maxLevel >= 1)
        {
            step = FindNakedSingle(state);
        }

        if (step is null && maxLevel >= 2)
        {
            step = FindHiddenSingle(state);
        }

        if (step is not null)
        {
            return step;
        }

        if (maxLevel >= 3)
        {
            step = FindLockedCandidates(state).FirstOrDefault();
            if (step is not null)
            {
                return step;
            }
        }

        if (maxLevel >= 4)
        {
            step = FindNakedSubsets(state, 2).FirstOrDefault() ?? FindHiddenSubsets(state, 2).FirstOrDefault();
            if (step is not null)
            {
                return step;
            }
        }

        if (maxLevel >= 5)
        {
            step = FindNakedSubsets(state, 3).FirstOrDefault() ?? FindHiddenSubsets(state, 3).FirstOrDefault();
            if (step is not null)
            {
                return step;
            }
        }

        if (maxLevel >= 6)
        {
            int[] masks = state.SnapshotCandidates();
            step = ChainTechniques.FindXWings(masks).FirstOrDefault()
                ?? ChainTechniques.FindSwordfishes(masks).FirstOrDefault()
                ?? ChainTechniques.FindXYWings(masks).FirstOrDefault();

            if (step is not null)
            {
                return step;
            }
        }

        if (maxLevel >= 7)
        {
            int[] masks = state.SnapshotCandidates();
            step = ChainSearch.FindFirstChain(masks, maxLevel)
                ?? RemotePair.FindRemotePairs(masks).FirstOrDefault();

            if (step is null && maxLevel >= 8)
            {
                step = UniqueRectangle.FindUniqueRectangles(masks).FirstOrDefault()
                    ?? FinnedFish.FindFinnedFish(masks, 2).FirstOrDefault();
            }

            if (step is null && maxLevel >= 9)
            {
                step = FinnedFish.FindFinnedFish(masks, 3).FirstOrDefault();
            }

            if (step is null && maxLevel >= 10)
            {
                step = BugPlusOne.FindBugPlusOne(masks).FirstOrDefault();
            }

            // ===== 阶段九：集合类与环类技巧（更高一档，只有确实需要时才用）=====
            if (step is null && maxLevel >= 11)
            {
                step = AlsTechniques.FindAlsXz(masks).FirstOrDefault();
            }

            if (step is null && maxLevel >= 12)
            {
                step = SueDeCoq.FindSueDeCoq(masks).FirstOrDefault();
            }

            if (step is null && maxLevel >= 13)
            {
                step = AlsTechniques.FindAlsChains(masks).FirstOrDefault()
                    ?? ContinuousLoop.FindContinuousLoops(masks).FirstOrDefault();
            }
        }

        return step;
    }

    private static TechniqueStep? FindNakedSingle(State state)
    {
        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            if (state.IsFilled(i))
            {
                continue;
            }

            int mask = state.Candidates(i);
            if (mask != 0 && SudokuGrid.CountDigits(mask) == 1)
            {
                int digit = SudokuGrid.LowestDigit(mask);
                return new TechniqueStep(
                    Technique.NakedSingle,
                    new[] { i },
                    i,
                    digit,
                    Array.Empty<CandidateRef>(),
                    $"{CellName(i)} 只剩一个候选数 {digit}，直接填入。");
            }
        }

        return null;
    }

    private static TechniqueStep? FindHiddenSingle(State state) => FindHiddenSingles(state).FirstOrDefault();

    private static IEnumerable<TechniqueStep> FindHiddenSingles(State state)
    {
        for (int unit = 0; unit < SudokuGrid.UnitCount; unit++)
        {
            int[] cells = SudokuGrid.AllUnits[unit];
            foreach (int digit in SudokuGrid.Digits(SudokuGrid.AllDigitsMask))
            {
                var holders = new List<int>(9);
                bool alreadyPlaced = false;
                foreach (int cell in cells)
                {
                    if (state.IsFilled(cell))
                    {
                        if (state.Value(cell) == digit)
                        {
                            alreadyPlaced = true;
                            break;
                        }

                        continue;
                    }

                    if ((state.Candidates(cell) & SudokuGrid.DigitBit(digit)) != 0)
                    {
                        holders.Add(cell);
                    }
                }

                if (alreadyPlaced || holders.Count != 1)
                {
                    continue;
                }

                int target = holders[0];
                yield return new TechniqueStep(
                    Technique.HiddenSingle,
                    new[] { target },
                    target,
                    digit,
                    Array.Empty<CandidateRef>(),
                    $"{UnitName(unit)} 内数字 {digit} 只能填在 {CellName(target)}，因此填入 {digit}。");
            }
        }
    }

    private static IEnumerable<TechniqueStep> FindLockedCandidates(State state)
    {
        // 指向型：宫内某数字的所有位置都在同一条线上 → 删除该线（宫外）的该数字
        for (int box = 0; box < SudokuGrid.Size; box++)
        {
            int boxUnit = 18 + box;
            foreach (int digit in SudokuGrid.Digits(SudokuGrid.AllDigitsMask))
            {
                List<int> holders = state.CellsWithCandidate(boxUnit, digit);
                if (holders.Count < 2)
                {
                    continue;
                }

                int row = SudokuGrid.Row(holders[0]);
                if (holders.All(c => SudokuGrid.Row(c) == row))
                {
                    var eliminations = CollectEliminations(state, SudokuGrid.Rows[row], digit, boxUnit);
                    if (eliminations.Count > 0)
                    {
                        yield return new TechniqueStep(
                            Technique.LockedCandidatesPointing,
                            holders.ToArray(),
                            -1,
                            0,
                            eliminations,
                            $"{UnitName(boxUnit)} 内数字 {digit} 只能出现在 {UnitName(row)}，故 {UnitName(row)} 上其余格的候选数 {digit} 可删除。");
                    }
                }

                int col = SudokuGrid.Col(holders[0]);
                if (holders.All(c => SudokuGrid.Col(c) == col))
                {
                    var eliminations = CollectEliminations(state, SudokuGrid.Cols[col], digit, boxUnit);
                    if (eliminations.Count > 0)
                    {
                        yield return new TechniqueStep(
                            Technique.LockedCandidatesPointing,
                            holders.ToArray(),
                            -1,
                            0,
                            eliminations,
                            $"{UnitName(boxUnit)} 内数字 {digit} 只能出现在 {UnitName(9 + col)}，故 {UnitName(9 + col)} 上其余格的候选数 {digit} 可删除。");
                    }
                }
            }
        }

        // 归属型：某线上某数字的所有位置都在同一个宫内 → 删除该宫（线外）的该数字
        for (int lineUnit = 0; lineUnit < 18; lineUnit++)
        {
            foreach (int digit in SudokuGrid.Digits(SudokuGrid.AllDigitsMask))
            {
                List<int> holders = state.CellsWithCandidate(lineUnit, digit);
                if (holders.Count < 2)
                {
                    continue;
                }

                int box = SudokuGrid.Box(holders[0]);
                if (!holders.All(c => SudokuGrid.Box(c) == box))
                {
                    continue;
                }

                var eliminations = CollectEliminations(state, SudokuGrid.Boxes[box], digit, lineUnit);
                if (eliminations.Count > 0)
                {
                    yield return new TechniqueStep(
                        Technique.LockedCandidatesClaiming,
                        holders.ToArray(),
                        -1,
                        0,
                        eliminations,
                        $"{UnitName(lineUnit)} 中数字 {digit} 只能落在 {UnitName(18 + box)}，故 {UnitName(18 + box)} 内其余格的候选数 {digit} 可删除。");
                }
            }
        }
    }

    private static IEnumerable<TechniqueStep> FindNakedSubsets(State state, int size)
    {
        for (int unit = 0; unit < SudokuGrid.UnitCount; unit++)
        {
            var empties = new List<int>(9);
            foreach (int cell in SudokuGrid.AllUnits[unit])
            {
                if (!state.IsFilled(cell))
                {
                    empties.Add(cell);
                }
            }

            if (empties.Count <= size)
            {
                continue;
            }

            foreach (int[] combo in Combinations(empties.ToArray(), size))
            {
                int union = 0;
                foreach (int cell in combo)
                {
                    union |= state.Candidates(cell);
                }

                if (SudokuGrid.CountDigits(union) != size)
                {
                    continue;
                }

                var eliminations = new List<CandidateRef>();
                foreach (int cell in empties)
                {
                    if (combo.Contains(cell))
                    {
                        continue;
                    }

                    int hit = state.Candidates(cell) & union;
                    foreach (int digit in SudokuGrid.Digits(hit))
                    {
                        eliminations.Add(new CandidateRef(cell, digit));
                    }
                }

                if (eliminations.Count == 0)
                {
                    continue;
                }

                string cellsText = string.Join("、", combo.Select(CellName));
                string digitsText = string.Join("、", SudokuGrid.Digits(union));
                string techniqueName = size == 2 ? "数对" : "三数组";
                yield return new TechniqueStep(
                    size == 2 ? Technique.NakedPair : Technique.NakedTriple,
                    combo,
                    -1,
                    0,
                    eliminations,
                    $"{UnitName(unit)} 中 {cellsText} 构成显性{techniqueName} {{{digitsText}}}，故 {UnitName(unit)} 其余格的候选数 {digitsText} 可删除。");
            }
        }
    }

    private static IEnumerable<TechniqueStep> FindHiddenSubsets(State state, int size)
    {
        for (int unit = 0; unit < SudokuGrid.UnitCount; unit++)
        {
            var empties = new List<int>(9);
            foreach (int cell in SudokuGrid.AllUnits[unit])
            {
                if (!state.IsFilled(cell))
                {
                    empties.Add(cell);
                }
            }

            if (empties.Count <= size)
            {
                continue;
            }

            int present = 0;
            foreach (int cell in empties)
            {
                present |= state.Candidates(cell);
            }

            var digits = SudokuGrid.Digits(present).ToArray();
            if (digits.Length <= size)
            {
                continue;
            }

            foreach (int[] combo in Combinations(digits, size))
            {
                int comboMask = 0;
                foreach (int digit in combo)
                {
                    comboMask |= SudokuGrid.DigitBit(digit);
                }

                var holders = empties.Where(c => (state.Candidates(c) & comboMask) != 0).ToArray();
                if (holders.Length != size)
                {
                    continue;
                }

                var eliminations = new List<CandidateRef>();
                foreach (int cell in holders)
                {
                    int extra = state.Candidates(cell) & ~comboMask;
                    foreach (int digit in SudokuGrid.Digits(extra))
                    {
                        eliminations.Add(new CandidateRef(cell, digit));
                    }
                }

                if (eliminations.Count == 0)
                {
                    continue;
                }

                string cellsText = string.Join("、", holders.Select(CellName));
                string digitsText = string.Join("、", combo);
                string techniqueName = size == 2 ? "数对" : "三数组";
                yield return new TechniqueStep(
                    size == 2 ? Technique.HiddenPair : Technique.HiddenTriple,
                    holders,
                    -1,
                    0,
                    eliminations,
                    $"{UnitName(unit)} 中数字 {digitsText} 只可能落在 {cellsText}，构成隐性{techniqueName}，故这些格的其他候选数可删除。");
            }
        }
    }

    private static List<CandidateRef> CollectEliminations(State state, int[] unitCells, int digit, int excludeUnit)
    {
        int bit = SudokuGrid.DigitBit(digit);
        var result = new List<CandidateRef>();
        var exclude = new HashSet<int>(SudokuGrid.AllUnits[excludeUnit]);

        foreach (int cell in unitCells)
        {
            if (exclude.Contains(cell) || state.IsFilled(cell))
            {
                continue;
            }

            if ((state.Candidates(cell) & bit) != 0)
            {
                result.Add(new CandidateRef(cell, digit));
            }
        }

        return result;
    }

    private static IEnumerable<int[]> Combinations(int[] items, int size)
    {
        var buffer = new int[size];
        return Walk(0, 0);

        IEnumerable<int[]> Walk(int start, int depth)
        {
            if (depth == size)
            {
                yield return (int[])buffer.Clone();
                yield break;
            }

            for (int i = start; i <= items.Length - (size - depth); i++)
            {
                buffer[depth] = items[i];
                foreach (int[] combo in Walk(i + 1, depth + 1))
                {
                    yield return combo;
                }
            }
        }
    }

    private static string CellName(int index) => $"R{SudokuGrid.Row(index) + 1}C{SudokuGrid.Col(index) + 1}";

    private static string UnitName(int unitId) => unitId switch
    {
        < 9 => $"第 {unitId + 1} 行",
        < 18 => $"第 {unitId - 8} 列",
        _ => $"第 {unitId - 17} 宫",
    };

    /// <summary>求解过程中的可变状态。</summary>
    private sealed class State
    {
        private readonly int[] _cells;
        private readonly int[] _candidates;

        public State(Board board, int[]? candidateOverride = null)
        {
            _cells = board.ToArray();
            _candidates = new int[SudokuGrid.CellCount];
            for (int i = 0; i < SudokuGrid.CellCount; i++)
            {
                _candidates[i] = board.CandidateMask(i);
            }

            if (candidateOverride is not null)
            {
                // 只做「交」：允许调用方把候选数删得更少（例如玩家自己删掉的），
                // 但绝不会凭空引入规则上不成立的候选数。
                for (int i = 0; i < SudokuGrid.CellCount; i++)
                {
                    _candidates[i] &= candidateOverride[i];
                }
            }
        }

        public bool HasContradiction
        {
            get
            {
                for (int i = 0; i < SudokuGrid.CellCount; i++)
                {
                    if (_cells[i] == 0 && _candidates[i] == 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < SudokuGrid.CellCount; i++)
                {
                    if (_cells[i] == 0)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public bool IsFilled(int index) => _cells[index] != 0;

        public int Value(int index) => _cells[index];

        public int Candidates(int index) => _candidates[index];

        /// <summary>当前候选数掩码快照（供链级技巧等外部模块使用）。</summary>
        public int[] SnapshotCandidates() => (int[])_candidates.Clone();

        public List<int> CellsWithCandidate(int unitId, int digit)
        {
            int bit = SudokuGrid.DigitBit(digit);
            var result = new List<int>(9);
            foreach (int cell in SudokuGrid.AllUnits[unitId])
            {
                if (_cells[cell] == 0 && (_candidates[cell] & bit) != 0)
                {
                    result.Add(cell);
                }
            }

            return result;
        }

        public void Apply(TechniqueStep step)
        {
            if (step.IsPlacement)
            {
                Place(step.PlaceIndex, step.PlaceDigit);
            }

            foreach (CandidateRef elimination in step.Eliminations)
            {
                _candidates[elimination.Cell] &= ~SudokuGrid.DigitBit(elimination.Digit);
            }
        }

        public Board ToBoard() => Board.Wrap((int[])_cells.Clone());

        private void Place(int index, int digit)
        {
            _cells[index] = digit;
            _candidates[index] = 0;
            int bit = SudokuGrid.DigitBit(digit);
            foreach (int peer in SudokuGrid.Peers[index])
            {
                _candidates[peer] &= ~bit;
            }
        }
    }
}
