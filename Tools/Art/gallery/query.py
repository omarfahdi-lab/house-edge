# the most famous portrait paintings on Wikidata: painting, genre portrait, with an image, by an artist who died
# before 1926 (public domain worldwide), ranked by how many Wikipedias have an article on it
import json, urllib.request, urllib.parse
q = """
SELECT ?item ?itemLabel ?creatorLabel ?image ?links ?year WHERE {
  ?item wdt:P31 wd:Q3305213; wdt:P136 wd:Q134307; wdt:P18 ?image; wdt:P170 ?creator; wikibase:sitelinks ?links.
  ?creator wdt:P570 ?died. FILTER(YEAR(?died) < 1926)
  OPTIONAL { ?item wdt:P571 ?inception. BIND(YEAR(?inception) AS ?year) }
  FILTER(?links >= 3)
  FILTER NOT EXISTS { ?item wdt:P136 wd:Q40446 }
  FILTER NOT EXISTS { ?item wdt:P180 wd:Q40446 }
  FILTER NOT EXISTS { ?item wdt:P180 wd:Q10791 }
  FILTER NOT EXISTS { ?item wdt:P180 wd:Q1048607 }
  SERVICE wikibase:label { bd:serviceParam wikibase:language "en". }
} ORDER BY DESC(?links) LIMIT 900
"""
url = "https://query.wikidata.org/sparql?" + urllib.parse.urlencode({"query": q, "format": "json"})
req = urllib.request.Request(url, headers={"User-Agent": "HouseEdgeArt/1.0 (portfolio game)"})
rows = json.load(urllib.request.urlopen(req, timeout=120))["results"]["bindings"]
seen, out = set(), []
for r in rows:
    img = urllib.parse.unquote(r["image"]["value"].split("/Special:FilePath/")[1])
    if img in seen: continue
    seen.add(img)
    title = r["itemLabel"]["value"]
    if title.startswith("Q") and title[1:].isdigit(): continue
    out.append({"title": title, "artist": r["creatorLabel"]["value"], "year": r.get("year", {}).get("value", ""),
                "file": img, "links": int(r["links"]["value"]), "qid": r["item"]["value"].rsplit("/", 1)[1]})
json.dump(out, open("list.json", "w"), indent=1)
print(len(out))
for o in out[:25]: print(o["links"], o["title"], "-", o["artist"])
