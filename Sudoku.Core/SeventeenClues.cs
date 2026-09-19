namespace Sudoku.Core;

/// <summary>
/// 十七数题库（阶段九）。
///
/// 数独提示数的理论下限是 17：少于 17 个提示数不可能有唯一解。这样的题目极少见，
/// 靠随机挖洞几乎挖不出来（普通贪心挖洞一般停在 22~26 个提示数）。
/// 公开的 17 提示数全目录（49157 道）里，绝大多数其实是「唯一候选数就能推完」的简单题，
/// 只有约 2900 道必须用到高阶技巧，其中约 1900 道能被本项目的逻辑求解器完整解出。
/// 这里内置的就是从后者里挑出来的母题：每一道都满足
/// 「恰好 17 个提示数 + 唯一解 + 纯逻辑可解 + 解题路径上必须用到高阶技巧」，
/// 因此十七数这一档的难度系数不会低于大师档（实测 6.4 ~ 9.2 分）。
///
/// 由于同一道母题的等价变换（数字重命名 / 行带列带互换 / 转置）保持难度与提示数个数不变，
/// 少量母题即可派生出数以亿计的不同题面，而且题面看起来完全无关。
/// </summary>
public static class SeventeenClues
{
    /// <summary>
    /// 内置的 17 提示数母题，按「最难技巧」分组。
    /// 括号里是实测的难度评分与最难技巧（评分口径见 <see cref="DifficultyRating"/>）。
    /// </summary>
    private static readonly string[] BasePuzzles =
    {
        // ALS 链（ALS-XY-Wing 等，级 13）· 9.2 分
        ".........4.....2.6.8..7....2...3........18.7...6.......3.....1....2..9...1.5.....",
        "........9..718.....6.....5424.............8.....7..3....1...........5..2.....4.6.",
        "......7..4.......6..9..2......67........4......1...82..15....9......3.5..7.......",
        "......7.9....8.......123........54............182.....5......2.67...4...........1",

        // ALS-XZ（准锁定集，级 11）· 8.8 ~ 9.0 分
        ".....6...45......2...13..............7.89..........65..92...........436......5...",
        "1.....7.9......2....6..3...2...7.........4.5........6.....9.1...345.......5......",
        "1...5...........36....7..4..7....8........9...34..1......8........6.4...9.....5..",
        "........9....8.1..7.8.3....2.91.............6......43........7..4.9....5.3.......",

        // BUG+1（双值通用坟墓，级 10）· 8.6 分
        ".............8.1.379............7.5..81...4.....3.....63..1...........9...5.....7",
        "......7....6........92...5.....743.........6......5...37.1......4..........6...21",
        "......7...5.1.9...8.............4.95.....8...7.6.........6......4......1...72...8",
        "....5...94........78...........6.8.7.45.2............3.....8.4...2......9....3...",

        // 唯一矩形（级 8）· 6.7 分
        "......78.4....9.......31......5.4.7.31.......9..........78.........1...3......6..",
        "...4....9..6.8....7......5...4.....8...2.71.......5.....8......5....3.2.....9....",
        "..3.5....4.....1.....1....6............2..91..65..3................45...91....8..",
        ".2..........1.9...6.....4....9....713...45..........2...5..46............1.7.....",

        // 带鳍 X 翼（级 8）· 6.6 分
        "........9..718....6......452.....3.......5...........4...7........23.6...49......",
        ".......8...71..........24..........1...9....68....5.....2.......61.7.........835.",
        "......7...5.1....6..9.3....23...7............7.......1..16.4...8.............53..",
        ".....6.8.4.7.........2.....2...7.9.4.....8...........3....4.....3.9......85....6.",

        // 远程数对（级 7）· 6.4 分
        "....5..8.4....913............5.6......7....95.....3.4.3..............6.7.9.......",
        "..3.5....4...........1...6...7...3.5...6.4.......2.........3...6.......191.....7.",
        "1.......94...........2..5......9......8...2..9...6...1....17..6..5......8.2......",
        "1...5...94....9..2.8.............6........8..9....2.............6.8......7.6..4.5",
    };

    /// <summary>
    /// 保底母题：万一上面这批母题一条都没通过校验（比如被误改），
    /// 退回这两道已知的经典 17 数题，至少保证这一档还能出题。
    /// </summary>
    private static readonly string[] FallbackPuzzles =
    {
        "000000010400000000020000000000050407008000300001090000300400200050100000000806000",
        "000000000000003085001020000000507000004000100090000000500000073002010000000040009",
    };

    private static readonly Lazy<Board[]> Verified = new(Verify);

    /// <summary>可用的母题（已校验）。</summary>
    public static IReadOnlyList<Board> Bases => Verified.Value;

