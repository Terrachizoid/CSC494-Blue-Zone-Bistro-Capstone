"""Extract a store's interior layout from an image-to-3D mesh (e.g. a Tripo GLB of the 2D store screen).

The AI mesh is only a spatial reference: its geometry is fused, skewed and far too heavy for the game, but it knows
where things stand. This script flattens it into a top-down height map, finds the room's walls, and splits what
stands on the floor into fixtures: free-standing pieces (position, footprint, height, round or long, direction) and
runs of shelving against each wall. Output is a small JSON in room coordinates (u 0..1 west->east, v 0..1
front/south->back/north); cityinteriors.py builds procedural fixtures there.

    python Tools/City3D/layout_extract.py <mesh.glb> <out.json> [--preview out.png]

Only numpy and Pillow are needed (the GLB is read directly).
"""
import io, json, struct, sys
from collections import deque
import numpy as np
from PIL import Image, ImageDraw

def read_glb(path):
    b = open(path, "rb").read()
    n = struct.unpack_from("<I", b, 12)[0]; js = json.loads(b[20:20 + n]); off = 20 + n
    bl = struct.unpack_from("<I", b, off)[0]; bin_ = b[off + 8: off + 8 + bl]
    def acc(i):
        a = js["accessors"][i]; bv = js["bufferViews"][a["bufferView"]]
        comp = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}[a["componentType"]]
        nc = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
        arr = np.frombuffer(bin_, comp, a["count"] * nc, bv.get("byteOffset", 0) + a.get("byteOffset", 0))
        return arr.reshape(a["count"], nc) if nc > 1 else arr
    prim = js["meshes"][0]["primitives"][0]
    P = acc(prim["attributes"]["POSITION"]).astype(np.float64)
    I = acc(prim["indices"]).astype(np.int64).reshape(-1, 3)
    UV = acc(prim["attributes"]["TEXCOORD_0"]) if "TEXCOORD_0" in prim["attributes"] else None
    img = None
    mat = js["materials"][prim.get("material", 0)]
    t = mat.get("pbrMetallicRoughness", {}).get("baseColorTexture", {}).get("index")
    if t is not None:
        im = js["images"][js["textures"][t]["source"]]; bv = js["bufferViews"][im["bufferView"]]
        img = np.asarray(Image.open(io.BytesIO(bin_[bv.get("byteOffset", 0): bv.get("byteOffset", 0) + bv["byteLength"]])).convert("RGB"))
    return P, I, UV, img

def label(mask):
    N, M = mask.shape; lab = np.zeros(mask.shape, int); out = []; k = 0
    for i in range(N):
        for j in range(M):
            if mask[i, j] and lab[i, j] == 0:
                k += 1; q = deque([(i, j)]); lab[i, j] = k; cells = []
                while q:
                    a, b = q.popleft(); cells.append((a, b))
                    for da, db in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        x, y = a + da, b + db
                        if 0 <= x < N and 0 <= y < M and mask[x, y] and lab[x, y] == 0: lab[x, y] = k; q.append((x, y))
                out.append(np.array(cells))
    return out

