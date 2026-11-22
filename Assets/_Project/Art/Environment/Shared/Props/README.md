# Environment / Shared / Props

Новые ассеты разложены по единой схеме:

```text
<AssetName>/
├── Models/     — исходные fbx/glb
└── Textures/   — внешние текстуры
```

Если у ассета появятся материалы или prefab-ы, кладём их в
`Materials/` и `Prefabs/` внутри этой же папки.

## Что импортировано

| Папка | Модели | Текстуры |
|---|---|---|
| `ATM_InstantCash` | `InstantCashATM.glb` | встроенные |
| `ElectricalBox_01` | `ElectricalBox_01.fbx` | BaseColor, Normal, Metallic, Roughness, Displacement |
| `ElectricalBox_Old` | `ElectricalBox_Old.glb` | Color, Metallic, Roughness |
| `LampOutside` | `WallLamp.fbx` | albedo + light (emission) |
| `RedBricks_PS2` | `RedBrick_A`, `RedBrick_B`, `RedBrick_C_Broken`, `RedBricks_Scene` | 3 albedo + ground AO |
| `TrafficCones_Dirty` | `DirtyTrafficCones.glb` | встроенные |
| `TrashPack` | `TrashPack.glb` | 4 текстуры мусора/баков |
| `TrashPile_01` | `TrashPile_01.glb` | albedo |

## Источники

Оригинальные архивы перенесены из `Assets` в:

```text
docs/ArtSources/Environment/
```

Там же лежит дубликат `pile-of-trash-1 (1).zip` — в проекте использован
один экземпляр, второй оставлен только как исходник.

## Примечания

- `ElectricalBox_01` и `RedBricks_PS2` требуют назначения внешних текстур
  в материалы (в FBX они не были встроены).
- `LampOutside` — уличный настенный светильник. Материал `M_WallLamp`
  (URP Lit) использует `WallLamp.png` как albedo и `WallLamp_Light.png`
  как карту свечения. Prefab `PF_WallLamp` уже с масштабом и точкой опоры
  на плоскости стены (лампа выходит вдоль +Z) и тёплым Point Light.
- У GLB-моделей материалы и текстуры обычно встроены в сам файл.
- Имена файлов приведены к виду `AssetName_*`, без пробелов и лишних
  суффиксов вида `-low-poly-asset-pack`.
