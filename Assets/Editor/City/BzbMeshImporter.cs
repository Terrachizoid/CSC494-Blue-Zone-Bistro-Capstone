// File responsibility: Imports the city generator's .bzbmesh files (Tools/City3D/citygen.py) as Unity meshes.
// Format: "BZBM" or "BZB2", uint32 vertex count, float32 xyz per vertex, RGBA bytes per vertex (alpha = gloss mask);
// BZB2 adds 2 bytes per vertex: material id and aux (0..255), stored in UV0 as (material, aux / 255). The city shader
// reads its material table with the id; the water shader uses aux as distance from the shore (shallow colour, foam).
// The file stores every triangle with its own three vertices. On import, vertices with the same position, colour,
// material and facet normal are welded, so a roof, wall or panel made of several triangles becomes one connected
// piece (shading stays flat: only same-normal corners are merged). That matters for baking: Unity's lightmap
// unwrapper makes one UV island per connected piece, so unwelded triangles each got their own tiny island and
// neighbouring triangles of one roof could bake very differently (sharp diagonal seams, black halves).
// Lightmap UVs (UV2) are generated here so the city can be baked; water is skipped (it is not lightmapped).
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

[ScriptedImporter(4, "bzbmesh")]
public sealed class BzbMeshImporter : ScriptedImporter
{
    [Tooltip("Generate lightmap UVs so the mesh can be baked (slower import).")]
    public bool generateLightmapUVs = true;

    public override void OnImportAsset(AssetImportContext ctx)
    {
        var bytes = File.ReadAllBytes(ctx.assetPath);
        if (bytes.Length < 8 || bytes[0] != 'B' || bytes[1] != 'Z' || bytes[2] != 'B' || (bytes[3] != 'M' && bytes[3] != '2'))
        { ctx.LogImportError("Not a .bzbmesh file: " + ctx.assetPath); return; }
        bool v2 = bytes[3] == '2';
        int count = System.BitConverter.ToInt32(bytes, 4);
        if (count < 0 || bytes.Length < 8 + count * (v2 ? 18 : 16)) { ctx.LogImportError("Truncated .bzbmesh file: " + ctx.assetPath); return; }
        var vertices = new Vector3[count];
        var colors = new Color32[count];
        int p = 8;
        for (int i = 0; i < count; i++, p += 12)
            vertices[i] = new Vector3(System.BitConverter.ToSingle(bytes, p), System.BitConverter.ToSingle(bytes, p + 4), System.BitConverter.ToSingle(bytes, p + 8));
        for (int i = 0; i < count; i++, p += 4)
            colors[i] = new Color32(bytes[p], bytes[p + 1], bytes[p + 2], bytes[p + 3]);
        var extra = new Vector2[count];
        if (v2)
            for (int i = 0; i < count; i++, p += 2) extra[i] = new Vector2(bytes[p], bytes[p + 1] / 255f);
        // Weld identical corners (same position, colour, material/aux and facet normal).
        var map = new System.Collections.Generic.Dictionary<WeldKey, int>(count);
        var wPos = new System.Collections.Generic.List<Vector3>(count); var wCol = new System.Collections.Generic.List<Color32>(count);
        var wUv = new System.Collections.Generic.List<Vector2>(count); var wNrm = new System.Collections.Generic.List<Vector3>(count);
        var indices = new int[count];
        for (int t = 0; t + 2 < count; t += 3)
        {
            var n = Vector3.Cross(vertices[t + 1] - vertices[t], vertices[t + 2] - vertices[t]).normalized;
            for (int k = 0; k < 3; k++)
            {
                int i = t + k;
                var key = new WeldKey(vertices[i], n, colors[i], extra[i]);
                if (!map.TryGetValue(key, out int w))
                {
                    w = wPos.Count; map[key] = w;
                    wPos.Add(vertices[i]); wCol.Add(colors[i]); wUv.Add(extra[i]); wNrm.Add(n);
                }
                indices[i] = w;
            }
        }
        var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(ctx.assetPath) };
        mesh.indexFormat = wPos.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(wPos);
        mesh.SetColors(wCol);
        mesh.SetUVs(0, wUv);
        mesh.SetNormals(wNrm);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();
        if (generateLightmapUVs && !mesh.name.StartsWith("water"))
        {
            var unwrap = new UnityEditor.UnwrapParam();
            UnityEditor.UnwrapParam.SetDefaults(out unwrap);
            // Margin of about 3 lightmap texels at the city's 1 texel/m (the object's lightmap is roughly
            // sqrt(surface area) texels across), so islands never share texels.
            float area = 0f;
            for (int t = 0; t + 2 < count; t += 3) area += Vector3.Cross(vertices[t + 1] - vertices[t], vertices[t + 2] - vertices[t]).magnitude * .5f;
            unwrap.packMargin = Mathf.Clamp(3f / Mathf.Sqrt(Mathf.Max(area, 1f)), 0.004f, 0.05f);
            UnityEditor.Unwrapping.GenerateSecondaryUVSet(mesh, unwrap);
        }     // tight box per tile: the camera skips tiles that are off screen
        ctx.AddObjectToAsset("mesh", mesh);
        ctx.SetMainObject(mesh);
    }

    readonly struct WeldKey : System.IEquatable<WeldKey>
    {
        readonly int px, py, pz, nx, ny, nz; readonly uint col; readonly int ux, uy;
        public WeldKey(Vector3 p, Vector3 n, Color32 c, Vector2 uv)
        {
            px = Mathf.RoundToInt(p.x * 1000f); py = Mathf.RoundToInt(p.y * 1000f); pz = Mathf.RoundToInt(p.z * 1000f);
            nx = Mathf.RoundToInt(n.x * 100f); ny = Mathf.RoundToInt(n.y * 100f); nz = Mathf.RoundToInt(n.z * 100f);
            col = (uint)(c.r | c.g << 8 | c.b << 16 | c.a << 24); ux = Mathf.RoundToInt(uv.x); uy = Mathf.RoundToInt(uv.y * 255f);
        }
        public bool Equals(WeldKey o) => px == o.px && py == o.py && pz == o.pz && nx == o.nx && ny == o.ny && nz == o.nz && col == o.col && ux == o.ux && uy == o.uy;
        public override bool Equals(object o) => o is WeldKey k && Equals(k);
        public override int GetHashCode()
        {
            unchecked { int h = px; h = h * 397 ^ py; h = h * 397 ^ pz; h = h * 397 ^ nx; h = h * 397 ^ ny; h = h * 397 ^ nz; h = h * 397 ^ (int)col; return h * 397 ^ ux ^ uy << 8; }
        }
    }
}
