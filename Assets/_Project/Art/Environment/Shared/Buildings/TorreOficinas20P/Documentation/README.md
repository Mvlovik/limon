# Torre Oficinas 20P — Modern Office Tower (Free)

Low-poly modern 20-floor office tower, game-ready.

## Contents
- `Models/TorreOficinas20P.fbx` — building (~2,700 triangles), real-world scale (1 unit = 1 m)
- `Prefabs/TorreOficinas20P.prefab` — ready-to-use prefab with mesh colliders, marked static
- `Materials/` — 8 URP Lit PBR materials
- `Textures/` — Albedo, Normal and MetallicSmoothness maps (512–2048 px)
- `Scenes/Demo_TorreOficinas20P.unity` — demo scene

## Specs
- Footprint: 30 x 21 m (setback to 24 x 15 m from floor 15)
- Height: ~73 m roof, 90 m with antenna
- Pivot: ground level, centered
- Lightmap UVs generated on import
- Includes lobby with revolving door, entrance canopy, rooftop HVAC units, planters and shrubs

## Requirements
- Unity 2022.3 LTS or newer (tested on 2022.3.62f3)
- Universal Render Pipeline (URP) 14+

## Usage
1. Import the package.
2. Drag `Prefabs/TorreOficinas20P.prefab` into your scene.
3. Or open `Scenes/Demo_TorreOficinas20P.unity`.

Using Built-in Render Pipeline: select the materials and change the shader to `Standard`,
then assign Albedo, Normal and MetallicSmoothness maps (Metallic slot, smoothness source = Metallic Alpha).

## Notes
Model and textures were created in Blender.

## Support
jpinouribe@gmail.com
