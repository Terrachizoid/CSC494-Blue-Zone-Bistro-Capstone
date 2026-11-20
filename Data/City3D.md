# Low-poly 3D city (2026-10-30; building library and frozen plan 2026-11-01)

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
- **Generator (Python, the only one):** `Tools/City3D/citygen.py` + `cityparts.py` (building library). Two steps:
  1. **plan** (random, seeded, run once): decides every lot and tree and writes the **lots table**
     `Assets/GameData/City/Blue_Zone_Bistro_CityLots.csv`. From then on that table *is* the city (frozen); `plan`
     refuses to overwrite it without `--reroll`.
  2. **build** (no global randomness): layout + lots -> meshes. Details inside one building (rooftop units, which
     corner an L cuts) come from that row's own `seed`, so editing a row never changes any other building.
  District rules used by plan: towers near the news tower, mid-rise around them (shorter further out), houses at the
  edges and the north, warehouses on some waterfront blocks, pocket parks on leftover blocks next to landmarks/parks.
- **Lots table columns:** `id, type, variant, x0, z0, x1, z1, base, floors, wall, roof, faces, seed, notes`.
  - `type`: `house` (variants gable, hip, lshape), `midrise` (grid, ribbon, chamfer, lshape), `tower` (setback,
    glass, ribbon, chamfer, tall), `warehouse`, `tree` (round, small, poplar; x0/z0 = position, x1 = scale).
  - `x0..z1` lot rectangle in metres; `base` ground height; `floors`; `wall`/`roof` hex colours; `faces` which sides
    face a street (S/N/W/E; shopfronts, doors and windows go there); `seed` small details.
  - Polish by editing rows (change a type, variant, floors, colour; delete a row), then rebuild.
- **Building library (all faceted triangles):** windows as glossy panels just proud of the wall (punched grid with
  sills, ribbons, floor-high bays, glass curtain with slab lines); shop ground floors with glass bays and coloured
  awnings; cornices; setbacks, chamfered and L footprints; hip/gable/L houses with doors and chimneys; sawtooth
  warehouse roofs with north glazing and loading doors; rooftop units, water tanks, stair huts, antennas.
  Trees: small (14 triangles), round (26), poplar (22); no cones.
- **Output:** `Assets/GameData/City/Generated/*.bzbmesh` (one mesh per group per 4x4-block tile, so each has a tight
  bounding box and the camera skips tiles off screen), `city_manifest.csv`, `city_landmarks.csv`.
  `BzbMeshImporter` imports .bzbmesh as meshes (vertex colour alpha = gloss).
- **Unity menus** (Tools > Blue Zone Bistro): **Generate Low-Poly City** builds the prefab
  `Assets/GameData/City/LowPolyCity.prefab` from the generated meshes (no Python needed) and keeps the lighting you
  tuned on the previous prefab; **Rebuild City Meshes (Python)** runs build after you edit the tables;
  **Reroll City Plan (Python)** makes a new random plan (asks first). Python 3 must be installed for those two.
  Command line: `python Tools/City3D/citygen.py build .` / `plan . --reroll` / `preview . out.png [--close]`.
- **Budget now:** about 58k triangles (buildings 39k, trees 13k, roads/ground 6k), 78 meshes, one material.
  Phones and WebGL handle 150k+ with one light, so there is room for more detail.
- **Shader:** `Assets/Art/Shaders/BZB_LowPolyVertexColor.shader` ("BlueZone/LowPolyBlinnPhong"): Blinn-Phong with
  ambient (hemisphere sky/ground), diffuse, specular and a cheap fresnel sky reflection, plus distance fog.
  Coefficients Ka/Kd/Ks/Kr/shininess are on the material. Vertex alpha is the gloss mask: water, glass domes, the
  tower deck, store bands, the bistro awning and boats are glossy; everything else is matte. Two passes with the same
  lighting: UniversalForward (3D renderer, shadows) and Universal2D (the project's 2D renderer and scene view).
- **Lighting:** the prefab root has `CityLighting` and a child **Sun** directional light. Rotate the Sun to move the
  light; colour, intensity, ambient sky/ground, fog and the wellbeing tint (`SetWellbeing`, not wired yet) are on
  CityLighting and update live in edit mode.
- **Rendering setup:** the project's default renderer is 2D (no 3D lights, no shadow maps). The generator (or Tools >
  Blue Zone Bistro > Set Up 3D Rendering) adds `Assets/Settings/City3DRenderer.asset` to the URP renderer list
  (2D stays the default) and raises the shadow distance to 700 m. The city camera uses that renderer.
