namespace Sudoku.Core;

/// <summary>
/// 链式搜索（阶段二）：X 链（单数字链）、XY 链（双值格链）与交替推导链（AIC）。
/// <para>
/// 模型：节点 = 某格的某个候选数；强链 = 两者必有一真；弱链 = 两者不能同真。
/// 链以「强—弱—强—弱……—强」交替，因此链的两端至少有一个成立，
/// 于是凡是同时与两端构成弱链的候选数都可以删除（三者中总有一个与真者冲突）。
/// </para>
/// <para>
/// 节点编号 = cell * 9 + (digit - 1)，共 729 个。搜索带「扩展次数预算」，
/// 保证在最坏情况下也能在毫秒级返回，宁可少找几条链，绝不给出错误结论。
/// </para>
/// </summary>
public static class ChainSearch
{
    /// <summary>节点总数。</summary>
    private const int NodeCount = SudokuGrid.CellCount * SudokuGrid.Size;

    private enum Kind
    {
        XChain,
        XYChain,
        Aic,
    }

    /// <summary>X 链：整条链使用同一个数字。</summary>
    public static IEnumerable<TechniqueStep> FindXChains(Board board, int maxLinks = 7) => FindXChains(MasksOf(board), maxLinks);

    internal static IEnumerable<TechniqueStep> FindXChains(int[] masks, int maxLinks = 7)
    {
        for (int digit = 1; digit <= SudokuGrid.Size; digit++)
        {
            foreach (TechniqueStep step in SearchCore(masks, Kind.XChain, maxLinks, digit, minLinks: 3, firstOnly: false))
            {
                yield return step;
            }
        }
    }

    /// <summary>XY 链：整条链都由双值格组成。</summary>
    public static IEnumerable<TechniqueStep> FindXYChains(Board board, int maxLinks = 9) => FindXYChains(MasksOf(board), maxLinks);

    internal static IEnumerable<TechniqueStep> FindXYChains(int[] masks, int maxLinks = 9) =>
        SearchCore(masks, Kind.XYChain, maxLinks, filterDigit: 0, minLinks: 7, firstOnly: false);

    /// <summary>交替推导链（AIC）：允许同格不同数字的弱链，覆盖更广。</summary>
    public static IEnumerable<TechniqueStep> FindAics(Board board, int maxLinks = 7) => FindAics(MasksOf(board), maxLinks);

    internal static IEnumerable<TechniqueStep> FindAics(int[] masks, int maxLinks = 7) =>
        SearchCore(masks, Kind.Aic, maxLinks, filterDigit: 0, minLinks: 5, firstOnly: false);

    /// <summary>只找第一条链（供「一键提示」使用，命中即停，速度优先）。按等级从低到高依次尝试。</summary>
    internal static TechniqueStep? FindFirstChain(int[] masks, int maxLevel)
    {
        if (maxLevel >= 7)
        {
            for (int digit = 1; digit <= SudokuGrid.Size; digit++)
            {
                TechniqueStep? step = SearchCore(masks, Kind.XChain, 7, digit, minLinks: 3, firstOnly: true).FirstOrDefault();
                if (step is not null)
                {
                    return step;
                }
            }
        }

        if (maxLevel >= 8)
        {
            TechniqueStep? step = SearchCore(masks, Kind.XYChain, 9, 0, minLinks: 7, firstOnly: true).FirstOrDefault();
            if (step is not null)
            {
                return step;
            }
        }

        if (maxLevel < 9)
        {
            return null;
        }

        return SearchCore(masks, Kind.Aic, 7, 0, minLinks: 5, firstOnly: true).FirstOrDefault();
    }

    private static int[] MasksOf(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return board.ComputeCandidates();
    }

    // ------------------------------------------------------------------
    // 搜索主体
    // ------------------------------------------------------------------

