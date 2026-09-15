const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');
const puppeteer = require('puppeteer-core');

(async () => {
  const indexPath = process.argv[2];
  const outDir = process.argv[3] || path.join(process.cwd(), 'visual159');
  if (!indexPath || !fs.existsSync(indexPath)) throw new Error('index.html do GAT DASH nao encontrado');
  fs.mkdirSync(outDir, { recursive: true });

  const chromeCandidates = [
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe'
  ];
  const executablePath = chromeCandidates.find(fs.existsSync);
  if (!executablePath) throw new Error('Chrome/Edge nao encontrado no runner');

  const browser = await puppeteer.launch({
    executablePath,
    headless: true,
    args: ['--allow-file-access-from-files', '--disable-web-security', '--no-sandbox']
  });

  async function check(width, height, label) {
    const page = await browser.newPage();
    await page.setViewport({ width, height, deviceScaleFactor: 1 });
    await page.goto(pathToFileURL(path.resolve(indexPath)).href, { waitUntil: 'load' });
    await page.evaluate(() => {
      const login = document.getElementById('loginScreen');
      const dash = document.getElementById('dashScreen');
      const rotate = document.getElementById('rotateNotice');
      if (login) { login.classList.add('hidden'); login.style.display = 'none'; }
      if (dash) { dash.classList.remove('hidden'); dash.style.display = 'flex'; dash.style.visibility = 'visible'; }
      if (rotate) rotate.style.display = 'none';
    });
    await new Promise(r => setTimeout(r, 250));

    const boxes = await page.evaluate(() => {
      const rect = sel => {
        const el = document.querySelector(sel);
        if (!el) return null;
        const r = el.getBoundingClientRect();
        return { x:r.x, y:r.y, left:r.left, top:r.top, right:r.right, bottom:r.bottom, width:r.width, height:r.height };
      };
      return {
        vw: innerWidth, vh: innerHeight,
        grid: rect('.dashboard-grid'),
        route: rect('.route-panel'),
        center: rect('.center-cluster'),
        condition: rect('.condition-panel'),
        media: rect('.media-panel')
      };
    });

    const need = ['grid','route','center','condition','media'];
    for (const key of need) if (!boxes[key]) throw new Error(`${label}: elemento ${key} ausente`);
    const eps = 3;
    for (const key of ['route','center','condition','media']) {
      const b = boxes[key];
      if (b.width < 80 || b.height < 30) throw new Error(`${label}: ${key} pequeno demais ${JSON.stringify(b)}`);
      if (b.left < -eps || b.right > boxes.vw + eps) throw new Error(`${label}: ${key} saiu horizontalmente do viewport ${JSON.stringify(b)}`);
      if (b.top < -eps || b.bottom > boxes.vh + eps) throw new Error(`${label}: ${key} saiu verticalmente do viewport ${JSON.stringify(b)}`);
    }

    // Em desktop/janela normal, a estrutura deve ser a MESMA do maximizado:
    // rota a esquerda, centro no meio, radio a direita e condicao abaixo da rota.
    if (width >= 900) {
      if (!(boxes.route.right <= boxes.center.left + 8)) throw new Error(`${label}: rota nao ficou a esquerda do centro`);
      if (!(boxes.center.right <= boxes.media.left + 8)) throw new Error(`${label}: radio nao ficou a direita do centro`);
      if (Math.abs(boxes.route.top - boxes.media.top) > 8) throw new Error(`${label}: radio desceu para outra linha`);
      if (Math.abs(boxes.center.top - boxes.route.top) > 8) throw new Error(`${label}: centro desalinhado no topo`);
      if (!(boxes.condition.top >= boxes.route.bottom - 8)) throw new Error(`${label}: condicao nao ficou abaixo da rota`);
    }

    fs.writeFileSync(path.join(outDir, `${label}.json`), JSON.stringify(boxes, null, 2));
    await page.screenshot({ path: path.join(outDir, `${label}.png`), fullPage: false });
    await page.close();
  }

  // 1220x490 aproxima a area interna do GAT DASH na captura em janela normal.
  await check(1220, 490, 'janela-normal-1220x490');
  // 1600x700 representa a area interna quando maximizado em Full HD.
  await check(1600, 700, 'maximizado-1600x700');
  // Tambem valida uma largura proxima de monitor 1366x768 apos descontar o chrome do app.
  await check(1180, 520, 'monitor-1366-1180x520');

  await browser.close();
  console.log('GAT DASH 1.0.59: validacao visual/responsiva concluida com sucesso.');
})().catch(err => { console.error(err); process.exit(1); });
