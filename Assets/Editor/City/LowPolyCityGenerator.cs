// File responsibility: Builds the low-poly 3D city from Assets/GameData/City/Blue_Zone_Bistro_CityLayout.csv.
// Menu: Tools > Blue Zone Bistro > Generate Low-Poly City. Same seed + same layout = same city, here and in the
// Python preview (Tools/City3D/citygen.py), which mirrors this file call for call (shared xorshift32 random numbers).
// Output: faceted vertex-coloured meshes saved under Assets/GameData/City/Generated (reused on regenerate, so links
// survive), one material, and the prefab Assets/GameData/City/LowPolyCity.prefab with clickable CityLandmark colliders.
// Everything is triangles (trees are jittered icosahedra, no smooth primitives). Hand edits belong in a copy or Blender.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class LowPolyCityGenerator
{
    public const string LayoutPath = "Assets/GameData/City/Blue_Zone_Bistro_CityLayout.csv";
    private const string OutputFolder = "Assets/GameData/City/Generated";
    private const string PrefabPath = "Assets/GameData/City/LowPolyCity.prefab";
    private const string MaterialPath = "Assets/GameData/City/LowPolyCity.mat";
    private const int MaxVerticesPerMesh = 60000; // 16-bit indices: safe on WebGL and mobile

    [MenuItem("Tools/Blue Zone Bistro/Generate Low-Poly City")]
    public static void GenerateMenu()
    {
        var prefab = Generate();
        if (prefab == null) return;
        var existing = GameObject.Find("Low-Poly City");
        if (existing == null) existing = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Selection.activeGameObject = existing;
        Debug.Log("Low-poly city generated: " + PrefabPath);
    }

    // ---------------- random numbers (identical in Python) ----------------
    private sealed class Rng
    {
        private uint s;
        public Rng(long seed) { s = (uint)(seed & 0xFFFFFFFF); if (s == 0) s = 0x9E3779B9; }
        public uint NextU32() { uint x = s; x ^= x << 13; x ^= x >> 17; x ^= x << 5; s = x; return s; }
        public double Value() => (NextU32() >> 8) / 16777216.0;
        public double Range(double a, double b) => a + (b - a) * Value();
        public T Pick<T>(T[] items) => items[(int)(Value() * items.Length) % items.Length];
    }

    // ---------------- palette ----------------
    private static Color32 Hex(string h) => new Color32(Convert.ToByte(h.Substring(1, 2), 16), Convert.ToByte(h.Substring(3, 2), 16), Convert.ToByte(h.Substring(5, 2), 16), 255);
    private static readonly Color32 Water = Hex("#6F93A8"), Pavement = Hex("#D9D2C3"), Sidewalk = Hex("#E6E0D4"), Road = Hex("#BDB8AE"),
        Sand = Hex("#E8D8B0"), Grass = Hex("#9DB878"), Plaza = Hex("#E2DACB"), Trunk = Hex("#8A6A4F"), Rock = Hex("#9C9A94"),
        FlatRoof = Hex("#CFC8BB"), Slate = Hex("#8F9AA3"), Terracotta = Hex("#B9876A"), Glass = Hex("#5F9E9A"), Tower = Hex("#E4DED2"),
        TowerDeck = Hex("#5C8F9A"), Orange = Hex("#E08A3C"), Blue = Hex("#3E78B5"), Awning = Hex("#3F78A8"), Parking = Hex("#C9C5BD"),
        Deck = Hex("#D8D3C8"), Hull = Hex("#F2F0EA"), Sail = Hex("#FBFAF5"), Pond = Hex("#7FA9B8"), BistroWall = Hex("#F4ECDC"), BlueRoof = Hex("#A9BFCF");
    private static readonly Color32[] Walls = { Hex("#EFE6D6"), Hex("#E3D5BF"), Hex("#D8D8D4"), Hex("#F3EFE6"), Hex("#E9DCC4"), Hex("#DCCFB9") };
    private static readonly Color32[] Leaves = { Hex("#7FA35B"), Hex("#6E9450"), Hex("#8DB36A"), Hex("#78A060") };
    private static readonly Color32[] HouseRoofs = { Slate, Terracotta };

    // ---------------- geometry builder ----------------
    private sealed class Builder
    {
        public readonly Dictionary<string, List<Vector3>> Vertices = new Dictionary<string, List<Vector3>>();
        public readonly Dictionary<string, List<Color32>> Colors = new Dictionary<string, List<Color32>>();
        public readonly List<(string id, Vector3 centre, Vector3 size)> Colliders = new List<(string, Vector3, Vector3)>();
        public void Tri(string g, Vector3 a, Vector3 b, Vector3 c, Color32 col)
        {
            if (!Vertices.TryGetValue(g, out var v)) { v = new List<Vector3>(); Vertices[g] = v; Colors[g] = new List<Color32>(); }
            v.Add(a); v.Add(b); v.Add(c); var cl = Colors[g]; cl.Add(col); cl.Add(col); cl.Add(col);
        }
        public void Quad(string g, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 col) { Tri(g, a, b, c, col); Tri(g, a, c, d, col); }
        public void Box(string g, double x0, double z0, double x1, double z1, double y0, double y1, Color32 wall, Color32? top = null)
        {
            var t = top ?? wall;
            Quad(g, V(x0, y0, z0), V(x0, y1, z0), V(x1, y1, z0), V(x1, y0, z0), wall);
            Quad(g, V(x1, y0, z1), V(x1, y1, z1), V(x0, y1, z1), V(x0, y0, z1), wall);
            Quad(g, V(x0, y0, z1), V(x0, y1, z1), V(x0, y1, z0), V(x0, y0, z0), wall);
            Quad(g, V(x1, y0, z0), V(x1, y1, z0), V(x1, y1, z1), V(x1, y0, z1), wall);
            Quad(g, V(x0, y1, z0), V(x0, y1, z1), V(x1, y1, z1), V(x1, y1, z0), t);
        }
        public void Prism(string g, double cx, double cz, double r0, double r1, int n, double y0, double y1, Color32 side, Color32? top, double phase = 0)
        {
            for (int k = 0; k < n; k++)
            {
                double a = phase + 2 * Math.PI * k / n, b = phase + 2 * Math.PI * (k + 1) / n;
                Quad(g, V(cx + Math.Cos(a) * r0, y0, cz + Math.Sin(a) * r0), V(cx + Math.Cos(a) * r1, y1, cz + Math.Sin(a) * r1),
                     V(cx + Math.Cos(b) * r1, y1, cz + Math.Sin(b) * r1), V(cx + Math.Cos(b) * r0, y0, cz + Math.Sin(b) * r0), side);
            }
            if (top == null) return;
            for (int k = 0; k < n; k++)
            {
                double a = phase + 2 * Math.PI * k / n, b = phase + 2 * Math.PI * (k + 1) / n;
                Tri(g, V(cx, y1, cz), V(cx + Math.Cos(b) * r1, y1, cz + Math.Sin(b) * r1), V(cx + Math.Cos(a) * r1, y1, cz + Math.Sin(a) * r1), top.Value);
            }
        }
        public void Pyramid(string g, double cx, double cz, double r, int n, double y0, double y1, Color32 col, double phase = 0)
        {
            for (int k = 0; k < n; k++)
            {
                double a = phase + 2 * Math.PI * k / n, b = phase + 2 * Math.PI * (k + 1) / n;
                Tri(g, V(cx + Math.Cos(a) * r, y0, cz + Math.Sin(a) * r), V(cx, y1, cz), V(cx + Math.Cos(b) * r, y0, cz + Math.Sin(b) * r), col);
            }
        }
        public void Gable(string g, double x0, double z0, double x1, double z1, double y0, double h, Color32 roof, Color32 gableWall)
        {
            if (x1 - x0 >= z1 - z0)
            {
                double zm = (z0 + z1) / 2;
                Quad(g, V(x0, y0, z0), V(x0, y0 + h, zm), V(x1, y0 + h, zm), V(x1, y0, z0), roof);
                Quad(g, V(x1, y0, z1), V(x1, y0 + h, zm), V(x0, y0 + h, zm), V(x0, y0, z1), roof);
                Tri(g, V(x0, y0, z1), V(x0, y0 + h, zm), V(x0, y0, z0), gableWall);
                Tri(g, V(x1, y0, z0), V(x1, y0 + h, zm), V(x1, y0, z1), gableWall);
            }
            else
            {
                double xm = (x0 + x1) / 2;
                Quad(g, V(x0, y0, z1), V(xm, y0 + h, z1), V(xm, y0 + h, z0), V(x0, y0, z0), roof);
                Quad(g, V(x1, y0, z0), V(xm, y0 + h, z0), V(xm, y0 + h, z1), V(x1, y0, z1), roof);
                Tri(g, V(x0, y0, z0), V(xm, y0 + h, z0), V(x1, y0, z0), gableWall);
                Tri(g, V(x1, y0, z1), V(xm, y0 + h, z1), V(x0, y0, z1), gableWall);
            }
        }
        public void Polygon(string g, List<Vector2d> pts, double y, Color32 col)
        {
            foreach (var (a, b, c) in EarClip(pts)) Tri(g, V(a.x, y, a.z), V(b.x, y, b.z), V(c.x, y, c.z), col);
        }
        // Faceted blob: icosahedron with jittered vertices (20 triangles). Tree canopies and rocks.
        public void Blob(string g, Rng rng, double cx, double cy, double cz, double rx, double ry, double rz, Color32 col, double jitter = 0.18)
        {
            var verts = new Vector3[IcoV.Length];
            for (int i = 0; i < IcoV.Length; i++)
            {
                var v = IcoV[i];
                double px = v.x * rx * (1 + rng.Range(-jitter, jitter)), py = v.y * ry * (1 + rng.Range(-jitter, jitter)), pz = v.z * rz * (1 + rng.Range(-jitter, jitter));
                verts[i] = V(px + cx, py + cy, pz + cz);
            }
            foreach (var f in IcoF) Tri(g, verts[f[0]], verts[f[1]], verts[f[2]], col);
        }
        public void Dome(string g, double cx, double cy, double cz, double r, Color32 col)
        {
            foreach (var t in DomeF) Tri(g, V(cx + t[0].x * r, cy + t[0].y * r, cz + t[0].z * r), V(cx + t[1].x * r, cy + t[1].y * r, cz + t[1].z * r), V(cx + t[2].x * r, cy + t[2].y * r, cz + t[2].z * r), col);
        }
    }
    private static Vector3 V(double x, double y, double z) => new Vector3((float)x, (float)y, (float)z);
    public struct Vector2d { public double x, z; public Vector2d(double x, double z) { this.x = x; this.z = z; } }
    private struct V3d { public double x, y, z; public V3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; } }

    private static readonly V3d[] IcoV = BuildIco(out IcoF);
    private static int[][] IcoF;
    private static V3d[] BuildIco(out int[][] faces)
    {
        double t = (1 + Math.Sqrt(5)) / 2, n = Math.Sqrt(1 + t * t);
        var raw = new double[,] { { -1, t, 0 }, { 1, t, 0 }, { -1, -t, 0 }, { 1, -t, 0 }, { 0, -1, t }, { 0, 1, t }, { 0, -1, -t }, { 0, 1, -t }, { t, 0, -1 }, { t, 0, 1 }, { -t, 0, -1 }, { -t, 0, 1 } };
        var v = new V3d[12];
        for (int i = 0; i < 12; i++) v[i] = new V3d(raw[i, 0] / n, raw[i, 1] / n, raw[i, 2] / n);
        faces = new[] { new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11}, new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
            new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9}, new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1} };
        return v;
    }
    private static readonly List<V3d[]> DomeF = BuildDome();
    private static List<V3d[]> BuildDome()
    {
        var output = new List<V3d[]>();
        V3d Mid(V3d a, V3d b) { double x = (a.x + b.x) / 2, y = (a.y + b.y) / 2, z = (a.z + b.z) / 2, l = Math.Sqrt(x * x + y * y + z * z); return new V3d(x / l, y / l, z / l); }
        foreach (var f in IcoF)
        {
            V3d a = IcoV[f[0]], b = IcoV[f[1]], c = IcoV[f[2]], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
            foreach (var tri in new[] { new[] { a, ab, ca }, new[] { ab, b, bc }, new[] { ca, bc, c }, new[] { ab, bc, ca } })
            {
                if ((tri[0].y + tri[1].y + tri[2].y) / 3 <= -0.05) continue;
                output.Add(tri.Select(p => new V3d(p.x, Math.Max(0, p.y), p.z)).ToArray());
            }
        }
        return output;
    }

    // ---------------- 2D helpers ----------------
    private static double Area2(List<Vector2d> p) { double s = 0; for (int i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; s += a.x * b.z - b.x * a.z; } return s; }
    private static bool Inside(List<Vector2d> p, double x, double z)
    {
        bool c = false;
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count];
            if ((a.z > z) != (b.z > z) && x < (b.x - a.x) * (z - a.z) / (b.z - a.z) + a.x) c = !c;
        }
        return c;
    }
    private static List<(Vector2d, Vector2d, Vector2d)> EarClip(List<Vector2d> pts)
    {
        var p = new List<Vector2d>(pts);
        if (Area2(p) < 0) p.Reverse();
        var tris = new List<(Vector2d, Vector2d, Vector2d)>();
        int guard = 0;
        while (p.Count > 3 && guard++ < 10000)
        {
            int n = p.Count; bool clipped = false;
            for (int i = 0; i < n; i++)
            {
                var a = p[(i - 1 + n) % n]; var b = p[i]; var c = p[(i + 1) % n];
                if ((b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x) <= 0) continue;
                bool blocked = false;
                for (int q = 0; q < n && !blocked; q++)
                {
                    if (q == i || q == (i - 1 + n) % n || q == (i + 1) % n) continue;
                    if (InTri(p[q], a, b, c)) blocked = true;
                }
                if (blocked) continue;
                tris.Add((a, b, c)); p.RemoveAt(i); clipped = true; break;
            }
            if (!clipped) break;
        }
        if (p.Count == 3) tris.Add((p[0], p[1], p[2]));
        return tris;
    }
    private static bool InTri(Vector2d q, Vector2d a, Vector2d b, Vector2d c)
    {
        double d1 = (q.x - b.x) * (a.z - b.z) - (a.x - b.x) * (q.z - b.z);
        double d2 = (q.x - c.x) * (b.z - c.z) - (b.x - c.x) * (q.z - c.z);
        double d3 = (q.x - a.x) * (c.z - a.z) - (c.x - a.x) * (q.z - a.z);
        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
    }

    // ---------------- layout ----------------
    private sealed class Row { public string kind, id, style; public double x, z, w, d; }
    private static double Num(string s) => string.IsNullOrWhiteSpace(s) ? 0 : double.Parse(s, CultureInfo.InvariantCulture);

    private static Builder Build(out string error)
    {
        error = null;
        if (!CsvImportUtility.TryRead(LayoutPath, "City layout", new[] { "kind", "id", "x", "z", "w", "d", "style" }, out var raw, out var columns))
        { error = "Could not read " + LayoutPath; return null; }
        var rows = raw.Skip(1).Where(r => !CsvImportUtility.IsBlankRow(r)).Select(r => new Row {
            kind = CsvImportUtility.Get(r, columns, "kind").Trim(), id = CsvImportUtility.Get(r, columns, "id").Trim(),
            style = CsvImportUtility.Get(r, columns, "style").Trim(),
            x = Num(CsvImportUtility.Get(r, columns, "x")), z = Num(CsvImportUtility.Get(r, columns, "z")),
            w = Num(CsvImportUtility.Get(r, columns, "w")), d = Num(CsvImportUtility.Get(r, columns, "d")) }).ToList();
        var settings = rows.Where(r => r.kind == "setting").ToDictionary(r => r.id, r => r.x);
        List<Row> Pick(string kind) => rows.Where(r => r.kind == kind).ToList();

        var rng = new Rng((long)settings["seed"]); var B = new Builder();
        int bx = (int)settings["blocks_x"], bz = (int)settings["blocks_z"]; double bs = settings["block_size"], rw = settings["road_width"];
        double P = bs + rw;
        double U(double v) => v * P;
        var coast = Pick("coast").Select(r => new Vector2d(U(r.x), U(r.z))).ToList();
        var island = Pick("island").Select(r => new Vector2d(U(r.x), U(r.z))).ToList();
        var lands = new List<List<Vector2d>> { coast }; if (island.Count >= 3) lands.Add(island);
        bool OnLand(double x, double z) => lands.Any(L => Inside(L, x, z));
        const double GROUND = 0.0, LAND = 0.6, ROAD = 0.66, WALK = 0.8;
        double m = 3 * P;
        B.Quad("water", V(-m, GROUND, -m), V(-m, GROUND, bz * P + m), V(bx * P + m, GROUND, bz * P + m), V(bx * P + m, GROUND, -m), Water);
        foreach (var L in lands)
        {
            B.Polygon("ground", L, LAND, Pavement);
            bool ccw = Area2(L) > 0;
            for (int i = 0; i < L.Count; i++)
            {
                var p1 = L[i]; var p2 = L[(i + 1) % L.Count];
                double x1 = p1.x, z1 = p1.z, dx = p2.x - x1, dz = p2.z - z1, ln = Math.Sqrt(dx * dx + dz * dz); if (ln == 0) ln = 1;
                double nx = ccw ? dz / ln : -dz / ln, nz = ccw ? -dx / ln : dx / ln, w = 6.0;
                B.Quad("ground", V(x1, 0.35, z1), V(x1 + nx * w, 0.05, z1 + nz * w), V(p2.x + nx * w, 0.05, p2.z + nz * w), V(p2.x, 0.35, p2.z), Sand);
                int promenade = (int)Math.Floor(ln / 11);
                for (int k = 0; k < promenade; k++)
                {
                    if (rng.Value() < .55)
                    {
                        double t = (k + .5) / Math.Max(1, promenade);
                        double px = x1 + dx * t - nx * 5.5, pz = z1 + dz * t - nz * 5.5;
                        if (OnLand(px, pz)) Tree(B, rng, px, LAND, pz, .9);
                    }
                }
                int steps = (int)Math.Floor(ln / 9);
                for (int k = 0; k < steps; k++)
                {
                    if (rng.Value() < 0.45)
                    {
                        double t = (k + rng.Value()) / Math.Max(1, steps);
                        double px = x1 + dx * t + nx * rng.Range(2, 6), pz = z1 + dz * t + nz * rng.Range(2, 6);
                        double r = rng.Range(0.8, 1.8);
                        B.Blob("ground", rng, px, 0.3, pz, r, r * 0.6, r, Rock, 0.25);
                    }
                }
            }
        }
        (double x0, double z0, double x1, double z1) Rect(Row r) => (U(r.x), U(r.z), U(r.x) + U(r.w), U(r.z) + U(r.d));
        var reserved = new[] { "landmark", "park", "pond" }.SelectMany(k => Pick(k)).Select(Rect).ToList();
        bool Overlaps((double x0, double z0, double x1, double z1) a, (double x0, double z0, double x1, double z1) b) => a.x0 < b.x1 && b.x0 < a.x1 && a.z0 < b.z1 && b.z0 < a.z1;
        double half = rw / 2;
        for (int i = 0; i <= bx; i++)
            for (int j = 0; j <= bz; j++)
            {
                double x = i * P, z = j * P;
                if (OnLand(x, z)) B.Quad("roads", V(x - half, ROAD, z - half), V(x - half, ROAD, z + half), V(x + half, ROAD, z + half), V(x + half, ROAD, z - half), Road);
                if (i < bx && OnLand(x, z) && OnLand(x + P, z) && OnLand(x + P / 2, z))
                    B.Quad("roads", V(x + half, ROAD, z - half), V(x + half, ROAD, z + half), V(x + P - half, ROAD, z + half), V(x + P - half, ROAD, z - half), Road);
                if (j < bz && OnLand(x, z) && OnLand(x, z + P) && OnLand(x, z + P / 2))
                    B.Quad("roads", V(x - half, ROAD, z + half), V(x - half, ROAD, z + P - half), V(x + half, ROAD, z + P - half), V(x + half, ROAD, z + half), Road);
            }
        double cx0 = U(7.5), cz0 = U(6.5), maxD = Math.Sqrt(bx * P * bx * P + bz * P * bz * P) / 2;
        var quarters = new[] { (0.0, 0.0, .5, .5), (.5, 0.0, 1.0, .5), (0.0, .5, .5, 1.0), (.5, .5, 1.0, 1.0) };
        for (int i = 0; i < bx; i++)
            for (int j = 0; j < bz; j++)
            {
                double x0 = i * P + half, z0 = j * P + half, x1 = x0 + bs, z1 = z0 + bs;
                if (reserved.Any(r => Overlaps((x0, z0, x1, z1), r))) continue;
                if (!(OnLand(x0 - 2, z0 - 2) && OnLand(x1 + 2, z0 - 2) && OnLand(x1 + 2, z1 + 2) && OnLand(x0 - 2, z1 + 2)))
                {
                    foreach (var (u0, v0, u1, v1) in quarters)
                    {
                        double lx0 = x0 + bs * u0 + 2.2, lz0 = z0 + bs * v0 + 2.2, lx1 = x0 + bs * u1 - 2.2, lz1 = z0 + bs * v1 - 2.2;
                        if (!(OnLand(lx0 - 3, lz0 - 3) && OnLand(lx1 + 3, lz0 - 3) && OnLand(lx1 + 3, lz1 + 3) && OnLand(lx0 - 3, lz1 + 3))) continue;
                        var wall = rng.Pick(Walls); double h = 3.2 * rng.Pick(new[] { 1, 2 });
                        B.Box("buildings", lx0, lz0, lx1, lz1, LAND, LAND + h, wall);
                        B.Gable("buildings", lx0 - .4, lz0 - .4, lx1 + .4, lz1 + .4, LAND + h, rng.Range(2.4, 3.4), rng.Pick(HouseRoofs), wall);
                    }
                    continue;
                }
                B.Box("ground", x0, z0, x1, z1, LAND, WALK, Sidewalk);
                double dist = Math.Sqrt(Math.Pow((x0 + x1) / 2 - cx0, 2) + Math.Pow((z0 + z1) / 2 - cz0, 2)) / maxD;
                bool residential = j >= bz - 2 || dist > 0.78;
                int lots = residential ? 4 : rng.Pick(new[] { 1, 2, 2, 4 });
                (double, double, double, double)[] cells;
                if (lots == 1) cells = new[] { (0.0, 0.0, 1.0, 1.0) };
                else if (lots == 2 && rng.Value() < .5) cells = new[] { (0.0, 0.0, .5, 1.0), (.5, 0.0, 1.0, 1.0) };
                else if (lots == 2) cells = new[] { (0.0, 0.0, 1.0, .5), (0.0, .5, 1.0, 1.0) };
                else cells = quarters;
                foreach (var (u0, v0, u1, v1) in cells)
                {
                    double inset = residential ? 2.2 : 1.6;
                    double lx0 = x0 + bs * u0 + inset, lz0 = z0 + bs * v0 + inset, lx1 = x0 + bs * u1 - inset, lz1 = z0 + bs * v1 - inset;
                    var wall = rng.Pick(Walls);
                    if (residential)
                    {
                        double h = 3.2 * rng.Pick(new[] { 1, 2, 2 });
                        B.Box("buildings", lx0, lz0, lx1, lz1, WALK, WALK + h, wall);
                        B.Gable("buildings", lx0 - .4, lz0 - .4, lx1 + .4, lz1 + .4, WALK + h, rng.Range(2.4, 3.4), rng.Pick(HouseRoofs), wall);
                    }
                    else
                    {
                        int floors = 2 + (int)(rng.Value() * 9 * Math.Pow(1 - dist, 1.5));
                        double h = 3.2 * floors;
                        B.Box("buildings", lx0, lz0, lx1, lz1, WALK, WALK + h, wall, FlatRoof);
                        if (floors >= 5 && rng.Value() < .5)
                        {
                            double sx = (lx1 - lx0) * .18, sz = (lz1 - lz0) * .18;
                            B.Box("buildings", lx0 + sx, lz0 + sz, lx1 - sx, lz1 - sz, WALK + h, WALK + h + 3.2 * rng.Pick(new[] { 1, 2 }), wall, FlatRoof);
                        }
                        if (rng.Value() < .6)
                        {
                            double ux = rng.Range(lx0 + 1, lx1 - 3), uz = rng.Range(lz0 + 1, lz1 - 3);
                            B.Box("buildings", ux, uz, ux + 2, uz + 2, WALK + h, WALK + h + 1.2, Slate);
                        }
                    }
                }
                for (int k = 0; k < 4; k++)
                    for (int t = 0; t < 3; t++)
                    {
                        if (rng.Value() < .55)
                        {
                            double f = (t + .5) / 3;
                            double px = k == 0 ? x0 + bs * f : k == 1 ? x1 - .9 : k == 2 ? x1 - bs * f : x0 + .9;
                            double pz = k == 0 ? z0 + .9 : k == 1 ? z0 + bs * f : k == 2 ? z1 - .9 : z1 - bs * f;
                            Tree(B, rng, px, WALK, pz, 1.25);
                        }
                    }
            }
        var ponds = Pick("pond");
        foreach (var r in Pick("park"))
        {
            var (x0, z0, x1, z1) = Rect(r);
            bool plaza = r.style == "plaza";
            B.Box("ground", x0 + half, z0 + half, x1 - half, z1 - half, LAND, WALK - .05, plaza ? Plaza : Grass);
            int n = (int)((x1 - x0) * (z1 - z0) / (plaza ? 320 : 110));
            for (int q = 0; q < n; q++)
            {
                double px = rng.Range(x0 + half + 2, x1 - half - 2), pz = rng.Range(z0 + half + 2, z1 - half - 2);
                if (ponds.Any(p => Overlaps((px - 3, pz - 3, px + 3, pz + 3), Rect(p)))) continue;
                if (plaza && Math.Sqrt(Math.Pow(px - (x0 + x1) / 2, 2) + Math.Pow(pz - (z0 + z1) / 2, 2)) < (x1 - x0) * .22) continue;
                Tree(B, rng, px, WALK - .05, pz, 1.0);
            }
        }
        foreach (var r in ponds)
        {
            var (x0, z0, x1, z1) = Rect(r);
            double cx = (x0 + x1) / 2, cz = (z0 + z1) / 2, rx = (x1 - x0) / 2, rz = (z1 - z0) / 2;
            var pts = new List<Vector2d>();
            for (int k = 0; k < 10; k++)
            {
                double a = 2 * Math.PI * k / 10;
                double px = cx + Math.Cos(a) * rx * rng.Range(.85, 1);
                double pz = cz + Math.Sin(a) * rz * rng.Range(.85, 1);
                pts.Add(new Vector2d(px, pz));
            }
            B.Polygon("water", pts, WALK - .02, Pond);
        }
        foreach (var r in Pick("landmark")) Landmark(B, rng, r, Rect(r), LAND, WALK);
        foreach (var r in Pick("bridge"))
        {
            var (x0, z0, x1, z1) = Rect(r);
            B.Box("landmarks", x0 - 2, z0, x1 + 2, z1, 3.0, 3.8, Deck);
            for (int k = 0; k < 4; k++) B.Prism("landmarks", x0 + (x1 - x0) * (k + .5) / 4, (z0 + z1) / 2, 1.3, 1.1, 6, 0, 3.0, Deck, null);
            B.Box("landmarks", x0 - 2, z0 - .2, x1 + 2, z0 + .4, 3.8, 4.8, Tower);
            B.Box("landmarks", x0 - 2, z1 - .4, x1 + 2, z1 + .2, 3.8, 4.8, Tower);
        }
        foreach (var r in Pick("pier"))
        {
            var (x0, z0, x1, z1) = Rect(r);
            B.Box("landmarks", x0, z0 - U(r.d) * .2, x1, z1 - U(r.d), .5, 1.0, Deck);
            for (int k = 0; k < 2; k++) Boat(B, rng, k == 0 ? x1 + 5 : x0 - 5, z0 - U(r.d) * (.3 + .4 * k));
        }
        return B;
    }

    private static void Tree(Builder B, Rng rng, double x, double y, double z, double scale)
    {
        double h = rng.Range(1.4, 2.0) * scale;
        B.Prism("trees", x, z, .28 * scale, .22 * scale, 6, y, y + h, Trunk, null);
        double r = rng.Range(2.2, 3.0) * scale;
        B.Blob("trees", rng, x, y + h + r * .55, z, r, r * .8, r, rng.Pick(Leaves));
    }

    private static void Boat(Builder B, Rng rng, double x, double z)
    {
        double L = rng.Range(6, 9), W = rng.Range(2.2, 3.0);
        var pts = new List<Vector2d> { new Vector2d(x - L / 2, z), new Vector2d(x - L / 4, z - W / 2), new Vector2d(x + L / 3, z - W / 2),
            new Vector2d(x + L / 2, z), new Vector2d(x + L / 3, z + W / 2), new Vector2d(x - L / 4, z + W / 2) };
        for (int k = 0; k < pts.Count; k++)
        {
            var a = pts[k]; var b = pts[(k + 1) % pts.Count];
            B.Quad("landmarks", V(a.x, .2, a.z), V(a.x, 1.1, a.z), V(b.x, 1.1, b.z), V(b.x, .2, b.z), Hull);
        }
        B.Polygon("landmarks", pts, 1.1, Deck);
        B.Box("landmarks", x - .15, z - .15, x + .15, z + .15, 1.1, 1.1 + L * .9, Hull);
        B.Tri("landmarks", V(x + .2, 1.6, z), V(x + .2, 1.1 + L * .85, z), V(x + L * .4, 1.6, z), Sail);
    }

    private static void Landmark(Builder B, Rng rng, Row r, (double x0, double z0, double x1, double z1) rc, double LAND, double WALK)
    {
        const double half = 4.0;
        double x0 = rc.x0 + half, z0 = rc.z0 + half, x1 = rc.x1 - half, z1 = rc.z1 - half;
        double cx = (x0 + x1) / 2, cz = (z0 + z1) / 2, top = WALK;
        B.Box("ground", x0, z0, x1, z1, LAND, WALK, Plaza);
        if (r.style == "dome")
        {
            double bw = Math.Min(x1 - x0, z1 - z0) * .26;
            B.Prism("landmarks", cx, cz, bw * 1.1, bw * 1.1, 8, WALK, WALK + 7, Tower, FlatRoof, Math.PI / 8);
            B.Dome("landmarks", cx, WALK + 7, cz, bw, Glass);
            B.Box("landmarks", cx - 4, z0 + 2, cx + 4, cz - bw * .9, WALK, WALK + 4, Tower, Glass);
            top = WALK + 7 + bw;
        }
        else if (r.style.StartsWith("bigbox", StringComparison.Ordinal))
        {
            bool orange = r.style.EndsWith("orange", StringComparison.Ordinal);
            var band = orange ? Orange : Blue; var roof = orange ? FlatRoof : BlueRoof;
            double bx0 = x0 + 2, bz0 = z0 + (z1 - z0) * .32, bx1 = x1 - 2, bz1 = z1 - 2;
            B.Box("landmarks", bx0, bz0, bx1, bz1, WALK, WALK + 9, Tower, roof);
            B.Box("landmarks", bx0 - .3, bz0 - .4, bx1 + .3, bz0 + .3, WALK + 6.5, WALK + 9.6, band);
            B.Box("ground", x0 + 2, z0 + 2, x1 - 2, bz0 - 1.5, WALK, WALK + .02, Parking);
            int trees = (int)Math.Floor((x1 - x0) / 9);
            for (int k = 0; k < trees; k++) Tree(B, rng, x0 + 4 + k * 9, WALK, z0 + 2.5, .7);
            top = WALK + 9.6;
        }
        else if (r.style == "tower")
        {
            double y = WALK;
            foreach (var (ra, rb, h) in new[] { (4.5, 4.5, 2.5), (2.4, 1.9, 30.0), (1.9, 1.9, 4.0) })
            { B.Prism("landmarks", cx, cz, ra, rb, 8, y, y + h, Tower, Tower, Math.PI / 8); y += h; }
            B.Prism("landmarks", cx, cz, 5.5, 5.5, 12, y, y + 3.2, TowerDeck, Tower); y += 3.2;
            B.Prism("landmarks", cx, cz, 2.4, 2.0, 8, y, y + 3, Tower, null, Math.PI / 8); y += 3;
            B.Pyramid("landmarks", cx, cz, 2.6, 8, y, y + 6, TowerDeck, Math.PI / 8); y += 6;
            top = y;
        }
        else if (r.style == "bistro")
        {
            double bx0 = cx - 11, bz0 = cz - 2, bx1 = cx + 11, bz1 = cz + 9;
            B.Box("landmarks", bx0, bz0, bx1, bz1, WALK, WALK + 7, BistroWall);
            B.Gable("landmarks", bx0 - .8, bz0 - .8, bx1 + .8, bz1 + .8, WALK + 7, 4.0, Awning, BistroWall);
            B.Quad("landmarks", V(bx0, WALK + 4.2, bz0), V(bx1, WALK + 4.2, bz0), V(bx1, WALK + 3.0, bz0 - 4.5), V(bx0, WALK + 3.0, bz0 - 4.5), Awning);
            for (int k = 0; k < 5; k++)
            {
                double tx = bx0 + 2.5 + k * 4.2;
                B.Prism("landmarks", tx, bz0 - 7.5, .12, .12, 6, WALK, WALK + 2.2, Trunk, null);
                B.Pyramid("landmarks", tx, bz0 - 7.5, 1.8, 6, WALK + 2.0, WALK + 2.9, Sail);
            }
            top = WALK + 11;
        }
        B.Colliders.Add((r.id, V(cx, (WALK + top) / 2, cz), V(x1 - x0, top - WALK, z1 - z0)));
    }

    // ---------------- assets ----------------
    public static GameObject Generate()
    {
        var B = Build(out string error);
        if (B == null) { Debug.LogError(error); return null; }
        CsvImportUtility.EnsureFolder(OutputFolder);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            var shader = Shader.Find("BlueZone/LowPolyVertexColor") ?? Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = "LowPolyCity" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        var root = new GameObject("Low-Poly City");
        try
        {
            int total = 0;
            foreach (var group in new[] { "water", "ground", "roads", "buildings", "landmarks", "trees" })
            {
                if (!B.Vertices.TryGetValue(group, out var verts)) continue;
                var cols = B.Colors[group];
                int parts = (verts.Count + MaxVerticesPerMesh - 1) / MaxVerticesPerMesh;
                for (int p = 0; p < parts; p++)
                {
                    int start = p * MaxVerticesPerMesh, count = Math.Min(MaxVerticesPerMesh, verts.Count - start);
                    string name = parts == 1 ? group : group + "_" + p;
                    var mesh = SaveMesh(OutputFolder + "/City_" + name + ".asset", verts.GetRange(start, count), cols.GetRange(start, count));
                    var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(root.transform, false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = group == "water" || group == "roads" ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    go.isStatic = true;
                    total += count / 3;
                }
            }
            var landmarks = new GameObject("Landmarks"); landmarks.transform.SetParent(root.transform, false);
            foreach (var (id, centre, size) in B.Colliders)
            {
                var go = new GameObject("Landmark " + id, typeof(BoxCollider), typeof(CityLandmark));
                go.transform.SetParent(landmarks.transform, false); go.transform.localPosition = centre;
                go.GetComponent<BoxCollider>().size = size; go.GetComponent<CityLandmark>().landmarkId = id;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Low-poly city: {total} triangles, {B.Colliders.Count} landmarks.");
            return prefab;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // Reuses an existing mesh asset (keeps its GUID, so the prefab and scenes stay linked).
    private static Mesh SaveMesh(string path, List<Vector3> vertices, List<Color32> colors)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create = mesh == null;
        if (create) mesh = new Mesh();
        mesh.Clear();
        mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
        mesh.indexFormat = IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        var indices = new int[vertices.Count];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals();   // no shared vertices, so every facet keeps its own flat normal
        mesh.RecalculateBounds();
        if (create) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
        return mesh;
    }
}
