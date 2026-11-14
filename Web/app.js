/* ==========================================================================
   All In Box - front-end core
   Talks to the C# host through window.chrome.webview (WebView2).
   Outside the host (plain browser) boot.js loads a mock library for design work.
   Modals (add/edit game, scan, settings, confirm) live in modals.js.
   ========================================================================== */
'use strict';

const host = window.chrome && window.chrome.webview;
const send = (type, data = {}) => {
  if (host) host.postMessage({ type, ...data });
  else console.log('[host <-]', type, data);
};

/* ---------- i18n ---------- */
const STR = {
  'pt-BR': {
    library: 'Biblioteca', all: 'Todos os jogos', favorites: 'Favoritos', recent: 'Recentes', platforms: 'Plataformas',
    insights: 'Análises', stats: 'Estatísticas', hidden: 'Ocultos', search: 'Buscar jogos…', sort: 'Ordenar',
    sName: 'Nome (A–Z)', sNameD: 'Nome (Z–A)', sRecent: 'Jogados recentemente', sPlayed: 'Mais jogados', sSizeD: 'Maiores primeiro',
    sSizeA: 'Menores primeiro', sAdded: 'Adicionados recentemente', sFav: 'Favoritos primeiro', sPlat: 'Plataforma',
    grid: 'Grade', table: 'Tabela', add: 'Adicionar jogo', scan: 'Escanear', optimize: 'Otimizar biblioteca',
    optimizeTip: 'Remove duplicados, corrige ícones, calcula tamanhos e oculta itens que não são jogos',
    selected: 'selecionado(s)', selectAll: 'Selecionar todos', hide: 'Ocultar', restore: 'Restaurar', remove: 'Remover', clear: 'Limpar seleção',
    play: 'Jogar', playing: 'Em jogo', unfav: 'Remover dos favoritos', fav: 'Favoritar', folder: 'Abrir pasta do jogo', edit: 'Editar informações',
    hideOne: 'Ocultar da biblioteca', restoreOne: 'Restaurar na biblioteca', removeOne: 'Remover da biblioteca',
    cont: 'Continuar jogando', games: 'jogos', game: 'jogo',
    emptyT: 'Sua biblioteca está vazia', emptyS: 'Escaneie o PC para encontrar seus jogos ou adicione um manualmente. Você também pode arrastar executáveis para cá.',
    noRes: 'Nenhum jogo encontrado', noResS: 'Tente outro termo de busca ou outro filtro.',
    emptyFav: 'Nenhum favorito ainda', emptyFavS: 'Clique na estrela de um jogo para marcá-lo.',
    emptyRec: 'Nada jogado ainda', emptyRecS: 'Os jogos que você abrir pelo All In Box aparecem aqui.',
    emptyHid: 'Nenhum item oculto', emptyHidS: 'Itens ocultados ficam aqui e podem ser restaurados.',
    cJogo: 'Jogo', cPlat: 'Plataforma', cSize: 'Tamanho', cTime: 'Tempo jogado', cLast: 'Última sessão',
    kDisk: 'Espaço em disco', kDiskS: 'Peso total calculado', kTime: 'Tempo total', kTimeS: 'Tempo somado', kGames: 'Jogos', kGamesS: 'Na biblioteca',
    kStarted: 'Iniciados', kStartedS: 'Já jogados', kBacklog: 'Backlog', kBacklogS: 'Nunca jogados', kFav: 'Favoritos', kFavS: 'Marcados com estrela',
    pPlayed: 'Mais jogados', pHeavy: 'Mais pesados', pPlat: 'Por plataforma', pBacklog: 'Backlog (nunca jogados)',
    noPlayed: 'Nenhum jogo com tempo registrado ainda.', allStarted: 'Todos os jogos já foram iniciados.', more: 'mais',
    dropT: 'Solte para adicionar à biblioteca', dropS: 'Executáveis (.exe), atalhos (.lnk) ou pastas. Ícones e tamanhos são extraídos automaticamente.',
    tagline: 'Todos os seus jogos em um só lugar', liveNow: 'Em jogo', settings: 'Configurações', themeLbl: 'Tema',
    thAuto: 'Auto', thDark: 'Escuro', thLight: 'Claro', thCustom: 'Personalizado',
    tipTray: 'Ocultar na bandeja', tipMin: 'Minimizar', tipMax: 'Maximizar', tipRestore: 'Restaurar', tipClose: 'Fechar', tipTheme: 'Alternar tema'
  },
  'en-US': {
    library: 'Library', all: 'All games', favorites: 'Favorites', recent: 'Recent', platforms: 'Platforms',
    insights: 'Insights', stats: 'Statistics', hidden: 'Hidden', search: 'Search games…', sort: 'Sort',
    sName: 'Name (A–Z)', sNameD: 'Name (Z–A)', sRecent: 'Recently played', sPlayed: 'Most played', sSizeD: 'Largest first',
    sSizeA: 'Smallest first', sAdded: 'Recently added', sFav: 'Favorites first', sPlat: 'Platform',
    grid: 'Grid', table: 'Table', add: 'Add game', scan: 'Scan', optimize: 'Optimize library',
    optimizeTip: 'Removes duplicates, fixes icons, computes sizes and hides items that are not games',
    selected: 'selected', selectAll: 'Select all', hide: 'Hide', restore: 'Restore', remove: 'Remove', clear: 'Clear selection',
    play: 'Play', playing: 'Playing', unfav: 'Remove from favorites', fav: 'Add to favorites', folder: 'Open game folder', edit: 'Edit details',
    hideOne: 'Hide from library', restoreOne: 'Restore to library', removeOne: 'Remove from library',
    cont: 'Continue playing', games: 'games', game: 'game',
    emptyT: 'Your library is empty', emptyS: 'Scan this PC to find your games or add one manually. You can also drag executables here.',
    noRes: 'No games found', noResS: 'Try a different search or filter.',
    emptyFav: 'No favorites yet', emptyFavS: 'Click the star on a game to mark it.',
    emptyRec: 'Nothing played yet', emptyRecS: 'Games you launch from All In Box show up here.',
    emptyHid: 'No hidden items', emptyHidS: 'Hidden items stay here and can be restored.',
    cJogo: 'Game', cPlat: 'Platform', cSize: 'Size', cTime: 'Play time', cLast: 'Last session',
    kDisk: 'Disk space', kDiskS: 'Total size', kTime: 'Total time', kTimeS: 'Time played', kGames: 'Games', kGamesS: 'In library',
    kStarted: 'Started', kStartedS: 'Already played', kBacklog: 'Backlog', kBacklogS: 'Never played', kFav: 'Favorites', kFavS: 'Starred',
    pPlayed: 'Most played', pHeavy: 'Heaviest', pPlat: 'By platform', pBacklog: 'Backlog (never played)',
    noPlayed: 'No play time recorded yet.', allStarted: 'Every game has been started.', more: 'more',
    dropT: 'Drop to add to your library', dropS: 'Executables (.exe), shortcuts (.lnk) or folders. Icons and sizes are extracted automatically.',
    tagline: 'All your games in one place', liveNow: 'Playing', settings: 'Settings', themeLbl: 'Theme',
    thAuto: 'Auto', thDark: 'Dark', thLight: 'Light', thCustom: 'Custom',
    tipTray: 'Hide to tray', tipMin: 'Minimize', tipMax: 'Maximize', tipRestore: 'Restore', tipClose: 'Close', tipTheme: 'Switch theme'
  },
  'es-ES': {
    library: 'Biblioteca', all: 'Todos los juegos', favorites: 'Favoritos', recent: 'Recientes', platforms: 'Plataformas',
    insights: 'Análisis', stats: 'Estadísticas', hidden: 'Ocultos', search: 'Buscar juegos…', sort: 'Ordenar',
    sName: 'Nombre (A–Z)', sNameD: 'Nombre (Z–A)', sRecent: 'Jugados recientemente', sPlayed: 'Más jugados', sSizeD: 'Más grandes primero',
    sSizeA: 'Más pequeños primero', sAdded: 'Añadidos recientemente', sFav: 'Favoritos primero', sPlat: 'Plataforma',
    grid: 'Cuadrícula', table: 'Tabla', add: 'Añadir juego', scan: 'Escanear', optimize: 'Optimizar biblioteca',
    optimizeTip: 'Elimina duplicados, corrige iconos, calcula tamaños y oculta elementos que no son juegos',
    selected: 'seleccionado(s)', selectAll: 'Seleccionar todo', hide: 'Ocultar', restore: 'Restaurar', remove: 'Quitar', clear: 'Borrar selección',
    play: 'Jugar', playing: 'Jugando', unfav: 'Quitar de favoritos', fav: 'Marcar favorito', folder: 'Abrir carpeta del juego', edit: 'Editar información',
    hideOne: 'Ocultar de la biblioteca', restoreOne: 'Restaurar en la biblioteca', removeOne: 'Quitar de la biblioteca',
    cont: 'Seguir jugando', games: 'juegos', game: 'juego',
    emptyT: 'Tu biblioteca está vacía', emptyS: 'Escanea el PC para encontrar tus juegos o añade uno manualmente. También puedes arrastrar ejecutables aquí.',
    noRes: 'No se encontraron juegos', noResS: 'Prueba con otra búsqueda u otro filtro.',
    emptyFav: 'Aún no hay favoritos', emptyFavS: 'Haz clic en la estrella de un juego para marcarlo.',
    emptyRec: 'Aún no has jugado nada', emptyRecS: 'Los juegos que abras desde All In Box aparecen aquí.',
    emptyHid: 'No hay elementos ocultos', emptyHidS: 'Los elementos ocultos quedan aquí y se pueden restaurar.',
    cJogo: 'Juego', cPlat: 'Plataforma', cSize: 'Tamaño', cTime: 'Tiempo jugado', cLast: 'Última sesión',
    kDisk: 'Espacio en disco', kDiskS: 'Tamaño total', kTime: 'Tiempo total', kTimeS: 'Tiempo sumado', kGames: 'Juegos', kGamesS: 'En la biblioteca',
    kStarted: 'Iniciados', kStartedS: 'Ya jugados', kBacklog: 'Pendientes', kBacklogS: 'Nunca jugados', kFav: 'Favoritos', kFavS: 'Con estrella',
    pPlayed: 'Más jugados', pHeavy: 'Más pesados', pPlat: 'Por plataforma', pBacklog: 'Pendientes (nunca jugados)',
    noPlayed: 'Aún no hay tiempo de juego registrado.', allStarted: 'Todos los juegos ya se han iniciado.', more: 'más',
    dropT: 'Suelta para añadir a la biblioteca', dropS: 'Ejecutables (.exe), accesos directos (.lnk) o carpetas. Iconos y tamaños se extraen automáticamente.',
    tagline: 'Todos tus juegos en un solo lugar', liveNow: 'Jugando', settings: 'Ajustes', themeLbl: 'Tema',
    thAuto: 'Auto', thDark: 'Oscuro', thLight: 'Claro', thCustom: 'Personalizado',
    tipTray: 'Ocultar en la bandeja', tipMin: 'Minimizar', tipMax: 'Maximizar', tipRestore: 'Restaurar', tipClose: 'Cerrar', tipTheme: 'Cambiar tema'
  },
  'ja-JP': {
    library: 'ライブラリ', all: 'すべてのゲーム', favorites: 'お気に入り', recent: '最近', platforms: 'プラットフォーム',
    insights: '分析', stats: '統計', hidden: '非表示', search: 'ゲームを検索…', sort: '並び替え',
    sName: '名前 (A–Z)', sNameD: '名前 (Z–A)', sRecent: '最近プレイ', sPlayed: 'プレイ時間順', sSizeD: 'サイズが大きい順',
    sSizeA: 'サイズが小さい順', sAdded: '追加日順', sFav: 'お気に入り優先', sPlat: 'プラットフォーム',
    grid: 'グリッド', table: 'テーブル', add: 'ゲームを追加', scan: 'スキャン', optimize: 'ライブラリを最適化',
    optimizeTip: '重複を削除し、アイコンを修正し、サイズを計算し、ゲームではない項目を非表示にします',
    selected: '件選択', selectAll: 'すべて選択', hide: '非表示', restore: '復元', remove: '削除', clear: '選択を解除',
    play: 'プレイ', playing: 'プレイ中', unfav: 'お気に入りから外す', fav: 'お気に入りに追加', folder: 'ゲームのフォルダを開く', edit: '情報を編集',
    hideOne: 'ライブラリから非表示', restoreOne: 'ライブラリに復元', removeOne: 'ライブラリから削除',
    cont: '続きをプレイ', games: '本', game: '本',
    emptyT: 'ライブラリは空です', emptyS: 'PCをスキャンしてゲームを探すか、手動で追加してください。実行ファイルをここにドラッグすることもできます。',
    noRes: 'ゲームが見つかりません', noResS: '別の検索語またはフィルターをお試しください。',
    emptyFav: 'お気に入りはまだありません', emptyFavS: 'ゲームの星をクリックして登録します。',
    emptyRec: 'まだプレイしていません', emptyRecS: 'All In Box から起動したゲームがここに表示されます。',
    emptyHid: '非表示の項目はありません', emptyHidS: '非表示にした項目はここに残り、復元できます。',
    cJogo: 'ゲーム', cPlat: 'プラットフォーム', cSize: 'サイズ', cTime: 'プレイ時間', cLast: '最終プレイ',
    kDisk: 'ディスク容量', kDiskS: '合計サイズ', kTime: '合計時間', kTimeS: '合計プレイ時間', kGames: 'ゲーム', kGamesS: 'ライブラリ内',
    kStarted: '起動済み', kStartedS: 'プレイ済み', kBacklog: '積みゲー', kBacklogS: '未プレイ', kFav: 'お気に入り', kFavS: 'スター付き',
    pPlayed: 'プレイ時間が長い', pHeavy: '容量が大きい', pPlat: 'プラットフォーム別', pBacklog: '積みゲー (未プレイ)',
    noPlayed: 'プレイ時間の記録はまだありません。', allStarted: 'すべてのゲームを起動済みです。', more: '件',
    dropT: 'ドロップしてライブラリに追加', dropS: '実行ファイル (.exe)、ショートカット (.lnk)、フォルダ。アイコンとサイズは自動で取得されます。',
    tagline: 'すべてのゲームを一か所に', liveNow: 'プレイ中', settings: '設定', themeLbl: 'テーマ',
    thAuto: '自動', thDark: 'ダーク', thLight: 'ライト', thCustom: 'カスタム',
    tipTray: 'トレイに隠す', tipMin: '最小化', tipMax: '最大化', tipRestore: '元に戻す', tipClose: '閉じる', tipTheme: 'テーマを切り替え'
  }
};

