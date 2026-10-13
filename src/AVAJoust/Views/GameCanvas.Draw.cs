using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAJoust.Engine;
using AVAJoust.Models;

namespace AVAJoust.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAJoust/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Yellow = Brush(0xFFFFD84A);
    private static readonly IBrush Red = Brush(0xFFFF4030);
    private static readonly IBrush Cyan = Brush(0xFF4FD7FF);
    private static readonly IBrush Dim = Brush(0xFF8A8A96);

    // Each rider's colours: bird, wing, knight, lance, feet.
    public static Sprites.Livery LiveryOf(RiderKind k) => k switch
    {
        RiderKind.Player1 => new(0xFFFFCC33, 0xFFD89A1A, 0xFFEDEDED, 0xFFFFFFFF, 0xFFFF8A1E),
        RiderKind.Player2 => new(0xFFE6F2FF, 0xFF9CC4FF, 0xFF4FD7FF, 0xFFFFFFFF, 0xFFFF8A1E),
        RiderKind.Bounder => new(0xFF3FA34D, 0xFF2A7A36, 0xFFE8342A, 0xFFF4F0E6, 0xFFFFD84A),
        RiderKind.Hunter => new(0xFF3FA34D, 0xFF2A7A36, 0xFFC9C9D6, 0xFFF4F0E6, 0xFFFFD84A),
        _ => new(0xFF3FA34D, 0xFF2A7A36, 0xFF3C6BFF, 0xFFF4F0E6, 0xFFFFD84A),
    };

    /// <summary>A buzzard with nobody on it.</summary>
    private static readonly Sprites.Livery Riderless = new(0xFF3FA34D, 0xFF2A7A36, 0, 0, 0xFFFFD84A);

    private static Point P(double x, double y) => new(x * Scale, y * Scale);

    private void DrawScreen(DrawingContext dc)
    {
        switch (Engine.State)
        {
            case GameState.Title:
                DrawArena(dc);
                DrawTitle(dc);
                break;
            case GameState.HighScores:
                DrawArena(dc);
                DrawHighScores(dc);
                break;
            default:
                DrawArena(dc);
                DrawPlay(dc);
                break;
        }
        DrawScores(dc);
    }

    private double Clock => _clock;

    /// <summary>The rock ledges, the ground and its bridges, and the lava.</summary>
    private void DrawArena(DrawingContext dc)
    {
        var g = Engine;
        // Lava under the bridges (once they're gone it shows).
        foreach (var (left, right) in new[] { (0.0, Arena.GroundLeft), (Arena.GroundRight, Arena.Width) })
        {
            for (double x = left; x < right; x += 2)
            {
                double wave = Math.Sin(x * 0.25 + Clock * 3) * 1.5;
                var top = P(x, Arena.LavaY + wave);
                dc.FillRectangle(Brush(0xFFE8341A), new Rect(top.X, top.Y, 2 * Scale + 0.5, (Arena.Height - Arena.LavaY - wave) * Scale));
                dc.FillRectangle(Brush(0xFFFFA41E), new Rect(top.X, top.Y, 2 * Scale + 0.5, Scale * 1.2));
            }
        }
        foreach (var ledge in g.Ledges)
        {
            if (ledge.Thickness > 10) DrawGround(dc, ledge);
            else DrawLedge(dc, ledge);
        }
        // The bridges burning.
        if (g.BridgeFire > 0)
        {
            foreach (var (left, right) in new[] { (0.0, Arena.GroundLeft), (Arena.GroundRight, Arena.Width) })
                for (double x = left; x < right; x += 6)
                {
                    double h = 6 + Math.Abs(Math.Sin(x * 0.7 + Clock * 13)) * 10;
                    var a = P(x, Arena.GroundY - h);
                    dc.FillRectangle(Brush(0xFFFF8A1E), new Rect(a.X, a.Y, 4 * Scale, h * Scale));
                    dc.FillRectangle(Brush(0xFFFFE14A), new Rect(a.X + Scale, a.Y + h * Scale * 0.4, 2 * Scale, h * Scale * 0.6));
                }
        }
        // The spawn pads glow faintly.
        foreach (var pad in Arena.Pads)
        {
            var a = P(pad.X - 12, pad.Y);
            dc.FillRectangle(Brush(0x6060C0FF), new Rect(a.X, a.Y, 24 * Scale, Scale));
        }
    }

    private static void DrawLedge(DrawingContext dc, Ledge l)
    {
        // Wrap: draw the parts that fall off either edge on the other side too.
        foreach (double shift in new[] { 0.0, -Arena.Width, Arena.Width })
        {
            double left = Math.Max(0, l.Left + shift), right = Math.Min(Arena.Width, l.Right + shift);
            if (right <= left) continue;
            var a = P(left, l.Top);
            double w = (right - left) * Scale;
            dc.FillRectangle(Brush(0xFF8A5A30), new Rect(a.X, a.Y, w, l.Thickness * Scale));
            dc.FillRectangle(Brush(0xFFC08A4E), new Rect(a.X, a.Y, w, Scale * 1.2));
            // A ragged rock underside.
            for (double x = left; x < right; x += 4)
            {
                double drop = 1 + Math.Abs(Math.Sin(x * 1.7 + l.Top)) * 3;
                var b = P(x, l.Bottom);
                dc.FillRectangle(Brush(0xFF6A4224), new Rect(b.X, b.Y - Scale, 3 * Scale, drop * Scale));
            }
        }
    }

    private static void DrawGround(DrawingContext dc, Ledge l)
    {
        double left = Math.Max(0, l.Left), right = Math.Min(Arena.Width, l.Right);
        var a = P(left, l.Top);
        dc.FillRectangle(Brush(0xFF6A4224), new Rect(a.X, a.Y, (right - left) * Scale, (Arena.Height - l.Top) * Scale));
        dc.FillRectangle(Brush(0xFFC08A4E), new Rect(a.X, a.Y, (right - left) * Scale, Scale * 1.4));
        dc.FillRectangle(Brush(0xFF8A5A30), new Rect(a.X, a.Y + Scale * 1.4, (right - left) * Scale, Scale * 3));
    }

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        foreach (var egg in g.Eggs)
        {
            if (egg.Hatched)
            {
                var frames = Sprites.Hatchling;
                DrawSprite(dc, frames[(int)(Clock * 3) % 2], egg.Pos, 1, LiveryOf(egg.HatchesInto));
            }
            else DrawSprite(dc, Sprites.Egg, egg.Pos, 1, LiveryOf(RiderKind.Bounder));
        }
        foreach (var b in g.Buzzards)
            DrawSprite(dc, (int)(Clock * 8) % 2 == 0 ? Sprites.WingsUp : Sprites.WingsDown, b.Pos, b.Facing, Riderless);

        foreach (var r in g.AllRiders())
        {
            if (r.Grabbed)
            {
                // The troll's hand, reaching out of the lava.
                var hand = Sprites.Hand;
                DrawSprite(dc, hand, new Vec(r.Pos.X, Math.Min(Arena.LavaY + 6, r.Pos.Y + hand.Height)), 1, LiveryOf(r.Kind));
            }
            DrawRider(dc, r);
        }

        foreach (var p in g.Pterodactyls)
            DrawSprite(dc, Sprites.Pterodactyl[p.MouthOpen ? 1 : 0], p.Pos + new Vec(0, 5), p.Facing, LiveryOf(RiderKind.Bounder));

        foreach (var fx in g.Effects)
        {
            double t = fx.Age / fx.Life;
            switch (fx.Kind)
            {
                case EffectKind.Puff:
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4, d = 4 + t * 14;
                        var at = P(fx.Pos.X + Math.Cos(a) * d, fx.Pos.Y + Math.Sin(a) * d);
                        dc.FillRectangle(Brush(t < 0.5 ? 0xFFFFFFFFu : 0xFFFFD84Au), new Rect(at.X, at.Y, Scale * 1.5, Scale * 1.5));
                    }
                    break;
                case EffectKind.Splash:
                    for (int i = 0; i < 6; i++)
                    {
                        double x = fx.Pos.X + (i - 2.5) * 3;
                        double y = fx.Pos.Y - Math.Sin(t * Math.PI) * (6 + i % 3 * 4);
                        var at = P(x, y);
                        dc.FillRectangle(Brush(0xFFFFA41E), new Rect(at.X, at.Y, Scale * 1.5, Scale * 1.5));
                    }
                    break;
                case EffectKind.Score:
                    Text(dc, fx.Text, P(fx.Pos.X, fx.Pos.Y - t * 8), 26, Yellow, center: true);
                    break;
            }
        }

        if (g.Banner.Length > 0)
            Text(dc, g.Banner, P(Arena.Width / 2, 90), 56, Yellow, center: true);
        if (g.Banner2.Length > 0)
            Text(dc, g.Banner2, P(Arena.Width / 2, 104), 34, Cyan, center: true);

        switch (g.State)
        {
            case GameState.Paused:
                Text(dc, "PAUSED", P(Arena.Width / 2, 80), 60, White, center: true);
                Text(dc, "ESC to quit, any other key to resume", P(Arena.Width / 2, 96), 28, Dim, center: true);
                break;
            case GameState.Attract:
                Text(dc, "DEMONSTRATION", P(Arena.Width / 2, 186), 30, Red, center: true);
                if (Clock % 1 < 0.6) Text(dc, "PRESS 1 OR 2 TO PLAY", P(Arena.Width / 2, 196), 28, White, center: true);
                break;
            case GameState.EnterInitials:
                DrawInitials(dc);
                break;
        }
    }

    private void DrawRider(DrawingContext dc, Rider r)
    {
        Sprites.Sprite sprite;
        if (r.OnGround)
        {
            if (r.Skidding) sprite = Sprites.Skid;
            else if (Math.Abs(r.Vel.X) > 5) sprite = Sprites.Run[(int)r.RunPhase % 3];
            else sprite = Sprites.Stand;
        }
        else sprite = r.SinceFlap < 0.14 ? Sprites.WingsDown : Sprites.WingsUp;

        // Appearing on a pad: the rider builds up from the feet, flickering.
        int reveal = sprite.Height;
        if (r.Materializing > 0)
            reveal = (int)(sprite.Height * (1 - r.Materializing / 1.2));
        bool flicker = r.Shield > 0 && (int)(Clock * 12) % 2 == 0;
        if (flicker) return;
        DrawSprite(dc, sprite, r.Pos, r.Facing, LiveryOf(r.Kind), reveal);
    }

    /// <summary>
    /// Draws a sprite standing on a point (its bottom centre), facing right (1) or left (−1). Riders'
    /// stand-in letters take their livery's colours. Sprites crossing the edge are drawn on both sides.
    /// </summary>
    private static void DrawSprite(DrawingContext dc, Sprites.Sprite s, Vec feet, int facing, Sprites.Livery livery, int rows = int.MaxValue)
    {
        foreach (double shift in new[] { 0.0, -Arena.Width, Arena.Width })
        {
            double left = feet.X + shift - s.Width / 2.0;
            if (left > Arena.Width || left + s.Width < 0) continue;
            double top = feet.Y - s.Height;
            for (int y = Math.Max(0, s.Height - rows); y < s.Height; y++)
            {
                var row = s.Rows[y];
                for (int x = 0; x < s.Width; x++)
                {
                    char ch = row[facing >= 0 ? x : s.Width - 1 - x];
                    if (ch == '.') continue;
                    uint c = ch switch
                    {
                        'B' => livery.Bird,
                        'W' => livery.Wing,
                        'K' => livery.Knight,
                        'L' => livery.Lance,
                        'F' => livery.Feet,
                        _ => Sprites.Fixed.GetValueOrDefault(ch, 0xFFFF00FF),
                    };
                    if (c == 0) continue;
                    var a = P(left + x, top + y);
                    dc.FillRectangle(Brush(c), new Rect(a.X, a.Y, Scale + 0.3, Scale + 0.3));
                }
            }
        }
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
