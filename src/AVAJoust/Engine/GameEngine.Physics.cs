using AVAJoust.Models;

namespace AVAJoust.Engine;

public sealed partial class GameEngine
{
    public const double Gravity = 230;
    public const double FlapLift = 92;
    public const double MaxRise = 150;
    public const double MaxFall = 170;
    public const double AirAccel = 75;
    public const double FlapPush = 16;
    public const double RunAccel = 170;
    public const double SkidDecel = 260;
    public const double GroundFriction = 120;

    /// <summary>Top speed for each kind of rider (enemies get faster as the waves go by).</summary>
    private double TopSpeed(Rider r) => r.Kind switch
    {
        RiderKind.Bounder => 72 + Math.Min(20, Wave * 1.5),
        RiderKind.Hunter => 92 + Math.Min(20, Wave * 1.5),
        RiderKind.ShadowLord => 112 + Math.Min(20, Wave * 1.5),
        _ => 120,
    };

    /// <summary>Moves a rider one step: running, flapping, gravity, ledges, the ceiling, the wrap, the lava.</summary>
    private void Move(Rider r, int dir, bool flap, double dt)
    {
        if (r.Dead) return;
        if (r.Materializing > 0)
        {
            r.Materializing -= dt;
            return;
        }
        if (r.Shield > 0)
        {
            r.Shield -= dt;
            if (dir != 0 || flap) r.Shield = 0; // moving gives up the spawn shield
        }
        r.SinceFlap += dt;
        var v = r.Vel;
        double top = TopSpeed(r);

        if (r.Grabbed)
        {
            // The troll's hand drags its catch down; hard flapping may pull free.
            r.GrabTime += dt;
            v = new Vec(v.X * 0.9, 26);
            if (flap)
            {
                Play(Sfx.Flap);
                r.SinceFlap = 0;
                if (_rng.NextDouble() < 0.22)
                {
                    r.Grabbed = false;
                    v = new Vec(v.X, -FlapLift * 1.4);
                }
            }
            r.Vel = v;
            r.Pos = new Vec(r.Pos.X, r.Pos.Y + v.Y * dt);
            if (r.Pos.Y >= Arena.LavaY + 6) Burn(r);
            return;
        }

        if (dir != 0) r.Facing = dir;

        if (r.Standing is { } standing)
        {
            // Running along a ledge: speed up, or skid when turning against the way you're going.
            r.Skidding = dir != 0 && Math.Sign(v.X) == -dir && Math.Abs(v.X) > 25;
            double vx = v.X;
            if (r.Skidding) vx = Toward(vx, 0, SkidDecel * dt);
            else if (dir != 0) vx = Math.Clamp(vx + dir * RunAccel * dt, -top * 0.85, top * 0.85);
            else vx = Toward(vx, 0, GroundFriction * dt);
            if (r.Skidding && r.RunPhase % 1 < 0.05) Play(Sfx.Skid);
            r.RunPhase += Math.Abs(vx) * dt / 9;
            v = new Vec(vx, 0);
            if (flap)
            {
                r.Standing = null;
                v = new Vec(v.X + dir * FlapPush, -FlapLift);
                r.SinceFlap = 0;
                Play(Sfx.Flap);
            }
            else
            {
                double x = Arena.Wrap(r.Pos.X + v.X * dt);
                r.Pos = new Vec(x, standing.Top);
                if (!standing.Spans(x)) r.Standing = null; // ran off the edge
                r.Vel = v;
                return;
            }
        }
        else
        {
            // In the air: gravity, steering, and a flap's lift.
            r.Skidding = false;
            double vx = v.X + dir * AirAccel * dt;
            double vy = v.Y + Gravity * dt;
            if (flap)
            {
                vy = Math.Max(vy - FlapLift, -MaxRise);
                vx += dir * FlapPush;
                r.SinceFlap = 0;
                Play(Sfx.Flap);
            }
            v = new Vec(Math.Clamp(vx, -top, top), Math.Min(vy, MaxFall));
        }

        var old = r.Pos;
        var pos = new Vec(old.X + v.X * dt, old.Y + v.Y * dt);

        // The ceiling.
        if (pos.Y - Rider.BodyHeight < Arena.Ceiling)
        {
            pos = new Vec(pos.X, Arena.Ceiling + Rider.BodyHeight);
            v = new Vec(v.X, Math.Abs(v.Y) * 0.3);
        }

        foreach (var ledge in Ledges)
        {
            bool over = ledge.Spans(pos.X);
            // Landing on top.
            if (over && v.Y >= 0 && old.Y <= ledge.Top + 0.5 && pos.Y >= ledge.Top)
            {
                pos = new Vec(pos.X, ledge.Top);
                v = new Vec(v.X, 0);
                r.Standing = ledge;
                break;
            }
            // Bumping the underside.
            if (over && v.Y < 0 && old.Y - Rider.BodyHeight >= ledge.Bottom - 0.5 && pos.Y - Rider.BodyHeight < ledge.Bottom)
            {
                pos = new Vec(pos.X, ledge.Bottom + Rider.BodyHeight);
                v = new Vec(v.X, 40);
                continue;
            }
            // Bumping the end of a ledge side-on.
            bool level = pos.Y > ledge.Top + 1 && pos.Y - Rider.BodyHeight < ledge.Bottom;
            if (level && over && !ledge.Spans(old.X))
            {
                pos = new Vec(old.X, pos.Y);
                v = new Vec(-v.X * 0.6, v.Y);
            }
        }

        r.Pos = new Vec(Arena.Wrap(pos.X), pos.Y);
        r.Vel = v;

        // Low over the lava the troll may reach up; into the lava is the end.
        if (!BridgesStanding && r.Pos.Y > Arena.GroundY - 4 && !Arena.Ground(false)[0].Spans(r.Pos.X))
        {
            if (r.Pos.Y >= Arena.LavaY) Burn(r);
            else if (TrollAwake && !r.Grabbed && _rng.NextDouble() < 0.06)
            {
                r.Grabbed = true;
                r.GrabTime = 0;
                Play(Sfx.Grab);
            }
        }
        if (r.Pos.Y > Arena.Height + 20) Burn(r);
    }

