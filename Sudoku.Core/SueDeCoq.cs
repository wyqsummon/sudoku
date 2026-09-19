namespace Sudoku.Core;

/// <summary>
/// Sue de Coq（阶段九）：行/列与宫交叉处的「双翼锁定集」。
///
/// 取一条线（行或列）与一个宫的交叉格 I，再在宫侧补几格（A = I ∪ 宫侧补格，A 全在宫里）、
/// 在线侧补几格（B = I ∪ 线侧补格，B 全在这条线上）。
///
/// **两侧必须各自都是锁定集**：A 的格数 = A 含有的候选数个数、B 的格数 = B 含有的候选数个数。
/// 因为同一侧的格两两同族（同一宫内 / 同一条线上），格数与候选数相等才能推出
/// 「这些候选数在 A 里各自恰好出现一次」，于是 A 的候选数只能落在 A 里、B 的候选数只能落在 B 里。
///
/// 注意：只用「两侧合计格数 = 合计候选数个数」是**不成立**的——宫侧的格与线侧的格彼此不同族
/// （比如宫侧的 R4C4 与线侧的 R5C2 既不同行也不同列），可以同时填同一个数字，
/// 合计数相等推不出锁定集，据此删除会删掉正确答案（阶段九实测复现过 5 例，已修）。
///
/// 于是：只在宫侧出现的候选数只能落在宫侧那几格里 → 宫里其他地方不能填它；
/// 只在线侧出现的候选数同理，线（行/列）上其他地方不能填它。
/// </summary>
public static class SueDeCoq
{
    /// <summary>每侧最多补几格（I 之外）。补得越多越慢，实战 2 格已足够。</summary>
    private const int MaxExtraCells = 3;

    /// <summary>从盘面查找 Sue de Coq。</summary>
    public static IEnumerable<TechniqueStep> FindSueDeCoq(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return FindSueDeCoq(board.ComputeCandidates());
    }

    /// <summary>从候选数掩码快照查找 Sue de Coq。</summary>
    internal static IEnumerable<TechniqueStep> FindSueDeCoq(int[] masks)
    {
        var cand = new ChainTechniques.Candidates(masks);

        // 27 组（线，宫）交叉：9 行 × 宫内 3 行 + 9 列 × 宫内 3 列
        for (int line = 0; line < SudokuGrid.Size; line++)
        {
            for (int box = 0; box < SudokuGrid.Size; box++)
            {
                for (int orientation = 0; orientation < 2; orientation++)
                {
                    TechniqueStep? step = TryIntersection(cand, line, box, orientation == 0);
                    if (step is not null)
                    {
                        yield return step;
                    }
                }
            }
        }
    }

