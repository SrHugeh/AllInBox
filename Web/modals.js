/* ==========================================================================
   All In Box - modals (confirm, add/edit game, scan, settings)
   Everything runs inside the page; native work (file pickers, scanning,
   saving) is requested from the C# host and answered through onHost().
   ========================================================================== */
'use strict';

/* ---------- strings ---------- */
const MODAL_STR = {
  'pt-BR': {
    cancel: 'Cancelar', close: 'Fechar',
    confirmRemoveOne: 'Tem certeza que deseja remover "{0}" da sua biblioteca?\n\nOs arquivos do jogo NÃO serão apagados do seu computador.',
    confirmRemoveMany: 'Tem certeza que deseja remover {0} jogo(s) da sua biblioteca?\n\nOs arquivos dos jogos NÃO serão apagados do seu computador.',
    gfAddT: 'Adicionar jogo', gfAddS: 'Configure o executável e os detalhes do jogo', gfEditT: 'Editar jogo', gfEditS: 'Edite as propriedades e atalhos do jogo',
    gfExe: 'Executável ou atalho', gfBrowse: 'Procurar…', gfName: 'Nome do jogo', gfNameph: 'Detectado automaticamente se vazio',
    gfPlat: 'Plataforma', gfCat: 'Categoria / gênero', gfArgs: 'Argumentos de inicialização', gfWork: 'Diretório de trabalho',
    gfNotes: 'Anotações', gfNotesph: 'Dicas, mods instalados…', gfFav: 'Adicionar aos favoritos', gfFavS: 'Aparece na aba Favoritos',
    gfIconHint: 'Trocar ícone', gfIconNone: 'Escolher ícone', gfSave: 'Salvar na biblioteca', gfSaveEdit: 'Salvar alterações',
    gfExeReq: 'Informe ou selecione o executável do jogo.', optional: 'opcional',
    scT: 'Escanear bibliotecas', scS: 'Detecte jogos automaticamente nas plataformas ou em pastas locais', scLoc: 'Locais padrão por plataforma',
    scAll: 'Todas as plataformas', scCustom: 'Pasta personalizada', scFolderph: 'Escolha uma pasta para procurar jogos', scScanFolder: 'Escanear pasta',
    scSearching: 'Procurando jogos e executáveis principais…', scSearchingS: 'Isso pode levar alguns segundos.',
    scEmptyT: 'Nenhum jogo escaneado ainda', scEmptyS: 'Escolha uma plataforma acima ou escaneie uma pasta.',
    scNoneT: 'Nenhum jogo novo encontrado', scNoneS: 'Tudo o que foi detectado já está na sua biblioteca.',
    scNoDirs: 'Nenhum local padrão de instalação foi encontrado para {0}. Escolha a pasta manualmente.',
    scSelAll: 'Selecionar todos', scDesel: 'Desmarcar todos', scImport: 'Importar selecionados ({0})', scFound: '{0} encontrado(s)',
    stT: 'Configurações', stS: 'Idioma, aparência e comportamento da bandeja', stLang: 'Idioma', stLangS: 'Idioma da interface do aplicativo',
    stAuto: 'Automático (sistema)', stTheme: 'Aparência', stThemeS: 'Escolha um tema pronto ou monte o seu', stPresets: 'Temas prontos',
    stCustom: 'Cores personalizadas', cBg: 'Fundo', cSurface: 'Superfície', cAccent: 'Destaque', cText: 'Texto',
    stTray: 'Bandeja do sistema', stTrayClose: 'Minimizar para a bandeja ao fechar', stTrayCloseS: 'O app continua rodando em segundo plano',
    stTrayLaunch: 'Ocultar ao iniciar um jogo', stTrayLaunchS: 'A janela some da tela enquanto você joga', stDone: 'Concluir',
    pDark: 'Escuro', pLight: 'Papel', pNordic: 'Nórdico', pWarm: 'Papel quente'
  },
  'en-US': {
    cancel: 'Cancel', close: 'Close',
    confirmRemoveOne: 'Are you sure you want to remove "{0}" from your library?\n\nThe game files will NOT be deleted from your computer.',
    confirmRemoveMany: 'Are you sure you want to remove {0} game(s) from your library?\n\nThe game files will NOT be deleted from your computer.',
    gfAddT: 'Add game', gfAddS: 'Set the executable and the game details', gfEditT: 'Edit game', gfEditS: 'Edit the game properties and shortcuts',
    gfExe: 'Executable or shortcut', gfBrowse: 'Browse…', gfName: 'Game name', gfNameph: 'Detected automatically if empty',
    gfPlat: 'Platform', gfCat: 'Category / genre', gfArgs: 'Launch arguments', gfWork: 'Working directory',
    gfNotes: 'Notes', gfNotesph: 'Tips, installed mods…', gfFav: 'Add to favorites', gfFavS: 'Shows up in the Favorites tab',
    gfIconHint: 'Change icon', gfIconNone: 'Choose icon', gfSave: 'Save to library', gfSaveEdit: 'Save changes',
    gfExeReq: 'Enter or select the game executable.', optional: 'optional',
    scT: 'Scan libraries', scS: 'Detect games automatically from platforms or local folders', scLoc: 'Default locations by platform',
    scAll: 'All platforms', scCustom: 'Custom folder', scFolderph: 'Pick a folder to look for games', scScanFolder: 'Scan folder',
    scSearching: 'Looking for games and main executables…', scSearchingS: 'This can take a few seconds.',
    scEmptyT: 'Nothing scanned yet', scEmptyS: 'Pick a platform above or scan a folder.',
    scNoneT: 'No new games found', scNoneS: 'Everything detected is already in your library.',
    scNoDirs: 'No default install location was found for {0}. Pick the folder manually.',
    scSelAll: 'Select all', scDesel: 'Deselect all', scImport: 'Import selected ({0})', scFound: '{0} found',
    stT: 'Settings', stS: 'Language, appearance and tray behavior', stLang: 'Language', stLangS: 'Interface language of the app',
    stAuto: 'Automatic (system)', stTheme: 'Appearance', stThemeS: 'Pick a ready-made theme or build your own', stPresets: 'Ready-made themes',
    stCustom: 'Custom colors', cBg: 'Background', cSurface: 'Surface', cAccent: 'Accent', cText: 'Text',
    stTray: 'System tray', stTrayClose: 'Minimize to tray on close', stTrayCloseS: 'The app keeps running in the background',
    stTrayLaunch: 'Hide when launching a game', stTrayLaunchS: 'The window disappears while you play', stDone: 'Done',
    pDark: 'Dark', pLight: 'Paper', pNordic: 'Nordic', pWarm: 'Warm paper'
  },
  'es-ES': {
    cancel: 'Cancelar', close: 'Cerrar',
    confirmRemoveOne: '¿Seguro que quieres quitar "{0}" de tu biblioteca?\n\nLos archivos del juego NO se borrarán de tu equipo.',
    confirmRemoveMany: '¿Seguro que quieres quitar {0} juego(s) de tu biblioteca?\n\nLos archivos de los juegos NO se borrarán de tu equipo.',
    gfAddT: 'Añadir juego', gfAddS: 'Configura el ejecutable y los detalles del juego', gfEditT: 'Editar juego', gfEditS: 'Edita las propiedades y accesos del juego',
    gfExe: 'Ejecutable o acceso directo', gfBrowse: 'Buscar…', gfName: 'Nombre del juego', gfNameph: 'Se detecta automáticamente si está vacío',
    gfPlat: 'Plataforma', gfCat: 'Categoría / género', gfArgs: 'Argumentos de inicio', gfWork: 'Directorio de trabajo',
    gfNotes: 'Notas', gfNotesph: 'Consejos, mods instalados…', gfFav: 'Añadir a favoritos', gfFavS: 'Aparece en la pestaña Favoritos',
    gfIconHint: 'Cambiar icono', gfIconNone: 'Elegir icono', gfSave: 'Guardar en la biblioteca', gfSaveEdit: 'Guardar cambios',
    gfExeReq: 'Indica o selecciona el ejecutable del juego.', optional: 'opcional',
    scT: 'Escanear bibliotecas', scS: 'Detecta juegos automáticamente en las plataformas o en carpetas locales', scLoc: 'Ubicaciones predeterminadas por plataforma',
    scAll: 'Todas las plataformas', scCustom: 'Carpeta personalizada', scFolderph: 'Elige una carpeta para buscar juegos', scScanFolder: 'Escanear carpeta',
    scSearching: 'Buscando juegos y ejecutables principales…', scSearchingS: 'Esto puede tardar unos segundos.',
    scEmptyT: 'Aún no se ha escaneado nada', scEmptyS: 'Elige una plataforma arriba o escanea una carpeta.',
    scNoneT: 'No se encontraron juegos nuevos', scNoneS: 'Todo lo detectado ya está en tu biblioteca.',
    scNoDirs: 'No se encontró ninguna ubicación de instalación para {0}. Elige la carpeta manualmente.',
    scSelAll: 'Seleccionar todo', scDesel: 'Deseleccionar todo', scImport: 'Importar seleccionados ({0})', scFound: '{0} encontrado(s)',
    stT: 'Ajustes', stS: 'Idioma, apariencia y comportamiento de la bandeja', stLang: 'Idioma', stLangS: 'Idioma de la interfaz de la aplicación',
    stAuto: 'Automático (sistema)', stTheme: 'Apariencia', stThemeS: 'Elige un tema listo o crea el tuyo', stPresets: 'Temas listos',
    stCustom: 'Colores personalizados', cBg: 'Fondo', cSurface: 'Superficie', cAccent: 'Acento', cText: 'Texto',
    stTray: 'Bandeja del sistema', stTrayClose: 'Minimizar a la bandeja al cerrar', stTrayCloseS: 'La aplicación sigue funcionando en segundo plano',
    stTrayLaunch: 'Ocultar al iniciar un juego', stTrayLaunchS: 'La ventana desaparece mientras juegas', stDone: 'Listo',
    pDark: 'Oscuro', pLight: 'Papel', pNordic: 'Nórdico', pWarm: 'Papel cálido'
  },
  'ja-JP': {
    cancel: 'キャンセル', close: '閉じる',
    confirmRemoveOne: '「{0}」をライブラリから削除しますか？\n\nゲームのファイルはコンピューターから削除されません。',
    confirmRemoveMany: '{0} 件のゲームをライブラリから削除しますか？\n\nゲームのファイルはコンピューターから削除されません。',
    gfAddT: 'ゲームを追加', gfAddS: '実行ファイルとゲームの詳細を設定します', gfEditT: 'ゲームを編集', gfEditS: 'ゲームのプロパティとショートカットを編集します',
    gfExe: '実行ファイルまたはショートカット', gfBrowse: '参照…', gfName: 'ゲーム名', gfNameph: '空の場合は自動検出されます',
    gfPlat: 'プラットフォーム', gfCat: 'カテゴリ / ジャンル', gfArgs: '起動引数', gfWork: '作業ディレクトリ',
    gfNotes: 'メモ', gfNotesph: 'ヒント、導入済みのMODなど…', gfFav: 'お気に入りに追加', gfFavS: 'お気に入りタブに表示されます',
    gfIconHint: 'アイコンを変更', gfIconNone: 'アイコンを選択', gfSave: 'ライブラリに保存', gfSaveEdit: '変更を保存',
    gfExeReq: 'ゲームの実行ファイルを入力または選択してください。', optional: '任意',
    scT: 'ライブラリをスキャン', scS: 'プラットフォームまたはローカルフォルダからゲームを自動検出します', scLoc: 'プラットフォーム別の既定の場所',
    scAll: 'すべてのプラットフォーム', scCustom: 'カスタムフォルダ', scFolderph: 'ゲームを探すフォルダを選択', scScanFolder: 'フォルダをスキャン',
    scSearching: 'ゲームとメインの実行ファイルを検索中…', scSearchingS: '数秒かかる場合があります。',
    scEmptyT: 'まだスキャンしていません', scEmptyS: '上でプラットフォームを選ぶか、フォルダをスキャンしてください。',
    scNoneT: '新しいゲームは見つかりませんでした', scNoneS: '検出されたものはすべてライブラリにあります。',
    scNoDirs: '{0} の既定のインストール場所が見つかりません。フォルダを手動で選んでください。',
    scSelAll: 'すべて選択', scDesel: 'すべて解除', scImport: '選択をインポート ({0})', scFound: '{0} 件見つかりました',
    stT: '設定', stS: '言語、外観、トレイの動作', stLang: '言語', stLangS: 'アプリのインターフェース言語',
    stAuto: '自動 (システム)', stTheme: '外観', stThemeS: '用意されたテーマを選ぶか、自分で作成します', stPresets: '用意されたテーマ',
    stCustom: 'カスタムカラー', cBg: '背景', cSurface: 'サーフェス', cAccent: 'アクセント', cText: 'テキスト',
    stTray: 'システムトレイ', stTrayClose: '閉じるときにトレイへ最小化', stTrayCloseS: 'アプリはバックグラウンドで動作し続けます',
    stTrayLaunch: 'ゲーム起動時に隠す', stTrayLaunchS: 'プレイ中はウィンドウが消えます', stDone: '完了',
    pDark: 'ダーク', pLight: 'ペーパー', pNordic: 'ノルディック', pWarm: 'ウォームペーパー'
  }
};
Object.keys(MODAL_STR).forEach((l) => Object.assign(STR[l], MODAL_STR[l]));
const tf = (key, ...args) => args.reduce((s, a, i) => s.replace(`{${i}}`, a), t(key));