/* ---------- state ---------- */
const S = {
  lang: 'pt-BR',
  games: [],
  view: 'all',          // all | favorites | recent | hidden | stats | platform
  platform: null,
  search: '',
  sort: 'name-asc',
  mode: 'grid',         // grid | table
  selected: new Set(),
  busy: false,
  lastClicked: null,
  maximized: false,
  themeMode: 'auto',
  systemLight: false,
  settings: { language: 'auto', theme: 'auto', bg: '#0F1319', surface: '#171D26', accent: '#4A8FE7', text: '#E6EDF3', trayClose: true, trayLaunch: false }
};
try {
  S.sort = localStorage.getItem('aib.sort') || S.sort;
  S.mode = localStorage.getItem('aib.mode') || S.mode;
} catch { /* storage can be unavailable */ }

const t = (k) => (STR[S.lang] && STR[S.lang][k]) || STR['en-US'][k] || k;
const $ = (id) => document.getElementById(id);
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

/* ---------- icons (Lucide, bundled in vendor/lucide.js) ---------- */
const ic = (name, cls = '') => `<svg class="ic ${cls}" viewBox="0 0 24 24" aria-hidden="true">${(window.LUCIDE && window.LUCIDE[name]) || ''}</svg>`;

/* ---------- motion (GSAP, optional) ---------- */
const REDUCE_MOTION = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
const canAnim = () => !!window.gsap && !REDUCE_MOTION;