    private static TechniqueStep? TryIntersection(
        ChainTechniques.Candidates cand,
        int line,
        int box,
        bool byRow)
    {
        int lineUnit = byRow ? line : SudokuGrid.Size + line;
        int boxUnit = (2 * SudokuGrid.Size) + box;

        var intersection = new List<int>();
        var boxOnly = new List<int>();
        var lineOnly = new List<int>();

        foreach (int cell in SudokuGrid.AllUnits[lineUnit])
        {
            if (SudokuGrid.Box(cell) == box)
            {
                intersection.Add(cell);
            }
            else
            {
                lineOnly.Add(cell);
            }
        }

        foreach (int cell in SudokuGrid.AllUnits[boxUnit])
        {
            if (SudokuGrid.Row(cell) != line && byRow)
            {
                boxOnly.Add(cell);
            }
            else if (SudokuGrid.Col(cell) != line && !byRow)
            {
                boxOnly.Add(cell);
            }
        }

        // 交叉格必须都还没填，且都有候选数
        if (intersection.Count == 0 || intersection.Any(c => cand.Count(c) == 0))
        {
            return null;
        }

        boxOnly.RemoveAll(c => cand.Count(c) == 0);
        lineOnly.RemoveAll(c => cand.Count(c) == 0);

        int intersectionMask = MaskOf(cand, intersection);
        int cellCount = intersection.Count;

        TechniqueStep? best = null;
        int bestEliminations = 0;

        foreach (int[] boxExtra in Subsets(boxOnly, MaxExtraCells))
        {
            var a = new List<int>(intersection);
            a.AddRange(boxExtra);
            int aMask = intersectionMask | MaskOf(cand, boxExtra);
            int aSize = a.Count;

            foreach (int[] lineExtra in Subsets(lineOnly, MaxExtraCells))
            {
                // 两侧都要补格，才是一个真正的「双翼」Sue de Coq
                if (boxExtra.Length == 0 || lineExtra.Length == 0)
                {
                    continue;
                }

                var b = new List<int>(intersection);
                b.AddRange(lineExtra);
                int bMask = intersectionMask | MaskOf(cand, lineExtra);
                int bSize = b.Count;

                // 关键：宫这一侧、线这一侧各自都必须是锁定集（同侧格两两同族，格数=候选数才有意义）
                if (aSize != SudokuGrid.CountDigits(aMask) || bSize != SudokuGrid.CountDigits(bMask))
                {
                    continue;
                }

                int unionMask = aMask | bMask;

                var eliminations = new List<CandidateRef>();
                var boxDigits = new List<int>();
                var lineDigits = new List<int>();

                foreach (int digit in SudokuGrid.Digits(unionMask))
                {
                    // 宫侧这一组既然是锁定集，宫里那一次必定落在 a 里面 ⇒ 宫里其他格子全组候选数都不能填
                    if (ContainsDigit(cand, a, digit))
                    {
                        boxDigits.Add(digit);
                        AddEliminations(cand, boxUnit, a, digit, eliminations);
                    }

                    if (ContainsDigit(cand, b, digit))
                    {
                        lineDigits.Add(digit);
                        AddEliminations(cand, lineUnit, b, digit, eliminations);
                    }
                }

                // 两侧各要贡献「只在自己这侧出现」的候选数，才是一个真正的双翼 Sue de Coq；
                // 否则宫侧那组本身就是个普通锁定集（裸数对/三数组那一类），不该算到这个技巧头上。
                // 注意也绝不能用「两侧共有的候选数」做额外删除：一个数字在盘上出现 9 次（每行/每列/每宫各一次），
                // 「宫侧那组里有它」说的是宫里那一次、「线侧那组里有它」说的是线上那一次，两者不是同一格
                // （阶段九实测：据此推「只能落在交叉格」会删掉 R1C5(7) 这类正解）。
                bool boxExclusive = false;
                bool lineExclusive = false;
                foreach (int digit in SudokuGrid.Digits(unionMask))
                {
                    bool inBoxSide = ContainsDigit(cand, a, digit);
                    bool inLineSide = ContainsDigit(cand, b, digit);
                    boxExclusive |= inBoxSide && !inLineSide;
                    lineExclusive |= inLineSide && !inBoxSide;
                }

                if (!boxExclusive || !lineExclusive)
                {
                    continue;
                }

                if (eliminations.Count <= bestEliminations)
                {
                    continue;
                }

                // 两侧都要有「只在自己这侧出现」的候选数，才是一个像样的 Sue de Coq
                if (boxDigits.Count == 0 || lineDigits.Count == 0)
                {
                    continue;
                }

                bestEliminations = eliminations.Count;
                best = BuildStep(
                    cand, byRow, line, box, intersection, a, b, boxDigits, lineDigits,
                    aMask, bMask, eliminations);
            }
        }

        return best;
    }

