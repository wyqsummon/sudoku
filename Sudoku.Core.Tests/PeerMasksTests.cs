using Xunit;

namespace Sudoku.Core.Tests;

/// <summary>
/// 关联格位掩码的一致性测试。
/// 该测试存在的意义：81 格超出 ulong 的 64 位，若用 ulong 承载，索引 ≥ 64 的格会被静默写错
/// （位移回绕），XY 翼等依赖「是否同时看见两格」的技巧会因此给出错误结论。
/// </summary>
public class PeerMasksTests
{
    [Fact]
    public void PeerMasks_ShouldMatchPeersArrayForAllCells()
    {
        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            Assert.Equal(20, SudokuGrid.Peers[i].Length);

            for (int j = 0; j < SudokuGrid.CellCount; j++)
            {
                bool expected = SudokuGrid.Peers[i].Contains(j);
                bool actual = (SudokuGrid.PeerMasks[i] & (UInt128.One << j)) != UInt128.Zero;

                Assert.True(
                    expected == actual,
                    $"格 {i}(R{SudokuGrid.Row(i) + 1}C{SudokuGrid.Col(i) + 1}) 与格 {j}(R{SudokuGrid.Row(j) + 1}C{SudokuGrid.Col(j) + 1}) 的关联关系不一致：Peers={expected}，PeerMasks={actual}");
            }
        }
    }

    [Fact]
    public void PeerMasks_ShouldHandleHighIndexCells()
    {
        // 索引 ≥ 64 的格专门覆盖 ulong 回绕问题
        int high = SudokuGrid.Index(8, 8); // 80
        Assert.Equal(80, high);
        Assert.False(SudokuGrid.Peers[0].Contains(high));
        Assert.Equal(UInt128.Zero, SudokuGrid.PeerMasks[0] & (UInt128.One << high));

        int peer = SudokuGrid.Index(8, 0); // 72：与 R9C1 同列
        Assert.True(SudokuGrid.Peers[0].Contains(peer));
        Assert.NotEqual(UInt128.Zero, SudokuGrid.PeerMasks[0] & (UInt128.One << peer));

        // 计数校验：每格恰好 20 个关联格
        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            int bits = 0;
            for (int j = 0; j < SudokuGrid.CellCount; j++)
            {
                if ((SudokuGrid.PeerMasks[i] & (UInt128.One << j)) != UInt128.Zero)
                {
                    bits++;
                }
            }

            Assert.Equal(20, bits);
        }
    }
}
