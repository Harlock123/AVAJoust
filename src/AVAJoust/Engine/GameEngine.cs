using AVAJoust.Models;

namespace AVAJoust.Engine;

public enum GameState { Title, Attract, Playing, Paused, GameOver, EnterInitials, HighScores }

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Flap, Skid, Bounce, Unseat, PlayerDie, Egg, Hatch, Materialize, Screech, PteroDie, Sizzle, Grab,
    WaveStart, Bonus, ExtraLife, GameOver, BridgeBurns, BuzzardCall, TitleTune,
}

/// <summary>
/// The whole game with no UI. Call <see cref="Update"/> with the elapsed time and each player's
/// controls; it runs in fixed 60 Hz steps, so it plays the same at any frame rate and a seed always
/// plays the same game.
/// </summary>
public sealed partial class GameEngine
{
    public const int StartingLives = 5;
    public const int ExtraLifeEvery = 20000;
    public const double TitleIdleSeconds = 15;
    public const double AttractSeconds = 45;
    public const double HighScoreSeconds = 9;

    private readonly Random _rng;
    private double _accumulator;
    private bool _startPrev;
    private readonly bool[] _flapPrev = new bool[2];
    private readonly double[] _flapHeld = new double[2];

    public GameEngine(int seed = 1982, HighScoreTable? table = null)
    {
        _rng = new Random(seed);
        HighScores = table ?? HighScoreTable.Load();
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public double StateTime { get; private set; }
    public HighScoreTable HighScores { get; }
    public int Players { get; private set; } = 1;

    public int[] Score { get; } = new int[2];
    public int[] Lives { get; } = new int[2];
    private readonly int[] _nextExtraLife = new int[2];
    /// <summary>Each player's rider (null between lives, and for player 2 in a one-player game).</summary>
    public Rider?[] Player { get; } = new Rider?[2];
    public bool[] InGame { get; } = new bool[2];

    public List<Rider> Enemies { get; } = [];
    public List<Egg> Eggs { get; } = [];
    public List<Buzzard> Buzzards { get; } = [];
    public List<Pterodactyl> Pterodactyls { get; } = [];
    public List<Effect> Effects { get; } = [];

    public int Wave { get; private set; }
    public double WaveTime { get; private set; }
    public string Banner { get; private set; } = "";
    public string Banner2 { get; private set; } = "";
    public double BannerTime { get; private set; }
    public bool BridgesStanding { get; private set; } = true;
    /// <summary>The bridges catch fire at the start of wave 3 and are gone moments later.</summary>
    public double BridgeFire { get; private set; }
    public bool TrollAwake => Wave >= 4 && !BridgesStanding;

    /// <summary>Players being placed for the high-score table, in turn.</summary>
    public InitialsEntry? Initials { get; private set; }
    public int InitialsFor { get; private set; }
    private readonly Queue<int> _initialsQueue = new();
    public int NewEntryIndex { get; private set; } = -1;

    public AttractPilot? Pilot { get; private set; }
    public bool Silent { get; set; }

    public IReadOnlyList<Ledge> Ledges => [.. Arena.Floating, .. Arena.Ground(BridgesStanding)];

    // The demonstration plays its sounds too, like a game; only Silent turns everything off.
    private void Play(Sfx s)
    {
        if (!Silent) Sound?.Invoke(s);
    }

    /// <summary>The title tune starts on the first step on the title screen (once the host is listening).</summary>
    private bool _titleTunePending = true;

    public void Update(double dt, Controls p1, Controls p2, bool start1, bool start2)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Arena.Step)
        {
            _accumulator -= Arena.Step;
            Step(p1, p2, start1, start2);
        }
    }

    /// <summary>One 1/60 s step. <paramref name="start1"/> starts a one-player game, <paramref name="start2"/> a two-player one.</summary>
    public void Step(Controls p1, Controls p2 = default, bool start1 = false, bool start2 = false)
    {
        double dt = Arena.Step;
        StateTime += dt;
        bool anyStart = start1 || start2;
        bool startPressed = anyStart && !_startPrev;
        _startPrev = anyStart;

        switch (State)
        {
            case GameState.Title:
                if (_titleTunePending)
                {
                    _titleTunePending = false;
                    Play(Sfx.TitleTune);
                }
                if (startPressed || (p1.Flap && StateTime > 0.5)) StartGame(start2 ? 2 : 1);
                else if (StateTime > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Attract:
                Simulate(dt, Pilot!.Decide(this), default);
                if (startPressed) { StartGame(start2 ? 2 : 1); break; }
                if (StateTime > AttractSeconds || State != GameState.Attract) ShowHighScores(-1);
                break;
            case GameState.Playing:
                Simulate(dt, p1, p2);
                break;
            case GameState.GameOver:
                UpdateEffects(dt);
                if (StateTime > 3.5) NextInitials();
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    int place = HighScores.Insert(done.Initials, Score[InitialsFor], Wave);
                    HighScores.Save();
                    NewEntryIndex = place;
                    NextInitials();
                }
                break;
            case GameState.HighScores:
                if (startPressed || (p1.Flap && StateTime > 1)) StartGame(start2 ? 2 : 1);
                else if (StateTime > HighScoreSeconds) ReturnToTitle();
                break;
        }
    }

    public void StartGame(int players)
    {
        Pilot = null;
        Begin(GameState.Playing, players);
    }

    private void StartAttract()
    {
        Pilot = new AttractPilot(_rng.Next());
        Begin(GameState.Attract, 1);
    }

    private void Begin(GameState state, int players)
    {
        State = state;
        StateTime = 0;
        Players = players;
        for (int i = 0; i < 2; i++)
        {
            InGame[i] = i < players;
            Score[i] = 0;
            Lives[i] = i < players ? StartingLives : 0;
            _nextExtraLife[i] = ExtraLifeEvery;
            Player[i] = null;
            _respawn[i] = 0.5 + i * 0.4;
        }
        Wave = 0;
        BridgesStanding = true;
        BridgeFire = 0;
        NewEntryIndex = -1;
        Enemies.Clear();
        Eggs.Clear();
        Buzzards.Clear();
        Pterodactyls.Clear();
        Effects.Clear();
        NextWave();
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        _titleTunePending = true;
        Pilot = null;
        Enemies.Clear();
        Eggs.Clear();
        Buzzards.Clear();
        Pterodactyls.Clear();
        Effects.Clear();
        Player[0] = Player[1] = null;
        Banner = Banner2 = "";
        BridgesStanding = true;
    }

    private void ShowHighScores(int place)
    {
        State = GameState.HighScores;
        StateTime = 0;
        NewEntryIndex = place;
        Pilot = null;
        Enemies.Clear();
        Eggs.Clear();
        Buzzards.Clear();
        Pterodactyls.Clear();
        Player[0] = Player[1] = null;
        Banner = Banner2 = "";
    }

    public void Pause()
    {
        if (State == GameState.Playing) State = GameState.Paused;
    }

    public void Resume()
    {
        if (State == GameState.Paused) State = GameState.Playing;
    }

    private void GameOver()
    {
        if (State == GameState.Attract)
        {
            ShowHighScores(-1);
            return;
        }
        Play(Sfx.GameOver);
        State = GameState.GameOver;
        StateTime = 0;
        Banner = "GAME OVER";
        Banner2 = "";
        _initialsQueue.Clear();
        // The better score enters first.
        foreach (int i in Enumerable.Range(0, Players).OrderByDescending(i => Score[i]))
            if (HighScores.Qualifies(Score[i])) _initialsQueue.Enqueue(i);
    }

    private void NextInitials()
    {
        if (_initialsQueue.Count > 0 && _initialsQueue.Peek() is var who && HighScores.Qualifies(Score[who]))
        {
            _initialsQueue.Dequeue();
            InitialsFor = who;
            Initials = new InitialsEntry(HighScores.LastInitials);
            State = GameState.EnterInitials;
            StateTime = 0;
            return;
        }
        _initialsQueue.Clear();
        Initials = null;
        ShowHighScores(NewEntryIndex);
    }

    internal void AddScore(int player, int points)
    {
        Score[player] += points;
        while (Score[player] >= _nextExtraLife[player])
        {
            Lives[player]++;
            _nextExtraLife[player] += ExtraLifeEvery;
            Play(Sfx.ExtraLife);
        }
    }

    private void UpdateEffects(double dt)
    {
        foreach (var e in Effects) e.Age += dt;
        Effects.RemoveAll(e => e.Done);
    }

    private void Puff(Vec at) => Effects.Add(new Effect { Kind = EffectKind.Puff, Pos = at, Life = 0.5 });

    private void Popup(Vec at, int points) =>
        Effects.Add(new Effect { Kind = EffectKind.Score, Pos = at, Life = 1.2, Text = points.ToString() });

    private void SetBanner(string text, string sub = "", double seconds = 3)
    {
        Banner = text;
        Banner2 = sub;
        BannerTime = seconds;
    }
}
