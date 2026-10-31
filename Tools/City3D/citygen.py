"""Blue Zone Bistro - procedural low-poly city (Python mirror of Assets/Editor/City/LowPolyCityGenerator.cs).

Reads Assets/GameData/City/Blue_Zone_Bistro_CityLayout.csv and builds faceted triangles with per-triangle colours.
Same seed and same layout give the same city as the Unity generator (shared xorshift32 random numbers, same order of
calls). Used to preview the city outside Unity:  python Tools/City3D/citygen.py <project_root> [out.png]
"""
import csv, math, os, sys

# ---------------- random numbers (identical in C#) ----------------
class Rng:
    def __init__(self, seed):
        self.s = (seed & 0xFFFFFFFF) or 0x9E3779B9
    def next_u32(self):
        x = self.s
        x ^= (x << 13) & 0xFFFFFFFF
        x ^= x >> 17
        x ^= (x << 5) & 0xFFFFFFFF
        self.s = x & 0xFFFFFFFF
        return self.s
    def value(self):            # [0,1)
        return (self.next_u32() >> 8) / 16777216.0
    def range(self, a, b):
        return a + (b - a) * self.value()
    def pick(self, items):
        return items[int(self.value() * len(items)) % len(items)]

# ---------------- palette (sRGB 0-255) ----------------
def hexc(h): return (int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16))
PAL = {k: hexc(v) for k, v in dict(
    water="#6F93A8", pavement="#D9D2C3", sidewalk="#E6E0D4", road="#BDB8AE", sand="#E8D8B0", grass="#9DB878",
    plaza="#E2DACB", trunk="#8A6A4F", rock="#9C9A94", flatroof="#CFC8BB", slate="#8F9AA3", terracotta="#B9876A",
    glass="#5F9E9A", tower="#E4DED2", towerdeck="#5C8F9A", orange="#E08A3C", blue="#3E78B5", awning="#3F78A8",
    parking="#C9C5BD", deck="#D8D3C8", hull="#F2F0EA", sail="#FBFAF5", pond="#7FA9B8").items()}
WALLS = [hexc(c) for c in ("#EFE6D6", "#E3D5BF", "#D8D8D4", "#F3EFE6", "#E9DCC4", "#DCCFB9")]
LEAVES = [hexc(c) for c in ("#7FA35B", "#6E9450", "#8DB36A", "#78A060")]