/* ---------- modal manager ---------- */
const Modals = (() => {
  let cur = null;

  function open({ html, cls = '', width = 640, onMount, onHost }) {
    if (cur) cur.close(true);

    const backdrop = document.createElement('div');
    backdrop.className = 'modal-backdrop';
    backdrop.innerHTML = `<div class="modal ${cls}" style="--w:${width}px" role="dialog" aria-modal="true">${html}</div>`;
    $('modal-root').appendChild(backdrop);
    const el = backdrop.firstElementChild;

    const m = { el, onHost, closing: false, resolve: null };
    const done = new Promise((r) => { m.resolve = r; });

    m.close = (immediate = false, result) => {
      if (m.closing) return;
      m.closing = true;
      if (cur === m) cur = null;
      const finish = () => { backdrop.remove(); m.resolve(result); };
      if (immediate || !canAnim()) { finish(); return; }
      gsap.to(el, { y: 10, scale: 0.98, opacity: 0, duration: 0.16, ease: 'power2.in' });
      gsap.to(backdrop, { opacity: 0, duration: 0.18, onComplete: finish });
    };

    // click on the dimmed area closes; a drag that started inside the dialog must not
    let downOnBackdrop = false;
    backdrop.addEventListener('mousedown', (e) => { downOnBackdrop = e.target === backdrop; });
    backdrop.addEventListener('click', (e) => {
      if (e.target === backdrop && downOnBackdrop) m.close(false, false);
      const c = e.target.closest('[data-close]');
      if (c) m.close(false, false);
    });

    cur = m;
    if (onMount) onMount(el, m);
    enhanceSelects(el);

    if (canAnim()) {
      gsap.fromTo(backdrop, { opacity: 0 }, { opacity: 1, duration: 0.2, ease: 'power1.out' });
      gsap.fromTo(el, { y: 20, scale: 0.97, opacity: 0 }, { y: 0, scale: 1, opacity: 1, duration: 0.34, ease: 'back.out(1.4)', clearProps: 'transform,opacity' });
    }
    const first = el.querySelector('[autofocus]') || el.querySelector('input:not([type=checkbox]), select, textarea');
    if (first) setTimeout(() => first.focus(), 60);

    m.done = done;
    return m;
  }

  document.addEventListener('keydown', (e) => {
    if (!cur) return;
    if (e.key === 'Escape') { e.preventDefault(); cur.close(false, false); }
  }, true);

  return {
    open,
    isOpen: () => !!cur,
    current: () => cur,
    onHost: (m) => (cur && cur.onHost ? (cur.onHost(m), true) : false)
  };
})();

