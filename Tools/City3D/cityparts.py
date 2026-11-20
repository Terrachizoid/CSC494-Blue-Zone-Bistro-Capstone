"""Blue Zone Bistro city - geometry builder, shapes and the building/tree library.

Every shape is flat-shaded triangles with one colour per triangle. Colours are (r, g, b, gloss) in 0-255; gloss is
written to the vertex alpha and drives the shader's specular/reflection mask (0 matte, 255 glossy).

Building types (each takes a lot row from the lots table and its own random stream for small details):
  house      1-2 floors; gable, hip or L-shaped; punched windows, door, chimney
  midrise    2-8 floors; footprint rect / chamfered / L; shop ground floor with awnings; window grid or ribbons;
             cornice; rooftop units, water tank, stair hut
  tower      8-16 floors on a podium; setback, glass curtain, ribbon or chamfered body; crown and antenna
  warehouse  big low box, sawtooth roof with north-light glazing, loading doors
Trees: small (street, 14 tris), round (park, 26 tris), poplar (22 tris). No cones, no smooth primitives.
"""
import math

# ---------------- colours and materials ----------------
# A colour is (r, g, b, gloss, material). Material ids select a row of the shader's material table
# (reflectance F0, roughness, specular strength): see BZB_LowPolyVertexColor.shader.
PLASTER, GLASS, METAL, STONE, FOLIAGE, PAINT, SAND, WATER, FOAM, SHORE = range(10)
MATERIAL_NAMES = ["plaster", "glass", "metal", "stone", "foliage", "paint", "sand", "water", "foam", "shore"]

def hexc(h, gloss=0, mat=PLASTER):
    return (int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16), gloss, mat)

def mat_of(c):
    return c[4] if len(c) > 4 else PLASTER

def shade(c, f):
    return (min(255, int(c[0] * f)), min(255, int(c[1] * f)), min(255, int(c[2] * f))) + tuple(c[3:])

def gloss(c, g):
    return (c[0], c[1], c[2], g) + tuple(c[4:])

def with_mat(c, m):
    return (c[0], c[1], c[2], c[3] if len(c) > 3 else 0, m)

# Palette after the painted city concept: cool whites and light greys, muted warm accents, deep greens.
PAL = dict(
    water=hexc("#4F7F99", 230, WATER), shore=hexc("#4F7F99", 230, SHORE), pond=hexc("#5F8FA3", 230, WATER),
    pavement=hexc("#D4D3CE", 0, STONE), sidewalk=hexc("#E2E1DC", 0, STONE), road=hexc("#A9AAA8", 0, STONE),
    promenade=hexc("#DCD5C6", 0, STONE), paving2=hexc("#C9C4B8", 0, STONE), sand=hexc("#E6D6B2", 0, SAND),
    grass=hexc("#8FAE6E", 0, FOLIAGE), grass2=hexc("#7FA060", 0, FOLIAGE), hedge=hexc("#56794A", 0, FOLIAGE),
    plaza=hexc("#E3E0D8", 0, STONE), trunk=hexc("#6E5B4A", 0, FOLIAGE), rock=hexc("#9A9893", 30, STONE),
    rock2=hexc("#85837E", 30, STONE), seawall=hexc("#D2CEC4", 0, STONE), coping=hexc("#E4E0D6", 0, STONE),
    flatroof=hexc("#CBCBC6", 0, STONE), slate=hexc("#6F7C86", 60, PAINT), terracotta=hexc("#B07A62", 40, PAINT),
    glass=hexc("#79AFB4", 240, GLASS), tower=hexc("#ECEBE6"), towerdeck=hexc("#5B8C99", 200, GLASS),
    orange=hexc("#D9843F", 120, METAL), blue=hexc("#3D73AE", 120, METAL), awning=hexc("#3F78A8", 110, PAINT),
    parking=hexc("#BDBDB9", 0, STONE), deck=hexc("#B89C7C", 0, PAINT), planks=hexc("#A8875F", 0, PAINT),
    hull=hexc("#F5F4F0", 140, PAINT), cabin=hexc("#DADFE2", 140, PAINT), sail=hexc("#FBFAF5", 0, PAINT),
    bistrowall=hexc("#F2EBDD"), blueroof=hexc("#9DB6C9", 160, METAL), sign=hexc("#2D5F8A", 120, PAINT),
    window=hexc("#4E6A78", 220, GLASS), windowwarm=hexc("#5C7480", 220, GLASS), curtain=hexc("#6D99A8", 235, GLASS),
    frame=hexc("#F7F6F2"), door=hexc("#5D4D40", 40, PAINT), loading=hexc("#878B8E", 120, METAL),
    mech=hexc("#A9AFB3", 150, METAL), tank=hexc("#8E8273", 60, METAL), dash=hexc("#F2F1EC", 0, STONE),
    roofdark=hexc("#B4B4AE", 0, STONE), antenna=hexc("#C9CDD0", 180, METAL), lamp=hexc("#3B4246", 160, METAL),
    bench=hexc("#7A5F46", 0, PAINT), railing=hexc("#E8E6E0", 80, METAL), post=hexc("#4A5054", 120, METAL),
    wallfoot=hexc("#8E8C84", 20, STONE),
)
WALLS = [hexc(c) for c in ("#EEEDE8", "#E4E2DC", "#D9D9D5", "#F2F0EA", "#E3DDD2", "#D3D4D1", "#E8E3DA", "#DADDDA", "#CFCBC2",
                            "#E8DFCF", "#DED3BF", "#E6D8C3", "#D8CDB8")]
TOWER_WALLS = [hexc(c) for c in ("#E9EAE7", "#D3D7D8", "#C7CCCE", "#DEDCD6", "#BFC6C8")]
HOUSE_WALLS = [hexc(c) for c in ("#F2EEE6", "#E8E1D5", "#EFE9E1", "#E2E5E2", "#F5F2EC", "#E3DACB")]
ROOFS = [PAL["slate"], PAL["terracotta"], hexc("#6D7880", 60, PAINT), hexc("#9C6F5C", 40, PAINT), hexc("#7F7770", 40, PAINT)]
AWNINGS = [hexc(c, 70, PAINT) for c in ("#3F78A8", "#B8664D", "#5C8A5A", "#C9963F", "#6F5C8A", "#2F6F73")]
LEAVES = [hexc(c, 0, FOLIAGE) for c in ("#5E8648", "#4F7A3F", "#6E944F", "#5A7F45", "#678C4C", "#4A6F3B")]
STREET_CARS = [hexc(c, 170, PAINT) for c in ("#F2F2EF", "#2E3338", "#B7BCC0", "#9E3B32", "#2F5E8C", "#D9D4C7", "#3F6B4F")]

