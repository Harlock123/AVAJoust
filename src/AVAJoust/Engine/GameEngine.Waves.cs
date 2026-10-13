using AVAJoust.Models;

namespace AVAJoust.Engine;

public sealed partial class GameEngine
{
    public enum WaveKind { Plain, Survival, Egg, Pterodactyl }

    /// <summary>Wave 2 and every fifth after is a survival wave; 4, 9... egg waves; 5, 10... pterodactyl waves.</summary>
    public static WaveKind KindOf(int wave) => wave switch
    {
        _ when wave % 5 == 2 => WaveKind.Survival,
        _ when wave % 5 == 4 => WaveKind.Egg,
        _ when wave % 5 == 0 => WaveKind.Pterodactyl,
        _ => WaveKind.Plain,
    };

    public WaveKind CurrentKind => KindOf(Wave);

    private readonly Queue<RiderKind> _toSpawn = new();
    private double _spawnClock;
    private readonly double[] _respawn = new double[2];
    private readonly bool[] _diedThisWave = new bool[2];
    private readonly int[] _eggChain = new int[2];
    private double _pteroClock;
    private double _waveOverClock;

    /// <summary>The riders a wave brings: more of them, and tougher, as the waves go by.</summary>
    public static List<RiderKind> Lineup(int wave)
    {
        int count = Math.Min(3 + (wave + 1) / 2, 8);
        var list = new List<RiderKind>();
        for (int i = 0; i < count; i++)
        {
            double t = (i + 0.5) / count;
            double shadow = wave >= 6 ? Math.Min(0.5, (wave - 5) * 0.08) : 0;
            double hunter = wave >= 2 ? Math.Min(0.7, 0.2 + (wave - 2) * 0.1) : 0;
            list.Add(t < shadow ? RiderKind.ShadowLord : t < shadow + hunter ? RiderKind.Hunter : RiderKind.Bounder);
        }
        return list;
    }

    private void NextWave()
    {
        Wave++;
        WaveTime = 0;
        _waveOverClock = 0;
        _pteroClock = 0;
        Pterodactyls.Clear();
        Buzzards.Clear();
        _toSpawn.Clear();
        _spawnClock = 1.2;
        for (int i = 0; i < 2; i++) { _diedThisWave[i] = false; _eggChain[i] = 0; }

        if (Wave == 3 && BridgesStanding)
        {
            BridgeFire = 2.5;
            Play(Sfx.BridgeBurns);
        }

        var kind = CurrentKind;
        if (kind == WaveKind.Egg)
        {
            // Eggs laid on the ledges: collect them before they hatch.
            int count = Math.Min(4 + Wave / 3, 10);
            for (int i = 0; i < count; i++)
            {
                var ledge = Arena.Floating[i % Arena.Floating.Count];
                double x = Arena.Wrap(Math.Clamp(ledge.Left + 10 + _rng.NextDouble() * (ledge.Width - 20), ledge.Left + 6, ledge.Right - 6));
                Eggs.Add(new Egg { Pos = new Vec(x, ledge.Top), Resting = true, From = RiderKind.Bounder, HatchAt = 6 + _rng.NextDouble() * 4 });
            }
        }
        else
        {
            foreach (var k in Lineup(Wave)) _toSpawn.Enqueue(k);
        }
        string sub = kind switch
        {
            WaveKind.Survival => "SURVIVAL WAVE",
            WaveKind.Egg => "EGG WAVE",
            WaveKind.Pterodactyl => "BEWARE THE PTERODACTYL",
            _ => "",
        };
        SetBanner($"WAVE {Wave}", sub, 3);
        Play(Sfx.WaveStart);
    }

