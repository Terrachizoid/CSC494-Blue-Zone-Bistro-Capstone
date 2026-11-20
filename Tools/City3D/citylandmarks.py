"""Hero models for the five places the game is played around (after the painted city, Art/UI/cityBackground.png):
Verdant & Co. (S001), CheapCart (S002), MegaBulk Club (S003), the News Tower and the Blue Zone Bistro.

Each builder gets the landmark's plot (already inset from the lot) and the ground height, draws into the
"landmarks" group (signs, frames and small props partly into the detail layer) and returns the roof height
for the click collider. Names are spelled in 3D block letters (5x7 font) so they read like the painting.
"""
import math
from cityparts import PAL, hexc, shade, tree, car, obox, rect_pts, inset_pts, GLASS, METAL, PAINT, STONE

G = "landmarks"

HERO = dict(
    v_wall=hexc("#E9DFCB"), v_trim=hexc("#F6F1E6"), v_green=hexc("#2F6B45", 120, PAINT), v_glass=hexc("#7FB3AE", 240, GLASS),
    v_rib=hexc("#E8EEEA", 120, METAL), v_roof=hexc("#D3CDBF", 0, STONE),
    c_wall=hexc("#E6DCC9"), c_orange=hexc("#D99A45", 90, PAINT), c_brown=hexc("#A44E1C", 100, PAINT), c_roof=hexc("#D6D3CC", 0, STONE),
    c_sky=hexc("#9DB4C2", 230, GLASS),
    m_wall=hexc("#E3DED3"), m_panel=hexc("#EDE5D3"), m_brick=hexc("#B9744F", 20, STONE), m_blue=hexc("#2D5D9F", 120, PAINT),
    m_roof=hexc("#93A9BF", 170, METAL), m_seam=hexc("#7F96AD", 170, METAL), m_door=hexc("#A7ACB0", 130, METAL),
    m_office=hexc("#D8D6CF"), truck=hexc("#F1F1EE", 150, PAINT), cab=hexc("#D7DCE0", 150, PAINT),
    t_white=hexc("#EEEDE8", 60, PAINT), t_glass=hexc("#3F7391", 230, GLASS), t_band=hexc("#DADDDD", 80, PAINT),
    t_red=hexc("#C9473B", 90, PAINT),
    b_wall=hexc("#F1E6D2"), b_trim=hexc("#FBF6EC"), b_navy=hexc("#1E3A5C", 110, PAINT), b_teal=hexc("#2F6F7C", 90, PAINT),
    b_roof=hexc("#DCD6C9", 0, STONE), umbrella=hexc("#FBFAF5", 0, PAINT), chair=hexc("#5A4A3C", 40, PAINT),
    planter=hexc("#CFC6B5", 0, STONE),
)

# ---------------- 5x7 block font (caps) ----------------
FONT = {
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "B": ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
    "C": [".####", "#....", "#....", "#....", "#....", "#....", ".####"],
    "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "G": [".####", "#....", "#....", "#.###", "#...#", "#...#", ".###."],
    "H": ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "I": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####"],
    "K": ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "M": ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    "N": ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "V": ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"],
    "F": ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
    "X": ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    "Z": ["#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"],
    "-": [".....", ".....", ".....", ".###.", ".....", ".....", "....."],
    " ": ["....."] * 7,
}

def _wall(a, b, off):
    l = math.hypot(b[0] - a[0], b[1] - a[1]) or 1
    u = ((b[0] - a[0]) / l, (b[1] - a[1]) / l); n = (u[1], -u[0])          # outward normal of a CCW edge
    return l, u, n, (lambda s, y, o=off: (a[0] + u[0] * s + n[0] * o, y, a[1] + u[1] * s + n[1] * o))

def text_width(text, h):
    return (len(text) * 6 - 1) * h / 7

def text3d(B, a, b, text, t_center, y0, h, col, off=.2, depth=.12, board=None):
    """Block letters on wall a->b (CCW edge, read from outside), centred at t_center (0..1), cap height h, bottom y0.
    Each run of pixels in a row is one slab with a front face and top/bottom faces (so they catch light from above)."""
    l, u, n, P = _wall(a, b, off)
    px = h / 7; w = text_width(text, h); s0 = t_center * l - w / 2
    out = (n[0], 0.0, n[1])
    if board is not None:
        pad = px * 1.4; _, _, _, Q = _wall(a, b, off - depth - .02)
        B.quad(G, Q(s0 - pad, y0 - pad), Q(s0 - pad, y0 + h + pad), Q(s0 + w + pad, y0 + h + pad), Q(s0 + w + pad, y0 - pad), board, out)
    _, _, _, Pb = _wall(a, b, off - depth)
    for ci, ch in enumerate(text.upper()):
        rows = FONT.get(ch, FONT[" "])
        for r, row in enumerate(rows):
            ytop = y0 + h - r * px; ybot = ytop - px
            c = 0
            while c < 5:
                if row[c] != "#": c += 1; continue
                c1 = c
                while c1 < 5 and row[c1] == "#": c1 += 1
                sa = s0 + (ci * 6 + c) * px; sb = s0 + (ci * 6 + c1) * px
                B.quad(G, P(sa, ybot), P(sa, ytop), P(sb, ytop), P(sb, ybot), col, out)
                B.quad(G, Pb(sa, ytop), P(sa, ytop), P(sb, ytop), Pb(sb, ytop), shade(col, 1.15), (0.0, 1.0, 0.0))
                c = c1

def wall_rect(B, a, b, s0, s1, y0, y1, off, col, g=G):
    l, u, n, P = _wall(a, b, off)
    B.quad(g, P(s0, y0), P(s0, y1), P(s1, y1), P(s1, y0), col, (n[0], 0.0, n[1]))

