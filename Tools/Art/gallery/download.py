import json, os, time, urllib.request, urllib.parse, urllib.error
L = json.load(open("list.json"))[:460]
for i, o in enumerate(L):
    path = f"raw/{o['qid']}.jpg"
    if os.path.exists(path): continue
    url = "https://commons.wikimedia.org/wiki/Special:FilePath/" + urllib.parse.quote(o["file"]) + "?width=700"
    for attempt in range(6):
        try:
            data = urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "HouseEdgeArt/1.0 (portfolio game; contact via github)"}), timeout=60).read()
            open(path, "wb").write(data)
            break
        except urllib.error.HTTPError as e:
            time.sleep(20 * (attempt + 1) if e.code == 429 else 2)
            if e.code == 404: break
        except Exception:
            time.sleep(10)
    time.sleep(1.5)
    if i % 50 == 0: print(i, flush=True)
print("done")
