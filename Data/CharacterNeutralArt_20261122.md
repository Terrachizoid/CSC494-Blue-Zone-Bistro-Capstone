# Neutral character art — 2026-11-22

User approved a simple chunky cartoon direction: dark organic outlines, broad hair shapes, restrained colours, plain human facial features, mostly frontal head-to-waist framing. Natural asymmetry and individual arm positions replace ID-photo stiffness. No fixed ages were assigned. June's earlier elderly concept was rejected; do not treat that as character canon.

Scope: seven default portraits only. Six regulars' happy/concerned/disappointed/tired artwork is deferred. The visiting chef uses one default for all dialogue emotions; the chef's unused happy row was removed and welcoming aliases added to neutral. No dialogue text, effects, state machines, motion code or fallback rules changed. Existing CSV image paths are used.

## Selected outputs

Built-in image generation, transparent-background output preserved without pixel editing. Source directory: `C:/Users/TERRA/.codex/generated_images/01a0c490-ad67-7e80-9c87-d625cf4b4e02/`.

| Runtime file under Assets/Art/Characters | Generated source | Pose |
|---|---|---|
| U001_neutral.png | exec-5ed75a4f-8eeb-4d44-a9cf-1846e01facea.png | June: small attentive inclination, both arms at sides |
| U002_neutral.png | exec-83e9234f-379c-4923-8b56-ae92949e964c.png | Dev: loosely folded arms, relaxed shoulders |
| U003_neutral.png | exec-27261050-c6bc-48be-8c4d-04c6ec34562d.png | Rosa: slight lean, hands resting together low |
| U004_neutral.png | exec-988b3478-8762-4734-9691-7c1c2357cb85.png | Sam: gentle tilt, arms at sides; rejected hand-in-pocket pose removed |
| U005_neutral.png | exec-e5d5f956-427c-463e-8512-3666464eaefe.png | Marco: forearm resting low, ochre cap and beard |
| U006_neutral.png | exec-8f8936b6-daf0-474f-8f95-46159fce54fe.png | Priya: subtle torso turn, ponytail, arms down |
| VisitingChef_neutral.png | exec-9bfd19c5-a2c1-4d22-a022-7cc0eb660a0c.png | Proposed Japanese visiting chef: small open-handed conversational gesture |

## Prompt/reference record

Each was edited from its prior individual neutral prototype, preserving identity, clothing and the approved June graphic style. Shared prompt constraints: one neutral adult character, head to waist only, both eyes visible, mostly front-facing, thick dark-brown contours, chunky hair, few muted colours, sparse flat shading, no props or lower body, no detailed anime rendering, transparent background, no halo. Per-character pose instructions are recorded in the table. June/Dev were revised again after the user rejected their repeated one-hand-in-front poses: June both arms down; Dev arms loosely folded across lower chest. The original user references guide abstraction only, not copied identities.

Image previews display brown haze around some figures; sampled source pixels outside the silhouette have alpha zero despite nonzero brown RGB. Judge actual compositing in Unity rather than the RGB preview alone. Preserve alpha and do not flatten onto black.

All appearance choices are visual designs rather than new backstory. The story describes Okinawa-inspired cooking; the Japanese chef design follows the user's requested visual direction.

Verification: CharacterNeutralCapture passed in a disposable WebGL-target Unity project. All seven neutral sprites resolve, six non-neutral emotions fall back to each neutral, and chef has exactly one row. Actual portrait component was rendered over authored room/counter; Dev and chef inspected. Transparent compositing is clean. Output PNGs and log remain in ignored Tools/UI/ReviewProject. Imported database and new metas copied after verifying matching CSV and database meta. Web texture sizes are about 913x1024 RGBA32 (Marco 909x1024); no new release-build size claim. No runtime source edits.
Also filled SampleScene GameBootstrap.characterExpressions with the existing database GUID; the new optional field had not yet been serialized in that scene.

## Follow-up: expressions and compression completed
The deferred 24 expressions are now integrated. All seven neutral PNGs were also proportionally prepared on transparent 1024-square canvases to enable actual DXT5 Web import; generated originals remain external. The earlier NPOT/RGBA32 observation is historical. See CharacterExpressionArt_20261122.md for current status and limits.
