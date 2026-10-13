using AVAJoust.Engine;
using AVAJoust.Models;

namespace AVAJoust.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "joust-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private GameEngine NewGame(int players = 1, int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame(players);
        return g;
    }

    /// <summary>Steps until player 1 is in play (materialised).</summary>
    private static Rider Ready(GameEngine g, int who = 0)
    {
        for (int i = 0; i < 300 && g.Player[who] is not { Materializing: <= 0 }; i++) g.Step(default);
        return g.Player[who]!;
    }

    [Fact]
    public void A_player_appears_on_a_pad_and_stands_on_the_ground()
    {
        var g = NewGame();
        var p = Ready(g);
        Assert.Equal(Arena.GroundY, p.Pos.Y);
        Assert.True(p.OnGround);
        Assert.Equal(GameEngine.StartingLives - 1, g.Lives[0]);
    }

    [Fact]
    public void Flapping_lifts_and_gravity_brings_you_down_to_land()
    {
        var g = NewGame();
        var p = Ready(g);
        double y = p.Pos.Y;
        g.Step(new Controls(false, false, true));
        for (int i = 0; i < 10; i++) g.Step(default);
        Assert.False(p.OnGround);
        Assert.True(p.Pos.Y < y);
        for (int i = 0; i < 200 && !p.OnGround; i++) g.Step(default);
        Assert.True(p.OnGround);
    }

    [Fact]
    public void The_screen_wraps_round()
    {
        var g = NewGame();
        var p = Ready(g);
        p.Pos = new Vec(Arena.Width - 1, 100);
        p.Standing = null;
        p.Vel = new Vec(100, 0);
        for (int i = 0; i < 10; i++) g.Step(new Controls(false, true, false));
        Assert.InRange(p.Pos.X, 0, 40);
        Assert.Equal(-20, Arena.DeltaX(Arena.Width - 10, 10) * -1, 3);
    }

    [Fact]
    public void Ledges_at_the_edges_reach_round_the_wrap()
    {
        var left = Arena.Floating[3];
        Assert.True(left.Spans(5));
        Assert.True(left.Spans(Arena.Width - 5)); // its part past the left edge
        Assert.False(left.Spans(150));
    }

    private static Rider Enemy(GameEngine g, RiderKind kind, Vec at)
    {
        var e = new Rider { Kind = kind, Pos = at, Vel = new Vec(0, 0) };
        g.Enemies.Add(e);
        return e;
    }

    [Fact]
    public void The_higher_lance_wins_and_leaves_an_egg()
    {
        var g = NewGame();
        var p = Ready(g);
        p.Shield = 0;
        p.Standing = null;
        p.Pos = new Vec(100, 100);
        var e = Enemy(g, RiderKind.Hunter, new Vec(106, 108)); // lower: we win
        g.Step(default);
        Assert.True(e.Dead || !g.Enemies.Contains(e));
        Assert.Equal(750, g.Score[0]);
        Assert.Single(g.Eggs);
        Assert.False(p.Dead);
    }

    [Fact]
    public void A_lower_lance_loses_and_level_lances_bounce()
    {
        var g = NewGame();
        var p = Ready(g);
        p.Shield = 0;
        p.Standing = null;
        p.Pos = new Vec(100, 100);
        Enemy(g, RiderKind.Bounder, new Vec(106, 101)); // level: bounce
        g.Step(default);
        Assert.False(p.Dead);
        Assert.Equal(0, g.Score[0]);
        p.Pos = new Vec(200, 120);
        p.Vel = new Vec(0, 0);
        Enemy(g, RiderKind.Bounder, new Vec(204, 110)); // higher: it wins
        g.Step(default);
        Assert.True(p.Dead);
        Assert.Contains(g.LossLog, l => l.Contains("jousted by Bounder"));
    }

    [Fact]
    public void Eggs_score_more_each_time_and_more_caught_in_the_air()
    {
        var g = NewGame();
        var p = Ready(g);
        int Collect(bool resting)
        {
            int before = g.Score[0];
            g.Eggs.Add(new Egg { Pos = p.Pos + new Vec(0, -4), Resting = resting, From = RiderKind.Bounder, Age = 1 });
            g.Step(default);
            return g.Score[0] - before;
        }
        Assert.Equal(250, Collect(true));
        Assert.Equal(500, Collect(true));
        Assert.Equal(750, Collect(true));
        Assert.Equal(1000, Collect(true));
        Assert.Equal(1000, Collect(true));
        Assert.Equal(1500, Collect(false)); // in mid-air
    }

    [Fact]
    public void An_egg_left_alone_hatches_into_a_tougher_rider()
    {
        var g = NewGame();
        Ready(g);
        g.Enemies.Clear();
        var egg = new Egg { Pos = new Vec(150, 66), Resting = true, From = RiderKind.Bounder, HatchAt = 1 };
        g.Eggs.Add(egg);
        // Keep player 1 well away, on the ground.
        g.Player[0]!.Pos = new Vec(80, Arena.GroundY);
        for (int i = 0; i < 60 * 12 && !g.Enemies.Any(e => e.Kind == RiderKind.Hunter); i++) g.Step(default);
        Assert.Contains(g.Enemies, e => e.Kind == RiderKind.Hunter);
        Assert.DoesNotContain(egg, g.Eggs);
    }

    [Fact]
    public void The_bridges_burn_on_the_third_wave_and_the_lava_is_deadly()
    {
        var g = NewGame();
        Assert.True(g.BridgesStanding);
        g.JumpToWave(3);
        Assert.True(g.BridgeFire > 0);
        g.Idle(60 * 4);
        Assert.False(g.BridgesStanding);
        var p = g.Player[0] ?? Ready(g);
        p = Ready(g);
        p.Shield = 0;
        p.Materializing = 0;
        p.Standing = null;
        p.Pos = new Vec(20, Arena.LavaY - 2);
        p.Vel = new Vec(0, 100);
        int lives = g.Lives[0];
        g.Idle(5);
        Assert.True(p.Dead);
        Assert.Contains(g.LossLog, l => l.Contains("lava"));
    }

    [Fact]
    public void Wave_kinds_follow_the_pattern()
    {
        Assert.Equal(GameEngine.WaveKind.Plain, GameEngine.KindOf(1));
        Assert.Equal(GameEngine.WaveKind.Survival, GameEngine.KindOf(2));
        Assert.Equal(GameEngine.WaveKind.Plain, GameEngine.KindOf(3));
        Assert.Equal(GameEngine.WaveKind.Egg, GameEngine.KindOf(4));
        Assert.Equal(GameEngine.WaveKind.Pterodactyl, GameEngine.KindOf(5));
        Assert.Equal(GameEngine.WaveKind.Survival, GameEngine.KindOf(7));
        Assert.All(GameEngine.Lineup(1), k => Assert.Equal(RiderKind.Bounder, k));
        Assert.Contains(RiderKind.Hunter, GameEngine.Lineup(3));
        Assert.Contains(RiderKind.ShadowLord, GameEngine.Lineup(10));
        Assert.True(GameEngine.Lineup(20).Count <= 8);
    }

    [Fact]
    public void The_pterodactyl_dies_only_to_a_lance_in_its_open_mouth()
    {
        // Head on with its mouth open: it dies.
        var g = NewGame();
        var p = Ready(g);
        p.Shield = 0;
        p.Standing = null;
        p.Pos = new Vec(150, 120);
        p.Facing = 1;
        g.SummonPterodactyl(160, 120 - 9, -1);
        g.Pterodactyls[0].Clock = 0.1; // mouth open
        g.Step(new Controls(false, true, false));
        Assert.Empty(g.Pterodactyls);
        Assert.Equal(1000, g.Score[0]);
        Assert.False(p.Dead);

        // From behind: it kills.
        var g2 = NewGame(seed: 2);
        var q = Ready(g2);
        q.Shield = 0;
        q.Standing = null;
        q.Pos = new Vec(150, 120);
        q.Facing = -1;
        g2.SummonPterodactyl(160, 120 - 9, -1);
        g2.Step(new Controls(true, false, false));
        Assert.True(q.Dead);
    }

    [Fact]
    public void Two_players_can_unseat_each_other_for_the_gladiators_prize()
    {
        var g = NewGame(players: 2);
        var a = Ready(g, 0);
        var b = Ready(g, 1);
        a.Shield = b.Shield = 0;
        a.Standing = b.Standing = null;
        a.Pos = new Vec(100, 90);
        b.Pos = new Vec(106, 100);
        g.Step(default);
        Assert.True(b.Dead);
        Assert.Equal(3000, g.Score[0]);
    }

    [Fact]
    public void An_extra_rider_every_twenty_thousand()
    {
        var g = NewGame();
        int lives = g.Lives[0];
        g.AddScore(0, 19999);
        Assert.Equal(lives, g.Lives[0]);
        g.AddScore(0, 1);
        Assert.Equal(lives + 1, g.Lives[0]);
        g.AddScore(0, 20000);
        Assert.Equal(lives + 2, g.Lives[0]);
    }

    [Fact]
    public void The_demonstration_pilot_clears_waves_and_the_game_ends_in_the_table()
    {
        var g = NewGame(seed: 8);
        var pilot = new AttractPilot(8);
        for (int i = 0; i < 60 * 60 * 6 && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
        Assert.True(g.Wave >= 4, $"reached wave {g.Wave}, state {g.State}, lives {g.Lives[0]}, losses {string.Join(" / ", g.LossLog)}");
        for (int i = 0; i < 60 * 60 * 6 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Finish();
        g.Idle(2);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(g.Score[0], HighScoreTable.Load(_dir).TopScore);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = NewGame(seed: 31);
            var pilot = new AttractPilot(4);
            for (int i = 0; i < 60 * 60; i++) g.Step(pilot.Decide(g));
            return $"{g.Wave} {g.Score[0]} {g.Lives[0]} {g.Enemies.Count} {g.Eggs.Count}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_title_runs_the_demonstration_then_the_high_scores()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 1)));
        Assert.Equal(GameState.Attract, g.State);
        g.Idle((int)(60 * (GameEngine.AttractSeconds + 1)));
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void The_title_plays_its_tune_and_the_demonstration_has_sound()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 10)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.Contains(Sfx.WaveStart, heard);
        Assert.Contains(Sfx.Flap, heard);
        // Back to the title: the tune again.
        g.ReturnToTitle();
        heard.Clear();
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        // Silent means silent.
        g.Silent = true;
        g.ReturnToTitle();
        heard.Clear();
        g.Idle(60);
        Assert.Empty(heard);
    }

    [Fact]
    public void Every_sound_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        foreach (var sfx in Enum.GetValues<Sfx>())
        {
            string name = sfx == Sfx.WaveStart ? "WaveStart" : sfx.ToString();
            Assert.True(File.Exists(Path.Combine(folder, name + ".wav")), name);
        }
    }

    [Fact]
    public void Every_sprite_is_well_formed()
    {
        // The sprite tables check their rows as they load; touching them is the test.
        Assert.Equal(17, Sprites.WingsUp.Height);
        Assert.Equal(Sprites.WingsUp.Height, Sprites.WingsDown.Height);
        Assert.All(Sprites.Run, s => Assert.Equal(Sprites.WingsUp.Height, s.Height));
        Assert.Equal(2, Sprites.Pterodactyl.Length);
    }
}
