(() => {
  'use strict';

  // Resolve from this shared script, including when called by nested event pages.
  const root = new URL('.', document.currentScript.src);
  const passUrl = new URL('passe/', root);

  function addPassLink() {
    const topbar = document.querySelector('.topbar');
    const nav = topbar?.querySelector('nav');
    if (!nav) return;

    if (!document.getElementById('gat-passe-nav-style')) {
      const style = document.createElement('style');
      style.id = 'gat-passe-nav-style';
      style.textContent = `
        .topbar.gat-passe-topbar{height:auto;min-height:76px;box-sizing:border-box;flex-wrap:wrap;gap:14px 20px;padding-top:12px;padding-bottom:12px}
        .topbar.gat-passe-topbar nav{display:flex!important;align-items:center;flex:1 1 600px;min-width:0;flex-wrap:wrap;gap:12px 18px}
        .topbar.gat-passe-topbar nav a{white-space:nowrap}
        .topbar.gat-passe-topbar nav .gat-passe-link{display:inline-flex!important;align-items:center;justify-content:center;gap:7px;flex:0 0 auto;padding:9px 13px;border:1px solid #b79a54;border-radius:9px;background:linear-gradient(135deg,#37332a,#211f1a);color:#ffe4a0!important;box-shadow:inset 0 1px 0 #ffffff0e,0 0 18px #d1ad4920;font-size:11px;font-weight:900;letter-spacing:.7px;line-height:1.2;text-decoration:none;transition:background .18s,border-color .18s,box-shadow .18s}
        .topbar.gat-passe-topbar nav .gat-passe-link::after{display:none!important}
        .topbar.gat-passe-topbar nav .gat-passe-link svg{width:16px;height:16px;flex:none}
        .topbar.gat-passe-topbar nav .gat-passe-link:hover{background:#45402f;border-color:#f3d387;box-shadow:0 0 20px #d1ad4930}
        .topbar.gat-passe-topbar nav .gat-passe-link:focus-visible{outline:2px solid #ffe4a0;outline-offset:4px}
        .topbar.gat-passe-topbar nav .gat-passe-link[aria-current="page"]{background:#dfbf73;color:#1a1915!important;border-color:#ffe4a0;box-shadow:0 0 18px #d1ad4935}
        @media(max-width:920px){
          .topbar.gat-passe-topbar{position:relative;gap:12px;min-height:65px}
          .topbar.gat-passe-topbar nav{order:10;flex:1 0 100%;width:100%;gap:10px 16px;padding:3px 0}
          .topbar.gat-passe-topbar nav a{font-size:11px}
          .topbar.gat-passe-topbar .eco-wallet-nav{margin-left:auto;max-width:100%;flex-wrap:wrap;gap:6px}
          .topbar.gat-passe-topbar .account-btn{margin-left:0}
        }
        @media(prefers-reduced-motion:reduce){.topbar.gat-passe-topbar nav .gat-passe-link{transition:none}}
      `;
      document.head.append(style);
    }

    topbar.classList.add('gat-passe-topbar');
    let link = nav.querySelector('[data-gat-passe-link]');
    if (!link) {
      link = document.createElement('a');
      link.className = 'gat-passe-link';
      link.dataset.gatPasseLink = '1';
      link.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true" focusable="false"><path d="M4 5h16a1 1 0 0 1 1 1v3a3 3 0 0 0 0 6v3a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-3a3 3 0 0 0 0-6V6a1 1 0 0 1 1-1Z"/><path d="M15 6v2m0 3v2m0 3v2"/></svg><span>PASSE</span>';
      const eventLink = nav.querySelector('.event-month-link,a[href$="eventos/"],a[href="./"]');
      if (eventLink) eventLink.after(link);
      else nav.append(link);
    }
    link.href = passUrl.href;
    link.setAttribute('aria-label', 'Passe mensal BiduLog');
    const currentPath = location.pathname.replace(/\/+$/, '') + '/';
    if (currentPath === passUrl.pathname || currentPath.startsWith(passUrl.pathname)) {
      link.setAttribute('aria-current', 'page');
    } else {
      link.removeAttribute('aria-current');
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', addPassLink, { once: true });
  } else {
    addPassLink();
  }
})();