    private static List<TechniqueStep> SearchCore(
        int[] masks,
        Kind kind,
        int maxLinks,
        int filterDigit,
        int minLinks,
        bool firstOnly)
    {
        // 强/弱链都要求候选数真实存在
        var allowed = new bool[NodeCount];
        for (int node = 0; node < NodeCount; node++)
        {
            allowed[node] = IsAllowed(masks, kind, filterDigit, node);
        }

        var results = new List<TechniqueStep>();
        var chainNodes = new List<int>(maxLinks + 1);
        var chainLinks = new List<TechniqueLink>(maxLinks);
        var onChain = new bool[NodeCount];
        var reported = new HashSet<long>();

        int budget = firstOnly ? 60000 : 400000;
        int expansions = 0;
        bool stopped = false;

        for (int start = 0; start < NodeCount && !stopped; start++)
        {
            if (!allowed[start])
            {
                continue;
            }

            foreach (int second in StrongNeighbors(masks, kind, allowed, start))
            {
                chainNodes.Clear();
                chainLinks.Clear();
                chainNodes.Add(start);
                chainNodes.Add(second);
                chainLinks.Add(DescribeStrongLink(masks, kind, start, second));
                onChain[start] = true;
                onChain[second] = true;

                Explore(second, links: 1, lastWasStrong: true);

                onChain[start] = false;
                onChain[second] = false;

                if (stopped)
                {
                    break;
                }
            }
        }

        return results;

        void Explore(int end, int links, bool lastWasStrong)
        {
            if (links >= minLinks && lastWasStrong)
            {
                TryEliminate(end, links);
                if (stopped)
                {
                    return;
                }
            }

            if (links >= maxLinks || ++expansions > budget)
            {
                if (expansions > budget)
                {
                    stopped = true;
                }

                return;
            }

            List<int> next = lastWasStrong
                ? WeakNeighbors(masks, kind, allowed, end)
                : StrongNeighbors(masks, kind, allowed, end);

            foreach (int node in next)
            {
                if (onChain[node] || !allowed[node])
                {
                    continue;
                }

                onChain[node] = true;
                chainNodes.Add(node);
                chainLinks.Add(lastWasStrong
                    ? DescribeWeakLink(masks, kind, end, node)
                    : DescribeStrongLink(masks, kind, end, node));

                Explore(node, links + 1, !lastWasStrong);

                chainLinks.RemoveAt(chainLinks.Count - 1);
                chainNodes.RemoveAt(chainNodes.Count - 1);
                onChain[node] = false;

                if (stopped)
                {
                    return;
                }
            }
        }

        void TryEliminate(int end, int links)
        {
            int start = chainNodes[0];
            long key = ((long)Math.Min(start, end) << 16) | (uint)Math.Max(start, end);
            if (!reported.Add(key))
            {
                return;
            }

            var eliminations = new List<CandidateRef>();
            foreach (int candidate in WeakNeighbors(masks, kind, allowed, start))
            {
                if (candidate == start || candidate == end || onChain[candidate])
                {
                    continue;
                }

                if (!IsWeakPair(masks, candidate, end))
                {
                    continue;
                }

                eliminations.Add(new CandidateRef(CellOf(candidate), DigitOf(candidate)));
            }

            if (eliminations.Count == 0)
            {
                reported.Remove(key);
                return;
            }

            results.Add(BuildStep(masks, kind, chainNodes, chainLinks, eliminations, links));

            if (firstOnly)
            {
                stopped = true;
            }
        }
    }

    // ------------------------------------------------------------------
    // 链的构造
    // ------------------------------------------------------------------

    private static TechniqueStep BuildStep(
        int[] masks,
        Kind kind,
        IReadOnlyList<int> chainNodes,
        IReadOnlyList<TechniqueLink> links,
        IReadOnlyList<CandidateRef> eliminations,
        int linkCount)
    {
        Technique technique = kind switch
        {
            Kind.XChain => Technique.XChain,
            Kind.XYChain => Technique.XYChain,
            _ => ClassifyAic(chainNodes),
        };

        int start = chainNodes[0];
        int end = chainNodes[^1];
        var highlights = chainNodes.Select(CellOf).Distinct().OrderBy(c => c).ToArray();
        string elimDesc = string.Join("、", eliminations.Select(e => e.ToString()));
        string chainExpr = BuildChainExpression(links);

        string description = technique switch
        {
            Technique.XChain =>
                $"X 链：数字 {DigitOf(start)} 的强链与弱链交替推进，{NodeText(start)} → {NodeText(end)}。" +
                $"链两端至少有一个成立，故同时看见两端的 {elimDesc} 可以删除。",
            Technique.XYChain =>
                $"XY 链：由 {highlights.Length} 个双值格串联，{NodeText(start)} → {NodeText(end)}。" +
                $"链两端至少有一个成立，故删除 {elimDesc}。",
            _ =>
                $"交替推导链（AIC）：{NodeText(start)} → {NodeText(end)} 强弱交替推断，" +
                $"链两端至少有一个成立，故删除 {elimDesc}。",
        };

        var derivation = new List<string> { $"链表达式：{chainExpr}" };
        derivation.AddRange(links.Select(l => $"{l.From} {l.KindName} {l.To}：{l.Reason}"));
        derivation.Add($"链两端 {NodeText(start)} 与 {NodeText(end)} 至少有一个成立（共 {linkCount} 条推断，强弱交替且两端为强链）。");
        derivation.Add($"同时看见这两个端点的位置都无法成立，删除：{elimDesc}。");

        return new TechniqueStep(
            technique,
            highlights,
            -1,
            0,
            eliminations,
            description,
            links.ToArray(),
            derivation);
    }

