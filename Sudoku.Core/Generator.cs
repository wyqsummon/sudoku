namespace Sudoku.Core;

/// <summary>一道题目：题面、唯一解与难度信息。</summary>
public sealed record Puzzle(Board Given, Board Solution, DifficultyLevel Level, int TechniqueLevel, int ClueCount)
{
    /// <summary>SE 风格评分（生成时计算；从旧存档恢复的题目为 0）。</summary>
    public double Score { get; init; }

    /// <summary>题面的 SDK 文本表示。</summary>
    public string ToSdkString() => Given.ToSdkString();

    /// <summary>空格数量。</summary>
    public int EmptyCount => SudokuGrid.CellCount - ClueCount;

    /// <summary>形如「困难 · 4.2」的难度显示文本。</summary>
    public string DifficultyText => Score > 0 ? $"{Difficulty.Name(Level)} · {Score:0.0}" : Difficulty.Name(Level);
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

        // 十七数是「按提示数个数定义的题型」而不是挖洞深度：随机挖洞挖不到 17 个提示数，
        // 走内置母题 + 等价变换这条路（见 SeventeenClues）。
        if (level == DifficultyLevel.Seventeen)
        {
            return GenerateSeventeen(maxAttempts);
        }

        Puzzle? best = null;
        int bestDelta = int.MaxValue;
        bool bestSolved = false;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Board solution = GenerateSolution();
            // 大师档改用非对称挖洞 + 多种子重挖：更少的线索才更可能逼出高阶技巧（题面美观相对次要）
            bool symmetric = _symmetric && level != DifficultyLevel.Master;
            int restarts = level == DifficultyLevel.Master ? 4 : 1;
            Board given = Dig(solution, MinClues(level), symmetric, restarts);
            AdjustToLevel(given, solution, level);

            // 需要逐步评分，因这里必须收集每一步的技巧信息
            LogicalSolveResult result = LogicalSolver.Solve(given, collectSteps: true);
            RatingReport rating = DifficultyRating.Rate(result);
            DifficultyLevel actual = rating.Level;
            var puzzle = new Puzzle(given, solution, actual, result.MaxLevel, given.FilledCount)
            {
                Score = rating.Score,
            };

            int delta = Math.Abs((int)actual - (int)level);
            bool solved = result.Solved;

            // 档位命中且能纯逻辑解出 → 直接采用
            // （只有纯逻辑可解，分段式提示才有技巧可给；否则提示会「无技巧可用」）
            if (delta == 0 && solved)
            {
                return puzzle;
            }

            if (delta < bestDelta || (delta == bestDelta && solved && !bestSolved))
            {
                bestDelta = delta;
                bestSolved = solved;
                best = puzzle;
            }
        }

        return best ?? throw new InvalidOperationException("题目生成失败。");
    }

    /// <summary>
    /// 生成一道「十七数」题目：恰好 17 个提示数、唯一解，且难度不低于大师档。
    /// 十七数这一档要求「必须用到高阶技巧」（ALS 链 / ALS-XZ / BUG+1 / 唯一矩形 / 带鳍鱼 / 远程数对），
    /// 内置母题已经按这个口径筛过，这里再兜一层：优先返回用到高阶技巧的派生题面。
    /// </summary>
    public Puzzle GenerateSeventeen(int maxAttempts = 40)
    {
        if (!SeventeenClues.IsAvailable)
        {
            throw new InvalidOperationException("没有可用的 17 提示数母题。");
        }

        Puzzle? solved = null;
        Puzzle? fallback = null;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Board given = SeventeenClues.Create(_random);
            if (!Solver.TrySolve(given, out Board solution))
            {
                continue;
            }

            LogicalSolveResult result = LogicalSolver.Solve(given, collectSteps: true);
            RatingReport rating = DifficultyRating.Rate(result);
            var puzzle = new Puzzle(given, solution, DifficultyLevel.Seventeen, result.MaxLevel, given.FilledCount)
            {
                Score = rating.Score,
            };

            fallback ??= puzzle;

            if (!result.Solved)
            {
                continue;
            }

            // 用到高阶技巧 → 评分不低于大师档，这才是十七数该有的难度
            if (result.UsesAdvancedTechnique)
            {
                return puzzle;
            }

            solved ??= puzzle;
        }

        return solved ?? fallback ?? throw new InvalidOperationException("十七数题目生成失败。");
    }

    /// <summary>各难度的目标最少已知数，决定挖洞深度。</summary>
    private static int MinClues(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Easy => 34,
        DifficultyLevel.Medium => 28,
        DifficultyLevel.Hard => 22,
        DifficultyLevel.Expert => 19,
        // 大师档要挖到几乎没有冗余线索，才更可能逼出带鳍鱼 / 唯一矩形 / BUG+1 / 远程数对
        DifficultyLevel.Master => 17,
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

    /// <summary>在保证唯一解的前提下尽可能挖洞（同一批格子反复扫，直到扫不动为止）。</summary>
    private Board Dig(Board solution, int minClues, bool symmetric, int restarts = 1)
    {
        Board best = DigOnce(solution, minClues, symmetric);
        int bestClues = best.FilledCount;

        // 贪心挖洞会落在局部最优：换几次随机顺序重挖，取挖得最深的那次
        for (int r = 1; r < restarts && bestClues > minClues; r++)
        {
            Board candidate = DigOnce(solution, minClues, symmetric);
            int clues = candidate.FilledCount;
            if (clues < bestClues)
            {
                best = candidate;
                bestClues = clues;
            }
        }

        return best;
    }

    private Board DigOnce(Board solution, int minClues, bool symmetric)
    {
        Board puzzle = solution.Clone();
        int clues = puzzle.FilledCount;

        // 单轮贪心挖洞容易早早卡住（剩下的第一次没挖掉的格子往往后面就挖得掉了），
        // 因此反复重排顺序再扫几轮，直到某一轮一点都挖不动为止。
        for (int round = 0; round < 6 && clues > minClues; round++)
        {
            var order = new List<int>();
            int limit = symmetric ? ((SudokuGrid.CellCount + 1) / 2) : SudokuGrid.CellCount;
            for (int i = 0; i < limit; i++)
            {
                order.Add(i);
            }

            Shuffle(order);

            bool removedAny = false;
            foreach (int index in order)
            {
                if (clues <= minClues)
                {
                    break;
                }

                var group = new List<int>(2) { index };
                if (symmetric)
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
                    removedAny = true;
                }
                else
                {
                    for (int k = 0; k < group.Count; k++)
                    {
                        puzzle[group[k]] = backup[k];
                    }
                }
            }

            if (!removedAny)
            {
                break;
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
