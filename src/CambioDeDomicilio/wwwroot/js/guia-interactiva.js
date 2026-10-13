/**
 * guia-interactiva.js - Guided tour (spotlight + Lico mascot) for the CambioDeDomicilio dashboard.
 * Based on the "Lico" theme kit (tematica-lico/). It already bundles the Lico mascot (licoSvg),
 * so lico.js must NOT be loaded next to it. Replaces the former system-tour.js.
 *
 * Multi-page: each step declares the page (`pagina`) where it lives; moving to a step from another
 * page navigates there with ?guia=1&paso=N and the tour resumes on arrival.
 * UI text is Spanish (end users); code and comments are English.
 */
(function () {
  'use strict';

  var tourPasoActual = 0;
  var tourKeyHandler = null;

  // Section label shown in the step badge, keyed by normalized route (see rutaActual).
  var SECCIONES = {
    '/': 'Cambio de Domicilio',
    '/f8': 'F8',
    '/ultimacarpeta': 'Última Carpeta',
    '/subidasasistema': 'Subidas a Sistema',
    '/sincarpetas': 'Sin Carpetas',
    '/caja': 'Caja',
    '/comunas': 'Comunas',
    '/discarded': 'Descartados',
    '/estadisticas': 'Estadísticas'
  };

  // pagina: normalized route where the step lives. pose: Lico pose (saluda | explica | alerta | celebra).
  // target: CSS selector (comma-separated alternatives allowed); fallbackTarget is used when it is missing.
  var PASOS_TOUR = [
    // ---------- CAMBIO DE DOMICILIO (route "/") ----------
    {
      pagina: '/', pose: 'saluda',
      target: '#btn-guia-global',
      titulo: '¡Hola! Me llamo Lico',
      icono: '👋',
      descripcion: 'Soy tu guía. Este botón (y yo, en el encabezado) abre esta guía cuando quieras. Las comunas piden carpetas por correo; el sistema <b>lee ese correo</b>, crea un <b>caso por contribuyente</b> y lo acompaña por Cambio de Domicilio, Subidas a Sistema o F8, y por último Caja o Sin Carpetas. Usa <b>Siguiente / Anterior</b> o las flechas del teclado; <b>Esc</b> cierra la guía.'
    },
    {
      pagina: '/',
      target: '.app-subnav-main',
      titulo: 'Los sistemas',
      icono: '🧭',
      descripcion: 'Aquí saltas entre pantallas: <b>Cambio de Domicilio</b>, <b>F8</b>, <b>Última Carpeta</b>, <b>Subidas a Sistema</b>, <b>Sin Carpetas</b>, <b>Caja</b> y <b>Estadísticas</b>. La pantalla donde estás aparece resaltada.'
    },
    {
      pagina: '/', pose: 'explica',
      target: '.nav-sync',
      fallbackTarget: '.app-header',
      titulo: 'Sincronizar ahora (la entrada de datos)',
      icono: '🔄',
      descripcion: 'La lectura del correo es <b>solo manual</b>: no hay sincronización automática. Al pulsar este botón el sistema lee <b>CARP. PARA PEDIR</b> (casos nuevos), <b>CARP. YA SUBIDAS</b> (marca subidos), revisa la bandeja por <b>rebotes</b> y actualiza el reporte CSV.'
    },
    {
      pagina: '/',
      target: '.nav-group-filters',
      fallbackTarget: '.app-header',
      titulo: 'Filtros y "Requiere revisión"',
      icono: '🏷️',
      descripcion: 'Filtra por <b>Todos</b>, <b>Pendientes</b>, <b>Subidos</b>, <b>Confirmados</b>, <b>Requiere revisión</b> y <b>Rebotados</b>. En revisión quedan los casos donde el correo no trajo nombre o RUT válido: debes digitarlos y no se pueden confirmar hasta corregirlos.'
    },
    {
      pagina: '/',
      target: '.nav-search',
      fallbackTarget: '.app-header',
      titulo: 'Búsqueda por RUT o nombre',
      icono: '🔍',
      descripcion: 'Escribe un <b>RUT</b> (se formatea solo) o un nombre. Si el caso ya avanzó a F8, Subidas, Caja o Sin Carpetas, el sistema te indica <b>dónde está</b> (incluso la caja y el N°) y te lleva a su fila resaltada.'
    },
    {
      pagina: '/',
      target: '.nav-group-tools',
      fallbackTarget: '.app-header',
      titulo: 'Comunas y Descartados',
      icono: '🗺️',
      descripcion: '<b>Comunas</b> es el directorio con el que se reconoce quién pide una carpeta (por el dominio del remitente). <b>Descartados</b> guarda los correos no reconocidos, con su motivo. Las visitaremos más adelante.'
    },
    {
      pagina: '/',
      target: '#panel-contadores',
      fallbackTarget: '.page-head',
      titulo: 'Contadores de la lista',
      icono: '📊',
      descripcion: 'Resumen de lo que estás viendo: casos en pantalla, <b>pendientes</b>, los que <b>requieren revisión</b> y los <b>rebotados</b>. Cambian según el filtro o la búsqueda activa.'
    },
    {
      pagina: '/', pose: 'explica',
      target: '#manual-entry-panel',
      fallbackTarget: 'main',
      titulo: 'Agregar casos a mano',
      icono: '➕',
      descripcion: 'Despliega este panel para ingresar contribuyentes manualmente. Marcando <b>Carpeta antigua</b> el caso va <b>directo a la cola de Caja</b> sin enviar correo a la comuna: sirve para carpetas viejas que nunca se registraron.'
    },
    {
      pagina: '/',
      target: '.table-card',
      fallbackTarget: 'main',
      titulo: 'Colores de las filas',
      icono: '🎨',
      descripcion: '<b>Blanco</b>: sin ninguna acción. <b>Gris</b>: acción terminada (subido, confirmado, en Caja o cerrado). <b>Amarillo</b>: casilla <b>Marcar</b>. <b>Morado</b>: <b>Pendiente carpeta</b>. <b>Rojo</b>: requiere revisión o rebotó. Marcar y Pendiente son solo para organizarte.'
    },
    {
      pagina: '/', pose: 'alerta',
      target: '.table-card',
      fallbackTarget: 'main',
      titulo: 'Plazo de 15 días hábiles',
      icono: '⏳',
      descripcion: 'Cada caso tiene <b>15 días hábiles</b> (lunes a viernes) desde que se recibió. El indicador va en <b>verde</b>; pasa a <b>ámbar</b> con 7 días o menos y a <b>rojo</b> con 3 o menos o vencido. Deja de contar cuando el caso ya está subido o confirmado.'
    },
    {
      pagina: '/',
      target: '.fecha-input, .sector-cell, .table-card',
      fallbackTarget: 'main',
      titulo: 'Fecha de última carpeta y sector',
      icono: '📅',
      descripcion: 'Escribe la fecha como <b>15 marzo 2024</b> o <b>15/03/2024</b>. El sector sale solo: antes del <b>1 de julio de 2023</b> es <b>Archivo</b>; desde esa fecha, <b>Oficina 43</b>. Con <b>S/C</b> en F8 el caso se cierra sin carpeta.'
    },
    {
      pagina: '/', pose: 'celebra',
      target: '.btn-action--upload, .action-group, .table-card',
      fallbackTarget: 'main',
      titulo: 'Marcar subida (confirmar a la comuna)',
      icono: '📤',
      descripcion: 'Al pulsar <b>Marcar subida</b> el sistema mueve el correo a <b>CARP. YA SUBIDAS</b>, marca el caso como Subido y Confirmado y <b>envía el correo de confirmación</b> a la comuna. Si fue un error existe <b>Rectificar</b>: envía una rectificación y vuelve el caso a Pendiente.'
    },
    {
      pagina: '/',
      target: '.app-subnav-docs',
      fallbackTarget: '.app-subnav',
      titulo: 'Listados PDF: Archivo y Oficina 43',
      icono: '📄',
      descripcion: 'Genera las nóminas para ir a buscar carpetas, separadas por <b>Archivo</b> u <b>Oficina 43</b>. Solo entran los casos con la casilla <b>Marcar</b>. Se imprimen o guardan como PDF desde el navegador.'
    },

    // ---------- F8 ----------
    {
      pagina: '/f8', pose: 'alerta',
      target: '.table-card',
      fallbackTarget: 'main',
      titulo: 'F8: carpeta física no encontrada',
      icono: '🚨',
      descripcion: 'Aquí llegan los casos con la casilla <b>F8</b>. Registras el <b>código F8</b> y la fecha de la penúltima carpeta. Desde F8 puedes <b>Marcar subida</b>, enviar a <b>Última carpeta</b> para pedir la última carpeta, cerrar <b>Sin carpeta</b> o <b>Revertir</b> sin perder lo digitado.'
    },

    // ---------- ULTIMA CARPETA ----------
    {
      pagina: '/ultimacarpeta', pose: 'explica',
      target: '.table-card',
      fallbackTarget: 'main',
      titulo: 'Última Carpeta: pedir la última carpeta',
      icono: '🗂️',
      descripcion: 'Aquí llegan los casos de F8 con <b>Última carpeta</b>. Imprime la lista para pedir cada carpeta y marca <b>Pedida</b>. Si la carpeta llega, <b>Caja</b> la manda a la cola; si no existe, <b>Sin carpeta</b> la cierra en Sin Carpetas, donde termina el trámite.'
    },

    // ---------- SUBIDAS A SISTEMA ----------
    {
      pagina: '/subidasasistema',
      target: '.table-card',
      fallbackTarget: 'main',
      titulo: 'Subidas a Sistema',
      icono: '☁️',
      descripcion: 'Lista de los casos ya <b>subidos o confirmados</b>: fechas, estado digital y confirmación. Desde aquí decides su destino físico: <b>Caja</b>, <b>Sin carpeta</b> o <b>Rectificar</b>.'
    },

    // ---------- CAJA ----------
    {
      pagina: '/caja', pose: 'explica',
      target: '.queue-table, .table-card',
      fallbackTarget: 'main',
      titulo: 'Caja: embalaje físico',
      icono: '📦',
      descripcion: 'Las carpetas llegan a una <b>cola</b> en orden de llegada. Escribes el <b>rótulo o N° de caja</b> y pulsas <b>Cerrar Caja</b>. Las cajas cerradas se pueden <b>imprimir</b>, <b>reabrir</b> o quitarles una carpeta; <b>Devolver a casos</b> saca carpetas de la cola.'
    },

    // ---------- SIN CARPETAS ----------
    {
      pagina: '/sincarpetas',
      target: '.table-card',
      fallbackTarget: 'main',
      titulo: 'Sin Carpetas: cierre definitivo',
      icono: '🗄️',
      descripcion: 'Aquí quedan los casos en que <b>no se encontró la carpeta física</b>. Es un listado final <b>sin acciones</b>: solo se puede <b>imprimir o guardar como PDF</b>. Es el cierre del trámite.'
    },

    // ---------- COMUNAS ----------
    {
      pagina: '/comunas',
      target: '.card-form',
      fallbackTarget: 'main',
      titulo: 'Directorio de comunas',
      icono: '🗺️',
      descripcion: 'Agrega una comuna con su <b>dominio</b> y <b>correo de contacto</b>. El sistema reconoce una solicitud por el dominio del remitente. Puedes <b>editar o eliminar</b> comunas en la tabla de abajo; los cambios rigen desde la próxima sincronización.'
    },

    // ---------- DESCARTADOS ----------
    {
      pagina: '/discarded', pose: 'alerta',
      target: '.review-hint',
      fallbackTarget: 'main',
      titulo: 'Descartados: correos no reconocidos',
      icono: '🗑️',
      descripcion: 'Si el remitente no está en el directorio (o comparte dominio con varias comunas sin estar registrado), el correo queda aquí con su <b>motivo</b>. Al registrar la comuna, la próxima sincronización crea el caso y lo quita de Descartados.'
    },

    // ---------- ESTADÍSTICAS ----------
    {
      pagina: '/estadisticas', pose: 'celebra',
      target: '.stats-grid',
      fallbackTarget: 'main',
      titulo: 'Estadísticas',
      icono: '📈',
      descripcion: 'Gráficos de casos por estado, ingresos por semana, tiempo promedio de confirmación, comunas con más volumen, sector Archivo vs Oficina 43, plazos F8 y correos descartados por motivo. Eso es todo: ¡puedes volver a abrirme cuando quieras!'
    }
  ];


  // ---- Lico: mascota licencia de conducir (SVG generado, sin archivos externos) ----
  function licoSvg(pose) {
    var INK = '#23262a', AZ = '#26388c';
    var brazo = function (d) {
      return '<path d="' + d + '" stroke="' + AZ + '" stroke-width="9" fill="none" stroke-linecap="round"/>';
    };
    var mano = function (x, y, cls) {
      return '<circle ' + (cls ? 'class="' + cls + '" ' : '') + 'cx="' + x + '" cy="' + y + '" r="9" fill="#fff" stroke="' + AZ + '" stroke-width="3.5"/>';
    };
    var ojos = function (dx) {
      dx = dx || 0;
      return '<g class="lico-ojos">' +
        '<circle cx="' + (66 + dx) + '" cy="92" r="9" fill="' + INK + '"/><circle cx="' + (69 + dx) + '" cy="89" r="3" fill="#fff"/>' +
        '<circle cx="' + (114 + dx) + '" cy="92" r="9" fill="' + INK + '"/><circle cx="' + (117 + dx) + '" cy="89" r="3" fill="#fff"/></g>';
    };
    var bocaAbierta = '<path d="M72 108Q90 134 108 108Z" fill="#e8503f" stroke="' + INK + '" stroke-width="3" stroke-linejoin="round"/>';
    var izq = '', der = '', cara = '', extra = '';

    if (pose === 'celebra') {
      izq = brazo('M24 104Q-2 90 6 54') + mano(6, 50);
      der = brazo('M156 104Q182 90 174 54') + mano(174, 50);
      cara = '<path d="M57 96q9-14 18 0M105 96q9-14 18 0" stroke="' + INK + '" stroke-width="4" fill="none" stroke-linecap="round"/>' +
        '<path d="M70 106Q90 140 110 106Z" fill="#e8503f" stroke="' + INK + '" stroke-width="3" stroke-linejoin="round"/>';
      extra = '<rect x="30" y="8" width="8" height="14" rx="2" fill="#1fb5d9" transform="rotate(20 34 15)"/>' +
        '<rect x="188" y="30" width="8" height="14" rx="2" fill="#e889c4" transform="rotate(-25 192 37)"/>' +
        '<rect x="90" y="0" width="8" height="14" rx="2" fill="#f6d23c" transform="rotate(35 94 7)"/>' +
        '<circle cx="204" cy="86" r="5" fill="#2fb56a"/><circle cx="14" cy="22" r="5" fill="#e8503f"/>';
    } else if (pose === 'alerta') {
      izq = brazo('M24 104Q6 120 12 142') + mano(12, 144);
      der = brazo('M156 100Q184 88 184 62') + mano(184, 58);
      cara = '<g class="lico-ojos"><circle cx="66" cy="94" r="9" fill="' + INK + '"/><circle cx="114" cy="94" r="9" fill="' + INK + '"/>' +
        '<circle cx="69" cy="91" r="3" fill="#fff"/><circle cx="117" cy="91" r="3" fill="#fff"/></g>' +
        '<path d="M54 76l24 6M126 76l-24 6" stroke="' + INK + '" stroke-width="4" stroke-linecap="round"/>' +
        '<path d="M74 118q8-10 16 0t16 0" stroke="' + INK + '" stroke-width="4" fill="none" stroke-linecap="round"/>';
      extra = '<path d="M158 62q8 12 0 18q-8-6 0-18z" fill="#1fb5d9"/>' +
        '<circle cx="196" cy="34" r="18" fill="#fff" stroke="#e8503f" stroke-width="5"/>' +
        '<path d="M196 24v11l8 5" stroke="#e8503f" stroke-width="4" fill="none" stroke-linecap="round"/>';
    } else if (pose === 'explica') {
      izq = brazo('M24 104Q6 120 12 142') + mano(12, 144);
      der = brazo('M156 104Q190 108 204 90') + mano(206, 86);
      cara = ojos(3) + '<path d="M74 108Q90 126 106 108Z" fill="#e8503f" stroke="' + INK + '" stroke-width="3" stroke-linejoin="round"/>';
    } else { // saluda
      izq = brazo('M24 104Q6 120 12 142') + mano(12, 144);
      der = '<g class="lico-saludo">' + brazo('M156 100Q184 88 180 60') + mano(180, 56) + '</g>';
      cara = ojos(0) + bocaAbierta;
    }

    return '<svg class="lico-svg" viewBox="-6 -4 232 228" role="img" aria-label="Lico, la mascota de licencias de conducir">' +
      '<ellipse cx="90" cy="214" rx="52" ry="7" fill="rgba(27,33,80,.18)"/>' +
      '<g class="lico-cuerpo">' +
        '<rect x="52" y="160" width="14" height="30" rx="7" fill="' + AZ + '"/><rect x="104" y="160" width="14" height="30" rx="7" fill="' + AZ + '"/>' +
        '<ellipse cx="58" cy="194" rx="16" ry="9" fill="#f6d23c" stroke="' + AZ + '" stroke-width="3.5"/>' +
        '<ellipse cx="112" cy="194" rx="16" ry="9" fill="#f6d23c" stroke="' + AZ + '" stroke-width="3.5"/>' +
        izq +
        '<rect x="20" y="20" width="140" height="150" rx="24" fill="#fff" stroke="' + AZ + '" stroke-width="5"/>' +
        '<path d="M22.5 60V46a21.5 21.5 0 0 1 21.5-21.5h92a21.5 21.5 0 0 1 21.5 21.5V60z" fill="' + AZ + '"/>' +
        '<circle cx="44" cy="42" r="5" fill="#1fb5d9"/><circle cx="58" cy="42" r="5" fill="#2fb56a"/>' +
        '<circle cx="72" cy="42" r="5" fill="#e889c4"/><circle cx="86" cy="42" r="5" fill="#f6d23c"/>' +
        '<rect x="110" y="38" width="34" height="8" rx="4" fill="#fff" opacity=".9"/>' +
        cara +
        '<ellipse cx="48" cy="114" rx="9" ry="6" fill="#e889c4" opacity=".7"/><ellipse cx="132" cy="114" rx="9" ry="6" fill="#e889c4" opacity=".7"/>' +
        '<text x="44" y="147" font-family="Segoe UI, system-ui, sans-serif" font-size="17" font-weight="800" letter-spacing="1.5" fill="' + AZ + '">LICO</text>' +
        '<rect x="44" y="154" width="40" height="6" rx="3" fill="#d9dcee"/>' +
        '<rect x="110" y="140" width="26" height="18" rx="4" fill="#f6d23c" stroke="#c9a400" stroke-width="2"/>' +
        der + extra +
      '</g></svg>';
  }

  function poseParaPaso(i) {
    return PASOS_TOUR[i].pose || 'explica';
  }

  // Ruta actual normalizada: '/', '/comunas', '/estadisticas', '/configuracion'.
  function rutaActual() {
    // Keep only the first path segment: Caja accepts a trailing box id (/Caja/12).
    var p = '/' + (location.pathname.toLowerCase().split('/')[1] || '');
    if (p === '/' || p === '/index') return '/';
    return p;
  }

  function seccionDe(paso) {
    return SECCIONES[paso.pagina] || '';
  }

  // Primer paso que pertenece a la página donde está el usuario.
  function primerPasoDePaginaActual() {
    var ruta = rutaActual();
    for (var i = 0; i < PASOS_TOUR.length; i++) {
      if (PASOS_TOUR[i].pagina === ruta) return i;
    }
    return 0;
  }

  // Cambia de paso; si el paso vive en otra página, navega y la guía continúa allá.
  function irAPaso(idx) {
    if (idx < 0 || idx >= PASOS_TOUR.length) return;
    if (PASOS_TOUR[idx].pagina !== rutaActual()) {
      cerrarGuiaInteractiva();
      location.href = PASOS_TOUR[idx].pagina + '?guia=1&paso=' + idx;
      return;
    }
    tourPasoActual = idx;
    renderPasoTour();
  }

  function montarLicos() {
    var els = document.querySelectorAll('[data-lico]');
    for (var i = 0; i < els.length; i++) {
      if (!els[i].querySelector('svg')) {
        els[i].insertAdjacentHTML('afterbegin', licoSvg(els[i].getAttribute('data-lico')));
      }
    }
  }

  function iniciarGuiaInteractiva(pasoInicial) {
    var tip = document.getElementById('tooltip-flotante');
    if (tip) tip.classList.remove('visible');

    cerrarGuiaInteractiva();
    tourPasoActual = (typeof pasoInicial === 'number' && pasoInicial >= 0 && pasoInicial < PASOS_TOUR.length) ? pasoInicial : 0;

    var overlay = document.createElement('div');
    overlay.id = 'tour-overlay';
    overlay.className = 'tour-overlay';
    overlay.innerHTML =
      '<button type="button" class="tour-btn-salir-flotante" id="tour-salir-flotante" title="Terminar y cerrar la guía">' +
        '<span>✕</span> Cerrar guía' +
      '</button>' +
      '<div id="tour-spotlight" class="tour-spotlight"></div>' +
      '<div id="tour-card" class="tour-card" role="dialog" aria-modal="true" aria-labelledby="tour-titulo">' +
        '<div class="tour-card-header">' +
          '<span class="tour-paso-badge" id="tour-badge">Paso 1 de ' + PASOS_TOUR.length + '</span>' +
          '<button type="button" class="tour-btn-cerrar" id="tour-cerrar" title="Cerrar guía" aria-label="Cerrar guía">&times;</button>' +
        '</div>' +
        '<div class="tour-card-body">' +
          '<div class="tour-mascota" id="tour-mascota"></div>' +
          '<div class="tour-card-texto">' +
            '<h3 id="tour-titulo" class="tour-card-titulo"></h3>' +
            '<p id="tour-desc" class="tour-card-desc"></p>' +
          '</div>' +
        '</div>' +
        '<div class="tour-card-footer">' +
          '<button type="button" class="tour-btn-nav" id="tour-prev">Anterior</button>' +
          '<div class="tour-dots" id="tour-dots"></div>' +
          '<button type="button" class="tour-btn-nav tour-btn-primary" id="tour-next">Siguiente</button>' +
        '</div>' +
        '<div id="tour-flecha" class="tour-flecha"></div>' +
      '</div>';

    document.body.appendChild(overlay);

    document.getElementById('tour-cerrar').onclick = cerrarGuiaInteractiva;
    document.getElementById('tour-salir-flotante').onclick = cerrarGuiaInteractiva;
    overlay.onclick = function (e) {
      if (e.target === overlay) cerrarGuiaInteractiva();
    };

    tourKeyHandler = function (e) {
      if (e.key === 'Escape') {
        cerrarGuiaInteractiva();
      } else if (e.key === 'ArrowRight' && tourPasoActual < PASOS_TOUR.length - 1) {
        irAPaso(tourPasoActual + 1);
      } else if (e.key === 'ArrowLeft' && tourPasoActual > 0) {
        irAPaso(tourPasoActual - 1);
      }
    };
    window.addEventListener('keydown', tourKeyHandler);

    document.getElementById('tour-prev').onclick = function () {
      if (tourPasoActual > 0) irAPaso(tourPasoActual - 1);
    };

    document.getElementById('tour-next').onclick = function () {
      if (tourPasoActual < PASOS_TOUR.length - 1) {
        irAPaso(tourPasoActual + 1);
      } else {
        cerrarGuiaInteractiva();
        mostrarToast('¡Guía interactiva completada!');
      }
    };

    renderPasoTour(true);
    // Keyboard users land on the primary action; the dialog is modal (see role/aria-modal above).
    var botonSiguiente = document.getElementById('tour-next');
    if (botonSiguiente) botonSiguiente.focus();
  }

  function renderPasoTour(esPrimerRender) {
    var paso = PASOS_TOUR[tourPasoActual];
    var target = document.querySelector(paso.target) || (paso.fallbackTarget ? document.querySelector(paso.fallbackTarget) : null) || document.querySelector('main');
    if (!target) return;

    var badge = document.getElementById('tour-badge');
    var titulo = document.getElementById('tour-titulo');
    var desc = document.getElementById('tour-desc');
    var prev = document.getElementById('tour-prev');
    var next = document.getElementById('tour-next');
    var dots = document.getElementById('tour-dots');

    if (badge) badge.textContent = 'Paso ' + (tourPasoActual + 1) + ' de ' + PASOS_TOUR.length + ' · ' + seccionDe(paso);
    if (titulo) titulo.innerHTML = '<span class="tour-ico">' + paso.icono + '</span> ' + paso.titulo;
    if (desc) desc.innerHTML = paso.descripcion;
    var mascota = document.getElementById('tour-mascota');
    if (mascota) mascota.innerHTML = licoSvg(poseParaPaso(tourPasoActual));
    if (prev) prev.disabled = tourPasoActual === 0;

    var esUltimo = tourPasoActual === PASOS_TOUR.length - 1;
    if (next) {
      if (esUltimo) {
        next.textContent = '✔ ¡Finalizar!';
        next.style.background = '#10b981';
        next.style.borderColor = '#059669';
        next.style.color = '#fff';
        next.style.fontWeight = '700';
      } else {
        next.textContent = 'Siguiente';
        next.style.background = '';
        next.style.borderColor = '';
        next.style.color = '';
        next.style.fontWeight = '';
      }
    }

    if (dots) {
      // Barra de progreso: con decenas de pasos, los puntos ya no caben.
      dots.innerHTML = '<span class="tour-progreso" role="progressbar" aria-valuemin="1" aria-valuemax="' + PASOS_TOUR.length +
        '" aria-valuenow="' + (tourPasoActual + 1) + '"><i style="width:' + Math.round((tourPasoActual + 1) * 100 / PASOS_TOUR.length) + '%"></i></span>';
    }

    var posicionar = function () {
      var r = target.getBoundingClientRect();
      var spot = document.getElementById('tour-spotlight');
      var card = document.getElementById('tour-card');
      var flecha = document.getElementById('tour-flecha');
      if (!spot || !card) return;

      var cardW = Math.min(540, window.innerWidth - 32);
      var cardH = 260;
      var pad = 6;
      var esGrilla = paso.target === '.table-responsive' || r.height > window.innerHeight * 0.55;

      if (esPrimerRender) {
        spot.style.transition = 'none';
        card.style.transition = 'none';
      }

      if (esGrilla) {
        var spotTop = Math.max(12, Math.round(r.top));
        var spotH = Math.min(Math.round(r.height), window.innerHeight - spotTop - 24);
        spot.style.left = Math.max(10, Math.round(r.left - pad)) + 'px';
        spot.style.top = spotTop + 'px';
        spot.style.width = Math.min(window.innerWidth - 20, Math.round(r.width + pad * 2)) + 'px';
        spot.style.height = Math.max(220, spotH) + 'px';

        var cardLeft = Math.round((window.innerWidth - cardW) / 2);
        var cardTop = Math.round(Math.max(80, (window.innerHeight - cardH) / 2));
        card.style.left = cardLeft + 'px';
        card.style.top = cardTop + 'px';
        if (flecha) flecha.style.display = 'none';
      } else {
        if (flecha) flecha.style.display = 'block';

        // Spotlight regular
        spot.style.left = Math.max(0, Math.round(r.left - pad)) + 'px';
        spot.style.top = Math.max(0, Math.round(r.top - pad)) + 'px';
        spot.style.width = Math.round(r.width + pad * 2) + 'px';
        spot.style.height = Math.round(r.height + pad * 2) + 'px';
        // Elementos más altos que la pantalla: el recuadro se recorta al área visible.
        var spotTopPx = parseInt(spot.style.top, 10);
        var maxH = window.innerHeight - spotTopPx - 12;
        if (parseInt(spot.style.height, 10) > maxH) spot.style.height = Math.max(120, maxH) + 'px';

        // Posicionar tarjeta
        var cLeft = Math.round(r.left + (r.width / 2) - (cardW / 2));
        if (cLeft < 16) cLeft = 16;
        if (cLeft + cardW > window.innerWidth - 16) cLeft = window.innerWidth - cardW - 16;

        var cTop = Math.round(r.bottom + 14);
        var flechaArriba = true;

        if (cTop + cardH > window.innerHeight - 16) {
          cTop = Math.round(r.top - cardH - 14);
          flechaArriba = false;
        }

        if (cTop < 16) cTop = 16;
        if (cTop + cardH > window.innerHeight - 16) cTop = window.innerHeight - cardH - 16;

        card.style.left = cLeft + 'px';
        card.style.top = cTop + 'px';

        if (flecha) {
          flecha.className = 'tour-flecha ' + (flechaArriba ? 'flecha-arriba' : 'flecha-abajo');
          var flechaX = Math.max(24, Math.min(cardW - 36, (r.left + r.width / 2) - cLeft));
          flecha.style.left = Math.round(flechaX) + 'px';
        }
      }

      if (esPrimerRender) {
        void card.offsetHeight;
        spot.style.transition = '';
        card.style.transition = '';
      }

      spot.classList.add('visible');
      card.classList.add('visible');
    };

    var rect = target.getBoundingClientRect();
    var yaVisible = rect.top >= 0 && rect.bottom <= window.innerHeight;

    if (yaVisible) {
      posicionar();
    } else {
      // Instantáneo: el spotlight se mide después del desplazamiento, no durante.
      target.scrollIntoView({ behavior: 'auto', block: 'center' });
      setTimeout(posicionar, 60);
    }
  }

  function cerrarGuiaInteractiva() {
    if (tourKeyHandler) {
      window.removeEventListener('keydown', tourKeyHandler);
      tourKeyHandler = null;
    }
    var o = document.getElementById('tour-overlay');
    if (o) o.remove();
  }

  function mostrarToast(mensaje) {
    var toast = document.getElementById('tour-toast');
    if (!toast) {
      toast = document.createElement('div');
      toast.id = 'tour-toast';
      toast.className = 'tour-toast';
      document.body.appendChild(toast);
    }
    toast.innerHTML = '<span>✔</span> ' + mensaje;
    toast.classList.add('visible');
    setTimeout(function () {
      toast.classList.remove('visible');
    }, 3200);
  }

  function iniciarTooltipsGlobales() {
    var tip = document.getElementById('tooltip-flotante');
    if (!tip) {
      tip = document.createElement('div');
      tip.id = 'tooltip-flotante';
      tip.className = 'tooltip-flotante';
      document.body.appendChild(tip);
    }

    document.addEventListener('mouseover', function (e) {
      var el = e.target.closest('[data-tooltip]');
      if (!el) {
        tip.classList.remove('visible');
        return;
      }
      var texto = el.getAttribute('data-tooltip');
      if (!texto) return;
      tip.textContent = texto;
      tip.classList.add('visible');

      var rect = el.getBoundingClientRect();
      var tipRect = tip.getBoundingClientRect();
      var left = rect.left + rect.width / 2 - tipRect.width / 2;
      if (left < 10) left = 10;
      if (left + tipRect.width > window.innerWidth - 10) left = window.innerWidth - tipRect.width - 10;
      var top = rect.bottom + 8;
      tip.classList.remove('pos-arriba');
      if (top + tipRect.height > window.innerHeight - 8) {
        top = rect.top - tipRect.height - 8;
        tip.classList.add('pos-arriba');
      }
      tip.style.left = Math.round(left) + 'px';
      tip.style.top = Math.round(top) + 'px';
    });

    document.addEventListener('mouseout', function (e) {
      var el = e.target.closest('[data-tooltip]');
      if (el) {
        tip.classList.remove('visible');
      }
    });
  }

  // Inicializar eventos de navegación y botón de guía
  document.addEventListener('DOMContentLoaded', function () {
    iniciarTooltipsGlobales();
    montarLicos();

    var btnLico = document.getElementById('btn-lico-saludo');
    if (btnLico) btnLico.addEventListener('click', function () { iniciarGuiaInteractiva(primerPasoDePaginaActual()); });

    // El botón global inicia la guía desde el primer paso de la página donde estás.
    var btnGuiaGlobal = document.getElementById('btn-guia-global');
    if (btnGuiaGlobal) {
      btnGuiaGlobal.addEventListener('click', function () {
        iniciarGuiaInteractiva(primerPasoDePaginaActual());
      });
    }

    // Si viene de otra página de la guía (?guia=1&paso=N), continúa en ese paso.
    var params = new URLSearchParams(location.search);
    if (params.get('guia') === '1') {
      var paso = parseInt(params.get('paso'), 10);
      if (isNaN(paso) || paso < 0 || paso >= PASOS_TOUR.length || PASOS_TOUR[paso].pagina !== rutaActual()) {
        paso = primerPasoDePaginaActual();
      }
      if (window.history && window.history.replaceState) {
        params.delete('guia');
        params.delete('paso');
        var qs = params.toString();
        window.history.replaceState({}, document.title, location.pathname + (qs ? '?' + qs : ''));
      }
      setTimeout(function () { iniciarGuiaInteractiva(paso); }, 300);
    }
  });

  // Exponer API global
  window.iniciarGuiaInteractiva = iniciarGuiaInteractiva;
  window.cerrarGuiaInteractiva = cerrarGuiaInteractiva;

})();