def to_hex(c):
    return "#%02X%02X%02X" % (c[0], c[1], c[2])

def from_hex(h, g=0, mat=PLASTER):
    return hexc(h, g, mat)

# ---------------- random numbers (xorshift32) ----------------
class Rng:
    def __init__(self, seed):
        self.s = (int(seed) & 0xFFFFFFFF) or 0x9E3779B9
    def next_u32(self):
        x = self.s
        x ^= (x << 13) & 0xFFFFFFFF
        x ^= x >> 17
        x ^= (x << 5) & 0xFFFFFFFF
        self.s = x & 0xFFFFFFFF
        return self.s
    def value(self):
        return (self.next_u32() >> 8) / 16777216.0
    def range(self, a, b):
        return a + (b - a) * self.value()
    def pick(self, items):
        return items[int(self.value() * len(items)) % len(items)]
    def chance(self, p):
        return self.value() < p

# ---------------- 2D helpers ----------------
def area2(pts):
    return sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))

def inside(pts, x, z):
    c = False; n = len(pts)
    for i in range(n):
        (x1, z1), (x2, z2) = pts[i], pts[(i + 1) % n]
        if (z1 > z) != (z2 > z) and x < (x2 - x1) * (z - z1) / (z2 - z1) + x1: c = not c
    return c

def _in_tri(q, a, b, c):
    d1 = (q[0] - b[0]) * (a[1] - b[1]) - (a[0] - b[0]) * (q[1] - b[1])
    d2 = (q[0] - c[0]) * (b[1] - c[1]) - (b[0] - c[0]) * (q[1] - c[1])
    d3 = (q[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (q[1] - a[1])
    return not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0))

def ear_clip(pts):
    p = list(pts)
    if area2(p) < 0: p.reverse()
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

def ccw(pts):
    return list(pts) if area2(pts) > 0 else list(reversed(pts))

def rect_pts(x0, z0, x1, z1):
    return [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]                # counter-clockwise (x right, z up)

def chamfer_pts(x0, z0, x1, z1, c):
    return [(x0 + c, z0), (x1 - c, z0), (x1, z0 + c), (x1, z1 - c), (x1 - c, z1), (x0 + c, z1), (x0, z1 - c), (x0, z0 + c)]

def l_pts(x0, z0, x1, z1, fx, fz, corner):
    """L footprint: the rectangle minus one corner (corner 0..3 = SW, SE, NE, NW) of size fx*w by fz*d."""
    w, d = x1 - x0, z1 - z0; cx, cz = w * fx, d * fz
    if corner == 0: return [(x0 + cx, z0), (x1, z0), (x1, z1), (x0, z1), (x0, z0 + cz), (x0 + cx, z0 + cz)]
    if corner == 1: return [(x0, z0), (x1 - cx, z0), (x1 - cx, z0 + cz), (x1, z0 + cz), (x1, z1), (x0, z1)]
    if corner == 2: return [(x0, z0), (x1, z0), (x1, z1 - cz), (x1 - cx, z1 - cz), (x1 - cx, z1), (x0, z1)]
    return [(x0, z0), (x1, z0), (x1, z1), (x0 + cx, z1), (x0 + cx, z1 - cz), (x0, z1 - cz)]

def inset_pts(pts, d):
    """Offset a convex-ish CCW polygon inward by d (miter)."""
    p = ccw(pts); n = len(p); out = []
    for i in range(n):
        a, b, c = p[i - 1], p[i], p[(i + 1) % n]
        def nrm(u, v):
            dx, dz = v[0] - u[0], v[1] - u[1]; l = math.hypot(dx, dz) or 1
            return (-dz / l, dx / l)                                 # inward normal for CCW
        n1, n2 = nrm(a, b), nrm(b, c)
        mx, mz = n1[0] + n2[0], n1[1] + n2[1]; ml = math.hypot(mx, mz) or 1
        mx, mz = mx / ml, mz / ml
        cos = mx * n1[0] + mz * n1[1] or 1
        out.append((b[0] + mx * d / cos, b[1] + mz * d / cos))
    return out

# ---------------- base shapes ----------------
def _ico():
    t = (1 + math.sqrt(5)) / 2
    v = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    n = math.sqrt(1 + t * t); v = [(a / n, b / n, c / n) for a, b, c in v]
    f = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
         (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    return v, f
ICO_V, ICO_F = _ico()
OCT_V = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]
OCT_F = [(0, 2, 4), (4, 2, 1), (1, 2, 5), (5, 2, 0), (0, 4, 3), (4, 1, 3), (1, 5, 3), (5, 0, 3)]

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

