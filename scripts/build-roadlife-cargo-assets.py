#!/usr/bin/env python3
import argparse, json, re, unicodedata
from pathlib import Path
from difflib import SequenceMatcher
from PIL import Image

def norm(v):
    s = unicodedata.normalize("NFD", str(v or ""))
    s = "".join(ch for ch in s if unicodedata.category(ch) != "Mn")
    return re.sub(r"[^a-z0-9]+", "", s.lower())

def extract_pt_terms(js_text):
    m = re.search(r"const\s+PT_TERMS\s*=\s*\{([\s\S]*?)\};\s*const\s+PT_DLC", js_text)
    if not m:
        return []
    body = m.group(1)
    pairs = []
    for a,b in re.findall(r"'((?:\\.|[^'])*)'\s*:\s*'((?:\\.|[^'])*)'", body):
        a = a.replace("\\'", "'").replace("\\\\", "\\")
        b = b.replace("\\'", "'").replace("\\\\", "\\")
        pairs.append((a,b))
    pairs.sort(key=lambda kv: len(kv[0]), reverse=True)
    return pairs

def translate_pt(name, pairs):
    out = str(name or "")
    for en, pt in pairs:
        out = re.sub(r"\b" + re.escape(en) + r"\b", lambda _: pt, out, flags=re.I)
    return out

def best_icon_for_name(name, defs):
    n = norm(name)
    if not n:
        return None
    exact = {}
    candidates = []
    for d in defs:
        icon = str(d.get("icon") or d.get("id") or "")
        for key in (d.get("id"), d.get("token"), d.get("icon")):
            k = norm(key)
            if not k:
                continue
            exact.setdefault(k, icon)
            candidates.append((k, icon))
    if n in exact:
        return exact[n]
    contained = [(len(k), icon) for k,icon in candidates if len(k) >= 5 and (k in n or n in k)]
    if contained:
        contained.sort(reverse=True)
        return contained[0][1]
    best = (0.0, None)
    for k,icon in candidates:
        score = SequenceMatcher(None, n, k).ratio()
        if score > best[0]:
            best = (score, icon)
    return best[1] if best[0] >= 0.72 else None

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    repo = Path(args.repo)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    generated = out / "generated"
    generated.mkdir(parents=True, exist_ok=True)

    defs_path = repo / "docs" / "assets" / "cargo" / "cargo-icon-defs.json"
    catalog_path = repo / "docs" / "ets2-official-cargos.json"
    js_path = repo / "docs" / "work-catalog.js"
    source_dir = repo / "docs" / "assets" / "cargo"

    data = json.loads(defs_path.read_text(encoding="utf-8"))
    defs = data.get("definitions") or []
    icons = data.get("icons") or {}
    sheets = data.get("sheets") or []
    tile_w, tile_h = data.get("tile") or [160,128]

    # Copy original metadata/sprites too, so the RoadLife folder remains self-contained.
    (out / "cargo-icon-defs.json").write_text(json.dumps(data, ensure_ascii=False, separators=(",",":")), encoding="utf-8")
    for sh in sheets:
        src = source_dir / sh["file"]
        if src.exists():
            (out / sh["file"]).write_bytes(src.read_bytes())

    opened = {}
    generated_count = 0
    for icon, pos in icons.items():
        si = int(pos.get("s", 0)); x = int(pos.get("x", 0)); y = int(pos.get("y", 0))
        if si < 0 or si >= len(sheets):
            continue
        file = sheets[si]["file"]
        src = source_dir / file
        if not src.exists():
            continue
        if file not in opened:
            opened[file] = Image.open(src).convert("RGBA")
        im = opened[file]
        left, top = x * tile_w, y * tile_h
        crop = im.crop((left, top, left + tile_w, top + tile_h))
        crop.save(generated / f"{icon}.png", "PNG", optimize=True)
        generated_count += 1

    aliases = {}
    for d in defs:
        icon = str(d.get("icon") or d.get("id") or "")
        if not icon:
            continue
        for key in (d.get("id"), d.get("token"), d.get("icon")):
            n = norm(key)
            if n:
                aliases[n] = icon

    pt_pairs = extract_pt_terms(js_path.read_text(encoding="utf-8"))
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    for rows in (catalog.get("categories") or {}).values():
        if not isinstance(rows, list):
            continue
        for row in rows:
            name = str((row or {}).get("name") or "").strip()
            if not name:
                continue
            icon = best_icon_for_name(name, defs)
            if not icon:
                continue
            aliases[norm(name)] = icon
            pt = translate_pt(name, pt_pairs)
            if pt:
                aliases[norm(pt)] = icon

    # Known PT telemetry aliases that are important and unambiguous.
    aliases.setdefault(norm("Tubos grandes"), "largetubes")
    aliases.setdefault(norm("Tubos de ferro"), "iron_pipes")
    aliases.setdefault(norm("Tubos metálicos"), "metal_pipes")
    aliases.setdefault(norm("Tubos plásticos"), "plast_pipes")
    aliases.setdefault(norm("Arroz"), "rice")
    aliases.setdefault(norm("Ração"), "pet_food")
    aliases.setdefault(norm("Gado vivo"), "live_cattle")
    aliases.setdefault(norm("Gado bovino vivo"), "live_cattle")
    aliases.setdefault(norm("Locomotiva CZ LOKO MUV 75"), "czl_muv75")

    (out / "cargo-name-map.json").write_text(
        json.dumps({"version":1,"aliases":aliases}, ensure_ascii=False, separators=(",",":")),
        encoding="utf-8"
    )

    for im in opened.values():
        try: im.close()
        except Exception: pass

    if generated_count < 300:
        raise SystemExit(f"Poucas miniaturas geradas: {generated_count}")
    if aliases.get(norm("Tubos grandes")) != "largetubes":
        raise SystemExit("Alias Tubos grandes não foi gerado corretamente")

    print(f"Cargo assets OK: {generated_count} PNGs, {len(aliases)} aliases")
    print("Tubos grandes ->", aliases.get(norm("Tubos grandes")))

if __name__ == "__main__":
    main()