/* ---------- platforms ---------- */
const PLATFORM_COLORS = {
  'Steam': '#5B9BD5', 'Epic Games': '#C9CED6', 'GOG': '#A66DF0', 'Ubisoft': '#3B82F6',
  'Riot Games': '#EF4444', 'Minecraft': '#4ADE80', 'Emuladores': '#F59E0B', 'EA App': '#FF6B4A', 'PC / Outros': '#94A3B8'
};
function normPlatform(p) {
  const v = (p || '').trim();
  if (/^gog/i.test(v)) return 'GOG';
  if (/^emulador/i.test(v)) return 'Emuladores';
  return v || 'PC / Outros';
}
const platColor = (p) => PLATFORM_COLORS[normPlatform(p)] || 'var(--accent)';

/* ---------- helpers ---------- */
function hue(s) {
  let h = 0;
  for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) >>> 0;
  return h % 360;
}
function fmtSize(b) {
  if (!b || b <= 0) return '--';
  const KB = 1024, MB = KB * 1024, GB = MB * 1024, TB = GB * 1024;
  if (b >= TB) return (b / TB).toFixed(2).replace(/\.?0+$/, '') + ' TB';
  if (b >= GB) return (b / GB).toFixed(1) + ' GB';
  if (b >= MB) return (b / MB).toFixed(1).replace(/\.0$/, '') + ' MB';
  return Math.round(b / KB) + ' KB';
}
function fmtTime(sec) {
  if (!sec || sec <= 0) return '0m';
  const d = Math.floor(sec / 86400), h = Math.floor((sec % 86400) / 3600), m = Math.floor((sec % 3600) / 60), s = Math.floor(sec % 60);
  if (d) return h ? `${d}d ${h}h` : `${d}d`;
  if (h) return m ? `${h}h ${m}m` : `${h}h`;
  if (m) return s ? `${m}m ${s}s` : `${m}m`;
  return `${s}s`;
}
const cmpStr = (a, b) => a.localeCompare(b, undefined, { sensitivity: 'base', numeric: true });
const SORTERS = {
  'name-asc': (a, b) => cmpStr(a.title, b.title),
  'name-desc': (a, b) => cmpStr(b.title, a.title),
  'recent': (a, b) => (b.lastPlayed || 0) - (a.lastPlayed || 0) || cmpStr(a.title, b.title),
  'played': (a, b) => b.playSeconds - a.playSeconds || cmpStr(a.title, b.title),
  'size-desc': (a, b) => b.size - a.size || cmpStr(a.title, b.title),
  'size-asc': (a, b) => a.size - b.size || cmpStr(a.title, b.title),
  'added': (a, b) => b.added - a.added || cmpStr(a.title, b.title),
  'fav': (a, b) => Number(b.favorite) - Number(a.favorite) || cmpStr(a.title, b.title),
  'platform': (a, b) => cmpStr(normPlatform(a.platform), normPlatform(b.platform)) || cmpStr(a.title, b.title)
};
const SORT_KEYS = [['name-asc', 'sName'], ['name-desc', 'sNameD'], ['recent', 'sRecent'], ['played', 'sPlayed'], ['size-desc', 'sSizeD'],
  ['size-asc', 'sSizeA'], ['added', 'sAdded'], ['fav', 'sFav'], ['platform', 'sPlat']];

const visibleGames = () => S.games.filter((g) => !g.hidden);
function currentList() {
  const q = S.search.trim().toLowerCase();
  let list = S.view === 'hidden' ? S.games.filter((g) => g.hidden) : visibleGames();

  if (S.view === 'favorites') list = list.filter((g) => g.favorite);
  else if (S.view === 'recent') list = list.filter((g) => g.lastPlayed);
  else if (S.view === 'platform') list = list.filter((g) => normPlatform(g.platform) === S.platform);

  if (q) list = list.filter((g) => g.title.toLowerCase().includes(q) || normPlatform(g.platform).toLowerCase().includes(q) || (g.category || '').toLowerCase().includes(q));

  const sorter = SORTERS[S.view === 'recent' ? 'recent' : S.sort] || SORTERS['name-asc'];
  return list.slice().sort(sorter);
}
const byId = (id) => S.games.find((g) => g.id === id);

/* ---------- top bar ---------- */
function themeLabel() {
  const names = { dark: t('thDark'), light: t('thLight'), custom: t('thCustom') };
  const mode = S.themeMode;
  const name = mode === 'auto' ? `${t('thAuto')} (${S.systemLight ? t('thLight') : t('thDark')})` : (names[mode] || t('thAuto'));
  return `${t('themeLbl')}: ${name}`;
}

