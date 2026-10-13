#!/usr/bin/env python3
"""Synthesises AVAJoust's sound effects and tunes into ../src/AVAJoust/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAJoust", "Sounds")
RNG = random.Random(1982)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def main():
    os.makedirs(OUT, exist_ok=True)
    # A wing beat: a soft whump of filtered noise.
    write("Flap.wav", tone(0.11, 260, 120, "noise", attack=0.01, release=1.5), 0.5)
    write("Skid.wav", tone(0.18, 2600, 1400, "noise", release=0.8), 0.35)
    # Lances meeting level: a clang.
    write("Bounce.wav", mix(tone(0.25, 1180, 1160, "tri", release=2.2), tone(0.25, 1770, 1750, "tri", release=2.5, gain=0.5)), 0.6)
    # An enemy unseated: a thump and a falling cry.
    write("Unseat.wav", cat(tone(0.06, 180, 90, "square"), tone(0.3, 900, 220, "pulse", release=1.2)), 0.6)
    write("PlayerDie.wav", mix(tone(0.9, 600, 60, "noise", release=0.8), tone(0.9, 400, 70, "saw", release=1.0, gain=0.4)), 0.8)
    write("Egg.wav", cat(*[tone(0.05, hz(n), kind="pulse", release=0.5) for n in ["C6", "E6", "G6", "C7"]]), 0.45)
    write("Hatch.wav", cat(tone(0.06, 1800, 2400, "pulse"), tone(0.06, 1500, 2100, "pulse"), tone(0.1, 2000, 2600, "pulse")), 0.4)
    # Appearing on a spawn pad: a rising shimmer.
    write("Materialize.wav", tone(0.55, 200, 1600, "pulse", release=0.4, vibrato=0.05, vib_rate=30), 0.4)
    # The pterodactyl's screech: harsh and wavering.
    write("Screech.wav", mix(tone(0.5, 2400, 1700, "saw", release=0.9, vibrato=0.08, vib_rate=26), tone(0.5, 3000, 2000, "noise", release=1.2, gain=0.4)), 0.55)
    write("PteroDie.wav", cat(tone(0.1, 2600, 3000, "saw"), tone(0.6, 1200, 80, "noise", release=0.9)), 0.7)
    write("Sizzle.wav", tone(0.7, 5000, 2500, "noise", attack=0.02, release=0.8), 0.45)
    write("Grab.wav", cat(tone(0.15, 90, 70, "square"), tone(0.15, 80, 60, "square")), 0.6)
    write("ExtraLife.wav", cat(*[tone(0.07, hz(n), kind="square", release=0.4) for n in ["A5", "C#6", "E6", "A6"] * 3]), 0.45)
    write("BridgeBurns.wav", mix(tone(2.0, 700, 300, "noise", attack=0.3, release=0.4), tone(2.0, 90, 60, "saw", attack=0.3, release=0.6, gain=0.3)), 0.6)
    write("BuzzardCall.wav", cat(tone(0.12, 900, 1300, "square", release=0.6), tone(0.16, 1300, 800, "square", release=0.8)), 0.4)

    # A new wave: a bold fanfare (new; in G).
    write("WaveStart.wav", tune(160,
        [("G4", 0.5), ("B4", 0.5), ("D5", 0.5), ("G5", 1.5), ("F#5", 0.5), ("E5", 0.5), ("D5", 1), ("-", 1)],
        [("G2", 1), ("D3", 1), ("G2", 1), ("D3", 1), ("G2", 1), ("-", 1)]), 0.65)
    # A survival bonus.
    write("Bonus.wav", tune(190,
        [("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("E6", 0.5), ("G6", 1), ("C7", 1)],
        [("C3", 1), ("G2", 1), ("C3", 2)], kind="square"), 0.6)
    write("GameOver.wav", tune(92,
        [("E5", 1), ("D5", 1), ("C5", 1), ("B4", 1), ("A4", 2), ("E4", 2)],
        [("A2", 2), ("E2", 2), ("F2", 2), ("A2", 2)], kind="tri"), 0.65)
    print("sounds written to", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