    /// <summary>是否内置了可用的 17 提示数母题。</summary>
    public static bool IsAvailable => Verified.Value.Length > 0;

    /// <summary>提示数个数（理论下限）。</summary>
    public const int ClueCount = 17;

    /// <summary>
    /// 十七数母题用到的最高技巧等级。十七数这一档会逼出 ALS 链 / BUG+1 这类高阶技巧，
    /// 所以提示与专项练习要按这个等级放宽技法上限，否则玩家点提示会得到「没有可用技巧」。
    /// </summary>
    public const int MaxTechniqueLevel = 13;

    /// <summary>随机派生一道 17 提示数题目（题面，不带答案）。</summary>
    public static Board Create(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        Board[] bases = Verified.Value;
        if (bases.Length == 0)
        {
            throw new InvalidOperationException("没有可用的 17 提示数母题。");
        }

        Board basis = bases[random.Next(bases.Length)];
        return Transform(basis, random);
    }

    /// <summary>
    /// 这道题面是否「必须用到高阶技巧」：纯逻辑能解完，且解题路径上出现了
    /// <see cref="Difficulty.IsAdvancedTechnique"/> 里的技巧。等价变换不改变这个性质。
    /// </summary>
    public static bool RequiresAdvancedTechnique(Board puzzle)
    {
        ArgumentNullException.ThrowIfNull(puzzle);

        LogicalSolveResult result = LogicalSolver.Solve(puzzle);
        return result.Solved && result.UsesAdvancedTechnique;
    }

    /// <summary>对一道 17 提示数题目做随机等价变换（保持唯一解与提示数个数）。</summary>
    public static Board Transform(Board puzzle, Random random)
    {
        ArgumentNullException.ThrowIfNull(puzzle);
        ArgumentNullException.ThrowIfNull(random);

        int[] rows = RowPermutation(random);
        int[] cols = RowPermutation(random);
        bool transpose = random.Next(2) == 0;

        // 数字重命名
        var digits = new int[SudokuGrid.Size + 1];
        var pool = new List<int>();
        for (int d = 1; d <= SudokuGrid.Size; d++)
        {
            pool.Add(d);
        }

        for (int d = 1; d <= SudokuGrid.Size; d++)
        {
            int pick = random.Next(pool.Count);
            digits[d] = pool[pick];
            pool.RemoveAt(pick);
        }

        var cells = new int[SudokuGrid.CellCount];

        for (int r = 0; r < SudokuGrid.Size; r++)
        {
            for (int c = 0; c < SudokuGrid.Size; c++)
            {
                int sourceRow = transpose ? cols[c] : rows[r];
                int sourceCol = transpose ? rows[r] : cols[c];
                int value = puzzle[SudokuGrid.Index(sourceRow, sourceCol)];
                cells[SudokuGrid.Index(r, c)] = value == 0 ? 0 : digits[value];
            }
        }

        return Board.Wrap(cells);
    }

    /// <summary>行/列的等价置换：先换三个带（stack），再在带内互换。</summary>
    private static int[] RowPermutation(Random random)
    {
        var bands = new List<int> { 0, 1, 2 };
        Shuffle(bands, random);

        var result = new int[SudokuGrid.Size];
        int index = 0;
        foreach (int band in bands)
        {
            var inner = new List<int> { 0, 1, 2 };
            Shuffle(inner, random);
            foreach (int offset in inner)
            {
                result[index++] = (band * SudokuGrid.BoxSize) + offset;
            }
        }

        return result;
    }

    private static void Shuffle<T>(IList<T> list, Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// 校验母题：只做「恰好 17 提示数 + 唯一解」这两项（毫秒级）。
    /// 「必须用到高阶技巧 + 评分不低于大师」这一项由单元测试离线把关
    /// （见 <c>SeventeenCluesTests</c>，那里会逐条跑 <see cref="RequiresAdvancedTechnique"/>），
    /// 免得每次进这一档都要在运行时跑一遍 ALS 链级别的求解。
    /// </summary>
    private static Board[] Verify()
    {
        var result = new List<Board>();

        foreach (Board board in ParseAll(BasePuzzles))
        {
            result.Add(board);
        }

        if (result.Count > 0)
        {
            return result.ToArray();
        }

        foreach (Board board in ParseAll(FallbackPuzzles))
        {
            result.Add(board);
        }

        return result.ToArray();
    }

    private static IEnumerable<Board> ParseAll(IEnumerable<string> texts)
    {
        foreach (string text in texts)
        {
            Board board;
            try
            {
                board = Board.Parse(text);
            }
            catch (Exception)
            {
                continue;
            }

            if (board.FilledCount != ClueCount || !Solver.HasUniqueSolution(board))
            {
                continue;
            }

            yield return board;
        }
    }
}