function renderTopbar() {
  const vis = visibleGames();
  const disk = vis.reduce((s, g) => s + (g.size || 0), 0);
  const time = vis.reduce((s, g) => s + (g.playSeconds || 0), 0);
  const running = vis.filter((g) => g.running);
  const themeIcon = { auto: 'monitor', dark: 'moon', light: 'sun', custom: 'palette' }[S.themeMode] || 'monitor';

  const center = running.length
    ? `<span class="live-banner"><span>${esc(t('liveNow'))}: ${esc(running.map((g) => g.title).join(', '))}</span></span>`
    : esc(t('tagline'));

  $('topbar').innerHTML =
    `<div class="brand"><img src="assets/logo.png" alt=""><span>ALL IN BOX</span><span class="tag">MANAGER</span></div>` +
    `<div class="stats-strip">` +
    `<span class="pill" title="${esc(t('games'))}">${ic('pad')}<b>${vis.length}</b></span>` +
    `<span class="pill" title="${esc(t('kDisk'))}">${ic('disk')}<b>${disk > 0 ? fmtSize(disk) : '0 MB'}</b></span>` +
    `<span class="pill" title="${esc(t('kTime'))}">${ic('clock')}<b>${time > 0 ? fmtTime(time) : '0m'}</b></span></div>` +
    `<div class="center">${center}</div>` +
    `<button class="tb-btn" data-act="theme" title="${esc(t('tipTheme'))}">${ic(themeIcon)}<span>${esc(themeLabel())}</span></button>` +
    `<button class="tb-btn" data-act="settings" title="${esc(t('settings'))}">${ic('settings')}<span>${esc(t('settings'))}</span></button>` +
    `<div class="winctl">` +
    `<button data-win="tray" title="${esc(t('tipTray'))}">${ic('tray')}</button>` +
    `<button data-win="minimize" title="${esc(t('tipMin'))}">${ic('minus')}</button>` +
    `<button data-win="maximize" title="${esc(S.maximized ? t('tipRestore') : t('tipMax'))}">${ic(S.maximized ? 'restore' : 'square')}</button>` +
    `<button data-win="close" class="close" title="${esc(t('tipClose'))}">${ic('x')}</button></div>`;
}

/* ---------- rendering ---------- */
function artHtml(g, extra = '', overlay = '') {
  const h = hue(g.title);
  let inner;
  if (g.cover) {
    inner = `<img class="cover" src="${g.cover}" alt="" loading="lazy">`;
  } else if (g.icon) {
    inner = `<div class="bg" style="background-image:url('${g.icon}')"></div>` +
      `<img class="icon" src="${g.icon}" alt="" loading="lazy" onerror="this.hidden=true;this.nextElementSibling.hidden=false">` +
      `<span class="letter" hidden>${esc(g.initial)}</span>`;
  } else {
    inner = `<span class="letter">${esc(g.initial)}</span>`;
  }
  return `<div class="art ${extra}" style="--h:${h}">${inner}${overlay}</div>`;
}

function renderSidebar() {
  const vis = visibleGames();
  const hiddenCount = S.games.length - vis.length;
  const counts = {};
  vis.forEach((g) => { const p = normPlatform(g.platform); counts[p] = (counts[p] || 0) + 1; });
  const plats = Object.entries(counts).sort((a, b) => b[1] - a[1] || cmpStr(a[0], b[0]));

  const item = (key, icon, label, count, active) =>
    `<button class="nav-item ${active ? 'active' : ''}" data-nav="${key}" title="${esc(label)}">${icon}<span class="label">${esc(label)}</span>${count == null ? '' : `<span class="count">${count}</span>`}</button>`;

  let html = `<div><div class="nav-title">${t('library')}</div>` +
    item('all', ic('pad'), t('all'), vis.length, S.view === 'all') +
    item('favorites', ic('star'), t('favorites'), vis.filter((g) => g.favorite).length, S.view === 'favorites') +
    item('recent', ic('clock'), t('recent'), vis.filter((g) => g.lastPlayed).length, S.view === 'recent') +
    (hiddenCount || S.view === 'hidden' ? item('hidden', ic('eyeoff'), t('hidden'), hiddenCount, S.view === 'hidden') : '') +
    '</div>';

  if (plats.length) {
    html += `<div><div class="nav-title">${t('platforms')}</div>` +
      plats.map(([p, n]) => item('plat:' + p, `<span class="dot" style="--dot:${platColor(p)}"></span>`, p, n, S.view === 'platform' && S.platform === p)).join('') +
      '</div>';
  }

  html += `<div><div class="nav-title">${t('insights')}</div>` + item('stats', ic('chart'), t('stats'), null, S.view === 'stats') + '</div>';
  $('sidebar').innerHTML = html;
}

let toolbarSig = '';
function renderToolbar(force = false) {
  const selecting = S.selected.size > 0;
  const sig = [S.view, S.mode, S.sort, S.lang, selecting, selecting ? S.selected.size : '', S.busy].join('|');
  if (!force && sig === toolbarSig) return;
  toolbarSig = sig;

  const tb = $('toolbar');
  ddClose();
  tb.classList.toggle('selecting', selecting);

  if (selecting) {
    const inHidden = S.view === 'hidden';
    tb.innerHTML =
      `<span class="sel-count">${S.selected.size}</span><span>${t('selected')}</span>` +
      `<button class="btn" data-act="selall">${ic('check')}${t('selectAll')}</button><span class="spacer"></span>` +
      `<button class="btn" data-act="hidesel">${ic(inHidden ? 'eye' : 'eyeoff')}${inHidden ? t('restore') : t('hide')}</button>` +
      `<button class="btn danger" data-act="removesel">${ic('trash')}${t('remove')}</button>` +
      `<button class="btn icon" data-act="clearsel" title="${esc(t('clear'))}">${ic('x')}</button>`;
    return;
  }

  const hadFocus = document.activeElement && document.activeElement.id === 'q';
  const lib = S.view !== 'stats';
  const actions =
    `<span class="spacer"></span>` +
    `<button class="btn" data-act="scan">${ic('scan')}${t('scan')}</button>` +
    `<button class="btn icon ${S.busy ? 'busy' : ''}" data-act="optimize" title="${esc(t('optimizeTip'))}">${ic(S.busy ? 'spinner' : 'wand')}</button>` +
    `<button class="btn primary" data-act="add">${ic('plus')}${t('add')}</button>`;

  tb.innerHTML = lib
    ? `<label class="search">${ic('search')}<input id="q" type="search" placeholder="${esc(t('search'))}" value="${esc(S.search)}" autocomplete="off" spellcheck="false"></label>` +
      `<select class="select" id="sort" title="${esc(t('sort'))}" ${S.view === 'recent' ? 'disabled' : ''}>${SORT_KEYS.map(([k, l]) => `<option value="${k}" ${k === S.sort ? 'selected' : ''}>${esc(t(l))}</option>`).join('')}</select>` +
      `<div class="seg"><button data-mode="grid" class="${S.mode === 'grid' ? 'on' : ''}" title="${esc(t('grid'))}">${ic('grid')}</button><button data-mode="table" class="${S.mode === 'table' ? 'on' : ''}" title="${esc(t('table'))}">${ic('list')}</button></div>` + actions
    : `<strong style="font-size:15px">${t('stats')}</strong>` + actions;

  enhanceSelects(tb);
  if (hadFocus) { const q = $('q'); if (q) { q.focus(); q.setSelectionRange(q.value.length, q.value.length); } }
}

function playBtn(g) {
  return g.running
    ? `<span class="live">${t('playing')}</span>`
    : `<button class="play" data-act="play" title="${esc(t('play'))}">${ic('play')}<span>${t('play')}</span></button>`;
}