    /// <summary>The game while it is played (or demonstrated).</summary>
    private void Simulate(double dt, Controls p1, Controls p2)
    {
        WaveTime += dt;
        UpdateEffects(dt);
        if (BannerTime > 0 && (BannerTime -= dt) <= 0) Banner = Banner2 = "";
        if (BridgeFire > 0 && (BridgeFire -= dt) <= 0) BridgesStanding = false;

        // In a one-player game either set of keys flies player 1.
        if (Players == 1) p1 = new Controls(p1.Left || p2.Left, p1.Right || p2.Right, p1.Flap || p2.Flap);
        Controls[] input = [p1, p2];
        for (int i = 0; i < 2; i++)
        {
            var me = Player[i];
            if (me is null || me.Dead)
            {
                Respawn(i, dt);
                continue;
            }
            var c = input[i];
            bool press = c.Flap && !_flapPrev[i];
            // Holding the button flaps steadily, for those who'd rather not hammer it.
            _flapHeld[i] = c.Flap ? _flapHeld[i] + dt : 0;
            bool flap = press || (c.Flap && _flapHeld[i] > 0.22 && me.SinceFlap > 0.2);
            _flapPrev[i] = c.Flap;
            Move(me, (c.Right ? 1 : 0) - (c.Left ? 1 : 0), flap, dt);
        }

        SpawnEnemies(dt);
        foreach (var e in Enemies) Think(e, dt);
        foreach (var egg in Eggs) MoveEgg(egg, dt);
        UpdateBuzzards(dt);
        UpdatePterodactyls(dt);
        Joust();
        CollectEggs();
        Enemies.RemoveAll(e => e.Dead);
        Eggs.RemoveAll(e => e.Collected || e.Burnt);

        if (State is not (GameState.Playing or GameState.Attract)) return;
        CheckWaveOver(dt);
    }

    private void SpawnEnemies(double dt)
    {
        if (_toSpawn.Count == 0) return;
        _spawnClock -= dt;
        if (_spawnClock > 0) return;
        _spawnClock = 1.1;
        var pad = FreePad();
        if (pad is null) return;
        var kind = _toSpawn.Dequeue();
        Enemies.Add(new Rider
        {
            Kind = kind,
            Pos = pad.Value,
            Facing = _rng.Next(2) == 0 ? -1 : 1,
            Materializing = 1.2,
            Standing = Ledges.FirstOrDefault(l => l.Spans(pad.Value.X) && Math.Abs(l.Top - pad.Value.Y) < 1),
            TargetY = 40 + _rng.NextDouble() * 120,
            Wander = _rng.Next(2) == 0 ? -1 : 1,
        });
        Play(Sfx.Materialize);
    }

    /// <summary>A spawn pad with nobody near it.</summary>
    private Vec? FreePad()
    {
        var riders = AllRiders().ToList();
        var free = Arena.Pads.Where(p => !riders.Any(r => Math.Abs(Arena.DeltaX(r.Pos.X, p.X)) < 24 && Math.Abs(r.Pos.Y - p.Y) < 24)).ToList();
        return free.Count == 0 ? null : free[_rng.Next(free.Count)];
    }

    public IEnumerable<Rider> AllRiders()
    {
        foreach (var p in Player) if (p is { Dead: false }) yield return p;
        foreach (var e in Enemies) if (!e.Dead) yield return e;
    }

    private void Respawn(int i, double dt)
    {
        if (!InGame[i] || Lives[i] <= 0) return;
        _respawn[i] -= dt;
        if (_respawn[i] > 0) return;
        var pad = i == 0 ? Arena.Pads[0] : Arena.Pads[1];
        if (AllRiders().Any(r => Math.Abs(Arena.DeltaX(r.Pos.X, pad.X)) < 20 && Math.Abs(r.Pos.Y - pad.Y) < 20)) pad = FreePad() ?? pad;
        Lives[i]--;
        Player[i] = new Rider
        {
            Kind = i == 0 ? RiderKind.Player1 : RiderKind.Player2,
            Pos = pad,
            Facing = i == 0 ? 1 : -1,
            Materializing = 1.0,
            Shield = 3,
            Standing = Ledges.FirstOrDefault(l => l.Spans(pad.X) && Math.Abs(l.Top - pad.Y) < 1),
        };
        _flapPrev[i] = true; // no flap carried over from the last life
        Play(Sfx.Materialize);
    }

    /// <summary>How each life was lost (for tuning and tests).</summary>
    internal List<string> LossLog { get; } = [];

    private void PlayerLost(Rider r, string why = "")
    {
        LossLog.Add($"wave {Wave} t {WaveTime:0.0}: {why} at {r.Pos} vel {r.Vel}");
        int i = r.PlayerIndex;
        r.Dead = true;
        _diedThisWave[i] = true;
        _eggChain[i] = 0;
        Play(Sfx.PlayerDie);
        Puff(r.Pos + new Vec(0, -7));
        _respawn[i] = 2.0;
        if (Lives[i] <= 0)
        {
            InGame[i] = false;
            if (!InGame.Any(x => x) && (Player[0] is null or { Dead: true }) && (Player[1] is null or { Dead: true }))
                GameOver();
        }
    }