const head = (icon, title, sub) =>
  `<div class="modal-head"><div class="mi">${ic(icon)}</div><div class="mt"><h2>${esc(title)}</h2>${sub ? `<p>${esc(sub)}</p>` : ''}</div>` +
  `<button class="modal-x" data-close title="${esc(t('close'))}">${ic('x')}</button></div>`;

/* ---------- confirm ---------- */
function confirmDialog({ title, message, confirm, danger = false }) {
  const m = Modals.open({
    cls: `confirm ${danger ? 'danger' : ''}`, width: 440,
    html: head(danger ? 'trash' : 'info', title) +
      `<div class="modal-body">${esc(message)}</div>` +
      `<div class="modal-foot"><span class="spacer"></span><button class="btn ghost" data-close>${esc(t('cancel'))}</button>` +
      `<button class="btn ${danger ? 'solid-danger' : 'primary'}" id="cf-ok" autofocus>${esc(confirm)}</button></div>`,
    onMount: (el, mm) => { el.querySelector('#cf-ok').addEventListener('click', () => mm.close(false, true)); }
  });
  return m.done.then((r) => r === true);
}

/* ---------- add / edit game ---------- */
const KNOWN_PLATFORMS = ['PC / Outros', 'Steam', 'Epic Games', 'GOG', 'Ubisoft', 'EA App', 'Riot Games', 'Minecraft', 'Emulador'];

