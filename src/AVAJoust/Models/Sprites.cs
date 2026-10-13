namespace AVAJoust.Models;

/// <summary>
/// The game's pixel art, this game's own drawings: grids of letters drawn facing right and mirrored
/// to face left. A rider's sprites use stand-in letters (B bird, W wing, K knight, L lance, F feet
/// and beak) that each rider's colours fill in, so one drawing serves every rider.
/// </summary>
public static class Sprites
{
    public sealed record Sprite(string[] Rows)
    {
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;
    }

    private static Sprite S(params string[] rows)
    {
        if (rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException("Sprite rows must be the same width: " + string.Join("|", rows));
        return new Sprite(rows);
    }

    /// <summary>Fixed colours for the letters that aren't a rider's own.</summary>
    public static readonly IReadOnlyDictionary<char, uint> Fixed = new Dictionary<char, uint>
    {
        ['w'] = 0xFFF4F0E6, // white
        ['g'] = 0xFF9A9AA6, // grey
        ['d'] = 0xFF55555F, // dark grey
        ['r'] = 0xFFE8342A, // red
        ['o'] = 0xFFFF8A1E, // orange
        ['y'] = 0xFFFFD84A, // yellow
        ['c'] = 0xFF4FD7FF, // cyan
        ['p'] = 0xFFB070E8, // purple
        ['n'] = 0xFF7A4A2A, // brown
        ['e'] = 0xFF2E2E36, // near black
    };

    /// <summary>A rider's colours for the stand-in letters.</summary>
    public sealed record Livery(uint Bird, uint Wing, uint Knight, uint Lance, uint Feet);

    // ----- riders: 18 wide, 16 high, facing right -------------------------------------------------

    public static readonly Sprite WingsUp = S(
        "..................",
        ".........KKK......",
        "..WW....KKKKK.....",
        ".WWWW...KeKeK.....",
        "WWWWWW...KKK......",
        ".WWWWWW.KKKKK.....",
        "...WWWWWKKKKKLLLLL",
        ".....BBBBKKK...BB.",
        "....BBBBBBBB..BBBF",
        "...BBBBBBBBBBBBB..",
        "..BBBBBBBBBBBB....",
        "...BBBBBBBBBB.....",
        ".....BBBBBB.......",
        "......F..F........",
        ".....F...F........",
        ".....F....F.......",
        "..................");

    public static readonly Sprite WingsDown = S(
        "..................",
        ".........KKK......",
        "........KKKKK.....",
        "........KeKeK.....",
        ".........KKK......",
        "........KKKKK.....",
        "........KKKKKLLLLL",
        ".....BBBBKKK...BB.",
        "....BBBBBBBB..BBBF",
        "...BBBBBBBBBBBBB..",
        ".WWWBBBBBBBBBB....",
        "WWWWWWBBBBBBB.....",
        ".WWWWWWBBBB.......",
        "...WWW.F..F.......",
        ".......F...F......",
        ".......F....F.....",
        "..................");

    // Running: wings folded, legs striding.
    private static readonly string[] RunTop =
    [
        ".........KKK......",
        "........KKKKK.....",
        "........KeKeK.....",
        ".........KKK......",
        "........KKKKK.....",
        "........KKKKKLLLLL",
        ".....BBBBKKK...BB.",
        "....BBWWWWBB..BBBF",
        "...BBBWWWWWBBBBB..",
        "..BBBBBWWWBBBB....",
        "...BBBBBBBBBB.....",
        ".....BBBBBB.......",
    ];

    public static readonly Sprite[] Run =
    [
        S([.. RunTop, "......F..F........", ".....F....F.......", "....F......F......", "...FF......FF.....", ".................."]),
        S([.. RunTop, "......F..F........", "......F..F........", "......F..F........", ".....FF..FF.......", ".................."]),
        S([.. RunTop, ".......FF.........", "......F.F.........", ".....F...F........", "....FF....FF......", ".................."]),
    ];

    /// <summary>Standing, or skidding to a stop (leaning back, feet forward).</summary>
    public static readonly Sprite Stand = S([.. RunTop, "......F..F........", "......F..F........", "......F..F........", ".....FF.FF........", ".................."]);

    public static readonly Sprite Skid = S([.. RunTop, ".......F..F.......", "........F..F......", ".........F..F.....", ".........FF.FF....", ".................."]);

    // ----- the rest ---------------------------------------------------------------------------

    public static readonly Sprite Egg = S(
        "..ww..",
        ".wwww.",
        "wwgwww",
        "wwwwgw",
        "wgwwww",
        ".wwww.");

    /// <summary>A hatchling knight, unmounted, waiting for a bird.</summary>
    public static readonly Sprite[] Hatchling =
    [
        S(
            "..KK..",
            ".KKKK.",
            ".KKKK.",
            "..KK..",
            ".KKKK.",
            "K.KK.K",
            "..KK..",
            ".K..K.",
            ".K..K."),
        S(
            "..KK..",
            ".KKKK.",
            ".KKKK.",
            "..KK..",
            "KKKKKK",
            "..KK..",
            "..KK..",
            ".K..K.",
            "K....K"),
    ];

    /// <summary>The pterodactyl: wings up and mouth shut, wings down and mouth open.</summary>
    public static readonly Sprite[] Pterodactyl =
    [
        S(
            "....pp......................",
            "....ppp.....................",
            ".....pppp...................",
            "......ppppp.................",
            ".......pppppp..........ppp..",
            "........ppppppp.......pppppy",
            "..pppppppppppppppppppppppppr",
            "....pppppppppppppppppppp.rrr",
            "......ppp......ppp..........",
            ".....pp.........pp.........."),
        S(
            "............................",
            "......................ppp...",
            ".....................ppppppy",
            "..pppppppppppppppppppppp..rr",
            "....pppppppppppppppppppp....",
            ".......pppppp.pppppp.....rrr",
            "......ppppp.....ppppp.......",
            ".....pppp.........pppp......",
            "....ppp.............ppp.....",
            "...pp.................pp...."),
    ];

    /// <summary>The lava troll's hand reaching up.</summary>
    public static readonly Sprite Hand = S(
        "o.o.o.o.",
        "o.o.o.o.",
        "oooooooo",
        "oooooooo",
        ".oooooo.",
        "..oooo..",
        "..oooo..",
        "..oooo..",
        "..oooo..",
        "..oooo..");

    /// <summary>Reserve riders in the score strip.</summary>
    public static readonly Sprite LifeIcon = S(
        "...KK.",
        "..KKKK",
        "BBBKK.",
        "BBBBBB",
        ".F..F.");
}