- **Budget:** about 41k triangles in one material, six combined meshes (split at 60k vertices for 16-bit indices).
  Trees are ~75% of it; their density is the main knob.

## Previewing without Unity
`python Tools/City3D/citygen.py preview . out.png` renders the same city (z-buffered, Blinn-Phong-ish shading).
Renders of the current plan: `Data/Previews/City3D/city_*_lots.png`.

## Seeing it in the game (optional, off by default)
1. Run Tools > Blue Zone Bistro > Generate Low-Poly City once.
2. On the BistroUITheme asset, tick **Use 3D City** and drag `LowPolyCity.prefab` into **City 3D Prefab**.
3. Play. The city screen shows the 3D city instead of the painted map. Untick to go back; with no prefab set, or if
   anything fails, the painted map is used.

How it works: `BistroCity3D` (`Assets/Scripts/World3D/`) puts one copy of the prefab far from the UI with its own
camera, sun (only if the scene has none), ambient light and fog, and renders it into a texture. `BistroCity3DView`
shows that texture in the city viewport: click-drag pans with springy edges, the wheel zooms toward the cursor,
clicking a building or its name tag does exactly what the 2D map does (store opens, News opens the news panel,
closed stores are greyed). Camera state lives on the host, so it survives screen rebuilds; the camera only renders
while the city screen is showing. Camera angle (Yaw/Pitch/FieldOfView) is on the host component for tuning; tick **City 3D Orthographic** on the
theme for a diorama look (same rendering cost, no perspective).

## Lighting and shading model (2026-11-11)
    final = albedo * (1 - F) * (indirect diffuse + direct sun diffuse)
          + F * (indirect specular + direct sun specular),  then fog
- **Indirect diffuse:** the baked lightmap (bounced light + baked AO, made once by Unity's lightmapper) when the city
  has been baked; otherwise CityLighting's sky/ground hemisphere. Times Ka and the wellbeing tint; SSAO darkens it.
- **Direct:** the live Sun (CityLighting): Kd * N.L * shadow, and Blinn-Phong Ks * (N.H)^shininess.
- **F:** Schlick Fresnel (F0 on the material) times the vertex gloss mask (only glossy surfaces reflect).
- **Indirect specular:** the city's baked reflection probe, looked up along R = reflect(-V, N), blurred by roughness.
- **Water** (`BZB_LowPolyWater.shader`, material `LowPolyWater.mat`): animated ripple normals, Fresnel, probe
  reflection, plus **SSR**: the reflected ray is marched across the screen against the depth buffer and takes the
  colour it hits; it fades with distance, near screen edges and on a miss (then the probe shows).
- Shader files: `Assets/Art/Shaders/BZB_LowPolyVertexColor.shader` (city, `Shade` logic in the ForwardLit pass) and
  `BZB_LowPolyWater.shader`. Coefficients on `LowPolyCity.mat` / `LowPolyWater.mat`; sun, ambient, fog and tint on
  CityLighting.

### Baking (once, in the editor)
1. Tools > Blue Zone Bistro > **Generate Low-Poly City**. This also sets up the 3D renderer (City3DRenderer),
   shadow distance 700 m, the scene's bake settings (Baked Indirect, 1 texel/m, 3 bounces, AO), a Mixed Sun and a
   baked Reflection Probe over the city. The city copy in the scene (at 1000, 0, 1000) is the one the game shows.
2. Window > Rendering > Lighting > **Generate Lighting**. Save the scene (lightmaps belong to the scene).
3. Re-bake after regenerating the city or changing the sun a lot (bounced light was computed with the old sun).
4. Optional: City3DRenderer.asset > Add Renderer Feature > **Screen Space Ambient Occlusion** (Source: Depth Normals).
   The graphics panel switches it on/off.

### Graphics settings and FPS (BistroGraphics, BistroGraphicsPanel)
FPS counter bottom-left; click it or press **F3** for the panel: preset (Low/Medium/High/Custom), render scale and
MSAA (3D city only), sun shadows / soft edges / shadow-map size, SSAO, SSR on water with steps and max distance,
baked lighting on/off, probe reflections on/off, and debug views (albedo, indirect, direct, specular, AO, normals,
shadow, SSR hits). Saved per player. Phones start on Low. In the editor the shadow-map size is put back on exit.

## Look pass (2026-11-11 evening): closer to the painted city
- **Palette:** cool whites and light greys with some warm creams, deep greens, muted roofs (`PAL`, `WALLS`... at the
  top of `Tools/City3D/cityparts.py`). `citygen.py recolor .` re-applies the palette to the lots table.
- **Materials:** every triangle has a material id (UV0.x): 0 plaster, 1 glass, 2 metal, 3 stone, 4 foliage,
  5 paint/tiles/boats, 6 sand (7-9 water). The city shader reads F0, roughness, specular and shininess for each from
  the table on `LowPolyCity.mat` (`_M0`..`_M6`); `_Ks`/`_Kr` scale them all.
- **Trees:** about 2,000 (street rows, fuller parks, groves on the waterfront lawns); kinds small, round, poplar,
  broad (several crowns) and bush. `citygen.py add-trees .` appends more without touching existing rows.
- **Shoreline:** the layout's coast points are smoothed into curves. Each original stretch is a seawall (stone wall,
  coping, railing, boulders at the base) or a beach (sand slope); a promenade with lamps and benches runs inside;
  open land is lawn.
