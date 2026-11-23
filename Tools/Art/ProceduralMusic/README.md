# Procedural placeholder music (offline tool)

Python + numpy + ffmpeg; not part of the game build. `tension.py <out> --backing` makes the current placeholders: the backing of three
loops (city D dorian, choosing E minor, cooking A minor) at three intensities (low/normal/high, 16 bars each; normal
and high at 128 bpm, low at 128/144/136). The game uses them without a lead melody (Data/Sound.md). `synth.py` and `styles.py` hold the
instruments and two earlier style experiments (friendly additive, ambient piano, chip). Each render takes seconds.
Real music should replace these: see the prompt pack in Data/Sound.md.
