/* Start-up: inside WebView2 talk to the host; in a plain browser load the design-time mock. */
'use strict';

(function boot() {
  renderAll();

  if (host) {
    host.addEventListener('message', (e) => onHost(e.data));
    send('ready');
    return;
  }

  const s = document.createElement('script');
  s.src = 'dev-mock.js';
  s.onload = () => window.__mockInit && window.__mockInit();
  document.head.appendChild(s);
})();