    private static Technique ClassifyAic(IReadOnlyList<int> chainNodes)
    {
        int digit = DigitOf(chainNodes[0]);
        return chainNodes.All(n => DigitOf(n) == digit) ? Technique.XChain : Technique.Aic;
    }

    private static string BuildChainExpression(IReadOnlyList<TechniqueLink> links)
    {
        if (links.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string> { NodeText(links[0].From) };
        foreach (TechniqueLink link in links)
        {
            parts.Add(link.IsStrong ? "==" : "--");
            parts.Add(NodeText(link.To));
        }

        return string.Join(" ", parts);
    }

    // ------------------------------------------------------------------
    // 强链 / 弱链
    // ------------------------------------------------------------------

    /// <summary>强链邻居：某单元内该数字只剩两处（共轭对），或双值格内的另一个候选数。</summary>
    private static List<int> StrongNeighbors(int[] masks, Kind kind, bool[] allowed, int node)
    {
        var result = new List<int>(8);
        int cell = CellOf(node);
        int digit = DigitOf(node);

        if (kind != Kind.XYChain)
        {
            foreach (int unit in SudokuGrid.UnitsOf[cell])
            {
                List<int> holders = CellsWithCandidate(masks, unit, digit);
                if (holders.Count == 2)
                {
                    int other = holders[0] == cell ? holders[1] : holders[0];
                    int otherNode = Node(other, digit);
                    if (otherNode != node && allowed[otherNode])
                    {
                        result.Add(otherNode);
                    }
                }
            }
        }

        if (kind != Kind.XChain && SudokuGrid.CountDigits(masks[cell]) == 2)
        {
            int otherDigit = SudokuGrid.LowestDigit(masks[cell] & ~SudokuGrid.DigitBit(digit));
            int otherNode = Node(cell, otherDigit);
            if (otherNode != node && allowed[otherNode])
            {
                result.Add(otherNode);
            }
        }

        return result.Distinct().ToList();
    }

    /// <summary>弱链邻居：同单元内的同数字候选数；同格内的其他候选数。强链邻居不重复计入。</summary>
    private static List<int> WeakNeighbors(int[] masks, Kind kind, bool[] allowed, int node)
    {
        var result = new List<int>(24);
        int cell = CellOf(node);
        int digit = DigitOf(node);

        foreach (int unit in SudokuGrid.UnitsOf[cell])
        {
            foreach (int other in CellsWithCandidate(masks, unit, digit))
            {
                if (other == cell)
                {
                    continue;
                }

                int otherNode = Node(other, digit);
                if (allowed[otherNode] && !IsStrongPair(masks, kind, node, otherNode))
                {
                    result.Add(otherNode);
                }
            }
        }

        if (kind == Kind.Aic && SudokuGrid.CountDigits(masks[cell]) > 2)
        {
            foreach (int otherDigit in SudokuGrid.Digits(masks[cell]))
            {
                if (otherDigit == digit)
                {
                    continue;
                }

                int otherNode = Node(cell, otherDigit);
                if (allowed[otherNode])
                {
                    result.Add(otherNode);
                }
            }
        }

        return result.Distinct().ToList();
    }

    /// <summary>判断两个节点之间是否只是弱链关系（用于删除候选数的条件检查）。</summary>
    private static bool IsWeakPair(int[] masks, int a, int b)
    {
        if (a == b)
        {
            return false;
        }

        int cellA = CellOf(a);
        int cellB = CellOf(b);

        if (cellA == cellB)
        {
            return DigitOf(a) != DigitOf(b);
        }

        return DigitOf(a) == DigitOf(b) && SharesUnit(cellA, cellB);
    }

    private static bool IsStrongPair(int[] masks, Kind kind, int a, int b)
    {
        int cellA = CellOf(a);
        int cellB = CellOf(b);

        // 双值格内的两个候选数互为强链
        if (cellA == cellB)
        {
            return kind != Kind.XChain && SudokuGrid.CountDigits(masks[cellA]) == 2;
        }

        if (kind == Kind.XYChain)
        {
            return false;
        }

        int digit = DigitOf(a);
        if (digit != DigitOf(b))
        {
            return false;
        }

        // 必须是「共享的」单元里该数字只剩两处，才构成共轭对
        foreach (int unit in SudokuGrid.UnitsOf[cellA])
        {
            if (!SudokuGrid.UnitsOf[cellB].Contains(unit))
            {
                continue;
            }

            if (CellsWithCandidate(masks, unit, digit).Count == 2)
            {
                return true;
            }
        }

        return false;
    }

    private static TechniqueLink DescribeStrongLink(int[] masks, Kind kind, int a, int b)
    {
        int cellA = CellOf(a);
        int cellB = CellOf(b);

        if (cellA == cellB)
        {
            return new TechniqueLink(
                new CandidateRef(cellA, DigitOf(a)),
                new CandidateRef(cellB, DigitOf(b)),
                true,
                "同一格内只剩这两个候选数，必有一真");
        }

        int digit = DigitOf(a);
        string unit = SharedUnitName(cellA, cellB, digit, masks, out bool isRow);
        return new TechniqueLink(
            new CandidateRef(cellA, digit),
            new CandidateRef(cellB, digit),
            true,
            $"{(isRow ? "第 " + (SudokuGrid.Row(cellA) + 1) + " 行" : unit)}内 {digit} 只剩这两处，必有一真");
    }

    private static TechniqueLink DescribeWeakLink(int[] masks, Kind kind, int a, int b)
    {
        int cellA = CellOf(a);
        int cellB = CellOf(b);

        if (cellA == cellB)
        {
            return new TechniqueLink(
                new CandidateRef(cellA, DigitOf(a)),
                new CandidateRef(cellB, DigitOf(b)),
                false,
                "同一格内两个候选数不能同时成立");
        }

        int digit = DigitOf(a);
        string where = SharedUnitName(cellA, cellB, digit, masks, out _);
        return new TechniqueLink(
            new CandidateRef(cellA, digit),
            new CandidateRef(cellB, digit),
            false,
            $"{where}内的 {digit} 不能同时成立");
    }

    private static string SharedUnitName(int cellA, int cellB, int digit, int[] masks, out bool isRow)
    {
        isRow = SudokuGrid.Row(cellA) == SudokuGrid.Row(cellB);
        if (isRow)
        {
            return $"第 {SudokuGrid.Row(cellA) + 1} 行";
        }

        if (SudokuGrid.Col(cellA) == SudokuGrid.Col(cellB))
        {
            return $"第 {SudokuGrid.Col(cellA) + 1} 列";
        }

        return $"第 {SudokuGrid.Box(cellA) + 1} 宫";
    }

    // ------------------------------------------------------------------
    // 基础工具
    // ------------------------------------------------------------------

    private static bool IsAllowed(int[] masks, Kind kind, int filterDigit, int node)
    {
        int cell = CellOf(node);
        int digit = DigitOf(node);

        if ((masks[cell] & SudokuGrid.DigitBit(digit)) == 0)
        {
            return false;
        }

        if (filterDigit != 0 && digit != filterDigit)
        {
            return false;
        }

        return kind != Kind.XYChain || SudokuGrid.CountDigits(masks[cell]) == 2;
    }

    private static List<int> CellsWithCandidate(int[] masks, int unitId, int digit)
    {
        int bit = SudokuGrid.DigitBit(digit);
        var result = new List<int>(SudokuGrid.Size);
        foreach (int cell in SudokuGrid.AllUnits[unitId])
        {
            if ((masks[cell] & bit) != 0)
            {
                result.Add(cell);
            }
        }

        return result;
    }

    private static bool SharesUnit(int a, int b) =>
        SudokuGrid.Row(a) == SudokuGrid.Row(b)
        || SudokuGrid.Col(a) == SudokuGrid.Col(b)
        || SudokuGrid.Box(a) == SudokuGrid.Box(b);

    private static int Node(int cell, int digit) => (cell * SudokuGrid.Size) + digit - 1;

    private static int CellOf(int node) => node / SudokuGrid.Size;

    private static int DigitOf(int node) => (node % SudokuGrid.Size) + 1;

    private static string NodeText(int node) => $"R{SudokuGrid.Row(CellOf(node)) + 1}C{SudokuGrid.Col(CellOf(node)) + 1}({DigitOf(node)})";

    private static string NodeText(CandidateRef candidate) =>
        $"R{SudokuGrid.Row(candidate.Cell) + 1}C{SudokuGrid.Col(candidate.Cell) + 1}({candidate.Digit})";
}
