/* Presentation-only enhancements: Lico Celeste unified topbar, interactive Lico hero,
   step-01 accordion, tabletop management bar, dark/light theme & case inspection dialog.
   No network requests or case mutations. */
(function () {
    'use strict';

    document.documentElement.removeAttribute('data-theme');
    try { localStorage.removeItem('am_theme'); } catch (_) {}

    var imageMeta = document.querySelector('meta[name="lico-image"]');
    var licoSrc = (imageMeta && imageMeta.content && imageMeta.content.trim())
        ? imageMeta.content.trim()
        : '/images/lico-licencia-3d.webp';
    var header = document.querySelector('header.app-header');
    if (!header) return;

    var reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    var motionPaused = reducedMotion.matches;
    try {
        var savedMotion = localStorage.getItem('lico:motion-paused');
        if (savedMotion !== null) {
            motionPaused = savedMotion === 'true';
        } else if (localStorage.getItem('spatial-focus') === 'true') {
            motionPaused = true;
        }
    } catch (_) {}

    function canAnimate() {
        return !reducedMotion.matches && !motionPaused;
    }

    function animate(element, frames, options) {
        if (canAnimate() && element && typeof element.animate === 'function') {
            return element.animate(frames, options);
        }
    }

    function ensureLicoImage(container, width, height) {
        if (!container) return null;
        var svg = container.querySelector('svg');
        if (svg) svg.remove();
        var img = container.querySelector('img.lico-volume, img');
        if (!img) {
            img = document.createElement('img');
            img.className = 'lico-volume';
            img.src = licoSrc;
            img.alt = 'Lico';
            img.width = width || 160;
            img.height = height || 160;
            img.decoding = 'sync';
            img.loading = 'eager';
            container.prepend(img);
        } else {
            if (!img.getAttribute('src')) {
                img.setAttribute('src', licoSrc);
            }
            img.decoding = 'sync';
            img.loading = 'eager';
        }
        return img;
    }

    // Colorful SVG icons matching 3.- Ordenador de Carpetas topbar
    var TOPBAR_ICONS = {
        casos: '<svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M2.5 6.2A2.2 2.2 0 0 1 4.7 4h4.2c.7 0 1.3.3 1.7.9l1 1.3h7.7a2.2 2.2 0 0 1 2.2 2.2v9.4a2.2 2.2 0 0 1-2.2 2.2H4.7a2.2 2.2 0 0 1-2.2-2.2V6.2z" fill="#d97706"/><rect x="5" y="6.8" width="14" height="5.5" rx="1" fill="#ffffff"/><path d="M7.5 8.5h6M7.5 10.2h4" stroke="#94a3b8" stroke-width="0.9" stroke-linecap="round"/><path d="M2.2 10.2a1.8 1.8 0 0 1 1.8-1.6h16a1.8 1.8 0 0 1 1.8 1.6l-.7 8.1a2 2 0 0 1-2 1.8H4.9a2 2 0 0 1-2-1.8l-.7-8.1z" fill="#fbbf24"/></svg>',
        f8: '<svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><rect x="4" y="3" width="16" height="18" rx="2.5" fill="#fef3c7" stroke="#f59e0b" stroke-width="1.6"/><path d="M8 8h8M8 12h5" stroke="#b45309" stroke-width="1.6" stroke-linecap="round"/><circle cx="16.5" cy="15.5" r="3" fill="#ef4444"/><path d="M16.5 14.1v1.7M16.5 17.2h.01" stroke="#fff" stroke-width="1.4" stroke-linecap="round"/></svg>',
        ultima: '<svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><rect x="4" y="4" width="14" height="16" rx="2" fill="#99f6e4" stroke="#0d9488" stroke-width="1.4"/><rect x="7" y="6" width="13" height="15" rx="2" fill="#f0fdfa" stroke="#14b8a6" stroke-width="1.4"/><path d="M10 10h7M10 13.5h7M10 17h4" stroke="#0f766e" stroke-width="1.4" stroke-linecap="round"/></svg>',
        subidas: '<svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M6.5 18h11a4 4 0 0 0 .8-7.9A5.5 5.5 0 0 0 7.6 8.6 4.2 4.2 0 0 0 6.5 18z" fill="#bae6fd" stroke="#0284c7" stroke-width="1.5"/><path d="M12 16V10M9.3 12.6L12 9.8l2.7 2.8" fill="none" stroke="#0369a1" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        sincarp: '<svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M3 7a2 2 0 0 1 2-2h4.5l1.5 2H19a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7z" fill="#fecaca" stroke="#dc2626" stroke-width="1.4"/><path d="M9.5 11.5l5 5M14.5 11.5l-5 5" stroke="#991b1b" stroke-width="1.8" stroke-linecap="round"/></svg>',
        caja: '<svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><polygon points="3,8.5 14.5,9.8 14.5,20.5 3,18.7" fill="#f3cf90"/><polygon points="14.5,9.8 21,7.5 21,17.8 14.5,20.5" fill="#b97528"/><polygon points="2.2,6.4 9.5,3.6 21.8,5.3 14.6,8.3" fill="#fef0c7"/><polygon points="5.2,11.8 12.2,12.7 12.2,16.5 5.2,15.5" fill="#ffffff"/></svg>',
        stats: '<svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="4" width="18" height="16" rx="2.5" fill="#e0f2fe" stroke="#38bdf8" stroke-width="1.4"/><rect x="6.5" y="12" width="2.8" height="5" rx="0.8" fill="#0284c7"/><rect x="10.8" y="9" width="2.8" height="8" rx="0.8" fill="#14b8a6"/><rect x="15.1" y="7" width="2.8" height="10" rx="0.8" fill="#f59e0b"/></svg>'
    };

    var SHORT_NAV_LABELS = {
        'nav-casos': 'Casos',
        'nav-f8': 'F8',
        'nav-ultima-carpeta': 'Penúltima',
        'nav-subidas': 'Subidas',
        'nav-sin-carpetas': 'Sin Carp.',
        'nav-caja': 'Caja',
        'nav-estadisticas': 'Estadísticas'
    };

    var ICON_KEYS = {
        'nav-casos': 'casos',
        'nav-f8': 'f8',
        'nav-ultima-carpeta': 'ultima',
        'nav-subidas': 'subidas',
        'nav-sin-carpetas': 'sincarp',
        'nav-caja': 'caja',
        'nav-estadisticas': 'stats'
    };

    // ------------------------------------------------------------------
    // 1. UNIFIED SINGLE TOPBAR (header.app-header.topbar-salvia)
    // ------------------------------------------------------------------
    document.body.classList.add('lico-salvia');
    header.classList.add('topbar-salvia');

    // Extract functional elements before restructuring header
    var oldBrandLogo = header.querySelector('.brand-logo');
    var logoSrc = oldBrandLogo ? oldBrandLogo.getAttribute('src') : '/images/logo-municipalidad.png';
    var oldAppNav = header.querySelector('nav.app-nav');
    var btnGuiaGlobal = header.querySelector('#btn-guia-global');
    var searchForm = header.querySelector('form.nav-search');
    var filtersGroup = header.querySelector('.nav-group-filters');
    var toolsGroup = header.querySelector('.nav-group-tools');
    var syncForm = header.querySelector('#sync-form');
    var extraNavGroups = [];
    if (oldAppNav) {
        oldAppNav.querySelectorAll('.nav-group').forEach(function (g) {
            if (g !== filtersGroup && g !== toolsGroup && !g.classList.contains('nav-group-end')) {
                extraNavGroups.push(g);
            }
        });
    }
    // Also grab direct links inside oldAppNav (e.g. Sector.cshtml / SectorF8.cshtml)
    var extraDirectLinks = [];
    if (oldAppNav) {
        Array.from(oldAppNav.children).forEach(function (child) {
            if (child.tagName === 'A') {
                extraDirectLinks.push(child);
            }
        });
    }

    var subnav = document.querySelector('body > .app-subnav');
    var subnavMain = subnav ? subnav.querySelector('.app-subnav-main') : null;
    var subnavDocs = subnav ? subnav.querySelector('.app-subnav-docs') : null;

    var topbarContainer = document.createElement('div');
    topbarContainer.className = 'topbar-container';

    // 1A. Left: Brand
    var brandLink = document.createElement('a');
    brandLink.className = 'topbar-brand';
    brandLink.href = '/';
    brandLink.setAttribute('aria-label', 'Cambio de Domicilio · Departamento de Licencias de Conducir');
    brandLink.innerHTML =
        '<img src="' + logoSrc + '" alt="Municipalidad de Valparaíso" class="brand-municipal-logo" width="50" height="58">' +
        '<div class="brand-text">' +
            '<span class="brand-title">Cambio de Domicilio</span>' +
            '<span class="brand-sub">DEPARTAMENTO DE LICENCIAS DE CONDUCIR</span>' +
        '</div>';
    topbarContainer.appendChild(brandLink);

    // 1B. Center: Unified horizontal nav + "Más ▾" dropdown
    var topbarNav = document.createElement('nav');
    topbarNav.className = 'topbar-nav';
    topbarNav.setAttribute('role', 'navigation');
    topbarNav.setAttribute('aria-label', 'Navegación principal');

    if (subnavMain) {
        subnavMain.querySelectorAll('a').forEach(function (link) {
            link.classList.add('nav-item');
            var matchedClass = Object.keys(SHORT_NAV_LABELS).find(function (cls) {
                return link.classList.contains(cls);
            });
            if (matchedClass) {
                var fullText = (link.textContent || '').trim();
                if (!link.getAttribute('title') && fullText) {
                    link.setAttribute('title', fullText);
                }
                var labelSpan = link.querySelector('span:last-child');
                if (labelSpan) {
                    labelSpan.textContent = SHORT_NAV_LABELS[matchedClass];
                }
                var oldSvg = link.querySelector('svg');
                if (oldSvg && TOPBAR_ICONS[ICON_KEYS[matchedClass]]) {
                    oldSvg.outerHTML = TOPBAR_ICONS[ICON_KEYS[matchedClass]];
                }
            }
            if (link.classList.contains('active')) {
                link.setAttribute('aria-current', 'page');
            }
        });
        topbarNav.appendChild(subnavMain);
    }

    // Build "Más ▾" dropdown for secondary destinations (Comunas, Descartados, PDF Archivo, PDF Oficina 43, overflow)
    var moreDetails = document.createElement('details');
    moreDetails.className = 'topbar-more';
    var moreSummary = document.createElement('summary');
    moreSummary.className = 'nav-item topbar-more-btn';
    moreSummary.innerHTML = '<span>Más</span><span class="topbar-more-caret" aria-hidden="true">▾</span>';
    var moreMenu = document.createElement('div');
    moreMenu.className = 'topbar-more-menu';

    var overflowGroup = document.createElement('div');
    overflowGroup.className = 'topbar-more-overflow';
    moreMenu.appendChild(overflowGroup);

    if (toolsGroup) {
        moreMenu.appendChild(toolsGroup);
    }
    extraNavGroups.forEach(function (g) {
        // Skip redundant "Volver a Casos" if Casos tab is already visible; keep Comunas/Descartados/F8
        var links = Array.from(g.querySelectorAll('a'));
        links.forEach(function (a) {
            var txt = (a.textContent || '').trim();
            if (/volver a casos/i.test(txt)) {
                a.remove();
            }
        });
        if (g.querySelector('a')) {
            moreMenu.appendChild(g);
        }
    });
    if (extraDirectLinks.length > 0) {
        var extraDocsWrap = document.createElement('div');
        extraDocsWrap.className = 'topbar-more-extra';
        extraDirectLinks.forEach(function (a) {
            var txt = (a.textContent || '').trim();
            if (/cambio de domicilio/i.test(txt)) return;
            extraDocsWrap.appendChild(a);
        });
        if (extraDocsWrap.children.length > 0) {
            moreMenu.appendChild(extraDocsWrap);
        }
    }
    if (subnavDocs) {
        moreMenu.appendChild(subnavDocs);
    }

    if (moreMenu.querySelector('a.active')) {
        moreDetails.classList.add('has-active');
    }

    if (moreMenu.querySelectorAll('a').length > 0 || (subnavMain && subnavMain.children.length > 5)) {
        moreDetails.append(moreSummary, moreMenu);
        topbarNav.appendChild(moreDetails);
    }

    document.addEventListener('click', function (e) {
        if (moreDetails.open && !moreDetails.contains(e.target)) {
            moreDetails.open = false;
        }
    });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && moreDetails.open) {
            moreDetails.open = false;
        }
    });

    topbarContainer.appendChild(topbarNav);

    // 1C. Right: Actions in exact order (Sync pill, Guía con Lico, Pausar movimiento, Theme toggle, RSJ)
    var topbarActions = document.createElement('div');
    topbarActions.className = 'topbar-actions';

    if (syncForm) {
        var syncBtn = syncForm.querySelector('button');
        if (syncBtn) {
            syncBtn.className = 'sync-badge badge-ok nav-sync';
            syncBtn.title = 'Sincronizar correos ahora';
            syncBtn.innerHTML = '<span class="sync-dot" aria-hidden="true"></span><span>Sincronizado</span>';
        }
        topbarActions.appendChild(syncForm);
    } else {
        var syncBadge = document.createElement('span');
        syncBadge.id = 'syncBadge';
        syncBadge.className = 'sync-badge badge-ok';
        syncBadge.innerHTML = '<span class="sync-dot" aria-hidden="true"></span><span>Sincronizado</span>';
        topbarActions.appendChild(syncBadge);
    }

    if (btnGuiaGlobal) {
        var miniWrap = btnGuiaGlobal.querySelector('.lico-mini');
        if (miniWrap) {
            ensureLicoImage(miniWrap, 38, 36);
        }
        var btnTextSpan = btnGuiaGlobal.querySelector('span:last-child');
        if (btnTextSpan && btnTextSpan !== miniWrap) {
            btnTextSpan.textContent = 'Guía con Lico';
        }
        topbarActions.appendChild(btnGuiaGlobal);
    }

    var motionToggle = document.createElement('button');
    motionToggle.id = 'lico-motion-toggle';
    motionToggle.type = 'button';
    motionToggle.className = 'lico-motion-toggle';
    motionToggle.textContent = 'Pausar movimiento';
    topbarActions.appendChild(motionToggle);

    var rsjSignature = document.createElement('div');
    rsjSignature.className = 'rsj-signature';
    rsjSignature.setAttribute('role', 'img');
    rsjSignature.setAttribute('aria-label', 'RSJ — Raúl Salazar Jamen');
    rsjSignature.title = 'RSJ — Raúl Salazar Jamen';
    rsjSignature.innerHTML = '<rsj-brand aria-hidden="true"></rsj-brand>';
    topbarActions.appendChild(rsjSignature);

    topbarContainer.appendChild(topbarActions);

    // Replace old header children cleanly while keeping references we moved
    header.replaceChildren(topbarContainer);
    if (subnav) {
        subnav.hidden = true;
    }

    // Dynamic overflow safety so topbar NEVER wraps or overflows on 1280px–1440px
    var primaryNavLinks = subnavMain ? Array.from(subnavMain.querySelectorAll('a')) : [];
    function fitTopbarNav() {
        if (!subnavMain || !moreDetails || window.innerWidth <= 991) {
            primaryNavLinks.forEach(function (link) {
                if (link.parentElement !== subnavMain) subnavMain.appendChild(link);
            });
            return;
        }
        primaryNavLinks.forEach(function (link) {
            if (link.parentElement !== subnavMain) subnavMain.appendChild(link);
        });
        moreDetails.classList.toggle('has-active', !!moreMenu.querySelector('a.active'));
        var maxSafety = primaryNavLinks.length;
        while (maxSafety-- > 0 && (topbarContainer.scrollWidth > topbarContainer.clientWidth + 2 || topbarNav.scrollWidth > topbarNav.clientWidth + 2)) {
            var visibleLinks = Array.from(subnavMain.querySelectorAll('a'));
            if (visibleLinks.length <= 4) break;
            // Prefer moving non-active links near the end (keeping Casos & active link visible)
            var candidate = null;
            for (var i = visibleLinks.length - 1; i >= 1; i--) {
                if (!visibleLinks[i].classList.contains('active')) {
                    candidate = visibleLinks[i];
                    break;
                }
            }
            if (!candidate) candidate = visibleLinks[visibleLinks.length - 1];
            overflowGroup.prepend(candidate);
        }
        moreDetails.classList.toggle('has-active', !!moreMenu.querySelector('a.active'));
    }
    fitTopbarNav();
    window.addEventListener('resize', fitTopbarNav);

    // Motion toggle logic
    function applyMotionState() {
        document.body.classList.toggle('motion-paused', motionPaused);
        document.body.classList.toggle('focus-mode', motionPaused);
        motionToggle.textContent = motionPaused ? 'Activar movimiento' : 'Pausar movimiento';
        motionToggle.setAttribute('aria-pressed', String(motionPaused));
        motionToggle.title = motionPaused
            ? 'Activar animaciones en el rincón de Lico'
            : 'Pausar animaciones en el rincón de Lico';
        var sceneEl = document.querySelector('[data-lico-espacio]');
        if (motionPaused && sceneEl) {
            sceneEl.style.setProperty('--li-x', '0deg');
            sceneEl.style.setProperty('--li-y', '0deg');
        }
        window.dispatchEvent(new Event('resize'));
    }

    motionToggle.addEventListener('click', function () {
        motionPaused = !motionPaused;
        try {
            localStorage.setItem('lico:motion-paused', String(motionPaused));
            localStorage.setItem('spatial-focus', String(motionPaused));
        } catch (_) {}
        applyMotionState();
    });
    applyMotionState();

    // ------------------------------------------------------------------
    // 2. HERO ESCÉNICO COMPLETO (.lico-hero sobre section.page-head)
    // ------------------------------------------------------------------
    var route = '/' + (location.pathname.toLowerCase().split('/')[1] || '');
    if (route === '/index') route = '/';

    var HERO_COPY = {
        '/': {
            titleHtml: 'Cada carpeta,<br><em>un expediente bajo control.</em>',
            descHtml: 'Recepción, seguimiento y confirmación desde las comunas.<br>Tu bandeja de cambios de domicilio, en un solo lugar.',
            tableTitle: 'Carpetas del cambio de domicilio',
            tableSub: 'Busca, filtra y gestiona cada expediente.',
            speech: function (total, pending) {
                if (total === 0) return 'Soy Lico. Sincroniza o agrega tu primer expediente hoy.';
                if (pending > 0) return '¡Excelente orden! ' + total + ' carpetas en pantalla y ' + pending + ' pendientes por gestionar.';
                return '¡Todo al día! ' + total + ' carpetas en pantalla completamente gestionadas.';
            }
        },
        '/f8': {
            titleHtml: 'Casos F8,<br><em>seguimiento al día.</em>',
            descHtml: 'Carpetas físicas no encontradas en revisión.<br>Registra código F8, plazos y derivaciones sin perder trazabilidad.',
            tableTitle: 'Casos F8 pendientes',
            tableSub: 'Asigna código F8, fecha o deriva a Penúltima Carpeta.',
            speech: function (total) {
                return total === 0
                    ? 'Sin casos F8 pendientes en pantalla. ¡Excelente gestión!'
                    : 'Tenemos ' + total + ' caso(s) F8 en revisión. Registremos su código y fecha.';
            }
        },
        '/ultimacarpeta': {
            titleHtml: 'Penúltima Carpeta,<br><em>solicitudes a tiempo.</em>',
            descHtml: 'Nómina de casos F8 cuya penúltima carpeta debe pedirse.<br>Imprime el listado oficial, marca pedidos y deriva a Caja.',
            tableTitle: 'Nómina de penúltima carpeta',
            tableSub: 'Solicita, marca como pedida o deriva a Caja.',
            speech: function (total) {
                return 'Hay ' + total + ' carpeta(s) en Penúltima Carpeta listas para gestionar.';
            }
        },
        '/subidasasistema': {
            titleHtml: 'Subidas a Sistema,<br><em>listo para derivar.</em>',
            descHtml: 'Casos ya subidos o confirmados a Conaset.<br>Define su destino físico hacia Caja o cierre Sin Carpeta.',
            tableTitle: 'Carpetas subidas a sistema',
            tableSub: 'Asigna el destino físico final de cada caso confirmado.',
            speech: function (total) {
                return total === 0
                    ? 'No quedan carpetas por derivar en Subidas a Sistema.'
                    : '¡Buen avance! ' + total + ' carpeta(s) subidas listas para enviar a Caja.';
            }
        },
        '/sincarpetas': {
            titleHtml: 'Sin Carpetas,<br><em>archivo y respaldo.</em>',
            descHtml: 'Solicitudes cerradas sin carpeta física precedente.<br>Imprime el acta oficial o reincorpora si la carpeta aparece.',
            tableTitle: 'Casos cerrados sin carpeta',
            tableSub: 'Registro histórico y reincorporación si aparece la carpeta.',
            speech: function (total) {
                return 'Registro histórico con ' + total + ' caso(s) archivados sin carpeta física.';
            }
        },
        '/caja': {
            titleHtml: 'Caja,<br><em>listados y cierres ordenados.</em>',
            descHtml: 'Cola física de embalaje y cajas cerradas con correlativo.<br>Cierra, reabre o imprime el rótulo oficial de cada caja.',
            tableTitle: 'Gestión de cajas y embalaje',
            tableSub: 'Organiza la cola abierta y consulta cajas cerradas.',
            speech: function (total) {
                return total === 0
                    ? 'La cola de Caja está despejada. Puedes revisar las cajas cerradas abajo.'
                    : 'Hay ' + total + ' carpeta(s) en cola listas para cerrar caja e imprimir.';
            }
        },
        '/estadisticas': {
            titleHtml: 'Estadísticas,<br><em>indicadores al instante.</em>',
            descHtml: 'Volumen por comuna, plazos de confirmación y estados F8.<br>Visualiza el rendimiento completo de tu oficina.',
            tableTitle: 'Panel estadístico',
            tableSub: 'Métricas consolidadas en tiempo real.',
            speech: function () {
                return 'Aquí tienes los indicadores y gráficos actualizados del sistema.';
            }
        },
        '/comunas': {
            titleHtml: 'Directorio de comunas,<br><em>remitentes al día.</em>',
            descHtml: 'Dominios y correos oficiales para identificar solicitudes.<br>Mantén actualizado el enrutamiento de cada municipio.',
            tableTitle: 'Comunas registradas',
            tableSub: 'Administra dominios y correos de confirmación.',
            speech: function (total) {
                return 'Directorio activo con ' + total + ' contacto(s) de comunas configurados.';
            }
        },
        '/discarded': {
            titleHtml: 'Descartados,<br><em>correos por revisar.</em>',
            descHtml: 'Mensajes de remitentes no reconocidos en el directorio.<br>Revisa el motivo o registra la comuna correspondiente.',
            tableTitle: 'Correos descartados',
            tableSub: 'Revisa remitentes no reconocidos y su motivo.',
            speech: function (total) {
                return total === 0
                    ? 'Bandeja limpia: no hay correos descartados pendientes.'
                    : 'Hay ' + total + ' correo(s) descartados para revisar su remitente.';
            }
        },
        '/buscar': {
            titleHtml: 'Búsqueda global,<br><em>todo conectado.</em>',
            descHtml: 'Consulta por RUT o nombre en todos los módulos.<br>Localiza expedientes, cajas cerradas y derivaciones al instante.',
            tableTitle: 'Resultados de búsqueda',
            tableSub: 'Coincidencias encontradas en los módulos del sistema.',
            speech: function (total) {
                return 'Consulta transversal lista en todos los módulos del sistema.';
            }
        }
    };

    var copy = HERO_COPY[route] || {
        titleHtml: 'Cambio de Domicilio,<br><em>gestión ordenada.</em>',
        descHtml: 'Control integral de solicitudes, carpetas y despachos.<br>Tu oficina de licencias, en un solo lugar.',
        tableTitle: 'Registros del módulo',
        tableSub: 'Busca y gestiona los expedientes.',
        speech: function () { return 'Soy Lico. Gestionemos y resguardemos tus carpetas hoy.'; }
    };

    var pageHead = document.querySelector('main > section.page-head');
    var statRow = null;
    var mainCountNum = 0;
    var pendingCountNum = 0;

    if (pageHead) {
        statRow = pageHead.querySelector('.stat-row');
        if (statRow) {
            var nums = statRow.querySelectorAll('.num, b');
            if (nums[0]) mainCountNum = parseInt(nums[0].textContent, 10) || 0;
            if (nums[1]) pendingCountNum = parseInt(nums[1].textContent, 10) || 0;
            statRow.remove();
        } else {
            mainCountNum = document.querySelectorAll('table.cases tbody tr').length;
        }

        var existingLicoBtn = pageHead.querySelector('#btn-lico-saludo');
        if (!existingLicoBtn) {
            existingLicoBtn = document.createElement('button');
            existingLicoBtn.type = 'button';
            existingLicoBtn.id = 'btn-lico-saludo';
            existingLicoBtn.className = 'head-lico';
            existingLicoBtn.setAttribute('data-lico', 'saluda');
        }
        existingLicoBtn.className = 'head-lico';
        existingLicoBtn.setAttribute('data-lico-accion', 'saludar');
        existingLicoBtn.setAttribute('aria-label', 'Saludar a Lico');
        existingLicoBtn.replaceChildren();
        var heroImg = document.createElement('img');
        heroImg.className = 'lico-volume';
        heroImg.src = licoSrc;
        heroImg.alt = 'Lico, la mascota de licencias de conducir';
        heroImg.width = 768;
        heroImg.height = 512;
        heroImg.decoding = 'sync';
        heroImg.loading = 'eager';
        existingLicoBtn.appendChild(heroImg);

        pageHead.classList.add('lico-hero');
        pageHead.replaceChildren();

        var heroCopy = document.createElement('div');
        heroCopy.className = 'hero-copy';
        heroCopy.innerHTML =
            '<span class="hero-eyebrow"><span class="hero-status-dot" aria-hidden="true"></span><span>LICENCIAS DE CONDUCIR / VALPARAÍSO</span></span>' +
            '<h1 id="page-title">' + copy.titleHtml + '</h1>' +
            '<p class="hero-description">' + copy.descHtml + '</p>' +
            '<span class="hero-footnote">CON LICO, CADA EXPEDIENTE TIENE SU LUGAR.</span>';

        var scene = document.createElement('div');
        scene.className = 'li-scene lico-stage';
        scene.setAttribute('data-lico-espacio', '');
        scene.setAttribute('aria-label', 'El rincón de Lico');

        var aura = document.createElement('div');
        aura.className = 'salvia-aura';
        aura.setAttribute('aria-hidden', 'true');

        var plinth = document.createElement('div');
        plinth.className = 'salvia-plinth';
        plinth.setAttribute('aria-hidden', 'true');

        var liFigure = document.createElement('div');
        liFigure.className = 'li-figure li-mascot';
        var liStage = document.createElement('div');
        liStage.className = 'li-mascot-stage';
        liStage.appendChild(existingLicoBtn);
        liFigure.appendChild(liStage);

        var lampBtn = document.createElement('button');
        lampBtn.type = 'button';
        lampBtn.className = 'lico-objeto lico-lampara';
        lampBtn.setAttribute('data-lico-accion', 'luz');
        lampBtn.setAttribute('aria-label', 'Luz de escritorio de Lico');
        lampBtn.setAttribute('aria-pressed', 'true');
        lampBtn.innerHTML =
            '<span class="lico-lampara-dibujo" aria-hidden="true"><span class="lico-lampara-base"></span><span class="lico-lampara-brazo"></span><span class="lico-lampara-foco"></span></span>' +
            '<span class="lico-objeto-etiqueta" data-lico-luz-etiqueta>Luz encendida</span>';

        var folderBtn = document.createElement('button');
        folderBtn.type = 'button';
        folderBtn.className = 'lico-objeto lico-carpeta';
        folderBtn.setAttribute('data-lico-accion', 'carpetas');
        folderBtn.setAttribute('aria-label', 'Ir a los registros en pantalla');
        folderBtn.innerHTML =
            '<span class="lico-carpeta-dibujo" aria-hidden="true"><span class="lico-carpeta-hoja"></span><span class="lico-carpeta-tapa"></span><span class="lico-carpeta-sello">L</span></span>' +
            '<span class="lico-objeto-etiqueta">En pantalla <b>' + mainCountNum + '</b></span>';

        var restingMessage = copy.speech(mainCountNum, pendingCountNum);
        var bubble = document.createElement('div');
        bubble.className = 'studio-bubble lico-speech';
        bubble.id = 'licoSpeech';
        var bubbleMsg = document.createElement('p');
        bubbleMsg.id = 'lico-studio-message';
        bubbleMsg.setAttribute('aria-live', 'polite');
        bubbleMsg.textContent = restingMessage;
        bubble.appendChild(bubbleMsg);

        var caption = document.createElement('span');
        caption.className = 'li-scene-caption lico-hint';
        caption.textContent = 'TOCA A LICO · ABRE SU CARPETA · CAMBIA LA LUZ';

        scene.append(aura, plinth, liFigure, lampBtn, folderBtn, bubble, caption);
        pageHead.append(heroCopy, scene);

        // Hero interactions
        var GREETINGS = [
            '¡Hola! Soy Lico. Una carpeta a la vez, todo tiene su lugar.',
            '¡Aquí estoy! Tus expedientes de cambio de domicilio siempre ordenados.',
            'Usa el buscador o los filtros de la mesa para ubicar cualquier RUT al instante.',
            'Recuerda que puedes generar los listados PDF oficiales para Archivo y Oficina 43.',
            'Cada carpeta gestionada es un paso más hacia un archivo impecable.'
        ];
        var greetingIdx = 0;
        var msgTimer = null;
        var reactTimer = null;

        function sayLico(text, duration) {
            clearTimeout(msgTimer);
            bubbleMsg.textContent = text;
            var ms = typeof duration === 'number' ? duration : 5000;
            if (ms > 0) {
                msgTimer = setTimeout(function () {
                    if (bubbleMsg.textContent === text) {
                        bubbleMsg.textContent = restingMessage;
                    }
                }, ms);
            }
        }

        function stopReaction() {
            clearTimeout(reactTimer);
            scene.classList.remove('lico-saludando', 'lico-atento', 'lico-abre-carpeta');
        }

        function triggerReaction(cls) {
            if (!canAnimate() || document.hidden) return;
            stopReaction();
            void scene.offsetWidth;
            scene.classList.add(cls);
            reactTimer = setTimeout(stopReaction, 1500);
        }

        // Lamp state
        var lightOn = true;
        try {
            var savedLight = localStorage.getItem('lico:luz-encendida');
            if (savedLight !== null) lightOn = savedLight === 'true';
        } catch (_) {}

        function applyLamp() {
            scene.classList.toggle('lico-luz-apagada', !lightOn);
            lampBtn.setAttribute('aria-pressed', String(lightOn));
            var lbl = lampBtn.querySelector('[data-lico-luz-etiqueta]');
            if (lbl) lbl.textContent = lightOn ? 'Luz encendida' : 'Luz apagada';
        }
        applyLamp();

        scene.addEventListener('click', function (e) {
            var actionEl = e.target.closest('[data-lico-accion]');
            if (!actionEl || !scene.contains(actionEl)) return;
            var action = actionEl.getAttribute('data-lico-accion');
            if (action === 'saludar') {
                sayLico(GREETINGS[greetingIdx % GREETINGS.length]);
                greetingIdx++;
                triggerReaction('lico-saludando');
            } else if (action === 'luz') {
                lightOn = !lightOn;
                try { localStorage.setItem('lico:luz-encendida', String(lightOn)); } catch (_) {}
                applyLamp();
                sayLico(lightOn ? '¡Qué bien! Mi rincón vuelve a tener luz.' : 'Un poco más de calma. La luz está apagada.');
                triggerReaction('lico-atento');
            } else if (action === 'carpetas') {
                sayLico('Tienes ' + mainCountNum + ' registro(s) en pantalla para revisar.');
                triggerReaction('lico-abre-carpeta');
                var targetEl = document.querySelector('.records-shell, table.cases, #search-input');
                if (targetEl) {
                    targetEl.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            }
        });

        pageHead.addEventListener('pointermove', function (e) {
            if (!canAnimate() || e.pointerType === 'touch') return;
            var rect = pageHead.getBoundingClientRect();
            var xPct = (e.clientX - rect.left) / Math.max(1, rect.width);
            var yPct = (e.clientY - rect.top) / Math.max(1, rect.height);
            scene.style.setProperty('--li-x', ((0.5 - yPct) * 5).toFixed(2) + 'deg');
            scene.style.setProperty('--li-y', ((xPct - 0.5) * 7).toFixed(2) + 'deg');
        });
        pageHead.addEventListener('pointerleave', function () {
            scene.style.setProperty('--li-x', '0deg');
            scene.style.setProperty('--li-y', '0deg');
        });

        document.addEventListener('visibilitychange', function () {
            scene.classList.toggle('lico-en-reposo', document.hidden);
            if (document.hidden) stopReaction();
        });
    }

    // ------------------------------------------------------------------
    // 3. ACORDEÓN 01 (#manual-entry-panel en Index.cshtml)
    // ------------------------------------------------------------------
    var manualPanel = document.getElementById('manual-entry-panel');
    if (manualPanel) {
        manualPanel.classList.add('upload');
        var manualSummary = manualPanel.querySelector('summary.manual-entry-summary');
        if (manualSummary) {
            manualSummary.classList.add('sectiontitle');
            manualSummary.innerHTML =
                '<span class="step">01</span>' +
                '<div>' +
                    '<h3>Agregar caso(s) manualmente</h3>' +
                    '<p>Nombre, RUT, comuna y envío directo a caja, automáticamente.</p>' +
                '</div>' +
                '<span class="uploadchevron" aria-hidden="true">⌄</span>';
        }
    }

    // ------------------------------------------------------------------
    // 4. BARRA MESA DE GESTIÓN (.records-shell / .tabletop)
    // ------------------------------------------------------------------
    var mainEl = document.querySelector('main');
    if (mainEl && pageHead && (searchForm || statRow || filtersGroup)) {
        var recordsShell = document.createElement('section');
        recordsShell.className = 'card records-shell';

        var tabletop = document.createElement('div');
        tabletop.className = 'tabletop';

        var tabletopHeader = document.createElement('div');
        tabletopHeader.className = 'tabletop-header';
        tabletopHeader.innerHTML =
            '<h3>' + copy.tableTitle + '<span class="count">' + mainCountNum + '</span></h3>' +
            '<p>' + copy.tableSub + '</p>';

        var tabletopActions = document.createElement('div');
        tabletopActions.className = 'tabletop-actions';
        if (searchForm) {
            tabletopActions.appendChild(searchForm);
        }
        if (statRow) {
            tabletopActions.appendChild(statRow);
        }

        tabletop.append(tabletopHeader, tabletopActions);
        recordsShell.appendChild(tabletop);

        if (filtersGroup) {
            var filterNav = document.createElement('nav');
            filterNav.className = 'spatial-filters';
            filterNav.setAttribute('aria-label', 'Filtrar solicitudes por estado');
            filterNav.appendChild(filtersGroup);
            recordsShell.appendChild(filterNav);
        }

        var anchor = mainEl.querySelector('.card.table-card')
            || mainEl.querySelector('.caja-section')
            || mainEl.querySelector('.card-form')
            || mainEl.querySelector('.review-hint')
            || mainEl.querySelector('.stats-grid')
            || mainEl.querySelector('.sin-carpetas-toolbar')
            || mainEl.querySelector('.card');

        if (anchor && anchor.parentElement === mainEl) {
            mainEl.insertBefore(recordsShell, anchor);
        } else {
            pageHead.insertAdjacentElement('afterend', recordsShell);
        }
    } else if (filtersGroup && pageHead) {
        var standaloneFilterNav = document.createElement('nav');
        standaloneFilterNav.className = 'spatial-filters';
        standaloneFilterNav.setAttribute('aria-label', 'Filtrar solicitudes por estado');
        standaloneFilterNav.appendChild(filtersGroup);
        pageHead.insertAdjacentElement('afterend', standaloneFilterNav);
    } else if (!pageHead && searchForm && mainEl) {
        var printToolbar = mainEl.querySelector('div.no-print');
        if (printToolbar) {
            searchForm.style.marginLeft = 'auto';
            printToolbar.appendChild(searchForm);
        }
    }

    // ------------------------------------------------------------------
    // 5. MODAL DE FICHA DEL REGISTRO (#case-detail-dialog)
    // ------------------------------------------------------------------
    var dialog = document.createElement('dialog');
    if (typeof dialog.showModal !== 'function') return;
    dialog.className = 'case-dialog';
    dialog.id = 'case-detail-dialog';
    dialog.setAttribute('aria-labelledby', 'case-detail-title');
    dialog.innerHTML =
        '<div class="case-dialog-head">' +
            '<div><small>RSJ / VISTA DEL REGISTRO</small><h2 id="case-detail-title"></h2></div>' +
            '<button type="button" class="case-dialog-close" aria-label="Cerrar ficha">×</button>' +
        '</div>' +
        '<dl></dl>' +
        '<div class="case-dialog-footer">' +
            '<img alt="" width="52" height="52">' +
            '<p>Consulta los datos sin salir de la lista. Para modificarlos, cierra esta ficha y utiliza los campos de la tabla.</p>' +
        '</div>';
    dialog.querySelector('img').src = licoSrc;
    document.body.appendChild(dialog);

    dialog.querySelector('.case-dialog-close').addEventListener('click', function () {
        dialog.close();
    });
    dialog.addEventListener('click', function (event) {
        if (event.target !== dialog) return;
        var bounds = dialog.getBoundingClientRect();
        if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) {
            dialog.close();
        }
    });

    document.querySelectorAll('table.cases td.name-cell').forEach(function (cell) {
        var name = cell.querySelector('input[name="nombre"]');
        if (!name) return;
        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'case-inspect';
        button.textContent = '↗';
        button.title = 'Abrir ficha del registro';
        button.setAttribute('aria-label', 'Abrir ficha de ' + (name.value || 'este caso'));
        button.setAttribute('aria-haspopup', 'dialog');
        button.setAttribute('aria-controls', dialog.id);
        var nameGroup = cell.querySelector('.name-with-alert');
        (nameGroup || cell).prepend(button);
    });

    document.addEventListener('click', function (event) {
        var button = event.target.closest('.case-inspect');
        if (!button) return;
        var row = button.closest('tr');
        var table = row.closest('table');
        var headings = table.querySelectorAll('thead tr:last-child th');
        var name = row.querySelector('input[name="nombre"]');
        dialog.querySelector('h2').textContent = name ? name.value : 'Ficha del registro';
        var fields = dialog.querySelector('dl');
        fields.replaceChildren();
        Array.from(row.cells).forEach(function (cell, index) {
            var heading = headings[index];
            if (!heading || cell.querySelector('.action-group') || cell.querySelector('button[type="submit"]')) return;
            var label = heading.innerText.replace(/\s+/g, ' ').trim();
            if (!label || /acci[oó]n/i.test(label)) return;
            var inputs = cell.querySelectorAll('input:not([type="hidden"]), select, textarea');
            var value = inputs.length ? Array.from(inputs).map(function (input) {
                if (input.type === 'checkbox') return input.checked ? 'Sí' : 'No';
                if (input.type === 'date' && input.value) return input.value.split('-').reverse().join('-');
                return input.value;
            }).join(' · ') : cell.textContent.trim();
            var field = document.createElement('div');
            field.className = 'case-dialog-field';
            var term = document.createElement('dt');
            var detail = document.createElement('dd');
            term.textContent = label;
            detail.textContent = value || 'Sin información';
            field.append(term, detail);
            fields.appendChild(field);
        });
        dialog.showModal();
        animate(dialog, [
            { transform: 'perspective(900px) translateY(20px) rotateX(-6deg) scale(.97)', opacity: 0.5 },
            { transform: 'perspective(900px) translateY(0) rotateX(0) scale(1)', opacity: 1 }
        ], { duration: 280, easing: 'cubic-bezier(.2,.85,.25,1)' });
    });
})();
