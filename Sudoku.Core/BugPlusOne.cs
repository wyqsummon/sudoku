namespace Sudoku.Core;

/// <summary>
/// BUG+1（Bivalue Universal Grave + 1）。当盘面上除了一格以外，所有空格都只剩两个候选数，
/// 并且每个单元里每个未填数字都恰好出现两次时，这个盘面就成了「双值通用坟墓」——
/// 它的解要么不存在、要么成对出现（互换某些候选数即可），因此不是合法题目。
/// 题目有唯一解，说明那唯一的「三候选数格」里多出来的候选数就是答案。
/// </summary>
public static class BugPlusOne
{
    /// <summary>查找 BUG+1。</summary>
    public static IEnumerable<TechniqueStep> FindBugPlusOne(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return FindBugPlusOne(board.ComputeCandidates());
    }

    /// <summary>基于候选数掩码快照查找 BUG+1。</summary>
    internal static IEnumerable<TechniqueStep> FindBugPlusOne(int[] masks)
    {
        var cand = new ChainTechniques.Candidates(masks);
        int triCell = -1;

        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            int count = cand.Count(cell);
            if (count == 0)
            {
                continue;
            }

            if (count == 2)
            {
                continue;
            }

            if (count == 3 && triCell < 0)
            {
                triCell = cell;
                continue;
            }

            return Array.Empty<TechniqueStep>(); // 不满足「只有一个三候选数格，其余全是双值格」
        }

        if (triCell < 0)
        {
            return Array.Empty<TechniqueStep>();
        }

        // 对三候选数格的三个候选数，分别统计它在行/列/宫里的出现次数
        var rowCount = new int[SudokuGrid.Size + 1];
        var colCount = new int[SudokuGrid.Size + 1];
        var boxCount = new int[SudokuGrid.Size + 1];

        foreach (int digit in SudokuGrid.Digits(cand.Mask(triCell)))
        {
            rowCount[digit] = CountInUnit(cand, SudokuGrid.Row(triCell), digit);
            colCount[digit] = CountInUnit(cand, SudokuGrid.Size + SudokuGrid.Col(triCell), digit);
            boxCount[digit] = CountInUnit(cand, (2 * SudokuGrid.Size) + SudokuGrid.Box(triCell), digit);
        }

        // 只有「在行、列、宫内都出现 3 次」的那个候选数才是答案
        var answers = SudokuGrid.Digits(cand.Mask(triCell))
            .Where(d => rowCount[d] == 3 && colCount[d] == 3 && boxCount[d] == 3)
            .ToArray();

        if (answers.Length != 1)
        {
            return Array.Empty<TechniqueStep>();
        }

        // 再核对一次 BUG 条件：除三候选数格的三个候选数外，每个单元里每个数字只能出现 0 或 2 次
        if (!IsBugLike(cand, triCell))
        {
            return Array.Empty<TechniqueStep>();
        }

        int answer = answers[0];
        int others = cand.Mask(triCell) & ~SudokuGrid.DigitBit(answer);

        var eliminations = SudokuGrid.Digits(others)
            .Select(d => new CandidateRef(triCell, d))
            .ToArray();

        string cellName = ChainTechniques.CellName(triCell);
        string otherText = string.Join("、", SudokuGrid.Digits(others));
        string elimDesc = ChainTechniques.Join(eliminations);

        string description =
            $"BUG+1：除 {cellName} 外所有空格都是双值格，{cellName} 的候选数 {answer} 在行、列、宫内都出现 3 次，其余候选数各出现 2 次，" +
            $"故 {cellName} 必填 {answer}（删除 {elimDesc}）。";

        var derivation = new List<string>
        {
            $"盘面上除 {cellName} 外，每个空格都恰好只剩两个候选数。",
            $"每个单元里每个未填数字都恰好出现两次——这就是 BUG 的形状，会导出两个解，题目不合法。",
            $"{cellName} 是唯一的例外，它有 3 个候选数；其中 {answer} 在它所在的行、列、宫内都出现 3 次。",
            $"只有把 {answer} 填进 {cellName}，才能打破 BUG 形状；所以 {cellName} = {answer}，删除：{elimDesc}。",
        };

        return new[]
        {
            new TechniqueStep(
                Technique.BugPlusOne,
                new[] { triCell },
                triCell,
                answer,
                eliminations,
                description,
                Array.Empty<TechniqueLink>(),
                derivation),
        };
    }

    private static bool IsBugLike(ChainTechniques.Candidates cand, int triCell)
    {
        int triMask = cand.Mask(triCell);

        for (int unit = 0; unit < SudokuGrid.UnitCount; unit++)
        {
            bool containsTri = SudokuGrid.AllUnits[unit].Contains(triCell);

            for (int digit = 1; digit <= SudokuGrid.Size; digit++)
            {
                int count = CountInUnit(cand, unit, digit);

                // 该数字在本单元已填出（不可能再作为候选数出现）或恰好出现两次，都符合 BUG 形状
                if (count == 0 || count == 2)
                {
                    continue;
                }

                // 只有三候选数格所在的单元，允许它的三个候选数各多算一次
                if (containsTri && count == 3 && (triMask & SudokuGrid.DigitBit(digit)) != 0)
                {
                    continue;
                }

                return false;
            }
        }

        return true;
    }

    /// <summary>统计某单元的空格里，某数字作为候选数出现了多少次。</summary>
    private static int CountInUnit(ChainTechniques.Candidates cand, int unitId, int digit)
    {
        int count = 0;
        foreach (int cell in SudokuGrid.AllUnits[unitId])
        {
            if (cand.Has(cell, digit))
            {
                count++;
            }
        }

        return count;
    }
}