function cardHtml(g) {
  const sel = S.selected.has(g.id);
  const meta =
    (g.size > 0 ? `<span>${ic('disk')}${esc(g.sizeText)}</span>` : '') +
    (g.playSeconds > 0 ? `<span>${ic('clock')}${esc(g.playText)}</span>` : '');
  // Overlays live inside the art tile so they position against it, not against the whole card
  const overlay =
    `<span class="chip"><i class="dot" style="--dot:${platColor(g.platform)}"></i><span class="t">${esc(normPlatform(g.platform))}</span></span>` +
    `<button class="fav-btn ${g.favorite ? 'on' : ''}" data-act="fav" title="${esc(g.favorite ? t('unfav') : t('fav'))}">${ic('star')}</button>` +
    `<span class="check" data-act="check">${ic('check')}</span>` +
    playBtn(g);
  return `<article class="card ${sel ? 'selected' : ''} ${g.running ? 'running' : ''}" data-id="${esc(g.id)}">` +
    artHtml(g, '', overlay) +
    `<div class="card-body"><div class="title" title="${esc(g.title)}">${esc(g.title)}</div><div class="meta">${meta || '&nbsp;'}</div></div></article>`;
}

function rowHtml(g) {
  const sel = S.selected.has(g.id);
  return `<div class="row item ${sel ? 'selected' : ''} ${g.running ? 'running' : ''}" data-id="${esc(g.id)}">` +
    `<input class="rowcheck" type="checkbox" data-act="check" ${sel ? 'checked' : ''}>` +
    artHtml(g, 'thumb') +
    `<span class="name" title="${esc(g.title)}">${esc(g.title)}</span>` +
    `<span class="plat"><i class="dot" style="--dot:${platColor(g.platform)}"></i>${esc(normPlatform(g.platform))}</span>` +
    `<span class="muted">${g.size > 0 ? esc(g.sizeText) : ''}</span>` +
    `<span class="muted">${g.playSeconds > 0 ? esc(g.playText) : ''}</span>` +
    `<span class="muted">${esc(g.lastText || '')}</span>` +
    `<button class="star ${g.favorite ? 'on' : ''}" data-act="fav" title="${esc(g.favorite ? t('unfav') : t('fav'))}">${ic('star')}</button>` +
    (g.running ? `<span class="live">${t('playing')}</span>` : `<button class="mini-play" data-act="play">${t('play').toUpperCase()}</button>`) +
    `</div>`;
}

function headHtml() {
  const col = (label, key, asc, desc) => {
    const sorted = S.sort === asc || S.sort === desc;
    return `<span class="sortable ${sorted ? 'sorted' : ''}" data-sort="${key}">${esc(label)}${sorted ? (S.sort === asc ? ' ↑' : ' ↓') : ''}</span>`;
  };
  return `<div class="row head"><span></span><span></span>${col(t('cJogo'), 'name', 'name-asc', 'name-desc')}${col(t('cPlat'), 'platform', 'platform', 'platform')}` +
    `${col(t('cSize'), 'size', 'size-asc', 'size-desc')}${col(t('cTime'), 'played', 'played', 'played')}${col(t('cLast'), 'recent', 'recent', 'recent')}<span></span><span></span></div>`;
}

function shelfHtml() {
  const items = visibleGames().filter((g) => g.running || g.lastPlayed)
    .sort((a, b) => Number(b.running) - Number(a.running) || (b.lastPlayed || 0) - (a.lastPlayed || 0)).slice(0, 8);
  if (!items.length) return '';
  return `<section class="shelf-wrap"><div class="section-head"><h2>${t('cont')}</h2></div><div class="shelf">` +
    items.map((g) => `<div class="mini" data-id="${esc(g.id)}">${artHtml(g, 'thumb')}<div class="info"><div class="title">${esc(g.title)}</div>` +
      `<div class="meta">${esc(g.running ? t('playing') : g.lastText || '')}</div></div>` +
      `${g.running ? '' : `<button class="go" data-act="play" title="${esc(t('play'))}">${ic('play')}</button>`}</div>`).join('') +
    `</div></section>`;
}

function emptyHtml(kind) {
  const map = {
    none: ['pad', 'emptyT', 'emptyS', true], search: ['search', 'noRes', 'noResS', false], favorites: ['star', 'emptyFav', 'emptyFavS', false],
    recent: ['clock', 'emptyRec', 'emptyRecS', false], hidden: ['eyeoff', 'emptyHid', 'emptyHidS', false]
  };
  const [icon, title, sub, actions] = map[kind] || map.search;
  return `<div class="empty"><div><div class="big">${ic(icon)}</div><h3>${t(title)}</h3><p>${t(sub)}</p>` +
    (actions ? `<div class="actions"><button class="btn primary" data-act="scan">${ic('scan')}${t('scan')}</button><button class="btn" data-act="add">${ic('plus')}${t('add')}</button></div>` : '') +
    `</div></div>`;
}

function viewTitle() {
  return S.view === 'favorites' ? t('favorites') : S.view === 'recent' ? t('recent') : S.view === 'hidden' ? t('hidden') : S.view === 'platform' ? S.platform : t('all');
}

function renderContent() {
  const el = $('content');
  el.classList.toggle('selecting-mode', S.selected.size > 0);
  if (S.view === 'stats') { el.innerHTML = statsHtml(); animateContent(); return; }

  const list = currentList();
  if (!list.length) {
    const kind = S.search.trim() ? 'search' : S.games.length === 0 ? 'none' : (['favorites', 'recent', 'hidden'].includes(S.view) ? S.view : 'search');
    el.innerHTML = emptyHtml(kind);
    animateContent();
    return;
  }

  const showShelf = S.view === 'all' && !S.search.trim();
  const head = `<div class="section-head"><h2>${esc(viewTitle())}</h2><span class="sub">${list.length} ${list.length === 1 ? t('game') : t('games')}</span></div>`;
  const body = S.mode === 'table'
    ? `<div class="table">${headHtml()}${list.map(rowHtml).join('')}</div>`
    : `<div class="grid">${list.map(cardHtml).join('')}</div>`;
  el.innerHTML = (showShelf ? shelfHtml() : '') + head + body;
  animateContent();
}

const kpiFormat = { int: (v) => String(Math.round(v)), size: (v) => fmtSize(v), time: (v) => fmtTime(v) };

