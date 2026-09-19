using Sudoku.Core;
using Xunit;
using Xunit.Abstractions;

namespace Sudoku.Core.Tests;

/// <summary>诊断：逐个技巧检查教程例题与专项练习开局是否成立，打印失败原因。</summary>
public class LessonDiagnostics
{
    private readonly ITestOutputHelper _output;

    public LessonDiagnostics(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Check_All()
    {
        foreach (TechniqueLesson lesson in TechniqueLessons.All)
        {
            if (!lesson.HasExample)
            {
                _output.WriteLine($"NOEX {lesson.Name}（没有采到例题）题面长度={lesson.ExamplePuzzle.Length}");
                continue;
            }

            PracticeSetup? setup = TechniqueLessons.CreatePractice(lesson.Technique);
            if (setup is not null)
            {
                _output.WriteLine(
                    $"OK   {lesson.Name}｜卡点剩余 {setup.Remaining} 格｜{setup.Goal}｜{setup.Note}");
                continue;
            }

            Board given = Board.Parse(lesson.ExamplePuzzle);
            bool solved = Solver.TrySolve(given, out _);
            _output.WriteLine($"FAIL {lesson.Name}：题面解析={given.FilledCount} 格 可解={solved}");
        }
    }
}
