namespace Sudoku.Core;

/// <summary>
/// 手绘连线的几何：把两个候选数之间那条线改成一条带弧度的二次贝塞尔，让它绕开直线路径上的候选数
/// （直线横跨好几格时会从一格格的数字上压过去，看不清链上到底连着哪个候选数）。
///
/// 这里只有纯浮点计算，方便单元测试；界面层负责把结果交给绘图 API。
/// </summary>
public static class LinkCurve
{
    /// <summary>控制点偏移量占链长的比例。二次贝塞尔的实际弯曲幅度是这个值的一半（约链长的 11%）。</summary>
    public const float BowRatio = 0.22f;

    /// <summary>弯曲幅度的上限（占格宽的比例）——长链不要弯到别的行列上去。</summary>
    public const float MaxBowRatio = 0.8f;

    /// <summary>短于「格宽 × 此比例」的连线不画弧：同一格内两个候选数离得太近，弧线反而更乱。</summary>
    public const float MinLengthRatio = 0.75f;

    /// <summary>链长是否够长、值得画弧线。</summary>
    public static bool ShouldCurve(float length, float cellSize) =>
        cellSize > 0 && length > cellSize * MinLengthRatio;

    /// <summary>按链长算控制点该偏移多少（含上限）。</summary>
    public static float BowFor(float length, float cellSize)
    {
        float bow = length * BowRatio;
        float max = MathF.Max(0f, cellSize) * MaxBowRatio * 2f;
        return max > 0f && bow > max ? max : bow;
    }

    /// <summary>
    /// 二次贝塞尔的控制点：中点朝行进方向的垂直方向偏移 <paramref name="bow"/>。
    /// bow = 0 时就是中点，此时曲线退化成直线。
    /// </summary>
    public static (float X, float Y) ControlPoint(float ax, float ay, float bx, float by, float bow)
    {
        float dx = bx - ax;
        float dy = by - ay;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length < 1e-4f)
        {
            return (ax, ay);
        }

        float px = -dy / length;
        float py = dx / length;
        return (((ax + bx) / 2f) + (px * bow), ((ay + by) / 2f) + (py * bow));
    }

    /// <summary>曲线在参数 t∈[0,1] 处的点（t=0 落在起点，t=1 落在终点）。</summary>
    public static (float X, float Y) PointAt(float ax, float ay, float cx, float cy, float bx, float by, float t)
    {
        float u = 1f - t;
        return (
            (u * u * ax) + (2f * u * t * cx) + (t * t * bx),
            (u * u * ay) + (2f * u * t * cy) + (t * t * by));
    }

    /// <summary>
    /// 把 [0, t] 这一段曲线重新表示成一条二次贝塞尔：
    /// 起点不变，控制点落在 a→c 的 t 处，终点是原曲线在 t 处的点。
    /// 用来在箭头前面把线头收住，免得线穿出箭头。
    /// </summary>
    public static (float Cx, float Cy, float EndX, float EndY) SubCurve(
        float ax, float ay, float cx, float cy, float bx, float by, float t)
    {
        (float ex, float ey) = PointAt(ax, ay, cx, cy, bx, by, t);
        return (ax + ((cx - ax) * t), ay + ((cy - ay) * t), ex, ey);
    }

    /// <summary>
    /// 终点处的切线方向（单位向量）：二次贝塞尔在 t=1 的切线方向就是「控制点 → 终点」，箭头按它贴上去。
    /// 控制点与终点重合（退化）时用兜底方向。
    /// </summary>
    public static (float Ux, float Uy) EndDirection(
        float cx, float cy, float bx, float by, float fallbackUx, float fallbackUy)
    {
        float dx = bx - cx;
        float dy = by - cy;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length < 1e-4f)
        {
            return (fallbackUx, fallbackUy);
        }

        return (dx / length, dy / length);
    }
}
