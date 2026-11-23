# Procedural placeholder music for Blue Zone Bistro (2026-11-22). Simple additive synthesis, no samples.
# Each track is an exact number of bars; reverb and delay tails are wrapped so the loop is seamless.
import numpy as np, subprocess, sys
SR = 44100
rng = np.random.default_rng(7)

def note_hz(n):  # MIDI note -> Hz
    return 440.0 * 2 ** ((n - 69) / 12)

def env(n, a, d, s, r, total):
    t = np.arange(n) / SR
    e = np.ones(n) * s
    a_n, d_n = int(a * SR), int(d * SR)
    e[:a_n] = np.linspace(0, 1, max(a_n, 1))[:a_n]
    if d_n: e[a_n:a_n + d_n] = np.linspace(1, s, d_n)[: max(0, min(d_n, n - a_n))]
    rel = int(r * SR); start = max(0, n - rel)
    e[start:] *= np.linspace(1, 0, n - start)
    return e

def pluck(freq, dur, bright=1.0):   # soft guitar/harp-like: harmonics with faster decay up high
    n = int(dur * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for k, amp in enumerate([1, .5, .28, .16, .09, .05], 1):
        y += amp * bright ** (k - 1) * np.sin(2 * np.pi * freq * k * t) * np.exp(-t * (2.2 + 1.6 * k))
    return y * env(n, .004, 0, 1, .05, dur)

def marimba(freq, dur):
    n = int(dur * SR); t = np.arange(n) / SR
    y = np.sin(2 * np.pi * freq * t) * np.exp(-t * 4.5) + .35 * np.sin(2 * np.pi * freq * 3.98 * t) * np.exp(-t * 14)
    return y * env(n, .002, 0, 1, .04, dur)

def keys(freq, dur):   # warm electric-piano-ish (gentle FM)
    n = int(dur * SR); t = np.arange(n) / SR
    mod = .9 * np.exp(-t * 3) * np.sin(2 * np.pi * freq * 2 * t)
    y = np.sin(2 * np.pi * freq * t + mod) * np.exp(-t * 1.6) + .12 * np.sin(2 * np.pi * freq * 4 * t) * np.exp(-t * 6)
    return y * env(n, .006, 0, 1, .12, dur)

def pad(freqs, dur):   # slow, airy chord: detuned sines with a little 2nd harmonic
    n = int(dur * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for f in freqs:
        for det in (-0.12, 0.12):
            ff = f * 2 ** (det / 12)
            y += np.sin(2 * np.pi * ff * t + rng.uniform(0, 6.28)) + .15 * np.sin(4 * np.pi * ff * t)
    return y / (2 * len(freqs)) * env(n, dur * .35, 0, 1, dur * .35, dur)

def bass(freq, dur):
    n = int(dur * SR); t = np.arange(n) / SR
    y = np.sin(2 * np.pi * freq * t) + .25 * np.sin(4 * np.pi * freq * t)
    return y * env(n, .01, .15, .7, .08, dur)

def noise_hit(dur, decay, hp=True):
    n = int(dur * SR); t = np.arange(n) / SR
    x = rng.standard_normal(n)
    if hp: x = np.diff(np.concatenate([[0], x]))   # brighter
    return x * np.exp(-t * decay)

def kick(dur=.25):
    n = int(dur * SR); t = np.arange(n) / SR
    f = 95 * np.exp(-t * 18) + 45
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 14)

def place(buf, sig, at, gain=1.0, pan=0.0):
    i = int(at * SR); j = min(len(buf), i + len(sig))
    if i >= len(buf): return
    l, r = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
    buf[i:j, 0] += sig[: j - i] * gain * l * 1.41
    buf[i:j, 1] += sig[: j - i] * gain * r * 1.41
    if i + len(sig) > len(buf):   # wrap the tail to the start: seamless loop
        rest = sig[j - i:]
        buf[: len(rest), 0] += rest * gain * l * 1.41
        buf[: len(rest), 1] += rest * gain * r * 1.41

def reverb(buf, seconds=1.4, mix=.22):
    n = int(seconds * SR); t = np.arange(n) / SR
    ir = rng.standard_normal((n, 2)) * np.exp(-t * (6.9 / seconds))[:, None]
    ir[:, 0] = np.convolve(ir[:, 0], np.ones(8) / 8, 'same'); ir[:, 1] = np.convolve(ir[:, 1], np.ones(8) / 8, 'same')
    out = np.zeros_like(buf); L = len(buf)
    for c in range(2):   # circular convolution = wrapped tail
        out[:, c] = np.real(np.fft.ifft(np.fft.fft(buf[:, c], L + n) * np.fft.fft(ir[:, c], L + n)))[: L + n][:L]
        tail = np.real(np.fft.ifft(np.fft.fft(buf[:, c], L + n) * np.fft.fft(ir[:, c], L + n)))[L:L + n]
        out[: len(tail), c] += tail
    out *= np.sqrt(np.mean(buf ** 2) / max(np.mean(out ** 2), 1e-12))
    return buf * (1 - mix) + out * mix

def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR); y = np.zeros_like(x); prev = np.zeros(x.shape[1])
    for i in range(len(x)): prev = (1 - a) * x[i] + a * prev; y[i] = prev
    return y

def master(buf, peak_db=-3.0):
    buf = buf - buf.mean(0)
    buf = np.tanh(buf / (np.abs(buf).max() + 1e-9) * 1.4) / np.tanh(1.4)
    return buf * 10 ** (peak_db / 20)

def chord_tones(root, quality):
    return [root, root + (3 if quality == 'm' else 4), root + 7]

