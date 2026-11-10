# Built-in drug list

`egyptian-drugs.json.gz` is the gzip-compressed `data/egyptian-drugs.json` from the open **Egyptian Drug Database** project, re-serialized without whitespace.

| | |
|---|---|
| Source | https://github.com/karem505/egyptian-drug-database |
| Commit | `7d76aa2ac3474684f142aefa52452e1963eb0cc6` (24 Oct 2026) |
| SHA-256 of the original JSON | `351130d918cb901f38fda88b47a41b044f16a241cc51c34d30097e672518e645` |
| Records | 25,070 medicines registered in Egypt |
| Fields | `commercial_name_en`, `commercial_name_ar`, `scientific_name`, `manufacturer`, `drug_class`, `route`, `price_egp` |
| Licence | **CC0-1.0** (public-domain dedication): copy, change and redistribute without permission or attribution |

From the project's disclaimer: the data is for information and software development. Prices and availability change. The Arabic names are the compiler's own transliterations, not official registrations. **Check against the Egyptian Drug Authority and a pharmacist before clinical use.** In SafyaClinic the list only suggests names while a doctor types a prescription. The doctor can always type a name that isn't in it.

## Updating it

1. Download `data/egyptian-drugs.json` from a newer commit of the project.
2. Compress it: `python -c "import gzip,json,sys;d=json.load(open(sys.argv[1],encoding='utf-8'));open('egyptian-drugs.json.gz','wb').write(gzip.compress(json.dumps(d,ensure_ascii=False,separators=(',',':')).encode(),9,mtime=0))" egyptian-drugs.json`
3. Replace the file here, and update the commit, hash and count above.