function openGameForm(game) {
  const edit = !!game;
  const st = { icon: edit ? game.icon : '', customIcon: '' };
  const plats = KNOWN_PLATFORMS.includes(game?.platform) || !game?.platform ? KNOWN_PLATFORMS : [game.platform, ...KNOWN_PLATFORMS];
  const v = (x) => esc(x ?? '');

  const html = head(edit ? 'edit' : 'plus', t(edit ? 'gfEditT' : 'gfAddT'), t(edit ? 'gfEditS' : 'gfAddS')) +
    `<div class="modal-body"><div class="form-grid">` +
    `<div><div class="icon-pick" id="gf-icon" title="${esc(t('gfIconHint'))}"></div></div>` +
    `<div class="fields">` +
    `<div class="field" id="gf-exe-f"><label for="gf-exe">${t('gfExe')} <em>*</em></label>` +
    `<div class="input-btn"><input id="gf-exe" value="${v(game?.exe)}" placeholder="C:\\Games\\MyGame\\game.exe" spellcheck="false" autofocus>` +
    `<button class="btn" id="gf-browse" type="button">${ic('folder')}${t('gfBrowse')}</button></div><span class="errmsg" id="gf-err" hidden></span></div>` +
    `<div class="field"><label for="gf-title">${t('gfName')}</label><input id="gf-title" value="${v(game?.title)}" placeholder="${esc(t('gfNameph'))}"></div>` +
    `<div class="row2"><div class="field"><label for="gf-plat">${t('gfPlat')}</label><select id="gf-plat">` +
    plats.map((p) => `<option ${p === (game?.platform || 'PC / Outros') ? 'selected' : ''}>${esc(p)}</option>`).join('') + `</select></div>` +
    `<div class="field"><label for="gf-cat">${t('gfCat')}</label><input id="gf-cat" value="${v(game?.category && game.category !== 'Geral' ? game.category : '')}" placeholder="Geral"></div></div>` +
    `</div></div>` +
    `<div class="fields" style="margin-top:16px">` +
    `<div class="row2"><div class="field"><label for="gf-args">${t('gfArgs')} <span class="muted-note">· ${t('optional')}</span></label><input id="gf-args" value="${v(game?.args)}" spellcheck="false"></div>` +
    `<div class="field"><label for="gf-work">${t('gfWork')} <span class="muted-note">· ${t('optional')}</span></label><input id="gf-work" value="${v(game?.workDir)}" spellcheck="false"></div></div>` +
    `<div class="field"><label for="gf-notes">${t('gfNotes')} <span class="muted-note">· ${t('optional')}</span></label><textarea id="gf-notes" placeholder="${esc(t('gfNotesph'))}">${v(game?.notes)}</textarea></div>` +
    `<label class="switch"><input type="checkbox" id="gf-fav" ${game?.favorite ? 'checked' : ''}><span class="txt"><b>${t('gfFav')}</b><span>${t('gfFavS')}</span></span></label>` +
    `</div></div>` +
    `<div class="modal-foot"><span class="spacer"></span><button class="btn ghost" data-close>${esc(t('cancel'))}</button>` +
    `<button class="btn primary" id="gf-save">${ic('check')}${t(edit ? 'gfSaveEdit' : 'gfSave')}</button></div>`;

  Modals.open({
    width: 680, html,
    onMount: (el, m) => {
      const q = (s) => el.querySelector(s);
      const renderIcon = () => {
        q('#gf-icon').innerHTML = st.icon
          ? `<img src="${st.icon}" alt=""><div class="hint">${esc(t('gfIconHint'))}</div>`
          : `<div class="none">${ic('image')}<span>${esc(t('gfIconNone'))}</span></div>`;
      };
      renderIcon();

      q('#gf-browse').addEventListener('click', () => send('pickExe'));
      q('#gf-icon').addEventListener('click', () => send('pickIcon'));
      q('#gf-exe').addEventListener('change', () => {
        q('#gf-exe-f').classList.remove('err'); q('#gf-err').hidden = true;
        if (!st.customIcon) send('previewIcon', { path: q('#gf-exe').value.trim(), title: q('#gf-title').value.trim(), workDir: q('#gf-work').value.trim() });
      });

      const save = q('#gf-save');
      const submit = () => {
        const exe = q('#gf-exe').value.trim();
        if (!exe) {
          q('#gf-exe-f').classList.add('err');
          const err = q('#gf-err'); err.textContent = t('gfExeReq'); err.hidden = false;
          q('#gf-exe').focus();
          if (canAnim()) gsap.fromTo(q('#gf-exe-f'), { x: -6 }, { x: 0, duration: 0.4, ease: 'elastic.out(1, 0.35)' });
          return;
        }
        save.disabled = true;
        send('saveGame', {
          id: game?.id || null, exe, title: q('#gf-title').value.trim(), workDir: q('#gf-work').value.trim(), args: q('#gf-args').value.trim(),
          platform: q('#gf-plat').value, category: q('#gf-cat').value.trim(), favorite: q('#gf-fav').checked, notes: q('#gf-notes').value.trim(),
          customIcon: st.customIcon
        });
      };
      save.addEventListener('click', submit);
      el.addEventListener('keydown', (e) => { if (e.key === 'Enter' && e.target.tagName === 'INPUT' && e.target.type !== 'checkbox') { e.preventDefault(); submit(); } });

      m.onHost = (msg) => {
        if (msg.type === 'picked' && msg.kind === 'exe') {
          q('#gf-exe').value = msg.path || '';
          q('#gf-exe-f').classList.remove('err'); q('#gf-err').hidden = true;
          if (msg.workDir) q('#gf-work').value = msg.workDir;
          if (msg.args) q('#gf-args').value = msg.args;
          if (!q('#gf-title').value.trim() && msg.title) q('#gf-title').value = msg.title;
          if (msg.platform && (q('#gf-plat').selectedIndex <= 0 || !edit)) {
            const opt = Array.from(q('#gf-plat').options).find((o) => o.value === msg.platform);
            if (opt) { q('#gf-plat').value = msg.platform; ddSync(q('#gf-plat')); }
          }
          if (!st.customIcon && msg.icon) { st.icon = msg.icon; renderIcon(); }
        } else if (msg.type === 'picked' && msg.kind === 'icon') {
          st.customIcon = msg.path || ''; if (msg.icon) st.icon = msg.icon; renderIcon();
        } else if (msg.type === 'picked' && msg.kind === 'preview') {
          if (!st.customIcon && msg.icon) { st.icon = msg.icon; renderIcon(); }
        } else if (msg.type === 'saveResult') {
          if (msg.ok) m.close();
          else { save.disabled = false; const err = q('#gf-err'); err.textContent = msg.message || ''; err.hidden = false; q('#gf-exe-f').classList.add('err'); }
        }
      };
    }
  });
}

