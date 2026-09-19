namespace Sudoku.Core;

/// <summary>
/// 格子涂色：玩家为特定格子刷上的底色标记（用于标记"已确认/待观察/存疑"等）。
/// 颜色用编号表示（0 = 未涂色，1..<see cref="MaxColor"/> 由界面映射到具体颜色），
/// 放在 Core 里以便单元测试与随存档序列化。
/// </summary>
public sealed class CellColoring
{
    /// <summary>最大颜色编号（含）。</summary>
    public const int MaxColor = 6;

    private readonly int[] _colors = new int[SudokuGrid.CellCount];

    /// <summary>取某格的颜色编号（0 = 未涂色）。</summary>
    public int this[int cell] => IsValidCell(cell) ? _colors[cell] : 0;

    /// <summary>已涂色的格子数。</summary>
    public int Count => _colors.Count(c => c != 0);

    public bool IsEmpty => Count == 0;

    /// <summary>坐标是否合法。</summary>
    public static bool IsValidCell(int cell) => cell >= 0 && cell < SudokuGrid.CellCount;

    /// <summary>颜色编号是否合法（0 表示擦除）。</summary>
    public static bool IsValidColor(int colorIndex) => colorIndex >= 0 && colorIndex <= MaxColor;

    /// <summary>涂色；<paramref name="colorIndex"/> 为 0 表示擦除该格。返回是否发生了变化。</summary>
    public bool Set(int cell, int colorIndex)
    {
        if (!IsValidCell(cell) || !IsValidColor(colorIndex))
        {
            return false;
        }

        if (_colors[cell] == colorIndex)
        {
            return false;
        }

        _colors[cell] = colorIndex;
        return true;
    }

    /// <summary>擦除某格颜色。</summary>
    public bool Clear(int cell) => Set(cell, 0);

    /// <summary>擦除全部涂色。</summary>
    public void ClearAll() => Array.Clear(_colors);

    /// <summary>复制一份（撤销需要）。</summary>
    public CellColoring Clone()
    {
        var copy = new CellColoring();
        Array.Copy(_colors, copy._colors, SudokuGrid.CellCount);
        return copy;
    }

    /// <summary>底层数组副本。</summary>
    public int[] ToArray() => (int[])_colors.Clone();

    /// <summary>序列化为存档文本：cell:color，条目之间用 ; 分隔。</summary>
    public string Serialize() => string.Join(
        ";",
        Enumerable.Range(0, SudokuGrid.CellCount)
            .Where(c => _colors[c] != 0)
            .Select(c => $"{c}:{_colors[c]}"));

    /// <summary>从存档文本恢复；格式错误的条目直接跳过。</summary>
    public static CellColoring Parse(string? text)
    {
        var coloring = new CellColoring();
        if (string.IsNullOrWhiteSpace(text))
        {
            return coloring;
        }

        foreach (string part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] halves = part.Split(':');
            if (halves.Length != 2 ||
                !int.TryParse(halves[0], out int cell) ||
                !int.TryParse(halves[1], out int color))
            {
                continue;
            }

            coloring.Set(cell, color);
        }

        return coloring;
    }
}
