using System.Text.Json;
using System.Text.Json.Serialization;
using Sudoku.Core;

namespace Sudoku.App.Game;

/// <summary>对局存档数据。</summary>
public sealed record GameSnapshot
{
    public required string PuzzleSdk { get; init; }

    public required string SolutionSdk { get; init; }

    public required int LevelValue { get; init; }

    public required int TechniqueLevel { get; init; }

    /// <summary>SE 风格难度评分（阶段二新增；旧存档为 0）。</summary>
    public double DifficultyScore { get; init; }

    /// <summary>格子涂色（阶段三新增）：cell:color;…。</summary>
    public string CellColors { get; init; } = string.Empty;

    /// <summary>自动标记开关状态。</summary>
    public bool AutoMark { get; init; }

    /// <summary>锁定的数字（0 = 未锁定）。</summary>
    public int LockedDigit { get; init; }

    /// <summary>数字锁定模式是否开启。</summary>
    public bool LockMode { get; init; }

    /// <summary>绘制颜色编号。</summary>
    public int DrawColorIndex { get; init; } = 1;

    public required int[] Values { get; init; }

    public required int[] Notes { get; init; }

    public int MistakeCount { get; init; }

    public int ElapsedSeconds { get; init; }

    public int HintsUsed { get; init; }

    public int HintCell { get; init; } = -1;

    /// <summary>用户手绘的强弱链标记（<see cref="LinkDrawing.Serialize"/> 的紧凑文本）。</summary>
    public string Links { get; init; } = string.Empty;

    public bool IsCompleted { get; init; }

    public bool IsFailed { get; init; }

    public bool IsPaused { get; init; }

    public string SavedAtUtc { get; init; } = string.Empty;

    /// <summary>存档对应的难度。</summary>
    [JsonIgnore]
    public DifficultyLevel Level => (DifficultyLevel)LevelValue;
}

/// <summary>对局存档读写（JSON 文件，位于应用数据目录）。</summary>
public static class GameStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private static string FilePath => Path.Combine(FileSystem.AppDataDirectory, "current-game.json");

    /// <summary>是否存在可继续的对局。</summary>
    public static bool HasSavedGame()
    {
        try
        {
            return File.Exists(FilePath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>保存对局。</summary>
    public static void Save(GameSnapshot snapshot)
    {
        try
        {
            string json = JsonSerializer.Serialize(snapshot, Options);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存对局失败：{ex.Message}");
        }
    }

    /// <summary>读取对局，失败返回 null。</summary>
    public static GameSnapshot? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<GameSnapshot>(json, Options);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"读取对局失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>删除存档。</summary>
    public static void Delete()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"删除对局失败：{ex.Message}");
        }
    }
}
