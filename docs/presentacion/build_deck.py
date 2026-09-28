import json, pathlib, re

# Regenerates docs/presentacion-modelo-dominio.html from the slide sources in docs/presentacion/project.
# Run from anywhere: python docs/presentacion/build_deck.py
root = pathlib.Path(__file__).resolve().parent.parent
deck = json.loads((root / 'presentacion' / 'project' / 'deck.json').read_text(encoding='utf-8'))

arrow = ('<svg width="96" height="48" viewBox="0 0 96 48" aria-hidden="true" style="flex:none">'
         '<path d="M0 16 H58 V0 L96 24 L58 48 V32 H0 Z" fill="#0F2A44"/></svg>')

slides = []
for sid in deck['order']:
    html = (root / 'presentacion' / 'project' / 'slides' / f'{sid}.html').read_text(encoding='utf-8')
    html = re.sub(r'<x-shape kind="arrow-right"[^>]*></x-shape>', arrow, html)
    notes = re.search(r'<aside>(.*?)</aside>', html, re.S)
    html = re.sub(r'<aside>.*?</aside>', '', html, flags=re.S)
    html = html.replace('<section id="', '<section class="slide" data-notes="%s" id="' % (
        (notes.group(1).strip() if notes else '').replace('"', '&quot;')), 1)
    slides.append(html.strip())

page = '''<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="author" content="Raúl Salazar">
<title>Modelo de Dominio — Cambio de Domicilio (presentación)</title>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Libre+Baskerville:wght@400;700&family=Public+Sans:wght@400;600;700&display=swap">
<style>
  html, body { margin: 0; height: 100%; background: #0B1723; overflow: hidden; }
  body { font-family: 'Public Sans', 'Segoe UI', Arial, sans-serif; }
  #stage { position: absolute; left: 50%; top: 50%; width: 1920px; height: 1080px; transform-origin: center center; }
  .slide { position: absolute; inset: 0; width: 1920px; height: 1080px; box-sizing: border-box; }
  .slide:not(.current) { display: none !important; }
  .slide h1, .slide h2, .slide h3, .slide p, .slide ul, .slide table { margin: 0; }
  .slide table { border-collapse: collapse; width: 100%; }
  .slide th, .slide td { padding: 14px 18px; border-bottom: 1px solid #DDE3E8; text-align: left; }
  .slide th { font-weight: 700; }
  #bar { position: fixed; left: 0; right: 0; bottom: 0; display: flex; justify-content: space-between; align-items: center;
         padding: 8px 16px; color: #C9D6E3; font-size: 13px; background: rgba(11, 23, 35, .85); }
  #bar button { font: inherit; color: #F6F7F4; background: #1F3A55; border: 0; border-radius: 6px; padding: 6px 12px; cursor: pointer; }
  #bar button:focus-visible { outline: 2px solid #7DD3A8; }
  #notes { max-width: 60%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  body.clean #bar { display: none; }
</style>
</head>
<body>
<div id="stage">
''' + '\n'.join(slides) + '''
</div>
<div id="bar">
  <div><button id="prev" aria-label="Diapositiva anterior">←</button> <button id="next" aria-label="Diapositiva siguiente">→</button> <span id="count"></span></div>
  <span id="notes"></span>
  <span>Autor: Raúl Salazar · F pantalla completa · H oculta esta barra</span>
</div>
<script>
(function () {
  var slides = Array.prototype.slice.call(document.querySelectorAll('.slide'));
  var stage = document.getElementById('stage');
  var i = 0;
  var fromHash = parseInt((location.hash || '').replace('#', ''), 10);
  if (fromHash >= 1 && fromHash <= slides.length) i = fromHash - 1;
  function fit() {
    var s = Math.min(window.innerWidth / 1920, window.innerHeight / 1080);
    stage.style.transform = 'translate(-50%, -50%) scale(' + s + ')';
  }
  function show(n) {
    i = Math.max(0, Math.min(slides.length - 1, n));
    slides.forEach(function (sl, k) { sl.classList.toggle('current', k === i); });
    document.getElementById('count').textContent = (i + 1) + ' / ' + slides.length;
    document.getElementById('notes').textContent = slides[i].getAttribute('data-notes') || '';
    history.replaceState(null, '', '#' + (i + 1));
  }
  document.getElementById('prev').onclick = function () { show(i - 1); };
  document.getElementById('next').onclick = function () { show(i + 1); };
  document.addEventListener('keydown', function (e) {
    if (e.key === 'ArrowRight' || e.key === 'PageDown' || e.key === ' ') { e.preventDefault(); show(i + 1); }
    if (e.key === 'ArrowLeft' || e.key === 'PageUp') { e.preventDefault(); show(i - 1); }
    if (e.key === 'Home') show(0);
    if (e.key === 'End') show(slides.length - 1);
    if (e.key === 'f' || e.key === 'F') { if (!document.fullscreenElement) document.documentElement.requestFullscreen().catch(function () {}); else document.exitFullscreen(); }
    if (e.key === 'h' || e.key === 'H') document.body.classList.toggle('clean');
  });
  document.getElementById('stage').addEventListener('click', function () { show(i + 1); });
  window.addEventListener('resize', fit);
  fit(); show(i);
})();
</script>
</body>
</html>
'''
out = root / 'presentacion-modelo-dominio.html'
out.write_text(page, encoding='utf-8')
print(out, len(slides))
