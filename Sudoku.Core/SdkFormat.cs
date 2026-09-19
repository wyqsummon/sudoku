using System.Text;

namespace Sudoku.Core;

/// <summary>SDK 文本导入的结果。</summary>
/// <param name="Success">是否通过全部校验。</param>
/// <param name="Board">解析出的题面（失败时为空盘）。</param>
/// <param name="Solution">校验到解时给出的答案（无解或未校验时为 null）。</param>
/// <param name="ClueCount">已知数个数。</param>
/// <param name="Message">中文提示（成功与失败都给一句明确的说明）。</param>
public sealed record SdkImportResult(
    bool Success,
    Board Board,
    Board? Solution,
    int ClueCount,
    string Message)
{
    /// <summary>是否给出了答案。</summary>
    public bool HasSolution => Solution is not null;

    /// <summary>空格数量。</summary>
    public int EmptyCount => SudokuGrid.CellCount - ClueCount;
}

/// <summary>
/// SDK（Sudoku Format）文本的导入导出：一行 81 个字符，'1'-'9' 为已知数，
/// '.' / '0' / '_' / '*' 为空格。
/// 兼容 Hodoku 的 .sdk 文本习惯：多行题库（每行一道题）、'#' 注释行、
/// 以及带空格 / '|' / '---+---' 分隔的可读排版。
/// </summary>
public static class SdkFormat
{
    /// <summary>导出时空格使用的字符。</summary>
    public const char EmptyChar = '.';

    /// <summary>导出为单行 SDK 文本。</summary>
    public static string Format(Board board, char emptyChar = EmptyChar)
    {
        ArgumentNullException.ThrowIfNull(board);

        var sb = new StringBuilder(SudokuGrid.CellCount);
        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            int value = board[cell];
            sb.Append(value == 0 ? emptyChar : (char)('0' + value));
        }