class Builder:
    """Collects faceted triangles per group (one combined mesh per group in Unity)."""
    def __init__(self):
        self.groups = {}
        self.colliders = []      # (id, centre xyz, size xyz)
    def tri(self, g, a, b, c, col):
        self.groups.setdefault(g, []).append((a, b, c, col))
    def quad(self, g, a, b, c, d, col):
        self.tri(g, a, b, c, col); self.tri(g, a, c, d, col)
    # Axis-aligned box: four walls and a top (bottom omitted, never seen).
    def box(self, g, x0, z0, x1, z1, y0, y1, wall, top=None):
        top = top or wall
        self.quad(g, (x0, y0, z0), (x0, y1, z0), (x1, y1, z0), (x1, y0, z0), wall)   # south
        self.quad(g, (x1, y0, z1), (x1, y1, z1), (x0, y1, z1), (x0, y0, z1), wall)   # north
        self.quad(g, (x0, y0, z1), (x0, y1, z1), (x0, y1, z0), (x0, y0, z0), wall)   # west
        self.quad(g, (x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1), wall)   # east
        self.quad(g, (x0, y1, z0), (x0, y1, z1), (x1, y1, z1), (x1, y1, z0), top)
    def prism(self, g, cx, cz, r0, r1, n, y0, y1, side, top=None, phase=0.0):
        top = top or side
        pts = [(math.cos(phase + 2 * math.pi * k / n), math.sin(phase + 2 * math.pi * k / n)) for k in range(n)]
        for k in range(n):
            a, b = pts[k], pts[(k + 1) % n]
            self.quad(g, (cx + a[0] * r0, y0, cz + a[1] * r0), (cx + a[0] * r1, y1, cz + a[1] * r1),
                      (cx + b[0] * r1, y1, cz + b[1] * r1), (cx + b[0] * r0, y0, cz + b[1] * r0), side)
        if top is not False:
            for k in range(n):
                a, b = pts[k], pts[(k + 1) % n]
                self.tri(g, (cx, y1, cz), (cx + b[0] * r1, y1, cz + b[1] * r1), (cx + a[0] * r1, y1, cz + a[1] * r1), top)
    def pyramid(self, g, cx, cz, r, n, y0, y1, col, phase=0.0):
        for k in range(n):
            a = phase + 2 * math.pi * k / n; b = phase + 2 * math.pi * (k + 1) / n
            self.tri(g, (cx + math.cos(a) * r, y0, cz + math.sin(a) * r), (cx, y1, cz), (cx + math.cos(b) * r, y0, cz + math.sin(b) * r), col)
    # Gable roof over a rectangle, ridge along the longer side.
    def gable(self, g, x0, z0, x1, z1, y0, h, roof, gable_wall):
        if (x1 - x0) >= (z1 - z0):
            zm = (z0 + z1) / 2
            self.quad(g, (x0, y0, z0), (x0, y0 + h, zm), (x1, y0 + h, zm), (x1, y0, z0), roof)
            self.quad(g, (x1, y0, z1), (x1, y0 + h, zm), (x0, y0 + h, zm), (x0, y0, z1), roof)
            self.tri(g, (x0, y0, z1), (x0, y0 + h, zm), (x0, y0, z0), gable_wall)
            self.tri(g, (x1, y0, z0), (x1, y0 + h, zm), (x1, y0, z1), gable_wall)
        else:
            xm = (x0 + x1) / 2
            self.quad(g, (x0, y0, z1), (xm, y0 + h, z1), (xm, y0 + h, z0), (x0, y0, z0), roof)
            self.quad(g, (x1, y0, z0), (xm, y0 + h, z0), (xm, y0 + h, z1), (x1, y0, z1), roof)
            self.tri(g, (x0, y0, z0), (xm, y0 + h, z0), (x1, y0, z0), gable_wall)
            self.tri(g, (x1, y0, z1), (xm, y0 + h, z1), (x0, y0, z1), gable_wall)
    def polygon(self, g, pts, y, col):
        for a, b, c in ear_clip(pts):
            self.tri(g, (a[0], y, a[1]), (b[0], y, b[1]), (c[0], y, c[1]), col)
    # Faceted blob from an icosahedron with jittered vertices (20 triangles). Used for tree canopies and rocks.
    def blob(self, g, rng, cx, cy, cz, rx, ry, rz, col, jitter=0.18):
        verts = [(v[0] * rx * (1 + rng.range(-jitter, jitter)), v[1] * ry * (1 + rng.range(-jitter, jitter)),
                  v[2] * rz * (1 + rng.range(-jitter, jitter))) for v in ICO_V]
        for f in ICO_F:
            a, b, c = (tuple(p + q for p, q in zip(verts[i], (cx, cy, cz))) for i in f)
            self.tri(g, a, b, c, col)
    def dome(self, g, cx, cy, cz, r, col):
        for a, b, c in DOME_F:
            self.tri(g, (cx + a[0] * r, cy + a[1] * r, cz + a[2] * r), (cx + b[0] * r, cy + b[1] * r, cz + b[2] * r),
                     (cx + c[0] * r, cy + c[1] * r, cz + c[2] * r), col)

def _ico():
    t = (1 + math.sqrt(5)) / 2
    v = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    n = math.sqrt(1 + t * t); v = [(a / n, b / n, c / n) for a, b, c in v]
    f = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
         (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    return v, f
ICO_V, ICO_F = _ico()
def _dome():
    out = []
    def mid(a, b):
        m = [(a[i] + b[i]) / 2 for i in range(3)]; l = math.sqrt(sum(x * x for x in m)); return tuple(x / l for x in m)
    for f in ICO_F:
        a, b, c = (ICO_V[i] for i in f)
        ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
        for t in ((a, ab, ca), (ab, b, bc), (ca, bc, c), (ab, bc, ca)):
            if sum(p[1] for p in t) / 3 > -0.05:
                out.append(tuple((p[0], max(0.0, p[1]), p[2]) for p in t))
    return out
DOME_F = _dome()

# ---------------- 2D helpers ----------------
def area2(pts):
    return sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))
def inside(pts, x, z):
    c = False; n = len(pts)
    for i in range(n):
        (x1, z1), (x2, z2) = pts[i], pts[(i + 1) % n]
        if (z1 > z) != (z2 > z) and x < (x2 - x1) * (z - z1) / (z2 - z1) + x1: c = not c
    return c