function statsHtml() {
  const g = visibleGames();
  const totalSize = g.reduce((s, x) => s + (x.size || 0), 0);
  const totalTime = g.reduce((s, x) => s + (x.playSeconds || 0), 0);
  const played = g.filter((x) => x.playSeconds > 0);
  const backlog = g.filter((x) => !x.playSeconds);
  const kpi = (cls, k, kind, raw, s) =>
    `<div class="kpi ${cls}"><div class="k">${t(k)}</div><div class="v" data-count="${raw}" data-kind="${kind}">${kpiFormat[kind](raw)}</div><div class="s">${t(s)}</div></div>`;

  const bars = (items, valFn, fmtFn, color) => {
    if (!items.length) return `<div class="muted-note">${t('noPlayed')}</div>`;
    const max = Math.max(...items.map(valFn), 1);
    return `<div class="bars">` + items.map((x, i) =>
      `<div class="bar-row"><span class="rank">${i + 1}</span><span class="nm" title="${esc(x.title)}">${esc(x.title)}</span><span class="val">${esc(fmtFn(x))}</span>` +
      `<div class="track"><div class="fill" style="width:${(valFn(x) / max * 100).toFixed(1)}%;--c:${color}"></div></div></div>`).join('') + `</div>`;
  };

  const byPlat = {};
  g.forEach((x) => { const p = normPlatform(x.platform); (byPlat[p] ||= { n: 0, size: 0 }); byPlat[p].n++; byPlat[p].size += x.size || 0; });
  const plats = Object.entries(byPlat).sort((a, b) => b[1].n - a[1].n);
  const stack = plats.map(([p, v]) => `<i style="width:${(v.n / Math.max(g.length, 1) * 100).toFixed(2)}%;--c:${platColor(p)}" title="${esc(p)}"></i>`).join('');
  const legend = plats.map(([p, v]) => `<div class="li"><i class="dot" style="--dot:${platColor(p)}"></i><span class="nm">${esc(p)}</span><span class="val">${v.n} · ${fmtSize(v.size)}</span></div>`).join('');

  const topPlayed = played.slice().sort((a, b) => b.playSeconds - a.playSeconds).slice(0, 8);
  const topHeavy = g.filter((x) => x.size > 0).sort((a, b) => b.size - a.size).slice(0, 8);
  const shown = backlog.slice(0, 30);

  return `<div class="kpis">` +
    kpi('accent', 'kDisk', 'size', totalSize, 'kDiskS') + kpi('success', 'kTime', 'time', totalTime, 'kTimeS') +
    kpi('', 'kGames', 'int', g.length, 'kGamesS') + kpi('', 'kStarted', 'int', played.length, 'kStartedS') +
    kpi('', 'kBacklog', 'int', backlog.length, 'kBacklogS') + kpi('', 'kFav', 'int', g.filter((x) => x.favorite).length, 'kFavS') + `</div>` +
    `<div class="panels">` +
    `<section class="panel"><h3>${t('pPlayed')}</h3>${bars(topPlayed, (x) => x.playSeconds, (x) => x.playText || fmtTime(x.playSeconds), 'var(--success)')}</section>` +
    `<section class="panel"><h3>${t('pHeavy')}</h3>${bars(topHeavy, (x) => x.size, (x) => x.sizeText, 'var(--accent)')}</section>` +
    `<section class="panel"><h3>${t('pPlat')}</h3><div class="stack">${stack}</div><div class="legend">${legend}</div></section>` +
    `<section class="panel"><h3>${t('pBacklog')}</h3>${backlog.length ? `<div class="tags">${shown.map((x) => `<span class="tag">${esc(x.title)}</span>`).join('')}${backlog.length > shown.length ? `<span class="tag">+${backlog.length - shown.length} ${t('more')}</span>` : ''}</div>` : `<div class="muted-note">${t('allStarted')}</div>`}</section>` +
    `</div>`;
}

/* entrance animation: only when the view itself changes, never on a data refresh */
let animKey = '';
function animateContent() {
  const key = [S.view, S.platform, S.mode, S.lang].join('|');
  if (key === animKey) return;
  animKey = key;
  if (!canAnim()) return;

  const els = $('content').querySelectorAll('.card, .row.item, .mini, .kpi, .panel, .empty > div');
  if (els.length) {
    gsap.fromTo(els, { opacity: 0, y: 14 }, {
      opacity: 1, y: 0, duration: 0.4, ease: 'power2.out', stagger: { each: 0.018, amount: 0.45 }, clearProps: 'opacity,transform'
    });
  }
  if (S.view === 'stats') {
    gsap.from('#content .fill', { width: 0, duration: 0.8, ease: 'power3.out', stagger: 0.05, delay: 0.1 });
    document.querySelectorAll('#content [data-count]').forEach((el) => {
      const end = Number(el.dataset.count), fmt = kpiFormat[el.dataset.kind], o = { v: 0 };
      gsap.to(o, { v: end, duration: 0.9, ease: 'power2.out', onUpdate: () => { el.textContent = fmt(o.v); }, onComplete: () => { el.textContent = fmt(end); } });
    });
  }
}

function renderAll() { renderTopbar(); renderSidebar(); renderToolbar(); renderContent(); }

/* ---------- themed dropdown (replaces the native <select> look) ----------
   The real <select> stays in the DOM (hidden) and remains the source of truth, so existing
   `change` listeners keep working: picking an option sets select.value and dispatches `change`. */
let ddMenu = null;      // the open popup, if any
let ddOwner = null;     // wrapper (.dd) that opened it
const ddIsOpen = () => !!ddMenu;

function ddSync(sel) {
  const wrap = sel && sel._dd;
  if (!wrap) return;
  const btn = wrap.querySelector('.dd-btn');
  const opt = sel.options[sel.selectedIndex];
  btn.querySelector('.dd-label').textContent = opt ? opt.textContent : '';
  btn.disabled = sel.disabled;
  wrap.classList.toggle('disabled', sel.disabled);
}

function ddClose(focusButton = false) {
  if (!ddMenu) return;
  const menu = ddMenu, owner = ddOwner;
  ddMenu = null; ddOwner = null;
  owner.querySelector('.dd-btn').setAttribute('aria-expanded', 'false');
  if (focusButton) owner.querySelector('.dd-btn').focus();
  if (canAnim()) gsap.to(menu, { opacity: 0, y: -4, duration: 0.1, onComplete: () => menu.remove() });
  else menu.remove();
}

function ddOpen(wrap) {
  ddClose();
  const sel = wrap._sel, btn = wrap.querySelector('.dd-btn');
  const menu = document.createElement('div');
  menu.className = 'dd-menu';
  menu.setAttribute('role', 'listbox');
  menu.innerHTML = Array.from(sel.options).map((o, i) =>
    `<button type="button" class="dd-opt ${i === sel.selectedIndex ? 'selected' : ''}" role="option" data-i="${i}" aria-selected="${i === sel.selectedIndex}">` +
    `<span class="t">${esc(o.textContent)}</span>${ic('check')}</button>`).join('');
  document.body.appendChild(menu);

  // place under the button; flip above when there is not enough room below
  const r = btn.getBoundingClientRect();
  menu.style.minWidth = Math.max(r.width, 180) + 'px';
  const mh = menu.offsetHeight, below = innerHeight - r.bottom - 12;
  const up = mh > below && r.top > below;
  menu.style.left = Math.max(8, Math.min(r.left, innerWidth - menu.offsetWidth - 8)) + 'px';
  menu.style.top = (up ? Math.max(8, r.top - mh - 6) : r.bottom + 6) + 'px';

  ddMenu = menu; ddOwner = wrap;
  btn.setAttribute('aria-expanded', 'true');
  if (canAnim()) gsap.fromTo(menu, { opacity: 0, y: up ? 6 : -6 }, { opacity: 1, y: 0, duration: 0.16, ease: 'power2.out' });

  const current = menu.querySelector('.selected') || menu.firstElementChild;
  if (current) { current.focus({ preventScroll: true }); current.scrollIntoView({ block: 'nearest' }); }

  menu.addEventListener('click', (e) => {
    const o = e.target.closest('.dd-opt');
    if (!o) return;
    const i = +o.dataset.i;
    if (sel.selectedIndex !== i) {
      sel.selectedIndex = i;
      ddSync(sel);
      sel.dispatchEvent(new Event('change', { bubbles: true }));
    }
    ddClose(true);
  });
}

