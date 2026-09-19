namespace Sudoku.Core;

/// <summary>
/// 远程数对（Remote Pair）。一串「双值格」都只含同样的两个数字 a/b，
/// 相邻两格在某个单元内构成该数字的共轭对（该单元里这个数字只剩这两处），
/// 于是沿着链走一步，a/b 就互换一次：链长为奇数时两端必然一 a 一 b。
/// 因此同时能看见两端的位置都不能填 a 或 b。
/// </summary>
public static class RemotePair
{
    /// <summary>查找远程数对。</summary>
    public static IEnumerable<TechniqueStep> FindRemotePairs(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return FindRemotePairs(board.ComputeCandidates());
    }

    /// <summary>基于候选数掩码快照查找远程数对。</summary>
    internal static IEnumerable<TechniqueStep> FindRemotePairs(int[] masks)
    {
        var cand = new ChainTechniques.Candidates(masks);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int start = 0; start < SudokuGrid.CellCount; start++)
        {
            if (cand.Count(start) != 2)
            {
                continue;
            }

            int pair = cand.Mask(start);
            int a = SudokuGrid.LowestDigit(pair);
            int b = SudokuGrid.LowestDigit(pair & ~SudokuGrid.DigitBit(a));

            // BFS：记录到达每格时的步数奇偶，只保留最短
            var dist = new int[SudokuGrid.CellCount];
            var parent = new int[SudokuGrid.CellCount];
            var parentDigit = new int[SudokuGrid.CellCount];
            var parentUnit = new int[SudokuGrid.CellCount];
            Array.Fill(dist, -1);
            Array.Fill(parent, -1);

            var queue = new Queue<int>();
            dist[start] = 0;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();

                foreach (int digit in new[] { a, b })
                {
                    foreach (int unit in SudokuGrid.UnitsOf[cell])
                    {
                        List<int> spots = ChainTechniques.CellsWithCandidate(cand, unit, digit)
                            .Where(c => cand.Mask(c) == pair)
                            .ToList();

                        if (spots.Count != 2 || !spots.Contains(cell))
                        {
                            continue;
                        }

                        int other = spots[0] == cell ? spots[1] : spots[0];
                        if (dist[other] >= 0)
                        {
                            continue;
                        }

                        dist[other] = dist[cell] + 1;
                        parent[other] = cell;
                        parentDigit[other] = digit;
                        parentUnit[other] = unit;
                        queue.Enqueue(other);
                    }
                }
            }

            for (int end = 0; end < SudokuGrid.CellCount; end++)
            {
                // 两端必须是同一对双值格，且链长为奇数（两端一 a 一 b）
                if (end == start || dist[end] < 1 || (dist[end] % 2) == 0 || cand.Mask(end) != pair)
                {
                    continue;
                }

                var chain = new List<int>();
                for (int node = end; node >= 0; node = parent[node])
                {
                    chain.Add(node);
                }

                chain.Reverse();

                var eliminations = new List<CandidateRef>();
                foreach (int cell in SudokuGrid.Peers[end])
                {
                    if (chain.Contains(cell) || cell == end || !ChainTechniques.IsPeer(cell, start))
                    {
                        continue;
                    }

                    foreach (int digit in new[] { a, b })
                    {
                        if (cand.Has(cell, digit))
                        {
                            eliminations.Add(new CandidateRef(cell, digit));
                        }
                    }
                }

                if (eliminations.Count == 0 || !seen.Add($"{start}:{end}"))
                {
                    continue;
                }

                yield return BuildStep(chain, parentDigit, parentUnit, a, b, eliminations);
            }
        }
    }

    private static TechniqueStep BuildStep(
        IReadOnlyList<int> chain,
        IReadOnlyList<int> parentDigit,
        IReadOnlyList<int> parentUnit,
        int a,
        int b,
        IReadOnlyList<CandidateRef> eliminations)
    {
        var links = new List<TechniqueLink>();
        var derivation = new List<string>();
        string unitNames = string.Join("→", chain.Select(ChainTechniques.CellName));

        derivation.Add($"链上每一格都只含 {a}/{b}：{unitNames}（共 {chain.Count} 格）。");

        for (int i = 1; i < chain.Count; i++)
        {
            int from = chain[i - 1];
            int to = chain[i];
            int digit = parentDigit[to];
            int unit = parentUnit[to];
            links.Add(new TechniqueLink(
                new CandidateRef(from, digit),
                new CandidateRef(to, digit),
                true,
                StrongReason(unit, digit)));

            derivation.Add(
                $"第 {i} 步：{ChainTechniques.CellName(from)} 与 {ChainTechniques.CellName(to)} 的 {digit} 在{UnitName(unit)}内只剩这两处（强链），" +
                $"所以其中一格是 {digit}、另一格就是另一个数字。");
        }

        int head = chain[0];
        int tail = chain[^1];
        string elimDesc = ChainTechniques.Join(eliminations);

        derivation.Add($"{ChainTechniques.CellName(head)} 与 {ChainTechniques.CellName(tail)} 相隔奇数步，必然一个填 {a}、一个填 {b}。");
        derivation.Add($"因此同时看见这两格的位置都不能是 {a} 或 {b}，删除：{elimDesc}。");

        string description =
            $"远程数对：{ChainTechniques.CellName(head)} 与 {ChainTechniques.CellName(tail)} 由 {chain.Count - 1} 条共轭对串起，" +
            $"两端必然一个是 {a}、一个是 {b}，故删除 {elimDesc}。";

        return new TechniqueStep(
            Technique.RemotePair,
            chain.ToArray(),
            -1,
            0,
            eliminations,
            description,
            links,
            derivation);
    }

    private static string StrongReason(int unit, int digit) =>
        $"在{UnitName(unit)}内的 {digit} 只剩这两处，必有一真（强链）";

    private static string UnitName(int unit)
    {
        if (unit < SudokuGrid.Size)
        {
            return $"第 {unit + 1} 行";
        }

        if (unit < 2 * SudokuGrid.Size)
        {
            return $"第 {unit - SudokuGrid.Size + 1} 列";
        }

        return $"第 {unit - (2 * SudokuGrid.Size) + 1} 宫";
    }
}