def ear_clip(pts):
    p = list(pts)
    if area2(p) < 0: p.reverse()                      # make counter-clockwise
    tris = []; guard = 0
    while len(p) > 3 and guard < 10000:
        guard += 1; n = len(p)
        for i in range(n):
            a, b, c = p[i - 1], p[i], p[(i + 1) % n]
            if (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]) <= 0: continue
            if any(q not in (a, b, c) and _in_tri(q, a, b, c) for q in p): continue
            tris.append((a, b, c)); p.pop(i); break
        else:
            break
    if len(p) == 3: tris.append((p[0], p[1], p[2]))
    return tris
def _in_tri(q, a, b, c):
    d1 = (q[0] - b[0]) * (a[1] - b[1]) - (a[0] - b[0]) * (q[1] - b[1])
    d2 = (q[0] - c[0]) * (b[1] - c[1]) - (b[0] - c[0]) * (q[1] - c[1])
    d3 = (q[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (q[1] - a[1])
    return not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0))

# ---------------- layout ----------------
def load_layout(path):
    rows = list(csv.DictReader(open(path, encoding="utf-8")))
    s = {r["id"]: float(r["x"]) for r in rows if r["kind"] == "setting"}
    pick = lambda k: [r for r in rows if r["kind"] == k]
    return s, pick