class Builder:
    """Collects triangles per group: groups[g] = list of (a, b, c, colour)."""
    def __init__(self):
        self.groups = {}
        self.colliders = []      # (id, centre xyz, size xyz)
        self.detail = False      # True: triangles go to "<group>_detail" (the high-detail layer phones can switch off)
        self.redirect = None     # e.g. "store_S001_shell": landmarks/trees/props triangles go there instead (per-store meshes)
        self.lift = 0.0          # vertical offset added to everything drawn (store interiors are built from floor height 0)
        self.shelves = []        # clickable shelf zones (store id, shelf id, centre, size), see cityinteriors.py
        self.footprint = None    # last hero building's footprint (x0, z0, x1, z1, floor y), for its interior
        self.hint = None         # where "outside" is for triangles without an explicit direction: None = up,
                                 # ("from", point) = away from that point
    # Every triangle is wound so its normal (cross(b-a, c-a), the same rule Unity uses) points OUT: up for ground
    # and roofs, away from the wall for windows, away from the centre for closed shapes. The lightmapper needs this:
    # a surface whose normal points into the building gathers light from inside and bakes black.
    def tri(self, g, a, b, c, col, d=None, aux=(0, 0, 0)):
        n = ((b[1] - a[1]) * (c[2] - a[2]) - (b[2] - a[2]) * (c[1] - a[1]),
             (b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]),
             (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]))
        if d is None:
            if self.hint is None: d = (0.0, 1.0, 0.0)
            else:
                p = self.hint[1]
                d = ((a[0] + b[0] + c[0]) / 3 - p[0], (a[1] + b[1] + c[1]) / 3 - p[1], (a[2] + b[2] + c[2]) / 3 - p[2])
        if n[0] * d[0] + n[1] * d[1] + n[2] * d[2] < 0: b, c = c, b; aux = (aux[0], aux[2], aux[1])
        if self.redirect and g in ("landmarks", "trees", "props"): g = self.redirect
        if self.lift:
            a, b, c = (a[0], a[1] + self.lift, a[2]), (b[0], b[1] + self.lift, b[2]), (c[0], c[1] + self.lift, c[2])
        if self.detail and not g.endswith("_detail"): g = g + "_detail"
        self.groups.setdefault(g, []).append((a, b, c, col, aux))
    def quad(self, g, a, b, c, d, col, out=None, aux4=(0, 0, 0, 0)):
        self.tri(g, a, b, c, col, out, (aux4[0], aux4[1], aux4[2])); self.tri(g, a, c, d, col, out, (aux4[0], aux4[2], aux4[3]))
    def around(self, point):
        """Set the 'outside' reference for following triangles; returns the previous one (restore with B.hint = prev)."""
        prev = self.hint; self.hint = ("from", point); return prev
    def box(self, g, x0, z0, x1, z1, y0, y1, wall, top=None):
        self.extrude(g, rect_pts(x0, z0, x1, z1), y0, y1, wall, top or wall)
    def extrude(self, g, pts, y0, y1, wall, top=None):
        p = ccw(pts)
        for i in range(len(p)):
            (ax, az), (bx, bz) = p[i], p[(i + 1) % len(p)]
            self.quad(g, (ax, y0, az), (ax, y1, az), (bx, y1, bz), (bx, y0, bz), wall, (bz - az, 0.0, -(bx - ax)))
        if top is not None and top is not False:
            self.polygon(g, p, y1, top)
    def prism(self, g, cx, cz, r0, r1, n, y0, y1, side, top=None, phase=0.0):
        pts = [(math.cos(phase + 2 * math.pi * k / n), math.sin(phase + 2 * math.pi * k / n)) for k in range(n)]
        for k in range(n):
            a, b = pts[k], pts[(k + 1) % n]
            self.quad(g, (cx + a[0] * r0, y0, cz + a[1] * r0), (cx + a[0] * r1, y1, cz + a[1] * r1),
                      (cx + b[0] * r1, y1, cz + b[1] * r1), (cx + b[0] * r0, y0, cz + b[1] * r0), side, (a[0] + b[0], 0.0, a[1] + b[1]))
        if top is not None and top is not False:
            for k in range(n):
                a, b = pts[k], pts[(k + 1) % n]
                self.tri(g, (cx, y1, cz), (cx + b[0] * r1, y1, cz + b[1] * r1), (cx + a[0] * r1, y1, cz + a[1] * r1), top, (0.0, 1.0, 0.0))
    def pyramid(self, g, cx, cz, r, n, y0, y1, col, phase=0.0):
        for k in range(n):
            a = phase + 2 * math.pi * k / n; b = phase + 2 * math.pi * (k + 1) / n
            m = (a + b) / 2
            self.tri(g, (cx + math.cos(a) * r, y0, cz + math.sin(a) * r), (cx, y1, cz), (cx + math.cos(b) * r, y0, cz + math.sin(b) * r), col,
                     (math.cos(m) * (y1 - y0), r, math.sin(m) * (y1 - y0)))
    def gable(self, g, x0, z0, x1, z1, y0, h, roof, gable_wall, along_x=None):
        prev = self.around(((x0 + x1) / 2, y0, (z0 + z1) / 2))
        self._gable(g, x0, z0, x1, z1, y0, h, roof, gable_wall, along_x)
        self.hint = prev
    def _gable(self, g, x0, z0, x1, z1, y0, h, roof, gable_wall, along_x):
        if along_x is None: along_x = (x1 - x0) >= (z1 - z0)
        if along_x:
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
    def hip(self, g, x0, z0, x1, z1, y0, h, roof):
        """Hip roof: four slopes, ridge along the longer side."""
        prev = self.around(((x0 + x1) / 2, y0, (z0 + z1) / 2))
        self._hip(g, x0, z0, x1, z1, y0, h, roof)
        self.hint = prev
    def _hip(self, g, x0, z0, x1, z1, y0, h, roof):
        r2 = shade(roof, .9)
        if (x1 - x0) >= (z1 - z0):
            zm = (z0 + z1) / 2; k = (z1 - z0) / 2
            a, b = (x0 + k, y0 + h, zm), (x1 - k, y0 + h, zm)
            self.quad(g, (x0, y0, z0), a, b, (x1, y0, z0), roof)
            self.quad(g, (x1, y0, z1), b, a, (x0, y0, z1), roof)
            self.tri(g, (x0, y0, z1), a, (x0, y0, z0), r2)
            self.tri(g, (x1, y0, z0), b, (x1, y0, z1), r2)
        else:
            xm = (x0 + x1) / 2; k = (x1 - x0) / 2
            a, b = (xm, y0 + h, z0 + k), (xm, y0 + h, z1 - k)
            self.quad(g, (x0, y0, z1), b, a, (x0, y0, z0), roof)
            self.quad(g, (x1, y0, z0), a, b, (x1, y0, z1), roof)
            self.tri(g, (x0, y0, z0), a, (x1, y0, z0), r2)
            self.tri(g, (x1, y0, z1), b, (x0, y0, z1), r2)
    def polygon(self, g, pts, y, col):
        for a, b, c in ear_clip(pts):
            self.tri(g, (a[0], y, a[1]), (b[0], y, b[1]), (c[0], y, c[1]), col, (0.0, 1.0, 0.0))
    def blob(self, g, rng, cx, cy, cz, rx, ry, rz, col, jitter=0.18, low=False):
        V, F = (OCT_V, OCT_F) if low else (ICO_V, ICO_F)
        verts = [(v[0] * rx * (1 + rng.range(-jitter, jitter)) + cx, v[1] * ry * (1 + rng.range(-jitter, jitter)) + cy,
                  v[2] * rz * (1 + rng.range(-jitter, jitter)) + cz) for v in V]
        for f in F:
            self.tri(g, verts[f[0]], verts[f[1]], verts[f[2]], col, self._from(verts[f[0]], verts[f[1]], verts[f[2]], (cx, cy, cz)))
    def dome(self, g, cx, cy, cz, r, col):
        for a, b, c in DOME_F:
            self.tri(g, (cx + a[0] * r, cy + a[1] * r, cz + a[2] * r), (cx + b[0] * r, cy + b[1] * r, cz + b[2] * r),
                     (cx + c[0] * r, cy + c[1] * r, cz + c[2] * r), col,
                     (a[0] + b[0] + c[0], a[1] + b[1] + c[1] + 0.01, a[2] + b[2] + c[2]))
    @staticmethod
    def _from(a, b, c, centre):
        return ((a[0] + b[0] + c[0]) / 3 - centre[0], (a[1] + b[1] + c[1]) / 3 - centre[1], (a[2] + b[2] + c[2]) / 3 - centre[2])

    # ---------------- facades ----------------
    def panel(self, g, a, b, t0, t1, y0, y1, off, col):
        """A flat rectangle on the wall a->b (CCW footprint edge), between t0..t1 along it, slightly proud of it."""
        dx, dz = b[0] - a[0], b[1] - a[1]; l = math.hypot(dx, dz) or 1
        nx, nz = dz / l * off, -dx / l * off                        # outward offset
        p0 = (a[0] + dx * t0 + nx, a[1] + dz * t0 + nz); p1 = (a[0] + dx * t1 + nx, a[1] + dz * t1 + nz)
        self.quad(g, (p0[0], y0, p0[1]), (p0[0], y1, p0[1]), (p1[0], y1, p1[1]), (p1[0], y0, p1[1]), col, (nx, 0.0, nz))

    def windows(self, g, a, b, y0, floors, floor_h, style, glass, frame=None, width=1.3, height=1.6, spacing=3.2, margin=1.0, first=0, pair_gap=0.0):
        """Windows on one wall, floors first..floors-1. Styles:
        punched  one window per column, sill below (frame colour)       grid  same, small and dense, no sill
        pairs    columns of two windows `pair_gap` apart                 ribbon  one glass band per floor
        strip    narrow floor-high windows (vertical rhythm)             tall  floor-high bays
        The detail layer adds a light frame behind each punched/grid/pairs window."""
        l = math.hypot(b[0] - a[0], b[1] - a[1])
        if l < 2.5: return
        unit = width * 2 + pair_gap if style == "pairs" else width
        n = max(1, int((l - 2 * margin + (spacing - unit)) // spacing))
        used = n * spacing - (spacing - unit); start = (l - used) / 2
        for f in range(first, floors):
            fy = y0 + f * floor_h
            if style == "ribbon":
                self.panel(g, a, b, margin / l, 1 - margin / l, fy + .85, fy + .85 + height, .07, glass)
                continue
            for k in range(n):
                c0 = start + k * spacing
                spans = [(c0, c0 + width), (c0 + width + pair_gap, c0 + 2 * width + pair_gap)] if style == "pairs" else [(c0, c0 + width)]
                for u0, u1 in spans:
                    t0, t1 = u0 / l, u1 / l
                    if style in ("tall", "strip"):
                        wy0, wy1 = fy + .35, fy + floor_h - .35
                    else:
                        wy0, wy1 = fy + (floor_h - height) * .55, fy + (floor_h - height) * .55 + height
                        if style == "punched" and frame is not None:
                            self.panel(g, a, b, t0 - .15 / l, t1 + .15 / l, wy0 - .2, wy0 - .02, .1, frame)   # sill
                    if style in ("punched", "grid", "pairs"):
                        prev = self.detail; self.detail = True
                        self.panel(g, a, b, t0 - .1 / l, t1 + .1 / l, wy0 - .1, wy1 + .1, .045, PAL["frame"])
                        self.detail = prev
                    self.panel(g, a, b, t0, t1, wy0, wy1, .07, glass)

    def pilasters(self, g, a, b, y0, y1, every, col, w=.45, off=.22):
        """Vertical fins (front face only) every `every` metres along the wall, from y0 to y1."""
        l = math.hypot(b[0] - a[0], b[1] - a[1])
        n = int(l // every)
        if n < 1: return
        for k in range(n + 1):
            c = (l - n * every) / 2 + k * every
            c = min(max(c, w / 2), l - w / 2)
            self.panel(g, a, b, (c - w / 2) / l, (c + w / 2) / l, y0, y1, off, col)

    def awning(self, g, a, b, t0, t1, y, depth, drop, col):
        dx, dz = b[0] - a[0], b[1] - a[1]; l = math.hypot(dx, dz) or 1
        nx, nz = dz / l, -dx / l
        p0 = (a[0] + dx * t0, a[1] + dz * t0); p1 = (a[0] + dx * t1, a[1] + dz * t1)
        self.quad(g, (p0[0] + nx * .05, y, p0[1] + nz * .05), (p0[0] + nx * depth, y - drop, p0[1] + nz * depth),
                  (p1[0] + nx * depth, y - drop, p1[1] + nz * depth), (p1[0] + nx * .05, y, p1[1] + nz * .05), col, (0.0, 1.0, 0.0))
        # valance (front strip)
        self.quad(g, (p0[0] + nx * depth, y - drop, p0[1] + nz * depth), (p0[0] + nx * depth, y - drop - .35, p0[1] + nz * depth),
                  (p1[0] + nx * depth, y - drop - .35, p1[1] + nz * depth), (p1[0] + nx * depth, y - drop, p1[1] + nz * depth), shade(col, .85), (nx, 0.0, nz))

# ---------------- which walls face the street ----------------
SIDE_OF_EDGE = {}   # computed per footprint: edge midpoint vs lot rectangle

def street_edges(pts, rect, faces, all_sides=False):
    """Yield (a, b) CCW edges of pts whose outward side is one of `faces` (S/N/W/E of the lot rectangle)."""
    p = ccw(pts); x0, z0, x1, z1 = rect
    for i in range(len(p)):
        a, b = p[i], p[(i + 1) % len(p)]
        if all_sides: yield a, b; continue
        dx, dz = b[0] - a[0], b[1] - a[1]; l = math.hypot(dx, dz) or 1
        nx, nz = dz / l, -dx / l
        side = "E" if nx > .7 else "W" if nx < -.7 else "N" if nz > .7 else "S" if nz < -.7 else None
        if side is None:                     # chamfer: counts if either neighbouring side is a street side
            sx = "E" if nx > 0 else "W"; sz = "N" if nz > 0 else "S"
            if sx in faces or sz in faces: yield a, b
        elif side in faces:
            yield a, b

# ---------------- building types ----------------
FLOOR = 3.2
GROUND_FLOOR = 4.2

def house(B, row, rng):
    x0, z0, x1, z1 = row["rect"]; y = row["base"]; floors = max(1, row["floors"]); wall = row["wall"]; roof = row["roof"]
    faces = row["faces"] or "SNWE"; v = row["variant"]
    h = floors * 3.0
    parts = []
    if v == "lshape" and min(x1 - x0, z1 - z0) > 7:
        if (x1 - x0) >= (z1 - z0):
            zm = z0 + (z1 - z0) * .55
            parts = [(x0, z0, x1, zm), (x0, zm, x0 + (x1 - x0) * .45, z1)]
        else:
            xm = x0 + (x1 - x0) * .55
            parts = [(x0, z0, xm, z1), (xm, z0, x1, z0 + (z1 - z0) * .45)]
    else:
        parts = [(x0, z0, x1, z1)]
    door_done = False
    for (a0, b0, a1, b1) in parts:
        B.box("buildings", a0, b0, a1, b1, y, y + h, wall)
        pitch = min(a1 - a0, b1 - b0) * rng.range(.32, .42)
        if v == "hip": B.hip("buildings", a0 - .45, b0 - .45, a1 + .45, b1 + .45, y + h, pitch, roof)
        else: B.gable("buildings", a0 - .45, b0 - .45, a1 + .45, b1 + .45, y + h, pitch, roof, wall)
        for a, b in street_edges(rect_pts(a0, b0, a1, b1), (x0, z0, x1, z1), faces):
            B.windows("buildings", a, b, y, floors, 3.0, "punched", PAL["windowwarm"], PAL["frame"], width=1.0, height=1.2, spacing=2.7, margin=1.1)
            if not door_done:
                l = math.hypot(b[0] - a[0], b[1] - a[1]); t = .5 - .5 / l
                B.panel("buildings", a, b, t, t + 1.0 / l, y, y + 2.1, .08, PAL["door"]); door_done = True
    if rng.chance(.4):
        cx = rng.range(x0 + 1.2, x1 - 1.9); cz = rng.range(z0 + 1.2, z1 - 1.9)
        B.box("buildings", cx, cz, cx + .7, cz + .7, y + h, y + h + min(x1 - x0, z1 - z0) * .42 + .9, shade(wall, .82), PAL["roofdark"])

def rooftop(B, rng, pts, y, big=False):
    xs = [p[0] for p in pts]; zs = [p[1] for p in pts]
    x0, x1, z0, z1 = min(xs) + 1.5, max(xs) - 1.5, min(zs) + 1.5, max(zs) - 1.5
    if x1 - x0 < 3 or z1 - z0 < 3: return
    for _ in range(rng.pick([0, 1, 1, 2, 3])):
        ux, uz = rng.range(x0, x1 - 1.8), rng.range(z0, z1 - 1.4)
        B.box("buildings", ux, uz, ux + 1.8, uz + 1.4, y, y + 1.0, PAL["mech"])
    if rng.chance(.35 if not big else .2):
        tx, tz = rng.range(x0 + 1.4, x1 - 1.4), rng.range(z0 + 1.4, z1 - 1.4)
        for k in range(3):                                           # tower legs
            a = 2 * math.pi * k / 3
            B.box("buildings", tx + math.cos(a) * .9 - .08, tz + math.sin(a) * .9 - .08, tx + math.cos(a) * .9 + .08, tz + math.sin(a) * .9 + .08, y, y + 1.6, PAL["trunk"], None)
        B.prism("buildings", tx, tz, 1.25, 1.25, 6, y + 1.6, y + 3.6, PAL["tank"], None)
        B.pyramid("buildings", tx, tz, 1.35, 6, y + 3.6, y + 4.3, shade(PAL["tank"], .85))
    if rng.chance(.4):
        sx, sz = rng.range(x0, x1 - 2.6), rng.range(z0, z1 - 2.6)
        B.box("buildings", sx, sz, sx + 2.6, sz + 2.6, y, y + 2.6, PAL["roofdark"], PAL["mech"])

def footprint(v, x0, z0, x1, z1, rng):
    if v == "chamfer": return chamfer_pts(x0, z0, x1, z1, min(x1 - x0, z1 - z0) * .16)
    if v == "lshape" and min(x1 - x0, z1 - z0) > 12:
        return l_pts(x0, z0, x1, z1, rng.range(.38, .5), rng.range(.38, .5), rng.pick([0, 1, 2, 3]))
    return rect_pts(x0, z0, x1, z1)

# Facade schemes (after the painted city: every building reads differently). Picked per building from its seed.
FACADES = {
    "grid3":  dict(style="punched", width=1.6, height=1.7, spacing=3.6),     # few big windows (3 across on a small face)
    "dense":  dict(style="grid", width=1.0, height=1.5, spacing=1.75),       # many small windows
    "pairs":  dict(style="pairs", width=.95, height=1.6, spacing=3.9, pair_gap=.45),
    "ribbon": dict(style="ribbon", height=1.3),                              # glass band, concrete band
    "band2":  dict(style="ribbon", height=1.9),                              # deeper glass band, thin spandrel
    "strip":  dict(style="strip", width=.8, spacing=1.7),                   # narrow floor-high windows
    "bays":   dict(style="grid", width=1.1, height=1.6, spacing=2.0, pilaster=4.3),   # windows in bays between fins
}
MIDRISE_FACADES = {"grid": ["grid3", "dense", "pairs", "bays", "dense"], "ribbon": ["ribbon", "band2"],
                   "chamfer": ["dense", "strip", "grid3"], "lshape": ["pairs", "grid3", "bays", "dense"]}

def facade(B, rng, a, b, y0, floors, scheme, wall, first=1):
    f = dict(FACADES[scheme]); pil = f.pop("pilaster", None); style = f.pop("style")
    B.windows("buildings", a, b, y0, floors, FLOOR, style, PAL["window"], PAL["frame"], margin=1.0, first=first, **f)
    if pil: B.pilasters("buildings", a, b, y0 + first * FLOOR, y0 + floors * FLOOR, pil, shade(wall, 1.05))

def parapet(B, pts, y, h, col):
    """Roof edge: a low wall just proud of the facade (outer face, cap and inner face) above a roof at height y."""
    outer = inset_pts(pts, -.25); inner = inset_pts(pts, .15)
    B.extrude("buildings", outer, y - .15, y + h, col, None)
    n = len(outer); cx = sum(p[0] for p in pts) / n; cz = sum(p[1] for p in pts) / n
    for i in range(n):
        j = (i + 1) % n
        B.quad("buildings", (outer[i][0], y + h, outer[i][1]), (outer[j][0], y + h, outer[j][1]), (inner[j][0], y + h, inner[j][1]), (inner[i][0], y + h, inner[i][1]), shade(col, 1.04))
        mx, mz = (inner[i][0] + inner[j][0]) / 2, (inner[i][1] + inner[j][1]) / 2
        B.quad("buildings", (inner[i][0], y, inner[i][1]), (inner[i][0], y + h, inner[i][1]), (inner[j][0], y + h, inner[j][1]), (inner[j][0], y, inner[j][1]), shade(col, .92), (cx - mx, 0.0, cz - mz))

def midrise(B, row, rng):
    x0, z0, x1, z1 = row["rect"]; y = row["base"]; floors = max(2, row["floors"]); wall = row["wall"]; roof = row["roof"]
    faces = row["faces"] or "SNWE"; v = row["variant"]; rect = (x0, z0, x1, z1)
    pts = footprint(v, x0, z0, x1, z1, rng)
    gf = GROUND_FLOOR; top = y + gf + (floors - 1) * FLOOR
    base_c = shade(wall, .86)
    B.extrude("buildings", pts, y, y + gf, base_c, None)
    B.extrude("buildings", pts, y + gf, top, wall, roof)
    scheme = rng.pick(MIDRISE_FACADES.get(v, ["grid3"]))
    awning_c = rng.pick(AWNINGS); want_awning = rng.chance(.55)
    street = set(street_edges(pts, rect, faces))
    for a, b in street_edges(pts, rect, faces, all_sides=True):
        l = math.hypot(b[0] - a[0], b[1] - a[1])
        if (a, b) in street and l > 4:
            # Shopfront: glass bays with a pier between.
            bays = max(1, int((l - 1.2) // 4.2))
            for k in range(bays):
                t0 = (0.6 + k * (l - 1.2) / bays + .35) / l; t1 = (0.6 + (k + 1) * (l - 1.2) / bays - .35) / l
                B.panel("buildings", a, b, t0, t1, y + .35, y + 3.0, .07, PAL["window"])
            if want_awning: B.awning("buildings", a, b, .6 / l, 1 - .6 / l, y + 3.45, 1.5, .45, awning_c)
        # Upper floors: the building's scheme on every wall (back and side walls show above lower neighbours).
        facade(B, rng, a, b, y + gf - FLOOR, floors, scheme, wall)
    parapet(B, pts, top, .7, shade(wall, .95))
    rooftop(B, rng, pts, top)

def tower(B, row, rng):
    x0, z0, x1, z1 = row["rect"]; y = row["base"]; floors = max(8, row["floors"]); wall = row["wall"]; roof = row["roof"]
    faces = row["faces"] or "SNWE"; v = row["variant"]; rect = (x0, z0, x1, z1)
    pod_f = rng.pick([2, 2, 3])
    pod = rect_pts(x0, z0, x1, z1); pod_top = y + GROUND_FLOOR + (pod_f - 1) * FLOOR
    B.extrude("buildings", pod, y, pod_top, shade(wall, .88), PAL["flatroof"])
    for a, b in street_edges(pod, rect, faces):
        l = math.hypot(b[0] - a[0], b[1] - a[1])
        B.panel("buildings", a, b, .8 / l, 1 - .8 / l, y + .35, y + 3.2, .07, PAL["window"])
        B.windows("buildings", a, b, y + GROUND_FLOOR - FLOOR, pod_f, FLOOR, "ribbon", PAL["window"], height=1.4, margin=1.0, first=1)
    ins = min(x1 - x0, z1 - z0) * rng.range(.16, .24)
    bx0, bz0, bx1, bz1 = x0 + ins, z0 + ins, x1 - ins, z1 - ins
    body = chamfer_pts(bx0, bz0, bx1, bz1, min(bx1 - bx0, bz1 - bz0) * .2) if v == "chamfer" else rect_pts(bx0, bz0, bx1, bz1)
    body_f = floors - pod_f
    split = body_f if v != "setback" else int(body_f * rng.range(.6, .75))
    def body_part(pts, f0, f1):
        yb, yt = pod_top + f0 * FLOOR, pod_top + f1 * FLOOR
        if v == "glass":
            B.extrude("buildings", pts, yb, yt, PAL["curtain"], roof)
            for a, b in street_edges(pts, rect, faces, all_sides=True):      # slab lines every floor
                for f in range(f0, f1):
                    fy = pod_top + f * FLOOR
                    B.panel("buildings", a, b, 0, 1, fy, fy + .35, .06, wall)
            for a, b in street_edges(pts, rect, faces, all_sides=True):      # vertical mullions
                B.pilasters("buildings", a, b, yb, yt, 1.6, shade(wall, 1.02), w=.14, off=.08)
        else:
            B.extrude("buildings", pts, yb, yt, wall, roof)
            scheme = {"ribbon": "band2", "chamfer": "ribbon", "tall": "strip", "setback": "dense"}.get(v, "dense")
            for a, b in street_edges(pts, rect, faces, all_sides=True):
                facade(B, rng, a, b, pod_top, f1, scheme, wall, first=f0)
        return yt
    top = body_part(body, 0, split)
    if split < body_f:
        upper = inset_pts(body, min(bx1 - bx0, bz1 - bz0) * .14)
        B.extrude("buildings", inset_pts(body, -.2), top, top + .4, shade(wall, .92), roof)
        top = body_part(upper, split, body_f)
        body = upper
    # Crown: band, mechanical penthouse, sometimes an antenna.
    parapet(B, body, top, .9, shade(wall, .9) if v != "glass" else wall)
    xs = [p[0] for p in body]; zs = [p[1] for p in body]
    cx, cz = (min(xs) + max(xs)) / 2, (min(zs) + max(zs)) / 2; hw = (max(xs) - min(xs)) * .28; hd = (max(zs) - min(zs)) * .28
    B.box("buildings", cx - hw, cz - hd, cx + hw, cz + hd, top, top + 3.0, PAL["roofdark"], PAL["mech"])
    if rng.chance(.45):
        B.box("buildings", cx - .15, cz - .15, cx + .15, cz + .15, top + 3.0, top + 3.0 + rng.range(5, 10), PAL["antenna"], None)

def warehouse(B, row, rng):
    x0, z0, x1, z1 = row["rect"]; y = row["base"]; wall = row["wall"]; roof = row["roof"]; faces = row["faces"] or "SNWE"
    h = 6.5 + 1.2 * max(0, row["floors"] - 1); rect = (x0, z0, x1, z1)
    B.box("buildings", x0, z0, x1, z1, y, y + h, wall, None)
    prev = B.around(((x0 + x1) / 2, y + h - 3, (z0 + z1) / 2))
    # Sawtooth roof: teeth run along the longer side, glazing faces north (+z) or east (+x).
    along_x = (x1 - x0) >= (z1 - z0)
    n = max(2, int(((z1 - z0) if along_x else (x1 - x0)) // 5))
    th = 2.2
    for k in range(n):
        if along_x:
            a, b = z0 + (z1 - z0) * k / n, z0 + (z1 - z0) * (k + 1) / n
            B.quad("buildings", (x0, y + h, a), (x0, y + h + th, b), (x1, y + h + th, b), (x1, y + h, a), roof)
            B.quad("buildings", (x0, y + h, b), (x0, y + h + th, b), (x1, y + h + th, b), (x1, y + h, b), PAL["curtain"])
            B.tri("buildings", (x0, y + h, a), (x0, y + h + th, b), (x0, y + h, b), wall)
            B.tri("buildings", (x1, y + h, a), (x1, y + h + th, b), (x1, y + h, b), wall)
        else:
            a, b = x0 + (x1 - x0) * k / n, x0 + (x1 - x0) * (k + 1) / n
            B.quad("buildings", (a, y + h, z0), (b, y + h + th, z0), (b, y + h + th, z1), (a, y + h, z1), roof)
            B.quad("buildings", (b, y + h, z0), (b, y + h + th, z0), (b, y + h + th, z1), (b, y + h, z1), PAL["curtain"])
            B.tri("buildings", (a, y + h, z0), (b, y + h + th, z0), (b, y + h, z0), wall)
            B.tri("buildings", (a, y + h, z1), (b, y + h + th, z1), (b, y + h, z1), wall)
    B.hint = prev
    for a, b in street_edges(rect_pts(x0, z0, x1, z1), rect, faces):
        l = math.hypot(b[0] - a[0], b[1] - a[1])
        doors = max(1, int(l // 9))
        for k in range(doors):
            t = (k + .5) / doors
            B.panel("buildings", a, b, t - 2.0 / l, t + 2.0 / l, y, y + 4.4, .08, PAL["loading"])
        B.panel("buildings", a, b, .8 / l, 1 - .8 / l, y + h - 1.6, y + h - .7, .07, PAL["window"])
        B.panel("buildings", a, b, 0, 1, y + h - .3, y + h, .1, shade(wall, .8))

BUILDERS = {"house": house, "midrise": midrise, "tower": tower, "warehouse": warehouse}

# ---------------- trees ----------------
def tree(B, rng, x, y, z, scale, kind="round"):
    """small: street tree (14 tris). round: park tree (26). poplar: tall (22). broad: 2-3 overlapping crowns (46-66).
    bush: low shrub on the ground (8)."""
    leaf = rng.pick(LEAVES)
    if kind == "bush":
        r = rng.range(.9, 1.4) * scale
        B.blob("trees", rng, x, y + r * .45, z, r, r * .6, r, shade(leaf, 1.05), .25, low=True)
        return
    if kind == "small":
        h = rng.range(1.3, 1.7) * scale
        B.prism("trees", x, z, .22 * scale, .16 * scale, 3, y, y + h, PAL["trunk"], None, rng.range(0, 2))
        r = rng.range(1.6, 2.1) * scale
        B.blob("trees", rng, x, y + h + r * .6, z, r, r * .9, r, leaf, .2, low=True)
    elif kind == "poplar":
        h = rng.range(1.0, 1.4) * scale
        B.prism("trees", x, z, .22 * scale, .16 * scale, 3, y, y + h, PAL["trunk"], None, rng.range(0, 2))
        r = rng.range(1.3, 1.7) * scale
        B.blob("trees", rng, x, y + h + r * 1.4, z, r, r * 1.6, r, leaf, .15, low=True)
        B.blob("trees", rng, x, y + h + r * 3.4, z, r * .7, r * 1.1, r * .7, shade(leaf, 1.06), .15, low=True)
    elif kind == "broad":
        h = rng.range(1.6, 2.2) * scale
        B.prism("trees", x, z, .32 * scale, .22 * scale, 3, y, y + h, PAL["trunk"], None, rng.range(0, 2))
        r = rng.range(2.4, 3.0) * scale
        B.blob("trees", rng, x, y + h + r * .6, z, r, r * .75, r, leaf)
        for k in range(rng.pick([1, 2])):
            a = rng.range(0, 6.28); d = r * .7
            B.blob("trees", rng, x + math.cos(a) * d, y + h + r * rng.range(.35, .7), z + math.sin(a) * d, r * .7, r * .55, r * .7, shade(leaf, rng.range(.92, 1.08)))
    else:
        h = rng.range(1.4, 2.0) * scale
        B.prism("trees", x, z, .28 * scale, .2 * scale, 3, y, y + h, PAL["trunk"], None, rng.range(0, 2))
        r = rng.range(2.2, 3.0) * scale
        B.blob("trees", rng, x, y + h + r * .55, z, r, r * .8, r, leaf)

# ---------------- props ----------------
def obox(B, g, cx, cz, w, d, y0, y1, angle, col, top=None):
    """Box rotated about the vertical axis (angle in radians, w along the angle direction)."""
    ca, sa = math.cos(angle), math.sin(angle)
    pts = [(cx + ca * u - sa * v, cz + sa * u + ca * v) for u, v in ((-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2))]
    B.extrude(g, pts, y0, y1, col, top or col)

def rock_cluster(B, rng, x, y, z, size):
    """Two to four rounded boulders (subdivided-looking 20-face blobs, flattened), in two stone shades."""
    for k in range(rng.pick([2, 3, 3, 4])):
        r = size * rng.range(.45, 1.0)
        a = rng.range(0, 6.28); d = size * rng.range(0, .9)
        B.blob("ground", rng, x + math.cos(a) * d, y + r * .25, z + math.sin(a) * d, r, r * rng.range(.45, .7), r * rng.range(.8, 1.1),
               rng.pick([PAL["rock"], PAL["rock2"], shade(PAL["rock"], 1.08)]), .22)

def car(B, rng, x, z, angle, y):
    col = rng.pick(STREET_CARS)
    obox(B, "props", x, z, 4.2, 1.8, y + .25, y + .95, angle, col)
    ca, sa = math.cos(angle), math.sin(angle)
    off = rng.range(-.3, -.1)
    obox(B, "props", x + ca * off, z + sa * off, 2.2, 1.6, y + .95, y + 1.5, angle, PAL["window"], shade(col, .95))

def lamp(B, x, z, y):
    B.box("props", x - .08, z - .08, x + .08, z + .08, y, y + 4.2, PAL["lamp"])
    B.box("props", x - .3, z - .3, x + .3, z + .3, y + 4.2, y + 4.45, PAL["lamp"], hexc("#FFF6D8", 200, GLASS))

def bench(B, x, z, y, angle):
    obox(B, "props", x, z, 1.8, .5, y + .4, y + .5, angle, PAL["bench"])
    obox(B, "props", x, z, 1.6, .4, y, y + .4, angle, PAL["lamp"])

# ---------------- curves ----------------
def catmull_rom(points, closed=True, samples=6):
    """Smooth curve through the points (centripetal-free uniform Catmull-Rom). Returns the sampled points."""
    n = len(points); out = []
    rng_ = range(n) if closed else range(n - 1)
    for i in rng_:
        p0 = points[(i - 1) % n] if closed or i > 0 else points[0]
        p1 = points[i]; p2 = points[(i + 1) % n]
        p3 = points[(i + 2) % n] if closed or i + 2 < n else points[-1]
        for k in range(samples):
            t = k / samples; t2 = t * t; t3 = t2 * t
            out.append(tuple(.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2 +
                                   (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in range(len(p1))))
    if not closed: out.append(points[-1])
    return out

def polyline_normals(pts, closed=True, outward_ccw=True):
    """Unit normals at each point (average of neighbouring segment normals). For a CCW loop they point outward."""
    n = len(pts); res = []
    for i in range(n):
        a = pts[i - 1] if (closed or i > 0) else pts[i]
        b = pts[(i + 1) % n] if (closed or i < n - 1) else pts[i]
        dx, dz = b[0] - a[0], b[1] - a[1]; l = math.hypot(dx, dz) or 1
        nx, nz = dz / l, -dx / l
        res.append((nx, nz) if outward_ccw else (-nx, -nz))
    return res

def ribbon(B, g, pts, normals, off0, off1, y0, y1, col, closed=True, aux=(0, 0), out=(0.0, 1.0, 0.0)):
    """A strip between two offset curves (offset along the normals), heights y0 at off0 and y1 at off1."""
    n = len(pts)
    for i in range(n if closed else n - 1):
        j = (i + 1) % n
        a0 = (pts[i][0] + normals[i][0] * off0, y0, pts[i][1] + normals[i][1] * off0)
        a1 = (pts[i][0] + normals[i][0] * off1, y1, pts[i][1] + normals[i][1] * off1)
        b0 = (pts[j][0] + normals[j][0] * off0, y0, pts[j][1] + normals[j][1] * off0)
        b1 = (pts[j][0] + normals[j][0] * off1, y1, pts[j][1] + normals[j][1] * off1)
        B.quad(g, a0, a1, b1, b0, col, out, (aux[0], aux[1], aux[1], aux[0]))

def wall_strip(B, g, pts, normals, off, y0, y1, col, closed=True, facing=1.0):
    """A vertical wall along the curve at a fixed offset, facing along +normal (facing=1) or -normal."""
    n = len(pts)
    for i in range(n if closed else n - 1):
        j = (i + 1) % n
        a = (pts[i][0] + normals[i][0] * off, pts[i][1] + normals[i][1] * off)
        b = (pts[j][0] + normals[j][0] * off, pts[j][1] + normals[j][1] * off)
        nx, nz = (normals[i][0] + normals[j][0]) * facing, (normals[i][1] + normals[j][1]) * facing
        B.quad(g, (a[0], y0, a[1]), (a[0], y1, a[1]), (b[0], y1, b[1]), (b[0], y0, b[1]), col, (nx, 0.0, nz))