def extract(path, N=300):
    P, I, UV, tex = read_glb(path)
    C = P[I].mean(1)
    pts = np.vstack([P, C])
    # Side-facing triangles carry the real colours (seen from above the texture is smeared).
    cols = None
    if tex is not None and UV is not None:
        uvc = UV[I].mean(1); th, tw = tex.shape[:2]
        cols = tex[((1 - uvc[:, 1] % 1) * (th - 1)).astype(int), (uvc[:, 0] % 1 * (tw - 1)).astype(int)]
        nrm = np.cross(P[I[:, 1]] - P[I[:, 0]], P[I[:, 2]] - P[I[:, 0]]); nrm /= np.linalg.norm(nrm, axis=1, keepdims=True) + 1e-12
        side = np.abs(nrm[:, 1]) < .5
    lo, hi = pts[:, [0, 2]].min(0), pts[:, [0, 2]].max(0)
    gx = ((pts[:, 0] - lo[0]) / (hi[0] - lo[0]) * (N - 1)).astype(int); gz = ((pts[:, 2] - lo[1]) / (hi[1] - lo[1]) * (N - 1)).astype(int)
    H = np.full((N, N), -1.0); order = np.argsort(pts[:, 1]); H[gz[order], gx[order]] = pts[order, 1]
    Hp = np.pad(H, 1, constant_values=-1); H = np.max([Hp[i:i + N, j:j + N] for i in range(3) for j in range(3)], axis=0)
    inside = H >= 0
    floor = np.percentile(H[inside], 40); top = np.percentile(H[inside], 97) - floor
    Hn = (H - floor) / top                                   # 0 floor .. 1 wall top
    # Walls: tall cells, kept only where they belong to the big outer ring.
    tall = Hn > .7
    ring = max(label(tall), key=len)
    wall = np.zeros_like(tall); wall[ring[:, 0], ring[:, 1]] = True
    # Room box: robust bounds of the wall ring (the AI skews the room; we straighten it into a rectangle).
    zi, xi = np.nonzero(inside)
    x0, x1 = np.percentile(xi, 3), np.percentile(xi, 97); z0, z1 = np.percentile(zi, 3), np.percentile(zi, 97)
    def uv(x, z): return float((x - x0) / (x1 - x0)), float(1 - (z - z0) / (z1 - z0))    # v = 0 at the front (high z)
    # Dilate walls a little so fixtures touching them separate.
    w = wall.copy()
    for _ in range(3):
        p = np.pad(w, 1); w = w | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    stand = (Hn > .05) & ~w & inside
    fixtures = []
    for c in label(stand):
        if len(c) < 30: continue
        zz, xx = c[:, 0], c[:, 1]
        span = max((xx.max() - xx.min()) / (x1 - x0), (zz.max() - zz.min()) / (z1 - z0))
        if span > .6 and len(c) / ((xx.max() - xx.min() + 1) * (zz.max() - zz.min() + 1)) < .25:
            wall[zz, xx] = True; continue                    # a low (cut-away) wall, not a fixture
        cov = np.cov(np.vstack([xx, zz])) + np.eye(2) * 1e-6; ev, evec = np.linalg.eigh(cov)
        u0, v0 = uv(xx.min(), zz.max()); u1, v1 = uv(xx.max(), zz.min())
        cu, cv = uv(xx.mean(), zz.mean())
        f = dict(u=cu, v=cv, u0=u0, u1=u1, v0=v0, v1=v1, h=float(np.percentile(Hn[zz, xx], 90)),
                 fill=float(len(c) / ((xx.max() - xx.min() + 1) * (zz.max() - zz.min() + 1))),
                 elong=float(np.sqrt(ev[1] / max(ev[0], 1e-6))),
                 axis="u" if abs(evec[0, 1]) > abs(evec[1, 1]) else "v",
                 cells=[uv(x, z) for z, x in c[::max(1, len(c) // 400)]])
        # touching which wall (within a few cells of the dilated wall)
        near = []
        for name, test in (("W", xx.min() - x0 < N * .06), ("E", x1 - xx.max() < N * .06), ("N", zz.min() - z0 < N * .06), ("S", z1 - zz.max() < N * .06)):
            if test: near.append(name)
        f["walls"] = near
        if cols is not None:
            sel = side & (gz[len(P):] >= zz.min()) & (gz[len(P):] <= zz.max()) & (gx[len(P):] >= xx.min()) & (gx[len(P):] <= xx.max()) & (C[:, 1] > floor + top * .05)
            if sel.sum() > 10: f["colour"] = "#%02X%02X%02X" % tuple(int(v) for v in np.median(cols[sel], axis=0))
        fixtures.append(f)
    fixtures.sort(key=lambda f: -(f["u1"] - f["u0"]) * (f["v1"] - f["v0"]))
    return dict(source=path.replace("\\", "/").split("/")[-1], aspect=float((x1 - x0) / (z1 - z0)), fixtures=fixtures), (H, Hn, wall, stand, (x0, x1, z0, z1))

def preview(result, maps, out):
    H, Hn, wall, stand, (x0, x1, z0, z1) = maps
    N = H.shape[0]
    img = np.zeros((N, N, 3), np.uint8); img[..., :] = (np.clip(Hn, 0, 1) * 160).astype(np.uint8)[..., None]
    img[wall] = (230, 230, 230); img[stand] = (90, 150, 110)
    im = Image.fromarray(img).resize((N * 2, N * 2), Image.NEAREST); d = ImageDraw.Draw(im)
    def px(u, v): return ((x0 + u * (x1 - x0)) * 2, (z1 - v * (z1 - z0)) * 2)
    d.rectangle([px(0, 1), px(1, 0)], outline=(255, 200, 0), width=2)
    for n, f in enumerate(result["fixtures"]):
        a, b = px(f["u0"], f["v1"]), px(f["u1"], f["v0"]); d.rectangle([a, b], outline=(255, 60, 60), width=2)
        d.text((a[0] + 3, a[1] + 3), "%d %s" % (n, "".join(f["walls"])), fill=(255, 255, 0))
    im.save(out)

if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    res, maps = extract(args[0])
    json.dump(res, open(args[1], "w"), indent=1)
    if "--preview" in sys.argv: preview(res, maps, sys.argv[sys.argv.index("--preview") + 1])
    for n, f in enumerate(res["fixtures"]):
        print(n, {k: (round(v, 2) if isinstance(v, float) else v) for k, v in f.items() if k != "cells"})
