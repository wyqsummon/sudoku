namespace Sudoku.Core;

/// <summary>一道题目：题面、唯一解与难度信息。</summary>
public sealed record Puzzle(Board Given, Board Solution, DifficultyLevel Level, int TechniqueLevel, int ClueCount)
{
    /// <summary>题面的 SDK 文本表示。</summary>
    public string ToSdkString() => Given.ToSdkString();

    /// <summary>空格数量。</summary>
    public int EmptyCount => SudokuGrid.CellCount - ClueCount;
}

/// <summary>
/// 出题器：先生成完整终盘，再在保证唯一解的前提下挖洞，最后按技巧难度校准档位。
/// </summary>
public sealed class Generator
{
    private readonly Random _random;
    private readonly bool _symmetric;

    /// <param name="seed">随机种子（便于复现同一道题）。</param>
    /// <param name="symmetric">是否采用中心对称挖洞（题面更美观，类似 Hodoku 的题面风格）。</param>
    public Generator(int? seed = null, bool symmetric = true)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _symmetric = symmetric;
    }

    /// <summary>生成一个完整合法的终盘。</summary>
    public Board GenerateSolution()
    {
        var cells = new int[SudokuGrid.CellCount];
        var rows = new int[SudokuGrid.Size];
        var cols = new int[SudokuGrid.Size];
        var boxes = new int[SudokuGrid.Size];

        if (!Fill(cells, rows, cols, boxes))
        {
            throw new InvalidOperationException("终盘生成失败。");
        }

        return Board.Wrap(cells);
    }

    /// <summary>生成指定难度的题目（保证唯一解）。</summary>
    /// <param name="level">目标难度。</param>
    /// <param name="maxAttempts">最大重试次数；若始终无法精确命中则返回最接近的一次。</param>
    public Puzzle Generate(DifficultyLevel level, int maxAttempts = 40)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "maxAttempts 必须大于 0。");
        }

        Puzzle? best = null;
        int bestDelta = int.MaxValue;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Board solution = GenerateSolution();
            Board given = Dig(solution, MinClues(level));
            AdjustToLevel(given, solution, level);

            LogicalSolveResult result = LogicalSolver.Solve(given);
            DifficultyLevel actual = Difficulty.FromSolve(result);
            var puzzle = new Puzzle(given, solution, actual, result.MaxLevel, given.FilledCount);

            int delta = Math.Abs((int)actual - (int)level);
            if (delta == 0)
            {
                return puzzle;
            }

            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = puzzle;
            }
        }

        return best ?? throw new InvalidOperationException("题目生成失败。");
    }

    /// <summary>各难度的目标最少已知数，决定挖洞深度。</summary>
    private static int MinClues(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Easy => 34,
        DifficultyLevel.Medium => 28,
        DifficultyLevel.Hard => 22,
        _ => 30,
    };

    /// <summary>若题面比目标难度更难，则按对称规则回填线索，直到难度降到目标档位。</summary>
    private void AdjustToLevel(Board given, Board solution, DifficultyLevel target)
    {
        LogicalSolveResult result = LogicalSolver.Solve(given);
        if ((int)Difficulty.FromSolve(result) <= (int)target)
        {
            return;
        }

        var removed = new List<int>();
        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            if (given[i] == 0)
            {
                removed.Add(i);
            }
        }

        Shuffle(removed);

        foreach (int cell in removed)
        {
            if (given[cell] != 0)
            {
                continue; // 已被对称回填
            }

            given[cell] = solution[cell];

            if (_symmetric)
            {
                int mirror = SudokuGrid.CellCount - 1 - cell;
                if (mirror != cell && given[mirror] == 0)
                {
                    given[mirror] = solution[mirror];
                }
            }

            result = LogicalSolver.Solve(given);
            if ((int)Difficulty.FromSolve(result) <= (int)target)
            {
                return;
            }
        }
    }

    /// <summary>在保证唯一解的前提下尽可能挖洞。</summary>
    private Board Dig(Board solution, int minClues)
    {
        Board puzzle = solution.Clone();

        var order = new List<int>();
        int limit = _symmetric ? ((SudokuGrid.CellCount + 1) / 2) : SudokuGrid.CellCount;
        for (int i = 0; i < limit; i++)
        {
            order.Add(i);
        }

        Shuffle(order);

        int clues = puzzle.FilledCount;
        foreach (int index in order)
        {
            if (clues <= minClues)
            {
                break;
            }

            var group = new List<int>(2) { index };
            if (_symmetric)
            {
                int mirror = SudokuGrid.CellCount - 1 - index;
                if (mirror != index)
                {
                    group.Add(mirror);
                }
            }

            if (group.Any(c => puzzle[c] == 0))
            {
                continue;
            }

            var backup = group.Select(c => puzzle[c]).ToArray();
            foreach (int c in group)
            {
                puzzle[c] = 0;
            }

            if (Solver.HasUniqueSolution(puzzle))
            {
                clues -= group.Count;
            }
            else
            {
                for (int k = 0; k < group.Count; k++)
                {
                    puzzle[group[k]] = backup[k];
                }
            }
        }

        return puzzle;
    }

    private bool Fill(int[] cells, int[] rows, int[] cols, int[] boxes)
    {
        int best = -1;
        int bestMask = 0;
        int bestCount = 10;

        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            if (cells[i] != 0)
            {
                continue;
            }

            int used = rows[SudokuGrid.Row(i)] | cols[SudokuGrid.Col(i)] | boxes[SudokuGrid.Box(i)];
            int mask = SudokuGrid.AllDigitsMask & ~used;
            if (mask == 0)
            {
                return false;
            }

            int count = SudokuGrid.CountDigits(mask);
            if (count < bestCount)
            {
                bestCount = count;
                best = i;
                bestMask = mask;
                if (count == 1)
                {
                    break;
                }
            }
        }

        if (best < 0)
        {
            return true;
        }

        var digits = SudokuGrid.Digits(bestMask).ToList();
        Shuffle(digits);

        foreach (int digit in digits)
        {
            int bit = SudokuGrid.DigitBit(digit);
            cells[best] = digit;
            rows[SudokuGrid.Row(best)] |= bit;
            cols[SudokuGrid.Col(best)] |= bit;
            boxes[SudokuGrid.Box(best)] |= bit;

            if (Fill(cells, rows, cols, boxes))
            {
                return true;
            }

            cells[best] = 0;
            rows[SudokuGrid.Row(best)] &= ~bit;
            cols[SudokuGrid.Col(best)] &= ~bit;
            boxes[SudokuGrid.Box(best)] &= ~bit;
        }

        return false;
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
