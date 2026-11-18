// Section navigation accessibility attributes (permanent horizontal top bar).
(function () {
    var nav = document.querySelector('body > .app-subnav');
    if (!nav) return;
    nav.id = nav.id || 'section-navigation';
    nav.setAttribute('role', 'navigation');
    nav.setAttribute('aria-label', 'Secciones del sistema');
    document.body.classList.remove('sidebar-collapsed');
    nav.querySelectorAll('a.active').forEach(function (link) {
        link.setAttribute('aria-current', 'page');
    });
})();
