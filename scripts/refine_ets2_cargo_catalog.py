#!/usr/bin/env python3
import json
import urllib.request
from pathlib import Path

CATALOG = Path('docs/ets2-official-cargos.json')
VERSION_URL = 'https://raw.githubusercontent.com/AlexOQ/trucker/main/public/data/ets2/data-version.json'


def fetch_version():
    try:
        req = urllib.request.Request(VERSION_URL, headers={'User-Agent':'GAT-LOG/1.0'})
        with urllib.request.urlopen(req, timeout=30) as r:
            d = json.loads(r.read().decode('utf-8'))
        return str(d.get('game_version') or ''), str(d.get('refreshed_at') or '')
    except Exception:
        return '', ''


def main():
    d = json.loads(CATALOG.read_text(encoding='utf-8'))
    cats = d.get('categories') or {}
    removed = []
    for key, rows in list(cats.items()):
        kept = []
        for row in rows or []:
            dlc = str((row or {}).get('dlc') or '')
            if 'removed' in dlc.lower():
                removed.append(row)
            else:
                kept.append(row)
        cats[key] = kept

    version, refreshed = fetch_version()
    d['reference_game_version'] = version or d.get('reference_game_version','')
    d['reference_data_refreshed'] = refreshed or d.get('reference_data_refreshed','')
    d['removed_entries_excluded'] = len(removed)
    d['total_entries'] = sum(len(v or []) for v in cats.values())
    d['category_counts'] = {k: len(v or []) for k, v in cats.items()}
    d['source_note'] = 'Cargas transportáveis atuais do Truck Simulator Wiki. Entradas marcadas como removidas foram excluídas; versão de referência conferida no conjunto AlexOQ/trucker.'
    CATALOG.write_text(json.dumps(d, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Cargas atuais:', d['total_entries'])
    print('Removidas excluídas:', len(removed))
    print('Versão de referência:', d['reference_game_version'])


if __name__ == '__main__':
    main()
