"""Store interiors: cutaway "dollhouse" rooms for the three stores, after the 2D store screens
(Art/Stores/S00x_background.png). Back walls (north, east) are full height and carry the shelf signs; the walls
nearer the default camera (south, west) are cut low, so the room reads from above once the store's shell (its
roof and full walls) is hidden. Everything is drawn into "store_<id>_interior" (one mesh per store, not tiled),
hidden in the city view. Fixtures are stylised: about 1.5x real size so they read at the zoom the store view uses.

Each clickable shelf zone is recorded in B.shelves as (store id, shelf id, centre xyz, size xyz); the Unity
generator turns those into box colliders tagged with the shelf id. Which shelf zones exist comes from the store
data (Blue_Zone_Bistro_Stores.csv, shelf_definitions); the layout below says where each known zone sits.
"""
import json, math, os
from cityparts import PAL, hexc, shade, tree, obox, GLASS, METAL, PAINT, STONE
import citylandmarks as hero

G = "landmarks"           # drawn through B.redirect into store_<id>_interior

C = dict(
    wood=hexc("#C99D66", 20, PAINT), wood_d=hexc("#A9783F", 20, PAINT), wood_l=hexc("#E2C597", 20, PAINT),
    metal=hexc("#E6E4DF", 90, METAL), metal_d=hexc("#7F8A86", 90, METAL), glass=hexc("#BFD9E2", 230, GLASS),
    white=hexc("#F4F1EA", 40, PAINT), counter=hexc("#E9E1D2", 40, PAINT), screen=hexc("#2C3338", 160, GLASS),
    v_wall=hexc("#F1E8D6"), v_trim=hexc("#7F978A", 60, PAINT), v_floor=hexc("#EADFCB", 30, STONE), v_rug=hexc("#BCC8A9", 0, PAINT),
    c_wall=hexc("#EFE6D5"), c_trim=hexc("#8E978D", 60, PAINT), c_floor=hexc("#E4DED3", 30, STONE), c_orange=hexc("#D59B47", 90, PAINT),
    m_wall=hexc("#CDD1D2", 60, METAL), m_trim=hexc("#B26C4B", 20, STONE), m_floor=hexc("#A7AAA7", 30, STONE),
    m_line=hexc("#E2B93B", 40, PAINT), rack=hexc("#2F4D74", 100, PAINT), beam=hexc("#D9772E", 100, PAINT),
    pallet=hexc("#B88A55", 0, PAINT), card=hexc("#C59C68", 0, PAINT), wrap=hexc("#E9ECEE", 120, PAINT), sack=hexc("#E1D2B1", 0, PAINT),
    sign=hexc("#F6F1E4", 40, PAINT), sign_text=hexc("#2F4A3C", 100, PAINT), jack=hexc("#E3B22E", 100, PAINT),
)
PRODUCTS = [hexc(h, 60, PAINT) for h in ("#D9483B", "#E98B2E", "#F0C33C", "#6FA34A", "#3F7F4A", "#3C6DA6", "#8E5A9E",
                                         "#F2EFE6", "#C9A066", "#8B5A3C", "#E7C9A0", "#5BA3B5", "#B23A48", "#F4E3B5")]
FRUIT = [hexc(h, 80, PAINT) for h in ("#D8412F", "#EE8A22", "#F2CB32", "#7DB040", "#4C8F3A", "#9A3B6B", "#E9D27A", "#6E9E3C")]

# ---------------- primitives ----------------
def items_row(B, rng, x0, x1, z, y, depth, hmax, palette, axis="x", gap=.06, wmin=.4, wmax=.75, face=1.0):
    """A row of product boxes on a shelf from x0..x1 (axis x) or z0..z1 (axis z) at depth position z."""
    s = x0
    while s < x1 - wmin:
        w = min(rng.range(wmin, wmax), x1 - s)
        h = hmax * rng.range(.55, 1.0); col = rng.pick(palette); d = depth * rng.range(.75, 1.0)
        if axis == "x": B.box(G, s, z - d / 2, s + w, z + d / 2, y, y + h, col, shade(col, 1.1))
        else: B.box(G, z - d / 2, s, z + d / 2, s + w, y, y + h, col, shade(col, 1.1))
        s += w + gap

def gondola(B, rng, cx, cz, length, h, levels, axis="x", palette=PRODUCTS, frame=None, end_col=None, low_big=False):
    """Double-sided shelving run: plinth, back panel, shelf boards, end caps, products on both faces."""
    frame = frame or C["metal"]; depth = 1.7; half = length / 2
    if axis == "x": X0, X1, Z0, Z1 = cx - half, cx + half, cz - depth / 2, cz + depth / 2
    else: X0, X1, Z0, Z1 = cx - depth / 2, cx + depth / 2, cz - half, cz + half
    B.box(G, X0, Z0, X1, Z1, 0, .3, shade(frame, .8))
    if axis == "x": B.box(G, X0, cz - .08, X1, cz + .08, .3, h, frame)
    else: B.box(G, cx - .08, Z0, cx + .08, Z1, .3, h, frame)
    for e in (-1, 1):                                                                # end caps
        if axis == "x": B.box(G, cx + e * half - (.15 if e > 0 else 0), Z0, cx + e * half + (0 if e > 0 else .15), Z1, 0, h + .15, end_col or frame)
        else: B.box(G, X0, cz + e * half - (.15 if e > 0 else 0), X1, cz + e * half + (0 if e > 0 else .15), 0, h + .15, end_col or frame)
    gapy = (h - .45) / levels
    for k in range(levels):
        y = .3 + k * gapy
        if axis == "x": B.box(G, X0 + .15, Z0, X1 - .15, Z1, y, y + .07, shade(frame, 1.05))
        else: B.box(G, X0, Z0 + .15, X1, Z1 - .15, y, y + .07, shade(frame, 1.05))
        pal = [hexc("#E3D3B0"), hexc("#CDB58A"), hexc("#F1E9D6")] if (low_big and k == 0) else palette
        for side in (-1, 1):
            if axis == "x":
                items_row(B, rng, X0 + .25, X1 - .25, cz + side * depth * .27, y + .07, depth * .42, gapy * .85, pal, "x",
                          wmin=.6 if low_big and k == 0 else .4, wmax=1.0 if low_big and k == 0 else .75)
            else:
                items_row(B, rng, Z0 + .25, Z1 - .25, cx + side * depth * .27, y + .07, depth * .42, gapy * .85, pal, "z")
    return (X0, Z0, X1, Z1, h)

