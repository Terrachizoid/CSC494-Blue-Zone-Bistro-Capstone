# Two style families of original procedural loops (2026-11-22): "ambient" (soft piano, space, no drums; Minecraft-like
# mood) and "chip" (pulse lead, triangle bass, chip drums, swing, jazzy chords; Toby Fox-like mood). Melodies are
# original motifs written here; nothing is transcribed from existing music.
import numpy as np, subprocess, sys
import os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from synth import SR, note_hz, env, place, reverb, master, noise_hit, rng

def piano(freq, dur, vel=1.0):
    n = int(dur * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for k, amp in enumerate([1, .42, .22, .12, .07, .04], 1):
        inh = 1 + .0004 * k * k                          # slight string stiffness
        y += amp * np.sin(2 * np.pi * freq * k * inh * t) * np.exp(-t * (.9 + .7 * k) * (1 + freq / 900))
    hammer = rng.standard_normal(min(n, 400)) * np.exp(-np.arange(min(n, 400)) / 60) * .05
    y[:len(hammer)] += hammer
    return y * vel * env(n, .003, 0, 1, .25, dur)

def soft_pad(freqs, dur):
    n = int(dur * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for f in freqs:
        for det in (-.07, .07):
            y += np.sin(2 * np.pi * f * 2 ** (det / 12) * t + rng.uniform(0, 6.28))
    return y / (2 * len(freqs)) * env(n, dur * .45, 0, 1, dur * .45, dur)

def pulse(freq, dur, duty=.25, bright=14):
    n = int(dur * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for k in range(1, bright + 1):
        if freq * k > 9000: break
        y += (2 / (k * np.pi)) * np.sin(np.pi * k * duty) * np.cos(2 * np.pi * freq * k * t)
    return y * env(n, .004, .08, .7, .04, dur)

def triangle(freq, dur):
    n = int(dur * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for i, k in enumerate(range(1, 16, 2)):
        y += ((-1) ** i) / (k * k) * np.sin(2 * np.pi * freq * k * t)
    return y * .8 * env(n, .003, .05, .85, .03, dur)

def chip_kick():
    n = int(.12 * SR); t = np.arange(n) / SR
    f = 160 * np.exp(-t * 40) + 50
    return np.sign(np.sin(2 * np.pi * np.cumsum(f) / SR)) * np.exp(-t * 30) * .7

def chip_noise(dur, decay):
    n = int(dur * SR); x = rng.choice([-1.0, 1.0], n)
    x = np.repeat(x[::4], 4)[:n]                         # crunchy 8-bit-ish noise
    return x * np.exp(-np.arange(n) / SR * decay)

CH = {'maj7': [0, 4, 7, 11], 'm7': [0, 3, 7, 10], '7': [0, 4, 7, 10], 'add9': [0, 4, 7, 14], 'm9': [0, 3, 7, 14], 'maj': [0, 4, 7], 'm': [0, 3, 7], '6': [0, 4, 7, 9]}

def ambient(name, bpm, prog, motif, seed, bars=16, register=72):
    r = np.random.default_rng(seed); beat = 60 / bpm; bar = 4 * beat; L = int(bars * bar * SR)
    buf = np.zeros((L, 2))
    for b in range(bars):
        root, q = prog[b % len(prog)]; t0 = b * bar; tones = [root + i for i in CH[q]]
        place(buf, soft_pad([note_hz(x) for x in tones[:3]], bar * 1.05), t0, .07)
        place(buf, piano(note_hz(root - 12), bar * 1.3, .55), t0 + r.uniform(0, .03), .5, -.15)
        # Broken chord, unhurried, a little loose in time.
        for i, iv in enumerate(tones):
            place(buf, piano(note_hz(root + iv), 2.4, .35), t0 + (i * .75 + .5) * beat + r.uniform(-.03, .05), .32, -.3 + .2 * i)
        # Melody: the motif over its chord, every other bar, with a rest-heavy answer in between.
        if b % 2 == 0 or b % 8 == 7:
            for (step, at, length) in motif[(b // 2) % len(motif)]:
                # The melody stays in the song's key (all chords here are diatonic), so it never clashes with a chord.
                place(buf, piano(note_hz(register + step), length * beat * 1.6, .5), t0 + at * beat + r.uniform(-.02, .04), .34, .25)
    buf = reverb(buf, seconds=2.6, mix=.34)
    return master(buf, -4)

def chip(name, bpm, prog, motif, seed, bars=16, swing=.18, drums=True, lead_duty=.25, lead_gain=.20):
    r = np.random.default_rng(seed); beat = 60 / bpm; bar = 4 * beat; L = int(bars * bar * SR)
    buf = np.zeros((L, 2)); e8 = lambda i: (i // 2) * beat + (i % 2) * (beat / 2 + swing * beat / 2)
    for b in range(bars):
        root, q = prog[b % len(prog)]; t0 = b * bar; tones = [root + i for i in CH[q]]
        # Walking-ish triangle bass on quarters: root, fifth, approach.
        walk = [root - 24, root - 24 + 7, root - 24 + (12 if b % 2 else 4), prog[(b + 1) % len(prog)][0] - 24 - 1]
        for i, n in enumerate(walk): place(buf, triangle(note_hz(n), beat * .9), t0 + i * beat, .34)
        # Off-beat chord stabs (soft square, low).
        for i in (1, 3, 5, 7):
            for k, iv in enumerate(tones[1:]): place(buf, pulse(note_hz(root + iv), beat * .3, .5, 6), t0 + e8(i), .045, -.3 + .3 * k)
        # Lead motif (two bars), restated with a variation on later bars.
        line = motif[(b // 2) % len(motif)][b % 2]
        for (step, at8, len8) in line:
            place(buf, pulse(note_hz(72 + step + (root - prog[0][0]) * (0 if b % 4 < 2 else 0)), len8 * beat / 2 * .95, lead_duty), t0 + e8(at8), lead_gain, .15)
        if drums:
            for i in range(8):
                if i in (0, 4) or (i == 6 and b % 2): place(buf, chip_kick(), t0 + e8(i), .30)
                if i in (2, 6): place(buf, chip_noise(.09, 35), t0 + e8(i), .10, -.1)
                place(buf, chip_noise(.03, 140), t0 + e8(i), .04 if i % 2 else .06, .25)
    buf = reverb(buf, seconds=1.1, mix=.14)
    return master(buf, -3.5)

def write(audio, path):
    pcm = (audio * 32767).astype('<i2').tobytes()
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-f', 's16le', '-ar', str(SR), '-ac', '2', '-i', '-',
                    '-c:a', 'libvorbis', '-q:a', '5', path], input=pcm, check=True)

if __name__ == '__main__':
    C, D, E, F, G, A, B = 48, 50, 52, 53, 55, 57, 59
    out = sys.argv[1]
    CHIP = '--ambient-only' not in sys.argv
    # Ambient motifs: (semitone above register, beat in bar, length in beats). Sparse on purpose.
    amb_city = [[(7, 0, 1.5), (4, 1.5, .5), (2, 2, 2)], [(9, 0, 1), (7, 1, 1), (4, 2.5, 1.5)], [(12, .5, 1.5), (11, 2, 1), (7, 3, 1)], [(4, 0, 2), (2, 2, 2)]]
    amb_cook = [[(4, 0, 1), (7, 1, 1), (11, 2, 2)], [(9, .5, 1.5), (7, 2, 2)], [(2, 0, 1), (4, 1, 1), (7, 2, 1), (4, 3, 1)], [(0, 0, 3)]]
    amb_shop = [[(0, 0, .5), (2, .5, .5), (4, 1, 1), (7, 2, 2)], [(9, 0, 1), (7, 1, .5), (4, 1.5, 2.5)], [(2, 0, 1), (4, 1, 1), (2, 2, 2)], [(-1, .5, 1.5), (0, 2, 2)]]
    write(ambient('city', 70, [(F, 'maj7'), (C, 'add9'), (A - 12, 'm7'), (G - 12, '6')], amb_city, 1), f'{out}/city_ambient.ogg')
    write(ambient('choosing', 84, [(C, 'maj7'), (E - 12, 'm7'), (F, 'maj7'), (G - 12, '6')], amb_shop, 2), f'{out}/choosing_ambient.ogg')
    write(ambient('cooking', 64, [(D, 'm9'), (G - 12, '7'), (C, 'maj7'), (A - 12, 'm7')], amb_cook, 3, register=72), f'{out}/cooking_ambient.ogg')
    # Chip motifs: per two-bar phrase, two bars of (semitones above C5, start eighth, length in eighths).
    chip_shop = [
        [[(0, 0, 1), (4, 1, 1), (7, 2, 2), (9, 4, 1), (7, 5, 1), (4, 6, 2)], [(2, 0, 2), (5, 2, 1), (4, 3, 1), (2, 4, 4)]],
        [[(0, 0, 1), (4, 1, 1), (7, 2, 2), (12, 4, 2), (11, 6, 1), (9, 7, 1)], [(7, 0, 3), (4, 4, 2), (2, 6, 2)]]]
    chip_city = [
        [[(7, 0, 3), (4, 3, 1), (2, 4, 2), (4, 6, 2)], [(0, 0, 4), (-1, 5, 1), (0, 6, 2)]],
        [[(9, 0, 3), (7, 3, 1), (4, 4, 2), (2, 6, 2)], [(4, 0, 6)]]]
    chip_cook = [
        [[(4, 0, 2), (7, 2, 1), (9, 3, 3), (7, 6, 2)], [(5, 0, 2), (4, 2, 2), (2, 4, 4)]],
        [[(4, 0, 2), (7, 2, 1), (11, 3, 3), (12, 6, 2)], [(9, 0, 3), (7, 4, 4)]]]
    if CHIP: write(chip('choosing', 116, [(C, 'maj7'), (A - 12, 'm7'), (D, 'm7'), (G - 12, '7')], chip_shop, 4), f'{out}/choosing_chip.ogg')
    if CHIP: write(chip('city', 96, [(F, 'maj7'), (E, 'm7'), (D, 'm7'), (C, 'maj7')], chip_city, 5, swing=.22, lead_duty=.125, lead_gain=.17), f'{out}/city_chip.ogg')
    if CHIP: write(chip('cooking', 88, [(C, 'maj7'), (E, 'm7'), (A - 12, 'm7'), (D, '7')], chip_cook, 6, swing=.28, drums=True, lead_duty=.5, lead_gain=.15), f'{out}/cooking_chip.ogg')
    print('done')
