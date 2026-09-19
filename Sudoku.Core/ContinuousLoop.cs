namespace Sudoku.Core;

/// <summary>
/// 连续环（Continuous Nice Loop，阶段九）。
///
/// 把候选数当节点、强链/弱链当边，若存在一个首尾相接、强弱交替的环，
/// 并且环上每个候选数恰好「一强一弱」两条边（这就是「连续」），
/// 那么环上的候选数会严格按交替的两组分为「真 / 假」，可以放心地做三种删除：
///
/// 1. 环上某条弱链的两端在同一个单元里、数字相同 → 该单元其他格的这个数字可以删
///    （该数字在这个单元里只能落在这两端之一）。
/// 2. 环上某条强链的两端在同一个单元里、数字相同（共轭对）→ 这两格必然填这个数字，
///    所以这两格里的其他候选数可以删。
/// 3. 环经过同一格的两种候选数 → 这一格必然是其中之一，其他候选数可以删。
///
/// （与已实现的「交替推导链 AIC」互补：AIC 处理不连续环的两个端点，这里处理闭环。）
/// </summary>
public static class ContinuousLoop
{
    /// <summary>环上最多几个候选数（长环既慢又难读）。</summary>
    private const int MaxNodes = 10;

    /// <summary>搜索时最多展开多少个节点，防止硬题上卡住。</summary>
    private const int MaxVisits = 120_000;

    /// <summary>从盘面查找连续环。</summary>
    public static IEnumerable<TechniqueStep> FindContinuousLoops(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return FindContinuousLoops(board.ComputeCandidates());
    }

