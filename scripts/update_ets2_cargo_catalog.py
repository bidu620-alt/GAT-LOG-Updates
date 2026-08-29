#!/usr/bin/env python3
from __future__ import annotations

import datetime as dt
import html
import json
import re
import unicodedata
import urllib.request
from html.parser import HTMLParser
from pathlib import Path

WIKI_URL = "https://trucksimulator.wiki.gg/wiki/Cargo_types/Euro_Truck_Simulator_2"
DATA_VERSION_URL = "https://raw.githubusercontent.com/AlexOQ/trucker/main/public/data/ets2/data-version.json"
OUT = Path("docs/ets2-official-cargos.json")

CATEGORY_ORDER = [
    "tractor", "fuel", "food", "drinks", "timber", "container", "heavy_machine",
    "vehicles", "motorcycles", "chemicals", "construction", "steel", "paper",
    "electronics", "furniture", "glass", "pipes", "cables", "industrial", "mining",
    "grain", "rural", "dairy", "medical", "scrap", "road_machine", "boats",
    "aircraft", "refrigerated", "custom",
]

CATEGORY_TITLES = {
    "tractor": "Trator e máquinas agrícolas",
    "fuel": "Combustível",
    "food": "Alimentos",
    "drinks": "Bebidas",
    "timber": "Madeira e toras",
    "container": "Contêiner",
    "heavy_machine": "Máquinas pesadas",
    "vehicles": "Veículos",
    "motorcycles": "Motocicletas",
    "chemicals": "Produtos químicos",
    "construction": "Material de construção",
    "steel": "Aço e metais",
    "paper": "Papel e celulose",
    "electronics": "Eletrônicos",
    "furniture": "Móveis",
    "glass": "Vidro",
    "pipes": "Tubos",
    "cables": "Cabos e bobinas",
    "industrial": "Equipamento industrial",
    "mining": "Minério e carvão",
    "grain": "Grãos e cereais",
    "rural": "Animais e produtos rurais",
    "dairy": "Leite e laticínios",
    "medical": "Medicamentos e material médico",
    "scrap": "Sucata e recicláveis",
    "road_machine": "Máquinas rodoviárias",
    "boats": "Barcos e iates",
    "aircraft": "Aeronaves e helicópteros",
    "refrigerated": "Carga refrigerada",
    "custom": "Outras cargas oficiais / personalizada",
}


def norm(value: str) -> str:
    value = unicodedata.normalize("NFKD", value or "")
    value = "".join(ch for ch in value if not unicodedata.combining(ch))
    value = value.lower().replace("–", "-").replace("—", "-")
    value = re.sub(r"[^a-z0-9]+", " ", value)
    return re.sub(r"\s+", " ", value).strip()


def has(text: str, *needles: str) -> bool:
    return any(norm(x) in text for x in needles)


