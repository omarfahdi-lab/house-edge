# Art tools

The characters and room backdrops are generated from outside art by these scripts (Python 3 + Pillow).

- `bake.py <out dir> [names...]` builds every character from Kenney's
  [Modular Characters](https://kenney.nl/assets/modular-characters) pack (CC0): legs, arms, torso and head as
  separate layers with pivots, so the game can animate them, plus shared mouths for expressions. Shirts and pants
  are recoloured and the hats, shades, cigars, monocles and card fans are drawn in the same flat style.
  Point `KENNEY_PNG` at the pack's `PNG` folder. Output goes to `Assets/Resources/Characters`.
- `grade.py <photo> <out.jpg> <tint hex> [blur]` turns a photo into a room backdrop: crop, soft blur, darken,
  colour grade and vignette. Output goes to `Assets/Resources/Backgrounds`. Sources are listed in `docs/CREDITS.md`.
- `preview.py <dir> <out.png> <ids...>` reassembles baked characters the way the game does, for checking.