/* ---------- scan ---------- */
const SCAN_PRESETS = [['all', null], ['Steam', 'Steam'], ['Epic Games', 'Epic Games'], ['Riot Games', 'Riot Games'], ['Minecraft', 'Minecraft'], ['Ubisoft', 'Ubisoft'], ['GOG', 'GOG']];

function openScanModal() {
  const st = { preset: null, loading: false, results: null, sel: new Set(), note: '', folder: '' };

  const html = head('scan', t('scT'), t('scS')) +
    `<div class="modal-body">` +
    `<div class="section-lbl" style="margin-top:4px">${t('scLoc')}</div><div class="chips" id="sc-chips"></div>` +
    `<div class="section-lbl">${t('scCustom')}</div>` +
    `<div class="input-btn" style="grid-template-columns:minmax(0,1fr) auto auto"><div class="field"><input id="sc-folder" placeholder="${esc(t('scFolderph'))}" spellcheck="false"></div>` +
    `<button class="btn" id="sc-browse" type="button">${ic('folder')}${t('gfBrowse')}</button><button class="btn primary" id="sc-scan" type="button">${ic('search')}${t('scScanFolder')}</button></div>` +
    `<div class="scan-results" id="sc-results"></div></div>` +
    `<div class="modal-foot"><button class="btn ghost sm" id="sc-all">${t('scSelAll')}</button><button class="btn ghost sm" id="sc-none">${t('scDesel')}</button>` +
    `<span class="spacer"></span><button class="btn ghost" data-close>${esc(t('cancel'))}</button><button class="btn primary" id="sc-import" disabled></button></div>`;

  Modals.open({
    width: 760, html,
    onMount: (el, m) => {
      const q = (s) => el.querySelector(s);

      const renderChips = () => {
        q('#sc-chips').innerHTML = SCAN_PRESETS.map(([key, plat]) =>
          `<button class="chip-btn ${st.preset === key ? 'on' : ''}" data-preset="${key}">` +
          (plat ? `<i class="dot" style="--dot:${platColor(plat)}"></i>` : ic('sparkles')) + `${esc(plat || t('scAll'))}</button>`).join('');
      };

      const renderResults = () => {
        const box = q('#sc-results');
        if (st.loading) {
          box.innerHTML = `<div class="scan-state"><div>${ic('spinner', 'big spin')}<b>${esc(t('scSearching'))}</b>${esc(t('scSearchingS'))}</div></div>`;
        } else if (st.results === null) {
          box.innerHTML = `<div class="scan-state"><div>${ic('foldersearch', 'big')}<b>${esc(t('scEmptyT'))}</b>${esc(st.note || t('scEmptyS'))}</div></div>`;
        } else if (!st.results.length) {
          box.innerHTML = `<div class="scan-state"><div>${ic('ok', 'big')}<b>${esc(t('scNoneT'))}</b>${esc(st.note || t('scNoneS'))}</div></div>`;
        } else {
          box.innerHTML = `<div class="scan-list scroll">` + st.results.map((r, i) =>
            `<label class="scan-item" data-i="${i}"><input class="chk" type="checkbox" ${st.sel.has(r.path) ? 'checked' : ''}>` +
            `<div style="min-width:0"><div class="nm">${esc(r.title)}</div><div class="pth" title="${esc(r.path)}">${esc(r.path)}</div></div>` +
            `<span class="plat"><i class="dot" style="--dot:${platColor(r.platform)}"></i>${esc(normPlatform(r.platform))}${r.size > 0 ? ' · ' + fmtSize(r.size) : ''}</span></label>`).join('') + `</div>`;
        }
        const n = st.sel.size;
        const imp = q('#sc-import');
        imp.innerHTML = `${ic('plus')}${esc(tf('scImport', n))}`;
        imp.disabled = n === 0;
        q('#sc-scan').disabled = st.loading;
      };

      renderChips(); renderResults();

      q('#sc-chips').addEventListener('click', (e) => {
        const b = e.target.closest('[data-preset]');
        if (!b || st.loading) return;
        st.preset = b.dataset.preset;
        renderChips();
        send('scanRun', st.preset === 'all' ? { mode: 'all' } : { mode: 'platform', platform: st.preset });
      });
      q('#sc-browse').addEventListener('click', () => send('pickFolder'));
      q('#sc-scan').addEventListener('click', () => {
        const path = q('#sc-folder').value.trim();
        if (!path) { q('#sc-folder').focus(); return; }
        st.preset = null; renderChips();
        send('scanRun', { mode: 'folder', path });
      });
      q('#sc-folder').addEventListener('keydown', (e) => { if (e.key === 'Enter') q('#sc-scan').click(); });

      q('#sc-results').addEventListener('change', (e) => {
        const row = e.target.closest('.scan-item');
        if (!row) return;
        const r = st.results[+row.dataset.i];
        e.target.checked ? st.sel.add(r.path) : st.sel.delete(r.path);
        const imp = q('#sc-import'); imp.innerHTML = `${ic('plus')}${esc(tf('scImport', st.sel.size))}`; imp.disabled = st.sel.size === 0;
      });
      q('#sc-all').addEventListener('click', () => { (st.results || []).forEach((r) => st.sel.add(r.path)); renderResults(); });
      q('#sc-none').addEventListener('click', () => { st.sel.clear(); renderResults(); });
      q('#sc-import').addEventListener('click', () => {
        if (!st.sel.size) return;
        send('scanImport', { paths: [...st.sel] });
        m.close();
      });

      m.onHost = (msg) => {
        if (msg.type === 'picked' && msg.kind === 'folder') { q('#sc-folder').value = msg.path || ''; if (msg.path) q('#sc-scan').click(); }
        else if (msg.type === 'scanState') { st.loading = !!msg.loading; if (st.loading) { st.results = null; st.sel.clear(); st.note = ''; } renderResults(); }
        else if (msg.type === 'scanResults') {
          st.loading = false;
          st.results = msg.items || [];
          st.sel = new Set(st.results.map((r) => r.path));
          st.note = msg.noDirs ? tf('scNoDirs', msg.platform || '') : '';
          if (msg.path) q('#sc-folder').value = msg.path;
          renderResults();
          if (canAnim()) gsap.from('#sc-results .scan-item', { opacity: 0, y: 8, duration: 0.25, stagger: { each: 0.015, amount: 0.3 }, clearProps: 'opacity,transform' });
        }
      };
    }
  });
}

