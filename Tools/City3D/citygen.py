"""Blue Zone Bistro - procedural low-poly city generator (the only generator; Unity imports its output).

Two steps:
  plan   Random, seeded. Decides what stands on every lot (building type, variant, floors, colours, which walls face
         the street) and where every tree goes, and writes it to the lots table
         Assets/GameData/City/Blue_Zone_Bistro_CityLots.csv. Run once; after that the table IS the city ("frozen").
  build  No global randomness. Reads the layout + lots tables and builds the meshes. Small details inside a building
         (rooftop units, which corner an L-shape cuts) come from that row's own `seed` column, so editing one row
         never changes any other building.

Edit the lots table to polish the city (change a type, floors, a colour, delete a row), then build again.
`plan` refuses to overwrite an existing lots table unless given --reroll.

Output (build): Assets/GameData/City/Generated/*.bzbmesh (one per group per tile, imported by BzbMeshImporter),
city_manifest.csv (mesh list) and city_landmarks.csv (click boxes). Unity: Tools > Blue Zone Bistro > Generate
Low-Poly City assembles the prefab from these.

  python Tools/City3D/citygen.py build   [project_root]            plan if needed, then build
  python Tools/City3D/citygen.py plan    [project_root] [--reroll]
  python Tools/City3D/citygen.py preview [project_root] [out.png] [--close]   build in memory and render
  python Tools/City3D/citygen.py add-trees [project_root]   append more trees to the lots table (keeps all rows)
  python Tools/City3D/citygen.py recolor   [project_root]   give every building the current palette
Needs Python 3; preview also needs numpy and Pillow.
"""
import csv, math, os, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cityparts import (car as parts_car, Builder, Rng, PAL, WALLS, TOWER_WALLS, HOUSE_WALLS, ROOFS, BUILDERS, tree, hexc, shade, to_hex,
                       from_hex, inside, area2, mat_of, rock_cluster, car, lamp, bench, catmull_rom, polyline_normals, ribbon,
                       wall_strip, obox, STONE, FOLIAGE, PAINT, GLASS, METAL)

LAYOUT = "Assets/GameData/City/Blue_Zone_Bistro_CityLayout.csv"
LOTS = "Assets/GameData/City/Blue_Zone_Bistro_CityLots.csv"
OUT_DIR = "Assets/GameData/City/Generated"
GROUPS = ("water", "ground", "roads", "buildings", "landmarks", "trees", "props")
NO_SHADOW = ("water", "roads")
TILE_BLOCKS = 4                       # meshes are cut into tiles of 4x4 blocks so the camera can skip off-screen tiles
MAX_VERTS = 60000                     # 16-bit indices
GROUND, LAND, ROAD, WALK = 0.0, 0.6, 0.66, 0.8
LOT_COLUMNS = ["id", "type", "variant", "x0", "z0", "x1", "z1", "base", "floors", "wall", "roof", "faces", "seed", "notes"]

# ---------------- layout ----------------
class Layout:
    def __init__(self, root):
        self.root = root; load_store_shelves(root)
        rows = list(csv.DictReader(open(os.path.join(root, LAYOUT), encoding="utf-8")))
        self.s = {r["id"]: float(r["x"]) for r in rows if r["kind"] == "setting"}
        self.rows = rows
        s = self.s
        self.bx, self.bz, self.bs, self.rw = int(s["blocks_x"]), int(s["blocks_z"]), s["block_size"], s["road_width"]
        self.P = self.bs + self.rw; self.half = self.rw / 2
        # Coastlines are smoothed into curves through the layout's points (Catmull-Rom), counter-clockwise.
        def smooth(raw):
            if len(raw) < 3: return raw
            if area2(raw) < 0: raw = list(reversed(raw))
            return catmull_rom(raw, closed=True, samples=6)
        self.coast_raw = [(self.U(r["x"]), self.U(r["z"])) for r in self.pick("coast")]
        self.island_raw = [(self.U(r["x"]), self.U(r["z"])) for r in self.pick("island")]
        self.coast = smooth(self.coast_raw)
        self.island = smooth(self.island_raw)
        self.lands = [self.coast] + ([self.island] if len(self.island) >= 3 else [])
        self.reserved = [self.rect(r) for k in ("landmark", "park", "pond") for r in self.pick(k)]
    def pick(self, kind): return [r for r in self.rows if r["kind"] == kind]
    def U(self, v): return float(v) * self.P
    def rect(self, r): return (self.U(r["x"]), self.U(r["z"]), self.U(r["x"]) + self.U(r["w"]), self.U(r["z"]) + self.U(r["d"]))
    def on_land(self, x, z): return any(inside(L, x, z) for L in self.lands)

def overlaps(a, b): return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]

