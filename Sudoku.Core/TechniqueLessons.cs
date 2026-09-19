namespace Sudoku.Core;

/// <summary>
/// 一种技巧的教程内容：原理 / 怎么找 / 怎么做，外加一道真实例题的题面。
/// 例题的「卡点盘面」不写死——<see cref="TechniqueLessons.CreatePractice"/> 会按人类走法
/// 把题面推进到该技巧可用的那一步（并要求用全新候选数也能复现这一招），
/// 这样技巧代码改动后练习内容不会失效。
/// </summary>
public sealed record TechniqueLesson(
    Technique Technique,
    string Idea,
    string HowToFind,
    string Action,
    string ExamplePuzzle)
{
    /// <summary>技巧中文名。</summary>
    public string Name => TechniqueInfo.Name(Technique);

    /// <summary>难度等级（1 最简单）。</summary>
    public int Level => TechniqueInfo.Level(Technique);

    /// <summary>一句话说明。</summary>
    public string Summary => TechniqueInfo.Summary(Technique);

    /// <summary>难度分组：基础 / 进阶 / 高阶。</summary>
    public string Group => Level switch
    {
        <= 2 => "基础技巧",
        <= 6 => "进阶技巧",
        _ => "高阶技巧",
    };

    /// <summary>是否有可用例题题面。</summary>
    public bool HasExample => ExamplePuzzle.Length == SudokuGrid.CellCount;
}

/// <summary>专项练习的一个开局：盘面停在「该技巧可用」的那一步，玩家自己把这一步找出来。</summary>
public sealed record PracticeSetup(
    Technique Technique,
    Puzzle Puzzle,
    TechniqueStep Target,
    string Intro)
{
    /// <summary>技巧中文名。</summary>
    public string Name => TechniqueInfo.Name(Technique);

    /// <summary>练习目标说明。</summary>
    public string Goal => Target.IsPlacement
        ? $"找到 {CellName(Target.PlaceIndex)} 该填的数字"
        : $"用这一步技巧删掉 {Target.Eliminations.Count} 个候选数";

    /// <summary>这一步的推导说明（教程里作为例题讲解）。</summary>
    public string Note => Target.Description;

    /// <summary>卡点盘面上还剩几格没填。</summary>
    public int Remaining => SudokuGrid.CellCount - Puzzle.ClueCount;

    private static string CellName(int cell) =>
        cell < 0 ? "?" : $"R{SudokuGrid.Row(cell) + 1}C{SudokuGrid.Col(cell) + 1}";
}

/// <summary>
/// 技巧教程与专项练习的内容库：每种技巧一条 <see cref="TechniqueLesson"/>，
/// 例题题面都是从真实生成的题目里采集来的。
/// </summary>
public static class TechniqueLessons
{
    /// <summary>推进盘面时最多走多少步（防止死循环）。</summary>
    private const int MaxWalkSteps = 120;

    private static readonly Dictionary<Technique, TechniqueLesson> Map = Build();

    /// <summary>全部教程（按技巧等级从易到难）。</summary>
    public static IReadOnlyList<TechniqueLesson> All { get; } = Map.Values
        .OrderBy(l => l.Level)
        .ThenBy(l => (int)l.Technique)
        .ToArray();

    /// <summary>取某个技巧的教程。</summary>
    public static TechniqueLesson Get(Technique technique) =>
        Map.TryGetValue(technique, out TechniqueLesson? lesson)
            ? lesson
            : throw new ArgumentOutOfRangeException(nameof(technique), technique, "该技巧还没有教程内容。");

    /// <summary>按分组列出教程。</summary>
    public static IReadOnlyList<IGrouping<string, TechniqueLesson>> Grouped() =>
        All.GroupBy(l => l.Group).ToArray();