function enhanceSelects(root) {
  root.querySelectorAll('select:not([data-dd])').forEach((sel) => {
    sel.dataset.dd = '1';
    sel.classList.add('dd-native');
    const wrap = document.createElement('div');
    wrap.className = 'dd' + (sel.classList.contains('select') ? ' dd-toolbar' : '');
    wrap.innerHTML = `<button type="button" class="dd-btn" aria-haspopup="listbox" aria-expanded="false" title="${esc(sel.title || '')}"><span class="dd-label"></span>${ic('chevron')}</button>`;
    sel.after(wrap);
    wrap._sel = sel; sel._dd = wrap;
    ddSync(sel);

    const btn = wrap.querySelector('.dd-btn');
    btn.addEventListener('click', () => (ddOwner === wrap ? ddClose() : ddOpen(wrap)));
    btn.addEventListener('keydown', (e) => { if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); ddOpen(wrap); } });
  });
}

// keyboard inside the open menu; Escape closes only the menu (and must not close a modal underneath)
document.addEventListener('keydown', (e) => {
  if (!ddMenu) return;
  const opts = Array.from(ddMenu.querySelectorAll('.dd-opt'));
  const i = opts.indexOf(document.activeElement);
  if (e.key === 'Escape') { e.preventDefault(); e.stopImmediatePropagation(); ddClose(true); }
  else if (e.key === 'ArrowDown') { e.preventDefault(); opts[Math.min(opts.length - 1, i + 1)].focus(); }
  else if (e.key === 'ArrowUp') { e.preventDefault(); opts[Math.max(0, i - 1)].focus(); }
  else if (e.key === 'Home') { e.preventDefault(); opts[0].focus(); }
  else if (e.key === 'End') { e.preventDefault(); opts[opts.length - 1].focus(); }
  else if (e.key === 'Tab') ddClose();
}, true);
document.addEventListener('mousedown', (e) => {
  if (ddMenu && !e.target.closest('.dd-menu') && !(ddOwner && ddOwner.contains(e.target))) ddClose();
}, true);
window.addEventListener('blur', () => ddClose());
window.addEventListener('resize', () => ddClose());
document.addEventListener('scroll', (e) => { if (!(e.target instanceof Element) || !e.target.closest('.dd-menu')) ddClose(); }, true);

/* ---------- selection ---------- */
function setSelection(ids) {
  S.selected = new Set(ids);
  renderToolbar(); renderContent();
}
function toggleSelect(id) {
  const n = new Set(S.selected);
  n.has(id) ? n.delete(id) : n.add(id);
  S.lastClicked = id;
  setSelection(n);
}

/* ---------- context menu ---------- */
function showCtx(x, y, g) {
  const m = $('ctx');
  m.innerHTML =
    (g.running ? '' : `<button data-c="play">${ic('play')}${t('play')}</button>`) +
    `<button data-c="fav">${ic('star')}${g.favorite ? t('unfav') : t('fav')}</button>` +
    `<button data-c="folder">${ic('folder')}${t('folder')}</button>` +
    `<button data-c="edit">${ic('edit')}${t('edit')}</button>` +
    `<button data-c="hide">${ic(g.hidden ? 'eye' : 'eyeoff')}${g.hidden ? t('restoreOne') : t('hideOne')}</button><hr>` +
    `<button class="danger" data-c="remove">${ic('trash')}${t('removeOne')}</button>`;
  m.dataset.id = g.id;
  m.hidden = false;
  const w = m.offsetWidth, h = m.offsetHeight;
  m.style.left = Math.max(8, Math.min(x, innerWidth - w - 8)) + 'px';
  m.style.top = Math.max(8, Math.min(y, innerHeight - h - 8)) + 'px';
  if (canAnim()) gsap.fromTo(m, { opacity: 0, scale: 0.96, y: -4 }, { opacity: 1, scale: 1, y: 0, duration: 0.14, ease: 'power2.out', transformOrigin: 'top left' });
}
const hideCtx = () => { $('ctx').hidden = true; };

/* ---------- actions ---------- */
function act(name, id) {
  const g = id ? byId(id) : null;
  switch (name) {
    case 'play': if (g && !g.running) send('play', { id }); break;
    case 'fav': if (g) send('fav', { id }); break;
    case 'folder': send('folder', { id }); break;
    case 'edit': if (g) openGameForm(g); break;
    case 'hide': if (g) send('hide', { ids: [id], hidden: !g.hidden }); break;
    case 'remove': if (g) askRemove([g]); break;
    case 'add': openGameForm(null); break;
    case 'scan': openScanModal(); break;
    case 'optimize': if (!S.busy) send('optimize'); break;
    case 'settings': openSettings(); break;
    case 'theme': send('themeCycle'); break;
    case 'selall': setSelection(currentList().map((x) => x.id)); break;
    case 'clearsel': setSelection([]); break;
    case 'hidesel': send('hide', { ids: [...S.selected], hidden: S.view !== 'hidden' }); break;
    case 'removesel': askRemove([...S.selected].map(byId).filter(Boolean)); break;
  }
}

async function askRemove(list) {
  if (!list.length) return;
  const one = list.length === 1;
  const ok = await confirmDialog({
    title: one ? t('removeOne') : `${t('remove')} (${list.length})`,
    message: one ? t('confirmRemoveOne').replace('{0}', list[0].title) : t('confirmRemoveMany').replace('{0}', list.length),
    confirm: t('remove'), danger: true
  });
  if (ok) send('remove', { ids: list.map((g) => g.id) });
}

function setView(key) {
  S.selected.clear();
  if (key.startsWith('plat:')) { S.view = 'platform'; S.platform = key.slice(5); }
  else { S.view = key; S.platform = null; }
  renderAll();
  $('content').scrollTop = 0;
}

/* ---------- events ---------- */
document.addEventListener('click', (e) => {
  if (e.target.closest('#modal-root, .dd-menu, .dd')) return; // modals and dropdowns handle their own clicks

  const ctx = e.target.closest('#ctx');
  if (ctx) {
    const c = e.target.closest('button[data-c]');
    if (c) { act(c.dataset.c, ctx.dataset.id); }
    hideCtx(); return;
  }
  hideCtx();

  const win = e.target.closest('[data-win]');
  if (win) { send('win', { action: win.dataset.win }); return; }

  const nav = e.target.closest('[data-nav]');
  if (nav) { setView(nav.dataset.nav); return; }

  const mode = e.target.closest('[data-mode]');
  if (mode) { S.mode = mode.dataset.mode; try { localStorage.setItem('aib.mode', S.mode); } catch { } renderToolbar(true); renderContent(); return; }

  const sortHead = e.target.closest('[data-sort]');
  if (sortHead) {
    const k = sortHead.dataset.sort;
    const pairs = { name: ['name-asc', 'name-desc'], size: ['size-desc', 'size-asc'], platform: ['platform', 'platform'], played: ['played', 'played'], recent: ['recent', 'recent'] };
    const [a, b] = pairs[k];
    S.sort = S.sort === a ? b : a;
    try { localStorage.setItem('aib.sort', S.sort); } catch { }
    renderToolbar(true); renderContent(); return;
  }

  const actEl = e.target.closest('[data-act]');
  if (actEl) {
    const card = actEl.closest('[data-id]');
    const id = card && card.dataset.id;
    e.stopPropagation();
    if (actEl.dataset.act === 'check') { if (id) toggleSelect(id); return; }
    act(actEl.dataset.act, id);
    return;
  }

  const card = e.target.closest('.card, .row.item');
  if (card) {
    const id = card.dataset.id;
    if (S.selected.size > 0 || e.ctrlKey || e.metaKey) {
      if (e.shiftKey && S.lastClicked) {
        const ids = currentList().map((x) => x.id);
        const a = ids.indexOf(S.lastClicked), b = ids.indexOf(id);
        if (a >= 0 && b >= 0) { const [lo, hi] = a < b ? [a, b] : [b, a]; setSelection([...S.selected, ...ids.slice(lo, hi + 1)]); return; }
      }
      toggleSelect(id);
    }
  }
});

