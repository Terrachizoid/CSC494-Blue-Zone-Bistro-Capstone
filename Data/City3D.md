# Low-poly 3D city (first pass, 2026-10-30)

Goal (Owen's decision): places become low-poly 3D, the card/UI layer stays 2D on top, screen by screen with the
painted 2D art as fallback. The city is first. Look target: "shader-pack" style, i.e. simple faceted geometry made
rich later by the material (noise normals, ramps, fog, post-processing). Web build that also runs on phones.

## How it is built
- **Layout table:** `Assets/GameData/City/Blue_Zone_Bistro_CityLayout.csv`, traced from the city concept
  (`Assets/Art/UI/cityBackground.png`). Units are city blocks (1 block = block_size + road_width = 32 m).
  - `setting` rows: seed, grid size, block and street size.
  - `coast` / `island` rows: land outlines (points in order). Streets, blocks and the shore follow them.
  - `landmark` rows: S001 Verdant (dome), S002 CheapCart and S003 MegaBulk (big boxes), NEWS (tower), BISTRO.
    `store_id` / `id` match Stores.csv and the 2D click regions.
  - `park` (style `plaza` or `park`), `pond`, `bridge`, `pier` rows.
- **Generator:** Tools > Blue Zone Bistro > Generate Low-Poly City (`Assets/Editor/City/LowPolyCityGenerator.cs`).
  Fills everything else procedurally: streets, blocks split into lots, mid-rise downtown (taller near the tower),
  houses with gable roofs at the edges, shore houses, street/park/promenade trees (jittered icosahedra, 20
  triangles), sand strip and rocks, boats. Every shape is a triangle mesh; no smooth primitives.
- **Output:** meshes in `Assets/GameData/City/Generated/` (reused on regenerate so links stay), material
  `Assets/GameData/City/LowPolyCity.mat`, prefab `Assets/GameData/City/LowPolyCity.prefab`, placed in the open
  scene as "Low-Poly City". Each landmark has a BoxCollider + `CityLandmark` (landmarkId) for clicks later.
- **Shader:** `Assets/Art/Shaders/BZB_LowPolyVertexColor.shader` (URP): vertex colour, main light + shadows, ambient
  probe, fog, two-sided. Intentionally minimal: the starting point for Owen's shading work.
- **Budget:** about 41k triangles in one material, six combined meshes (split at 60k vertices for 16-bit indices).
  Trees are ~75% of it; their density is the main knob.

## Previewing without Unity
`python Tools/City3D/citygen.py <project root> out.png` builds the same city (same random numbers, same call order)
and renders an isometric preview. Renders of the current seed: `Data/Previews/City3D/`. Keep the Python and C#
files in step when changing the generator, or the preview stops matching.

## Not done yet (next steps)
- 3D view layer: a camera rendering the city behind the Canvas on the city screen, drag to pan/zoom, raycast
  clicks on `CityLandmark` colliders reporting the same IDs the 2D regions use; switch per screen with 2D fallback.
- Look: lighting setup (sun angle, ambient), fog/volume profile, then Owen's shader pass. Windows and facade detail
  are expected to come from the material (triplanar/noise), not geometry.
- Not checked in Unity yet: compile the generator, run it, check the shader compiles on URP.