def wall_shelf(B, rng, x0, x1, z, y_top, levels, facing=-1, palette=PRODUCTS, frame=None, axis="x", top_col=None):
    """Single-sided shelving against a wall (axis x: wall at z, shelves stick out towards facing)."""
    frame = frame or C["metal"]; depth = 1.0
    if axis == "x":
        za, zb = (z - depth, z) if facing < 0 else (z, z + depth)
        B.box(G, x0, za, x1, zb, 0, .3, shade(frame, .8))
        B.box(G, x0, (zb - .12) if facing < 0 else za, x1, zb if facing < 0 else za + .12, .3, y_top, frame, top_col)
        for e in (x0, x1 - .12): B.box(G, e, za, e + .12, zb, 0, y_top, frame)
    else:
        za, zb = (z - depth, z) if facing < 0 else (z, z + depth)
        B.box(G, za, x0, zb, x1, 0, .3, shade(frame, .8))
        B.box(G, (zb - .12) if facing < 0 else za, x0, zb if facing < 0 else za + .12, x1, .3, y_top, frame, top_col)
        for e in (x0, x1 - .12): B.box(G, za, e, zb, e + .12, 0, y_top, frame)
    gapy = (y_top - .45) / levels
    mid = (za + zb) / 2 + (.06 if facing < 0 else -.06)
    for k in range(levels):
        y = .3 + k * gapy
        if axis == "x":
            B.box(G, x0 + .12, za, x1 - .12, zb, y, y + .06, shade(frame, 1.05))
            items_row(B, rng, x0 + .2, x1 - .2, mid, y + .06, depth * .7, gapy * .85, palette, "x")
        else:
            B.box(G, za, x0 + .12, zb, x1 - .12, y, y + .06, shade(frame, 1.05))
            items_row(B, rng, x0 + .2, x1 - .2, mid, y + .06, depth * .7, gapy * .85, palette, "z")

def fruit_pile(B, rng, x0, z0, x1, z1, y, palette=FRUIT, size=.3):
    """A crate's worth of fruit: a coloured mound plus a few loose fruit on top."""
    col = rng.pick(palette)
    B.box(G, x0, z0, x1, z1, y, y + size * 1.1, shade(col, .85), col)
    y += size * .3
    nx = max(1, int((x1 - x0) / (size * 2.2))); nz = max(1, int((z1 - z0) / (size * 2.2)))
    prev = B.detail; B.detail = True
    for i in range(nx):
        for j in range(nz):
            fx = x0 + (i + .5) * (x1 - x0) / nx + rng.range(-.05, .05); fz = z0 + (j + .5) * (z1 - z0) / nz + rng.range(-.05, .05)
            B.prism(G, fx, fz, size, size * .6, 5, y + size * .8, y + size * 1.5, shade(col, rng.range(.92, 1.1)), shade(col, 1.12), rng.range(0, 1))
    B.detail = prev

def crate(B, rng, x0, z0, x1, z1, y, h, palette=FRUIT, wood=None):
    wood = wood or C["wood"]
    B.box(G, x0, z0, x1, z1, y, y + h, wood, shade(wood, .8))
    fruit_pile(B, rng, x0 + .12, z0 + .12, x1 - .12, z1 - .12, y + h - .35, palette)

