using Sudoku.Core;
using Xunit;

namespace Sudoku.Core.Tests;

/// <summary>画链弧线的几何：控制点在中点垂直方向上偏移，端点、切线方向都要对得上。</summary>
public class LinkCurveTests
{
    private const float Cell = 60f;

    /// <summary>浮点比较留一点误差，别揪着最后几位。</summary>
    private static void Close(float expected, float actual, float tolerance = 0.01f) =>
        Assert.True(MathF.Abs(expected - actual) <= tolerance, $"期望 {expected}，实际 {actual}");

    [Fact]
    public void ShouldCurve_ShouldRejectShortLinks()
    {
        // 同一格内两个候选数（约 1/3 格宽）不值得画弧
        Assert.False(LinkCurve.ShouldCurve(Cell / 3f, Cell));

        // 跨格的链才画弧
        Assert.True(LinkCurve.ShouldCurve(Cell, Cell));
        Assert.True(LinkCurve.ShouldCurve(Cell * 3f, Cell));

        // 没有格宽信息时一律不画，避免除零 / 乱画
        Assert.False(LinkCurve.ShouldCurve(100f, 0f));

        // 正好卡在阈值上不算（要「长于」阈值）
        Assert.False(LinkCurve.ShouldCurve(Cell * LinkCurve.MinLengthRatio, Cell));
    }

    [Fact]
    public void BowFor_ShouldScaleWithLengthAndRespectCap()
    {
        Close(100f * LinkCurve.BowRatio, LinkCurve.BowFor(100f, Cell));

        // 很长的链不会无限弯下去：控制点偏移封顶在 MaxBowRatio × 格宽 × 2
        float cap = Cell * LinkCurve.MaxBowRatio * 2f;
        Close(cap, LinkCurve.BowFor(Cell * 30f, Cell));

        // 短链不受上限影响
        Assert.True(LinkCurve.BowFor(Cell * 0.5f, Cell) < cap);
    }

    [Fact]
    public void ControlPoint_ShouldSitOnThePerpendicularBisector()
    {
        // 水平链：控制点 x 恰好是中点，y 偏移 bow
        (float cx, float cy) = LinkCurve.ControlPoint(0f, 0f, 100f, 0f, 20f);
        Close(50f, cx);
        Close(20f, MathF.Abs(cy));

        // 垂直链：控制点 y 是中点，x 偏移 bow
        (float cx2, float cy2) = LinkCurve.ControlPoint(0f, 0f, 0f, 100f, 20f);
        Close(50f, cy2);
        Close(20f, MathF.Abs(cx2));

        // bow = 0 就是中点本身（直线）
        (float cx3, float cy3) = LinkCurve.ControlPoint(10f, 20f, 50f, 20f, 0f);
        Close(30f, cx3);
        Close(20f, cy3);
    }

    [Fact]
    public void ControlPoint_ShouldMirrorWhenTheLinkIsReversed()
    {
        // 反向画同一条链时弧线落到另一侧：两条方向相反的链不会叠在一起
        (float cx1, float cy1) = LinkCurve.ControlPoint(0f, 0f, 100f, 0f, 20f);
        (float cx2, float cy2) = LinkCurve.ControlPoint(100f, 0f, 0f, 0f, 20f);

        Close(cx1, cx2);
        Close(cy1, -cy2);
    }

    [Fact]
    public void PointAt_ShouldHitBothEnds()
    {
        (float cx, float cy) = LinkCurve.ControlPoint(0f, 0f, 100f, 0f, 20f);

        (float x0, float y0) = LinkCurve.PointAt(0f, 0f, cx, cy, 100f, 0f, 0f);
        Close(0f, x0);
        Close(0f, y0);

        (float x1, float y1) = LinkCurve.PointAt(0f, 0f, cx, cy, 100f, 0f, 1f);
        Close(100f, x1);
        Close(0f, y1);

        // t = 0.5 的偏移量是控制点偏移的一半（二次贝塞尔的性质）
        (float xm, float ym) = LinkCurve.PointAt(0f, 0f, cx, cy, 100f, 0f, 0.5f);
        Close(50f, xm);
        Close(cy / 2f, ym);
    }

    [Fact]
    public void PointAt_ShouldBowAwayFromTheStraightLine()
    {
        // 真实场景：同一行相邻两格的候选数，直线正好压在两格之间的字上
        const float Ax = 0f, Ay = 0f, Bx = Cell, By = 0f;
        float bow = LinkCurve.BowFor(Cell, Cell);
        (float cx, float cy) = LinkCurve.ControlPoint(Ax, Ay, Bx, By, bow);
        (float xm, float ym) = LinkCurve.PointAt(Ax, Ay, cx, cy, Bx, By, 0.5f);

        // 中点（直线经过的地方）已经被让开至少 1/10 格，但也没夸张到弯进别的行
        Assert.True(MathF.Abs(ym) > Cell * 0.1f, $"中点只让开了 {MathF.Abs(ym):0.0} px");
        Assert.True(MathF.Abs(ym) < Cell * 0.2f, "让开太多会弯到别的行上去");
        Close(Cell / 2f, xm);
    }

    [Fact]
    public void SubCurve_ShouldShortenTheLineBeforeTheArrow()
    {
        (float cx, float cy) = LinkCurve.ControlPoint(0f, 0f, 100f, 0f, 20f);

        // t = 1 时退化成原曲线
        (float c1x, float c1y, float e1x, float e1y) = LinkCurve.SubCurve(0f, 0f, cx, cy, 100f, 0f, 1f);
        Close(cx, c1x);
        Close(cy, c1y);
        Close(100f, e1x);
        Close(0f, e1y);

        // t = 0.9 时：控制点落在 a→c 的 90% 处，终点提前落在曲线上
        (float c2x, float c2y, float e2x, float e2y) = LinkCurve.SubCurve(0f, 0f, cx, cy, 100f, 0f, 0.9f);
        Close(cx * 0.9f, c2x);
        Close(cy * 0.9f, c2y);
        (float px, float py) = LinkCurve.PointAt(0f, 0f, cx, cy, 100f, 0f, 0.9f);
        Close(px, e2x);
        Close(py, e2y);
        Assert.True(e2x < 100f, "线头应该收在箭头之前");
    }

    [Fact]
    public void EndDirection_ShouldFollowTheCurveTangent()
    {
        // 直线（控制点 = 中点）时终点切线就是 a→b 方向
        (float ux, float uy) = LinkCurve.EndDirection(50f, 0f, 100f, 0f, 1f, 0f);
        Close(1f, ux);
        Close(0f, uy);

        // 控制点偏在下方时，终点切线朝右上，且仍是单位向量
        (float vx, float vy) = LinkCurve.EndDirection(50f, 30f, 100f, 0f, 1f, 0f);
        Assert.True(vx > 0f);
        Assert.True(vy < 0f);
        Close(1f, MathF.Sqrt((vx * vx) + (vy * vy)));

        // 退化（控制点与终点重合）时用兜底方向
        (float fx, float fy) = LinkCurve.EndDirection(10f, 10f, 10f, 10f, 0.5f, -0.5f);
        Close(0.5f, fx);
        Close(-0.5f, fy);
    }
}