def classify(name: str) -> str:
    n = norm(name)

    # Refrigerados antes de alimentos para evitar duplicação de peixe/carne congelados.
    if has(n, "frozen", "chilled", "refrigerated", "ice cream", "fresh fish", "fresh meat", "fresh fruit"):
        return "refrigerated"
    if has(n, "service boat", "boat", "yacht", "sailboat", "catamaran", "motorboat"):
        return "boats"
    if has(n, "helicopter", "airplane", "aeroplane", "glider") or ("aircraft" in n and "tyre" not in n and "tire" not in n):
        return "aircraft"
    if has(n, "asphalt miller", "asphalt paver", "paver", "road roller", "road grader", "grader", "milling machine", "road compactor"):
        return "road_machine"
    if has(n, "agricultural", "tractor", "harvester", "combine harvester", "cultivator", "seeder", "seed drill", "plough", "plow", "baler", "mower", "sprayer", "farm machinery", "disc harrow", "forage harvester", "telehandler"):
        return "tractor"
    if has(n, "excavator", "bulldozer", "dozer", "wheel loader", "skid steer", "backhoe", "articulated hauler", "articulated dumper", "underground loader", "haul truck", "crawler", "rock bucket", "mobile crane", "heavy machinery"):
        return "heavy_machine"
    if has(n, "motorcycle", "motorbike", "scooter"):
        return "motorcycles"
    if has(n, "car ", "cars", "automobile", "vehicle", "van", "truck chassis", "bus", "buses", "pickup", "motorhome", "camper", "suv", "caravan"):
        return "vehicles"
    if has(n, "fuel", "diesel", "gasoline", "petrol", "kerosene", "lpg", "propane", "butane", "ethanol", "aviation fuel", "fuel oil"):
        return "fuel"
    if has(n, "beverage", "bottled water", "carbonated water", "water bottles", "juice", "soft drink", "soda", "beer", "wine", "cider", "tea", "coffee"):
        return "drinks"
    if has(n, "milk", "cheese", "yogurt", "yoghurt", "butter", "cream cheese", "dry milk", "dairy"):
        return "dairy"
    if has(n, "barley", "wheat", "corn", "maize", "rye", "oat", "sunflower", "soy", "soybean", "grain", "rice", "seed", "flour"):
        return "grain"
    if has(n, "livestock", "cattle", "sheep", "pig", "pigs", "hay", "straw", "wool", "live animals"):
        return "rural"
    if has(n, "apple", "almond", "beans", "beef", "carrot", "cauliflower", "caviar", "chicken", "meat", "pork", "tuna", "sardine", "salmon", "fish", "potato", "onion", "orange", "pear", "grape", "tomato", "sugar", "chocolate", "food", "groceries", "basil", "chewing gum", "olive", "pasta"):
        return "food"
    if has(n, "acid", "chemical", "acetylene", "arsenic", "ammonia", "chlorine", "solvent", "resin", "fertilizer", "boric", "sorbent", "carbon black", "brake fluid", "pesticide"):
        return "chemicals"
    if has(n, "medicine", "medical", "pharmaceutical", "vaccine", "hospital"):
        return "medical"
    if has(n, "coal", "ore", "bauxite", "mineral", "mining material"):
        return "mining"
    if has(n, "scrap", "waste", "garbage", "recycl", "used plastic"):
        return "scrap"
    if has(n, "logs", "timber", "lumber", "wood", "tree trunk", "wooden beam", "planks", "sawdust"):
        return "timber"
    if has(n, "container"):
        return "container"
    if has(n, "brick", "cement", "concrete", "roof tile", "sand", "gravel", "gypsum", "plaster", "marble", "granite", "construction staircase", "prefabricated", "chimney system", "building material"):
        return "construction"
    if has(n, "steel", "metal", "aluminium", "aluminum", "copper", "iron", "lead", "zinc", "ingot", "rebar", "coil", "profiles"):
        return "steel"
    if has(n, "paper", "pulp", "cardboard", "tissue"):
        return "paper"
    if has(n, "electronic", "computer", "server", "television", " tv", "mobile phone", "smartphone", "appliance", "high tech device"):
        return "electronics"
    if has(n, "furniture", "table", "chair", "sofa", "mattress", "cabinet"):
        return "furniture"
    if has(n, "glass"):
        return "glass"
    if has(n, "pipe", "tube", "pipeline", "backflow preventer"):
        return "pipes"
    if has(n, "cable reel", "cable", "wire reel", "wire spool"):
        return "cables"
    if has(n, "locomotive", "railcar", "boiler", "transformer", "generator", "compressor", "heat exchanger", "condenser", "forklift", "industrial", "machine parts", "machinery parts", "pressure tank", "reservoir tank", "silo", "wind turbine", "equipment", "huge tyres", "aircraft tyres", "tires", "tyres"):
        return "industrial"
    return "custom"


