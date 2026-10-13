using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAJoust.Engine;
using AVAJoust.Models;
using AVAJoust.Views;

namespace AVAJoust.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set JOUST_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "joust-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("JOUST_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAJoust-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    private static void Play(GameEngine g, AttractPilot pilot, int steps, AttractPilot? second = null)
    {
        for (int i = 0; i < steps && g.State == GameState.Playing; i++)
            g.Step(pilot.Decide(g), second?.Decide(g, 1) ?? default);
    }

    [AvaloniaFact]
    public void The_title_and_the_first_wave()
    {
        var (w, c, g) = Open();
        g.Idle(60);
        Shot(w, c, "title");
        g.StartGame(1);
        Play(g, new AttractPilot(2), 60 * 6);
        Assert.NotEmpty(g.Enemies);
        Shot(w, c, "wave1");
        w.Close();
    }

    [AvaloniaFact]
    public void Two_players_eggs_and_the_burning_bridges()
    {
        var (w, c, g) = Open(seed: 5);
        g.StartGame(2);
        var p1 = new AttractPilot(3);
        var p2 = new AttractPilot(4);
        for (int i = 0; i < 60 * 30 && g.Eggs.Count < 2 && g.State == GameState.Playing; i++) g.Step(p1.Decide(g), p2.Decide(g, 1));
        Shot(w, c, "two-players");
        g.JumpToWave(3);
        Play(g, p1, 60, p2);
        Assert.True(g.BridgeFire > 0);
        Shot(w, c, "bridges-burn");
        w.Close();
    }

    [AvaloniaFact]
    public void The_lava_the_troll_and_the_pterodactyl()
    {
        var (w, c, g) = Open(seed: 6);
        g.StartGame(1);
        g.JumpToWave(5);
        Assert.Equal(GameEngine.WaveKind.Pterodactyl, g.CurrentKind);
        var pilot = new AttractPilot(5);
        Play(g, pilot, 60 * 5);
        g.SummonPterodactyl(60, 90, 1);
        Play(g, pilot, 20);
        Assert.NotEmpty(g.Pterodactyls);
        Shot(w, c, "pterodactyl");
        w.Close();
    }

    [AvaloniaFact]
    public void Game_over_initials_and_the_high_scores()
    {
        var (w, c, g) = Open(seed: 4);
        g.StartGame(1);
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 60 * 5 && g.Score[0] < 6000; i++) g.Step(pilot.Decide(g));
        for (int i = 0; i < 60 * 60 * 5 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(20);
        Shot(w, c, "game-over");
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Type('A');
        g.Initials.Type('V');
        Shot(w, c, "initials");
        g.Initials.Type('A');
        g.Idle(5);
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle(20);
        Shot(w, c, "high-scores");
        w.Close();
    }
}
