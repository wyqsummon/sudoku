namespace Sudoku.Core;

/// <summary>
/// 链级技巧（阶段二）。统一以「候选数图」为模型：
/// 节点 = 某格某候选数；强链 = 两者必有一真，弱链 = 两者不能同真。
/// 本类实现有限图案类（X 翼 / 剑鱼 / XY 翼）；链式搜索类（X 链 / XY 链 / AIC）见 ChainSearch。
/// </summary>
public static class ChainTechniques
{
    /// <summary>X 翼：某数字在两行（或两列）中只出现在相同的两列（或两行）。</summary>
    public static IEnumerable<TechniqueStep> FindXWings(Board board) => FindXWings(MasksOf(board));

    /// <summary>剑鱼：某数字在三行（或三列）中只出现在相同的三列（或三行）。</summary>
    public static IEnumerable<TechniqueStep> FindSwordfishes(Board board) => FindSwordfishes(MasksOf(board));

    /// <summary>基于候选数掩码快照查找 X 翼（求解器内部使用，保证消除后不会重复命中）。</summary>
    internal static IEnumerable<TechniqueStep> FindXWings(int[] masks) => FindFish(masks, 2);

    /// <summary>基于候选数掩码快照查找剑鱼。</summary>
    internal static IEnumerable<TechniqueStep> FindSwordfishes(int[] masks) => FindFish(masks, 3);

    private static int[] MasksOf(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return board.ComputeCandidates();
    }

    // ------------------------------------------------------------------
    // 鱼形技巧（X 翼 / 剑鱼）
    // ------------------------------------------------------------------

