using Avalonia.Input;

namespace AVAJoust.Engine;

/// <summary>
/// Keyboard and gamepads boiled down to each player's three controls: left, right and flap.
/// Player 1: the arrows and Space (or Up), and the first gamepad. Player 2: A, D and W, and the second.
/// </summary>
public sealed class InputHandler : IDisposable
{
    private readonly HashSet<Key> _pressed = [];
    private readonly Dictionary<PhysicalKey, Key> _pressedAs = [];
    private readonly ControllerManager[] _pads = [new(0), new(1)];

    public void OnKeyDown(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        if (physical != PhysicalKey.None)
        {
            if (_pressedAs.TryGetValue(physical, out var was) && was != key) _pressed.Remove(was);
            _pressedAs[physical] = key;
        }
        _pressed.Add(key);
    }
    /// <summary>A key comes up. The key it went down as is let go too: with the keyboard option
    /// shift:both_capslock_cancel, letting go of a Shift arrives as CapsLock, and the Shift would stay held.</summary>
    public void OnKeyUp(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        _pressed.Remove(key);
        if (physical != PhysicalKey.None && _pressedAs.Remove(physical, out var was)) _pressed.Remove(was);
    }

    public bool Has(Key key) => _pressed.Contains(key);

    public void Update()
    {
        foreach (var p in _pads) p.Update();
    }

    private Controls Pad(int i)
    {
        var p = _pads[i];
        return new Controls(
            p.DPadLeft || p.LeftStickX < -0.4f,
            p.DPadRight || p.LeftStickX > 0.4f,
            p.ButtonA || p.ButtonB || p.ButtonX || p.RightTrigger > 0.3f);
    }

    public Controls Player1
    {
        get
        {
            var pad = Pad(0);
            return new Controls(Has(Key.Left) || pad.Left, Has(Key.Right) || pad.Right,
                Has(Key.Space) || Has(Key.Up) || Has(Key.RightCtrl) || pad.Flap);
        }
    }

    public Controls Player2
    {
        get
        {
            var pad = Pad(1);
            return new Controls(Has(Key.A) || pad.Left, Has(Key.D) || pad.Right,
                Has(Key.W) || Has(Key.LeftShift) || Has(Key.LeftCtrl) || pad.Flap);
        }
    }

    /// <summary>1 (or Enter, or Start on pad 1) for one player; 2 (or Start on pad 2) for two.</summary>
    public bool Start1 => Has(Key.D1) || Has(Key.NumPad1) || Has(Key.Enter) || _pads[0].ButtonStart;
    public bool Start2 => Has(Key.D2) || Has(Key.NumPad2) || _pads[1].ButtonStart;

    // Gamepad menu inputs, for initials entry.
    public bool PadUp => _pads.Any(p => p.DPadUp || p.LeftStickY < -0.5f);
    public bool PadDown => _pads.Any(p => p.DPadDown || p.LeftStickY > 0.5f);
    public bool PadLeft => _pads.Any(p => p.DPadLeft || p.LeftStickX < -0.5f);
    public bool PadRight => _pads.Any(p => p.DPadRight || p.LeftStickX > 0.5f);
    public bool PadAccept => _pads.Any(p => p.ButtonA || p.ButtonX || p.ButtonB);
    public bool PadDone => _pads.Any(p => p.ButtonStart);
    public bool PadPause => _pads.Any(p => p.ButtonStart);
    public bool PadBack => _pads.Any(p => p.ButtonBack);

    public void Dispose()
    {
        foreach (var p in _pads) p.Dispose();
    }
}
