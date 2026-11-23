# Tense, driving chip loops with three intensities (low = desolate, normal, high = popular), 2026-11-22.
# Original material in a "battle / decision" mood: minor keys, octave bass, backbeat, 16th arpeggios.
# All three intensities share bar count and parts, so the game can switch between them in time (same position in the
# loop). Normal and high share a tempo; low may differ (2026-11-22: choosing/cooking normal+high slowed to city's pace).
import numpy as np, subprocess, sys
import os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from synth import SR, note_hz, env, place, reverb, master, rng
from styles import pulse, triangle, chip_kick, chip_noise, soft_pad, write

def snare():
    n = int(.16 * SR); t = np.arange(n) / SR
    tone = np.sin(2 * np.pi * 190 * t) * np.exp(-t * 40)
    return (chip_noise(.16, 22)[:n] * .8 + tone * .5)

CHORD = {'m': [0, 3, 7], 'maj': [0, 4, 7], 'sus': [0, 5, 7], 'm7': [0, 3, 7, 10], 'dim': [0, 3, 6]}

def track(bpm, bars, prog, lead, counter, level, seed):
    beat = 60 / bpm; bar = 4 * beat; s16 = beat / 4; L = int(bars * bar * SR)
    buf = np.zeros((L, 2))
    for b in range(bars):
        root, q = prog[b % len(prog)]; t0 = b * bar; tones = [root + i for i in CHORD[q]]
        phrase_end = b % 4 == 3
        # Bass: desolate = one long low note; otherwise driving octave eighths with a syncopated push.
        if level == 0:
            place(buf, triangle(note_hz(root - 24), bar * .95), t0, .36)
        else:
            for i in range(8):
                n = root - 24 + (12 if i % 2 else 0)
                if i == 7 and phrase_end: n = prog[(b + 1) % len(prog)][0] - 24 + 1
                place(buf, triangle(note_hz(n), beat / 2 * .9), t0 + i * beat / 2, .34)
                place(buf, pulse(note_hz(n), beat / 2 * .6, .5, 5), t0 + i * beat / 2, .07)
        # Arpeggio: 16ths over the chord (thin pulse). Low: none. High: an octave layer too.
        if level >= 1:
            pattern = [0, 1, 2, 1, 2, 0, 1, 2, 0, 2, 1, 2, 0, 1, 2, 1]
            for i, k in enumerate(pattern):
                place(buf, pulse(note_hz(tones[k] + 12), s16 * .8, .125, 8), t0 + i * s16, .055, -.35)
                if level == 2: place(buf, pulse(note_hz(tones[k] + 24), s16 * .6, .125, 6), t0 + i * s16, .03, .35)
        else:
            place(buf, soft_pad([note_hz(x + 12) for x in tones], bar * 1.05), t0, .10)
        # Lead: the motif (two bars per entry). Low: only a few lonely notes of it, softer.
        line = lead[(b // 2) % len(lead)][b % 2]
        second_half = b >= bars // 2
        if not (second_half and b - bars // 2 < 2):        # B section: the lead rests two bars, then returns higher
            up = 12 if second_half and level >= 1 else 0
            for j, (step, at16, len16) in enumerate(line):
                if level == 0 and j % 3: continue
                place(buf, pulse(note_hz(72 + step + up), len16 * s16 * .95, .25, 12 if not up else 8), t0 + at16 * s16,
                      .11 if level == 0 else (.19 if not up else .14), .1)
        if level == 2:   # counter-line answers the lead
            for (step, at16, len16) in counter[(b // 2) % len(counter)][b % 2]:
                place(buf, pulse(note_hz(60 + step), len16 * s16 * .9, .5, 8), t0 + at16 * s16, .085, -.2)
        # Drums.
        if level == 0:
            place(buf, chip_kick(), t0, .26)
            if b % 2: place(buf, chip_noise(.4, 6), t0 + 2 * beat, .025, .2)   # distant wash
        else:
            for i in range(16):
                if i in (0, 8) or (i in (6, 14) and b % 2) or (level == 2 and i in (3, 10)): place(buf, chip_kick(), t0 + i * s16, .32)
                if i in (4, 12): place(buf, snare(), t0 + i * s16, .20)
                if level == 2 or i % 2 == 0: place(buf, chip_noise(.03, 150), t0 + i * s16, .045 if i % 4 else .07, .25)
            if level == 2 and b % 4 == 0: place(buf, chip_noise(.9, 4), t0, .06, -.2)    # crash on phrase start
            if phrase_end and level >= 1:
                for i in (13, 14, 15): place(buf, snare(), t0 + i * s16, .14)            # fill
    buf = reverb(buf, seconds=1.0, mix=.12)
    return buf

def finish(parts):
    # Desolate sits quieter; normal and high share loudness so popularity changes energy, not volume.
    return [master(parts[0], -7.0), master(parts[1], -3.0), master(parts[2], -3.0)]

D, E, F, G, A, Bb, C = 50, 52, 53, 55, 57, 58, 48
# Lead motifs: (semitones above C5, start 16th, length in 16ths); each entry is two bars.
choose_lead = [
    [[(4, 0, 2), (7, 2, 2), (11, 4, 4), (12, 8, 2), (11, 10, 2), (7, 12, 4)], [(9, 0, 3), (7, 3, 3), (4, 6, 2), (2, 8, 4), (4, 12, 4)]],
    [[(4, 0, 2), (7, 2, 2), (11, 4, 4), (14, 8, 4), (12, 12, 2), (11, 14, 2)], [(12, 0, 6), (11, 6, 2), (7, 8, 8)]]]
choose_counter = [[[(-5, 8, 4), (-3, 12, 4)], [(0, 0, 8), (-1, 8, 8)]], [[(-5, 8, 4), (2, 12, 4)], [(0, 0, 12)]]]
cook_lead = [
    [[(9, 0, 3), (12, 3, 1), (9, 4, 2), (7, 6, 2), (4, 8, 4), (7, 12, 2), (9, 14, 2)], [(12, 0, 2), (14, 2, 2), (12, 4, 4), (9, 8, 8)]],
    [[(9, 0, 3), (12, 3, 1), (9, 4, 2), (7, 6, 2), (4, 8, 2), (2, 10, 2), (4, 12, 4)], [(0, 0, 4), (2, 4, 2), (4, 6, 2), (9, 8, 8)]]]
cook_counter = [[[(0, 0, 4), (-3, 8, 8)], [(4, 0, 8), (2, 8, 8)]], [[(0, 0, 4), (-3, 8, 8)], [(-1, 0, 8), (-3, 8, 8)]]]
city_lead = [
    [[(2, 0, 4), (5, 4, 2), (9, 6, 6), (7, 12, 2), (5, 14, 2)], [(7, 0, 4), (5, 4, 2), (2, 6, 2), (0, 8, 4), (2, 12, 4)]],
    [[(2, 0, 4), (5, 4, 2), (9, 6, 6), (12, 12, 4)], [(10, 0, 4), (9, 4, 4), (7, 8, 4), (9, 12, 4)]]]
city_counter = [[[(-3, 8, 8)], [(-5, 0, 8), (-7, 8, 8)]], [[(-3, 8, 8)], [(-2, 0, 16)]]]
songs = {
    # E minor with a harmonic-minor turn (B major): urgent "decide now" energy.
    'choosing': ((144, 128), 16, [(E, 'm'), (C, 'maj'), (A - 12, 'm'), (59 - 12, 'maj')], choose_lead, choose_counter),
    # A minor, funky push: customers are waiting.
    'cooking': ((136, 128), 16, [(A - 12, 'm'), (F, 'maj'), (C, 'maj'), (G, 'maj')], cook_lead, cook_counter),
    # D dorian: determined, a bit bittersweet, still moving forward.
    'city': ((128, 128), 16, [(D, 'm'), (G, 'maj'), (Bb - 12, 'maj'), (C, 'maj')], city_lead, city_counter),
}
if __name__ == '__main__':
    # python tension.py <out dir> [--backing]   (--backing: no lead or counter-line; this is what the game uses)
    out = sys.argv[1]; backing = '--backing' in sys.argv
    for name, ((low_bpm, bpm), bars, prog, lead, counter) in songs.items():
        if backing: lead = counter = [[[], []]]
        levels = [track(low_bpm if lv == 0 else bpm, bars, prog, lead, counter, lv, 10) for lv in (0, 1, 2)]
        levels = finish(levels)
        for lv, audio in zip(('low', 'normal', 'high'), levels): write(audio, f'{out}/{name}_{lv}.ogg')
        # Listening reel: low -> normal -> high, each once, with short crossfades.
        xf = int(.8 * SR); reel = levels[0]
        for nxt in levels[1:]:
            ramp = np.linspace(0, 1, xf)[:, None]
            reel = np.concatenate([reel[:-xf], reel[-xf:] * (1 - ramp) + nxt[:xf] * ramp, nxt[xf:]])
        write(reel, f'{out}/{name}_reel_low-normal-high.ogg')
        print(name, f'{len(levels[0]) / SR:.1f}s per level')