/* ---------- settings ---------- */
const THEME_PRESETS = [
  ['pDark', '#0F1319', '#171D26', '#4A8FE7', '#E6EDF3', 'dark'],
  ['pLight', '#F5F3EE', '#FAF9F5', '#3A6EA5', '#1C2127', 'light'],
  ['pNordic', '#161B22', '#1F2630', '#58A6FF', '#F0F6FC', 'custom'],
  ['pWarm', '#EFECE4', '#F7F5F0', '#2B5B84', '#22262A', 'custom']
];
const LANGS = [['auto', null], ['pt-BR', 'Português (Brasil)'], ['en-US', 'English'], ['es-ES', 'Español'], ['ja-JP', '日本語']];

function openSettings() {
  const html = head('settings', t('stT'), t('stS')) +
    `<div class="modal-body scroll">` +
    `<section class="set-section"><h3>${t('stLang')}</h3><p class="desc">${t('stLangS')}</p>` +
    `<div class="field" style="max-width:320px"><select id="st-lang">${LANGS.map(([c, n]) => `<option value="${c}">${esc(n || t('stAuto'))}</option>`).join('')}</select></div></section>` +
    `<section class="set-section"><h3>${t('stTheme')}</h3><p class="desc">${t('stThemeS')}</p>` +
    `<div class="seg" id="st-theme">${[['auto', 'thAuto'], ['dark', 'thDark'], ['light', 'thLight'], ['custom', 'thCustom']].map(([k, l]) => `<button data-th="${k}">${esc(t(l))}</button>`).join('')}</div>` +
    `<div class="section-lbl">${t('stPresets')}</div><div class="presets" id="st-presets">${THEME_PRESETS.map(([l, bg, sf, ac, tx, mode], i) =>
      `<button class="preset" data-p="${i}"><span class="sw" style="background:${bg}"><i style="background:${ac}"></i></span>${esc(t(l))}</button>`).join('')}</div>` +
    `<div class="section-lbl">${t('stCustom')}</div><div class="colors" id="st-colors">${[['bg', 'cBg'], ['surface', 'cSurface'], ['accent', 'cAccent'], ['text', 'cText']].map(([k, l]) =>
      `<div class="color-field"><input type="color" data-c="${k}"><div class="fl"><label>${t(l)}</label><input type="text" data-h="${k}" maxlength="7" spellcheck="false"></div></div>`).join('')}</div></section>` +
    `<section class="set-section"><h3>${t('stTray')}</h3><div class="stack-gap" style="margin-top:10px">` +
    `<label class="switch"><input type="checkbox" id="st-tclose"><span class="txt"><b>${t('stTrayClose')}</b><span>${t('stTrayCloseS')}</span></span></label>` +
    `<label class="switch"><input type="checkbox" id="st-tlaunch"><span class="txt"><b>${t('stTrayLaunch')}</b><span>${t('stTrayLaunchS')}</span></span></label></div></section></div>` +
    `<div class="modal-foot"><span class="spacer"></span><button class="btn primary" data-close>${esc(t('stDone'))}</button></div>`;

  const modal = Modals.open({
    width: 600, html,
    onMount: (el, m) => {
      const q = (s) => el.querySelector(s);
      const HEX = /^#[0-9a-f]{6}$/i;

      const sync = () => {
        const s = S.settings;
        q('#st-lang').value = s.language;
        ddSync(q('#st-lang'));
        el.querySelectorAll('#st-theme button').forEach((b) => b.classList.toggle('on', b.dataset.th === s.theme));
        [['bg', s.bg], ['surface', s.surface], ['accent', s.accent], ['text', s.text]].forEach(([k, val]) => {
          const c = q(`[data-c="${k}"]`), h = q(`[data-h="${k}"]`);
          if (HEX.test(val)) c.value = val.toLowerCase();
          if (document.activeElement !== h) h.value = val;
        });
        q('#st-tclose').checked = !!s.trayClose;
        q('#st-tlaunch').checked = !!s.trayLaunch;
      };
      sync();

      q('#st-lang').addEventListener('change', (e) => send('setSetting', { key: 'language', value: e.target.value }));
      q('#st-theme').addEventListener('click', (e) => { const b = e.target.closest('[data-th]'); if (b) send('setSetting', { key: 'theme', value: b.dataset.th }); });
      q('#st-presets').addEventListener('click', (e) => {
        const b = e.target.closest('[data-p]'); if (!b) return;
        const [, bg, surface, accent, text, mode] = THEME_PRESETS[+b.dataset.p];
        send('applyPreset', { bg, surface, accent, text, mode });
      });

      let timer = 0;
      const pushColors = () => {
        const vals = {};
        for (const k of ['bg', 'surface', 'accent', 'text']) vals[k] = q(`[data-h="${k}"]`).value.trim();
        if (Object.values(vals).every((x) => HEX.test(x))) { clearTimeout(timer); timer = setTimeout(() => send('setCustomColors', vals), 90); }
      };
      q('#st-colors').addEventListener('input', (e) => {
        const c = e.target.closest('[data-c]'), h = e.target.closest('[data-h]');
        if (c) { q(`[data-h="${c.dataset.c}"]`).value = c.value.toUpperCase(); pushColors(); }
        else if (h) { let val = h.value.trim(); if (val && val[0] !== '#') { val = '#' + val; h.value = val; } if (HEX.test(val)) q(`[data-c="${h.dataset.h}"]`).value = val.toLowerCase(); pushColors(); }
      });
      q('#st-tclose').addEventListener('change', (e) => send('setSetting', { key: 'trayClose', value: e.target.checked }));
      q('#st-tlaunch').addEventListener('change', (e) => send('setSetting', { key: 'trayLaunch', value: e.target.checked }));

      m.onHost = (msg) => { if (msg.type === 'theme') sync(); };
    }
  });
  modal.reopen = openSettings; // a language change rebuilds the dialog in the new language
}
