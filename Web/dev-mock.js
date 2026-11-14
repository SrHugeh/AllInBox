/* Design-time mock library. Only loaded when the page runs outside WebView2 (plain browser).
   Not shipped with the app (excluded from the build output). */
(function () {
  const GB = 1024 ** 3, MB = 1024 ** 2;
  const rows = [
    ['AbioticFactor', 'Steam', 6.7 * GB, 0], ['Aseprite', 'Steam', 31.6 * MB, 0], ['Brawlhalla', 'Steam', 1.5 * GB, 0],
    ['Chivalry2', 'Epic Games', 26.6 * GB, 23], ['CurseForge', 'Minecraft', 411 * MB, 0], ['Cyberpunk 2077', 'Steam', 82.4 * GB, 0],
    ['DayZ', 'Steam', 19.2 * MB, 0], ['Fortnite', 'Epic Games', 62.2 * GB, 3271], ['Green Hell', 'Steam', 10 * GB, 69],
    ['Half-Life 2', 'Steam', 5.4 * GB, 0], ['Half-Life 2 Update', 'Steam', 6.3 * GB, 0], ['League of Legends', 'Riot Games', 41.9 * GB, 0],
    ['Lethal Company', 'Steam', 1.1 * GB, 0], ['Mega Man X Legacy Collection', 'Steam', 2 * GB, 0], ['Minecraft Launcher', 'Minecraft', 6.2 * MB, 0],
    ['Modrinth App', 'Minecraft', 44.4 * MB, 0], ['Moonlighter', 'Steam', 1.2 * GB, 0], ['Necesse', 'Steam', 498 * MB, 0],
    ['nmrih2', 'Steam', 23.6 * GB, 0], ["No Man's Sky", 'Steam', 31.3 * GB, 0], ['Pogo Rogue', 'Steam', 202 * MB, 0],
    ['romestead', 'Steam', 1.6 * GB, 0], ['Soundpad', 'Steam', 16.9 * MB, 0], ['Stardew Valley', 'Steam', 659 * MB, 0],
    ['Teardown', 'Steam', 4.5 * GB, 0], ['Terraria', 'Steam', 766 * MB, 0], ['TFTClient', 'Riot Games', 6.3 * GB, 0],
    ['The Binding of Isaac Rebirth', 'Steam', 1.5 * GB, 1], ['The Farmer Was Replaced', 'Steam', 657 * 1024, 0], ['Tinkerlands', 'Steam', 337 * MB, 0],
    ['tModLoader', 'Steam', 197 * MB, 0], ['Trove', 'Steam', 1.2 * GB, 0], ['Valheim', 'Steam', 4.2 * GB, 0],
    ['VALORANT', 'Riot Games', 32.7 * GB, 0], ['VRChat', 'Steam', 1 * GB, 0], ['wallpaper engine', 'Steam', 788 * MB, 0], ['West of Dead', 'Steam', 618 * MB, 0]
  ];
  const fmtSize = (b) => b >= GB ? (b / GB).toFixed(1) + ' GB' : b >= MB ? (b / MB).toFixed(1) + ' MB' : Math.round(b / 1024) + ' KB';
  const fmtTime = (s) => s >= 3600 ? Math.floor(s / 3600) + 'h ' + Math.floor(s % 3600 / 60) + 'm' : s >= 60 ? Math.floor(s / 60) + 'm ' + (s % 60) + 's' : s + 's';
  const recentIdx = { 'Green Hell': 0, 'Fortnite': 2, 'The Binding of Isaac Rebirth': 45, 'Chivalry2': 70 };
  const lastLabel = (d) => d === 0 ? 'Hoje' : d < 7 ? d + ' dias atrás' : '05/09/2026';

  // Synthetic square icons so the "blurred backdrop + crisp icon" art style can be judged.
  const palette = ['#e5534b', '#2ea043', '#4a8fe7', '#d29922', '#a66df0', '#ef7aa7', '#22c1c3'];
  const svgIcon = (title, i) => 'data:image/svg+xml;utf8,' + encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect width="64" height="64" rx="12" fill="${palette[i % palette.length]}"/>` +
    `<text x="32" y="43" font-family="Segoe UI,Arial" font-size="32" font-weight="700" fill="#fff" text-anchor="middle">${title[0].toUpperCase()}</text></svg>`);

  const games = rows.map(([title, platform, size, play], i) => {
    const d = recentIdx[title];
    return {
      id: 'g' + i, title, platform, favorite: i % 11 === 3, hidden: false, running: false,
      icon: i % 5 === 4 ? '' : svgIcon(title, i), cover: '', category: 'Geral', initial: title[0].toUpperCase(),
      size, sizeText: fmtSize(size), playSeconds: play, playText: play ? fmtTime(play) : '',
      lastPlayed: d === undefined ? null : Date.now() - d * 86400000, lastText: d === undefined ? '' : lastLabel(d),
      added: Date.now() - i * 3600000
    };
  });
  // a couple of "not games" already hidden, to preview the Hidden tab
  games.find((g) => g.title === 'Soundpad').hidden = true;
  games.find((g) => g.title === 'Aseprite').hidden = true;

  window.__mockGames = games;
  const LIGHT = { bg: '#F5F3EE', panel: '#EBE8E1', card: '#FAF9F5', 'card-hover': '#EFECE6', 'card-selected': '#E1EAF4', 'card-border': '#D6D2C8',
    divider: '#DFDCD3', input: '#FFFFFF', badge: '#E2DFD7', accent: '#3A6EA5', 'accent-hover': '#4B80B8', 'accent-pressed': '#2C5887',
    'accent-subtle': '#DCE6F2', 'accent-fg': '#FAF9F5', text: '#1C2127', 'text-2': '#4D5766', 'text-3': '#768190' };

  games.forEach((g, i) => {
    g.exe = `C:\\Program Files (x86)\\Steam\\steamapps\\common\\${g.title}\\${g.title}.exe`;
    g.workDir = `C:\\Program Files (x86)\\Steam\\steamapps\\common\\${g.title}`;
    g.args = ''; g.notes = i === 3 ? 'Modo online precisa do Epic aberto.' : '';
  });

  const fakeScan = [
    ['Hades II', 'Steam', 14.2 * GB], ['Hollow Knight', 'Steam', 1.1 * GB], ['Celeste', 'Steam', 1.6 * 1024 * MB / 1024],
    ['Rocket League', 'Epic Games', 38.4 * GB], ['The Witcher 3', 'GOG', 52.9 * GB], ['Rayman Legends', 'Ubisoft', 9.8 * GB]
  ].map(([title, platform, size], i) => ({ title, platform, size, path: `C:\\Games\\${title}\\${title}.exe` }));

  // design-time shortcuts:
  //   ?view=stats|favorites|recent|hidden  ?mode=table  ?select=3  ?running=1  ?light=1  ?lang=en-US
  //   ?modal=add|edit|scan|scanresults|settings|confirm
  window.__mockInit = function () {
    const q = new URLSearchParams(location.search);
    if (q.get('noanim')) window.gsap = undefined; // screenshots: skip motion so frames are not caught mid-tween
    const settings = { language: 'auto', theme: q.get('light') ? 'light' : 'dark', bg: '#0F1319', surface: '#171D26', accent: '#4A8FE7', text: '#E6EDF3', trayClose: true, trayLaunch: false };
    onHost({ type: 'init', lang: q.get('lang') || 'pt-BR', games, theme: q.get('light') ? LIGHT : undefined, themeMode: settings.theme, systemLight: false, settings, maximized: !!q.get('max') });
    if (q.get('running')) { games[7].running = true; onHost({ type: 'games', games }); }
    if (q.get('mode')) { S.mode = q.get('mode'); }
    if (q.get('view')) { setView(q.get('view')); }
    if (q.get('select')) { setSelection(currentList().slice(0, +q.get('select')).map((g) => g.id)); }
    renderToolbar(true); renderContent();

    if (q.get('dd')) setTimeout(() => { const b = document.querySelectorAll('.dd-btn')[+q.get('dd') - 1]; if (b) b.click(); }, 120); // open the Nth dropdown
    if (q.get('hover')) { // headless browsers cannot hover: mirror the :hover look on one card
      const st = document.createElement('style');
      st.textContent = '.card.fh .art::after{opacity:1}.card.fh .play,.card.fh .fav-btn{opacity:1;transform:none}.card.fh{transform:translateY(-2px);border-color:var(--accent);box-shadow:var(--shadow)}';
      document.head.appendChild(st);
      const cards = document.querySelectorAll('.card');
      cards[+q.get('hover')] && cards[+q.get('hover')].classList.add('fh');
    }

    const modal = q.get('modal');
    if (modal === 'add') openGameForm(null);
    else if (modal === 'edit') openGameForm(games[3]);
    else if (modal === 'settings') openSettings();
    else if (modal === 'confirm') askRemove([games[1], games[2]]);
    else if (modal === 'scan' || modal === 'scanresults') {
      openScanModal();
      if (modal === 'scanresults') setTimeout(() => onHost({ type: 'scanResults', items: fakeScan }), 50);
    }
  };
})();
