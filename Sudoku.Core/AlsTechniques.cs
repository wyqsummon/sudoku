namespace Sudoku.Core;

/// <summary>
/// 集合类高阶技巧（阶段九）：ALS-XZ 与 ALS 链（ALS-XY-Wing 及更长）。
///
/// ALS（Almost Locked Set，准锁定集）= 同一单元内的 k 个格恰好含 k+1 个候选数。
/// 它「差一个候选数就成锁定集」：这 k 格里必然要填进它全部候选数中的 k 个，
/// 也就是必然有一个候选数落空——这正是下面所有推理的支点。
///
/// ALS-XZ：两个 ALS 之间有「受限公共候选数」X（A 里能填 X 的格全都看得见 B 里能填 X 的格，
/// 因此 A、B 至多一处填 X），则两者的另一个公共候选数 Z 必然出现在其中一个 ALS 里，
/// 于是同时看得见两个 ALS 所有 Z 的位置都不能填 Z。
///
/// ALS 链：A1 -X1- A2 -X2- ... -Xk- A(k+1)，相邻两两靠受限公共候选数相连，
/// 首尾还要共享一个 Z（Z 不等于任何一个 Xi），推理与 ALS-XZ 相同（它是长度 2 的特例）。
/// </summary>
public static class AlsTechniques
{
    /// <summary>ALS 的最大格数（实战里 1~3 格已覆盖绝大多数题，再大只是拖慢搜索）。</summary>
    private const int MaxAlsCells = 3;

    /// <summary>ALS 链最多串几个 ALS。</summary>
    private const int MaxChainLength = 4;

    /// <summary>从盘面查找 ALS-XZ。</summary>
    public static IEnumerable<TechniqueStep> FindAlsXz(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return FindAlsXz(board.ComputeCandidates());
    }

    /// <summary>从候选数掩码快照查找 ALS-XZ。</summary>
    internal static IEnumerable<TechniqueStep> FindAlsXz(int[] masks)
    {
        var cand = new ChainTechniques.Candidates(masks);
        List<Als> alss = EnumerateAls(cand);
        if (alss.Count < 2)
        {
            return Array.Empty<TechniqueStep>();
        }

        var steps = new List<TechniqueStep>();
        var seen = new HashSet<string>();

        foreach (var (ai, bi) in Pairs(alss))
        {
            Als a = alss[ai];
            Als b = alss[bi];
            if (Overlaps(a, b))
            {
                continue;
            }

            // 两边都是一个双值格时，这只是「数对」，交给低阶模块处理，别在这里重复报
            if (a.Size == 1 && b.Size == 1)
            {
                continue;
            }

            foreach (int x in SudokuGrid.Digits(a.Mask & b.Mask))
            {
                if (!IsRestrictedCommon(cand, a, b, x))
                {
                    continue;
                }

                // Z：除受限公共候选数以外的公共候选数
                foreach (int z in SudokuGrid.Digits(a.Mask & b.Mask & ~SudokuGrid.DigitBit(x)))
                {
                    List<CandidateRef> eliminations = EliminationsFor(cand, new[] { a, b }, z);
                    if (eliminations.Count == 0)
                    {
                        continue;
                    }

                    string key = $"{string.Join(",", a.Cells)}|{string.Join(",", b.Cells)}|{x}|{z}";
                    if (seen.Add(key))
                    {
                        steps.Add(BuildAlsXzStep(cand, a, b, x, z, eliminations));
                    }
                }
            }
        }

        return steps;
    }

    /// <summary>从盘面查找 ALS 链（长度 ≥ 3 个 ALS）。</summary>
    public static IEnumerable<TechniqueStep> FindAlsChains(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return FindAlsChains(board.ComputeCandidates());
    }