- **Water:** sea extends 2.5 km; a shore band gives shallow turquoise water and animated foam lines rolling in;
  directional waves (four Gerstner-style waves around one wind direction) on the water material.
- **Places:** round tower plaza (rings, spokes, planted sectors, benches, lamps), curved loop and cross paths with
  hedges in the parks, stone rims on ponds, arched bridge with end towers (now joining the west island to the city),
  plank piers with posts, sail and motor boats, cars on streets and in store car parks, bistro terrace and planters.
- **Post-processing** (`BZB_CityPost.shader`, a Full Screen Pass feature on City3DRenderer added by Generate):
  outlines from depth/normal edges, tilt-shift blur toward top/bottom/sides, colour grading. Tune on
  `Assets/GameData/City/CityPost.mat`; switch each part in the F3 panel.
- **Budget:** about 103k triangles (buildings 39k, trees 45k, props 7k), 97 tile meshes.

## Detail pass, redone (2026-11-11 late, after Owen's review)
The first detail pass (scattered rocks, uniform split windows, balconies, slab bands, roof gardens, wall-side boats)
was rolled back: it did not read like the painted city. Kept from it: the detail layer, zebra crossings, piers that
start at the shoreline. In its place:
- **Shores** (`shoreline` in citygen.py): two kinds alternate, as in the painting.
  - *Terrace*: a raised stone embankment 2.2 m above the water (1.6 m above the street): smooth light wall with a
    darker waterline, coping lip, paved deck 2.2-4.5 m wide (narrower where a building or street is close), a
    two-rail railing with dark posts every 2.4 m, lamps and benches on the deck, a street-side wall with a flight of
    steps where there is room. Piers leave from the terrace at deck height.
  - *Beach*: a sand cove that bulges out to 15 m in the middle of its run and narrows at the ends, behind a low kerb
    wall. Where a beach meets a terrace the terrace gets an end face and a small rocky point.
  - Boulders only as short runs tight against the wall foot on a few stretches; none scattered in the water.
  - Near the bridge the old low seawall stays, so the bridge ramp meets street level.