    private void CheckWaveOver(double dt)
    {
        if (_toSpawn.Count > 0 || Enemies.Count > 0 || Eggs.Count > 0 || Buzzards.Count > 0) return;
        _waveOverClock += dt;
        if (_waveOverClock < 1.5) return;
        if (CurrentKind == WaveKind.Survival)
        {
            for (int i = 0; i < Players; i++)
                if (!_diedThisWave[i] && InGame[i])
                {
                    AddScore(i, 3000);
                    if (Player[i] is { } p) Popup(p.Pos + new Vec(0, -20), 3000);
                    Play(Sfx.Bonus);
                }
        }
        NextWave();
    }

    // ----- enemy brains -----------------------------------------------------------------------

    /// <summary>
    /// Bounders wander; hunters go for the nearest player; shadow lords climb above a player and dive.
    /// Everyone keeps clear of the lava.
    /// </summary>
    private void Think(Rider e, double dt)
    {
        if (e.Materializing > 0) { Move(e, 0, false, dt); return; }
        e.Think -= dt;
        var target = Player.Where(p => p is { Dead: false, Materializing: <= 0 })
            .OrderBy(p => Math.Abs(Arena.DeltaX(e.Pos.X, p!.Pos.X)) + Math.Abs(p.Pos.Y - e.Pos.Y)).FirstOrDefault();

        if (e.Think <= 0)
        {
            e.Think = 0.6 + _rng.NextDouble() * 1.2;
            switch (e.Kind)
            {
                case RiderKind.Bounder:
                    e.TargetY = 30 + _rng.NextDouble() * 160;
                    if (_rng.NextDouble() < 0.25) e.Wander = -e.Wander;
                    break;
                case RiderKind.Hunter:
                    e.TargetY = target is null ? 60 + _rng.NextDouble() * 100 : target.Pos.Y - 6 - _rng.NextDouble() * 14;
                    e.Wander = target is null ? e.Wander : Math.Sign(Arena.DeltaX(e.Pos.X, target.Pos.X)) is var s && s != 0 ? s : e.Wander;
                    break;
                default:
                    e.TargetY = target is null ? 40 : target.Pos.Y - 14 - _rng.NextDouble() * 10;
                    e.Wander = target is null ? e.Wander : Math.Sign(Arena.DeltaX(e.Pos.X, target.Pos.X)) is var t && t != 0 ? t : e.Wander;
                    e.Think *= 0.6;
                    break;
            }
            e.TargetY = Math.Clamp(e.TargetY, 24, Arena.GroundY - 24);
        }
        // Keep off the lava, and away from a hand.
        double floor = BridgesStanding ? Arena.GroundY : Arena.GroundY - 30;
        bool low = e.Pos.Y > Math.Min(e.TargetY, floor - 10);
        double flapGap = e.Kind switch { RiderKind.Bounder => 0.34, RiderKind.Hunter => 0.26, _ => 0.2 };
        bool flap = (low && e.SinceFlap > flapGap && e.Vel.Y > -40) || (e.Grabbed && e.SinceFlap > 0.15)
            || (e.OnGround && _rng.NextDouble() < 0.02);
        Move(e, e.Wander, flap, dt);
    }

    // ----- jousts ------------------------------------------------------------------------------

    /// <summary>Riders that meet: the higher lance wins; level lances bounce apart.</summary>
    private void Joust()
    {
        var riders = AllRiders().Where(r => r.Solid).ToList();
        for (int a = 0; a < riders.Count; a++)
            for (int b = a + 1; b < riders.Count; b++)
            {
                var r1 = riders[a];
                var r2 = riders[b];
                if (r1.Dead || r2.Dead) continue;
                double dx = Arena.DeltaX(r1.Pos.X, r2.Pos.X);
                if (Math.Abs(dx) > Rider.HalfWidth * 2 - 2 || Math.Abs(r1.Pos.Y - r2.Pos.Y) > Rider.BodyHeight - 2) continue;
                // Enemies don't fight each other; they just bounce.
                bool contest = r1.IsPlayer || r2.IsPlayer;
                double dy = r2.Pos.Y - r1.Pos.Y; // positive: r1 is higher
                if (!contest || Math.Abs(dy) < 3 || (r1.Safe && r2.Safe))
                {
                    Bounce(r1, r2, dx);
                    continue;
                }
                var (winner, loser) = dy > 0 ? (r1, r2) : (r2, r1);
                if (loser.Safe) { Bounce(r1, r2, dx); continue; }
                Unseat(winner, loser);
            }
    }

