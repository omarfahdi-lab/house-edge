# copies the cropped gallery into the game with its index, and writes the credits list
import json, os, shutil
G = "../../../Assets/Resources/Gallery"
os.makedirs(G, exist_ok=True)
L = json.load(open("index.json"))
lines, rows = [], []
for o in L:
    clean = lambda t: t.replace("|", "/").replace("\n", " ").strip()
    artist = o["artist"] if not (o["artist"].startswith("Q") and o["artist"][1:].isdigit()) else "Unknown artist"
    shutil.copy(f"out/{o['qid']}.jpg", f"{G}/{o['qid']}.jpg")
    lines.append(f"{o['qid']}|{clean(o['title'])}|{clean(artist)}|{o['year']}")
    rows.append(f"| [{clean(o['title'])}](https://www.wikidata.org/wiki/{o['qid']}) | {clean(artist)} | {o['year']} |")
open(f"{G}/index.txt", "w").write("\n".join(lines) + "\n")
doc = ["# The gallery", "",
       f"{len(L)} public-domain portraits that play the dealers, hosts and you. Every artist died before 1926, so the",
       "paintings are public domain worldwide; the images come from Wikimedia Commons via Wikidata. Each was cropped",
       "around its face for the game (`Tools/Art/gallery/README.md` explains how).", "",
       "| Painting | Artist | Year |", "| -------- | ------ | ---- |"] + rows
open("../../../docs/GALLERY.md", "w").write("\n".join(doc) + "\n")
print(len(L), "exported")