- **Windows: one facade scheme per building** (`FACADES`, picked from the building's seed): big punched grid (3
  across on a small face), dense small grid, paired windows, ribbon band with concrete between, deep glass band,
  narrow floor-high strips, and windows in bays between pilasters. Every wall gets windows (not only street sides).
  Towers: dense grid, deep bands, ribbons or strips; glass towers get vertical mullions. The detail layer adds a
  light frame behind each punched window.
- **Roofs**: a parapet rim (outer face, cap, inner face) on every flat roof, rooftop units inside it.
- **Detail layer**: "<group>_detail" meshes (window frames, zebra crossings). Preset Low hides them; F3 panel
  "Extra detail". About 118k triangles base, 145k with detail.
- Grading off by default and neutral (saturation 1, contrast 1.04, white tint).
- Unused files from the first pass may remain in Assets/GameData/City/Generated (ground_detail_*, props_detail_*,
  trees_detail_*, landmarks_detail_*, landmarks_0_0). They are not in city_manifest.csv, so the generator ignores
  them; safe to delete.

## Hero landmarks (2026-11-12): the five places the game is played around
`Tools/City3D/citylandmarks.py`, after the painted city (Art/UI/cityBackground.png). Names are spelled in 3D block
letters (5x7 caps font, `text3d`) so each place reads like the painting from the game camera.
- **Verdant & Co.** (S001): cream classical market hall, two storeys of round-headed windows between pilasters, string
  course, cornice and parapet; glass greenhouse roof of two crossing ribbed vaults with a lantern dome; arched glass
  entrance porch on the south and a side porch on the west; green VERDANT on an attic panel over the entrance;
  forecourt with planter trees and benches.
- **CheapCart** (S002): beige big box with orange corner towers and entrance pylons, CHEAPCART in orange-brown, a
  wide shopfront under a canopy, display windows, skylights and AC units on the roof, two-row parking, cart corrals.
- **MegaBulk Club** (S003): warehouse hall with a low curved standing-seam metal roof, cream name panel with blue
  MEGABULK rising above the eaves between brick piers, glass entry bays, two-storey office block with a brick corner
  on the west, three loading docks with roll-up doors and two box trucks on the east, parking in front.
- **News Tower**: round glass pavilion at the foot, core shaft with four splayed legs and tie rings, a two-level
  glass observation pod with white bands, a flared rim with railing, a blue glass drum and a red/white mast.
- **Blue Zone Bistro**: cream two-storey corner building facing the water, a taller corner tower with BISTRO on both
  faces, big ground-floor glass under teal awnings, framed windows upstairs, roof terrace with tables, stair
  penthouse; a deck of umbrella tables running down to the waterfront promenade.
- Lot trees no longer stand on the hero plots or on the bistro terrace. About 6.4k triangles for all five, plus
  1.9k in the detail layer (chairs, mullions, door slats, stall lines).

## Bake seams fixed (2026-11-12): welded meshes for lightmap UVs
Symptom: roofs baked half sky-blue, half black, split exactly along a triangle diagonal; neighbouring roofs very
different. Cause: the .bzbmesh format stores every triangle with its own three corners, so Unity's lightmap unwrapper
saw every triangle as a separate island. Thousands of islands (many smaller than one 1 m texel) were packed with a
tiny margin, so texels were shared between unrelated islands and each triangle of a roof baked on its own; an island
whose few texels touched hidden geometry (e.g. a sidewalk top inside a building) went black.
Fix: `BzbMeshImporter` (v4) welds corners with the same position, colour, material and facet normal before unwrapping
(shading stays flat), and sets the pack margin to about 3 texels for the tile's size. Trees, props and the detail layer
now take indirect light from probes (`ReceiveGI.LightProbes`) instead of the lightmap; they still bounce light.
After updating: let Unity reimport the meshes, Generate Low-Poly City, then Generate Lighting again.