    /// <summary>从候选数掩码快照查找 ALS 链。</summary>
    internal static IEnumerable<TechniqueStep> FindAlsChains(int[] masks)
    {
        var cand = new ChainTechniques.Candidates(masks);
        List<Als> alss = EnumerateAls(cand);
        if (alss.Count < 3)
        {
            return Array.Empty<TechniqueStep>();
        }

        // 先把「受限公共候选数」的关系图建好，链搜索只在图上走
        var links = new Dictionary<int, List<(int Other, int Digit)>>();
        foreach (var (ai, bi) in Pairs(alss))
        {
            Als a = alss[ai];
            Als b = alss[bi];
            if (Overlaps(a, b))
            {
                continue;
            }

            // 两边都是一个双值格时，这只是「数对」，交给低阶模块处理，别在这里重复报
            if (a.Size == 1 && b.Size == 1)
            {
                continue;
            }

            foreach (int x in SudokuGrid.Digits(a.Mask & b.Mask))
            {
                if (!IsRestrictedCommon(cand, a, b, x))
                {
                    continue;
                }

                Add(links, ai, bi, x);
                Add(links, bi, ai, x);
            }
        }

        var steps = new List<TechniqueStep>();
        var seen = new HashSet<string>();

        foreach (int start in links.Keys.OrderBy(k => k))
        {
            Walk(start, start, new List<int> { start }, new List<int>());
        }

        return steps;

        void Walk(int start, int current, List<int> path, List<int> usedDigits)
        {
            if (path.Count > MaxChainLength)
            {
                return;
            }

            foreach ((int next, int digit) in links[current])
            {
                if (next == start || path.Contains(next) || usedDigits.Contains(digit))
                {
                    continue;
                }

                // 链上的 ALS 必须两两不相交（标准 ALS 链的前提），否则推理不成立
                if (path.Any(index => Overlaps(alss[index], alss[next])))
                {
                    continue;
                }

                // 首尾共享的 Z 不能是链上用过的受限公共候选数
                int shared = alss[start].Mask & alss[next].Mask;
                foreach (int used in usedDigits.Append(digit))
                {
                    shared &= ~SudokuGrid.DigitBit(used);
                }

                if (path.Count >= 2 && shared != 0)
                {
                    var chain = new List<Als>();
                    foreach (int index in path)
                    {
                        chain.Add(alss[index]);
                    }

                    chain.Add(alss[next]);

                    var chainDigits = new List<int>(usedDigits) { digit };
                    foreach (int z in SudokuGrid.Digits(shared))
                    {
                        // 首尾必须不是同一个 ALS 家族（否则推理退化）
                        List<CandidateRef> eliminations = EliminationsFor(cand, chain, z);
                        if (eliminations.Count == 0)
                        {
                            continue;
                        }

                        string key = $"{string.Join(",", chain.Select(a => string.Join(",", a.Cells)))}|{z}";
                        if (seen.Add(key))
                        {
                            steps.Add(BuildAlsChainStep(cand, chain, chainDigits, z, eliminations));
                        }
                    }
                }

                path.Add(next);
                usedDigits.Add(digit);
                Walk(start, next, path, usedDigits);
                usedDigits.RemoveAt(usedDigits.Count - 1);
                path.RemoveAt(path.Count - 1);
            }
        }
    }

    // ------------------------------------------------------------------
    // ALS 枚举与判定
    // ------------------------------------------------------------------

    /// <summary>一个准锁定集。</summary>
    private readonly struct Als(int[] cells, int mask)
    {
        public int[] Cells { get; } = cells;

        public int Mask { get; } = mask;

        public int Size => Cells.Length;

        public bool Contains(int cell) => Array.IndexOf(Cells, cell) >= 0;
    }

    /// <summary>枚举全盘所有 ALS（同一单元内 k 格恰好含 k+1 个候选数）。</summary>
    private static List<Als> EnumerateAls(ChainTechniques.Candidates cand)
    {
        var result = new List<Als>();
        var seen = new HashSet<string>();

        for (int unit = 0; unit < SudokuGrid.UnitCount; unit++)
        {
            var cells = new List<int>(SudokuGrid.Size);
            foreach (int cell in SudokuGrid.AllUnits[unit])
            {
                if (cand.Count(cell) >= 2)
                {
                    cells.Add(cell);
                }
            }

            for (int size = 1; size <= MaxAlsCells && size <= cells.Count; size++)
            {
                foreach (int[] combo in ChainTechniques.Combinations(cells, size))
                {
                    int mask = 0;
                    foreach (int cell in combo)
                    {
                        mask |= cand.Mask(cell);
                    }

                    if (SudokuGrid.CountDigits(mask) != size + 1)
                    {
                        continue;
                    }

                    // 同一批格可能同时属于行/列/宫，只留一份
                    string key = string.Join(",", combo);
                    if (seen.Add(key))
                    {
                        result.Add(new Als(combo, mask));
                    }
                }
            }
        }

        return result;
    }