def arch_window(B, a, b, s0, s1, y0, y1, off, col, frame=None, segs=5):
    """Round-headed window between s0..s1 (metres along the wall), springing at y1 - half width."""
    l, u, n, P = _wall(a, b, off); out = (n[0], 0.0, n[1])
    r = (s1 - s0) / 2; cs = (s0 + s1) / 2; spring = y1 - r
    if frame is not None:
        prev = B.detail; B.detail = True
        _, _, _, F = _wall(a, b, off - .03)
        fr = r + .18
        B.quad(G, F(cs - fr, y0 - .18), F(cs - fr, spring), F(cs + fr, spring), F(cs + fr, y0 - .18), frame, out)
        for k in range(segs):
            t0, t1 = math.pi * k / segs, math.pi * (k + 1) / segs
            B.tri(G, F(cs, spring), F(cs + math.cos(t0) * fr, spring + math.sin(t0) * fr), F(cs + math.cos(t1) * fr, spring + math.sin(t1) * fr), frame, out)
        B.detail = prev
    B.quad(G, P(s0, y0), P(s0, spring), P(s1, spring), P(s1, y0), col, out)
    for k in range(segs):
        t0, t1 = math.pi * k / segs, math.pi * (k + 1) / segs
        B.tri(G, P(cs, spring), P(cs + math.cos(t0) * r, spring + math.sin(t0) * r), P(cs + math.cos(t1) * r, spring + math.sin(t1) * r), col, out)