        return sb.ToString();
    }

    /// <summary>导出为带宫线的可读排版（9 行）。</summary>
    public static string FormatPretty(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return board.ToString();
    }

    /// <summary>导出多道题（题库文本，每行一道）。</summary>
    public static string FormatLibrary(IEnumerable<Board> boards)
    {
        ArgumentNullException.ThrowIfNull(boards);
        return string.Join(Environment.NewLine, boards.Select(board => Format(board)));
    }

    /// <summary>各种换行符：'\\n'、'\\r'（WinUI 文本框用的是它）、'\\r\\n'、Unicode 行分隔符。</summary>
    private static readonly char[] LineSeparators = { '\r', '\n', '\u2028', '\u2029' };

    /// <summary>从一段文本里切出所有题目行（跳过空行与 '#' 注释行）。</summary>
    public static IReadOnlyList<string> SplitPuzzles(string? text)
    {
        var puzzles = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return puzzles;
        }

        foreach (string rawLine in text.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (CountCells(line) == SudokuGrid.CellCount)
            {
                puzzles.Add(line);
            }
        }

        return puzzles;
    }

    /// <summary>
    /// 解析并校验一段 SDK 文本（多行文本取第一道题）。
    /// 校验顺序：字符集 → 格值数量 → 盘面合法性（重复数字）→ 解的存在性 → 唯一解。
    /// </summary>
    /// <param name="text">待解析文本。</param>
    /// <param name="requireUniqueSolution">是否要求唯一解（默认要求，便于当作题目来玩）。</param>
    public static SdkImportResult Import(string? text, bool requireUniqueSolution = true)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Fail("请先粘贴 SDK 文本：81 个字符，数字 1-9 为已知数，. 或 0 表示空格。");
        }

        string source = StripComments(SplitPuzzles(text).FirstOrDefault() ?? text);

        var values = new List<int>(SudokuGrid.CellCount);
        var unknown = new List<char>();

        foreach (char ch in source)
        {
            if (char.IsWhiteSpace(ch) || ch is '-' or '|' or '+')
            {
                continue; // 排版分隔符
            }

            if (ch is >= '1' and <= '9')
            {
                values.Add(ch - '0');
                continue;
            }

            if (ch is '0' or '.' or '_' or '*')
            {
                values.Add(0);
                continue;
            }

            unknown.Add(ch);
        }

        if (unknown.Count > 0)
        {
            string shown = string.Join(' ', unknown.Distinct().Take(5));
            return Fail($"发现无法识别的字符：{shown}（只允许 1-9、.、0、_、* 与排版分隔符）");
        }

        if (values.Count != SudokuGrid.CellCount)
        {
            return Fail($"需要 {SudokuGrid.CellCount} 个格值，实际解析到 {values.Count} 个。");
        }

        Board board = Board.Wrap(values.ToArray());

        if (!board.IsValid())
        {
            return Fail($"盘面不合法：{DescribeConflict(board)}");
        }

        int clues = board.FilledCount;
        if (clues == SudokuGrid.CellCount)
        {
            return Fail("这是一份完整解（81 个已知数），不是待解题面。");
        }

        if (clues == 0)
        {
            return Fail("盘面完全是空的，没有任何已知数。");
        }

        int solutions = Solver.CountSolutions(board, 2);

        if (solutions == 0)
        {
            return Fail("该盘面无解，请检查是否有填错的数字。");
        }

        if (solutions > 1 && requireUniqueSolution)
        {
            return Fail("该盘面有多个解，无法作为题目。");
        }

        Board? solution = Solver.TrySolve(board, out Board solved) ? solved : null;

        string message = solution is not null
            ? $"导入成功：{clues} 个已知数、{SudokuGrid.CellCount - clues} 个空格，唯一解。"
            : $"导入成功：{clues} 个已知数。";

        return new SdkImportResult(true, board, solution, clues, message);
    }

    /// <summary>逐行去掉 '#' 之后的注释内容（保留换行，便于按行解析）。</summary>
    private static string StripComments(string text)
    {
        if (!text.Contains('#'))
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        foreach (string line in text.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            int cut = line.IndexOf('#');
            sb.Append(cut >= 0 ? line[..cut] : line);
            sb.Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>统计文本里可识别的格值数量（忽略排版字符）。</summary>
    private static int CountCells(string line)
    {
        int count = 0;
        foreach (char ch in line)
        {
            if (ch is >= '1' and <= '9' || ch is '0' or '.' or '_' or '*')
            {
                count++;
            }
            else if (ch == '#')
            {
                break; // 行内注释
            }
        }

        return count;
    }

    /// <summary>找出第一处重复数字，返回带坐标的中文说明。</summary>
    private static string DescribeConflict(Board board)
    {
        for (int unit = 0; unit < SudokuGrid.AllUnits.Length; unit++)
        {
            var seen = new Dictionary<int, int>(SudokuGrid.Size);

            foreach (int cell in SudokuGrid.AllUnits[unit])
            {
                int value = board[cell];
                if (value == 0)
                {
                    continue;
                }

                if (seen.TryGetValue(value, out int other))
                {
                    return $"{CellName(other)} 与 {CellName(cell)} 都填了 {value}（{UnitName(unit)}内重复）";
                }

                seen[value] = cell;
            }
        }

        return "同行 / 同列 / 同宫内存在重复数字";
    }

    private static string CellName(int cell) => $"R{SudokuGrid.Row(cell) + 1}C{SudokuGrid.Col(cell) + 1}";

    private static string UnitName(int unit) => unit switch
    {
        < SudokuGrid.Size => $"第 {unit + 1} 行",
        < 2 * SudokuGrid.Size => $"第 {unit - SudokuGrid.Size + 1} 列",
        _ => $"第 {unit - (2 * SudokuGrid.Size) + 1} 宫",
    };

    private static SdkImportResult Fail(string message) =>
        new(false, Board.Empty(), null, 0, message);
}