## Store interiors (2026-11-13): shell + interior per store
`Tools/City3D/cityinteriors.py`, after the 2D store screens (Art/Stores/S00x_background.png). Each store now has:
- **site** (parking, forecourt, trucks): tiled with the rest of the city, always visible;
- **shell** (`store_<id>_shell`): the building as seen from the street; hidden when the player is inside;
- **interior** (`store_<id>_interior`): a cutaway "dollhouse" room, hidden in the city view. North and east walls are
  full height with the shelf signs; south and west walls are cut low (glass) so the room reads from the default
  camera (yaw about 34). The room is smaller than the building (Verdant 32x26 m, CheapCart 44x22, MegaBulk 40x26)
  and fixtures are about 1.5x real size, so it reads like the 2D screens at the store-view zoom.
  - Verdant: produce stands and fruit wall (PRODUCE), wellness wall shelving, island run and round display
    (WELLNESS), white plinths and an east-wall cabinet (LUXURY), sage rugs, checkout, plants.
  - CheapCart: back freezers (BACK_FREEZER), eye-level gondola (EYE_LEVEL), bottom-shelf gondola with big plain
    packs low (BOTTOM_SHELF), bargain bin by the door (BARGAIN_BIN), wall shelving, checkout.
  - MegaBulk: pallet racking and floor stacks (PALLETS), sack shelving and sack pallets (BULK_SACKS), a field of
    produce crates (BULK_PRODUCE), yellow aisle lines, checkout, cart row, pallet jack.
- Which shelf zones exist comes from the store data (`shelf_definitions` in Blue_Zone_Bistro_Stores.csv); sign text is
  the shelf's display name. The layout code says where each known zone sits; a new shelf ID needs a placement there.
- `city_shelves.csv` lists each shelf zone's box (padded, easy to tap). The Unity generator adds one BoxCollider +
  `StoreShelf` (storeId, shelfId) per zone under the interior, and a `CityStore` per store (Stores/Store S00x) with
  `SetInside(bool)` to swap shell and interior. Interiors are not in the city bake (probe-lit).
- About 7k (Verdant), 10k (CheapCart), 6k (MegaBulk) triangles each, drawn only in the store view.
- Next: the store view itself (camera glide in/out, focus fade in the post pass, interior lights instead of the sun,
  clicks on StoreShelf opening the purchasing page, MegaBulk "opens on day 4"), with the 2D store screens kept as a
  fallback toggle.

## Layout from an AI reference mesh (2026-11-13, Verdant trial)
Pipeline: 2D store screen -> image-to-3D (Tripo, whole store, kept raw in Data/AssetTrials/) -> `layout_extract.py`
-> `Tools/City3D/interiors/<store>_layout.json` -> procedural interior (`cityinteriors.py`).
- The AI mesh is used only as a spatial reference (it is fused, skewed and ~500k triangles). The extractor reads the
  GLB directly (numpy + Pillow), flattens it into a top-down height map, finds the walls (tall outer ring; low
  cut-away walls are recognised by their thin, room-spanning shape), straightens the room into a rectangle, and
  splits what stands on the floor into fixtures: position, footprint, height, round/long, direction, which wall it
  touches, a sampled side colour, and the covered cells (for irregular runs).
- `verdant()` places a procedural fixture for each: irregular runs become produce stands on the covered squares
  (walkways where coverage is weak), round pieces become the wellness display or a luxury table, wall runs become
  wall shelving, a long front counter becomes the checkout, small pieces become plants/pedestals. Zones (PRODUCE /
  WELLNESS / LUXURY) follow position. Without the JSON the old fixed layout (`verdant_fixed`) is used.
- Verdant's building now has the room's footprint (32 x 30 m), so walls line up when the roof lifts.
- Run: `python Tools/City3D/layout_extract.py <mesh.glb> Tools/City3D/interiors/S001_layout.json --preview out.png`
- Rights: the Tripo free plan has attribution/ownership terms; its geometry is not shipped, only the derived layout.

## Not done yet (next steps)
- Check in Unity: Generate Low-Poly City with the new meshes; shadows through the 3D city camera (Use 3D City).
- Street planters; store interiors are 2D and stay so.
- Check the bake and SSR in play mode; tune samples/resolution; shadow distance that follows zoom; optional lit windows at night.
- Mobile: touch pinch-zoom is not wired yet (drag pan works); render-texture size follows the viewport.