def build(root):
    s, pick = load_layout(os.path.join(root, "Assets/GameData/City/Blue_Zone_Bistro_CityLayout.csv"))
    rng = Rng(int(s["seed"])); B = Builder()
    bx, bz, bs, rw = int(s["blocks_x"]), int(s["blocks_z"]), s["block_size"], s["road_width"]
    P = bs + rw                                         # block pitch in metres
    U = lambda v: float(v) * P                          # block units -> metres
    coast = [(U(r["x"]), U(r["z"])) for r in pick("coast")]
    island = [(U(r["x"]), U(r["z"])) for r in pick("island")]
    lands = [coast] + ([island] if len(island) >= 3 else [])
    on_land = lambda x, z: any(inside(L, x, z) for L in lands)
    GROUND, LAND, ROAD, WALK = 0.0, 0.6, 0.66, 0.8
    # Water: one large plane.
    m = 3 * P
    B.quad("water", (-m, GROUND, -m), (-m, GROUND, bz * P + m), (bx * P + m, GROUND, bz * P + m), (bx * P + m, GROUND, -m), PAL["water"])
    # Land, with a sand strip along every shore edge and rocks scattered on it.
    for L in lands:
        B.polygon("ground", L, LAND, PAL["pavement"])
        ccw = area2(L) > 0
        for i in range(len(L)):
            (x1, z1), (x2, z2) = L[i], L[(i + 1) % len(L)]
            dx, dz = x2 - x1, z2 - z1; ln = math.hypot(dx, dz) or 1
            nx, nz = (dz / ln, -dx / ln) if ccw else (-dz / ln, dx / ln)      # outward normal
            w = 6.0
            B.quad("ground", (x1, 0.35, z1), (x1 + nx * w, 0.05, z1 + nz * w), (x2 + nx * w, 0.05, z2 + nz * w), (x2, 0.35, z2), PAL["sand"])
            for k in range(int(ln // 11)):
                if rng.value() < .55:
                    t = (k + .5) / max(1, int(ln // 11))
                    px, pz = x1 + dx * t - nx * 5.5, z1 + dz * t - nz * 5.5
                    if on_land(px, pz): tree(B, rng, px, LAND, pz, .9)
            steps = int(ln // 9)
            for k in range(steps):
                if rng.value() < 0.45:
                    t = (k + rng.value()) / max(1, steps)
                    px, pz = x1 + dx * t + nx * rng.range(2, 6), z1 + dz * t + nz * rng.range(2, 6)
                    r = rng.range(0.8, 1.8)
                    B.blob("ground", rng, px, 0.3, pz, r, r * 0.6, r, PAL["rock"], 0.25)
    # Reserved areas: landmarks, parks, ponds (in metres).
    def rect(r): return (U(r["x"]), U(r["z"]), U(r["x"]) + U(r["w"]), U(r["z"]) + U(r["d"]))
    reserved = [rect(r) for k in ("landmark", "park", "pond") for r in pick(k)]
    def overlaps(a, b): return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]
    # Streets: segments between intersections, kept when both ends and the middle are on land.
    half = rw / 2
    for i in range(bx + 1):
        for j in range(bz + 1):
            x, z = i * P, j * P
            if on_land(x, z):
                B.quad("roads", (x - half, ROAD, z - half), (x - half, ROAD, z + half), (x + half, ROAD, z + half), (x + half, ROAD, z - half), PAL["road"])
            if i < bx and on_land(x, z) and on_land(x + P, z) and on_land(x + P / 2, z):
                B.quad("roads", (x + half, ROAD, z - half), (x + half, ROAD, z + half), (x + P - half, ROAD, z + half), (x + P - half, ROAD, z - half), PAL["road"])
            if j < bz and on_land(x, z) and on_land(x, z + P) and on_land(x, z + P / 2):
                B.quad("roads", (x - half, ROAD, z + half), (x - half, ROAD, z + P - half), (x + half, ROAD, z + P - half), (x + half, ROAD, z + half), PAL["road"])
    # City blocks.
    cx0, cz0 = U(7.5), U(6.5)                            # downtown centre (tallest buildings)
    max_d = math.hypot(bx * P, bz * P) / 2
    for i in range(bx):
        for j in range(bz):
            x0, z0 = i * P + half, j * P + half; x1, z1 = x0 + bs, z0 + bs
            if any(overlaps((x0, z0, x1, z1), r) for r in reserved): continue
            if not all(on_land(px, pz) for px, pz in ((x0 - 2, z0 - 2), (x1 + 2, z0 - 2), (x1 + 2, z1 + 2), (x0 - 2, z1 + 2))):
                # Shore block: houses on whichever quarter lots are fully on land, no pavement slab.
                for (u0, v0, u1, v1) in [(0, 0, .5, .5), (.5, 0, 1, .5), (0, .5, .5, 1), (.5, .5, 1, 1)]:
                    lx0, lz0, lx1, lz1 = x0 + bs * u0 + 2.2, z0 + bs * v0 + 2.2, x0 + bs * u1 - 2.2, z0 + bs * v1 - 2.2
                    if not all(on_land(px, pz) for px, pz in ((lx0 - 3, lz0 - 3), (lx1 + 3, lz0 - 3), (lx1 + 3, lz1 + 3), (lx0 - 3, lz1 + 3))): continue
                    wall = rng.pick(WALLS); h = 3.2 * rng.pick([1, 2])
                    B.box("buildings", lx0, lz0, lx1, lz1, LAND, LAND + h, wall)
                    B.gable("buildings", lx0 - .4, lz0 - .4, lx1 + .4, lz1 + .4, LAND + h, rng.range(2.4, 3.4), rng.pick([PAL["slate"], PAL["terracotta"]]), wall)
                continue
            B.box("ground", x0, z0, x1, z1, LAND, WALK, PAL["sidewalk"])
            d = math.hypot((x0 + x1) / 2 - cx0, (z0 + z1) / 2 - cz0) / max_d
            residential = j >= bz - 2 or d > 0.78
            lots = 4 if residential else rng.pick([1, 2, 2, 4])
            cells = [(0, 0, 1, 1)] if lots == 1 else ([(0, 0, .5, 1), (.5, 0, 1, 1)] if lots == 2 and rng.value() < .5 else
                     [(0, 0, 1, .5), (0, .5, 1, 1)] if lots == 2 else [(0, 0, .5, .5), (.5, 0, 1, .5), (0, .5, .5, 1), (.5, .5, 1, 1)])
            for (u0, v0, u1, v1) in cells:
                inset = 2.2 if residential else 1.6
                lx0, lz0 = x0 + bs * u0 + inset, z0 + bs * v0 + inset
                lx1, lz1 = x0 + bs * u1 - inset, z0 + bs * v1 - inset
                wall = rng.pick(WALLS)
                if residential:
                    h = 3.2 * rng.pick([1, 2, 2])
                    B.box("buildings", lx0, lz0, lx1, lz1, WALK, WALK + h, wall)
                    B.gable("buildings", lx0 - .4, lz0 - .4, lx1 + .4, lz1 + .4, WALK + h, rng.range(2.4, 3.4), rng.pick([PAL["slate"], PAL["terracotta"]]), wall)
                else:
                    floors = 2 + int(rng.value() * 9 * (1 - d) ** 1.5)
                    h = 3.2 * floors
                    B.box("buildings", lx0, lz0, lx1, lz1, WALK, WALK + h, wall, PAL["flatroof"])
                    if floors >= 5 and rng.value() < .5:       # setback upper storey
                        sx, sz = (lx1 - lx0) * .18, (lz1 - lz0) * .18
                        B.box("buildings", lx0 + sx, lz0 + sz, lx1 - sx, lz1 - sz, WALK + h, WALK + h + 3.2 * rng.pick([1, 2]), wall, PAL["flatroof"])
                    if rng.value() < .6:                        # rooftop unit
                        ux, uz = rng.range(lx0 + 1, lx1 - 3), rng.range(lz0 + 1, lz1 - 3)
                        B.box("buildings", ux, uz, ux + 2, uz + 2, WALK + h, WALK + h + 1.2, PAL["slate"])
            # Street trees along the block edge.
            for k in range(4):
                for t in range(3):
                    if rng.value() < .55:
                        f = (t + .5) / 3
                        px, pz = [(x0 + bs * f, z0 + .9), (x1 - .9, z0 + bs * f), (x1 - bs * f, z1 - .9), (x0 + .9, z1 - bs * f)][k]
                        tree(B, rng, px, WALK, pz, 1.25)
    # Parks, plazas and ponds.
    for r in pick("park"):
        x0, z0, x1, z1 = rect(r)
        plaza = r["style"] == "plaza"
        B.box("ground", x0 + half, z0 + half, x1 - half, z1 - half, LAND, WALK - .05, PAL["plaza"] if plaza else PAL["grass"])
        n = int((x1 - x0) * (z1 - z0) / (110 if not plaza else 320))
        for _ in range(n):
            px, pz = rng.range(x0 + half + 2, x1 - half - 2), rng.range(z0 + half + 2, z1 - half - 2)
            if any(overlaps((px - 3, pz - 3, px + 3, pz + 3), rect(q)) for q in pick("pond")): continue
            if plaza and math.hypot(px - (x0 + x1) / 2, pz - (z0 + z1) / 2) < (x1 - x0) * .22: continue
            tree(B, rng, px, WALK - .05, pz, 1.0)
    for r in pick("pond"):
        x0, z0, x1, z1 = rect(r)
        cx, cz, rx, rz = (x0 + x1) / 2, (z0 + z1) / 2, (x1 - x0) / 2, (z1 - z0) / 2
        pts = [(cx + math.cos(a) * rx * rng.range(.85, 1), cz + math.sin(a) * rz * rng.range(.85, 1)) for a in [2 * math.pi * k / 10 for k in range(10)]]
        B.polygon("water", pts, WALK - .02, PAL["pond"])
    # Landmarks.
    for r in pick("landmark"):
        landmark(B, rng, r, rect(r), LAND, WALK, P)
    # Bridges and piers.
    for r in pick("bridge"):
        x0, z0, x1, z1 = rect(r)
        B.box("landmarks", x0 - 2, z0, x1 + 2, z1, 3.0, 3.8, PAL["deck"])
        for k in range(4):
            px = x0 + (x1 - x0) * (k + .5) / 4
            B.prism("landmarks", px, (z0 + z1) / 2, 1.3, 1.1, 6, 0, 3.0, PAL["deck"], False)
        B.box("landmarks", x0 - 2, z0 - .2, x1 + 2, z0 + .4, 3.8, 4.8, PAL["tower"])
        B.box("landmarks", x0 - 2, z1 - .4, x1 + 2, z1 + .2, 3.8, 4.8, PAL["tower"])
    for r in pick("pier"):
        x0, z0, x1, z1 = rect(r)
        B.box("landmarks", x0, z0 - U(r["d"]) * .2, x1, z1 - U(r["d"]), .5, 1.0, PAL["deck"])
        for k in range(2):
            bxp = x1 + 5 if k == 0 else x0 - 5
            boat(B, rng, bxp, z0 - U(r["d"]) * (.3 + .4 * k))
    return B

def tree(B, rng, x, y, z, scale):
    h = rng.range(1.4, 2.0) * scale
    B.prism("trees", x, z, .28 * scale, .22 * scale, 6, y, y + h, PAL["trunk"], False)
    r = rng.range(2.2, 3.0) * scale
    B.blob("trees", rng, x, y + h + r * .55, z, r, r * .8, r, rng.pick(LEAVES))

def boat(B, rng, x, z):
    L, W = rng.range(6, 9), rng.range(2.2, 3.0)
    pts = [(x - L / 2, z), (x - L / 4, z - W / 2), (x + L / 3, z - W / 2), (x + L / 2, z), (x + L / 3, z + W / 2), (x - L / 4, z + W / 2)]
    for k in range(len(pts)):
        a, b = pts[k], pts[(k + 1) % len(pts)]
        B.quad("landmarks", (a[0], .2, a[1]), (a[0], 1.1, a[1]), (b[0], 1.1, b[1]), (b[0], .2, b[1]), PAL["hull"])
    B.polygon("landmarks", pts, 1.1, PAL["deck"])
    B.box("landmarks", x - .15, z - .15, x + .15, z + .15, 1.1, 1.1 + L * .9, PAL["hull"])
    B.tri("landmarks", (x + .2, 1.6, z), (x + .2, 1.1 + L * .85, z), (x + L * .4, 1.6, z), PAL["sail"])

def landmark(B, rng, r, rc, LAND, WALK, P):
    x0, z0, x1, z1 = rc; half = 4.0
    x0, z0, x1, z1 = x0 + half, z0 + half, x1 - half, z1 - half
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    style = r["style"]; top = WALK
    B.box("ground", x0, z0, x1, z1, LAND, WALK, PAL["plaza"])
    if style == "dome":
        bw = min(x1 - x0, z1 - z0) * .26
        B.prism("landmarks", cx, cz, bw * 1.1, bw * 1.1, 8, WALK, WALK + 7, PAL["tower"], PAL["flatroof"], math.pi / 8)
        B.dome("landmarks", cx, WALK + 7, cz, bw, PAL["glass"])
        B.box("landmarks", cx - 4, z0 + 2, cx + 4, cz - bw * .9, WALK, WALK + 4, PAL["tower"], PAL["glass"])
        top = WALK + 7 + bw
    elif style.startswith("bigbox"):
        band = PAL["orange"] if style.endswith("orange") else PAL["blue"]
        roof = PAL["flatroof"] if band == PAL["orange"] else hexc("#A9BFCF")
        bx0, bz0, bx1, bz1 = x0 + 2, z0 + (z1 - z0) * .32, x1 - 2, z1 - 2
        B.box("landmarks", bx0, bz0, bx1, bz1, WALK, WALK + 9, PAL["tower"], roof)
        B.box("landmarks", bx0 - .3, bz0 - .4, bx1 + .3, bz0 + .3, WALK + 6.5, WALK + 9.6, band)
        B.box("ground", x0 + 2, z0 + 2, x1 - 2, bz0 - 1.5, WALK, WALK + .02, PAL["parking"])
        for k in range(int((x1 - x0) // 9)):
            tree(B, rng, x0 + 4 + k * 9, WALK, z0 + 2.5, .7)
        top = WALK + 9.6
    elif style == "tower":
        y = WALK
        for k, (ra, rb, h) in enumerate([(4.5, 4.5, 2.5), (2.4, 1.9, 30), (1.9, 1.9, 4)]):
            B.prism("landmarks", cx, cz, ra, rb, 8, y, y + h, PAL["tower"], PAL["tower"], math.pi / 8); y += h
        B.prism("landmarks", cx, cz, 5.5, 5.5, 12, y, y + 3.2, PAL["towerdeck"], PAL["tower"]); y += 3.2
        B.prism("landmarks", cx, cz, 2.4, 2.0, 8, y, y + 3, PAL["tower"], False, math.pi / 8); y += 3
        B.pyramid("landmarks", cx, cz, 2.6, 8, y, y + 6, PAL["towerdeck"], math.pi / 8); y += 6
        top = y
    elif style == "bistro":
        bx0, bz0, bx1, bz1 = cx - 11, cz - 2, cx + 11, cz + 9
        B.box("landmarks", bx0, bz0, bx1, bz1, WALK, WALK + 7, hexc("#F4ECDC"))
        B.gable("landmarks", bx0 - .8, bz0 - .8, bx1 + .8, bz1 + .8, WALK + 7, 4.0, PAL["awning"], hexc("#F4ECDC"))
        B.quad("landmarks", (bx0, WALK + 4.2, bz0), (bx1, WALK + 4.2, bz0), (bx1, WALK + 3.0, bz0 - 4.5), (bx0, WALK + 3.0, bz0 - 4.5), PAL["awning"])
        for k in range(5):                                   # terrace umbrellas (six-sided canopies)
            tx = bx0 + 2.5 + k * 4.2
            B.prism("landmarks", tx, bz0 - 7.5, .12, .12, 6, WALK, WALK + 2.2, PAL["trunk"], False)
            B.pyramid("landmarks", tx, bz0 - 7.5, 1.8, 6, WALK + 2.0, WALK + 2.9, PAL["sail"])
        top = WALK + 11
    B.colliders.append((r["id"], (cx, (WALK + top) / 2, cz), (x1 - x0, top - WALK, z1 - z0)))

# ---------------- preview renderer ----------------
def render(B, out, width=1672, height=940, yaw=-28.0, pitch=38.0, zoom=1.0, focus=None):
    import numpy as np
    from PIL import Image, ImageDraw
    tris = [t for g in ("water", "ground", "roads", "buildings", "landmarks", "trees") for t in B.groups.get(g, [])]
    groups_of = [g for g in ("water", "ground", "roads", "buildings", "landmarks", "trees") for _ in B.groups.get(g, [])]
    A = np.array([[t[0], t[1], t[2]] for t in tris], dtype=np.float64)        # (n,3,3)
    C = np.array([t[3] for t in tris], dtype=np.float64)
    yw, pt = math.radians(yaw), math.radians(pitch)
    f = np.array([math.sin(yw) * math.cos(pt), -math.sin(pt), math.cos(yw) * math.cos(pt)])   # view direction
    right = np.cross([0, 1, 0], f); right /= np.linalg.norm(right); up = np.cross(f, right)
    n = np.cross(A[:, 1] - A[:, 0], A[:, 2] - A[:, 0]); ln = np.linalg.norm(n, axis=1, keepdims=True); ln[ln == 0] = 1; n /= ln
    n[n[:, 1] < 0] *= 1                                                     # keep winding as authored
    sun = np.array([-.45, .8, -.38]); sun /= np.linalg.norm(sun)
    lam = np.abs(n @ sun)
    shade = .58 + .42 * lam
    col = np.clip(C * shade[:, None], 0, 255)
    fog_c = np.array([214, 224, 228.0])
    sx = A @ right; sy = A @ up; depth = (A @ f).mean(axis=1)
    if focus is None:
        landmask = np.array([g != "water" for g in groups_of])
        lx, ly = sx[landmask], sy[landmask]
        cxs, cys = (lx.min() + lx.max()) / 2, (ly.min() + ly.max()) / 2; span = max((lx.max() - lx.min()) / width, (ly.max() - ly.min()) / height)
    else:
        p = np.array(focus); cxs, cys = p @ right, p @ up; span = (sx.max() - sx.min()) / width
    s = 1 / (span * 1.02) * zoom
    X = (sx - cxs) * s + width / 2; Y = height / 2 - (sy - cys) * s
    dn = (depth - depth.min()) / (np.ptp(depth) or 1)
    col = col * (1 - .18 * dn[:, None]) + fog_c * (.18 * dn[:, None])
    # Flat ground layers (water, land, sand, roads, pavements) first, lowest first; then 3D shapes far to near.
    flat = (np.ptp(A[:, :, 1], axis=1) < 1e-6) & (A[:, 0, 1] < 1.0)
    lay = np.where(flat)[0]; lay = lay[np.argsort(A[lay, 0, 1], kind="stable")]
    rest = np.where(~flat)[0]; rest = rest[np.argsort(-depth[rest])]
    order = np.concatenate([lay, rest])
    img = Image.new("RGB", (width, height), (214, 224, 228)); d = ImageDraw.Draw(img)
    for i in order:
        c = tuple(int(v) for v in col[i])
        d.polygon([(X[i, 0], Y[i, 0]), (X[i, 1], Y[i, 1]), (X[i, 2], Y[i, 2])], fill=c, outline=c)
    img.save(out)
    return len(tris)

if __name__ == "__main__":
    root = sys.argv[1] if len(sys.argv) > 1 else "."
    out = sys.argv[2] if len(sys.argv) > 2 else "city_preview.png"
    B = build(root)
    counts = {g: len(t) for g, t in B.groups.items()}
    print("triangles", sum(counts.values()), counts)
    print("colliders", [c[0] for c in B.colliders])
    render(B, out)
