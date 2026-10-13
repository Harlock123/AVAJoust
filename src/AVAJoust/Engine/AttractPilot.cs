using AVAJoust.Models;

namespace AVAJoust.Engine;

/// <summary>
/// The computer player for attract mode and the tests. It plays the old ceiling-hugger's game: cruise
/// just under the roof, where nothing can get above you, swoop on enemies well beneath and climb
/// straight back, pick up eggs when the coast is clear, and keep out of the lava and the pterodactyl's way.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);
    private double _sinceFlap;
    private int _patrol = 1;
    private double _turnClock;

    /// <summary>How readily it swoops (0: it only cruises).</summary>
    public double Aggression { get; init; } = 1;

    private const double Cruise = Arena.Ceiling + Rider.BodyHeight + 14;

    public Controls Decide(GameEngine g, int player = 0)
    {
        var me = g.Player[player];
        if (me is not { Dead: false } || me.Materializing > 0) return default;
        _sinceFlap += Arena.Step;
        _turnClock -= Arena.Step;
        if (_turnClock <= 0)
        {
            _turnClock = 3 + _rng.NextDouble() * 3;
            if (_rng.NextDouble() < 0.4) _patrol = -_patrol;
        }

        var enemies = g.Enemies.Where(e => !e.Dead && e.Materializing <= 0).ToList();
        double tx = me.Pos.X + _patrol * 40, ty = Cruise;

        // Swoop on an enemy well beneath and close; never on one near our height.
        var prey = enemies
            .Where(e => e.Pos.Y > me.Pos.Y + 16 && Math.Abs(Arena.DeltaX(me.Pos.X, e.Pos.X)) < 60)
            .OrderBy(e => Math.Abs(Arena.DeltaX(me.Pos.X, e.Pos.X))).FirstOrDefault();
        if (prey is not null && Aggression > 0)
        {
            tx = prey.Pos.X + prey.Vel.X * 0.15;
            ty = prey.Pos.Y - 7;
        }
        else if (g.Eggs.Where(e => !e.Collected).OrderBy(e => Dist(me, e.Pos)).FirstOrDefault() is { } egg
                 && !enemies.Any(e => Dist(me, e.Pos) < 90))
        {
            tx = egg.Pos.X;
            ty = egg.Pos.Y - 2;
        }
        else if (enemies.Count > 0)
        {
            // Patrol towards the nearest enemy, high up.
            var nearest = enemies.OrderBy(e => Math.Abs(Arena.DeltaX(me.Pos.X, e.Pos.X))).First();
            tx = nearest.Pos.X;
        }

        // Anything at or above our height and close: climb, and slide away from it.
        var threat = enemies
            .Where(e => e.Pos.Y < me.Pos.Y + 6 && Math.Abs(Arena.DeltaX(me.Pos.X, e.Pos.X)) < 50)
            .OrderBy(e => Math.Abs(Arena.DeltaX(me.Pos.X, e.Pos.X))).FirstOrDefault();
        if (threat is not null)
        {
            ty = Math.Max(Cruise, threat.Pos.Y - 24);
            tx = me.Pos.X - Math.Sign(Arena.DeltaX(me.Pos.X, threat.Pos.X)) * 40;
        }

        // The pterodactyl: meet its open mouth head on, otherwise keep clear above or below.
        var ptero = g.Pterodactyls.OrderBy(p => Math.Abs(Arena.DeltaX(me.Pos.X, p.Pos.X))).FirstOrDefault();
        if (ptero is not null && Math.Abs(Arena.DeltaX(me.Pos.X, ptero.Pos.X)) < 110)
        {
            bool coming = Math.Sign(Arena.DeltaX(ptero.Pos.X, me.Pos.X)) == ptero.Facing;
            if (coming && ptero.MouthOpen)
            {
                tx = ptero.Pos.X;
                ty = ptero.Head.Y + 11;
            }
            else
            {
                ty = ptero.Pos.Y > 90 ? Cruise : Math.Min(Arena.GroundY - 40, ptero.Pos.Y + 55);
            }
        }

        // Keep out of the lava (and away from the hand).
        bool overLava = !g.BridgesStanding && !Arena.Ground(false)[0].Spans(me.Pos.X);
        if (overLava) ty = Math.Min(ty, Arena.GroundY - 40);

        double dx = Arena.DeltaX(me.Pos.X, tx);
        int dir = Math.Abs(dx) < 3 ? 0 : Math.Sign(dx);
        bool urgent = me.Grabbed || (overLava && me.Pos.Y > Arena.GroundY - 30) || threat is not null;
        bool want = me.Pos.Y > ty + 1 || urgent;
        bool flap = want && _sinceFlap > (urgent ? 0.13 : 0.2) && me.Vel.Y > (urgent ? -120 : -70);
        if (flap) _sinceFlap = 0;
        return new Controls(dir < 0, dir > 0, flap);
    }

    private static double Dist(Rider r, Vec p) => Math.Abs(Arena.DeltaX(r.Pos.X, p.X)) + Math.Abs(r.Pos.Y - p.Y);
}