    private static IEnumerable<(int A, int B)> Pairs(List<Als> alss)
    {
        // 按「共同候选数 ≥ 2」剪枝：ALS-XZ 至少需要一个受限公共候选数 + 一个结论候选数
        var byDigit = new List<int>[SudokuGrid.Size + 1];
        for (int d = 0; d <= SudokuGrid.Size; d++)
        {
            byDigit[d] = new List<int>();
        }

        for (int i = 0; i < alss.Count; i++)
        {
            foreach (int d in SudokuGrid.Digits(alss[i].Mask))
            {
                byDigit[d].Add(i);
            }
        }

        var seen = new HashSet<long>();
        var result = new List<(int, int)>();

        for (int d = 1; d <= SudokuGrid.Size; d++)
        {
            List<int> group = byDigit[d];
            for (int i = 0; i < group.Count; i++)
            {
                for (int j = i + 1; j < group.Count; j++)
                {
                    long key = ((long)group[i] << 20) | (uint)group[j];
                    if (seen.Add(key))
                    {
                        result.Add((group[i], group[j]));
                    }
                }
            }
        }

        return result;
    }

    private static void Add(Dictionary<int, List<(int Other, int Digit)>> map, int from, int to, int digit)
    {
        if (!map.TryGetValue(from, out List<(int Other, int Digit)>? list))
        {
            list = new List<(int Other, int Digit)>();
            map[from] = list;
        }

        if (!list.Any(item => item.Other == to && item.Digit == digit))
        {
            list.Add((to, digit));
        }
    }

    private static bool Overlaps(Als a, Als b) => a.Cells.Any(b.Contains);

    /// <summary>X 是否为受限公共候选数：A 里能填 X 的每一格都看得见 B 里能填 X 的每一格。</summary>
    private static bool IsRestrictedCommon(ChainTechniques.Candidates cand, Als a, Als b, int digit)
    {
        bool any = false;

        foreach (int ca in a.Cells)
        {
            if (!cand.Has(ca, digit))
            {
                continue;
            }

            foreach (int cb in b.Cells)
            {
                if (!cand.Has(cb, digit))
                {
                    continue;
                }

                if (!ChainTechniques.IsPeer(ca, cb))
                {
                    return false;
                }

                any = true;
            }
        }

        return any;
    }

    /// <summary>能删除 Z 的位置：既不在这些 ALS 里，又同时看得见它们所有能填 Z 的格。</summary>
    private static List<CandidateRef> EliminationsFor(ChainTechniques.Candidates cand, IReadOnlyList<Als> alss, int digit)
    {
        var holders = new List<int>();
        foreach (Als als in alss)
        {
            foreach (int cell in als.Cells)
            {
                if (cand.Has(cell, digit))
                {
                    holders.Add(cell);
                }
            }
        }

        var result = new List<CandidateRef>();

        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            if (!cand.Has(cell, digit))
            {
                continue;
            }

            if (alss.Any(als => als.Contains(cell)))
            {
                continue;
            }

            bool seesAll = true;
            foreach (int holder in holders)
            {
                if (!ChainTechniques.IsPeer(cell, holder))
                {
                    seesAll = false;
                    break;
                }
            }

            if (seesAll)
            {
                result.Add(new CandidateRef(cell, digit));
            }
        }

