# Verdant whole-store Tripo trial — 2026-11-13

## Result
Automated browser upload, generation, and GLB export completed. Experimental raw output only; not integrated into Unity or approved as final art.

- Model: Verdant_WholeStore_H25_Raw.glb (original export bytes, unchanged).
- Reference: reference.png, copied from Assets/Art/Stores/S001_background.png (approved architecture v2 interior).
- Provider job: https://studio.tripo3d.ai/workspace/generate/51f7d06c-2648-4a26-a19e-b8b19cec00f4
- Settings: HD Model, H2.5 Legacy, Geometry & Texture, single input image, Generate in Parts off, 8K off, Public (free account default). No text prompt entered.
- Cost: 40 free Studio credits; balance observed 200 before / 160 after. No payment or subscription. Export allowance observed 15 before / 14 after.
- Export: GLB, current 4K textures. H2.5 selected because provider documentation lists it as the free-export version; this does not benchmark H3.1 quality.
- inspection.json records file hash and structural counts. Valid GLB v2 header and length, 502300 triangles, one mesh/node/material, three embedded images, no external resource URIs.
- tripo_preview.png records the browser preview (export panel fading; not a clean game-camera capture).

## Claude handoff
Preserve this raw file. Make any processed derivative in a separate path. First preview at the intended elevated store camera, then assess proportions, shelf fidelity, fused geometry, textures, normals and shadows. One mesh does not prove one connected component. Automated partitioning cannot recover missing detail. Static decoration may stay combined; use separate gameplay click volumes keyed to existing shelf IDs. Do not replace the current store or its semantics automatically.

Not checked: full mesh validity, topology/connectivity, normals, Unity import/material compatibility, camera-match quality, performance or collision. No game code, CSV or live assets changed. Optimization will likely be needed; this is not low-poly.

## Provenance / rights
AI-generated source illustration and Tripo-generated mesh. Tripo Studio free-plan documentation lists attribution requirements and provider ownership; do not assume unrestricted paid-plan ownership. Check applicable terms before publishing this trial. Documentation: https://www.tripo3d.ai/product-doc/getting-started/pricing-credits . No new terms were accepted by the assistant.

## Pipeline observations
File chooser upload succeeded. AX-based export selection was unreliable; a dialog-scoped Playwright Export click succeeded and returned the download path. No second generation or retry was submitted. Automatic approval review rejected opening Upgrade to inspect plans; no upgrade action proceeded. Free GLB export was subsequently confirmed by the actual export dialog and downloaded bytes.