    /// <summary>
    /// 生成一次专项练习：把例题题面按人类走法推进到该技巧可用的那一步，玩家从那里开始。
    /// 找不到（没有例题 / 走不到 / 卡死了）时返回 null。
    /// </summary>
    public static PracticeSetup? CreatePractice(Technique technique)
    {
        TechniqueLesson lesson = Get(technique);
        if (!lesson.HasExample)
        {
            return null;
        }

        Board given = Board.Parse(lesson.ExamplePuzzle);
        if (!Solver.TrySolve(given, out Board solution))
        {
            return null;
        }

        int targetLevel = TechniqueInfo.Level(technique);
        int[] cells = given.ToArray();
        int[] masks = given.ComputeCandidates();

        for (int stepIndex = 0; stepIndex < MaxWalkSteps; stepIndex++)
        {
            Board state = Board.Wrap((int[])cells.Clone());

            // 关键：用「全新候选数」检查这一招是否可用——练习盘面上不会有任何预先删好的候选数，
            // 否则玩家打开练习会看不到这一步成立的理由。
            IReadOnlyList<TechniqueStep> steps = LogicalSolver.FindAllSteps(state, targetLevel);

            TechniqueStep? hit = steps.FirstOrDefault(s => s.Technique == technique);
            if (hit is not null)
            {
                return new PracticeSetup(
                    technique,
                    BuildPuzzle(state, solution, technique),
                    hit,
                    $"{TechniqueInfo.Name(technique)} 专项练习：盘面已经推进到「只剩这一招」的位置，请自己找出来。");
            }

            TechniqueStep? progress = steps
                .OrderBy(s => TechniqueInfo.Level(s.Technique))
                .FirstOrDefault();

            if (progress is null)
            {
                return null; // 卡死了，这一招在这道题里用不上
            }

            if (progress.IsPlacement)
            {
                Place(cells, masks, progress.PlaceIndex, progress.PlaceDigit);
            }
            else
            {
                foreach (CandidateRef elimination in progress.Eliminations)
                {
                    masks[elimination.Cell] &= ~SudokuGrid.DigitBit(elimination.Digit);
                }
            }

            if (cells.All(v => v != 0))
            {
                return null; // 一路填完了也没轮到这一招
            }
        }

        return null;
    }

    /// <summary>练习局不写档、不影响「继续上一局」；等级按技巧等级折算，方便提示面板按等级取用。</summary>
    private static Puzzle BuildPuzzle(Board state, Board solution, Technique technique) => new(
        state,
        solution,
        LevelToDifficulty(technique),
        TechniqueInfo.Level(technique),
        state.FilledCount);

