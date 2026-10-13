using Avalonia;
using Avalonia.Media;
using AVAJoust.Engine;
using AVAJoust.Models;

namespace AVAJoust.Views;

public sealed partial class GameCanvas
{
    /// <summary>The scores are carved into the face of the ground: player 1 on the left, player 2 on the right.</summary>
    private void DrawScores(DrawingContext dc)
    {
        var g = Engine;
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.GameOver or GameState.EnterInitials or GameState.Attract;
        for (int i = 0; i < 2; i++)
        {
            bool shown = playing ? i < g.Players : i == 0;
            if (!shown) continue;
            double cx = i == 0 ? 112 : 208;
            var livery = LiveryOf(i == 0 ? RiderKind.Player1 : RiderKind.Player2);
            Text(dc, (playing ? g.Score[i] : 0).ToString("000000"), P(cx, 220), 40, Brush(livery.Bird), center: true);
            int lives = playing ? g.Lives[i] : 0;
            for (int k = 0; k < Math.Min(lives, 7); k++)
                DrawSprite(dc, Sprites.LifeIcon, new Vec(cx - 18 + k * 7, 236), 1, livery);
        }
        Text(dc, $"HIGH {Math.Max(g.HighScores.TopScore, Math.Max(g.Score[0], g.Score[1]))}", P(160, 233), 28, Dim, center: true);
        if (playing && g.Wave > 0) Text(dc, $"WAVE {g.Wave}", P(160, 214), 28, Dim, center: true);
    }

    /// <summary>A dark panel so text reads clearly over the ledges.</summary>
    private static void Panel(DrawingContext dc, double left, double top, double right, double bottom) =>
        dc.FillRectangle(Brush(0xE0000000), new Rect(P(left, top), P(right, bottom)));

    private void DrawTitle(DrawingContext dc)
    {
        Panel(dc, 60, 14, 260, 204);
        Text(dc, "AVAJOUST", P(160, 32), 110, Yellow, center: true);
        Text(dc, "AFTER THE 1982 ARCADE CLASSIC", P(160, 52), 28, Dim, center: true);
        Text(dc, "THE HIGHER LANCE WINS THE JOUST", P(160, 80), 32, White, center: true);
        Row(RiderKind.Bounder, 100, "BOUNDER", 500);
        Row(RiderKind.Hunter, 118, "HUNTER", 750);
        Row(RiderKind.ShadowLord, 136, "SHADOW LORD", 1500);
        DrawSprite(dc, Sprites.Egg, new Vec(106, 150), 1, LiveryOf(RiderKind.Bounder));
        Text(dc, "EGGS  250 - 1000", P(122, 147), 30, White);
        DrawSprite(dc, Sprites.Pterodactyl[1], new Vec(106, 168), -1, LiveryOf(RiderKind.Bounder));
        Text(dc, "PTERODACTYL  1000", P(122, 164), 30, White);
        if (Clock % 1 < 0.65) Text(dc, "PRESS 1 FOR ONE PLAYER, 2 FOR TWO", P(160, 186), 34, White, center: true);
        Text(dc, "P1: ARROWS + SPACE    P2: A D + W", P(160, 198), 26, Dim, center: true);

        void Row(RiderKind kind, double y, string name, int points)
        {
            DrawSprite(dc, Sprites.WingsUp, new Vec(106, y + 6), 1, LiveryOf(kind));
            Text(dc, $"{name}  {points}", P(122, y), 30, White);
        }
    }

    private void DrawHighScores(DrawingContext dc)
    {
        Panel(dc, 80, 14, 240, 202);
        Text(dc, "TODAY'S GREATEST", P(160, 26), 56, Yellow, center: true);
        Text(dc, "RANK   SCORE   NAME   WAVE", P(160, 44), 28, Dim, center: true);
        var table = Engine.HighScores.Entries;
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            bool fresh = i == Engine.NewEntryIndex;
            if (fresh && Clock % 0.5 > 0.35) continue;
            var brush = fresh ? Yellow : i == 0 ? Cyan : White;
            Text(dc, $"{i + 1,2}.  {e.Score,8}   {e.Initials}   {e.Stage,3}", P(160, 58 + i * 13), 34, brush, center: true);
        }
        if (Clock % 1 < 0.65) Text(dc, "PRESS 1 OR 2 TO PLAY", P(160, 194), 32, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var entry = Engine.Initials;
        if (entry is null) return;
        int who = Engine.InitialsFor;
        var color = Brush(LiveryOf(who == 0 ? RiderKind.Player1 : RiderKind.Player2).Bird);
        Panel(dc, 50, 58, 270, 140);
        Text(dc, $"PLAYER {who + 1}: A NEW HIGH SCORE!", P(160, 70), 44, color, center: true);
        Text(dc, $"{Engine.Score[who]}", P(160, 86), 40, White, center: true);
        for (int i = 0; i < HighScoreTable.InitialsLength; i++)
        {
            var at = P(148 + i * 12, 108);
            bool cursor = i == entry.Cursor;
            Text(dc, entry.Initials[i].ToString(), at, 70, cursor ? Yellow : White, center: true);
            if (cursor && Clock % 0.6 < 0.4)
                dc.FillRectangle(Yellow, new Rect(at.X - 16, at.Y + 32, 32, 5));
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE.  ENTER WHEN DONE", P(160, 132), 26, Dim, center: true);
    }
}
