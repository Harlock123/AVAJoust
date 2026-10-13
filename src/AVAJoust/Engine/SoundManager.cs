using AVAJoust.Audio;

namespace AVAJoust.Engine;

/// <summary>
/// Plays the game's sounds through OpenAL Soft, which mixes as many at once as the game needs. The
/// title tune and the wave tunes play on the music channel. In a
/// virtual machine a larger mixing buffer keeps the sound from breaking up.
/// </summary>
public sealed class SoundManager : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly string _folder;

    public SoundManager(bool silent = false)
    {
        _folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        _engine = silent
            ? new NullAudioEngine("silent")
            : OpenAlAudioEngine.CreateOrSilent(AudioBuffer.PeriodFrames(AudioBuffer.Automatic, VirtualMachine.Detect()));
        _engine.SetVolumes(0.9f, 0.8f, 0.7f);
        _engine.SetAmbienceVolume(0.45f);
    }

    public bool IsAvailable => _engine.IsAvailable;

    public bool IsMuted
    {
        get => _muted;
        set
        {
            _muted = value;
            if (value) StopAll();
        }
    }
    private bool _muted;

    public void Play(Sfx sfx)
    {
        if (_muted) return;
        switch (sfx)
        {
            case Sfx.TitleTune: _engine.PlayMusic(Path.Combine(_folder, "TitleTune.wav"), loop: true); return;
            case Sfx.WaveStart: _engine.PlayMusic(Path.Combine(_folder, "WaveStart.wav"), loop: false); return;
            case Sfx.GameOver: _engine.PlayMusic(Path.Combine(_folder, "GameOver.wav"), loop: false); return;
            case Sfx.Bonus: _engine.PlayMusic(Path.Combine(_folder, "Bonus.wav"), loop: false); return;
        }
        string file = sfx switch
        {
            Sfx.Flap => "Flap.wav",
            Sfx.Skid => "Skid.wav",
            Sfx.Bounce => "Bounce.wav",
            Sfx.Unseat => "Unseat.wav",
            Sfx.PlayerDie => "PlayerDie.wav",
            Sfx.Egg => "Egg.wav",
            Sfx.Hatch => "Hatch.wav",
            Sfx.Materialize => "Materialize.wav",
            Sfx.Screech => "Screech.wav",
            Sfx.PteroDie => "PteroDie.wav",
            Sfx.Sizzle => "Sizzle.wav",
            Sfx.Grab => "Grab.wav",
            Sfx.ExtraLife => "ExtraLife.wav",
            Sfx.BridgeBurns => "BridgeBurns.wav",
            _ => "BuzzardCall.wav",
        };
        _engine.PlayEffect(Path.Combine(_folder, file), sfx == Sfx.Flap ? 0.55f : 1f);
    }

    public void StopAll()
    {
        _engine.StopAmbience();
        _engine.StopMusic();
    }

    public void Dispose() => _engine.Dispose();
}
