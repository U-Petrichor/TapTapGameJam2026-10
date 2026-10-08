# Wall geometry scan

Scene: Assets/Scenes/GameScene.unity; scope: Scene/Environment; local-space tolerance: 0.0001. No scene/prefab modifications.

| Sample prefabs | Total | Plain objects | Existing prefab instances | Different materials | Extra components/children | Maximum geometry error |
|---|---:|---:|---:|---:|---:|---:|
| Wall1 | 88 | 0 | 88 | 69 | 88 | 3.29208E-06 |
| Wall10 | 2 | 0 | 2 | 1 | 2 | 1.62537E-06 |
| Wall2 | 58 | 0 | 58 | 0 | 58 | 0 |
| Wall3 / Wall4 / Wall6 | 169 | 0 | 169 | 70 | 169 | 1.56048E-05 |
| Wall5 | 62 | 0 | 62 | 16 | 62 | 3.60701E-06 |
| Wall7 | 10 | 0 | 10 | 8 | 10 | 2.64058E-06 |
| Wall8 | 4 | 0 | 4 | 2 | 4 | 9.83025E-07 |
| Wall9 | 8 | 0 | 8 | 6 | 8 | 1.64977E-06 |
| WallCornor | 15 | 0 | 15 | 0 | 15 | 0 |

Scanned mesh objects: 8242. Unique scene meshes: 7813. Meshes read: 270. Matched objects: 416. Ambiguous objects: 0 (0 unique meshes).

Matching checks local vertex positions, oriented triangle connectivity and submesh structure. Materials and vertex attributes are intentionally not identity criteria. Instance transforms are ignored. Unknown wall types outside the supplied samples are not classified. Existing colliders/scripts/children are reported for review before conversion.