        return result;
    }

    // ------------------------------------------------------------------
    // 生成技巧步骤
    // ------------------------------------------------------------------

    private static TechniqueStep BuildAlsXzStep(
        ChainTechniques.Candidates cand,
        Als a,
        Als b,
        int x,
        int z,
        IReadOnlyList<CandidateRef> eliminations)
    {
        string aCells = DescribeCells(a);
        string bCells = DescribeCells(b);
        string aCands = JoinDigits(a.Mask);
        string bCands = JoinDigits(b.Mask);
        string elimDesc = ChainTechniques.Join(eliminations);

        var highlights = new List<int>();
        highlights.AddRange(a.Cells);
        highlights.AddRange(b.Cells);

        var derivation = new List<string>
        {
            $"ALS A = {aCells}，这 {a.Cells.Length} 格共用 {aCands} 共 {SudokuGrid.CountDigits(a.Mask)} 个候选数（只比格数多一个）。",
            $"ALS B = {bCells}，这 {b.Cells.Length} 格共用 {bCands} 共 {SudokuGrid.CountDigits(b.Mask)} 个候选数。",
            $"{x} 是两者的受限公共候选数：A 里能填 {x} 的格都看得见 B 里能填 {x} 的格，所以 A 与 B 至多只有一处填 {x}。",
            $"于是 A、B 里必有一个「不填 {x}」——它就得把其余候选数全部填满，其中包含 {z}。",
            $"所以同时看得见 A、B 中所有 {z} 的位置都不能再填 {z}，删除：{elimDesc}。",
        };

        var marks = new List<HintMark>();
        foreach (int cell in a.Cells)
        {
            marks.Add(new HintMark(cell, 0, HintMarkRole.Pattern, 0));
        }

        foreach (int cell in b.Cells)
        {
            marks.Add(new HintMark(cell, 0, HintMarkRole.Pattern, 1));
        }

        foreach (int cell in a.Cells)
        {
            if (cand.Has(cell, x))
            {
                marks.Add(new HintMark(cell, x, HintMarkRole.DigitA, 2));
            }
        }

        foreach (int cell in b.Cells)
        {
            if (cand.Has(cell, x))
            {
                marks.Add(new HintMark(cell, x, HintMarkRole.DigitA, 2));
            }
        }

        foreach (int cell in a.Cells)
        {
            if (cand.Has(cell, z))
            {
                marks.Add(new HintMark(cell, z, HintMarkRole.DigitB, 3));
            }
        }

        foreach (int cell in b.Cells)
        {
            if (cand.Has(cell, z))
            {
                marks.Add(new HintMark(cell, z, HintMarkRole.DigitB, 3));
            }
        }

        foreach (CandidateRef elimination in eliminations)
        {
            marks.Add(new HintMark(elimination.Cell, elimination.Digit, HintMarkRole.Elimination, 4));
        }

        string description =
            $"ALS-XZ：准锁定集 A = {aCells}（{aCands}），B = {bCells}（{bCands}）；" +
            $"受限公共候选数 {x}，另一个公共候选数 {z} 必在 A 或 B 中被填上，故删除 {elimDesc}。";

        return new TechniqueStep(
            Technique.AlsXz,
            highlights,
            -1,
            0,
            eliminations,
            description,
            Array.Empty<TechniqueLink>(),
            derivation,
            HintMarks.Normalize(marks));
    }

    private static TechniqueStep BuildAlsChainStep(
        ChainTechniques.Candidates cand,
        IReadOnlyList<Als> chain,
        IReadOnlyList<int> linkDigits,
        int z,
        IReadOnlyList<CandidateRef> eliminations)
    {
        string chainText = string.Join(" —— ", chain.Select(a => $"{DescribeCells(a)}（{JoinDigits(a.Mask)}）"));
        string linkText = string.Join("、", linkDigits.Select(d => d.ToString()));
        string elimDesc = ChainTechniques.Join(eliminations);

        var highlights = new List<int>();
        foreach (Als als in chain)
        {
            highlights.AddRange(als.Cells);
        }

        var derivation = new List<string>
        {
            $"把盘面上「只差一个候选数就填满」的格组找出来：{chainText}。",
            $"相邻两组之间都有受限公共候选数（依次是 {linkText}），一组若填了它，另一组就不能再填。",
            $"链首与链尾还共同含有候选数 {z}；顺着链推下去，无论中间怎么选，链首或链尾总有一处要填 {z}。",
            $"所以同时看得见链首、链尾所有 {z} 的位置都不能填 {z}，删除：{elimDesc}。",
        };

        var marks = new List<HintMark>();
        int stage = 0;
        foreach (Als als in chain)
        {
            foreach (int cell in als.Cells)
            {
                marks.Add(new HintMark(cell, 0, HintMarkRole.Pattern, Math.Min(stage, 1)));
            }

            stage++;
        }

        foreach (int digit in linkDigits)
        {
            foreach (Als als in chain)
            {
                if ((als.Mask & SudokuGrid.DigitBit(digit)) == 0)
                {
                    continue;
                }

                foreach (int cell in als.Cells)
                {
                    if (cand.Has(cell, digit))
                    {
                        marks.Add(new HintMark(cell, digit, HintMarkRole.DigitA, 1));
                    }
                }
            }
        }

        foreach (Als als in chain)
        {
            foreach (int cell in als.Cells)
            {
                if (cand.Has(cell, z))
                {
                    marks.Add(new HintMark(cell, z, HintMarkRole.DigitB, 2));
                }
            }
        }

        foreach (CandidateRef elimination in eliminations)
        {
            marks.Add(new HintMark(elimination.Cell, elimination.Digit, HintMarkRole.Elimination, 3));
        }

        string description =
            $"ALS 链（{chain.Count} 组）：{chainText}；相邻靠受限公共候选数 {linkText} 相连，" +
            $"首尾共有 {z}，故删除 {elimDesc}。";

        return new TechniqueStep(
            Technique.AlsChain,
            highlights,
            -1,
            0,
            eliminations,
            description,
            Array.Empty<TechniqueLink>(),
            derivation,
            HintMarks.Normalize(marks));
    }

    private static string DescribeCells(Als als) =>
        string.Join("+", als.Cells.Select(ChainTechniques.CellName));

    private static string JoinDigits(int mask) => string.Join("/", SudokuGrid.Digits(mask));
}
