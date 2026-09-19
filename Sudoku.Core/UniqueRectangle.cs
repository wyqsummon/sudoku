namespace Sudoku.Core;

/// <summary>
/// 唯一矩形（Unique Rectangle）。前提是题目有唯一解：
/// 若四格正好落在两行、两列、两宫内，且都只能填同样的两个数字 a/b，
/// 就会出现「致命矩形」——这两格填 a、那两格填 b，与互换后的填法都成立，
/// 于是题目就不是唯一解了。所以这种形状必须被打破。
/// 本类实现最常用的两类：
/// 类型 1：三格已经只剩 a/b，第四格多了若干候选数 → 这些多余候选数必须删掉；
/// 类型 2：两格多了同一个候选数 c → 同时看见这两格的位置都不能填 c。
/// </summary>
public static class UniqueRectangle
{
    /// <summary>查找唯一矩形（类型 1 / 类型 2）。</summary>
    public static IEnumerable<TechniqueStep> FindUniqueRectangles(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return FindUniqueRectangles(board.ComputeCandidates());
    }

    /// <summary>基于候选数掩码快照查找唯一矩形。</summary>
    internal static IEnumerable<TechniqueStep> FindUniqueRectangles(int[] masks)
    {
        var cand = new ChainTechniques.Candidates(masks);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int r1 = 0; r1 < SudokuGrid.Size; r1++)
        {
            for (int r2 = r1 + 1; r2 < SudokuGrid.Size; r2++)
            {
                for (int c1 = 0; c1 < SudokuGrid.Size; c1++)
                {
                    for (int c2 = c1 + 1; c2 < SudokuGrid.Size; c2++)
                    {
                        int[] cells =
                        {
                            SudokuGrid.Index(r1, c1),
                            SudokuGrid.Index(r1, c2),
                            SudokuGrid.Index(r2, c1),
                            SudokuGrid.Index(r2, c2),
                        };

                        // 必须恰好横跨两个宫：两行同宫带（或同宫）而两列不同堆，或反过来
                        bool sameBand = SudokuGrid.Row(cells[0]) / SudokuGrid.BoxSize == SudokuGrid.Row(cells[2]) / SudokuGrid.BoxSize;
                        bool sameStack = SudokuGrid.Col(cells[0]) / SudokuGrid.BoxSize == SudokuGrid.Col(cells[1]) / SudokuGrid.BoxSize;
                        if (sameBand == sameStack)
                        {
                            continue;
                        }

                        // 四格都得能填同样的两个数字
                        int common = cand.Mask(cells[0]);
                        for (int i = 1; i < cells.Length; i++)
                        {
                            common &= cand.Mask(cells[i]);
                        }

                        if (SudokuGrid.CountDigits(common) < 2)
                        {
                            continue;
                        }

                        foreach (int pair in Pairs(common))
                        {
                            int a = SudokuGrid.LowestDigit(pair);
                            int b = SudokuGrid.LowestDigit(pair & ~SudokuGrid.DigitBit(a));
                            int pairMask = pair;

                            var extras = new int[cells.Length];
                            for (int i = 0; i < cells.Length; i++)
                            {
                                extras[i] = cand.Mask(cells[i]) & ~pairMask;
                            }

                            int withExtras = extras.Count(e => e != 0);
                            if (withExtras == 1)
                            {
                                int index = Array.FindIndex(extras, e => e != 0);
                                var eliminations = SudokuGrid.Digits(extras[index])
                                    .Select(d => new CandidateRef(cells[index], d))
                                    .ToArray();

                                if (eliminations.Length == 0 || !seen.Add($"T1:{cells[index]}:{pairMask}"))
                                {
                                    continue;
                                }

                                yield return BuildType1(cells, index, a, b, eliminations);
                            }
                            else if (withExtras == 2)
                            {
                                int i1 = Array.FindIndex(extras, e => e != 0);
                                int i2 = Array.FindLastIndex(extras, e => e != 0);
                                if (extras[i1] != extras[i2] || SudokuGrid.CountDigits(extras[i1]) != 1)
                                {
                                    continue;
                                }

                                if (!ChainTechniques.IsPeer(cells[i1], cells[i2]))
                                {
                                    continue; // 类型 2 的两个「多候选」格必须在同一单元内
                                }

                                int c = SudokuGrid.LowestDigit(extras[i1]);
                                var eliminations = new List<CandidateRef>();
                                foreach (int cell in SudokuGrid.Peers[cells[i1]])
                                {
                                    if (cell == cells[i2] || cells.Contains(cell) || !cand.Has(cell, c))
                                    {
                                        continue;
                                    }

                                    if (!ChainTechniques.IsPeer(cell, cells[i2]))
                                    {
                                        continue;
                                    }

                                    eliminations.Add(new CandidateRef(cell, c));
                                }

                                if (eliminations.Count == 0 || !seen.Add($"T2:{cells[i1]}:{cells[i2]}:{c}"))
                                {
                                    continue;
                                }

                                yield return BuildType2(cells, i1, i2, a, b, c, eliminations);
                            }
                        }
                    }
                }
            }
        }
    }

    private static TechniqueStep BuildType1(
        IReadOnlyList<int> cells,
        int index,
        int a,
        int b,
        IReadOnlyList<CandidateRef> eliminations)
    {
        int extraCell = cells[index];
        string elimDesc = ChainTechniques.Join(eliminations);
        string corners = string.Join("、", cells.Where(c => c != extraCell).Select(ChainTechniques.CellName));

        string description =
            $"唯一矩形（类型 1）：{corners} 与 {ChainTechniques.CellName(extraCell)} 构成两行两列两宫的矩形，" +
            $"前三格都只剩 {a}/{b}；若第四格填别的数字也会留下 {a}/{b} 互换的两个解，故删除 {elimDesc}。";

        var derivation = new List<string>
        {
            $"这四格（{string.Join("、", cells.Select(ChainTechniques.CellName))}）落在两行、两列、两宫内。",
            $"其中 {corners} 三格的候选数恰好只有 {a} 和 {b}。",
            $"如果第四格 {ChainTechniques.CellName(extraCell)} 也不填 {a}/{b}，这四格就会形成 {a}/{b} 可互换的「致命矩形」，题目将有两个解。",
            $"题目唯一解，所以 {ChainTechniques.CellName(extraCell)} 必须是 {a} 或 {b}，删除：{elimDesc}。",
        };

        return new TechniqueStep(
            Technique.UniqueRectangle,
            cells.ToArray(),
            -1,
            0,
            eliminations,
            description,
            RectangleLinks(cells, a, b),
            derivation);
    }

    private static TechniqueStep BuildType2(
        IReadOnlyList<int> cells,
        int i1,
        int i2,
        int a,
        int b,
        int c,
        IReadOnlyList<CandidateRef> eliminations)
    {
        int roof1 = cells[i1];
        int roof2 = cells[i2];
        string elimDesc = ChainTechniques.Join(eliminations);

        string description =
            $"唯一矩形（类型 2）：{ChainTechniques.CellName(roof1)} 与 {ChainTechniques.CellName(roof2)} 都比 {a}/{b} 多一个 {c}，" +
            $"这两格必有一格是 {c}，故同时看见它们的位置不能填 {c}，删除 {elimDesc}。";

        var derivation = new List<string>
        {
            $"四格（{string.Join("、", cells.Select(ChainTechniques.CellName))}）落在两行、两列、两宫内，都可填 {a}/{b}。",
            $"{ChainTechniques.CellName(roof1)} 与 {ChainTechniques.CellName(roof2)} 额外都带候选数 {c}，其余两格只有 {a}/{b}。",
            $"若这两格都不是 {c}，四格就形成 {a}/{b} 的致命矩形，题目出现两个解。",
            $"所以 {c} 必在其中一格，同时看见这两格的候选数 {c} 都要删除：{elimDesc}。",
        };

        return new TechniqueStep(
            Technique.UniqueRectangle,
            cells.ToArray(),
            -1,
            0,
            eliminations,
            description,
            RectangleLinks(cells, a, b),
            derivation);
    }

    /// <summary>把矩形四条边画成弱链（同一行/列内同一数字不能同时成立）。</summary>
    private static IReadOnlyList<TechniqueLink> RectangleLinks(IReadOnlyList<int> cells, int a, int b)
    {
        int topLeft = cells[0];
        int topRight = cells[1];
        int bottomLeft = cells[2];
        int bottomRight = cells[3];

        return new[]
        {
            new TechniqueLink(new CandidateRef(topLeft, a), new CandidateRef(topRight, a), false, "同一行内的两个 " + a + " 不能同真"),
            new TechniqueLink(new CandidateRef(topRight, b), new CandidateRef(bottomRight, b), false, "同一列内的两个 " + b + " 不能同真"),
            new TechniqueLink(new CandidateRef(bottomRight, a), new CandidateRef(bottomLeft, a), false, "同一行内的两个 " + a + " 不能同真"),
            new TechniqueLink(new CandidateRef(bottomLeft, b), new CandidateRef(topLeft, b), false, "同一列内的两个 " + b + " 不能同真"),
        };
    }

    private static IEnumerable<int> Pairs(int mask)
    {
        int[] digits = SudokuGrid.Digits(mask).ToArray();
        for (int i = 0; i < digits.Length; i++)
        {
            for (int j = i + 1; j < digits.Length; j++)
            {
                yield return SudokuGrid.DigitBit(digits[i]) | SudokuGrid.DigitBit(digits[j]);
            }
        }
    }
}