    private static TechniqueStep BuildStep(
        ChainTechniques.Candidates cand,
        bool byRow,
        int line,
        int box,
        List<int> intersection,
        List<int> a,
        List<int> b,
        List<int> boxDigits,
        List<int> lineDigits,

        int aMask,
        int bMask,
        List<CandidateRef> eliminations)
    {
        string lineName = byRow ? $"第 {line + 1} 行" : $"第 {line + 1} 列";
        string boxName = $"第 {box + 1} 宫";
        string iText = string.Join("+", intersection.Select(ChainTechniques.CellName));
        string aText = string.Join("+", a.Select(ChainTechniques.CellName));
        string bText = string.Join("+", b.Select(ChainTechniques.CellName));
        string aDigits = string.Join("/", SudokuGrid.Digits(aMask));
        string bDigits = string.Join("/", SudokuGrid.Digits(bMask));
        string boxOnly = Join(SudokuGrid.Digits(aMask & ~bMask).ToList());
        string lineOnly = Join(SudokuGrid.Digits(bMask & ~aMask).ToList());
        string elimDesc = ChainTechniques.Join(eliminations);

        var derivation = new List<string>
        {
            $"{lineName}与{boxName}交叉在 {iText}。",
            $"宫这一侧取 {aText}（全在{boxName}内），共 {a.Count} 格，候选数只有 {aDigits} 这 " +
            $"{SudokuGrid.CountDigits(aMask)} 个——格数与候选数一样多，又全是宫里的同族格，" +
            $"所以 {aDigits} 在 {aText} 里各自恰好出现一次，{boxName}里其他格子都不能再填它们。",
            $"线这一侧取 {bText}（全在{lineName}内），共 {b.Count} 格，候选数只有 {bDigits} 这 " +
            $"{SudokuGrid.CountDigits(bMask)} 个，同样各自恰好出现一次，{lineName}上其他格子都不能再填它们。",
            $"其中 {boxOnly} 只在宫侧这一组出现、{lineOnly} 只在线侧这一组出现——两侧互相牵制，这就是 Sue de Coq。",
            $"所以删除：{elimDesc}。",
        };

        var marks = new List<HintMark>();
        foreach (int cell in intersection)
        {
            marks.Add(new HintMark(cell, 0, HintMarkRole.Pattern, 0));
        }



        foreach (int cell in a)
        {
            foreach (int digit in boxDigits)
            {
                if (cand.Has(cell, digit))
                {
                    marks.Add(new HintMark(cell, digit, HintMarkRole.DigitA, 2));
                }
            }
        }

        foreach (int cell in b)
        {
            foreach (int digit in lineDigits)
            {
                if (cand.Has(cell, digit))
                {
                    marks.Add(new HintMark(cell, digit, HintMarkRole.DigitB, 3));
                }
            }
        }

        foreach (CandidateRef elimination in eliminations)
        {
            marks.Add(new HintMark(elimination.Cell, elimination.Digit, HintMarkRole.Elimination, 4));
        }

        var highlights = new List<int>(a);
        highlights.AddRange(b);

        string description =
            $"Sue de Coq：{lineName}与{boxName}的交叉格 {iText}；宫侧 {aText} 这 {a.Count} 格只含 {aDigits}，" +
            $"线侧 {bText} 这 {b.Count} 格只含 {bDigits}，各自都是锁定集（{boxOnly} 只在宫侧、{lineOnly} 只在线侧），" +
            $"故这些候选数在宫/线上都只能待在这两组格里，删除 {elimDesc}。";

        return new TechniqueStep(
            Technique.SueDeCoq,
            highlights,
            -1,
            0,
            eliminations,
            description,
            Array.Empty<TechniqueLink>(),
            derivation,
            HintMarks.Normalize(marks));
    }

    private static void AddEliminations(
        ChainTechniques.Candidates cand,
        int unit,
        IReadOnlyList<int> allowed,
        int digit,
        List<CandidateRef> eliminations)
    {
        foreach (int cell in SudokuGrid.AllUnits[unit])
        {
            if (allowed.Contains(cell) || !cand.Has(cell, digit))
            {
                continue;
            }

            var reference = new CandidateRef(cell, digit);
            if (!eliminations.Contains(reference))
            {
                eliminations.Add(reference);
            }
        }
    }

    private static bool ContainsDigit(ChainTechniques.Candidates cand, IReadOnlyList<int> cells, int digit)
    {
        foreach (int cell in cells)
        {
            if (cand.Has(cell, digit))
            {
                return true;
            }
        }

        return false;
    }

    private static int MaskOf(ChainTechniques.Candidates cand, IReadOnlyList<int> cells)
    {
        int mask = 0;
        foreach (int cell in cells)
        {
            mask |= cand.Mask(cell);
        }

        return mask;
    }

    private static IEnumerable<int[]> Subsets(IReadOnlyList<int> items, int maxSize)
    {
        for (int size = 0; size <= maxSize && size <= items.Count; size++)
        {
            foreach (int[] combo in ChainTechniques.Combinations(items, size))
            {
                yield return combo;
            }
        }
    }

    private static string Join(IReadOnlyList<int> digits) =>
        digits.Count == 0 ? "（无）" : string.Join("、", digits);
}