    private void Bounce(Rider a, Rider b, double dx)
    {
        int side = dx >= 0 ? 1 : -1; // b is to the right of a
        a.Vel = new Vec(-side * Math.Max(50, Math.Abs(a.Vel.X)), a.Vel.Y * 0.5);
        b.Vel = new Vec(side * Math.Max(50, Math.Abs(b.Vel.X)), b.Vel.Y * 0.5);
        a.Pos = new Vec(Arena.Wrap(a.Pos.X - side * 2), a.Pos.Y);
        b.Pos = new Vec(Arena.Wrap(b.Pos.X + side * 2), b.Pos.Y);
        if (a.IsPlayer || b.IsPlayer) Play(Sfx.Bounce);
    }

    private void Unseat(Rider winner, Rider loser)
    {
        if (loser.IsPlayer)
        {
            if (winner.IsPlayer)
            {
                AddScore(winner.PlayerIndex, loser.Points);
                Popup(loser.Pos + new Vec(0, -16), loser.Points);
            }
            PlayerLost(loser, $"jousted by {winner.Kind} at {winner.Pos}");
            return;
        }
        // An enemy unseated: points, and an egg falls where it was.
        loser.Dead = true;
        Play(Sfx.Unseat);
        Puff(loser.Pos + new Vec(0, -7));
        if (winner.IsPlayer)
        {
            AddScore(winner.PlayerIndex, loser.Points);
            Popup(loser.Pos + new Vec(0, -16), loser.Points);
        }
        Eggs.Add(new Egg
        {
            Pos = loser.Pos + new Vec(0, -6),
            Vel = new Vec(loser.Vel.X * 0.6, -30),
            From = loser.Kind,
            HatchAt = Math.Max(4.5, 9 - Wave * 0.3) + _rng.NextDouble() * 2,
        });
        winner.Vel = new Vec(winner.Vel.X, Math.Min(winner.Vel.Y, -20));
    }

    /// <summary>Eggs (and hatchlings) a player touches: 250, 500, 750, then 1,000 each; +500 caught in the air.</summary>
    private void CollectEggs()
    {
        foreach (var p in Player)
        {
            if (p is not { Dead: false, Materializing: <= 0 }) continue;
            foreach (var egg in Eggs)
            {
                if (egg.Collected || egg.Burnt) continue;
                // A freshly dropped egg can't be snatched straight away: you have to go after it.
                if (!egg.Resting && egg.Age < 0.45) continue;
                if (Math.Abs(Arena.DeltaX(p.Pos.X, egg.Pos.X)) > 9 || Math.Abs(p.Pos.Y - 4 - egg.Pos.Y) > 12) continue;
                egg.Collected = true;
                int i = p.PlayerIndex;
                _eggChain[i]++;
                int points = Math.Min(1000, 250 * _eggChain[i]);
                if (!egg.Resting && !egg.Hatched) points += 500;
                AddScore(i, points);
                Popup(egg.Pos + new Vec(0, -10), points);
                Play(Sfx.Egg);
            }
        }
    }

    // ----- hatchlings and their buzzards --------------------------------------------------------

