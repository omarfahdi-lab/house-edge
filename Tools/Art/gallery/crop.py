# crops each painting to a 4:5 portrait around the largest face OpenCV finds (or the top middle if none)
import json, os, sys, cv2
from PIL import Image, ImageFilter
L = json.load(open("list.json"))
front = cv2.CascadeClassifier(cv2.data.haarcascades + "haarcascade_frontalface_default.xml")
prof = cv2.CascadeClassifier(cv2.data.haarcascades + "haarcascade_profileface.xml")
W, H = 400, 500
index = []
hashes = []
for o in L:
    src = f"raw/{o['qid']}.jpg"
    if not os.path.exists(src): continue
    try: im = Image.open(src).convert("RGB")
    except Exception: continue
    if im.width < 250 or im.height < 250: continue
    gray = cv2.cvtColor(cv2.imread(src), cv2.COLOR_BGR2GRAY)
    minside = max(28, int(min(gray.shape) * 0.09))
    faces = list(front.detectMultiScale(gray, 1.1, 6, minSize=(minside, minside)))
    if not faces: faces = list(prof.detectMultiScale(gray, 1.1, 6, minSize=(minside, minside)))
    if faces:
        x, y, w, h = max(faces, key=lambda f: f[2] * f[3])
        cw = min(im.width, max(w * 3.0, im.width * 0.25)); ch = cw * H / W
        if ch > im.height: ch = im.height; cw = ch * W / H
        cx, cy = x + w / 2, y + h / 2
        left = min(max(cx - cw / 2, 0), im.width - cw)
        top = min(max(cy - ch * 0.36, 0), im.height - ch)
        found = True
    else:
        continue
    crop = im.crop((round(left), round(top), round(left + cw), round(top + ch))).resize((W, H), Image.LANCZOS)
    crop = crop.filter(ImageFilter.UnsharpMask(radius=1, percent=50, threshold=2))
    # the same picture under two entries: skip it
    small = crop.convert("L").resize((9, 8))
    px = list(small.getdata())
    sig = sum(1 << i for i in range(64) if px[(i // 8) * 9 + i % 8] > px[(i // 8) * 9 + i % 8 + 1])
    if any(bin(sig ^ other).count("1") <= 8 for other in hashes): continue
    hashes.append(sig)
    crop.save(f"out/{o['qid']}.jpg", quality=80, optimize=True)
    index.append(o | {"face": found})
json.dump(index, open("index.json", "w"), indent=1)
print(len(index), "cropped,", sum(1 for i in index if i["face"]), "with faces")