def produce_stand(B, rng, cx, cz, w, d, tiers=2):
    """Stepped wooden produce table with crates of fruit on each tier."""
    for t in range(tiers):
        z0 = cz - d / 2 + t * d / tiers; z1 = z0 + d / tiers
        y = .9 + t * .45
        B.box(G, cx - w / 2, z0, cx + w / 2, z1, 0, y, C["wood_d"], C["wood"])
        n = max(2, int(w // 1.3))
        for k in range(n):
            x0 = cx - w / 2 + .1 + k * (w - .2) / n
            crate(B, rng, x0 + .05, z0 + .1, x0 + (w - .2) / n - .05, z1 - .1, y, .35)

def round_display(B, rng, cx, cz, r, palette=PRODUCTS, plant=True):
    """Tiered round table (wellness display) with products around each tier and a potted tree on top."""
    for t, (rr, y0, y1) in enumerate(((r, 0, .9), (r * .66, .9, 1.7), (r * .36, 1.7, 2.4))):
        B.prism(G, cx, cz, rr, rr, 12, y0, y1, C["wood"], C["wood_l"])
        n = int(2 * math.pi * rr / .7)
        for k in range(n):
            a = 2 * math.pi * (k + .5) / n
            px, pz = cx + math.cos(a) * (rr - .3), cz + math.sin(a) * (rr - .3)
            if t < 2 and math.hypot(px - cx, pz - cz) > r * .66 * (1 if t == 0 else .55) + .15 or t == 2:
                col = rng.pick(palette); hh = rng.range(.35, .6)
                obox(B, G, px, pz, .45, .35, y1, y1 + hh, a, col, shade(col, 1.1))
    if plant:
        B.prism(G, cx, cz, .45, .55, 8, 2.4, 3.0, C["white"], hexc("#6B5A45"))
        tree(B, rng, cx, 3.0, cz, .7, "round")

def plinth(B, rng, cx, cz, r, h, items=3, palette=PRODUCTS, round_=True):
    if round_: B.prism(G, cx, cz, r, r, 10, 0, h, C["white"], shade(C["white"], 1.03))
    else: B.box(G, cx - r, cz - r, cx + r, cz + r, 0, h, C["white"])
    for k in range(items):
        a = 2 * math.pi * k / items + .4; col = rng.pick(palette)
        px, pz = cx + math.cos(a) * r * .45, cz + math.sin(a) * r * .45
        B.box(G, px - .2, pz - .2, px + .2, pz + .2, h, h + rng.range(.35, .7), col, shade(col, 1.1))

def freezer_wall(B, rng, x0, x1, z, h, doors):
    """Upright freezers against the back wall (facing -z): body, lit interior with products, glass doors."""
    B.box(G, x0, z - 1.3, x1, z, 0, h, C["white"], C["white"])
    w = (x1 - x0) / doors
    for k in range(doors):
        a = x0 + k * w + .12; b = a + w - .24
        B.quad(G, (a, .35, z - 1.32), (a, h - .35, z - 1.32), (b, h - .35, z - 1.32), (b, .35, z - 1.32), hexc("#DDEFF4", 0, PAINT), (0.0, 0.0, -1.0))
        for lvl in range(4):
            y = .5 + lvl * (h - .9) / 4
            items_row(B, rng, a + .1, b - .1, z - 1.6, y, .35, (h - .9) / 4 * .75, PRODUCTS, "x", wmin=.3, wmax=.5)
        B.quad(G, (a, .35, z - 1.95), (a, h - .35, z - 1.95), (b, h - .35, z - 1.95), (b, .35, z - 1.95), C["glass"], (0.0, 0.0, -1.0))
        for e in (a - .06, b):
            B.box(G, e, z - 2.0, e + .06, z - 1.3, .3, h - .3, C["metal_d"])
        B.box(G, b - .25, z - 2.08, b - .17, z - 1.95, h * .35, h * .65, C["metal_d"])        # handle
    B.box(G, x0, z - 2.0, x1, z - 1.3, h - .35, h, C["white"])

def bargain_bin(B, rng, cx, cz, w, d):
    """Deep bin with a heap of tumbled product packs."""
    B.box(G, cx - w / 2, cz - d / 2, cx + w / 2, cz + d / 2, 0, 1.2, C["c_orange"], shade(C["c_orange"], .7))
    for k in range(int(w * d * 2.2)):
        px = cx + rng.range(-w / 2 + .35, w / 2 - .35); pz = cz + rng.range(-d / 2 + .35, d / 2 - .35)
        y = 1.0 + rng.range(0, .5); col = rng.pick(PRODUCTS)
        obox(B, G, px, pz, rng.range(.4, .7), rng.range(.3, .5), y, y + rng.range(.15, .3), rng.range(0, 3.1), col, shade(col, 1.12))

def checkout(B, rng, cx, cz, w, axis_x=True, col=None):
    col = col or C["counter"]
    if axis_x:
        B.box(G, cx - w / 2, cz - .6, cx + w / 2, cz + .6, 0, 1.1, col, shade(col, 1.05))
        B.box(G, cx - w / 2 + .3, cz - .45, cx + w / 2 - 1.4, cz + .45, 1.1, 1.16, hexc("#3B3F42", 60, PAINT))
        B.box(G, cx + w / 2 - 1.1, cz - .3, cx + w / 2 - .5, cz + .3, 1.1, 1.35, C["screen"])
        B.box(G, cx + w / 2 - 1.0, cz + .05, cx + w / 2 - .6, cz + .15, 1.35, 1.75, C["screen"])
    for k in range(3):
        col2 = rng.pick(PRODUCTS)
        B.box(G, cx - w / 2 + .5 + k * .5, cz - .2, cx - w / 2 + .85 + k * .5, cz + .2, 1.16, 1.16 + rng.range(.2, .4), col2)

def potted(B, rng, x, z, s=1.0):
    B.prism(G, x, z, .35 * s, .45 * s, 8, 0, .7 * s, C["white"], hexc("#6B5A45"))
    tree(B, rng, x, .7 * s, z, .45 * s, rng.pick(["round", "bush"]))

def pallet(B, x0, z0, x1, z1, y=0.0):
    B.box(G, x0, z0, x1, z1, y, y + .18, C["pallet"], shade(C["pallet"], 1.05))

def pallet_stack(B, rng, x0, z0, x1, z1, y, h, col=None):
    """Pallet with stacked cartons (or a shrink-wrapped block)."""
    pallet(B, x0, z0, x1, z1, y)
    col = col or rng.pick([C["card"], C["wrap"], C["card"], hexc("#7FA4C9", 60, PAINT), hexc("#9CBF7E", 60, PAINT)])
    nx = 2 if x1 - x0 > 1.3 else 1; nz = 2 if z1 - z0 > 1.3 else 1
    layers = max(1, int(h // .55))
    for l in range(layers):
        for i in range(nx):
            for j in range(nz):
                a = x0 + .05 + i * (x1 - x0 - .1) / nx; b = z0 + .05 + j * (z1 - z0 - .1) / nz
                yy = y + .18 + l * (h / layers)
                c = shade(col, rng.range(.92, 1.06))
                B.box(G, a + .03, b + .03, a + (x1 - x0 - .1) / nx - .03, b + (z1 - z0 - .1) / nz - .03, yy, yy + h / layers - .03, c, shade(c, 1.08))

def racking(B, rng, x0, x1, z0, z1, h, levels, axis="x"):
    """Pallet racking: navy uprights, orange beams, a pallet load on each bay and level."""
    L = (x1 - x0) if axis == "x" else (z1 - z0)
    bays = max(1, int(L // 3.0)); bw = L / bays
    for k in range(bays + 1):
        s = (x0 if axis == "x" else z0) + k * bw
        for e in ((z0, z0 + .15), (z1 - .15, z1)) if axis == "x" else ((x0, x0 + .15), (x1 - .15, x1)):
            if axis == "x": B.box(G, s - .08, e[0], s + .08, e[1], 0, h, C["rack"])
            else: B.box(G, e[0], s - .08, e[1], s + .08, 0, h, C["rack"])
    for l in range(levels):
        y = .1 + l * (h - .3) / levels
        for e in ((z0, z0 + .12), (z1 - .12, z1)) if axis == "x" else ((x0, x0 + .12), (x1 - .12, x1)):
            if axis == "x": B.box(G, x0, e[0], x1, e[1], y + .3, y + .5, C["beam"])
            else: B.box(G, e[0], z0, e[1], z1, y + .3, y + .5, C["beam"])
        for k in range(bays):
            a = (x0 if axis == "x" else z0) + k * bw + .25; b = a + bw - .5
            hh = (h - .3) / levels - .75
            if rng.chance(.88):
                if axis == "x": pallet_stack(B, rng, a, z0 + .15, b, z1 - .15, y + .5, hh * rng.range(.7, 1.0))
                else: pallet_stack(B, rng, x0 + .15, a, x1 - .15, b, y + .5, hh * rng.range(.7, 1.0))

def sack(B, x, z, y, w, d, h, col):
    B.box(G, x - w / 2, z - d / 2, x + w / 2, z + d / 2, y, y + h * .8, col, shade(col, 1.05))
    B.pyramid(G, x, z, min(w, d) * .55, 4, y + h * .8, y + h, shade(col, 1.06), math.pi / 4)

def sack_stack(B, rng, x0, z0, x1, z1, rows=3):
    pallet(B, x0, z0, x1, z1)
    col = rng.pick([C["sack"], hexc("#D8C59A", 0, PAINT), hexc("#EFE6CF", 0, PAINT), hexc("#C9B48A", 0, PAINT)])
    nx = max(1, int((x1 - x0) // .9)); nz = max(1, int((z1 - z0) // .7))
    for r in range(rows):
        for i in range(nx):
            for j in range(nz):
                sx = x0 + (i + .5) * (x1 - x0) / nx; sz = z0 + (j + .5) * (z1 - z0) / nz
                sack(B, sx, sz, .18 + r * .42, (x1 - x0) / nx - .08, (z1 - z0) / nz - .08, .45, shade(col, rng.range(.95, 1.05)))

def sign_board(B, a, b, text, y, h=1.0, board=None, col=None, off=.1):
    """Sign board on a wall face (edge a->b, outward normal towards the viewer) with block letters."""
    board = board or C["sign"]; col = col or C["sign_text"]
    l = math.hypot(b[0] - a[0], b[1] - a[1]); w = hero.text_width(text, h) + h * 1.2
    t0 = .5 - w / 2 / l; t1 = .5 + w / 2 / l
    hero.wall_rect(B, a, b, t0 * l, t1 * l, y - h * .5, y + h * 1.5, off, board)
    hero.text3d(B, a, b, text, .5, y, h, col, off=off + .12, depth=.08)

def hanging_sign(B, cx, cz, y, text, h=.8, axis="x"):
    """Free-standing sign above a fixture, readable from the south (axis x) or the west (axis z), two posts."""
    w = hero.text_width(text, h) + h * 1.2
    if axis == "x":
        a, b = (cx - w / 2, cz), (cx + w / 2, cz)
        B.box(G, cx - w / 2, cz - .06, cx + w / 2, cz + .06, y - h * .5, y + h * 1.5, C["sign"])
    else:
        a, b = (cx, cz + w / 2), (cx, cz - w / 2)
        B.box(G, cx - .06, cz - w / 2, cx + .06, cz + w / 2, y - h * .5, y + h * 1.5, C["sign"])
    hero.text3d(B, a, b, text, .5, y, h, C["sign_text"], off=.14, depth=.08)

def label(text):
    return "".join(ch for ch in text.upper() if ch in hero.FONT)

# ---------------- room ----------------
def room(B, x0, z0, x1, z1, H, low, wall, trim, floor, door_w=6.0, windows=None, pillar=None):
    """Floor, four walls (north and east full height, south and west cut to `low`), pillars and caps, a door gap
    in the middle of the south wall, glass in the low walls."""
    pillar = pillar or trim
    B.box(G, x0, z0, x1, z1, -.05, .02, shade(floor, .9), floor)
    prev = B.detail; B.detail = True                                             # floor joints
    for k in range(1, int((x1 - x0) // 2.5)):
        x = x0 + k * 2.5; B.quad(G, (x - .03, .025, z0), (x - .03, .025, z1), (x + .03, .025, z1), (x + .03, .025, z0), shade(floor, .94))
    for k in range(1, int((z1 - z0) // 2.5)):
        z = z0 + k * 2.5; B.quad(G, (x0, .025, z - .03), (x0, .025, z + .03), (x1, .025, z + .03), (x1, .025, z - .03), shade(floor, .94))
    B.detail = prev
    t = .5
    cx = (x0 + x1) / 2
    # Full-height back walls with a cornice.
    B.box(G, x0, z1 - t, x1, z1, 0, H, wall, shade(trim, 1.1)); B.box(G, x1 - t, z0, x1, z1, 0, H, wall, shade(trim, 1.1))
    B.box(G, x0, z1 - t - .25, x1 - t, z1 - t, H - .5, H - .1, trim); B.box(G, x1 - t - .25, z0, x1 - t, z1 - t, H - .5, H - .1, trim)
    B.box(G, x0, z1 - t - .1, x1 - t, z1 - t, 0, .3, shade(trim, .9)); B.box(G, x1 - t - .1, z0, x1 - t, z1 - t, 0, .3, shade(trim, .9))
    # Low front walls with a cap; door gap in the south wall; glass.
    B.box(G, x0, z0, cx - door_w / 2, z0 + t, 0, low, wall, shade(trim, 1.1))
    B.box(G, cx + door_w / 2, z0, x1 - t, z0 + t, 0, low, wall, shade(trim, 1.1))
    B.box(G, x0, z0 + t, x0 + t, z1 - t, 0, low, wall, shade(trim, 1.1))
    for (a, b) in (((x0 + 1.5, z0), (cx - door_w / 2 - 1.0, z0)), ((cx + door_w / 2 + 1.0, z0), (x1 - 1.5, z0))):
        l = b[0] - a[0]
        if l > 2: hero.wall_rect(B, a, b, 0, l, .7, low - .35, .02, C["glass"])
    a, b = (x0, z1 - 1.5), (x0, z0 + 1.5)
    hero.wall_rect(B, a, b, 0, (z1 - z0) - 3, .7, low - .35, .02, C["glass"])
    # Pillars at corners and along the walls.
    def pil(px, pz, h): B.box(G, px - .45, pz - .45, px + .45, pz + .45, 0, h, pillar, shade(pillar, 1.08))
    for px in (x0 + .25, x1 - .25):
        pil(px, z1 - .25, H + .4)
    pil(x1 - .25, z0 + .25, H + .4); pil(x0 + .25, z0 + .25, low + .4)
    n = max(1, int((x1 - x0) // 9))
    for k in range(1, n):
        px = x0 + k * (x1 - x0) / n
        pil(px, z1 - .25, H + .4)
        if abs(px - cx) > door_w / 2 + .6: pil(px, z0 + .25, low + .4)
    n = max(1, int((z1 - z0) // 9))
    for k in range(1, n):
        pz = z0 + k * (z1 - z0) / n
        pil(x1 - .25, pz, H + .4); pil(x0 + .25, pz, low + .4)
    for px in (cx - door_w / 2 - .45, cx + door_w / 2 + .45):
        pil(px, z0 + .25, low + .9)
    B.box(G, cx - door_w / 2, z0, cx + door_w / 2, z0 + .1, 0, .04, hexc("#5B5F5C", 0, PAINT))   # door mat

def shelf_zone(B, store, shelf, box, y0=0.0, pad=.6):
    x0, z0, x1, z1, h = box
    B.shelves.append((store, shelf, ((x0 + x1) / 2, y0 + h / 2, (z0 + z1) / 2), (x1 - x0 + 2 * pad, h + .6, z1 - z0 + 2 * pad)))

def union(*boxes):
    return (min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes), max(b[4] for b in boxes))

class Local:
    """Store-local placement: u (0..1 west->east), v (0..1 south->north) inside the room, y from the floor."""
    def __init__(self, x0, z0, x1, z1, y): self.x0, self.z0, self.x1, self.z1, self.y = x0, z0, x1, z1, y
    def x(self, u): return self.x0 + (self.x1 - self.x0) * u
    def z(self, v): return self.z0 + (self.z1 - self.z0) * v

class Lift:
    """Draw everything at floor height y: temporarily offsets the builder's output vertically."""
    def __init__(self, B, y): self.B, self.y = B, y
    def __enter__(self): self.prev = getattr(self.B, "lift", 0.0); self.B.lift = self.y
    def __exit__(self, *a): self.B.lift = self.prev

# ---------------- the three stores ----------------
LAYOUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "interiors")

def load_layout(store):
    """Fixture layout extracted from an image-to-3D reference mesh (layout_extract.py), or None."""
    path = os.path.join(LAYOUT_DIR, store + "_layout.json")
    return json.load(open(path)) if os.path.exists(path) else None

def arched_bays(B, a, b, s0, s1, y0, h, bays, frame, back):
    """Arched alcoves on an inside wall face (edge a->b facing into the room): darker recess panels with round heads
    in a trim frame, like the shelving niches of the store screens."""
    w = (s1 - s0) / bays
    for k in range(bays):
        p0 = s0 + k * w + .35; p1 = p0 + w - .7
        hero.arch_window(B, a, b, p0, p1, y0, y0 + h, .04, back, frame)
        hero.wall_rect(B, a, b, s0 + k * w - .18, s0 + k * w + .18, y0 - .2, y0 + h + .5, .12, frame)
    hero.wall_rect(B, a, b, s1 - .18, s1 + .18, y0 - .2, y0 + h + .5, .12, frame)

def produce_cluster(B, rng, R, cells, step=3.4):
    """Wooden produce stands covering an irregular floor region (the cells of an extracted fixture): one tiered
    stand per covered grid square, so curves and L-shapes of the reference come through."""
    pts = [(R.x(u), R.z(v)) for u, v in cells]
    xs = [p[0] for p in pts]; zs = [p[1] for p in pts]
    boxes = []
    gx0, gz0 = min(xs), min(zs)
    nx, nz = int((max(xs) - gx0) // step) + 1, int((max(zs) - gz0) // step) + 1
    count = {}
    for x, z in pts: count[(int((x - gx0) // step), int((z - gz0) // step))] = count.get((int((x - gx0) // step), int((z - gz0) // step)), 0) + 1
    need = max(2, len(pts) / max(1, len(count)) * .8)        # only well-covered squares; gaps become walkways
    for (i, j), n in count.items():
        if n < need: continue
        cx, cz = gx0 + (i + .5) * step, gz0 + (j + .5) * step
        w, d = step - .9, step - 1.3
        tiers = 2 if (i + j) % 3 else 3
        produce_stand(B, rng, cx, cz, w, d, tiers)
        boxes.append((cx - w / 2, cz - d / 2, cx + w / 2, cz + d / 2, .9 + tiers * .45 + .4))
    return boxes

def verdant(B, rng, x0, z0, x1, z1, y, shelves):
    """Premium grocer, laid out from the extracted reference (interiors/S001_layout.json) when present: produce
    stands following the reference's curved run on the west, the round wellness display and wellness shelving at
    the back, the luxury cabinet run and round table on the east, checkout counter; arched sage alcoves on the back
    walls, rugs, plants. Falls back to a fixed layout without the file."""
    L = load_layout("S001")
    if L is None: return verdant_fixed(B, rng, x0, z0, x1, z1, y, shelves)
    with Lift(B, y):
        room(B, x0, z0, x1, z1, 7.0, 2.6, C["v_wall"], C["v_trim"], C["v_floor"], door_w=5.0)
        R = Local(x0 + .6, z0 + .6, x1 - .6, z1 - .6, 0)
        # Arched alcoves on both back walls (behind the wall shelving).
        arched_bays(B, (x0 + .5, z1 - .5), (x1 - .5, z1 - .5), .8, (x1 - x0) - 1.6, 1.0, 4.4, 6, C["v_trim"], hexc("#E2D6BE"))
        arched_bays(B, (x1 - .5, z1 - .5), (x1 - .5, z0 + .5), 1.2, (z1 - z0) - 2.0, 1.0, 4.4, 5, C["v_trim"], hexc("#E2D6BE"))
        zones = {"PRODUCE": [], "WELLNESS": [], "LUXURY": []}
        def zone_of(f):
            if f["u"] < .40: return "PRODUCE"
            if f["u"] > .62 and f["v"] < .85: return "LUXURY"
            return "WELLNESS"
        for f in L["fixtures"]:
            w = (f["u1"] - f["u0"]) * (R.x1 - R.x0); d = (f["v1"] - f["v0"]) * (R.z1 - R.z0)
            cx, cz = R.x(f["u"]), R.z(f["v"])
            area = w * d; z = zone_of(f); walls = f.get("walls", [])
            if f["fill"] < .7 and area > 40:                                         # irregular run -> produce stands
                for bx in produce_cluster(B, rng, R, f["cells"]): zones["PRODUCE"].append(bx)
                for (u, v, r) in ((f["u"] + .04, f["v"] - .05, max(w, d) * .55),):
                    B.prism(G, R.x(u), R.z(v), r, r, 18, .02, .035, C["v_rug"], C["v_rug"])
            elif ("N" in walls or (f["v"] > .9 and f["elong"] > 3)):               # back-wall shelving
                s0, s1 = R.x(f["u0"]), R.x(f["u1"])
                if s1 - s0 > 1.5:
                    wall_shelf(B, rng, s0, s1, z1 - .5, 3.6, 4, -1, FRUIT if z == "PRODUCE" else PRODUCTS, C["wood"])
                    zones[z].append((s0, z1 - 1.5, s1, z1 - .5, 3.6))
            elif ("W" in walls and f["elong"] > 3):                                   # west wall shelving (low wall)
                s0, s1 = R.z(f["v0"]), R.z(f["v1"])
                if s1 - s0 > 1.5:
                    wall_shelf(B, rng, s0, s1, x0 + .5, 2.2, 2, 1, FRUIT, C["wood"], "z")
                    zones["PRODUCE"].append((x0 + .5, s0, x0 + 1.5, s1, 2.2))
            elif "E" in walls and f["elong"] > 1.8:                                    # east wall luxury cabinets
                s0, s1 = R.z(f["v0"]) - 1.0, R.z(f["v1"]) + 1.0
                wall_shelf(B, rng, s0, s1, x1 - .5, 4.2, 4, -1, PRODUCTS, C["white"], "z", C["v_trim"])
                zones["LUXURY"].append((x1 - 1.5, s0, x1 - .5, s1, 4.2))
                for k in range(2):
                    plinth(B, rng, x1 - 3.4, s0 + (k + .5) * (s1 - s0) / 2, .8, 1.1 + .2 * k, 3)
                    zones["LUXURY"].append((x1 - 4.2, s0, x1 - 2.6, s1, 1.8))
            elif f["elong"] < 1.35 and f["fill"] > .75 and area > 6:                   # round pieces
                r = min(w, d) / 2
                if z == "LUXURY":
                    plinth(B, rng, cx, cz, r * .9, 1.0, 5)
                    B.prism(G, cx, cz, r * .9 + 1.2, r * .9 + 1.2, 18, .02, .035, C["v_rug"], C["v_rug"])
                    for k in range(3):
                        a = k * 2.1 + .5; plinth(B, rng, cx + math.cos(a) * (r + .9), cz + math.sin(a) * (r + .9), .45, .8 + .25 * k, 2)
                else:
                    B.prism(G, cx, cz, r + 1.6, r + 1.6, 18, .02, .035, C["v_rug"], C["v_rug"])
                    round_display(B, rng, cx, cz, r)
                zones[z].append((cx - r - 1, cz - r - 1, cx + r + 1, cz + r + 1, 3.6))
            elif f["elong"] > 1.8 and f["fill"] > .9 and not walls and f["v"] < .55:   # long counter, front half
                checkout(B, rng, cx, cz, max(w, 4.0), True, C["wood_l"])
                B.box(G, cx - max(w, 4.0) / 2, cz - .62, cx + max(w, 4.0) / 2, cz - .58, .2, 1.0, C["v_trim"])
            elif area > 4:                                                             # other free-standing tables
                ax = "x" if f["axis"] == "u" else "z"
                if z == "PRODUCE":
                    produce_stand(B, rng, cx, cz, max(w, d), min(w, d) + .4, 3)
                    zones[z].append((cx - max(w, d) / 2, cz - d / 2, cx + max(w, d) / 2, cz + d / 2, 2.3))
                else:
                    zones[z].append(gondola(B, rng, cx, cz, max(w, d, 3.0), 2.2, 3, ax, PRODUCTS, C["wood_l"], C["v_trim"]))
            else:                                                                      # small: plant or pedestal
                potted(B, rng, cx, cz, 1.1 if area > 1 else .9)
        for k, bx in zones.items():
            if bx and k in shelves: shelf_zone(B, "S001", k, union(*bx), y)
        # Signs over each zone's back wall / the east wall.
        if "PRODUCE" in shelves: sign_board(B, (R.x(.04), z1 - .5), (R.x(.46), z1 - .5), label(shelves["PRODUCE"]), 5.7, .8)
        if "WELLNESS" in shelves: sign_board(B, (R.x(.48), z1 - .5), (R.x(.80), z1 - .5), label(shelves["WELLNESS"]), 5.7, .8)
        if "LUXURY" in shelves: sign_board(B, (x1 - .5, R.z(.92)), (x1 - .5, R.z(.50)), label(shelves["LUXURY"]), 5.7, .8)
        for (u, v) in ((.02, .04), (.98, .04), (.5, .04), (.98, .5), (.02, .97)):
            potted(B, rng, R.x(u), R.z(v), 1.3)

def verdant_fixed(B, rng, x0, z0, x1, z1, y, shelves):
    """Premium grocer: produce stands on the west, wellness wall shelves, an island run and a round display at the
    back, luxury plinths and cabinets on the east, checkout by the door. Cream walls, sage trim, sage rugs."""
    with Lift(B, y):
        room(B, x0, z0, x1, z1, 6.5, 2.6, C["v_wall"], C["v_trim"], C["v_floor"], door_w=5.0)
        R = Local(x0 + .5, z0 + .5, x1 - .5, z1 - .5, 0)
        for (u, v, r) in ((.22, .52, 6.0), (.55, .56, 3.4), (.80, .34, 4.4)):            # rugs under the zones
            B.prism(G, R.x(u), R.z(v), r, r, 16, .02, .04, C["v_rug"], C["v_rug"])
        # Produce (west): stepped stands in a curve, a crate table, wall shelving of fruit.
        boxes = []
        for (u, v, w, d) in ((.14, .74, 5.0, 2.4), (.33, .66, 4.2, 2.2), (.12, .48, 5.0, 2.4), (.30, .40, 4.0, 2.2),
                             (.16, .24, 4.4, 2.0)):
            produce_stand(B, rng, R.x(u), R.z(v), w, d, 2)
            boxes.append((R.x(u) - w / 2, R.z(v) - d / 2, R.x(u) + w / 2, R.z(v) + d / 2, 2.3))
        wall_shelf(B, rng, R.x(.02), R.x(.40), z1 - .5, 3.4, 3, -1, FRUIT, C["wood"])
        boxes.append((R.x(.02), z1 - 1.5, R.x(.40), z1 - .5, 3.4))
        if "PRODUCE" in shelves:
            shelf_zone(B, "S001", "PRODUCE", union(*boxes), y)
            sign_board(B, (R.x(.03), z1 - .5), (R.x(.40), z1 - .5), label(shelves["PRODUCE"]), 4.6, .85)
        # Wellness (back): wall shelving, an island run, a round display.
        wb = []
        wall_shelf(B, rng, R.x(.44), R.x(.98), z1 - .5, 4.0, 4, -1, PRODUCTS, C["wood"])
        wb.append((R.x(.44), z1 - 1.5, R.x(.98), z1 - .5, 4.0))
        wb.append(gondola(B, rng, R.x(.66), R.z(.76), 6.0, 2.3, 3, "x", PRODUCTS, C["wood_l"], C["v_trim"]))
        round_display(B, rng, R.x(.55), R.z(.54), 2.2)
        wb.append((R.x(.55) - 2.2, R.z(.54) - 2.2, R.x(.55) + 2.2, R.z(.54) + 2.2, 3.6))
        if "WELLNESS" in shelves:
            shelf_zone(B, "S001", "WELLNESS", union(*wb), y)
            sign_board(B, (R.x(.46), z1 - .5), (R.x(.98), z1 - .5), label(shelves["WELLNESS"]), 5.0, .85)
        # Luxury (east): white plinths and tall cabinets on the east wall.
        lb = []
        for (u, v, r) in ((.74, .40, .8), (.84, .28, .7), (.86, .46, .75), (.72, .22, .65), (.80, .12, .6)):
            plinth(B, rng, R.x(u), R.z(v), r, 1.0 + rng.range(0, .4), 3)
            lb.append((R.x(u) - r, R.z(v) - r, R.x(u) + r, R.z(v) + r, 1.8))
        wall_shelf(B, rng, R.z(.10), R.z(.62), x1 - .5, 3.8, 4, -1, PRODUCTS, C["white"], "z", C["v_trim"])
        lb.append((x1 - 1.5, R.z(.10), x1 - .5, R.z(.62), 3.8))
        if "LUXURY" in shelves:
            shelf_zone(B, "S001", "LUXURY", union(*lb), y)
            sign_board(B, (x1 - .5, R.z(.62)), (x1 - .5, R.z(.10)), label(shelves["LUXURY"]), 4.7, .8)
        # Checkout, plants.
        checkout(B, rng, R.x(.60), R.z(.14), 3.8, True, C["wood_l"])
        for (u, v) in ((.03, .06), (.42, .95), (.97, .70), (.40, .06), (.66, .06), (.50, .30)):
            potted(B, rng, R.x(u), R.z(v), 1.2)

def cheapcart(B, rng, x0, z0, x1, z1, y, shelves):
    """Discounter: wall shelving along the north and west, rows of gondolas (eye-level shelves in the middle,
    bottom-shelf staples in front), a bargain bin by the door, freezers on the back wall, checkout."""
    with Lift(B, y):
        room(B, x0, z0, x1, z1, 6.0, 2.4, C["c_wall"], C["c_trim"], C["c_floor"], door_w=7.0, pillar=C["c_orange"])
        R = Local(x0 + 1, z0 + 1, x1 - 1, z1 - 1, 0)
        wall_shelf(B, rng, R.x(.02), R.x(.54), z1 - .5, 3.8, 4, -1)
        wall_shelf(B, rng, R.z(.32), R.z(.90), x0 + .5, 3.0, 3, 1, PRODUCTS, None, "z")
        if "BACK_FREEZER" in shelves:
            freezer_wall(B, rng, R.x(.58), R.x(.98), z1 - .5, 3.6, 6)
            shelf_zone(B, "S002", "BACK_FREEZER", (R.x(.58), z1 - 2.6, R.x(.98), z1 - .5, 3.6), y)
            sign_board(B, (R.x(.58), z1 - .5), (R.x(.98), z1 - .5), label(shelves["BACK_FREEZER"]), 4.4, .9)
        eye = [gondola(B, rng, R.x(.30), R.z(.64), 13.0, 3.0, 4, "x"), gondola(B, rng, R.x(.72), R.z(.58), 12.0, 3.0, 4, "x")]
        if "EYE_LEVEL" in shelves:
            shelf_zone(B, "S002", "EYE_LEVEL", eye[0], y)
            hanging_sign(B, R.x(.30), R.z(.64) - .2, 3.7, label(shelves["EYE_LEVEL"]), .7)
        if "BOTTOM_SHELF" in shelves:
            bot = gondola(B, rng, R.x(.52), R.z(.36), 12.0, 2.4, 3, "x", low_big=True)
            shelf_zone(B, "S002", "BOTTOM_SHELF", bot, y)
            hanging_sign(B, R.x(.52), R.z(.36) - .2, 3.1, label(shelves["BOTTOM_SHELF"]), .7)
        if "BARGAIN_BIN" in shelves:
            bargain_bin(B, rng, R.x(.18), R.z(.24), 6.0, 2.6)
            shelf_zone(B, "S002", "BARGAIN_BIN", (R.x(.18) - 3, R.z(.24) - 1.3, R.x(.18) + 3, R.z(.24) + 1.3, 1.8), y)
            hanging_sign(B, R.x(.18), R.z(.24) - 1.5, 2.2, label(shelves["BARGAIN_BIN"]), .6)
        checkout(B, rng, R.x(.84), R.z(.18), 4.5, True)
        prev = B.detail; B.detail = True
        for k in range(4):                                                           # basket stack
            B.box(G, R.x(.93) - .4, R.z(.10) - .3, R.x(.93) + .4, R.z(.10) + .3, k * .22, k * .22 + .2, hexc("#C8473B", 60, PAINT))
        B.detail = prev

def megabulk(B, rng, x0, z0, x1, z1, y, shelves):
    """Warehouse club: pallet racking and floor stacks on the west, bulk sacks on shelving and pallets at the
    back, a field of big produce crates on the east, checkout and a cart row by the door, painted floor lines."""
    with Lift(B, y):
        room(B, x0, z0, x1, z1, 8.0, 2.4, C["m_wall"], C["m_trim"], C["m_floor"], door_w=7.0, pillar=C["m_trim"])
        R = Local(x0 + 1, z0 + 1, x1 - 1, z1 - 1, 0)
        prev = B.detail; B.detail = True                                             # yellow aisle lines
        for v in (.12, .5):
            z = R.z(v); B.quad(G, (R.x(.02), .03, z - .08), (R.x(.02), .03, z + .08), (R.x(.98), .03, z + .08), (R.x(.98), .03, z - .08), C["m_line"])
        B.detail = prev
        if "PALLETS" in shelves:
            pb = []
            racking(B, rng, R.x(.02), R.x(.40), z1 - 2.0, z1 - .5, 5.2, 3, "x"); pb.append((R.x(.02), z1 - 2.0, R.x(.40), z1 - .5, 5.2))
            for u in (.10, .24):
                racking(B, rng, R.x(u) - .8, R.x(u) + .8, R.z(.26), R.z(.70), 4.4, 2, "z"); pb.append((R.x(u) - .8, R.z(.26), R.x(u) + .8, R.z(.70), 4.4))
            for (u, v) in ((.34, .62), (.36, .40), (.31, .26)):
                pallet_stack(B, rng, R.x(u) - .8, R.z(v) - .8, R.x(u) + .8, R.z(v) + .8, 0, rng.range(1.2, 2.2))
                pb.append((R.x(u) - .8, R.z(v) - .8, R.x(u) + .8, R.z(v) + .8, 2.4))
            shelf_zone(B, "S003", "PALLETS", union(*pb), y)
            sign_board(B, (R.x(.03), z1 - .5), (R.x(.40), z1 - .5), label(shelves["PALLETS"]), 6.3, .8)
            # pallet jack
            B.box(G, R.x(.42) - .35, R.z(.50) - 1.0, R.x(.42) + .35, R.z(.50) + .2, 0, .25, C["jack"])
            B.box(G, R.x(.42) - .08, R.z(.50) + .1, R.x(.42) + .08, R.z(.50) + .3, .25, 1.4, C["jack"])
        if "BULK_SACKS" in shelves:
            sb = []
            wall_shelf(B, rng, R.x(.44), R.x(.70), z1 - .5, 4.0, 2, -1, [C["sack"], hexc("#D8C59A"), hexc("#EFE6CF")], C["rack"])
            sb.append((R.x(.44), z1 - 1.5, R.x(.70), z1 - .5, 4.0))
            for (u, v) in ((.47, .76), (.56, .76), (.65, .76), (.51, .62), (.61, .62)):
                sack_stack(B, rng, R.x(u) - .9, R.z(v) - .7, R.x(u) + .9, R.z(v) + .7, rng.pick([2, 3]))
                sb.append((R.x(u) - .9, R.z(v) - .7, R.x(u) + .9, R.z(v) + .7, 1.6))
            shelf_zone(B, "S003", "BULK_SACKS", union(*sb), y)
            sign_board(B, (R.x(.44), z1 - .5), (R.x(.70), z1 - .5), label(shelves["BULK_SACKS"]), 5.3, .9)
        if "BULK_PRODUCE" in shelves:
            cb = []
            for i in range(4):
                for j in range(4):
                    u = .74 + i * .065; v = .22 + j * .17
                    cx, cz = R.x(u), R.z(v)
                    crate(B, rng, cx - 1.1, cz - .9, cx + 1.1, cz + .9, 0, 1.2, FRUIT, C["wood"])
                    cb.append((cx - 1.1, cz - .9, cx + 1.1, cz + .9, 1.7))
            shelf_zone(B, "S003", "BULK_PRODUCE", union(*cb), y)
            sign_board(B, (x1 - .5, R.z(.92)), (x1 - .5, R.z(.30)), label(shelves["BULK_PRODUCE"]), 5.0, .9)
        checkout(B, rng, R.x(.56), R.z(.20), 4.5, True)
        prev = B.detail; B.detail = True                                             # cart row
        for k in range(6):
            cx = R.x(.66) + k * .25
            B.box(G, cx - .1, R.z(.08) - .45, cx + .1, R.z(.08) + .45, .25, 1.0, C["metal_d"])
        B.detail = prev
        for (u, v) in ((.42, .95), (.72, .95)):
            potted(B, rng, R.x(u), R.z(v), 1.2)

BUILDERS = {"dome": verdant, "bigbox_orange": cheapcart, "bigbox_blue": megabulk}