# ---------------- plan (random, seeded) ----------------
def plan(L):
    rng = Rng(int(L.s["seed"]))
    rows = []
    def add(**kw):
        kw.setdefault("variant", ""); kw.setdefault("faces", ""); kw.setdefault("notes", "")
        kw["seed"] = rng.next_u32(); kw["id"] = "L%04d" % (len(rows) + 1); rows.append(kw)
    P, bs, half = L.P, L.bs, L.half
    cx0, cz0 = L.U(7.5), L.U(6.5)                       # downtown centre (tallest buildings)
    max_d = math.hypot(L.bx * P, L.bz * P) / 2
    quarters = [(0, 0, .5, .5), (.5, 0, 1, .5), (0, .5, .5, 1), (.5, .5, 1, 1)]
    def faces_of(u0, v0, u1, v1):
        return ("S" if v0 == 0 else "") + ("N" if v1 == 1 else "") + ("W" if u0 == 0 else "") + ("E" if u1 == 1 else "")
    def house_row(lx0, lz0, lx1, lz1, base, faces):
        add(type="house", variant=rng.pick(["gable", "gable", "hip", "lshape"]), x0=lx0, z0=lz0, x1=lx1, z1=lz1, base=base,
            floors=rng.pick([1, 2, 2]), wall=to_hex(rng.pick(HOUSE_WALLS)), roof=to_hex(rng.pick(ROOFS)), faces=faces)
    # Promenade trees along the shore.
    for land in L.lands:
        ccw = area2(land) > 0
        for i in range(len(land)):
            (x1, z1), (x2, z2) = land[i], land[(i + 1) % len(land)]
            dx, dz = x2 - x1, z2 - z1; ln = math.hypot(dx, dz) or 1
            nx, nz = (dz / ln, -dx / ln) if ccw else (-dz / ln, dx / ln)
            n = int(ln // 13)
            for k in range(n):
                if rng.chance(.5):
                    t = (k + .5) / max(1, n); px, pz = x1 + dx * t - nx * 5.5, z1 + dz * t - nz * 5.5
                    if L.on_land(px, pz): add(type="tree", variant="round", x0=px, z0=pz, x1=.85, z1=0, base=LAND, floors=0, wall="", roof="")
    for i in range(L.bx):
        for j in range(L.bz):
            x0, z0 = i * P + half, j * P + half; x1, z1 = x0 + bs, z0 + bs
            if any(overlaps((x0, z0, x1, z1), r) for r in L.reserved):
                # Leftover of a block partly taken by a landmark or park: a pocket park with a few trees.
                for _ in range(6):
                    px, pz = rng.range(x0 + 2, x1 - 2), rng.range(z0 + 2, z1 - 2)
                    if L.on_land(px, pz) and not any(overlaps((px - 4, pz - 4, px + 4, pz + 4), r) for r in L.reserved):
                        add(type="tree", variant="round" if rng.chance(.7) else "poplar", x0=px, z0=pz, x1=.95, z1=0, base=WALK - .05, floors=0, wall="", roof="")
                continue
            if not all(L.on_land(px, pz) for px, pz in ((x0 - 2, z0 - 2), (x1 + 2, z0 - 2), (x1 + 2, z1 + 2), (x0 - 2, z1 + 2))):
                for (u0, v0, u1, v1) in quarters:            # shore block: houses on the quarters fully on land
                    lx0, lz0, lx1, lz1 = x0 + bs * u0 + 2.2, z0 + bs * v0 + 2.2, x0 + bs * u1 - 2.2, z0 + bs * v1 - 2.2
                    if not all(L.on_land(px, pz) for px, pz in ((lx0 - 3, lz0 - 3), (lx1 + 3, lz0 - 3), (lx1 + 3, lz1 + 3), (lx0 - 3, lz1 + 3))): continue
                    house_row(lx0, lz0, lx1, lz1, LAND, faces_of(u0, v0, u1, v1))
                continue
            d = math.hypot((x0 + x1) / 2 - cx0, (z0 + z1) / 2 - cz0) / max_d
            residential = j >= L.bz - 2 or d > 0.78
            waterfront = j <= 1 and not residential and rng.chance(.45)
            if residential: cells = quarters
            elif waterfront: cells = [(0, 0, .5, 1), (.5, 0, 1, 1)] if rng.chance(.5) else [(0, 0, 1, .5), (0, .5, 1, 1)]
            else:
                lots = rng.pick([1, 2, 2, 4]) if d > .3 else rng.pick([1, 1, 2])
                cells = [(0, 0, 1, 1)] if lots == 1 else ([(0, 0, .5, 1), (.5, 0, 1, 1)] if lots == 2 and rng.chance(.5) else
                         [(0, 0, 1, .5), (0, .5, 1, 1)] if lots == 2 else quarters)
            for (u0, v0, u1, v1) in cells:
                inset = 2.2 if residential else 1.6
                lx0, lz0, lx1, lz1 = x0 + bs * u0 + inset, z0 + bs * v0 + inset, x0 + bs * u1 - inset, z0 + bs * v1 - inset
                faces = faces_of(u0, v0, u1, v1)
                if residential:
                    house_row(lx0, lz0, lx1, lz1, WALK, faces)
                elif waterfront:
                    add(type="warehouse", x0=lx0, z0=lz0, x1=lx1, z1=lz1, base=WALK, floors=rng.pick([1, 1, 2]),
                        wall=to_hex(rng.pick([hexc("#D8D3C8"), hexc("#CFC6B6"), hexc("#C9CCC8"), hexc("#B9A58E")])),
                        roof=to_hex(rng.pick([PAL["slate"], hexc("#B7B0A2"), hexc("#9FA6A8")])), faces=faces)
                elif d < .3 and (u1 - u0) * (v1 - v0) >= .5 and rng.chance(.75):
                    add(type="tower", variant=rng.pick(["setback", "glass", "ribbon", "chamfer", "tall"]), x0=lx0, z0=lz0, x1=lx1, z1=lz1,
                        base=WALK, floors=8 + int(rng.value() * 9 * (1 - d / .3) ** .7), wall=to_hex(rng.pick(TOWER_WALLS)),
                        roof=to_hex(PAL["flatroof"]), faces=faces)
                else:
                    top = 8 if d < .45 else 6 if d < .62 else 4
                    add(type="midrise", variant=rng.pick(["grid", "grid", "ribbon", "chamfer", "lshape"]), x0=lx0, z0=lz0, x1=lx1, z1=lz1,
                        base=WALK, floors=2 + int(rng.value() * (top - 1)), wall=to_hex(rng.pick(WALLS)),
                        roof=to_hex(rng.pick([PAL["flatroof"], PAL["roofdark"], hexc("#C7C2B6")])), faces=faces)
            # Street trees along the block edge (small kind, a little sparser than before).
            for k in range(4):
                for t in range(3):
                    if rng.chance(.45):
                        f = (t + .5) / 3
                        px, pz = [(x0 + bs * f, z0 + .9), (x1 - .9, z0 + bs * f), (x1 - bs * f, z1 - .9), (x0 + .9, z1 - bs * f)][k]
                        add(type="tree", variant="small", x0=px, z0=pz, x1=1.15, z1=0, base=WALK, floors=0, wall="", roof="")
    ponds = [L.rect(p) for p in L.pick("pond")]
    for r in L.pick("park"):
        x0, z0, x1, z1 = L.rect(r); plaza = r["style"] == "plaza"
        n = int((x1 - x0) * (z1 - z0) / (110 if not plaza else 320))
        for _ in range(n):
            px, pz = rng.range(x0 + half + 2, x1 - half - 2), rng.range(z0 + half + 2, z1 - half - 2)
            if any(overlaps((px - 3, pz - 3, px + 3, pz + 3), q) for q in ponds): continue
            if plaza and math.hypot(px - (x0 + x1) / 2, pz - (z0 + z1) / 2) < (x1 - x0) * .22: continue
            add(type="tree", variant="poplar" if rng.chance(.3) else "round", x0=px, z0=pz, x1=1.0, z1=0, base=WALK - .05, floors=0, wall="", roof="")
    return rows

def write_lots(path, rows):
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(LOT_COLUMNS)
        for r in rows:
            fmt = lambda v: ("%.2f" % v) if isinstance(v, float) else v
            w.writerow([fmt(r.get(c, "")) for c in LOT_COLUMNS])

def read_lots(path):
    out = []
    for r in csv.DictReader(open(path, encoding="utf-8")):
        if not r.get("type"): continue
        num = lambda k: float(r[k]) if r.get(k, "").strip() else 0.0
        out.append(dict(id=r["id"], type=r["type"].strip(), variant=r.get("variant", "").strip(), x0=num("x0"), z0=num("z0"),
                        x1=num("x1"), z1=num("z1"), base=num("base"), floors=int(num("floors")), wall=r.get("wall", "").strip(),
                        roof=r.get("roof", "").strip(), faces=r.get("faces", "").strip(), seed=int(num("seed")), notes=r.get("notes", "") or ""))
    return out

# ---------------- build (deterministic) ----------------
def boat(B, rng, x, z, kind=None, heading=0.0):
    """Sailboat or motorboat: tapered hull, deck, then a mast + two sails or a cabin."""
    kind = kind or rng.pick(["sail", "sail", "motor"])
    Lb, W = rng.range(6, 9), rng.range(2.2, 3.0)
    ca, sa = math.cos(heading), math.sin(heading)
    local = [(-Lb / 2, -W / 2 * .8), (Lb / 4, -W / 2), (Lb / 2, 0), (Lb / 4, W / 2), (-Lb / 2, W / 2 * .8)]
    pts = [(x + ca * u - sa * v, z + sa * u + ca * v) for u, v in local]
    prev = B.around((x, 0.4, z))
    B.extrude("landmarks", pts, -.3, 1.0, PAL["hull"], PAL["deck"])
    B.extrude("landmarks", [(x + (px - x) * 1.03, z + (pz - z) * 1.03) for px, pz in pts], .55, .75, shade(PAL["sign"], 1.0), None)  # stripe
    B.hint = prev
    if kind == "motor":
        obox(B, "landmarks", x - ca * Lb * .08, z - sa * Lb * .08, Lb * .38, W * .62, 1.0, 2.2, heading, PAL["cabin"], PAL["hull"])
        obox(B, "landmarks", x + ca * Lb * .05, z + sa * Lb * .05, Lb * .08, W * .5, 1.5, 2.1, heading, PAL["window"])
    else:
        mx, mz = x + ca * Lb * .05, z + sa * Lb * .05
        B.box("landmarks", mx - .12, mz - .12, mx + .12, mz + .12, 1.0, 1.0 + Lb * 1.1, PAL["hull"])
        tip = (mx, 1.0 + Lb * 1.05, mz)
        B.tri("landmarks", (mx - ca * .2, 1.4, mz - sa * .2), tip, (mx - ca * Lb * .42, 1.4, mz - sa * Lb * .42), PAL["sail"], (-sa, 0, ca))
        B.tri("landmarks", (mx + ca * .2, 1.3, mz + sa * .2), (mx, 1.0 + Lb * .85, mz), (mx + ca * Lb * .45, 1.2, mz + sa * Lb * .45), shade(PAL["sail"], .96), (-sa, 0, ca))

STORES_CSV = "Assets/GameData/CSV/Blue_Zone_Bistro_Stores.csv"
STORE_SHELVES = {}                 # store id -> {shelf id: display name}, from the store data (shelf_definitions)

def load_store_shelves(root):
    STORE_SHELVES.clear()
    path = os.path.join(root, STORES_CSV)
    if not os.path.exists(path): return
    for r in csv.DictReader(open(path, encoding="utf-8")):
        shelves = {}
        for part in (r.get("shelf_definitions") or "").split("|"):
            if ":" in part:
                k, v = part.split(":", 1); shelves[k.strip()] = v.strip()
        STORE_SHELVES[r["store_id"].strip()] = shelves

def hash_id(text):
    h = 2166136261
    for ch in text.encode(): h = ((h ^ ch) * 16777619) & 0xFFFFFFFF
    return h or 1

def _coast_z_below(L, x, z):
    """Highest z where the coast crosses x south of z (the south waterfront), or None."""
    best = None; pts = L.coast
    for i in range(len(pts)):
        (ax, az), (bx, bz) = pts[i], pts[(i + 1) % len(pts)]
        if (ax - x) * (bx - x) <= 0 and ax != bx:
            zc = az + (bz - az) * (x - ax) / (bx - ax)
            if zc < z and (best is None or zc > best): best = zc
    return best

def landmark(B, rng, r, rc, L=None):
    """The five hero places (citylandmarks.py) on their plots."""
    import citylandmarks as hero
    x0, z0, x1, z1 = rc; inset = 4.0
    x0, z0, x1, z1 = x0 + inset, z0 + inset, x1 - inset, z1 - inset
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    style = r["style"]
    if style != "tower": B.box("ground", x0, z0, x1, z1, LAND, WALK, PAL["plaza"])
    store = style in ("dome", "bigbox_orange", "bigbox_blue")
    if store: B.redirect = "store_%s_shell" % r["id"]               # the building itself; the hero builder resets it for the site
    if style == "dome":
        top = hero.verdant(B, rng, x0, z0, x1, z1, WALK)
    elif style == "bigbox_orange":
        top = hero.cheapcart(B, rng, x0, z0, x1, z1, WALK)
    elif style.startswith("bigbox"):
        top = hero.megabulk(B, rng, x0, z0, x1, z1, WALK)
    elif style == "tower":
        top = hero.news_tower(B, rng, cx, cz, WALK)
    elif style == "bistro":
        zs = [_coast_z_below(L, x, z0) for x in (x0, cx, x1)] if L is not None else []
        zs = [z for z in zs if z is not None]
        front = (max(zs) + TERRACE_W + .3) if zs else z0 - 10
        top = hero.bistro(B, rng, x0, z0, x1, z1, WALK, front)
    else:
        top = WALK + 6
    B.redirect = None
    if store and B.footprint is not None:
        import cityinteriors
        fx0, fz0, fx1, fz1, fy = B.footprint
        # The room is smaller than the building (stylised, denser, like the 2D store screens), centred in it.
        tw, td = {"dome": (99, 99), "bigbox_orange": (44, 22), "bigbox_blue": (40, 26)}[style]   # Verdant: room = building
        cxr, czr = (fx0 + fx1) / 2, (fz0 + fz1) / 2
        w, d = min(tw, fx1 - fx0), min(td, fz1 - fz0)
        fx0, fx1, fz0, fz1 = cxr - w / 2, cxr + w / 2, czr - d / 2, czr + d / 2
        B.redirect = "store_%s_interior" % r["id"]
        cityinteriors.BUILDERS[style](B, Rng(hash_id(r["id"])), fx0, fz0, fx1, fz1, fy, STORE_SHELVES.get(r["id"], {}))
        B.redirect = None; B.footprint = None
    B.colliders.append((r["id"], (cx, (WALK + top) / 2, cz), (x1 - x0, top - WALK, z1 - z0)))

TERRACE_TOP = LAND + 1.6          # raised waterfront terrace: 2.2 m above the water, 1.6 m above the street
TERRACE_W = 4.5                   # its widest; narrower where a building or street comes close to the shore
WALL_FOOT = -1.2

def _shore_kinds(pts, raw_count, beach_runs, zones):
    """Per smoothed segment: 'beach', 'terrace' (raised stone embankment with railing) or 'low' (old low seawall:
    used where the bridge lands, so its ramp meets ground level). Piers force 'terrace' (their deck meets it)."""
    n = len(pts); per = max(1, n // max(1, raw_count))
    kinds = []
    for i in range(n):
        seg = (i // per) % max(1, raw_count)
        k = "beach" if seg in beach_runs else "terrace"
        mx, mz = (pts[i][0] + pts[(i + 1) % n][0]) / 2, (pts[i][1] + pts[(i + 1) % n][1]) / 2
        for kind, (x0, z0, x1, z1) in zones:
            if x0 <= mx <= x1 and z0 <= mz <= z1: k = kind
        kinds.append(k)
    return kinds

def _top(kind):
    return TERRACE_TOP if kind == "terrace" else LAND + .3 if kind == "low" else LAND + .05

def _runs(kinds, kind):
    """Position 0..1 of every segment inside its run of consecutive `kind` segments (closed loop)."""
    n = len(kinds); t = [None] * n
    if all(k == kind for k in kinds): return [(i + .5) / n for i in range(n)]
    start = next(i for i in range(n) if kinds[i] != kind)
    i = start
    for _ in range(n):
        if kinds[i] == kind and kinds[i - 1] != kind:
            run = []; j = i
            while kinds[j] == kind: run.append(j); j = (j + 1) % n
            for k, idx in enumerate(run): t[idx] = (k, len(run))
        i = (i + 1) % n
    return t

def shoreline(B, rng, land, raw_count, beach_runs, zones=(), obstacles=()):
    """The waterfront, after the painted city: raised stone terraces (smooth wall, darker waterline, coping, railing
    with posts, steps down to the street side) alternating with sandy coves behind a low kerb wall. Boulders only
    as short revetment runs tight against the wall and at the points where a beach meets the wall; no rocks out in
    the water. Outside: a shallow-water band (aux 0 at the shore -> 255 offshore) for the water shader."""
    pts = land; n = len(pts); normals = polyline_normals(pts)
    kinds = _shore_kinds(pts, raw_count, beach_runs, zones)
    per = max(1, n // max(1, raw_count))
    riprap = {k for k in range(raw_count) if rng.chance(.18)}
    def clearance(i):
        px, pz, nx, nz = pts[i][0], pts[i][1], normals[i][0], normals[i][1]
        d = 0.5
        while d < 12:
            x, z = px - nx * d, pz - nz * d
            if any(x0 <= x <= x1 and z0 <= z <= z1 for x0, z0, x1, z1 in obstacles): return d
            d += .25
        return 12.0
    clear = [clearance(i) for i in range(n)]
    W = [max(2.2, min(TERRACE_W, clear[i] - .5)) for i in range(n)]               # terrace width per point
    # Beach width per point: a crescent across each run of beach segments.
    bt = _runs(kinds, "beach")
    BW = [3.0] * n
    for i in range(n):
        if kinds[i] == "beach" and bt[i] is not None:
            k, m = bt[i]; BW[i] = 3.0 + 12.0 * math.sin(math.pi * k / m) ** .8
    def P(i, o, y): return (pts[i][0] + normals[i][0] * o, y, pts[i][1] + normals[i][1] * o)
    def band(g, i, j, oi0, oj0, oi1, oj1, y0, y1, col, out=(0.0, 1.0, 0.0)):
        B.quad(g, P(i, oi0, y0), P(i, oi1, y1), P(j, oj1, y1), P(j, oj0, y0), col, out)
    def face(g, i, j, oi, oj, y0, y1, col, facing=1.0):
        nx, nz = (normals[i][0] + normals[j][0]) * facing, (normals[i][1] + normals[j][1]) * facing
        B.quad(g, P(i, oi, y0), P(i, oi, y1), P(j, oj, y1), P(j, oj, y0), col, (nx, 0.0, nz))
    lamp_gap, stair_gap, post_gap = 0.0, 30.0, 0.0
    for i in range(n):
        j = (i + 1) % n
        kind, prev_kind = kinds[i], kinds[i - 1]
        length = math.hypot(pts[j][0] - pts[i][0], pts[j][1] - pts[i][1])
        px, pz, nx, nz = pts[i][0], pts[i][1], normals[i][0], normals[i][1]
        tx, tz = (pts[j][0] - px) / (length or 1), (pts[j][1] - pz) / (length or 1)      # along the shore
        if kind != prev_kind:
            _shore_cap(B, rng, px, pz, nx, nz, tx, tz, prev_kind, kind, W[i])
        wj = W[j] if kinds[j] == "terrace" else W[i]
        bj = BW[j] if kinds[j] == "beach" else 3.0
        if kind == "beach":
            band("ground", i, j, -1.6, -1.6, .4, .4, LAND + .05, LAND + .02, PAL["sand"])                 # dry sand over the lawn edge
            band("ground", i, j, .4, .4, BW[i], bj, LAND + .02, -0.6, PAL["sand"])
            face("ground", i, j, -1.6, -1.6, LAND - .1, LAND + .55, PAL["seawall"])                  # kerb wall
            band("ground", i, j, -2.1, -2.1, -1.5, -1.5, LAND + .55, LAND + .55, PAL["coping"])
            inner = -2.1
        elif kind == "low":
            face("ground", i, j, 0.0, 0.0, WALL_FOOT, LAND + .3, PAL["seawall"])
            band("ground", i, j, -1.4, -1.4, .15, .15, LAND + .3, LAND + .3, PAL["coping"])
            for h in (.55, 1.0):
                face("props", i, j, -.2, -.2, LAND + .3 + h, LAND + .37 + h, PAL["railing"])
            inner = -1.4
        else:
            T = TERRACE_TOP
            face("ground", i, j, 0.0, 0.0, WALL_FOOT, T, PAL["seawall"])                               # wall face
            face("ground", i, j, .02, .02, -0.05, .38, PAL["wallfoot"])                                   # waterline
            face("ground", i, j, .22, .22, T - .04, T + .14, PAL["coping"])                               # coping lip
            band("ground", i, j, -.35, -.35, .22, .22, T + .14, T + .14, PAL["coping"])
            band("ground", i, j, -W[i], -wj, -.35, -.35, T, T, PAL["promenade"])                          # deck
            face("ground", i, j, -W[i], -wj, LAND - .1, T, PAL["seawall"], -1.0)                          # street-side wall
            band("ground", i, j, -W[i] - .3, -wj - .3, -W[i] + .05, -wj + .05, T + .1, T + .1, PAL["coping"])
            face("ground", i, j, -W[i] - .3, -wj - .3, T - .1, T + .1, PAL["coping"], -1.0)
            # Railing: two rails along the coping, posts every ~2.4 m.
            for h, th in ((.5, .06), (1.0, .09)):
                face("props", i, j, -.05, -.05, T + .14 + h, T + .14 + h + th, PAL["railing"])
                face("props", i, j, -.05, -.05, T + .14 + h, T + .14 + h + th, PAL["railing"], -1.0)
            d = post_gap
            while d < length:
                qx, qz = px + tx * d - nx * .05, pz + tz * d - nz * .05
                B.box("props", qx - .07, qz - .07, qx + .07, qz + .07, T + .14, T + 1.25, PAL["post"])
                d += 2.4
            post_gap = d - length
            # Boulder revetment on some stretches: a tight row against the wall foot, not spread into the water.
            if ((i // per) % max(1, raw_count)) in riprap:
                for k in range(2):
                    o = (k + .5) / 2 * length
                    rock_cluster(B, rng, px + tx * o + nx * rng.range(.9, 1.5), -0.55, pz + tz * o + nz * rng.range(.9, 1.5), rng.range(.9, 1.3))
            # Steps from the street side up to the terrace where there is room for them.
            stair_gap += length
            if stair_gap > 40 and kinds[j] == "terrace" and clear[i] > W[i] + 4.0:
                stair_gap = 0.0
                ang = math.atan2(nz, nx); steps = 6
                for k in range(steps):
                    o = -W[i] - .3 - (k + .5) * .5
                    obox(B, "ground", px + nx * o + tx * 2, pz + nz * o + tz * 2, .5, 2.8, LAND - .1, LAND + T * 0 + (T - LAND) * (steps - k) / (steps + 1), ang, PAL["coping"])
            inner = -W[i] - .3
        # Promenade path inland of the shore, lamps and benches along it.
        band("ground", i, j, -8.5, -8.5, inner, inner if kind != "terrace" else -wj - .3, LAND + .03, LAND + .03, PAL["promenade"])
        lamp_gap += length
        if lamp_gap > 22:
            lamp_gap = 0.0
            if kind == "terrace": lamp(B, px - nx * 1.2, pz - nz * 1.2, TERRACE_TOP)
            else: lamp(B, px - nx * (-inner + .6), pz - nz * (-inner + .6), LAND + .03)
            if kind == "terrace" and W[i] > 3.6 and rng.chance(.6):
                bench(B, px - nx * (W[i] - .8), pz - nz * (W[i] - .8), TERRACE_TOP, math.atan2(nz, nx) + math.pi / 2)
    # Shallow water band (in the water group: drawn by the water shader).
    # Sloped (0.06 at the shore -> 0.004 offshore) so overlapping pieces in tight bays intersect instead of flickering.
    ribbon(B, "water", pts, normals, 0.0, 30.0, 0.06, 0.004, PAL["shore"], closed=True, aux=(0, 255))

def _shore_cap(B, rng, px, pz, nx, nz, tx, tz, a, b, w):
    """Where two kinds of shore meet: close the taller one with an end face, and put a rocky point at a beach end."""
    hi, lo = (a, b) if _top(a) >= _top(b) else (b, a)
    face = (tx, 0.0, tz) if hi == a else (-tx, 0.0, -tz)          # towards the lower side
    inner = -w - .3 if hi == "terrace" else -1.4
    y_lo = _top(lo) if lo != "beach" else LAND - .1
    P = lambda o, y: (px + nx * o, y, pz + nz * o)
    B.quad("ground", P(inner, y_lo), P(inner, _top(hi) + .1), P(.22, _top(hi) + .1), P(.22, y_lo), PAL["seawall"], face)
    B.quad("ground", P(-.6, WALL_FOOT), P(-.6, y_lo), P(.22, y_lo), P(.22, WALL_FOOT), PAL["seawall"], face)
    if "beach" in (a, b):
        for k in range(rng.pick([2, 3])):
            rock_cluster(B, rng, px + nx * rng.range(.8, 3.0) - face[0] * rng.range(0, 1.5), -0.45,
                         pz + nz * rng.range(.8, 3.0) - face[2] * rng.range(0, 1.5), rng.range(.8, 1.3))

def radial_plaza(B, rng, rc):
    """Round plaza: paved disk, two rings, eight spokes, grass sectors with a tree each, a ring of trees, benches."""
    x0, z0, x1, z1 = rc
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    R = min(x1 - x0, z1 - z0) / 2 - 1.0
    y = WALK - .03
    disk = [(cx + math.cos(2 * math.pi * k / 48) * R, cz + math.sin(2 * math.pi * k / 48) * R) for k in range(48)]
    B.polygon("ground", disk, y, PAL["plaza"])
    ring = lambda r0, r1, col, yy: ribbon(B, "ground", [(cx + math.cos(2 * math.pi * k / 48) * r0, cz + math.sin(2 * math.pi * k / 48) * r0) for k in range(48)],
                                         [(math.cos(2 * math.pi * k / 48), math.sin(2 * math.pi * k / 48)) for k in range(48)], 0.0, r1 - r0, yy, yy, col)
    ring(R * .30, R * .36, PAL["paving2"], y + .01)
    ring(R * .93, R * .99, PAL["paving2"], y + .01)
    for k in range(8):                                                           # grass sectors between the spokes
        a0 = 2 * math.pi * k / 8 + .16; a1 = 2 * math.pi * (k + 1) / 8 - .16
        arc = [(a0 + (a1 - a0) * t / 6) for t in range(7)]
        sector = [(cx + math.cos(a) * R * .48, cz + math.sin(a) * R * .48) for a in arc] + \
                 [(cx + math.cos(a) * R * .86, cz + math.sin(a) * R * .86) for a in reversed(arc)]
        prev = B.around((cx, y, cz))
        B.extrude("ground", sector, y, y + .25, PAL["hedge"], PAL["grass"])
        B.hint = prev
        am = (a0 + a1) / 2
        tree(B, rng, cx + math.cos(am) * R * .67, y + .25, cz + math.sin(am) * R * .67, 1.0, rng.pick(["broad", "round"]))
        bench(B, cx + math.cos(am) * R * .42, cz + math.sin(am) * R * .42, y, am + math.pi / 2)
    for k in range(18):                                                          # outer ring of trees and lamps
        a = 2 * math.pi * (k + .5) / 18
        tree(B, rng, cx + math.cos(a) * R * .9, y, cz + math.sin(a) * R * .9, .8, "small")
        if k % 3 == 0: lamp(B, cx + math.cos(a + .17) * R * .9, cz + math.sin(a + .17) * R * .9, y)

def park_paths(B, rng, rc, ponds):
    """Hedge border, a curved loop path and a winding cross path, lamps along the loop."""
    x0, z0, x1, z1 = rc
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2; w, d = x1 - x0, z1 - z0
    y = WALK - .03
    border = [(x0 + 1, z0 + 1), (x1 - 1, z0 + 1), (x1 - 1, z1 - 1), (x0 + 1, z1 - 1)]
    loop_ctrl = [(cx + math.cos(2 * math.pi * k / 7) * w * .36 * rng.range(.85, 1.05), cz + math.sin(2 * math.pi * k / 7) * d * .34 * rng.range(.85, 1.05)) for k in range(7)]
    loop = catmull_rom(loop_ctrl, closed=True, samples=6)
    ribbon(B, "ground", loop, polyline_normals(loop), -1.3, 1.3, y + .02, y + .02, PAL["promenade"])
    cross_ctrl = [(x0 + 1, cz + rng.range(-3, 3)), (cx - w * .2, cz + rng.range(-8, 8)), (cx + w * .2, cz + rng.range(-8, 8)), (x1 - 1, cz + rng.range(-3, 3))]
    cross = catmull_rom(cross_ctrl, closed=False, samples=8)
    ribbon(B, "ground", cross, polyline_normals(cross, closed=False), -1.0, 1.0, y + .025, y + .025, PAL["promenade"], closed=False)
    for i in range(0, len(loop), 7):
        lamp(B, loop[i][0], loop[i][1], y)
    # Hedge along the border (low wall with a green top).
    for i in range(4):
        a, b = border[i], border[(i + 1) % 4]
        dx, dz = b[0] - a[0], b[1] - a[1]; l = math.hypot(dx, dz)
        for k in range(int(l // 6)):
            t0, t1 = k * 6 / l + .02, min(1, (k * 6 + 5) / l)
            p0 = (a[0] + dx * t0, a[1] + dz * t0); p1 = (a[0] + dx * t1, a[1] + dz * t1)
            obox(B, "trees", (p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2, math.hypot(p1[0] - p0[0], p1[1] - p0[1]), .9, y, y + .9, math.atan2(dz, dx), PAL["hedge"])

def arched_bridge(B, rc):
    """Deck rising in an arch over the water, parapets, and a small tower with a cap at each end on both sides."""
    x0, z0, x1, z1 = rc
    xa, xb = x0 - 6, x1 + 6
    N = 18
    def h(t): return WALK + .1 + 4.2 * math.sin(math.pi * t)
    prev = B.around(((xa + xb) / 2, -20.0, (z0 + z1) / 2))
    for k in range(N):
        t0, t1 = k / N, (k + 1) / N
        xa0, xa1 = xa + (xb - xa) * t0, xa + (xb - xa) * t1
        y0, y1 = h(t0), h(t1)
        B.quad("landmarks", (xa0, y0, z0), (xa0, y0, z1), (xa1, y1, z1), (xa1, y1, z0), PAL["paving2"], (0, 1, 0))               # road deck
        for zz, s_ in ((z0, -1), (z1, 1)):                                                                                       # parapets
            B.quad("landmarks", (xa0, y0 - .9, zz), (xa0, y0 + 1.0, zz), (xa1, y1 + 1.0, zz), (xa1, y1 - .9, zz), PAL["coping"], (0, 0, s_))
            B.quad("landmarks", (xa0, y0 + 1.0, zz), (xa0, y0 + 1.0, zz - s_ * .5), (xa1, y1 + 1.0, zz - s_ * .5), (xa1, y1 + 1.0, zz), PAL["coping"], (0, 1, 0))
        # Arch underside: from deck down to a lower arc that meets the water at the ends.
        ya0, ya1 = -0.5 + 3.2 * math.sin(math.pi * t0), -0.5 + 3.2 * math.sin(math.pi * t1)
        for zz, s_ in ((z0, -1), (z1, 1)):
            B.quad("landmarks", (xa0, ya0, zz), (xa0, y0 - .9, zz), (xa1, y1 - .9, zz), (xa1, ya1, zz), PAL["seawall"], (0, 0, s_))
        B.quad("landmarks", (xa0, ya0, z0), (xa0, ya0, z1), (xa1, ya1, z1), (xa1, ya1, z0), shade(PAL["seawall"], .85), (0, -1, 0))
    B.hint = prev
    for xx in (xa + 1.2, xb - 1.2):
        for zz in (z0 - .6, z1 + .6):
            B.box("landmarks", xx - 1.0, zz - 1.0, xx + 1.0, zz + 1.0, WALK, WALK + 8, PAL["coping"], PAL["coping"])
            B.pyramid("landmarks", xx, zz, 1.4, 4, WALK + 8, WALK + 10.5, PAL["slate"], math.pi / 4)

def pier(B, rng, rc, d, L):
    """Plank pier running out from the shore (south) for d metres, posts every 4 m, boats moored on both sides."""
    x0, z0, x1, z1 = rc
    cx = (x0 + x1) / 2
    zs = z1
    while zs > z0 - 200 and L.on_land(cx, zs): zs -= 1.0                     # the shoreline below the pier's top
    top = TERRACE_TOP + .06                                                    # level with the terrace it leaves from
    za, zb = zs + 1.0, zs - d                                                   # start a little on land
    B.box("landmarks", x0, zb, x1, za, .7, top, PAL["planks"])
    zz = zs - 1.0
    while zz > zb + .5:
        for xx in (x0 + .3, x1 - .3):
            B.box("landmarks", xx - .18, zz - .18, xx + .18, zz + .18, -1.0, top + .5, PAL["bench"])
        zz -= 4.0
    for k in range(4):
        side = 1 if k % 2 == 0 else -1
        bz = zs - d * (.25 + .2 * k)
        boat(B, rng, (x1 + 4.0) if side > 0 else (x0 - 4.0), bz, None, -math.pi / 2)

def street_cars(B, rng, L):
    P, half, bs = L.P, L.half, L.bs
    plots = [L.rect(r) for r in L.pick("landmark")]                 # streets under a landmark plot are paved over
    def car(B_, rng_, x, z, a, y):
        if any(x0 - 2 <= x <= x1 + 2 and z0 - 2 <= z <= z1 + 2 for x0, z0, x1, z1 in plots): return
        parts_car(B_, rng_, x, z, a, y)
    for i in range(L.bx + 1):
        for j in range(L.bz + 1):
            x, z = i * P, j * P
            if i < L.bx and L.on_land(x, z) and L.on_land(x + P, z) and L.on_land(x + P / 2, z):
                for lane in (-1, 1):
                    if rng.chance(.33):
                        car(B, rng, x + half + rng.range(3, bs - 3), z + lane * 1.9, 0.0 if lane > 0 else math.pi, ROAD)
            if j < L.bz and L.on_land(x, z) and L.on_land(x, z + P) and L.on_land(x, z + P / 2):
                for lane in (-1, 1):
                    if rng.chance(.33):
                        car(B, rng, x + lane * 1.9, z + half + rng.range(3, bs - 3), math.pi / 2 if lane < 0 else -math.pi / 2, ROAD)

def crosswalks(B, L):
    """Detail: zebra crossings at both ends of every street segment."""
    P, half = L.P, L.half
    y = ROAD + .015
    B.detail = True
    for i in range(L.bx + 1):
        for j in range(L.bz + 1):
            x, z = i * P, j * P
            if i < L.bx and L.on_land(x, z) and L.on_land(x + P, z) and L.on_land(x + P / 2, z):
                for cx in (x + half + 1.6, x + P - half - 1.6):
                    for k in range(6):
                        zz = z - half + .7 + k * (2 * half - 1.4) / 5.5
                        B.quad("roads", (cx - 1.1, y, zz), (cx - 1.1, y, zz + .55), (cx + 1.1, y, zz + .55), (cx + 1.1, y, zz), PAL["dash"])
            if j < L.bz and L.on_land(x, z) and L.on_land(x, z + P) and L.on_land(x, z + P / 2):
                for cz in (z + half + 1.6, z + P - half - 1.6):
                    for k in range(6):
                        xx = x - half + .7 + k * (2 * half - 1.4) / 5.5
                        B.quad("roads", (xx, y, cz - 1.1), (xx, y, cz + 1.1), (xx + .55, y, cz + 1.1), (xx + .55, y, cz - 1.1), PAL["dash"])
    B.detail = False

SEA = 2500.0   # metres of sea around the city, so dragging never reaches its edge (fog hides the horizon)

def build(L, lots):
    B = Builder(); rng = Rng(int(L.s["seed"]) ^ 0x5BD1E995)   # only for ground scatter, ponds, boats, cars
    P, bs, half = L.P, L.bs, L.half
    W, D = L.bx * P, L.bz * P
    B.quad("water", (-SEA, GROUND, -SEA), (-SEA, GROUND, D + SEA), (W + SEA, GROUND, D + SEA), (W + SEA, GROUND, -SEA), PAL["water"], None, (255, 255, 255, 255))
    shore_rng = Rng(int(L.s["seed"]) ^ 0x2545F491)
    zones = []
    for r in L.pick("pier"):
        x0, z0, x1, z1 = L.rect(r); zones.append(("terrace", (x0 - 8, z0 - 40, x1 + 8, z1 + 8)))
    for r in L.pick("bridge"):
        x0, z0, x1, z1 = L.rect(r); zones.append(("low", (x0 - 16, z0 - 16, x1 + 16, z1 + 16)))
    obstacles = [(r["x0"], r["z0"], r["x1"], r["z1"]) for r in lots if r["type"] != "tree"]
    for i in range(L.bx + 1):                               # streets (crossings and segments)
        for j in range(L.bz + 1):
            x, z = i * P, j * P
            obstacles.append((x - half, z - half, x + P - half, z + half)); obstacles.append((x - half, z - half, x + half, z + P - half))
    obstacles += [L.rect(r) for k in ("landmark", "park") for r in L.pick(k)]
    beaches = [{k for k in range(len(raw)) if shore_rng.chance(.3)} for raw in (L.coast_raw, L.island_raw)]
    for (land, raw), beach in zip(((L.coast, L.coast_raw), (L.island, L.island_raw)), beaches):
        if len(land) < 3: continue
        B.polygon("ground", land, LAND, PAL["grass2"])          # open land is lawn; streets, blocks and paths sit on top
        shoreline(B, shore_rng, land, len(raw), beach, zones, obstacles)
    # Streets with dashed centre lines.
    for i in range(L.bx + 1):
        for j in range(L.bz + 1):
            x, z = i * P, j * P
            if L.on_land(x, z):
                B.quad("roads", (x - half, ROAD, z - half), (x - half, ROAD, z + half), (x + half, ROAD, z + half), (x + half, ROAD, z - half), PAL["road"])
            if i < L.bx and L.on_land(x, z) and L.on_land(x + P, z) and L.on_land(x + P / 2, z):
                B.quad("roads", (x + half, ROAD, z - half), (x + half, ROAD, z + half), (x + P - half, ROAD, z + half), (x + P - half, ROAD, z - half), PAL["road"])
                for k in range(3):
                    a = x + half + 3 + k * (bs - 4) / 3
                    B.quad("roads", (a, ROAD + .01, z - .12), (a, ROAD + .01, z + .12), (a + 3, ROAD + .01, z + .12), (a + 3, ROAD + .01, z - .12), PAL["dash"])
            if j < L.bz and L.on_land(x, z) and L.on_land(x, z + P) and L.on_land(x, z + P / 2):
                B.quad("roads", (x - half, ROAD, z + half), (x - half, ROAD, z + P - half), (x + half, ROAD, z + P - half), (x + half, ROAD, z + half), PAL["road"])
                for k in range(3):
                    a = z + half + 3 + k * (bs - 4) / 3
                    B.quad("roads", (x - .12, ROAD + .01, a), (x - .12, ROAD + .01, a + 3), (x + .12, ROAD + .01, a + 3), (x + .12, ROAD + .01, a), PAL["dash"])
    street_cars(B, rng, L)
    crosswalks(B, L)
    # Pavement slabs under full blocks.
    for i in range(L.bx):
        for j in range(L.bz):
            x0, z0 = i * P + half, j * P + half; x1, z1 = x0 + bs, z0 + bs
            full = all(L.on_land(px, pz) for px, pz in ((x0 - 2, z0 - 2), (x1 + 2, z0 - 2), (x1 + 2, z1 + 2), (x0 - 2, z1 + 2)))
            if any(overlaps((x0, z0, x1, z1), r) for r in L.reserved):
                if full: B.box("ground", x0, z0, x1, z1, LAND, WALK - .09, PAL["grass"])     # pocket park (landmark plazas sit above it)
                continue
            if full:
                B.box("ground", x0, z0, x1, z1, LAND, WALK, PAL["sidewalk"])
    # Lots: buildings and trees, each from its own seed.
    # Lot trees stay off the hero plots (parking, forecourts) and the bistro terrace down to the water.
    keep_out = []
    for r in L.pick("landmark"):
        if r["style"] == "tower": continue
        x0, z0, x1, z1 = L.rect(r)
        keep_out.append((x0 + 3, z0 + 3 - (22 if r["style"] == "bistro" else 0), x1 - 3, z1 - 3))
    for r in lots:
        local = Rng(r["seed"] or 1)
        if r["type"] == "tree":
            if any(a <= r["x0"] <= c and b <= r["z0"] <= d for a, b, c, d in keep_out): continue
            tree(B, local, r["x0"], r["base"], r["z0"], r["x1"] or 1.0, r["variant"] or "round"); continue
        fn = BUILDERS.get(r["type"])
        if fn is None: print("unknown lot type", r["id"], r["type"]); continue
        house = r["type"] == "house"
        row = dict(r, rect=(r["x0"], r["z0"], r["x1"], r["z1"]), wall=from_hex(r["wall"] or "#E6E4DE"),
                   roof=from_hex(r["roof"] or "#CBCBC6", 50 if house else 0, PAINT if house else STONE))
        fn(B, row, local)
    # Parks, plaza, ponds, landmarks, bridge, piers.
    ponds = [L.rect(p) for p in L.pick("pond")]
    for r in L.pick("park"):
        x0, z0, x1, z1 = L.rect(r)
        inner = (x0 + half, z0 + half, x1 - half, z1 - half)
        B.box("ground", inner[0], inner[1], inner[2], inner[3], LAND, WALK - .05, PAL["grass"])
        if r["style"] == "plaza": radial_plaza(B, rng, inner)
        else: park_paths(B, rng, inner, ponds)
    for r in L.pick("pond"):
        x0, z0, x1, z1 = L.rect(r)
        cx, cz, rx, rz = (x0 + x1) / 2, (z0 + z1) / 2, (x1 - x0) / 2, (z1 - z0) / 2
        ctrl = [(cx + math.cos(a) * rx * rng.range(.85, 1), cz + math.sin(a) * rz * rng.range(.85, 1)) for a in [2 * math.pi * k / 8 for k in range(8)]]
        pts = catmull_rom(ctrl, closed=True, samples=5)
        B.polygon("water", pts, WALK - .02, PAL["pond"])
        ribbon(B, "ground", pts, polyline_normals(pts), -.1, 1.1, WALK + .03, WALK + .03, PAL["coping"])     # stone rim
    for r in L.pick("landmark"):
        landmark(B, rng, r, L.rect(r), L)
    for r in L.pick("bridge"):
        arched_bridge(B, L.rect(r))
    for r in L.pick("pier"):
        pier(B, rng, L.rect(r), L.U(r["d"]), L)
    return B

# ---------------- table edits: more trees, new palette ----------------
def add_trees(L, rows):
    """Append trees to an existing lots table (keeps every existing row): denser street trees, fuller parks and
    pocket parks, a few bushes. Skips points inside buildings, ponds, the round plaza's paving and near other trees."""
    rng = Rng(int(L.s["seed"]) ^ 0x7F4A7C15)
    P, bs, half = L.P, L.bs, L.half
    buildings = [(r["x0"], r["z0"], r["x1"], r["z1"]) for r in rows if r["type"] != "tree"]
    ponds = [L.rect(p) for p in L.pick("pond")]
    plazas = []
    for r in L.pick("park"):
        if r["style"] == "plaza":
            x0, z0, x1, z1 = L.rect(r); plazas.append(((x0 + x1) / 2, (z0 + z1) / 2, min(x1 - x0, z1 - z0) / 2))
    grid = {}
    def key(x, z): return (int(x // 4), int(z // 4))
    def near(x, z, dmin):
        kx, kz = key(x, z)
        for ix in range(kx - 1, kx + 2):
            for iz in range(kz - 1, kz + 2):
                for (px, pz) in grid.get((ix, iz), ()):
                    if (px - x) ** 2 + (pz - z) ** 2 < dmin * dmin: return True
        return False
    for r in rows:
        if r["type"] == "tree": grid.setdefault(key(r["x0"], r["z0"]), []).append((r["x0"], r["z0"]))
    added = []
    def add(x, z, variant, scale, base):
        if not L.on_land(x, z) or near(x, z, 3.2): return
        if any(b[0] - 1 < x < b[2] + 1 and b[1] - 1 < z < b[3] + 1 for b in buildings): return
        if any(p[0] - 2 < x < p[2] + 2 and p[1] - 2 < z < p[3] + 2 for p in ponds): return
        if any(math.hypot(x - c[0], z - c[1]) < c[2] for c in plazas): return
        grid.setdefault(key(x, z), []).append((x, z))
        added.append(dict(id="T%05d" % (len(added) + 1), type="tree", variant=variant, x0=x, z0=z, x1=scale, z1=0, base=base,
                          floors=0, wall="", roof="", faces="", seed=rng.next_u32(), notes="added"))
    # Open land outside the street grid (waterfront lawns): scattered groves.
    W, D = L.bx * P, L.bz * P
    def in_grid(x, z):
        i, j = int(x // P), int(z // P)
        if x < 0 or z < 0 or x > W or z > D: return False
        corners = ((i * P, j * P), ((i + 1) * P, j * P), ((i + 1) * P, (j + 1) * P), (i * P, (j + 1) * P))
        return sum(1 for c in corners if L.on_land(*c)) >= 2
    xs = [p[0] for land in L.lands for p in land]; zs = [p[1] for land in L.lands for p in land]
    for _ in range(int((max(xs) - min(xs)) * (max(zs) - min(zs)) / 45)):
        x, z = rng.range(min(xs), max(xs)), rng.range(min(zs), max(zs))
        if in_grid(x, z) or not L.on_land(x, z): continue
        # stay off the promenade (within ~8 m of the shore)
        if any(inside(land, x, z) and min(math.hypot(x - p[0], z - p[1]) for p in land) < 9 for land in L.lands): continue
        add(x, z, rng.pick(["broad", "round", "round", "poplar", "bush", "bush"]), rng.range(.8, 1.1), LAND)
    for i in range(L.bx):
        for j in range(L.bz):
            x0, z0 = i * P + half, j * P + half; x1, z1 = x0 + bs, z0 + bs
            reserved = any(overlaps((x0, z0, x1, z1), q) for q in L.reserved)
            if reserved:                                                    # pocket parks and parks: fuller planting
                for _ in range(10):
                    x, z = rng.range(x0 + 2, x1 - 2), rng.range(z0 + 2, z1 - 2)
                    add(x, z, rng.pick(["broad", "round", "round", "poplar", "bush"]), rng.range(.8, 1.05), WALK - .05)
                continue
            for k in range(4):                                              # street trees every ~6 m
                for t in range(4):
                    if rng.chance(.65):
                        f = (t + .5) / 4
                        x, z = [(x0 + bs * f, z0 + .9), (x1 - .9, z0 + bs * f), (x1 - bs * f, z1 - .9), (x0 + .9, z1 - bs * f)][k]
                        add(x, z, "small", rng.range(1.0, 1.2), WALK)
    return rows + added

def recolor(rows):
    """Give every building the current palette (cool whites, muted roofs), from its own seed: hand edits to other
    columns survive; colours are replaced."""
    for r in rows:
        if r["type"] == "tree": continue
        c = Rng((r["seed"] ^ 0xC0FFEE) or 1)
        if r["type"] == "house": r["wall"] = to_hex(c.pick(HOUSE_WALLS)); r["roof"] = to_hex(c.pick(ROOFS))
        elif r["type"] == "tower": r["wall"] = to_hex(c.pick(TOWER_WALLS)); r["roof"] = to_hex(PAL["flatroof"])
        elif r["type"] == "warehouse": r["wall"] = to_hex(c.pick([hexc("#D3D3CF"), hexc("#C2C6C6"), hexc("#B9B1A4"), hexc("#CFCAC0")])); r["roof"] = to_hex(c.pick([PAL["slate"], hexc("#9AA1A4"), hexc("#B5B3AD")]))
        else: r["wall"] = to_hex(c.pick(WALLS)); r["roof"] = to_hex(c.pick([PAL["flatroof"], PAL["roofdark"], hexc("#C4C3BD")]))
    return rows

# ---------------- export ----------------
def write_bzb(path, chunk):
    """BZB2: positions, RGBA (A = gloss), then per vertex material id + aux (0..255; shore distance for water)."""
    with open(path, "wb") as f:
        f.write(b"BZB2"); f.write(struct.pack("<I", len(chunk) * 3))
        for t in chunk:
            for p in t[:3]: f.write(struct.pack("<fff", p[0], p[1], p[2]))
        for t in chunk:
            col = t[3]; f.write(bytes((col[0], col[1], col[2], col[3])) * 3)
        for t in chunk:
            m = mat_of(t[3]); aux = t[4] if len(t) > 4 else (0, 0, 0)
            f.write(bytes((m, aux[0], m, aux[1], m, aux[2])))

def export(root, L, B):
    out = os.path.join(root, OUT_DIR); os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith(".bzbmesh"): os.remove(os.path.join(out, f))
    tile = TILE_BLOCKS * L.P
    manifest = []
    order = list(GROUPS) + [g + "_detail" for g in GROUPS]
    for g in order:
        tris = B.groups.get(g, [])
        buckets = {}
        for t in tris:
            if g == "water": key = (0, 0)
            else:
                cx = (t[0][0] + t[1][0] + t[2][0]) / 3; cz = (t[0][2] + t[1][2] + t[2][2]) / 3
                key = (max(0, int(cx // tile)), max(0, int(cz // tile)))
            buckets.setdefault(key, []).append(t)
        for (tx, tz), items in sorted(buckets.items()):
            per = MAX_VERTS // 3
            for part in range((len(items) + per - 1) // per):
                chunk = items[part * per:(part + 1) * per]
                name = "%s_%d_%d%s" % (g, tx, tz, "" if part == 0 else "_%d" % part)
                write_bzb(os.path.join(out, name + ".bzbmesh"), chunk)
                manifest.append((name, g, 0 if g.replace("_detail", "") in NO_SHADOW else 1, len(chunk)))
    for g in sorted(k for k in B.groups if k not in order):                    # per-store meshes: one piece each, not tiled
        items = B.groups[g]; per = MAX_VERTS // 3
        for part in range((len(items) + per - 1) // per):
            chunk = items[part * per:(part + 1) * per]
            name = g if part == 0 else "%s_%d" % (g, part)
            write_bzb(os.path.join(out, name + ".bzbmesh"), chunk)
            manifest.append((name, g, 1, len(chunk)))
    with open(os.path.join(out, "city_shelves.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f); w.writerow(["store", "shelf", "cx", "cy", "cz", "sx", "sy", "sz"])
        for st, sh, c, sz in B.shelves: w.writerow([st, sh] + ["%.2f" % v for v in (*c, *sz)])
    with open(os.path.join(out, "city_manifest.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f); w.writerow(["mesh", "group", "shadows", "triangles"]); w.writerows(manifest)
    with open(os.path.join(out, "city_landmarks.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f); w.writerow(["id", "cx", "cy", "cz", "sx", "sy", "sz"])
        for lid, c, s in B.colliders: w.writerow([lid] + ["%.2f" % v for v in (*c, *s)])
    return manifest

# ---------------- preview renderer (painter's algorithm; good enough for a look) ----------------
def render(B, out, width=1672, height=940, yaw=-28.0, pitch=38.0, zoom=1.0, focus=None, detail=True, inside=None):
    """inside: a store id to show opened (its shell hidden, its interior shown); other stores show their shells."""
    import numpy as np
    from PIL import Image, ImageDraw
    base = list(GROUPS) + [g + "_detail" for g in GROUPS]
    extra = [g for g in B.groups if g not in base and (("_shell" in g and (inside is None or "_%s_" % inside not in g))
                                                         or ("_interior" in g and inside is not None and "_%s_" % inside in g))]
    names = [g for g in base + sorted(extra) if g in B.groups and (detail or not g.endswith("_detail"))]
    tris = [t for g in names for t in B.groups[g]]
    groups_of = [g for g in names for _ in B.groups[g]]
    A = np.array([[t[0], t[1], t[2]] for t in tris], dtype=np.float64)
    C = np.array([t[3][:3] for t in tris], dtype=np.float64); G = np.array([t[3][3] for t in tris], dtype=np.float64) / 255
    yw, pt = math.radians(yaw), math.radians(pitch)
    f = np.array([math.sin(yw) * math.cos(pt), -math.sin(pt), math.cos(yw) * math.cos(pt)])
    right = np.cross([0, 1, 0], f); right /= np.linalg.norm(right); up = np.cross(f, right)
    n = np.cross(A[:, 1] - A[:, 0], A[:, 2] - A[:, 0]); ln = np.linalg.norm(n, axis=1, keepdims=True); ln[ln == 0] = 1; n /= ln
    n[(n @ f) > 0] *= -1                                              # face the viewer (two-sided)
    sun = np.array([-.45, .8, -.38]); sun /= np.linalg.norm(sun)
    lam = np.clip(n @ sun, 0, 1); hemi = .5 + .5 * n[:, 1]
    h = sun - f; h /= np.linalg.norm(h); spec = np.clip(n @ h, 0, 1) ** 48 * G * 120
    col = np.clip(C * (.42 + .1 * hemi + .55 * lam)[:, None] + spec[:, None], 0, 255)
    fog_c = np.array([214, 224, 228.0])
    sx = A @ right; sy = A @ up; depth = (A @ f).mean(axis=1)
    if focus is None:
        landmask = np.array([g != "water" for g in groups_of])
        lx, ly = sx[landmask], sy[landmask]
        cxs, cys = (lx.min() + lx.max()) / 2, (ly.min() + ly.max()) / 2; span = max((lx.max() - lx.min()) / width, (ly.max() - ly.min()) / height)
    else:
        p = np.array(focus); cxs, cys = p @ right, p @ up; span = 1.0
    s = 1 / (span * 1.02) * zoom
    X = (sx - cxs) * s + width / 2; Y = height / 2 - (sy - cys) * s
    dn = (depth - depth.min()) / (np.ptp(depth) or 1)
    col = col * (1 - .16 * dn[:, None]) + fog_c * (.16 * dn[:, None])
    # Z-buffer rasteriser (per-triangle bounding box, barycentric test), so window panels sit correctly on walls.
    Z = A @ f                                                         # view depth per vertex (n,3)
    zbuf = np.full((height, width), np.inf); img = np.empty((height, width, 3)); img[:] = fog_c
    for i in np.where((X.max(axis=1) > 0) & (X.min(axis=1) < width) & (Y.max(axis=1) > 0) & (Y.min(axis=1) < height))[0]:
        x0, x1 = max(0, int(X[i].min())), min(width - 1, int(X[i].max()) + 1)
        y0, y1 = max(0, int(Y[i].min())), min(height - 1, int(Y[i].max()) + 1)
        if x1 < x0 or y1 < y0: continue
        (ax, bx_, cx_), (ay, by, cy) = X[i], Y[i]
        den = (by - cy) * (ax - cx_) + (cx_ - bx_) * (ay - cy)
        if abs(den) < 1e-9: continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
        w0 = ((by - cy) * (gx - cx_) + (cx_ - bx_) * (gy - cy)) / den
        w1 = ((cy - ay) * (gx - cx_) + (ax - cx_) * (gy - cy)) / den
        w2 = 1 - w0 - w1
        m = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
        if not m.any(): continue
        z = w0 * Z[i, 0] + w1 * Z[i, 1] + w2 * Z[i, 2]
        sub = zbuf[y0:y1 + 1, x0:x1 + 1]; m &= z < sub
        sub[m] = z[m]; img[y0:y1 + 1, x0:x1 + 1][m] = col[i]
    img = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
    img.save(out)

def stats(B):
    counts = {g: len(t) for g, t in B.groups.items()}
    return sum(counts.values()), counts

def main(argv):
    args = [a for a in argv if not a.startswith("--")]; flags = [a for a in argv if a.startswith("--")]
    cmd = args[0] if args else "build"; root = args[1] if len(args) > 1 else "."
    L = Layout(root); lots_path = os.path.join(root, LOTS)
    if cmd in ("add-trees", "recolor"):
        rows = read_lots(lots_path)
        before = len(rows)
        rows = add_trees(L, rows) if cmd == "add-trees" else recolor(rows)
        write_lots(lots_path, rows)
        print(cmd, ":", before, "->", len(rows), "rows in", LOTS); return 0
    if cmd == "plan" or not os.path.exists(lots_path):
        if os.path.exists(lots_path) and "--reroll" not in flags:
            print("Lots table exists (the city is frozen). Use --reroll to replace it."); return 1
        rows = plan(L); write_lots(lots_path, rows)
        print("planned", len(rows), "lots ->", LOTS)
        if cmd == "plan": return 0
    lots = read_lots(lots_path)
    B = build(L, lots)
    total, counts = stats(B)
    print("triangles", total, counts)
    if cmd == "preview":
        out = args[2] if len(args) > 2 else "city_preview.png"
        if "--close" in flags: render(B, out, zoom=3.2, focus=(L.U(7.5), 10, L.U(5.0)), yaw=-30, pitch=34)
        else: render(B, out)
        print("wrote", out); return 0
    manifest = export(root, L, B)
    print("wrote", len(manifest), "meshes to", OUT_DIR)
    return 0

if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
