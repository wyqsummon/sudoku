using Xunit;

namespace Sudoku.Core.Tests;

public class SudokuGridTests
{
    [Fact]
    public void Peers_ShouldBe20DistinctCellsWithoutSelf()
    {
        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            int[] peers = SudokuGrid.Peers[i];
            Assert.Equal(20, peers.Length);
            Assert.DoesNotContain(i, peers);
            Assert.Equal(20, peers.Distinct().Count());
        }
    }

    [Fact]
    public void Units_ShouldCoverEachCellExactlyThreeTimes()
    {
        Assert.Equal(SudokuGrid.UnitCount, SudokuGrid.AllUnits.Length);

        var covered = new int[SudokuGrid.CellCount];
        foreach (int[] unit in SudokuGrid.AllUnits)
        {
            Assert.Equal(SudokuGrid.Size, unit.Length);
            Assert.Equal(SudokuGrid.Size, unit.Distinct().Count());
            foreach (int cell in unit)
            {
                covered[cell]++;
            }
        }

        Assert.All(covered, count => Assert.Equal(3, count));
    }

    [Fact]
    public void BoxOf_ShouldMatchBoxUnits()
    {
        for (int box = 0; box < SudokuGrid.Size; box++)
        {
            foreach (int cell in SudokuGrid.Boxes[box])
            {
                Assert.Equal(box, SudokuGrid.BoxOf[cell]);
                Assert.Equal(box, SudokuGrid.Box(cell));
            }
        }
    }

    [Fact]
    public void UnitsOf_ShouldPointToTheThreeOwningUnits()
    {
        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            int[] units = SudokuGrid.UnitsOf[cell];
            Assert.Equal(3, units.Length);
            Assert.Contains(cell, SudokuGrid.AllUnits[units[0]]);
            Assert.Contains(cell, SudokuGrid.AllUnits[units[1]]);
            Assert.Contains(cell, SudokuGrid.AllUnits[units[2]]);
        }
    }

    [Fact]
    public void DigitMaskHelpers_ShouldBehave()
    {
        Assert.Equal(1, SudokuGrid.DigitBit(1));
        Assert.Equal(256, SudokuGrid.DigitBit(9));
        Assert.Equal(9, SudokuGrid.CountDigits(SudokuGrid.AllDigitsMask));
        Assert.Equal(1, SudokuGrid.LowestDigit(SudokuGrid.DigitBit(1) | SudokuGrid.DigitBit(7)));
        Assert.Equal(new[] { 1, 7 }, SudokuGrid.Digits(SudokuGrid.DigitBit(1) | SudokuGrid.DigitBit(7)).ToArray());
    }
}