    private static void Place(int[] cells, int[] masks, int cell, int digit)
    {
        if (cell < 0)
        {
            return;
        }

        cells[cell] = digit;
        masks[cell] = 0;
        int bit = SudokuGrid.DigitBit(digit);

        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            if (ChainTechniques.IsPeer(cell, i))
            {
                masks[i] &= ~bit;
            }
        }
    }

    private static DifficultyLevel LevelToDifficulty(Technique technique) => TechniqueInfo.Level(technique) switch
    {
        <= 2 => DifficultyLevel.Easy,
        <= 4 => DifficultyLevel.Medium,
        <= 6 => DifficultyLevel.Hard,
        <= 9 => DifficultyLevel.Expert,
        _ => DifficultyLevel.Master,
    };

    private static Dictionary<Technique, TechniqueLesson> Build()
    {
        var lessons = new List<TechniqueLesson>
        {
            new(Technique.NakedSingle,
                "一个格子里只剩一个候选数时，这个数字就是它的答案——因为它不可能填别的。",
                "看某个空格的行、列、宫，把已经出现的数字全部划掉，只剩一个数字的格子。",
                "直接把这个数字填进该格。",
                "......6454.158..9.....2...75..93.47..9.1.4.2..48.62..38...4.....2..135.4354......"),

            new(Technique.HiddenSingle,
                "某个数字在一个行（列、宫）里只有一格能放，即使那一格还有很多候选数，这个数字也只能放那儿。",
                "盯住一个数字和一个单元：把该单元里已经出现这个数字的格子排除，剩下的若只有一格，就是它。",
                "把该数字填入这一格（该格的其他候选数同时作废）。",
                "...2.1..4.24.3.....8....6..5..49.73...9...5...36.57..9..1....4.....2.98.6..9.5..."),

            new(Technique.LockedCandidatesPointing,
                "一个宫内某个数字的所有可能位置都挤在同一行（或列）上，那这一行（列）在这个宫之外的部分就不可能再放这个数字。",
                "逐宫看每个数字：它的候选格是否全部落在同一行或同一列。",
                "把该数字从这一行（列）上、宫外的格子候选数里删掉。",
                "9.3....2...13....8.8.2.5.1......72..21.....56..48......4.7.1.3.1....29...3....1.4"),

            new(Technique.LockedCandidatesClaiming,
                "反过来：一行（列）上某数字的所有可能位置都落在同一个宫里，这个宫的其他格子就不能再放它。",
                "逐行、逐列看每个数字：它的候选格是否全部落在同一个宫内。",
                "把该数字从该宫内、这一行（列）之外的格子候选数里删掉。",
                "96..81...58.........29.3.6..2.4....68...2...44....9.2..5.3.76.........49...19..72"),

            new(Technique.NakedPair,
                "同一单元里两格的候选数恰好都是同样的两个数字，这两格必定一格一个，同单元其他格就再也不能是这两个数字。",
                "扫行、列、宫：找候选数只有两个、而且两格完全相同的组合。",
                "删掉该单元其余格里的这两个数字。",
                "7.6..934.....8.......74..96..2...961.........865...4..15..97.......5.....342..5.7"),

            new(Technique.HiddenPair,
                "同一单元里两个数字只可能出现在同样的两格中，这两格就被这两个数字占满，格里的其他候选数全是多余的。",
                "扫行、列、宫：找两个数字，它们的候选格集合完全相同且只有两格。",
                "删掉这两格里除这两个数字以外的候选数。",
                "8..2...35.52.7..8....5..1.....71....4..3.6..1....94.....1..3....6..8.41.27...5..3"),

            new(Technique.NakedTriple,
                "一个单元里三格的候选数合起来只有三个数字（每格两个或三个），这三格就被这三个数字占满，同单元其他格不能再是这三个数字。",
                "扫行、列、宫：挑三个格，把它们候选数的并集数一数，恰好三个就成立。",
                "删掉该单元其余格里的这三个数字。",
                "7...3..4..3.....71.8.75..6.4....93...9.....2...23....6.1..63.9.34.....1..2..7...8"),

            new(Technique.HiddenTriple,
                "同一单元里三个数字只可能出现在同样的三格中，这三格就只保留这三个数字，其余候选数全删。",
                "扫行、列、宫：找三个数字，它们候选格的并集恰好就是三格。",
                "删掉这三格里除这三个数字以外的候选数。",
                "7.2..8.6............6.4..59..59..6.2...782...2.9..18..31..2.7............2.8..9.5"),

            new(Technique.XWing,
                "某个数字在两行里都只出现在相同的两列，这四格构成一个矩形：两行各占一列，于是这两列上其他格都不会是这个数字（反过来看两列也一样）。",
                "选定一个数字，逐行看它只出现在哪两列；若两行的列号完全相同，就是 X 翼。",
                "从这两列的其他格子里删掉该数字。",
                "1.8......4..8..95..7.4.......498.5...9.2.6.8...2.546.......8.7..25..3..8......2.4"),

            new(Technique.Swordfish,
                "X 翼的三行版：某数字在三行里都只出现在相同的三列，这三列的其他格就不能再是它。",
                "选定一个数字，找出它的候选格只落在三列之内的三行，再检查这三列的候选格是否也都只在这三行。",
                "从这三列（或三行）的其他格子里删掉该数字。",
                "...2.1...1....7.48.8....6.32....9.3....5.3....7.8....94.7....8.35.7....6...4.2..."),

            new(Technique.XYWing,
                "三个双值格组成 Y 形：枢轴格 {a,b}，两翼分别 {a,c} 和 {b,c}。枢轴填 a 时第一翼必是 c，填 b 时第二翼必是 c——总之两翼里必有一个 c。",
                "找一个双值格当枢轴，再看它同时能「看见」的两个双值格，三者候选数拼成 ab、ac、bc 的形状。",
                "删掉同时能看见两翼的格子里那个公共数字 c。",
                "...56....2.1....4..6...4..189.1.....1.37.96.5.....8.144..8...7..8....1.9....92..."),

            new(Technique.XChain,
                "同一个数字的强链（该单元里只剩这两处）与弱链（同单元不能同时成立）交替连成一条链，链的两端必有一个成立，因此同时看见两端的格子不能再放这个数字。",
                "挑一个数字，从它的共轭对出发，交替走强链、弱链，直到走到另一端。",
                "删掉链两端共同可见格里的该数字。",
                "..1....5.23.......4.....7...8.9..67....4....5.....2.1..7.2..16..2.74.83.....36..."),

            new(Technique.XYChain,
                "一串双值格首尾相连，相邻两格共享一个候选数。链上每个格子一真一假地交替，链首链尾的两个候选数必有一个成立。",
                "从某个双值格出发，顺着「共享一个数字」的关系一路连下去，最后回到同一个数字上。",
                "删掉链首、链尾共同可见格里的那个数字。",
                "2..46.8......21.5.4.......2531....9...2...3...9....1253.......9.4.69......6.85..3"),

            new(Technique.Aic,
                "最通用的链：候选数之间强弱交替推断，链两端必有一个成立，所以同时看见两端的候选数可以删。",
                "从任意候选数出发，强链与弱链交替延伸——强链可以是「同格两候选」也可以是「同单元同数字只剩两处」。",
                "删掉链两端共同可见的那个候选数。",
                "...4.75...5........8.63...7..79..1...4.....5...6..24..4...53.2........6...92.8..."),

            new(Technique.RemotePair,
                "一串双值格，每相邻两格之间用某个数字的共轭对连接。链上格子交错取真/假，于是链首与链尾必然一个是 a、一个是 b。",
                "从双值格出发，看它能否通过「某数字在该单元只剩两处」跳到另一个双值格。",
                "删掉同时看见链首链尾的格子里的 a 和 b。",
                ".....46.8.6.79......53.8...9.7...5.16.......24.1...3.7...6.52......31.5.5.34....."),

            new(Technique.FinnedXWing,
                "X 翼的形状里多出几个「鳍格」——它们和翼格同宫。鳍格成立时，同宫里看见鳍格的位置作废；鳍格不成立时，退化成普通 X 翼。两种情况都指向同一批删除。",
                "先按 X 翼找那两行两列的矩形，再看行（列）里是否多出一两格，多出的格子必须与某个翼格同宫。",
                "删掉「既在同宫看见鳍格、又在覆盖单元上」的格子里的该数字。",
                "64.5.....1...3.........2.9......7.1..856..3....29....4....4.95..5.....423..2....."),

            new(Technique.UniqueRectangle,
                "数独要求唯一解。若四格在两行、两列、两宫里只可能填同样的两个数字，就会出现「互换也成立」的两个解。为保住唯一性，必须打断这个矩形。",
                "找两行两列构成的矩形，四格分属两宫，其中三格都只剩同样两个候选数。",
                "删掉第四格里那两个数字（类型 1 的做法），或从两格里删掉那个共同可见的多余数字（类型 2）。",
                "....3..14.53.4..679..8..3..4.8.13...............49.7.6..6..4..889..2.14.24..8...."),

            new(Technique.FinnedSwordfish,
                "剑鱼的三行三列形状里多出鳍格，鳍格与某个覆盖列上的格子同宫。鳍格成立时同宫看见它的位置作废，不成立时退化为剑鱼——两条路都指向同一批删除。",
                "先按剑鱼找出三行三列，再放宽：允许每行多出一两格，多出的格子必须与另一行的覆盖列同宫。",
                "删掉覆盖列上、能看见鳍格的格子里的该数字。",
                string.Empty),

            new(Technique.BugPlusOne,
                "如果盘面上除了一格之外全是双值格，而且每个数字在每行、每列、每宫里都恰好出现两次，这个盘面就处在「双解坟墓」边缘：那一格里多出来的候选数就是唯一答案。",
                "数一数：是不是只剩一格有三个候选数，其余空格都只有两个。",
                "这一格里那个在它的行、列、宫中出现了三次的数字就是答案，直接填入。",
                string.Empty),

            new(Technique.AlsXz,
                "ALS 是「n 格只含 n+1 个候选数」的格组——再多一个数字就填满了。两个 ALS 若共享一个「受限公共候选数」（它在两个 ALS 里都只能落在同一单元、因而只能成立一个），那么它们的另一个公共候选数 z 至少落在其中一个 ALS 里。",
                "找出两处候选数只比格数多一个的格组，看它们是否有公共候选数，且其中一个公共候选数在两组的可见范围内受限。",
                "删掉同时能看见两个 ALS 中所有 z 位置的格子里的 z。",
                "...4....3.248...9.5...9.1.......2..4......5.1.13.....98....7...13...9......6..8.."),

            new(Technique.SueDeCoq,
                "一条线与一个宫的交叉格（3 格）是两侧共用的「核心」。在宫这一侧补几格、在这条线这一侧补几格，只要**两侧各自**都是「n 格只含 n 个候选数」的锁定集，这些候选数就被分别关在宫侧那组和线侧那组里——两侧还互相牵制（各有只在自侧出现的数字），所以比单侧锁定集管得更多。",
                "先盯住某个宫与某条行/列的交叉 3 格，往宫这边补格直到「格数 = 候选数个数」，再往行/列这边补格做同样的事；两侧都要出现「只在自己这侧」的候选数才算数。",
                "宫侧那组的候选数，在宫里其他格子全部删掉；线侧那组的候选数，在这条行/列上其他格子全部删掉。",
                "...2..8.4.3..1..9..863....7...9...7.......3.....4.1...7....2.......4..2831..9...."),

            new(Technique.AlsChain,
                "把多个 ALS 用受限公共候选数首尾串成链，相邻两组之间靠一个「只能落在这一格」的数字连接。链首链尾共享的候选数必落在其一。",
                "从任意 ALS 出发，找能与它通过受限公共候选数相连的下一个 ALS，一路串下去。",
                "删掉同时看见链首、链尾全部该候选数位置的格子里的这个数字。",
                ".49..........9...12.6.71......9......62....74....3..2...5.2..6......5.8..74....1."),

            new(Technique.ContinuousLoop,
                "强弱链交替成环，而且环上每个候选数正好一强一弱时，环就「连续」了：环上的候选数按强/弱交替被判定为真或假。于是弱链两端必有一真、强链两端必有一真一假，可以据此删除。",
                "从某个候选数出发，强链、弱链交替推进，最后绕回起点形成一个环。",
                "删掉：① 同单元同数字的弱链所在单元里其余格的该数字；② 同格两候选构成弱链时该格的其他候选数。",
                "1.8.....49....45.33...9.7..8.......22..8.5.1....6.....42..........1..93.....6...."),
        };

        return lessons.ToDictionary(l => l.Technique);
    }
}
