namespace Sudoku.Core;

/// <summary>
/// 带鳍鱼（Finned Fish）：普通鱼形（X 翼 / 剑鱼）要求基准行（列）里的候选数正好落在同样多的覆盖列（行）上。
/// 若基准单元里还多出几个「鳍格」——它们全都落在同一个宫内——
/// 那么无论鳍格是否成立，覆盖单元上不属于基准单元的位置都不能再放该数字：
/// 鳍不成立时是普通鱼形；鳍成立时它一定能看见这些位置。
/// </summary>
public static class FinnedFish
{
    /// <summary>在 2×2（带鳍 X 翼）与 3×3（带鳍剑鱼）范围内查找带鳍鱼。</summary>
    public static IEnumerable<TechniqueStep> FindFinnedFish(Board board, int maxSize = 3)
    {
        ArgumentNullException.ThrowIfNull(board);
        return FindFinnedFish(board.ComputeCandidates(), maxSize);
    }

    /// <summary>基于候选数掩码快照查找带鳍鱼（maxSize=2 只找带鳍 X 翼）。</summary>
    internal static IEnumerable<TechniqueStep> FindFinnedFish(int[] masks, int maxSize = 3)
    {
        var cand = new ChainTechniques.Candidates(masks);

        for (int size = 2; size <= maxSize; size++)
        {
            for (int digit = 1; digit <= SudokuGrid.Size; digit++)
            {
                for (int orientation = 0; orientation < 2; orientation++)
                {
                    bool byRow = orientation == 0;

                    // 收集候选单元：该数字在其中出现 2..size+2 次（多出来的可能是鳍）
                    var bases = new List<(int Index, List<int> Cells)>();
                    for (int i = 0; i < SudokuGrid.Size; i++)
                    {
                        int unitId = byRow ? i : SudokuGrid.Size + i;
                        List<int> cells = ChainTechniques.CellsWithCandidate(cand, unitId, digit);
                        if (cells.Count >= 2 && cells.Count <= size + 2)
                        {
                            bases.Add((i, cells));
                        }
                    }

                    if (bases.Count < size)
                    {
                        continue;
                    }

                    foreach (var combo in ChainTechniques.Combinations(bases, size))
                    {
                        var allCells = combo.SelectMany(b => b.Cells).Distinct().ToList();

                        // 快速剪枝：所有候选位置最多 size + 3 个（覆盖 size 个 + 鳍不超过 3 个）
                        if (allCells.Count > size + 3)
                        {
                            continue;
                        }

                        foreach (var coverCombo in ChainTechniques.Combinations(Enumerable.Range(0, SudokuGrid.Size).ToArray(), size))
                        {
                            var coverSet = coverCombo.ToHashSet();

                            var fins = new List<int>();
                            bool valid = true;

                            foreach (var baseUnit in combo)
                            {
                                int inCover = 0;
                                foreach (int cell in baseUnit.Cells)
                                {
                                    int cover = byRow ? SudokuGrid.Col(cell) : SudokuGrid.Row(cell);
                                    if (coverSet.Contains(cover))
                                    {
                                        inCover++;
                                    }
                                    else
                                    {
                                        fins.Add(cell);
                                    }
                                }

                                // 每个基准单元至少要有一半候选落在覆盖单元里，否则不算鱼形
                                if (inCover < 2)
                                {
                                    valid = false;
                                    break;
                                }
                            }

                            if (!valid || fins.Count == 0 || fins.Count > 3)
                            {
                                continue;
                            }

                            // 所有鳍格必须落在同一个宫内
                            if (fins.Select(SudokuGrid.Box).Distinct().Count() != 1)
                            {
                                continue;
                            }

                            var eliminations = new List<CandidateRef>();
                            foreach (int cover in coverSet)
                            {
                                int coverUnit = byRow ? SudokuGrid.Size + cover : cover;
                                foreach (int cell in SudokuGrid.AllUnits[coverUnit])
                                {
                                    if (!cand.Has(cell, digit))
                                    {
                                        continue;
                                    }

                                    int baseIndex = byRow ? SudokuGrid.Row(cell) : SudokuGrid.Col(cell);
                                    if (combo.Any(b => b.Index == baseIndex))
                                    {
                                        continue;
                                    }

                                    // 必须能看见所有鳍格
                                    if (fins.Any(f => !ChainTechniques.IsPeer(cell, f)))
                                    {
                                        continue;
                                    }

                                    eliminations.Add(new CandidateRef(cell, digit));
                                }
                            }

                            if (eliminations.Count == 0)
                            {
                                continue;
                            }

                            yield return BuildStep(cand, digit, size, byRow, combo, coverSet, fins, eliminations);
                        }
                    }
                }
            }
        }
    }