    private void UpdateBuzzards(double dt)
    {
        // A hatchling waits a moment, then a riderless buzzard flies in for it.
        foreach (var egg in Eggs.Where(e => e is { Hatched: true, Collected: false } && e.HatchedFor > 1.5))
        {
            if (Buzzards.Any(b => b.For == egg)) continue;
            int from = _rng.Next(2) == 0 ? -1 : 1;
            Buzzards.Add(new Buzzard { For = egg, Pos = new Vec(Arena.Wrap(egg.Pos.X - from * 150), Math.Max(30, egg.Pos.Y - 40)), Facing = from });
            Play(Sfx.BuzzardCall);
        }
        foreach (var b in Buzzards)
        {
            var egg = b.For;
            if (egg.Collected || egg.Burnt) { b.Done = true; continue; }
            double dx = Arena.DeltaX(b.Pos.X, egg.Pos.X);
            double dy = egg.Pos.Y - b.Pos.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 3)
            {
                // Mounted: a rider one kind tougher than before.
                egg.Collected = true;
                b.Done = true;
                Enemies.Add(new Rider
                {
                    Kind = egg.HatchesInto,
                    Pos = egg.Pos,
                    Facing = b.Facing,
                    Vel = new Vec(b.Facing * 40, -60),
                    TargetY = 60,
                    Wander = b.Facing,
                });
                continue;
            }
            double speed = 110 * dt;
            b.Facing = dx >= 0 ? 1 : -1;
            b.Pos = new Vec(Arena.Wrap(b.Pos.X + dx / dist * Math.Min(speed, dist)), b.Pos.Y + dy / dist * Math.Min(speed, dist));
        }
        Buzzards.RemoveAll(b => b.Done);
    }

    // ----- the pterodactyl ------------------------------------------------------------------------

    /// <summary>When the pterodactyl comes: soon on its own waves, otherwise when a wave drags on.</summary>
    public double PterodactylDue => CurrentKind == WaveKind.Pterodactyl ? 4 : Math.Max(35, 75 - Wave * 2);

    private void UpdatePterodactyls(double dt)
    {
        _pteroClock += dt;
        bool waveAlive = _toSpawn.Count > 0 || Enemies.Count > 0 || Eggs.Count > 0;
        if (waveAlive && WaveTime > PterodactylDue && _pteroClock > 0 && Pterodactyls.Count(p => !p.Dead) < 1 + Wave / 10)
        {
            int from = _rng.Next(2) == 0 ? -1 : 1;
            // In from one side, flying across towards the other.
            Pterodactyls.Add(new Pterodactyl { Pos = new Vec(from < 0 ? Arena.Width + 10 : -10, 40 + _rng.NextDouble() * 120), Facing = from < 0 ? -1 : 1 });
            _pteroClock = -18; // the next one, if this one goes, comes later
            Play(Sfx.Screech);
        }
        foreach (var p in Pterodactyls)
        {
            p.Clock += dt;
            // It sweeps across, dipping towards the nearest player.
            var target = Player.Where(r => r is { Dead: false }).OrderBy(r => Math.Abs(Arena.DeltaX(p.Pos.X, r!.Pos.X))).FirstOrDefault();
            // It weaves as it flies, so it can be met head on.
            double wantY = (target is null ? 90 : target.Pos.Y - 8) + Math.Sin(p.Clock * 2.3) * 18;
            if (target is not null && Math.Abs(Arena.DeltaX(p.Pos.X, target.Pos.X)) > 100) p.Facing = Math.Sign(Arena.DeltaX(p.Pos.X, target.Pos.X)) is var s && s != 0 ? s : p.Facing;
            double vy = Math.Clamp((wantY - p.Pos.Y) * 1.2, -65, 65);
            double vx = p.Facing * (128 + Math.Min(40, Wave * 2));
            p.Vel = new Vec(vx, vy);
            p.Pos = new Vec(Arena.Wrap(p.Pos.X + vx * dt), Math.Clamp(p.Pos.Y + vy * dt, 20, Arena.GroundY - 8));
            if ((int)(p.Clock * 0.8) != (int)((p.Clock - dt) * 0.8)) Play(Sfx.Screech);

            foreach (var r in Player)
            {
                if (r is not { Dead: false } || r.Materializing > 0 || p.Dead) continue;
                double dx = Arena.DeltaX(r.Pos.X, p.Pos.X);
                if (Math.Abs(dx) > 13 || Math.Abs(r.Pos.Y - 9 - p.Pos.Y) > 9) continue;
                // Only a lance meeting the open mouth, head on and level, kills it.
                bool headOn = r.Facing == Math.Sign(dx) && p.Facing == -r.Facing;
                bool level = Math.Abs(r.Pos.Y - 11 - p.Head.Y) < 6;
                if (headOn && level && p.MouthOpen)
                {
                    p.Dead = true;
                    AddScore(r.PlayerIndex, 1000);
                    Popup(p.Pos + new Vec(0, -12), 1000);
                    Puff(p.Pos);
                    Play(Sfx.PteroDie);
                }
                else if (!r.Safe) PlayerLost(r, $"pterodactyl at {p.Pos} facing {p.Facing}, mouth {p.MouthOpen}");
            }
        }
        Pterodactyls.RemoveAll(p => p.Dead);
    }
}

public sealed partial class GameEngine
{
    // ----- hooks for tests and screenshots -----------------------------------------------------

    /// <summary>Jumps to the start of a wave.</summary>
    internal void JumpToWave(int wave)
    {
        Wave = wave - 1;
        Enemies.Clear();
        Eggs.Clear();
        if (wave > 3) { BridgesStanding = false; BridgeFire = 0; }
        NextWave();
    }

    /// <summary>Brings the pterodactyl now.</summary>
    internal void SummonPterodactyl(double x, double y, int facing) =>
        Pterodactyls.Add(new Pterodactyl { Pos = new Vec(x, y), Facing = facing });

    /// <summary>Steps with no input.</summary>
    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Step(default);
    }
}
