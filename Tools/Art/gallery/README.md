# The gallery

Builds `Assets/Resources/Gallery`: a few hundred famous public-domain portraits that the game deals out to the
dealers, hosts and you, so faces rarely repeat between runs. Needs Python 3 with Pillow and
`opencv-python-headless<5` (OpenCV 5 dropped the Haar face detector). Run the scripts from this folder, in order:

1. `query.py` asks Wikidata for paintings in the portrait genre with an image, by an artist who died before 1926
   (public domain worldwide), ranked by how many Wikipedias cover them. Nudes and titles that suggest nudity are
   filtered out. Writes `list.json`.
2. `download.py` fetches the top 460 from Wikimedia Commons at 700px into `raw/`. Commons rate-limits, so it goes
   slowly and backs off on errors; rerun it to pick up where it stopped.
3. `crop.py` finds the largest face with OpenCV and crops a 400x500 portrait around it (face about a third of the way
   down). Paintings without a clear face, and near-duplicates (by image hash), are skipped. Writes `out/` and
   `index.json`.
4. Check the crops by eye and delete entries from `index.json` that missed the face or aren't suitable.
5. `export.py` copies the crops into the game, writes `Gallery/index.txt` (`qid|title|artist|year`) and
   `docs/GALLERY.md`.

The bosses keep their own hand-cropped portraits in `Assets/Resources/Portraits` (see `../portrait.py`).
