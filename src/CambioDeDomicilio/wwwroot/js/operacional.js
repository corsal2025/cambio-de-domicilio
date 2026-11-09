// Navigation preference is optional; storage restrictions must not prevent use.
(function () {
    var nav = document.querySelector('body > .app-subnav');
    var header = document.querySelector('header.app-header');
    if (!nav || !header) return;
    nav.id = nav.id || 'section-navigation';
    nav.setAttribute('role', 'navigation');
    nav.setAttribute('aria-label', 'Secciones del sistema');
    var button = document.createElement('button');
    button.type = 'button';
    button.className = 'sidebar-toggle';
    button.setAttribute('aria-controls', nav.id);
    var collapsed = false;
    try { collapsed = localStorage.getItem('navigation-collapsed') === 'true'; } catch (_) {}
    function update() {
        document.body.classList.toggle('sidebar-collapsed', collapsed);
        button.setAttribute('aria-expanded', String(!collapsed));
        button.textContent = collapsed ? 'Mostrar menú' : 'Ocultar menú';
        window.dispatchEvent(new Event('resize'));
    }
    button.addEventListener('click', function () {
        collapsed = !collapsed;
        try { localStorage.setItem('navigation-collapsed', String(collapsed)); } catch (_) {}
        update();
    });
    header.prepend(button);
    nav.querySelectorAll('a.active').forEach(function (link) { link.setAttribute('aria-current', 'page'); });
    update();
})();
