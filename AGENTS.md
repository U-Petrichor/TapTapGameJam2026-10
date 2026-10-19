# UI authoring preferences

- Keep visual UI elements as explicit, editable objects in Unity's Hierarchy, with references exposed in the Inspector.
- For alternate UI states (such as day/night), prefer separate authored child groups and enable/disable those groups at runtime.
- Preserve designer edits to RectTransform position, anchors, size, scale, and appearance. Do not overwrite layout in initialization or state-change code.
- When runtime positioning is necessary, prefer a designer-editable Transform/GameObject reference over hardcoded coordinates.
- Runtime UI scripts should normally update content and visibility; author layout, sprites, fonts, and colors in the scene or prefab.