    private static double Toward(double value, double target, double step) =>
        value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);

    /// <summary>Into the lava: a player loses a life; an enemy is simply gone (no egg, no points).</summary>
    private void Burn(Rider r)
    {
        if (r.Dead) return;
        r.Dead = true;
        r.Grabbed = false;
        Play(Sfx.Sizzle);
        Effects.Add(new Effect { Kind = EffectKind.Splash, Pos = new Vec(r.Pos.X, Arena.LavaY), Life = 0.8 });
        if (r.IsPlayer) PlayerLost(r, "lava");
    }

    /// <summary>Eggs fall, bounce and roll to a stop; in the lava they're lost.</summary>
    private void MoveEgg(Egg e, double dt)
    {
        e.Age += dt;
        if (e.Resting)
        {
            if (!e.Hatched && e.Age >= e.HatchAt)
            {
                e.Hatched = true;
                Play(Sfx.Hatch);
            }
            if (e.Hatched) e.HatchedFor += dt;
            return;
        }
        var v = e.Vel + new Vec(0, Gravity * dt);
        var old = e.Pos;
        var pos = new Vec(Arena.Wrap(old.X + v.X * dt), old.Y + v.Y * dt);
        foreach (var ledge in Ledges)
        {
            if (ledge.Spans(pos.X) && v.Y >= 0 && old.Y <= ledge.Top + 0.5 && pos.Y >= ledge.Top)
            {
                pos = new Vec(pos.X, ledge.Top);
                // A bounce, smaller each time, then it rolls to a stop.
                v = new Vec(v.X * 0.6, Math.Abs(v.Y) > 30 ? -Math.Abs(v.Y) * 0.4 : 0);
                if (v.Y == 0 && Math.Abs(v.X) < 4) e.Resting = true;
                break;
            }
        }
        if (!e.Resting && v.Y == 0)
        {
            // Rolling: slow down; roll off an edge and fall again.
            v = new Vec(Toward(v.X, 0, 40 * dt), 0);
            if (!Ledges.Any(l => l.Spans(pos.X) && Math.Abs(l.Top - pos.Y) < 0.5)) v = new Vec(v.X, 1);
            else if (Math.Abs(v.X) < 4) e.Resting = true;
        }
        e.Pos = pos;
        e.Vel = v;
        if (pos.Y >= Arena.LavaY || pos.Y > Arena.Height)
        {
            e.Burnt = true;
            Effects.Add(new Effect { Kind = EffectKind.Splash, Pos = new Vec(pos.X, Arena.LavaY), Life = 0.8 });
        }
    }
}