class CargoTableParser(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.tables = []
        self.table = None
        self.row = None
        self.cell = None
        self.cell_tag = None

    def handle_starttag(self, tag, attrs):
        if tag == "table":
            self.table = []
        elif self.table is not None and tag == "tr":
            self.row = []
        elif self.row is not None and tag in ("th", "td"):
            self.cell = []
            self.cell_tag = tag
        elif self.cell is not None and tag == "br":
            self.cell.append(" ")

    def handle_data(self, data):
        if self.cell is not None:
            self.cell.append(data)

    def handle_endtag(self, tag):
        if self.cell is not None and tag == self.cell_tag:
            text = re.sub(r"\s+", " ", html.unescape("".join(self.cell))).strip()
            self.row.append(text)
            self.cell = None
            self.cell_tag = None
        elif self.row is not None and tag == "tr":
            if self.row:
                self.table.append(self.row)
            self.row = None
        elif self.table is not None and tag == "table":
            if self.table:
                self.tables.append(self.table)
            self.table = None


def get_text(url: str) -> str:
    req = urllib.request.Request(url, headers={"User-Agent": "GAT-LOG/1.0 ETS2 cargo catalog (+GitHub Actions)"})
    with urllib.request.urlopen(req, timeout=45) as r:
        return r.read().decode("utf-8", errors="replace")


def scrape_wiki() -> list[dict]:
    parser = CargoTableParser()
    parser.feed(get_text(WIKI_URL))
    cargo_table = None
    for table in parser.tables:
        if not table:
            continue
        headers = [norm(x) for x in table[0]]
        if headers and headers[0] == "cargo" and any("dlc" in h or "patch" in h for h in headers[1:3]):
            cargo_table = table
            break
    if cargo_table is None:
        raise RuntimeError("Tabela de cargas do Truck Simulator Wiki não foi encontrada")

    rows = []
    seen = set()
    for row in cargo_table[1:]:
        if not row:
            continue
        name = re.sub(r"\[[0-9]+\]", "", row[0]).strip()
        if not name or norm(name) in ("cargo", "name"):
            continue
        dlc = row[1].strip() if len(row) > 1 else ""
        weight = row[2].strip() if len(row) > 2 else ""
        # Mantém variantes iguais quando são de DLCs diferentes.
        key = (norm(name), norm(dlc))
        if key in seen:
            continue
        seen.add(key)
        rows.append({"name": name, "dlc": dlc, "weight": weight, "category": classify(name)})
    if len(rows) < 300:
        raise RuntimeError(f"Catálogo incompleto: apenas {len(rows)} cargas encontradas")
    return rows


def game_version() -> dict:
    try:
        data = json.loads(get_text(DATA_VERSION_URL))
        return {
            "version": str(data.get("version") or ""),
            "last_refreshed": str(data.get("last_refreshed") or ""),
        }
    except Exception:
        return {"version": "", "last_refreshed": ""}


def main() -> None:
    rows = scrape_wiki()
    version = game_version()
    categories = {key: [] for key in CATEGORY_ORDER}
    for row in rows:
        categories[row["category"]].append({k: row[k] for k in ("name", "dlc", "weight")})
    for values in categories.values():
        values.sort(key=lambda x: (norm(x["name"]), norm(x["dlc"])))

    payload = {
        "game": "Euro Truck Simulator 2",
        "source": WIKI_URL,
        "source_note": "Lista de cargos transportáveis do Truck Simulator Wiki; versão instalada de referência obtida do conjunto de dados AlexOQ/trucker.",
        "reference_game_version": version["version"],
        "reference_data_refreshed": version["last_refreshed"],
        "generated_at": dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        "total_entries": len(rows),
        "category_titles": CATEGORY_TITLES,
        "category_counts": {key: len(categories[key]) for key in CATEGORY_ORDER},
        "categories": categories,
    }
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{len(rows)} cargas gravadas em {OUT}")
    print("Categorias:", ", ".join(f"{k}={len(v)}" for k, v in categories.items()))


if __name__ == "__main__":
    main()