def render(name, bpm, bars, progression, voice, groove):
    beat = 60 / bpm; bar = 4 * beat; L = int(bars * bar * SR)
    buf = np.zeros((L, 2))
    for b in range(bars):
        root, q = progression[b % len(progression)]
        tones = chord_tones(root, q); t0 = b * bar
        place(buf, pad([note_hz(x + 12) for x in tones], bar), t0, .16)
        place(buf, bass(note_hz(root - 12), beat * 1.8), t0, .30)
        place(buf, bass(note_hz(root - 12 + (7 if b % 2 else 0)), beat * 1.6), t0 + 2 * beat, .24)
        voice(buf, b, t0, beat, tones)
        groove(buf, b, t0, beat)
    buf = reverb(buf)
    return master(buf)

# --- City: C major, 92 bpm, curious walking arpeggio (pluck) + soft shaker -------------------------------------
def city_voice(buf, b, t0, beat, tones):
    pattern = [0, 1, 2, 1, 3, 2, 1, 2]   # up-down arpeggio in eighths
    pool = [tones[0] + 12, tones[1] + 12, tones[2] + 12, tones[0] + 24]
    for i, k in enumerate(pattern):
        if b % 4 == 3 and i >= 6: continue   # breathe at phrase ends
        place(buf, pluck(note_hz(pool[k]), .9, .7), t0 + i * beat / 2, .22, -.35 + .1 * (i % 3))
    if b % 2 == 1:   # little melody answer
        mel = [tones[2] + 24, tones[1] + 24, tones[0] + 24]
        for i, m in enumerate(mel): place(buf, pluck(note_hz(m), 1.0, .5), t0 + (2.5 + i * .5) * beat, .13, .4)
def city_groove(buf, b, t0, beat):
    for i in range(8): place(buf, noise_hit(.08, 60), t0 + i * beat / 2 + (0.02 if i % 2 else 0), .035 if i % 2 else .05, .3)
    place(buf, kick(), t0, .25); place(buf, kick(), t0 + 2.5 * beat, .14)

# --- Choosing (stores + sorting): F major, 104 bpm, marimba pattern, light clicks, a bit busier -----------------
def shop_voice(buf, b, t0, beat, tones):
    seq = [0, 2, 1, 2, 0, 2, 1, 3] if b % 2 == 0 else [2, 1, 0, 1, 2, 3, 2, 1]
    pool = [tones[0] + 12, tones[1] + 12, tones[2] + 12, tones[0] + 24]
    for i, k in enumerate(seq):
        place(buf, marimba(note_hz(pool[k]), .5), t0 + i * beat / 2, .26 if i % 2 == 0 else .18, .25 if i % 2 else -.25)
def shop_groove(buf, b, t0, beat):
    for i in range(4):
        place(buf, kick(.2), t0 + i * beat, .18 if i % 2 == 0 else .1)
        place(buf, noise_hit(.05, 90), t0 + i * beat + beat / 2, .045, -.2)
    place(buf, noise_hit(.12, 30, False), t0 + 3 * beat, .05, .1)   # soft rim/brush on 4

# --- Cooking (bistro + results): G major / E minor, 80 bpm, warm keys, brushed lazy groove ----------------------
def cafe_voice(buf, b, t0, beat, tones):
    voicing = [tones[0] + 12, tones[2] + 12, tones[1] + 24]   # open voicing
    for k, n in enumerate(voicing): place(buf, keys(note_hz(n), 2.2), t0 + k * .03, .16, -.2 + .2 * k)
    for k, n in enumerate(voicing): place(buf, keys(note_hz(n), 1.4), t0 + 2.5 * beat + k * .03, .10, -.2 + .2 * k)
    if b % 2 == 1:
        mel = [tones[1] + 24, tones[2] + 24, tones[0] + 36, tones[2] + 24]
        for i, m in enumerate(mel): place(buf, keys(note_hz(m), .9), t0 + (1 + i * .75) * beat, .10, .35)
def cafe_groove(buf, b, t0, beat):
    swing = beat / 2 * .16
    for i in range(8): place(buf, noise_hit(.18, 22, False), t0 + i * beat / 2 + (swing if i % 2 else 0), .018 if i % 2 else .028, .2)
    place(buf, kick(.3), t0, .2); place(buf, kick(.3), t0 + 2.5 * beat, .12)
    place(buf, noise_hit(.25, 14, False), t0 + beat, .045, -.1); place(buf, noise_hit(.25, 14, False), t0 + 3 * beat, .045, -.1)

C, D, E, F, G, A = 48, 50, 52, 53, 55, 57
tracks = {
    'city':     (92,  16, [(C, ''), (A, 'm'), (F, ''), (G, ''), (C, ''), (E, 'm'), (F, ''), (G, '')], city_voice, city_groove),
    'choosing': (104, 16, [(F, ''), (D, 'm'), (A - 12 + 12, 'm'), (C, ''), (F, ''), (D, 'm'), (G, 'm'), (C, '')], shop_voice, shop_groove),
    'cooking':  (80,  12, [(G, ''), (E, 'm'), (C, ''), (D, ''), (G, ''), (E, 'm'), (A, 'm'), (D, '')], cafe_voice, cafe_groove),
}
if __name__ == '__main__':
    out_dir = sys.argv[1]
    for name, (bpm, bars, prog, voice, groove) in tracks.items():
        audio = render(name, bpm, bars, prog, voice, groove)
        pcm = (audio * 32767).astype('<i2').tobytes()
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-f', 's16le', '-ar', str(SR), '-ac', '2', '-i', '-',
                        '-c:a', 'libvorbis', '-q:a', '5', f'{out_dir}/{name}.ogg'], input=pcm, check=True)
        print(name, f'{len(audio)/SR:.1f}s')
