using AVAJoust.Models;

namespace AVAJoust.Engine;

public enum RiderKind { Player1, Player2, Bounder, Hunter, ShadowLord }

/// <summary>A knight on a bird: a player, or an enemy on a buzzard.</summary>
public sealed class Rider
{
    public const double HalfWidth = 7;
    public const double BodyHeight = 16;

    public required RiderKind Kind { get; set; }
    /// <summary>The middle of the feet.</summary>
    public Vec Pos { get; set; }
    public Vec Vel { get; set; }
    /// <summary>+1 facing right, −1 facing left.</summary>
    public int Facing { get; set; } = 1;
    public Ledge? Standing { get; set; }
    public bool Skidding { get; set; }
    public double RunPhase { get; set; }
    /// <summary>Seconds since the last flap (the wings come down for a moment after one).</summary>
    public double SinceFlap { get; set; } = 1;
    /// <summary>Still appearing on a spawn pad: can't be touched and can't be hurt.</summary>
    public double Materializing { get; set; }
    /// <summary>A player safe after appearing, until they move or a few seconds pass.</summary>
    public double Shield { get; set; }
    public bool Dead { get; set; }
    /// <summary>Held by the lava troll's hand.</summary>
    public bool Grabbed { get; set; }
    public double GrabTime { get; set; }

    // Enemy brains.
    public double TargetY { get; set; } = 100;
    public double Think { get; set; }
    public int Wander { get; set; } = 1;

    public bool IsPlayer => Kind is RiderKind.Player1 or RiderKind.Player2;
    public int PlayerIndex => Kind == RiderKind.Player2 ? 1 : 0;
    public bool OnGround => Standing is not null;
    public bool Solid => !Dead && Materializing <= 0;
    public bool Safe => Materializing > 0 || Shield > 0;
    /// <summary>The top of the head, for bumping into ledge undersides and the ceiling.</summary>
    public double Top => Pos.Y - BodyHeight;

    /// <summary>Points for unseating this rider.</summary>
    public int Points => Kind switch
    {
        RiderKind.Bounder => 500,
        RiderKind.Hunter => 750,
        RiderKind.ShadowLord => 1500,
        _ => 3000, // the other player: the gladiator's prize
    };
}

/// <summary>What an unseated enemy leaves: an egg that hatches if it's left too long.</summary>
public sealed class Egg
{
    public Vec Pos { get; set; }
    public Vec Vel { get; set; }
    public bool Resting { get; set; }
    /// <summary>The kind of rider it came from (a hatchling comes back one kind tougher).</summary>
    public RiderKind From { get; init; }
    public double Age { get; set; }
    public double HatchAt { get; init; } = 8;
    /// <summary>Hatched: a little knight stands waiting for a buzzard.</summary>
    public bool Hatched { get; set; }
    public double HatchedFor { get; set; }
    public bool Collected { get; set; }
    public bool Burnt { get; set; }

    public RiderKind HatchesInto => From switch
    {
        RiderKind.Bounder => RiderKind.Hunter,
        _ => RiderKind.ShadowLord,
    };
}

/// <summary>A riderless buzzard flying in to pick up a hatchling.</summary>
public sealed class Buzzard
{
    public Vec Pos { get; set; }
    public required Egg For { get; init; }
    public int Facing { get; set; } = 1;
    public bool Done { get; set; }
}

/// <summary>The pterodactyl: deadly, unless your lance meets its open mouth.</summary>
public sealed class Pterodactyl
{
    public Vec Pos { get; set; }
    public Vec Vel { get; set; }
    public int Facing { get; set; } = 1;
    public double Clock { get; set; }
    public bool Dead { get; set; }
    /// <summary>The mouth opens for a moment, over and over.</summary>
    public bool MouthOpen => Clock % 1.0 < 0.6;
    /// <summary>Where its head is (the only place a lance can kill it).</summary>
    public Vec Head => new(Pos.X + Facing * 12, Pos.Y - 2);
}

public enum EffectKind { Puff, Score, Sparkle, Splash }

public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
    public double Life { get; init; } = 0.6;
    public string Text { get; init; } = "";
    public bool Done => Age >= Life;
}

/// <summary>One player's controls for a step.</summary>
public record struct Controls(bool Left, bool Right, bool Flap);
