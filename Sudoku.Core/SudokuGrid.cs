using System.Numerics;

namespace Sudoku.Core;

/// <summary>
/// 数独盘面的静态拓扑表：行/列/宫单元、关联格、位掩码工具。
/// 全盘 81 格，格索引 = row * 9 + col；数字 1..9 对应位掩码 bit0..bit8。
/// （刻意不叫 Grid，避免与 UI 框架的 Grid 控件重名）
/// </summary>
public static class SudokuGrid
{
    public const int Size = 9;
    public const int CellCount = 81;
    public const int UnitCount = 27;
    public const int BoxSize = 3;

    /// <summary>9 个数字全部可用的掩码（bit0..bit8 = 数字 1..9）。</summary>
    public const int AllDigitsMask = 0x1FF;

    /// <summary>9 个行单元（0..8），每个含 9 个格索引。</summary>
    public static readonly int[][] Rows;

    /// <summary>9 个列单元（0..8）。</summary>
    public static readonly int[][] Cols;

    /// <summary>9 个宫单元（0..8）。</summary>
    public static readonly int[][] Boxes;

    /// <summary>27 个单元统一编号：0-8 行单元，9-17 列单元，18-26 宫单元。</summary>
    public static readonly int[][] AllUnits;

    /// <summary>每格所属的 3 个单元编号（行、列、宫）。</summary>
    public static readonly int[][] UnitsOf;

    /// <summary>每格的 20 个关联格（同行/同列/同宫，不含自身）。</summary>
    public static readonly int[][] Peers;

    /// <summary>每格所属宫编号 0..8。</summary>
    public static readonly int[] BoxOf;

    /// <summary>
    /// 每格的 20 个关联格位掩码（按格索引）。
    /// 注意：必须用 128 位承载——81 格超出 ulong 的 64 位，位移会回绕（1UL &lt;&lt; 73 等于 1UL &lt;&lt; 9）。
    /// </summary>
    public static readonly UInt128[] PeerMasks;

    static SudokuGrid()
    {
        Rows = new int[Size][];
        Cols = new int[Size][];
        Boxes = new int[Size][];

        for (int r = 0; r < Size; r++)
        {
            Rows[r] = new int[Size];
            for (int c = 0; c < Size; c++)
            {
                Rows[r][c] = Index(r, c);
            }
        }

        for (int c = 0; c < Size; c++)
        {
            Cols[c] = new int[Size];
            for (int r = 0; r < Size; r++)
            {
                Cols[c][r] = Index(r, c);
            }
        }

        for (int b = 0; b < Size; b++)
        {
            Boxes[b] = new int[Size];
            int baseRow = (b / BoxSize) * BoxSize;
            int baseCol = (b % BoxSize) * BoxSize;
            int k = 0;
            for (int r = 0; r < BoxSize; r++)
            {
                for (int c = 0; c < BoxSize; c++)
                {
                    Boxes[b][k++] = Index(baseRow + r, baseCol + c);
                }
            }
        }

        AllUnits = new int[UnitCount][];
        for (int i = 0; i < Size; i++)
        {
            AllUnits[i] = Rows[i];
            AllUnits[Size + i] = Cols[i];
            AllUnits[(2 * Size) + i] = Boxes[i];
        }

        BoxOf = new int[CellCount];
        UnitsOf = new int[CellCount][];
        Peers = new int[CellCount][];
        PeerMasks = new UInt128[CellCount];

        for (int i = 0; i < CellCount; i++)
        {
            int r = Row(i);
            int c = Col(i);
            int b = (r / BoxSize) * BoxSize + (c / BoxSize);
            BoxOf[i] = b;
            UnitsOf[i] = new[] { r, Size + c, (2 * Size) + b };

            var peers = new List<int>(20);
            foreach (int p in Rows[r])
            {
                if (p != i)
                {
                    peers.Add(p);
                }
            }

            foreach (int p in Cols[c])
            {
                if (p != i && !peers.Contains(p))
                {
                    peers.Add(p);
                }
            }

            foreach (int p in Boxes[b])
            {
                if (p != i && !peers.Contains(p))
                {
                    peers.Add(p);
                }
            }

            Peers[i] = peers.ToArray();

            UInt128 mask = UInt128.Zero;
            foreach (int p in Peers[i])
            {
                mask |= UInt128.One << p;
            }

            PeerMasks[i] = mask;
        }
    }

    /// <summary>格索引。</summary>
    public static int Index(int row, int col) => (row * Size) + col;

    /// <summary>格所在行 0..8。</summary>
    public static int Row(int index) => index / Size;

    /// <summary>格所在列 0..8。</summary>
    public static int Col(int index) => index % Size;

    /// <summary>格所在宫 0..8。</summary>
    public static int Box(int index) => ((index / Size) / BoxSize * BoxSize) + ((index % Size) / BoxSize);

    /// <summary>数字对应的位掩码。</summary>
    public static int DigitBit(int digit) => 1 << (digit - 1);

    /// <summary>掩码中候选数个数。</summary>
    public static int CountDigits(int mask) => BitOperations.PopCount((uint)mask);

    /// <summary>掩码中最小的数字。</summary>
    public static int LowestDigit(int mask) => BitOperations.TrailingZeroCount((uint)mask) + 1;

    /// <summary>枚举掩码中的所有数字（升序）。</summary>
    public static IEnumerable<int> Digits(int mask)
    {
        for (int d = 1; d <= Size; d++)
        {
            if ((mask & DigitBit(d)) != 0)
            {
                yield return d;
            }
        }
    }

    /// <summary>把数字转成显示字符（0 显示为 '.'）。</summary>
    public static char ToChar(int value) => value == 0 ? '.' : (char)('0' + value);
}
