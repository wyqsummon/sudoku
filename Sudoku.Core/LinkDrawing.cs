namespace Sudoku.Core;

/// <summary>
/// 用户手绘的一条候选数连线。<see cref="From"/> → <see cref="To"/> 保留玩家的落笔顺序，
/// 界面据此在 <see cref="To"/> 端画箭头（表示推理方向）；
/// 强链 <see cref="IsStrong"/>=true（界面画实线），弱链 =false（界面画虚线）。
/// <see cref="ColorIndex"/> 为玩家选定的颜色编号（0 = 默认配色）。
/// 这是纯粹的标记，不做任何正确性校验——允许画在任意两个候选数之间。
/// </summary>
public readonly record struct UserLink(CandidateRef From, CandidateRef To, bool IsStrong, int ColorIndex) : IComparable<UserLink>
{
    /// <summary>按玩家的操作顺序创建连线：first 是起点，second 是终点（箭头指向 second）。</summary>
    public static UserLink Create(CandidateRef first, CandidateRef second, bool isStrong, int colorIndex = 0) =>
        new(first, second, isStrong, Math.Max(0, colorIndex));

    /// <summary>是否为同一格内两个候选数之间的连线。</summary>
    public bool IsInternal => From.Cell == To.Cell;

    /// <summary>关系中文名。</summary>
    public string KindName => IsStrong ? "强链" : "弱链";

    /// <summary>反向连线（箭头反向），用于把玩家画的链当作双向关系处理时使用。</summary>
    public UserLink Reversed() => this with { From = To, To = From };

    private static int Rank(CandidateRef candidate) => (candidate.Cell * 10) + candidate.Digit;

    public int CompareTo(UserLink other)
    {
        int byFrom = Rank(From).CompareTo(Rank(other.From));
        if (byFrom != 0)
        {
            return byFrom;
        }

        int byTo = Rank(To).CompareTo(Rank(other.To));
        if (byTo != 0)
        {
            return byTo;
        }

        return IsStrong != other.IsStrong ? (IsStrong ? -1 : 1) : ColorIndex.CompareTo(other.ColorIndex);
    }

    /// <summary>形如 R1C2(3) ==▶ R1C8(3) 的表达式（== 强链，-- 弱链，▶ 表示方向）。</summary>
    public override string ToString() => $"{From} {(IsStrong ? "==" : "--")}▶ {To}";
}

/// <summary>
/// 手绘连线集合：去重、随存档序列化、以及与格子的联动清理。
/// 放在 Core 里以便单元测试，界面层只负责绘制与交互。
/// </summary>
public sealed class LinkDrawing
{
    private readonly List<UserLink> _links = [];

    /// <summary>全部连线（按 <see cref="UserLink.CompareTo"/> 排序，顺序稳定）。</summary>
    public IReadOnlyList<UserLink> Links => _links;

    public int Count => _links.Count;

    public bool IsEmpty => _links.Count == 0;

    /// <summary>强链条数。</summary>
    public int StrongCount => _links.Count(l => l.IsStrong);

    /// <summary>弱链条数。</summary>
    public int WeakCount => _links.Count(l => !l.IsStrong);

    /// <summary>坐标是否合法。</summary>
    public static bool IsValidCandidate(CandidateRef candidate) =>
        candidate.Cell >= 0 &&
        candidate.Cell < SudokuGrid.CellCount &&
        candidate.Digit >= 1 &&
        candidate.Digit <= SudokuGrid.Size;

    /// <summary>加一条连线；完全相同的连线已存在或坐标非法时返回 false（反向连线视为另一条，方向有意义）。</summary>
    public bool Add(UserLink link)
    {
        if (!IsValidCandidate(link.From) || !IsValidCandidate(link.To) || link.From == link.To)
        {
            return false;
        }

        if (_links.Contains(link))
        {
            return false;
        }

        _links.Add(link);
        _links.Sort();
        return true;
    }

    /// <summary>删除完全相同的连线。</summary>
    public bool Remove(UserLink link) => _links.Remove(link);

    /// <summary>删除所有与某格相关的连线（该格被填入数字后候选数已消失）。</summary>
    public int RemoveForCell(int cell) => _links.RemoveAll(l => l.From.Cell == cell || l.To.Cell == cell);

    public void Clear() => _links.Clear();

    /// <summary>复制一份，用于撤销时对比。</summary>
    public LinkDrawing Clone()
    {
        var copy = new LinkDrawing();
        copy._links.AddRange(_links);
        return copy;
    }

    /// <summary>按当前顺序返回连线快照。</summary>
    public UserLink[] ToArray() => _links.ToArray();

    /// <summary>序列化为存档文本：cell:digit-cell:digit:S|W:color，条目之间用 ; 分隔。</summary>
    public string Serialize() => string.Join(
        ";",
        _links.Select(l => $"{l.From.Cell}:{l.From.Digit}-{l.To.Cell}:{l.To.Digit}:{(l.IsStrong ? 'S' : 'W')}:{l.ColorIndex}"));

    /// <summary>从存档文本恢复；格式错误的条目直接跳过，不抛异常。</summary>
    public static LinkDrawing Parse(string? text)
    {
        var drawing = new LinkDrawing();
        if (string.IsNullOrWhiteSpace(text))
        {
            return drawing;
        }

        foreach (string part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryParseEntry(part, out UserLink link))
            {
                drawing.Add(link);
            }
        }

        return drawing;
    }

    private static bool TryParseEntry(string entry, out UserLink link)
    {
        link = default;
        string[] halves = entry.Split('-');
        if (halves.Length != 2)
        {
            return false;
        }

        if (!TryParseEndpoint(halves[0], out CandidateRef from))
        {
            return false;
        }

        string[] tail = halves[1].Split(':');
        if (tail.Length is not (3 or 4) ||
            !int.TryParse(tail[0], out int cell) ||
            !int.TryParse(tail[1], out int digit))
        {
            return false;
        }

        var to = new CandidateRef(cell, digit);
        if (!IsValidCandidate(from) || !IsValidCandidate(to) || from == to)
        {
            return false;
        }

        // 类型必须是明确的 S/W，其余一律视为损坏数据
        string kindText = tail[2].Trim().ToUpperInvariant();
        if (kindText is not ("S" or "W"))
        {
            return false;
        }

        // 颜色字段是后加的：旧存档没有这一段，按默认配色处理
        int colorIndex = 0;
        if (tail.Length == 4 && (!int.TryParse(tail[3], out colorIndex) || colorIndex < 0))
        {
            colorIndex = 0;
        }

        link = UserLink.Create(from, to, kindText == "S", colorIndex);
        return true;
    }

    private static bool TryParseEndpoint(string text, out CandidateRef candidate)
    {
        candidate = default;
        string[] parts = text.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int cell) || !int.TryParse(parts[1], out int digit))
        {
            return false;
        }

        candidate = new CandidateRef(cell, digit);
        return true;
    }
}
