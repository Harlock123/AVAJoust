namespace AVAJoust.Models;

/// <summary>A 2D point or vector (y grows downwards).</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

/// <summary>A ledge: a flat top to land and run on, and a rock underside to bump into.</summary>
public sealed record Ledge(double Left, double Right, double Top, double Thickness = 6)
{
    public double Bottom => Top + Thickness;
    public double Width => Right - Left;
    /// <summary>Is x over this ledge? (Ledges at the edges reach round the wrap.)</summary>
    public bool Spans(double x) => In(x) || In(x + Arena.Width) || In(x - Arena.Width);

    private bool In(double x) => x >= Left && x <= Right;
}

/// <summary>
/// The arena: a 320×240 screen that wraps left to right, with rock ledges, four spawn pads where
/// riders appear, the ground in the middle with a bridge either side over the lava, and the score
/// strip along the bottom.
/// </summary>
public static class Arena
{
    public const double Width = 320;
    public const double Height = 240;
    /// <summary>The ground's top, and the lava's surface below it once the bridges burn.</summary>
    public const double GroundY = 206;
    public const double LavaY = 222;
    /// <summary>The middle of the ground, which never burns.</summary>
    public const double GroundLeft = 68;
    public const double GroundRight = 252;
    public const double Ceiling = 6;
    public const double Step = 1.0 / 60;

    /// <summary>The floating ledges. Those at the edges wrap round to the other side.</summary>
    public static readonly IReadOnlyList<Ledge> Floating =
    [
        new(-10, 52, 52),          // top left (wraps)
        new(272, 330, 52),         // top right (wraps)
        new(112, 196, 66),         // top middle
        new(-10, 76, 116),         // middle left (wraps)
        new(140, 212, 128),        // centre
        new(244, 330, 116),        // middle right (wraps, meeting the middle left)
        new(84, 140, 168),         // lower left
        new(216, 268, 160),        // lower right
    ];

    /// <summary>The ground: the middle, plus the bridges either side until they burn.</summary>
    public static IReadOnlyList<Ledge> Ground(bool bridges) => bridges
        ? [new(-10, 330, GroundY, 34)]
        : [new(GroundLeft, GroundRight, GroundY, 34)];

    /// <summary>Where riders appear: on the ground, the top middle, and the left and right ledges.</summary>
    public static readonly IReadOnlyList<Vec> Pads =
    [
        new(160, GroundY),
        new(154, 66),
        new(30, 116),
        new(284, 116),
    ];

    public static double Wrap(double x) => ((x % Width) + Width) % Width;

    /// <summary>Shortest horizontal distance from a to b on the wrapping screen.</summary>
    public static double DeltaX(double a, double b)
    {
        double d = b - a;
        if (d > Width / 2) d -= Width;
        if (d < -Width / 2) d += Width;
        return d;
    }
}