    /// <summary>从候选数掩码快照查找连续环。</summary>
    internal static IEnumerable<TechniqueStep> FindContinuousLoops(int[] masks)
    {
        var graph = new Graph(masks);
        if (graph.NodeCount == 0)
        {
            return Array.Empty<TechniqueStep>();
        }

        var steps = new List<TechniqueStep>();
        var seen = new HashSet<string>();
        int visits = 0;
        int[] path = new int[MaxNodes + 1];

        for (int start = 0; start < graph.NodeCount; start++)
        {
            path[0] = start;
            Walk(start, 1, true);
            if (visits > MaxVisits)
            {
                break;
            }
        }

        return steps;

        void Walk(int current, int depth, bool strongNext)
        {
            if (depth > MaxNodes || visits > MaxVisits)
            {
                return;
            }

            List<int> next = strongNext ? graph.Strong(current) : graph.Weak(current);
            foreach (int node in next)
            {
                visits++;

                if (node == path[0])
                {
                    // 收口：环上至少 4 个候选数（边上强弱交替由搜索本身保证）
                    if (depth >= 4)
                    {
                        Record(path, depth);
                    }

                    continue;
                }

                if (node < path[0] || Contains(path, depth, node))
                {
                    continue;
                }

                path[depth] = node;
                Walk(node, depth + 1, !strongNext);
            }
        }

        void Record(int[] nodes, int count)
        {
            var loop = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                loop.Add(nodes[i]);
            }

            // 只绕两格的「环」其实就是数对 / 双值格，属于更基础的技巧，不在这里重复报
            if (loop.Select(Graph.CellOf).Distinct().Count() < 3)
            {
                return;
            }

            if (!IsContinuous(graph, loop))
            {
                return;
            }

            List<CandidateRef> eliminations = Eliminations(graph, loop, out string[] reasons);
            if (eliminations.Count == 0)
            {
                return;
            }

            string key = string.Join(",", loop.OrderBy(n => n));
            if (seen.Add(key))
            {
                steps.Add(BuildStep(graph, loop, eliminations, reasons));
            }
        }
    }

    private static bool Contains(int[] path, int count, int node)
    {
        for (int i = 0; i < count; i++)
        {
            if (path[i] == node)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>连续环要求环上每个节点都恰好一条强链、一条弱链。</summary>
    private static bool IsContinuous(Graph graph, IReadOnlyList<int> loop)
    {
        for (int i = 0; i < loop.Count; i++)
        {
            int node = loop[i];
            int previous = loop[(i + loop.Count - 1) % loop.Count];
            int following = loop[(i + 1) % loop.Count];

            bool prevStrong = graph.IsStrong(node, previous);
            bool nextStrong = graph.IsStrong(node, following);

            if (prevStrong == nextStrong)
            {
                return false; // 两条都强或两条都弱 → 不是连续环
            }
        }

        return true;
    }

    private static List<CandidateRef> Eliminations(Graph graph, IReadOnlyList<int> loop, out string[] reasons)
    {
        var eliminations = new List<CandidateRef>();
        var reasonList = new List<string>();

        for (int i = 0; i < loop.Count; i++)
        {
            int a = loop[i];
            int b = loop[(i + 1) % loop.Count];
            int cellA = Graph.CellOf(a);
            int cellB = Graph.CellOf(b);
            int digitA = Graph.DigitOf(a);
            int digitB = Graph.DigitOf(b);
            bool strong = graph.IsStrong(a, b);

            if (!strong && cellA != cellB && digitA == digitB)
            {
                // 规则 1：同单元同数字的弱链 → 该单元其他格的这个数字可删
                int unit = SharedUnit(cellA, cellB);
                if (unit >= 0)
                {
                    foreach (int cell in SudokuGrid.AllUnits[unit])
                    {
                        if (cell == cellA || cell == cellB || !graph.Has(cell, digitA))
                        {
                            continue;
                        }

                        Add(eliminations, new CandidateRef(cell, digitA));
                    }

                    reasonList.Add($"环上 {Name(a)} -- {Name(b)} 是同单元同数字的弱链，{UnitName(unit)}里只有这两处能放 {digitA}");
                }
            }

            if (strong && cellA == cellB)
            {
                // 规则 2：同一格内的强链（双值格）在连续环里变成共轭 → 该格必然是其一的那个数字，
                // 其他候选数可以删。注意：跨格的同数字强链（共轭对）不能据此删除格里其他候选数。
                foreach (int digit in SudokuGrid.Digits(graph.Mask(cellA)))
                {
                    if (digit != digitA && digit != digitB)
                    {
                        Add(eliminations, new CandidateRef(cellA, digit));
                    }
                }

                reasonList.Add($"环经过 {ChainTechniques.CellName(cellA)} 的 {digitA} 与 {digitB}，该格必是其中之一");
            }

            if (cellA == cellB && !strong)
            {
                // 规则 3：同一格的两种候选数都在环上 → 该格必然是其中之一
                foreach (int digit in SudokuGrid.Digits(graph.Mask(cellA)))
                {
                    if (digit != digitA && digit != digitB)
                    {
                        Add(eliminations, new CandidateRef(cellA, digit));
                    }
                }

                reasonList.Add($"环经过 {ChainTechniques.CellName(cellA)} 的 {digitA} 与 {digitB} 两个候选数，该格必是其中之一");
            }
        }

        reasons = reasonList.Distinct().ToArray();
        return eliminations;
    }

    private static void Add(List<CandidateRef> list, CandidateRef candidate)
    {
        if (!list.Contains(candidate))
        {
            list.Add(candidate);
        }
    }

    private static TechniqueStep BuildStep(
        Graph graph,
        IReadOnlyList<int> loop,
        IReadOnlyList<CandidateRef> eliminations,
        IReadOnlyList<string> reasons)
    {
        string expression = string.Join(" ", Enumerable.Range(0, loop.Count).Select(i =>
        {
            int a = loop[i];
            int b = loop[(i + 1) % loop.Count];
            return $"{Name(a)} {(graph.IsStrong(a, b) ? "==" : "--")} {Name(b)}";
        }));

        string elimDesc = ChainTechniques.Join(eliminations);

        var derivation = new List<string>
        {
            $"把候选数连成环：{expression.Replace("R", " R").Trim()}。",
            "环上强弱链交替，而且每个候选数正好一强一弱两条边——这就是「连续环」。",
            "连续环上的候选数只能按交替的两组取真/假，于是可以放心做下面的删除。",
        };
        derivation.AddRange(reasons);
        derivation.Add($"所以删除：{elimDesc}。");

        var marks = new List<HintMark>();
        var links = new List<TechniqueLink>();

        for (int i = 0; i < loop.Count; i++)
        {
            int a = loop[i];
            int b = loop[(i + 1) % loop.Count];
            bool strong = graph.IsStrong(a, b);
            var from = new CandidateRef(Graph.CellOf(a), Graph.DigitOf(a));
            var to = new CandidateRef(Graph.CellOf(b), Graph.DigitOf(b));

            links.Add(new TechniqueLink(from, to, strong, strong ? "强链（共轭对或双值格）" : "弱链（同单元/同格）"));
            marks.Add(new HintMark(from.Cell, from.Digit, HintMarkRole.DigitA, 1));
            marks.Add(new HintMark(to.Cell, to.Digit, HintMarkRole.DigitB, 1));
        }

        foreach (CandidateRef elimination in eliminations)
        {
            marks.Add(new HintMark(elimination.Cell, elimination.Digit, HintMarkRole.Elimination, derivation.Count - 1));
        }

        var highlights = loop
            .Select(Graph.CellOf)
            .Distinct()
            .ToArray();

        string description =
            $"连续环（{loop.Count} 个候选数）：{expression}；环上的强弱链交替，可删除 {elimDesc}。";

        return new TechniqueStep(
            Technique.ContinuousLoop,
            highlights,
            -1,
            0,
            eliminations,
            description,
            links,
            derivation,
            HintMarks.Normalize(marks));
    }

    private static int SharedUnit(int a, int b)
    {
        foreach (int unit in SudokuGrid.UnitsOf[a])
        {
            if (SudokuGrid.AllUnits[unit].Contains(b))
            {
                return unit;
            }
        }

        return -1;
    }

    private static string UnitName(int unit) => unit switch
    {
        < SudokuGrid.Size => $"第 {unit + 1} 行",
        < 2 * SudokuGrid.Size => $"第 {unit - SudokuGrid.Size + 1} 列",
        _ => $"第 {unit - (2 * SudokuGrid.Size) + 1} 宫",
    };

    private static string Name(int node) => ChainTechniques.CellName(Graph.CellOf(node)) + $"({Graph.DigitOf(node)})";

    /// <summary>候选数图：节点 = 格 × 数字，边 = 强链 / 弱链。</summary>
    private sealed class Graph
    {
        private readonly int[] _masks;
        private readonly List<int>[] _strong;
        private readonly List<int>[] _weak;

        public Graph(int[] masks)
        {
            _masks = masks;
            _strong = new List<int>[SudokuGrid.CellCount * SudokuGrid.Size];
            _weak = new List<int>[SudokuGrid.CellCount * SudokuGrid.Size];

            for (int i = 0; i < _strong.Length; i++)
            {
                _strong[i] = new List<int>(6);
                _weak[i] = new List<int>(24);
            }

            Build();
        }

        public static int NodeOf(int cell, int digit) => (cell * SudokuGrid.Size) + digit - 1;

        public static int CellOf(int node) => node / SudokuGrid.Size;

        public static int DigitOf(int node) => (node % SudokuGrid.Size) + 1;

        public int NodeCount => SudokuGrid.CellCount * SudokuGrid.Size;

        public bool Has(int cell, int digit) => (_masks[cell] & SudokuGrid.DigitBit(digit)) != 0;

        public int Mask(int cell) => _masks[cell];

        public List<int> Strong(int node) => _strong[node];

        public List<int> Weak(int node) => _weak[node];

        public bool IsStrong(int a, int b) => _strong[a].Contains(b);

        private void Build()
        {
            for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
            {
                int mask = _masks[cell];
                if (mask == 0)
                {
                    continue;
                }

                foreach (int digit in SudokuGrid.Digits(mask))
                {
                    int node = NodeOf(cell, digit);

                    // 同格内的其他候选数是弱链
                    foreach (int other in SudokuGrid.Digits(mask))
                    {
                        if (other != digit)
                        {
                            _weak[node].Add(NodeOf(cell, other));
                        }
                    }

                    // 双值格内的两个候选数是强链
                    if (SudokuGrid.CountDigits(mask) == 2)
                    {
                        int other = SudokuGrid.LowestDigit(mask & ~SudokuGrid.DigitBit(digit));
                        _strong[node].Add(NodeOf(cell, other));
                    }

                    foreach (int unit in SudokuGrid.UnitsOf[cell])
                    {
                        var holders = new List<int>(SudokuGrid.Size);
                        foreach (int c in SudokuGrid.AllUnits[unit])
                        {
                            if ((_masks[c] & SudokuGrid.DigitBit(digit)) != 0)
                            {
                                holders.Add(c);
                            }
                        }

                        if (holders.Count < 2)
                        {
                            continue;
                        }

                        // 共轭对（该单元里这个数字只剩两处）是强链
                        if (holders.Count == 2)
                        {
                            int otherCell = holders[0] == cell ? holders[1] : holders[0];
                            _strong[node].Add(NodeOf(otherCell, digit));
                        }

                        foreach (int otherCell in holders)
                        {
                            if (otherCell != cell)
                            {
                                _weak[node].Add(NodeOf(otherCell, digit));
                            }
                        }
                    }
                }
            }

            // 共轭对在数学上「又是强链又是弱链」（至少一个真 / 至多一个真），
            // 所以弱链表里保留它们，循环搜索才不会漏掉大量环。
            for (int i = 0; i < _strong.Length; i++)
            {
                _strong[i] = _strong[i].Distinct().ToList();
                _weak[i] = _weak[i].Distinct().ToList();
            }
        }
    }
}
