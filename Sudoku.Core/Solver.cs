using System.Numerics;

namespace Sudoku.Core;

/// <summary>
/// 回溯求解器：位掩码 + 最少候选优先（MRV）。
/// 用于唯一解校验、出题时的挖洞判定，以及游戏内的答案求解。
/// </summary>
public static class Solver
{
    /// <summary>求解第一个解；无解返回 false。</summary>
    public static bool TrySolve(Board board, out Board solution)
    {
        ArgumentNullException.ThrowIfNull(board);
        solution = Board.Empty();

        if (!board.IsValid())
        {
            return false;
        }

        var cells = board.ToArray();
        var state = new State(cells);
        var result = new int[SudokuGrid.CellCount];
        int found = 0;
        Search(state, 1, result, ref found);

        if (found == 0)
        {
            return false;
        }

        solution = Board.Wrap(result);
        return true;
    }

    /// <summary>统计解的个数，最多统计到 <paramref name="limit"/> 个即提前结束。</summary>
    public static int CountSolutions(Board board, int limit = 2)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit 必须大于 0。");
        }

        if (!board.IsValid())
        {
            return 0;
        }

        var state = new State(board.ToArray());
        int found = 0;
        Search(state, limit, null, ref found);
        return found;
    }

    /// <summary>是否恰好一个解。</summary>
    public static bool HasUniqueSolution(Board board) => CountSolutions(board, 2) == 1;

    private static void Search(State state, int limit, int[]? solution, ref int found)
    {
        int bestIndex = -1;
        int bestMask = 0;
        int bestCount = 10;

        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            if (state.Cells[i] != 0)
            {
                continue;
            }

            int mask = state.Candidates(i);
            if (mask == 0)
            {
                return; // 该格无候选数，矛盾
            }

            int count = BitOperations.PopCount((uint)mask);
            if (count < bestCount)
            {
                bestCount = count;
                bestIndex = i;
                bestMask = mask;
                if (count == 1)
                {
                    break; // 已是最优分支
                }
            }
        }

        if (bestIndex < 0)
        {
            found++;
            if (solution is not null && found == 1)
            {
                Array.Copy(state.Cells, solution, SudokuGrid.CellCount);
            }

            return;
        }

        while (bestMask != 0)
        {
            int bit = bestMask & -bestMask;
            bestMask ^= bit;

            state.Place(bestIndex, bit);
            Search(state, limit, solution, ref found);
            state.Unplace(bestIndex, bit);

            if (found >= limit)
            {
                return;
            }
        }
    }

    /// <summary>求解过程中的可变状态（格值 + 行/列/宫已用数字掩码）。</summary>
    private sealed class State
    {
        public readonly int[] Cells;
        public readonly int[] RowMask = new int[SudokuGrid.Size];
        public readonly int[] ColMask = new int[SudokuGrid.Size];
        public readonly int[] BoxMask = new int[SudokuGrid.Size];

        public State(int[] cells)
        {
            Cells = cells;
            for (int i = 0; i < SudokuGrid.CellCount; i++)
            {
                int v = cells[i];
                if (v == 0)
                {
                    continue;
                }

                int bit = SudokuGrid.DigitBit(v);
                RowMask[SudokuGrid.Row(i)] |= bit;
                ColMask[SudokuGrid.Col(i)] |= bit;
                BoxMask[SudokuGrid.Box(i)] |= bit;
            }
        }

        public int Candidates(int index)
        {
            int used = RowMask[SudokuGrid.Row(index)] | ColMask[SudokuGrid.Col(index)] | BoxMask[SudokuGrid.Box(index)];
            return SudokuGrid.AllDigitsMask & ~used;
        }

        public void Place(int index, int bit)
        {
            Cells[index] = BitOperations.TrailingZeroCount((uint)bit) + 1;
            RowMask[SudokuGrid.Row(index)] |= bit;
            ColMask[SudokuGrid.Col(index)] |= bit;
            BoxMask[SudokuGrid.Box(index)] |= bit;
        }

        public void Unplace(int index, int bit)
        {
            Cells[index] = 0;
            RowMask[SudokuGrid.Row(index)] &= ~bit;
            ColMask[SudokuGrid.Col(index)] &= ~bit;
            BoxMask[SudokuGrid.Box(index)] &= ~bit;
        }
    }
}