    private static TechniqueStep BuildStep(
        ChainTechniques.Candidates cand,
        int digit,
        int size,
        bool byRow,
        IReadOnlyList<(int Index, List<int> Cells)> combo,
        HashSet<int> coverSet,
        IReadOnlyList<int> fins,
        IReadOnlyList<CandidateRef> eliminations)
    {
        Technique technique = size == 2 ? Technique.FinnedXWing : Technique.FinnedSwordfish;
        string baseName = byRow ? "行" : "列";
        string coverName = byRow ? "列" : "行";
        string fishName = size == 2 ? "带鳍 X 翼" : "带鳍剑鱼";

        var highlights = new List<int>();
        var links = new List<TechniqueLink>();
        var baseDescs = new List<string>();
        var marks = new List<HintMark>();

        foreach (var baseUnit in combo)
        {
            int unitId = byRow ? baseUnit.Index : SudokuGrid.Size + baseUnit.Index;
            List<int> cells = ChainTechniques.CellsWithCandidate(cand, unitId, digit);
            highlights.AddRange(cells);

            var inCover = new List<int>();
            var baseFins = new List<int>();
            foreach (int cell in cells)
            {
                int cover = byRow ? SudokuGrid.Col(cell) : SudokuGrid.Row(cell);
                if (coverSet.Contains(cover))
                {
                    inCover.Add(cell);
                }
                else
                {
                    baseFins.Add(cell);
                }
            }

            // 高亮分三段出现：先画出落在覆盖线上的基准格（鱼身），再单独标出鳍格，最后才是结论
            foreach (int cell in inCover)
            {
                marks.Add(new HintMark(cell, digit, HintMarkRole.Pattern, 0));
            }

            foreach (int cell in baseFins)
            {
                marks.Add(new HintMark(cell, digit, HintMarkRole.Fin, 2));
            }

            baseDescs.Add($"第 {baseUnit.Index + 1}{baseName}的 {string.Join("、", cells.Select(ChainTechniques.CellName))}");

            // 同一基准单元里同一数字两两不能同真（弱链）
            for (int i = 0; i < cells.Count; i++)
            {
                for (int j = i + 1; j < cells.Count; j++)
                {
                    links.Add(new TechniqueLink(
                        new CandidateRef(cells[i], digit),
                        new CandidateRef(cells[j], digit),
                        false,
                        $"第 {baseUnit.Index + 1}{baseName}内该数字不能同时成立"));
                }
            }

            if (baseFins.Count > 0)
            {
                // 鳍格到基准格之间画弱链，提示「鳍成立时基准形状被打破」
                foreach (int fin in baseFins)
                {
                    foreach (int cell in inCover)
                    {
                        links.Add(new TechniqueLink(
                            new CandidateRef(fin, digit),
                            new CandidateRef(cell, digit),
                            false,
                            "鳍格与基准格在同一单元，不能同真"));
                    }
                }
            }
        }

        string coverDesc = string.Join("、", coverSet.OrderBy(c => c).Select(c => $"第 {c + 1}{coverName}"));
        string finDesc = string.Join("、", fins.Select(f => ChainTechniques.CellName(f) + "(" + digit + ")"));
        string elimDesc = ChainTechniques.Join(eliminations);

        // 覆盖线上同样含该数字、但不属于基线的位置：按结构色先标出来（对应第 2 条推导），
        // 真正能删的那些到结论（第 6 条）再换成结论色打叉
        var baseCells = highlights.ToHashSet();
        foreach (int cover in coverSet)
        {
            int coverUnit = byRow ? SudokuGrid.Size + cover : cover;
            foreach (int cell in SudokuGrid.AllUnits[coverUnit])
            {
                if (baseCells.Contains(cell) || !cand.Has(cell, digit))
                {
                    continue;
                }

                marks.Add(new HintMark(cell, digit, HintMarkRole.Pattern, 1));
            }
        }

        foreach (CandidateRef elimination in eliminations)
        {
            marks.Add(new HintMark(elimination.Cell, elimination.Digit, HintMarkRole.Elimination, 5));
        }

        string description =
            $"数字 {digit} 在{string.Join("、", baseDescs)}中大部分落在 {coverDesc}，另有多出的鳍格 {finDesc}（同在第 {SudokuGrid.Box(fins[0]) + 1} 宫）；" +
            $"无论鳍格成立与否，{coverDesc}上能看见鳍格的位置都不能填 {digit}，删除 {elimDesc}。";

        var derivation = new List<string>
        {
            $"观察数字 {digit}：{string.Join("；", baseDescs)}。",
            $"这些位置几乎落在 {coverDesc} 这 {size} 个{coverName}上，构成本该成立的 {size}×{size} 鱼形。",
            $"但还多出鳍格 {finDesc}（全部在第 {SudokuGrid.Box(fins[0]) + 1} 宫内），所以鱼形不是必然成立。",
            $"如果鳍格都不是 {digit}，鱼形成立，{coverDesc}上该数字只能落在基准{baseName}里；",
            $"如果某个鳍格是 {digit}，那么同时看见所有鳍格的位置都不能是 {digit}。",
            $"两种情况下结论一致，可以删除：{elimDesc}。",
        };

        return new TechniqueStep(
            technique,
            highlights.Distinct().OrderBy(c => c).Concat(fins).Distinct().OrderBy(c => c).ToArray(),
            -1,
            0,
            eliminations,
            description,
            links,
            derivation,
            HintMarks.Normalize(marks));
    }
}