    private static IEnumerable<TechniqueStep> FindFish(int[] masks, int size)
    {
        var cand = new Candidates(masks);

        for (int digit = 1; digit <= SudokuGrid.Size; digit++)
        {
            for (int orientation = 0; orientation < 2; orientation++)
            {
                bool byRow = orientation == 0;

                // 收集可用作「基准单元」的行/列：该数字在其内出现 2..size 次
                var bases = new List<(int Index, int[] Covers)>();
                for (int i = 0; i < SudokuGrid.Size; i++)
                {
                    int unitId = byRow ? i : SudokuGrid.Size + i;
                    List<int> cells = CellsWithCandidate(cand, unitId, digit);
                    if (cells.Count < 2 || cells.Count > size)
                    {
                        continue;
                    }

                    int[] covers = cells
                        .Select(c => byRow ? SudokuGrid.Col(c) : SudokuGrid.Row(c))
                        .Distinct()
                        .OrderBy(v => v)
                        .ToArray();

                    if (covers.Length < 2 || covers.Length > size)
                    {
                        continue;
                    }

                    bases.Add((i, covers));
                }

                foreach (var combo in Combinations(bases, size))
                {
                    var coverSet = new SortedSet<int>();
                    foreach (var b in combo)
                    {
                        foreach (int c in b.Covers)
                        {
                            coverSet.Add(c);
                        }
                    }

                    // 覆盖单元数量必须正好等于 size，且每个基准单元的候选都落在覆盖集合内
                    if (coverSet.Count != size)
                    {
                        continue;
                    }

                    if (combo.Any(b => b.Covers.Any(c => !coverSet.Contains(c))))
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

                            eliminations.Add(new CandidateRef(cell, digit));
                        }
                    }

                    if (eliminations.Count == 0)
                    {
                        continue;
                    }

                    yield return BuildFishStep(cand, digit, size, byRow, combo, coverSet.ToArray(), eliminations);
                }
            }
        }
    }

    private static TechniqueStep BuildFishStep(
        Candidates cand,
        int digit,
        int size,
        bool byRow,
        IReadOnlyList<(int Index, int[] Covers)> combo,
        IReadOnlyList<int> coverSet,
        IReadOnlyList<CandidateRef> eliminations)
    {
        Technique technique = size == 2 ? Technique.XWing : Technique.Swordfish;
        string baseName = byRow ? "行" : "列";
        string coverName = byRow ? "列" : "行";
        string name = size == 2 ? "X 翼" : "剑鱼";

        var highlights = new List<int>();
        var links = new List<TechniqueLink>();
        var baseDescs = new List<string>();

        foreach (var b in combo)
        {
            int unitId = byRow ? b.Index : SudokuGrid.Size + b.Index;
            List<int> cells = CellsWithCandidate(cand, unitId, digit);
            highlights.AddRange(cells);
            baseDescs.Add($"第 {b.Index + 1}{baseName}的 {string.Join("、", cells.Select(CellName))}");

            // 基准单元内只剩两处 → 这两处构成强链（必有一真），可直接画在棋盘上
            if (cells.Count == 2)
            {
                links.Add(new TechniqueLink(
                    new CandidateRef(cells[0], digit),
                    new CandidateRef(cells[1], digit),
                    true,
                    $"第 {b.Index + 1}{baseName}内 {digit} 只剩这两处，必有一真"));
            }
        }

        string coverDesc = string.Join("、", coverSet.Select(c => $"第 {c + 1}{coverName}"));
        string elimDesc = Join(eliminations);

        string description =
            $"数字 {digit} 在{string.Join("、", baseDescs)}中都只出现在 {coverDesc}，构成 {size}×{size} 的{name}：" +
            $"{coverDesc}上的 {digit} 已被这些{baseName}占用，故删除 {elimDesc}。";

        var derivation = new List<string>
        {
            $"观察数字 {digit} 的分布：{string.Join("；", baseDescs)}。",
            $"这些候选位置恰好落在 {coverDesc} 这 {size} 个{coverName}上，形成 {size}×{size} 的{name}。",
            $"无论怎么填，{coverDesc} 上的 {digit} 都必须落在这些{baseName}里，不可能出现在{coverName}的其他位置。",
            $"因此可以删除：{elimDesc}。",
        };

        return new TechniqueStep(
            technique,
            highlights.Distinct().OrderBy(c => c).ToArray(),
            -1,
            0,
            eliminations,
            description,
            links,
            derivation);
    }

    // ------------------------------------------------------------------
    // XY 翼
    // ------------------------------------------------------------------

    /// <summary>XY 翼：枢轴为双值格，两翼分别与枢轴共享一个候选数，并共享第三个数字。</summary>
    public static IEnumerable<TechniqueStep> FindXYWings(Board board) => FindXYWings(MasksOf(board));

    /// <summary>基于候选数掩码快照查找 XY 翼。</summary>
    internal static IEnumerable<TechniqueStep> FindXYWings(int[] masks)
    {
        var cand = new Candidates(masks);
        var seen = new HashSet<(int Pivot, int W1, int W2, int Z)>();

        for (int pivot = 0; pivot < SudokuGrid.CellCount; pivot++)
        {
            if (cand.Count(pivot) != 2)
            {
                continue;
            }

            int pivotMask = cand.Mask(pivot);

            var wings = new List<int>();
            foreach (int peer in SudokuGrid.Peers[pivot])
            {
                if (cand.Count(peer) == 2)
                {
                    wings.Add(peer);
                }
            }

            for (int i = 0; i < wings.Count; i++)
            {
                for (int j = i + 1; j < wings.Count; j++)
                {
                    int w1 = wings[i];
                    int w2 = wings[j];
                    int m1 = cand.Mask(w1);
                    int m2 = cand.Mask(w2);

                    int shared = m1 & m2;
                    if (SudokuGrid.CountDigits(shared) != 1)
                    {
                        continue; // 两翼必须共享恰好一个数字 z
                    }

                    int z = SudokuGrid.LowestDigit(shared);
                    if ((pivotMask & shared) != 0)
                    {
                        continue; // z 不能属于枢轴
                    }

                    int s1 = m1 & pivotMask;
                    int s2 = m2 & pivotMask;
                    if (SudokuGrid.CountDigits(s1) != 1 || SudokuGrid.CountDigits(s2) != 1 || s1 == s2)
                    {
                        continue; // 两翼必须分别对应枢轴的两个不同候选数
                    }

                    var key = (pivot, Math.Min(w1, w2), Math.Max(w1, w2), z);
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    var eliminations = new List<CandidateRef>();
                    foreach (int peer in SudokuGrid.Peers[w1])
                    {
                        if (peer == w1 || peer == w2 || !IsPeer(peer, w2) || !cand.Has(peer, z))
                        {
                            continue;
                        }

                        eliminations.Add(new CandidateRef(peer, z));
                    }

                    if (eliminations.Count == 0)
                    {
                        continue;
                    }

                    int x = SudokuGrid.LowestDigit(s1);
                    int y = SudokuGrid.LowestDigit(s2);
                    yield return BuildXYWingStep(pivot, w1, w2, x, y, z, eliminations);
                }
            }
        }
    }

    private static TechniqueStep BuildXYWingStep(
        int pivot,
        int w1,
        int w2,
        int x,
        int y,
        int z,
        IReadOnlyList<CandidateRef> eliminations)
    {
        string elimDesc = Join(eliminations);

        var links = new List<TechniqueLink>
        {
            new(new CandidateRef(pivot, x), new CandidateRef(pivot, y), true, "同一格内两候选必有一真（强链）"),
            new(new CandidateRef(pivot, x), new CandidateRef(w1, x), false, $"同单元内的 {x} 不能同真（弱链）"),
            new(new CandidateRef(pivot, y), new CandidateRef(w2, y), false, $"同单元内的 {y} 不能同真（弱链）"),
        };

        string description =
            $"XY 翼：枢轴 {CellName(pivot)} 只含 {x}/{y}，翼格 {CellName(w1)} 含 {x}/{z}、{CellName(w2)} 含 {y}/{z}。" +
            $"无论枢轴填 {x} 还是 {y}，两翼中总有一个是 {z}，故删除 {elimDesc}。";

        var derivation = new List<string>
        {
            $"枢轴 {CellName(pivot)} 是双值格，候选数为 {x}、{y}。",
            $"翼格 {CellName(w1)}（{x}/{z}）与 {CellName(w2)}（{y}/{z}）都与枢轴在同一行/列/宫内。",
            $"假设枢轴填 {x}：那么 {CellName(w2)} 不能再是 {y}，只能是 {z}。",
            $"假设枢轴填 {y}：那么 {CellName(w1)} 不能再是 {x}，只能是 {z}。",
            $"两种情况下，{CellName(w1)} 与 {CellName(w2)} 里总有一个是 {z}。",
            $"所以同时能看见这两格的位置都不能填 {z}，删除：{elimDesc}。",
        };

        return new TechniqueStep(
            Technique.XYWing,
            new[] { pivot, w1, w2 },
            -1,
            0,
            eliminations,
            description,
            links,
            derivation);
    }

    // ------------------------------------------------------------------
    // 工具
    // ------------------------------------------------------------------

    /// <summary>盘面候选数的只读快照（由调用方保证不被修改）。</summary>
    internal readonly struct Candidates
    {
        private readonly int[] _masks;

        public Candidates(int[] masks) => _masks = masks;

        public int Mask(int cell) => _masks[cell];

        public int Count(int cell) => SudokuGrid.CountDigits(_masks[cell]);

        public bool Has(int cell, int digit) => (_masks[cell] & SudokuGrid.DigitBit(digit)) != 0;
    }

    private static List<int> CellsWithCandidate(Candidates cand, int unitId, int digit)
    {
        var result = new List<int>(SudokuGrid.Size);
        foreach (int cell in SudokuGrid.AllUnits[unitId])
        {
            if (cand.Has(cell, digit))
            {
                result.Add(cell);
            }
        }

        return result;
    }

    internal static bool IsPeer(int a, int b) => (SudokuGrid.PeerMasks[a] & (UInt128.One << b)) != UInt128.Zero;

    internal static string CellName(int index) => $"R{SudokuGrid.Row(index) + 1}C{SudokuGrid.Col(index) + 1}";

    internal static string Join(IEnumerable<CandidateRef> refs) => string.Join("、", refs.Select(r => r.ToString()));

    internal static IEnumerable<T[]> Combinations<T>(IReadOnlyList<T> items, int size)
    {
        if (size > items.Count)
        {
            yield break;
        }

        var buffer = new T[size];
        foreach (T[] combo in Walk(0, 0))
        {
            yield return combo;
        }

        IEnumerable<T[]> Walk(int start, int depth)
        {
            if (depth == size)
            {
                yield return (T[])buffer.Clone();
                yield break;
            }

            for (int i = start; i <= items.Count - (size - depth); i++)
            {
                buffer[depth] = items[i];
                foreach (T[] combo in Walk(i + 1, depth + 1))
                {
                    yield return combo;
                }
            }
        }
    }
}