document.addEventListener('dblclick', (e) => {
  if (e.target.closest('[data-act], #modal-root, #topbar')) return;
  const card = e.target.closest('.card, .row.item, .mini');
  if (card) act('play', card.dataset.id);
});

document.addEventListener('contextmenu', (e) => {
  e.preventDefault();
  if (e.target.closest('#modal-root')) return;
  const card = e.target.closest('.card, .row.item');
  if (!card) { hideCtx(); return; }
  const g = byId(card.dataset.id);
  if (g) showCtx(e.clientX, e.clientY, g);
});

document.addEventListener('input', (e) => {
  if (e.target.id === 'q') { S.search = e.target.value; renderContent(); }
});
document.addEventListener('change', (e) => {
  if (e.target.id === 'sort') {
    S.sort = e.target.value;
    try { localStorage.setItem('aib.sort', S.sort); } catch { }
    renderContent();
  }
});
document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape') {
    if ((typeof Modals !== 'undefined' && Modals) && Modals.isOpen()) return; // the modal closes itself
    hideCtx();
    if (S.selected.size) setSelection([]); else if (S.search) { S.search = ''; renderToolbar(true); renderContent(); }
    return;
  }
  const typing = ['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement.tagName);
  if ((typeof Modals !== 'undefined' && Modals) && Modals.isOpen()) return;
  if ((e.key === '/' || (e.key === 'k' && (e.ctrlKey || e.metaKey))) && !typing) { const q = $('q'); if (q) { e.preventDefault(); q.focus(); } }
  else if (e.key === 'a' && (e.ctrlKey || e.metaKey) && !typing && S.view !== 'stats') { e.preventDefault(); act('selall'); }
});
window.addEventListener('blur', hideCtx);
window.addEventListener('resize', hideCtx);

/* ---------- drag & drop of files from Explorer ---------- */
let dragDepth = 0;
const hasFiles = (e) => e.dataTransfer && Array.from(e.dataTransfer.types || []).includes('Files');
function setDropOverlay(on) {
  const d = $('drop');
  if (on) d.innerHTML = `<div><div class="big">${ic('plus')}</div><h3>${t('dropT')}</h3><p>${t('dropS')}</p></div>`;
  d.hidden = !on;
}
window.addEventListener('dragenter', (e) => { if (!hasFiles(e)) return; e.preventDefault(); dragDepth++; setDropOverlay(true); });
window.addEventListener('dragover', (e) => { if (hasFiles(e)) { e.preventDefault(); e.dataTransfer.dropEffect = 'copy'; } });
window.addEventListener('dragleave', (e) => { if (!hasFiles(e)) return; dragDepth = Math.max(0, dragDepth - 1); if (!dragDepth) setDropOverlay(false); });
window.addEventListener('drop', (e) => {
  if (!hasFiles(e)) return;
  e.preventDefault();
  dragDepth = 0;
  setDropOverlay(false);
  // WebView2 resolves File objects to real disk paths on the C# side
  if (host && host.postMessageWithAdditionalObjects) host.postMessageWithAdditionalObjects({ type: 'drop' }, e.dataTransfer.files);
});

/* ---------- host messages ---------- */
function toast(text) {
  const el = document.createElement('div');
  el.className = 'toast';
  el.innerHTML = `${ic('info')}<span></span>`;
  el.querySelector('span').textContent = text;
  $('toasts').appendChild(el);
  if (canAnim()) {
    gsap.from(el, { opacity: 0, y: 14, scale: 0.97, duration: 0.3, ease: 'power3.out' });
    gsap.to(el, { opacity: 0, y: 6, duration: 0.3, delay: 3.4, onComplete: () => el.remove() });
  } else {
    setTimeout(() => el.remove(), 3700);
  }
}

function applyTheme(vars) {
  const root = document.documentElement.style;
  Object.entries(vars || {}).forEach(([k, v]) => root.setProperty('--' + k, v));
  const hex = (vars && vars.bg || '').replace('#', '');
  if (hex.length >= 6) {
    const [r, g, b] = [0, 2, 4].map((i) => parseInt(hex.slice(i, i + 2), 16));
    const light = (r * 299 + g * 587 + b * 114) / 1000 > 140;
    root.setProperty('--art-l', light ? '80%' : '24%');
    root.setProperty('--art-s', light ? '38%' : '32%');
    root.setProperty('--shadow', light ? '0 10px 26px rgba(40,30,10,.18)' : '0 10px 28px rgba(0,0,0,.30)');
  }
}

function applyThemeMeta(m) {
  if (m.themeMode) S.themeMode = m.themeMode;
  if (typeof m.systemLight === 'boolean') S.systemLight = m.systemLight;
  if (m.settings) S.settings = { ...S.settings, ...m.settings };
}

function onHost(m) {
  if (!m || !m.type) return;
  switch (m.type) {
    case 'init':
      if (m.lang) S.lang = STR[m.lang] ? m.lang : 'en-US';
      document.documentElement.lang = S.lang;
      applyTheme(m.theme);
      applyThemeMeta(m);
      S.maximized = !!m.maximized;
      S.games = m.games || [];
      renderAll();
      break;
    case 'games': {
      S.games = m.games || [];
      const ids = new Set(S.games.map((g) => g.id));
      S.selected = new Set([...S.selected].filter((id) => ids.has(id)));
      if (S.view === 'hidden' && !S.games.some((g) => g.hidden)) { S.view = 'all'; }
      renderAll();
      break;
    }
    case 'theme': applyTheme(m.vars); applyThemeMeta(m); renderTopbar(); if ((typeof Modals !== 'undefined' && Modals)) Modals.onHost(m); break;
    case 'lang': {
      S.lang = STR[m.lang] ? m.lang : 'en-US';
      document.documentElement.lang = S.lang;
      applyThemeMeta(m);
      renderAll(); renderToolbar(true);
      const cm = typeof Modals !== 'undefined' && Modals.current();
      if (cm && cm.reopen) { cm.close(true); cm.reopen(); }
      break;
    }
    case 'toast': toast(m.text); break;
    case 'busy': S.busy = !!m.value; renderToolbar(true); break;
    case 'win': S.maximized = !!m.maximized; renderTopbar(); break;
    default: if ((typeof Modals !== 'undefined' && Modals)) Modals.onHost(m);
  }
}
