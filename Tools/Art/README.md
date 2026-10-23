# Art tools

The characters and room backdrops are made from public-domain and openly licensed art by these scripts
(Python 3 + Pillow). Sources and licenses are listed in `docs/CREDITS.md`.

- `gallery/` builds the gallery of famous portraits that plays everyone except the bosses (see its README).
- `portrait.py <painting> <out.jpg> <left> <top> <right> <bottom>` crops a boss painting (box in fractions of its size)
  to a 480x600 character portrait. Output goes to `Assets/Resources/Portraits`, named after the character.
- `grade.py <photo> <out.jpg> <tint hex> [blur]` turns a photo into a room backdrop: crop, soft blur, darken,
  colour grade and vignette. Output goes to `Assets/Resources/Backgrounds`. Sources are listed in `docs/CREDITS.md`.