def barrel(B, x0, x1, cz, r, y, col, axis="x", segs=8, rib=None, rib_every=3.0, ends=True):
    """Half-cylinder vault from x0..x1 (axis x) or z0..z1 (axis z, x0/x1 are then z bounds, cz the x centre)."""
    def V(s, ang, rr=r):
        h, c = math.sin(ang) * rr, math.cos(ang) * rr
        return (s, y + h, cz + c) if axis == "x" else (cz + c, y + h, s)
    for k in range(segs):
        a0, a1 = math.pi * k / segs, math.pi * (k + 1) / segs
        am = (a0 + a1) / 2
        out = (0.0, math.sin(am), math.cos(am)) if axis == "x" else (math.cos(am), math.sin(am), 0.0)
        B.quad(G, V(x0, a0), V(x0, a1), V(x1, a1), V(x1, a0), col, out)
        if rib is not None:
            n = max(1, int((x1 - x0) // rib_every))
            for j in range(n + 1):
                s = x0 + (x1 - x0) * j / n
                B.quad(G, V(s - .12, a0, r + .06), V(s - .12, a1, r + .06), V(s + .12, a1, r + .06), V(s + .12, a0, r + .06), rib, out)
    if ends:
        for s, sgn in ((x0, -1.0), (x1, 1.0)):
            out = (sgn, 0.0, 0.0) if axis == "x" else (0.0, 0.0, sgn)
            for k in range(segs):
                a0, a1 = math.pi * k / segs, math.pi * (k + 1) / segs
                ctr = (s, y, cz) if axis == "x" else (cz, y, s)
                B.tri(G, ctr, V(s, a0), V(s, a1), col, out)
            if rib is not None:                                                     # radial glazing bars on the end
                for k in range(1, 4):
                    ang = math.pi * k / 4
                    p0 = V(s + sgn * .03, ang, .2); p1 = V(s + sgn * .03, ang, r)
                    d = (.1 if axis == "x" else 0, 0, 0 if axis == "x" else .1)
                    B.quad(G, p0, p1, (p1[0] + d[2], p1[1], p1[2] + d[0]), (p0[0] + d[2], p0[1], p0[2] + d[0]), rib, out)

def leg(B, bx, bz, by, tx, tz, ty, r0, r1, col):
    """Square-section strut lofted from (bx,by,bz) to (tx,ty,tz)."""
    prev = B.around(((bx + tx) / 2, (by + ty) / 2, (bz + tz) / 2))
    bot = [(bx - r0, bz - r0), (bx + r0, bz - r0), (bx + r0, bz + r0), (bx - r0, bz + r0)]
    top = [(tx - r1, tz - r1), (tx + r1, tz - r1), (tx + r1, tz + r1), (tx - r1, tz + r1)]
    for i in range(4):
        j = (i + 1) % 4
        B.quad(G, (bot[i][0], by, bot[i][1]), (top[i][0], ty, top[i][1]), (top[j][0], ty, top[j][1]), (bot[j][0], by, bot[j][1]), col, None)
    B.hint = prev

def sides(x0, z0, x1, z1):
    """CCW edges keyed by the side they face."""
    return dict(S=((x0, z0), (x1, z0)), E=((x1, z0), (x1, z1)), N=((x1, z1), (x0, z1)), W=((x0, z1), (x0, z0)))

def parapet_box(B, x0, z0, x1, z1, y, h, col, cap=None):
    """Roof edge wall around a flat roof at height y."""
    cap = cap or shade(col, 1.05)
    B.extrude(G, rect_pts(x0 - .2, z0 - .2, x1 + .2, z1 + .2), y - .2, y + h, col, None)
    t = .35
    for (ax, az, bx, bz) in ((x0 - .2, z0 - .2, x1 + .2, z0 + t), (x0 - .2, z1 - t, x1 + .2, z1 + .2),
                             (x0 - .2, z0 + t, x0 + t, z1 - t), (x1 - t, z0 + t, x1 + .2, z1 - t)):
        B.quad(G, (ax, y + h, az), (ax, y + h, bz), (bx, y + h, bz), (bx, y + h, az), cap, (0.0, 1.0, 0.0))
    prev = B.around(((x0 + x1) / 2, y + h * 2, (z0 + z1) / 2))
    B.hint = ("from", ((x0 + x1) / 2, y, (z0 + z1) / 2))
    ix0, iz0, ix1, iz1 = x0 + t, z0 + t, x1 - t, z1 - t
    for (a, b) in (((ix0, iz0), (ix1, iz0)), ((ix1, iz0), (ix1, iz1)), ((ix1, iz1), (ix0, iz1)), ((ix0, iz1), (ix0, iz0))):
        mx, mz = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        B.quad(G, (a[0], y, a[1]), (a[0], y + h, a[1]), (b[0], y + h, b[1]), (b[0], y, b[1]), shade(col, .9),
               (((x0 + x1) / 2) - mx, 0.0, ((z0 + z1) / 2) - mz))
    B.hint = prev

def roof_unit(B, x, z, w, d, y, h, body, top=None):
    B.box(G, x - w / 2, z - d / 2, x + w / 2, z + d / 2, y, y + h, body, top or shade(body, 1.08))

def planter_tree(B, rng, x, z, y, kind="round", scale=.8):
    B.box(G, x - .8, z - .8, x + .8, z + .8, y, y + .5, HERO["planter"], PAL["hedge"])
    tree(B, rng, x, y + .5, z, scale, kind)

def truck(B, x, z, y, angle):
    """Box truck: cab + cargo box + wheels hint."""
    ca, sa = math.cos(angle), math.sin(angle)
    obox(B, G, x - ca * 1.2, z - sa * 1.2, 7.0, 2.5, y + .7, y + 3.6, angle, HERO["truck"])
    obox(B, G, x + ca * 3.3, z + sa * 3.3, 2.2, 2.4, y + .7, y + 2.8, angle, HERO["cab"])
    obox(B, G, x + ca * 4.25, z + sa * 4.25, .3, 2.0, y + 1.7, y + 2.6, angle, PAL["window"])
    obox(B, G, x + ca * .8, z + sa * .8, 10.2, 2.3, y + .3, y + .75, angle, PAL["lamp"])

def table_set(B, x, z, y, umbrella=True):
    B.prism(G, x, z, .65, .65, 6, y + .7, y + .8, PAL["bench"], PAL["bench"])
    B.prism(G, x, z, .08, .08, 4, y, y + .7, PAL["lamp"], False)
    prev = B.detail; B.detail = True
    for k in range(3):
        ang = k * 2 * math.pi / 3 + .4
        cx, cz = x + math.cos(ang) * 1.05, z + math.sin(ang) * 1.05
        B.box(G, cx - .22, cz - .22, cx + .22, cz + .22, y, y + .45, HERO["chair"])
    B.detail = prev
    if umbrella:
        B.prism(G, x, z, .05, .05, 4, y + .8, y + 2.4, PAL["lamp"], False)
        B.pyramid(G, x, z, 1.7, 8, y + 2.1, y + 2.85, HERO["umbrella"])

def parking(B, rng, x0, z0, x1, z1, y, rows=1, car_p=.5, along="x"):
    """Parking lot: tarmac, white stall lines (detail), cars."""
    B.box("ground", x0, z0, x1, z1, y, y + .02, PAL["parking"])
    if along == "x":
        n = int((x1 - x0) // 3.0)
        depth = (z1 - z0) / rows
        for row in range(rows):
            za, zb = z0 + row * depth + .6, z0 + (row + 1) * depth - .6
            for k in range(n + 1):
                lx = x0 + .6 + k * (x1 - x0 - 1.2) / n
                prev = B.detail; B.detail = True
                B.quad("ground", (lx, y + .03, za), (lx, y + .03, za + 5.0), (lx + .15, y + .03, za + 5.0), (lx + .15, y + .03, za), PAL["dash"])
                B.detail = prev
                if k < n and rng.chance(car_p): car(B, rng, lx + 1.5, za + 2.6, math.pi / 2, y)
    else:
        n = int((z1 - z0) // 3.0)
        for k in range(n + 1):
            lz = z0 + .6 + k * (z1 - z0 - 1.2) / n
            prev = B.detail; B.detail = True
            B.quad("ground", (x0 + .6, y + .03, lz), (x0 + .6, y + .03, lz + .15), (x0 + 5.6, y + .03, lz + .15), (x0 + 5.6, y + .03, lz), PAL["dash"])
            B.detail = prev
            if k < n and rng.chance(car_p): car(B, rng, x0 + 3.1, lz + 1.5, 0.0, y)

# ---------------- Verdant & Co. ----------------
def verdant(B, rng, x0, z0, x1, z1, y):
    """Cream classical market hall: two storeys, pilasters, round-headed windows, cornice and balustrade-like
    parapet; a glass cross-vault greenhouse roof with a lantern; arched glass entrance canopy and green name."""
    H = HERO; cx = (x0 + x1) / 2
    # Footprint = the store's interior room (32 x 30 m, the proportions of the store layout), so the walls line up
    # when the roof lifts in the store view. Forecourt in front, garden strip behind.
    bx0, bx1 = cx - 16, cx + 16
    bz0 = z0 + 15; bz1 = bz0 + 30
    top = y + 10.5
    B.footprint = (bx0, bz0, bx1, bz1, y)
    B.box(G, bx0, bz0, bx1, bz1, y, y + .6, shade(H["v_wall"], .9))                      # plinth
    B.box(G, bx0 + .15, bz0 + .15, bx1 - .15, bz1 - .15, y + .6, top, H["v_wall"], H["v_roof"])
    B.box(G, bx0 - .3, bz0 - .3, bx1 + .3, bz1 + .3, y + 5.0, y + 5.45, H["v_trim"])      # string course
    B.box(G, bx0 - .45, bz0 - .45, bx1 + .45, bz1 + .45, top - .1, top + .5, H["v_trim"])  # cornice
    parapet_box(B, bx0, bz0, bx1, bz1, top + .5, .9, H["v_wall"], H["v_trim"])
    for side, (a, b) in sides(bx0, bz0, bx1, bz1).items():
        l = math.hypot(b[0] - a[0], b[1] - a[1])
        n = max(2, int(l // 4.2)); bay = l / n
        for k in range(n + 1):                                                            # pilasters
            s = min(max(k * bay, .35), l - .35)
            wall_rect(B, a, b, s - .35, s + .35, y + .6, top - .1, .2, H["v_trim"])
        for k in range(n):
            s0 = k * bay + bay * .28; s1 = (k + 1) * bay - bay * .28
            mid = (k + .5) / n
            entrance = side == "S" and abs(mid - .5) < .5 / n + 1e-6
            if entrance:
                continue
            if side == "S" or side == "W" or k % 2 == 0:
                arch_window(B, a, b, s0, s1, y + 1.4, y + 4.6, .08, PAL["window"], H["v_trim"])
            arch_window(B, a, b, s0 + .2, s1 - .2, y + 6.1, y + 9.4, .08, PAL["window"], H["v_trim"])
    # Glass roof: two crossing vaults with ribs and a lantern dome at the crossing.
    vy = top + .5; r1 = (bz1 - bz0) * .30; r2 = (bx1 - bx0) * .22
    cz = (bz0 + bz1) / 2
    barrel(B, bx0 + 3, bx1 - 3, cz, r1, vy, H["v_glass"], "x", 10, H["v_rib"], 2.6)
    barrel(B, bz0 + 2.5, bz1 - 2.5, cx, r2, vy, H["v_glass"], "z", 10, H["v_rib"], 2.6)
    B.prism(G, cx, cz, r1 * .55, r1 * .55, 8, vy + r1 - .4, vy + r1 + 1.6, H["v_rib"], None, math.pi / 8)
    B.dome(G, cx, vy + r1 + 1.6, cz, r1 * .55, H["v_glass"])
    B.box(G, cx - .12, cz - .12, cx + .12, cz + .12, vy + r1 + 1.6 + r1 * .5, vy + r1 + 3.6 + r1 * .5, H["v_green"])
    # Entrance: projecting arched glass porch on the south front, name above it.
    a, b = sides(bx0, bz0, bx1, bz1)["S"]
    pw = 9.0
    B.box(G, cx - pw / 2 - .5, bz0 - 5.5, cx - pw / 2, bz0, y, y + 4.2, H["v_trim"])
    B.box(G, cx + pw / 2, bz0 - 5.5, cx + pw / 2 + .5, bz0, y, y + 4.2, H["v_trim"])
    barrel(B, bz0 - 5.5, bz0 + .2, cx, pw / 2 + .5, y + 4.2, H["v_glass"], "z", 8, H["v_rib"], 1.6)
    wall_rect(B, a, b, (bx1 - bx0) / 2 - pw / 2 + .6, (bx1 - bx0) / 2 + pw / 2 - .6, y + .6, y + 4.0, .08, PAL["window"])
    B.box(G, cx - 10.5, bz0 - .5, cx + 10.5, bz0 + 2.0, top - .1, top + 4.2, H["v_wall"], H["v_trim"])   # attic over the entrance
    B.box(G, cx - 10.9, bz0 - .8, cx + 10.9, bz0 + 2.2, top + 4.2, top + 4.7, H["v_trim"])
    text3d(B, (cx - 10.5, bz0 - .5), (cx + 10.5, bz0 - .5), "VERDANT", .5, top + .6, 2.8, H["v_green"], off=.25)
    # Side entrance on the west with a small glass canopy.
    a, b = sides(bx0, bz0, bx1, bz1)["W"]
    l = bz1 - bz0
    wall_rect(B, a, b, l * .5 - 2, l * .5 + 2, y + .6, y + 3.6, .08, PAL["window"])
    barrel(B, bz0 + l * .5 - 2.5, bz0 + l * .5 + 2.5, bx0 - 1.6, 1.6, y + 3.8, H["v_glass"], "z", 6, H["v_rib"], 1.6)
    B.redirect = None                                                                    # the rest is the site
    # Forecourt: paving, planters with trees, benches, bike rack, a low hedge along the street.
    B.box(G, x0 + 1, z0 + 1, x1 - 1, bz0 - 5.5, y, y + .03, PAL["paving2"])
    for k in range(6):
        px = x0 + 4 + k * (x1 - x0 - 8) / 5
        if abs(px - cx) < 6: continue
        planter_tree(B, rng, px, z0 + 4.5, y, "round", .85)
    for k in range(2):
        tree(B, rng, bx0 - 2.5, y, bz0 + 4 + k * 8, .8, "round"); tree(B, rng, bx1 + 2.5, y, bz0 + 4 + k * 8, .8, "round")
    prev = B.detail; B.detail = True
    for sx in (-1, 1):
        for k in range(3):
            B.box(G, cx + sx * (pw / 2 + 3 + k * 1.2) - .3, bz0 - 4.5, cx + sx * (pw / 2 + 3 + k * 1.2) + .3, bz0 - 1.5, y, y + .5, PAL["bench"])
        B.box(G, cx + sx * 9 - 1.2, z0 + 1.5, cx + sx * 9 + 1.2, z0 + 2.3, y, y + .7, PAL["hedge"])
    B.detail = prev
    return max(top + 4.7, top + 1.4 + r1 + 4)

# ---------------- CheapCart ----------------
def cheapcart(B, rng, x0, z0, x1, z1, y):
    """Discount big box: beige block with orange corner towers and entrance pylons, brown name, shopfront glass
    under a canopy, skylights and units on the roof, parking with cars and cart corrals."""
    H = HERO
    bx0, bz0, bx1, bz1 = x0 + 3, z0 + (z1 - z0) * .42, x1 - 3, z1 - 2
    top = y + 9.0; cx = (bx0 + bx1) / 2
    B.footprint = (bx0, bz0, bx1, bz1, y)
    B.box(G, bx0, bz0, bx1, bz1, y, top, H["c_wall"], H["c_roof"])
    parapet_box(B, bx0, bz0, bx1, bz1, top, .8, H["c_wall"])
    B.box(G, bx0 - .15, bz0 - .15, bx1 + .15, bz1 + .15, y, y + .5, shade(H["c_wall"], .85))
    # Orange towers at the front corners and either side of the entrance.
    for tx in (bx0 + 2.2, bx1 - 2.2):
        B.box(G, tx - 2.4, bz0 - .8, tx + 2.4, bz0 + 3.5, y, top + 2.2, H["c_orange"], shade(H["c_orange"], 1.05))
        B.box(G, tx - 2.6, bz0 - 1.0, tx + 2.6, bz0 + 3.7, top + 2.0, top + 2.4, shade(H["c_orange"], .85))
    ew = 26.0
    for tx in (cx - ew / 2, cx + ew / 2):
        B.box(G, tx - .9, bz0 - 1.2, tx + .9, bz0 + .4, y, top + 1.4, H["c_orange"], shade(H["c_orange"], 1.05))
    a, b = sides(bx0, bz0, bx1, bz1)["S"]
    l = bx1 - bx0
    # Shopfront glass with mullions, canopy over the entrance.
    wall_rect(B, a, b, l / 2 - ew / 2 + 1, l / 2 + ew / 2 - 1, y + .5, y + 3.8, .08, PAL["window"])
    prev = B.detail; B.detail = True
    for k in range(1, 6):
        s = l / 2 - ew / 2 + 1 + k * (ew - 2) / 6
        wall_rect(B, a, b, s - .08, s + .08, y + .5, y + 3.8, .11, PAL["frame"])
    B.detail = prev
    B.box(G, cx - 6, bz0 - 3.2, cx + 6, bz0, y + 4.0, y + 4.5, H["c_brown"], shade(H["c_wall"], 1.05))
    for s0, s1 in ((7.5, l / 2 - ew / 2 - 1.5), (l / 2 + ew / 2 + 1.5, l - 7.5)):            # side display windows
        if s1 - s0 > 3: wall_rect(B, a, b, s0, s1, y + 1.0, y + 3.2, .08, PAL["window"])
    text3d(B, a, b, "CHEAPCART", .5, y + 5.0, 3.0, H["c_brown"], off=.3)
    # East side: high window band, staff door.
    a2, b2 = sides(bx0, bz0, bx1, bz1)["E"]
    wall_rect(B, a2, b2, 5.5, (bz1 - bz0) - 2, y + 5.6, y + 7.2, .08, PAL["window"])
    wall_rect(B, a2, b2, 6.5, 8.0, y + .5, y + 2.9, .08, PAL["door"])
    # Roof: skylights (glass tops) and AC units.
    for i in range(3):
        for j in range(2):
            sx = bx0 + 8 + i * (bx1 - bx0 - 16) / 2; sz = bz0 + 7 + j * (bz1 - bz0 - 12)
            roof_unit(B, sx, sz, 5.0, 3.2, top, .9, shade(H["c_wall"], .92), H["c_sky"])
    for k in range(3):
        roof_unit(B, bx0 + 6 + k * 4, bz1 - 3, 2.2, 1.6, top, 1.2, PAL["mech"])
    B.redirect = None                                                                    # the rest is the site
    # Parking lot with two rows, trees at its ends, cart corrals.
    parking(B, rng, x0 + 2, z0 + 1.5, x1 - 2, bz0 - 4.5, y, rows=2, car_p=.45)
    for k in range(int((x1 - x0) // 10) + 1):
        tree(B, rng, x0 + 2 + k * (x1 - x0 - 4) / max(1, int((x1 - x0) // 10)), y, z0 + .8, .75, "small")
    prev = B.detail; B.detail = True
    for sx in (cx - 16, cx + 16):
        B.box(G, sx - 1.5, bz0 - 8, sx + 1.5, bz0 - 7.6, y, y + 1.0, PAL["railing"])
        for k in range(4):
            B.box(G, sx - 1.2 + k * .55, bz0 - 7.4, sx - .8 + k * .55, bz0 - 6.0, y + .3, y + 1.0, PAL["antenna"])
    B.detail = prev
    return top + 2.4

# ---------------- MegaBulk Club ----------------
def megabulk(B, rng, x0, z0, x1, z1, y):
    """Warehouse club: big hall with a low curved standing-seam metal roof, cream front panel with blue name,
    brick wings, recessed entrance, two-storey office block on the west, loading docks and trucks on the east."""
    H = HERO
    hx0, hz0, hx1, hz1 = x0 + 15, z0 + (z1 - z0) * .40, x1 - 9, z1 - 2
    wall_top = y + 10.0
    B.footprint = (hx0, hz0, hx1, hz1, y)
    B.box(G, hx0, hz0, hx1, hz1, y, wall_top, H["m_wall"], None)
    B.box(G, hx0 - .1, hz0 - .1, hx1 + .1, hz1 + .1, y, y + 1.0, shade(H["m_wall"], .85))
    # Low curved roof (vault along x, flattened) with seams.
    r = (hz1 - hz0) / 2; czr = (hz0 + hz1) / 2; rise = 3.0; segs = 8
    def RV(xx, k, extra=0.0):
        ang = math.pi * k / segs
        return (xx, wall_top + math.sin(ang) * rise + extra, czr + math.cos(ang) * (r + .4))
    for k in range(segs):
        am = math.pi * (k + .5) / segs
        out = (0.0, math.sin(am) * r, math.cos(am) * rise)
        B.quad(G, RV(hx0 - .4, k), RV(hx0 - .4, k + 1), RV(hx1 + .4, k + 1), RV(hx1 + .4, k), H["m_roof"], out)
        n = int((hx1 - hx0) // 2.2)
        prev = B.detail; B.detail = True
        for j in range(1, n):
            sx = hx0 + j * (hx1 - hx0) / n
            B.quad(G, RV(sx - .07, k, .06), RV(sx - .07, k + 1, .06), RV(sx + .07, k + 1, .06), RV(sx + .07, k, .06), H["m_seam"], out)
        B.detail = prev
    for xx, sgn in ((hx0 - .4, -1.0), (hx1 + .4, 1.0)):                                    # gable ends of the vault
        for k in range(segs):
            B.tri(G, (xx, wall_top, czr), RV(xx, k), RV(xx, k + 1), H["m_wall"], (sgn, 0.0, 0.0))
    for k, sx in enumerate((hx0 + (hx1 - hx0) * .3, hx0 + (hx1 - hx0) * .55, hx0 + (hx1 - hx0) * .78)):
        roof_unit(B, sx, czr + (3 if k % 2 else -4), 4.0, 2.2, wall_top + rise - .3, 1.1, PAL["mech"])
    # Front (south): cream name panel rising above the eaves, brick piers, recessed glass entrance.
    a, b = sides(hx0, hz0, hx1, hz1)["S"]; l = hx1 - hx0
    px0, px1 = hx0 + l * .28, hx0 + l * .86
    B.box(G, px0, hz0 - 1.2, px1, hz0 + .2, y, wall_top + 3.8, H["m_panel"], shade(H["m_panel"], 1.03))
    for bxp in (px0 - 4.0, px1):
        B.box(G, bxp, hz0 - 1.6, bxp + 4.0, hz0 + .2, y, wall_top + 1.6, H["m_brick"], shade(H["m_brick"], 1.05))
    pa, pb = (px0, hz0 - 1.2), (px1, hz0 - 1.2)
    text3d(B, pa, pb, "MEGABULK", .5, wall_top - .4, 3.0, H["m_blue"], off=.2)
    pl = px1 - px0
    for k in range(4):                                                                   # entrance + exit bays
        s0 = pl * (.1 + k * .21); s1 = s0 + pl * .15
        wall_rect(B, pa, pb, s0, s1, y + .3, y + 3.6, .06, PAL["window"])
        wall_rect(B, pa, pb, s0 - .2, s1 + .2, y + 3.6, y + 4.0, .1, H["m_blue"])
    wall_rect(B, pa, pb, pl * .05, pl * .95, y + 4.6, y + 6.0, .06, PAL["window"])
    wall_rect(B, a, b, l * .9, l - .8, y + 1.0, y + 7.5, .08, PAL["window"])
    # Office block on the west: two storeys, ribbon windows, flat roof with a unit.
    ox0, oz0, ox1, oz1 = x0 + 2, hz0 + 1, hx0 + .2, hz1 - 8
    B.box(G, ox0, oz0, ox1, oz1, y, y + 8.0, H["m_office"], PAL["flatroof"])
    parapet_box(B, ox0, oz0, ox1, oz1, y + 8.0, .6, H["m_office"])
    for side in ("S", "W"):
        oa, ob = sides(ox0, oz0, ox1, oz1)[side]; ol = math.hypot(ob[0] - oa[0], ob[1] - oa[1])
        for f in range(2):
            wall_rect(B, oa, ob, .9, ol - .9, y + 1.1 + f * 3.6, y + 2.9 + f * 3.6, .07, PAL["window"])
    B.box(G, ox0 - .1, oz0 - .5, ox0 + 4.5, oz0 + .1, y, y + 8.3, H["m_brick"])          # brick corner
    roof_unit(B, (ox0 + ox1) / 2, (oz0 + oz1) / 2, 3.0, 2.2, y + 8.0, 1.4, PAL["mech"])
    # Loading docks on the east: dock apron, roll-up doors, bumpers, two trucks backed in.
    ea, eb = sides(hx0, hz0, hx1, hz1)["E"]; el = hz1 - hz0
    B.box("ground", hx1, hz0 + 2, x1 - .5, hz1 - 2, y, y + .02, PAL["parking"])
    for k in range(3):
        s0 = el * .18 + k * 7.0
        wall_rect(B, ea, eb, s0, s0 + 4.2, y + 1.0, y + 5.0, .06, H["m_door"])
        prev = B.detail; B.detail = True
        for j in range(1, 6):
            wall_rect(B, ea, eb, s0, s0 + 4.2, y + 1.0 + j * .66 - .04, y + 1.0 + j * .66, .09, shade(H["m_door"], .85))
        B.detail = prev
        wall_rect(B, ea, eb, s0 - .5, s0 + 4.7, y + 5.2, y + 5.6, .3, H["m_blue"])
        B.box(G, hx1, hz0 + s0 - .2, hx1 + 1.2, hz0 + s0 + 4.4, y, y + 1.0, shade(H["m_wall"], .8))
    B.redirect = None                                                                    # the rest is the site
    truck(B, hx1 + 6.0, hz0 + el * .18 + 2.1, y, 0.0)
    truck(B, hx1 + 6.0, hz0 + el * .18 + 16.1, y, 0.0)
    # Parking in front, trees, a cart corral.
    parking(B, rng, x0 + 2, z0 + 1.5, x1 - 2, hz0 - 5.0, y, rows=2, car_p=.5)
    for k in range(int((x1 - x0) // 10) + 1):
        tree(B, rng, x0 + 2 + k * (x1 - x0 - 4) / max(1, int((x1 - x0) // 10)), y, z0 + .8, .75, "small")
    for k in range(4):
        tree(B, rng, x0 + 1.5, y, oz1 + 2 + k * 3, .7, "small")
    return wall_top + 3.8

# ---------------- News Tower ----------------
def news_tower(B, rng, cx, cz, y):
    """Observation tower: round glass pavilion at the foot, a core shaft held by four splayed legs, a two-level
    glass observation pod with white bands and a flared rim, a blue glass drum and a red/white mast."""
    H = HERO
    # Base: stepped round plinth, glass pavilion with a white roof ring, entrance canopy.
    B.prism(G, cx, cz, 9.5, 9.5, 16, y, y + .35, H["t_band"], H["t_white"])
    B.prism(G, cx, cz, 6.2, 6.2, 16, y + .35, y + 3.8, H["t_glass"], None)
    B.prism(G, cx, cz, 6.8, 6.8, 16, y + 3.8, y + 4.6, H["t_white"], H["t_band"])
    prev = B.detail; B.detail = True
    for k in range(16):                                                                   # pavilion mullions
        ang = 2 * math.pi * k / 16
        mx, mz = cx + math.cos(ang) * 6.25, cz + math.sin(ang) * 6.25
        B.box(G, mx - .1, mz - .1, mx + .1, mz + .1, y + .35, y + 3.8, H["t_white"])
    B.detail = prev
    B.box(G, cx - 2.2, cz - 9.0, cx + 2.2, cz - 6.0, y + 3.0, y + 3.3, H["t_white"])       # canopy
    for sx in (-2.0, 2.0):
        B.box(G, cx + sx - .1, cz - 8.8, cx + sx + .1, cz - 8.6, y + .35, y + 3.0, H["t_white"])
    # Core and legs.
    pod_y = y + 40.0
    B.prism(G, cx, cz, 2.0, 1.6, 12, y + 4.6, pod_y, H["t_white"], None)
    for k in range(4):
        ang = math.pi / 4 + k * math.pi / 2
        bx, bz = cx + math.cos(ang) * 7.8, cz + math.sin(ang) * 7.8
        tx, tz = cx + math.cos(ang) * 2.0, cz + math.sin(ang) * 2.0
        leg(B, bx, bz, y + .35, tx, tz, pod_y - 1.0, .75, .45, H["t_white"])
        B.box(G, bx - 1.0, bz - 1.0, bx + 1.0, bz + 1.0, y + .35, y + 1.2, H["t_band"])
    for frac in (.33, .62):                                                                # tie rings
        ry = y + 4.6 + (pod_y - y - 5.6) * frac; rr = 7.8 - (7.8 - 2.0) * frac
        B.prism(G, cx, cz, rr * 1.02, rr * 1.02, 12, ry, ry + .5, H["t_band"], False)
    # Pod: underside cone, two glass levels, white bands, flared rim, blue drum, cap.
    yy = pod_y
    B.prism(G, cx, cz, 2.2, 6.2, 16, yy - 2.0, yy, H["t_white"], None);
    B.prism(G, cx, cz, 6.2, 6.4, 16, yy, yy + .6, H["t_white"], None); yy += .6
    B.prism(G, cx, cz, 6.4, 6.6, 16, yy, yy + 2.6, H["t_glass"], None); yy += 2.6
    B.prism(G, cx, cz, 6.9, 6.9, 16, yy, yy + .6, H["t_white"], H["t_band"]); yy += .6
    B.prism(G, cx, cz, 6.0, 6.2, 16, yy, yy + 2.4, H["t_glass"], None); yy += 2.4
    B.prism(G, cx, cz, 7.6, 7.6, 16, yy, yy + .5, H["t_white"], H["t_band"]); yy += .5
    prev = B.detail; B.detail = True
    for lvl, (y0_, r_) in enumerate(((pod_y + .6, 6.62), (pod_y + 3.8, 6.22))):            # pod mullions
        for k in range(16):
            ang = 2 * math.pi * (k + .5) / 16
            mx, mz = cx + math.cos(ang) * r_, cz + math.sin(ang) * r_
            B.box(G, mx - .09, mz - .09, mx + .09, mz + .09, y0_, y0_ + (2.6 if lvl == 0 else 2.4), H["t_white"])
    B.prism(G, cx, cz, 7.55, 7.55, 16, yy, yy + .9, PAL["railing"], False)                 # rim railing
    B.detail = prev
    B.prism(G, cx, cz, 3.6, 3.4, 12, yy, yy + 3.2, H["t_glass"], None); yy += 3.2
    B.prism(G, cx, cz, 3.9, 3.9, 12, yy, yy + .5, H["t_white"], None); yy += .5
    B.prism(G, cx, cz, 3.9, 1.2, 12, yy, yy + 1.4, H["t_band"], H["t_white"]); yy += 1.4
    # Mast: red/white bands.
    for k in range(8):
        col = H["t_red"] if k % 2 == 0 else H["t_white"]
        r0 = .55 - k * .045
        B.prism(G, cx, cz, r0, r0 - .045, 6, yy, yy + 1.6, col, col); yy += 1.6
    return yy

# ---------------- Blue Zone Bistro ----------------
def bistro(B, rng, x0, z0, x1, z1, y, front_z):
    """Two-storey cream corner building facing the water: big ground-floor glass under teal awnings, framed
    windows above, cornice and parapet, a corner tower with the name on both faces, a stair penthouse and roof
    terrace, and a terrace of umbrella tables running down to the waterfront (front_z: where the paving ends)."""
    H = HERO
    cx = (x0 + x1) / 2
    bx0, bz0, bx1, bz1 = cx - 14, z0 + 2, cx + 11, z0 + 2 + 16
    top = y + 8.4
    B.box(G, bx0, bz0, bx1, bz1, y, top, H["b_wall"], H["b_roof"])
    B.box(G, bx0 - .15, bz0 - .15, bx1 + .15, bz1 + .15, y, y + .5, shade(H["b_wall"], .88))
    B.box(G, bx0 - .3, bz0 - .3, bx1 + .3, bz1 + .3, y + 4.3, y + 4.7, H["b_trim"])          # floor band
    B.box(G, bx0 - .4, bz0 - .4, bx1 + .4, bz1 + .4, top - .1, top + .4, H["b_trim"])
    parapet_box(B, bx0, bz0, bx1, bz1, top + .4, .8, H["b_wall"], H["b_trim"])
    # Corner tower (south-west, facing the water and the promenade) rising above the roof, name on two faces.
    tx0, tz0, tx1, tz1 = bx0 - .6, bz0 - .6, bx0 + 11.0, bz0 + 10.5
    ttop = top + 4.2
    B.box(G, tx0, tz0, tx1, tz1, y, ttop, H["b_wall"], H["b_roof"])
    B.box(G, tx0 - .35, tz0 - .35, tx1 + .35, tz1 + .35, ttop - .1, ttop + .45, H["b_trim"])
    parapet_box(B, tx0, tz0, tx1, tz1, ttop + .45, .6, H["b_wall"], H["b_trim"])
    tS = sides(tx0, tz0, tx1, tz1)
    text3d(B, *tS["S"], "BISTRO", .5, top + .2, 2.1, H["b_navy"], off=.25)
    text3d(B, *tS["W"], "BISTRO", .5, top + .2, 2.1, H["b_navy"], off=.25)
    for side in ("S", "W"):
        a, b = tS[side]; l = math.hypot(b[0] - a[0], b[1] - a[1])
        wall_rect(B, a, b, 1.0, l - 1.0, y + .5, y + 3.7, .08, PAL["window"])
        for s0 in (1.6, l - 3.2):
            wall_rect(B, a, b, s0, s0 + 1.6, y + 5.2, y + 7.2, .08, PAL["window"])
            prev = B.detail; B.detail = True
            wall_rect(B, a, b, s0 - .15, s0 + 1.75, y + 5.05, y + 7.35, .05, H["b_trim"])
            B.detail = prev
        B.awning(G, a, b, .08, .92, y + 4.1, 1.8, .6, H["b_teal"])
    # Main block: shopfront bays + awnings on the south (water) and east, framed windows upstairs.
    S = sides(bx0, bz0, bx1, bz1)
    for side in ("S", "E", "N"):
        a, b = S[side]; l = math.hypot(b[0] - a[0], b[1] - a[1])
        s_start = 12.0 if side == "S" else 1.0
        n = max(1, int((l - s_start - 1.0) // 3.6))
        bay = (l - s_start - 1.0) / n
        for k in range(n):
            s0 = s_start + k * bay + .35; s1 = s0 + bay - .7
            if side != "N":
                wall_rect(B, a, b, s0, s1, y + .5, y + 3.6, .08, PAL["window"])
            wall_rect(B, a, b, s0 + .25, s1 - .25, y + 5.3, y + 7.3, .08, PAL["window"])
            prev = B.detail; B.detail = True
            wall_rect(B, a, b, s0 + .1, s1 - .1, y + 5.15, y + 7.45, .05, H["b_trim"])
            wall_rect(B, a, b, s0 + .1, s1 - .1, y + 5.0, y + 5.2, .12, H["b_trim"])
            B.detail = prev
        if side != "N":
            B.awning(G, a, b, s_start / l, 1 - 1.0 / l, y + 4.1, 2.2, .7, H["b_teal"])
    an, bn = S["N"]
    wall_rect(B, an, bn, 2.0, 3.4, y + .5, y + 2.9, .08, PAL["door"])                       # kitchen door
    # Roof: stair penthouse, roof-terrace tables, a few planters, an extractor.
    roof_unit(B, bx1 - 4.5, bz1 - 3.5, 4.0, 3.5, top + .4, 2.8, H["b_wall"], H["b_roof"])
    roof_unit(B, bx1 - 9.5, bz1 - 2.5, 1.4, 1.4, top + .4, 1.6, PAL["mech"])
    B.box(G, bx0 + 9.5, bz0 + 1.5, bx1 - 7.0, bz0 + 8.0, top + .4, top + .5, PAL["planks"])
    for k in range(2):
        table_set(B, bx0 + 11.5 + k * 3.4, bz0 + 4.5, top + .5, umbrella=(k == 0))
    prev = B.detail; B.detail = True
    for k in range(4):
        B.box(G, bx0 + 9.5 + k * 1.6, bz0 + 8.2, bx0 + 10.5 + k * 1.6, bz0 + 9.0, top + .4, top + .9, H["planter"], PAL["hedge"])
    B.detail = prev
    # Terrace: deck from the front down to the waterfront, umbrella tables, planters, a menu board, lamps.
    tz_lo = min(front_z, bz0 - 6)
    B.box("ground", x0 - 2, tz_lo, x1 + 2, bz0, y - .2, y + .02, PAL["promenade"])
    B.box(G, bx0 - 1.5, max(tz_lo + .5, bz0 - 11), bx1 + 1, bz0 - .4, y, y + .14, PAL["planks"])
    deck_z0 = max(tz_lo + .5, bz0 - 11)
    for i in range(4):
        for j in range(2):
            table_set(B, bx0 + 1.5 + i * 5.6 + (j * 2.8), bz0 - 3.4 - j * 4.2, y + .14)
    for k in range(8):
        px = bx0 - 1.0 + k * (bx1 - bx0 + 1.5) / 7
        B.box(G, px - .55, deck_z0 - .5, px + .55, deck_z0 + .5, y, y + .65, H["planter"], PAL["hedge"])
    for px in (bx0 - 3.5, bx1 + 3.0):
        planter_tree(B, rng, px, bz0 - 4.0, y, "round", .85)
        planter_tree(B, rng, px, bz0 + 6.0, y, "round", .8)
    prev = B.detail; B.detail = True
    B.box(G, bx1 + 1.6, bz0 - 1.4, bx1 + 1.75, bz0 - .4, y, y + 1.4, H["b_navy"])          # menu board
    B.detail = prev
    return ttop + 1.0
