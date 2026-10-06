/**
 * system-tour.js — Simulador Interactivo con Spotlight y Guía con Flecha Explicativa
 * Sistema de Gestión de Carpetas 2.0 (CambioDeDomicilio)
 * Municipalidad de Valparaíso
 */

(function () {
    let tourPasoActual = 0;
    let tourKeyHandler = null;

    const PASOS_TOUR = [
        {
            target: '.app-subnav-main',
            titulo: '1. Flujo y Módulos del Sistema',
            icono: '🧭',
            descripcion: 'Desde esta barra gestionas el ciclo de vida completo: <b>Cambio de Domicilio</b> (bandeja de entrada), <b>F8</b> (búsqueda urgente en bodega), <b>Subidas a Sistema</b> (solicitudes subidas a Conaset), <b>Sin Carpetas</b> (archivo digital), <b>Caja</b> (embalaje físico foliado) y <b>Estadísticas</b>.'
        },
        {
            target: '.nav-group-filters',
            titulo: '2. Filtros de Estados',
            icono: '🏷️',
            descripcion: 'Clasifica tus solicitudes al instante: <b>Todos</b>, <b>Pendientes</b> de procesar, <b>Subidos</b> a sistema, <b>Confirmados</b> y <b>Requiere revisión</b> (aquellos cuyos datos del correo necesitan corrección manual).'
        },
        {
            target: '.nav-search',
            titulo: '3. Búsqueda Inteligente por RUT',
            icono: '🔍',
            descripcion: 'Busca en segundos por <b>RUT</b> o nombre del contribuyente. Si el caso ya avanzó a F8, Subidas o Caja, el sistema detecta su ubicación y te ofrece un botón de salto directo.'
        },
        {
            target: '.fecha-input, .sector-cell, th:nth-child(10), .table-card',
            titulo: '4. Fecha de Última Carpeta y Sector',
            icono: '📅',
            descripcion: 'Ingresa la fecha en formato <code>dd/mm/aaaa</code>. El sistema clasifica automáticamente el sector: carpetas anteriores al 2000 van a <b>Archivo Histórico</b>, mientras que del 2000 en adelante van a <b>Oficina 43</b>.'
        },
        {
            target: '.btn-action--upload, form.no-f8-only, .action-group, .table-card',
            titulo: '5. Marcar Subida (Avanzar a Subidas a Sistema)',
            icono: '📤',
            descripcion: 'Una vez ingresados los datos en Conaset, pulsa <b>Marcar subida</b>. El caso se retira de la bandeja principal y se traslada a <b>Subidas a Sistema</b> para definir su entrega física o cierre.'
        },
        {
            target: '.f8-cell, .btn-action--f8, th:nth-child(4), .table-card',
            titulo: '6. Traspaso a F8 (Carpeta no encontrada)',
            icono: '🚨',
            descripcion: 'Si la carpeta física no se localiza en la estantería, marca la casilla <b>F8</b> o presiona <b>Traspaso a F8</b> para derivarla al módulo de búsqueda urgente en bodega y asignarle código de rastreo.'
        },
        {
            target: '.app-subnav-docs',
            titulo: '7. Generación de Listados PDF',
            icono: '📄',
            descripcion: 'Genera las nóminas oficiales en PDF para <b>Archivo</b> u <b>Oficina 43</b> con los casos marcados, listas para imprimir y entregar a los estanteros.'
        },
        {
            target: '.nav-sin-carpetas, .nav-caja, .app-subnav-main',
            titulo: '8. Cierre Definitivo: Caja o Sin Carpeta',
            icono: '📦',
            descripcion: 'Todo trámite termina en uno de dos destinos: <b>Caja</b> si existe carpeta de papel para embalar con rótulo numerado oficial, o <b>Sin Carpetas</b> si el contribuyente no requirió carpeta física.'
        }
    ];

    function iniciarGuiaInteractiva() {
        const tip = document.getElementById('tooltip-flotante');
        if (tip) tip.classList.remove('visible');

        cerrarGuiaInteractiva();
        tourPasoActual = 0;

        const overlay = document.createElement('div');
        overlay.id = 'tour-overlay';
        overlay.className = 'tour-overlay';
        overlay.innerHTML = `
            <button class="tour-btn-salir-flotante" id="tour-salir-flotante" title="Terminar y cerrar la guía">
                <span>✕</span> Cerrar guía
            </button>
            <div id="tour-spotlight" class="tour-spotlight"></div>
            <div id="tour-card" class="tour-card">
                <div class="tour-card-header">
                    <span class="tour-paso-badge" id="tour-badge">Paso 1 de ${PASOS_TOUR.length}</span>
                    <button class="tour-btn-cerrar" id="tour-cerrar" title="Cerrar guía">&times;</button>
                </div>
                <div class="tour-card-body">
                    <h3 id="tour-titulo" class="tour-card-titulo"></h3>
                    <p id="tour-desc" class="tour-card-desc"></p>
                </div>
                <div class="tour-card-footer">
                    <button class="tour-btn-nav" id="tour-prev">Anterior</button>
                    <div class="tour-dots" id="tour-dots"></div>
                    <button class="tour-btn-nav tour-btn-primary" id="tour-next">Siguiente</button>
                </div>
                <div id="tour-flecha" class="tour-flecha"></div>
            </div>`;
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
                tourPasoActual++;
                renderPasoTour();
            } else if (e.key === 'ArrowLeft' && tourPasoActual > 0) {
                tourPasoActual--;
                renderPasoTour();
            }
        };
        window.addEventListener('keydown', tourKeyHandler);

        document.getElementById('tour-prev').onclick = function () {
            if (tourPasoActual > 0) {
                tourPasoActual--;
                renderPasoTour();
            }
        };

        document.getElementById('tour-next').onclick = function () {
            if (tourPasoActual < PASOS_TOUR.length - 1) {
                tourPasoActual++;
                renderPasoTour();
            } else {
                cerrarGuiaInteractiva();
                mostrarToastFinal();
            }
        };

        renderPasoTour(true);
    }

    function renderPasoTour(esPrimerRender = false) {
        const paso = PASOS_TOUR[tourPasoActual];
        let target = null;

        // Intentar los selectores especificados en target
        const selectores = paso.target.split(',');
        for (let s of selectores) {
            const found = document.querySelector(s.trim());
            if (found && found.offsetParent !== null) {
                target = found;
                break;
            }
        }

        // Fallback si no está el elemento específico
        if (!target) {
            target = document.querySelector('.table-card') || document.querySelector('.cases') || document.querySelector('.app-header');
        }
        if (!target) return;

        document.getElementById('tour-badge').textContent = `Paso ${tourPasoActual + 1} de ${PASOS_TOUR.length}`;
        document.getElementById('tour-titulo').innerHTML = `<span class="tour-ico">${paso.icono}</span> ${paso.titulo}`;
        document.getElementById('tour-desc').innerHTML = paso.descripcion;
        document.getElementById('tour-prev').disabled = tourPasoActual === 0;

        const esUltimo = tourPasoActual === PASOS_TOUR.length - 1;
        const btnNext = document.getElementById('tour-next');
        if (esUltimo) {
            btnNext.textContent = '✔ ¡Finalizar!';
            btnNext.style.background = '#10b981';
            btnNext.style.borderColor = '#059669';
            btnNext.style.color = '#fff';
            btnNext.style.fontWeight = '700';
        } else {
            btnNext.textContent = 'Siguiente';
            btnNext.style.background = '';
            btnNext.style.borderColor = '';
            btnNext.style.color = '';
            btnNext.style.fontWeight = '';
        }

        // Dots
        document.getElementById('tour-dots').innerHTML = PASOS_TOUR.map((_, i) =>
            `<span class="tour-dot ${i === tourPasoActual ? 'activo' : ''}"></span>`
        ).join('');

        const posicionar = () => {
            const r = target.getBoundingClientRect();
            const spot = document.getElementById('tour-spotlight');
            const card = document.getElementById('tour-card');
            const flecha = document.getElementById('tour-flecha');
            if (!spot || !card) return;

            const cardW = 390;
            const cardH = 260;
            const pad = 6;
            const esGrilla = r.height > window.innerHeight * 0.55 || r.width > window.innerWidth * 0.85;

            if (esPrimerRender) {
                spot.style.transition = 'none';
                card.style.transition = 'none';
            }

            if (esGrilla) {
                const spotTop = Math.max(12, Math.round(r.top));
                const spotH = Math.min(Math.round(r.height), window.innerHeight - spotTop - 24);
                spot.style.left = `${Math.max(10, Math.round(r.left - pad))}px`;
                spot.style.top = `${spotTop}px`;
                spot.style.width = `${Math.min(window.innerWidth - 20, Math.round(r.width + pad * 2))}px`;
                spot.style.height = `${Math.max(220, spotH)}px`;

                const cardLeft = Math.round((window.innerWidth - cardW) / 2);
                const cardTop = Math.round(Math.max(80, (window.innerHeight - cardH) / 2));
                card.style.left = `${cardLeft}px`;
                card.style.top = `${cardTop}px`;
                if (flecha) flecha.style.display = 'none';
            } else {
                if (flecha) flecha.style.display = 'block';

                // Spotlight regular
                spot.style.left = `${Math.max(0, Math.round(r.left - pad))}px`;
                spot.style.top = `${Math.max(0, Math.round(r.top - pad))}px`;
                spot.style.width = `${Math.round(r.width + pad * 2)}px`;
                spot.style.height = `${Math.round(r.height + pad * 2)}px`;

                // Posicionar tarjeta
                let cardLeft = Math.round(r.left + (r.width / 2) - (cardW / 2));
                if (cardLeft < 16) cardLeft = 16;
                if (cardLeft + cardW > window.innerWidth - 16) cardLeft = window.innerWidth - cardW - 16;

                let cardTop = Math.round(r.bottom + 14);
                let flechaArriba = true;

                // Si se sale por abajo, colocar arriba del elemento
                if (cardTop + cardH > window.innerHeight - 16) {
                    cardTop = Math.round(r.top - cardH - 14);
                    flechaArriba = false;
                }

                if (cardTop < 16) cardTop = 16;
                if (cardTop + cardH > window.innerHeight - 16) cardTop = window.innerHeight - cardH - 16;

                card.style.left = `${cardLeft}px`;
                card.style.top = `${cardTop}px`;

                if (flecha) {
                    flecha.className = `tour-flecha ${flechaArriba ? 'flecha-arriba' : 'flecha-abajo'}`;
                    const flechaX = Math.max(24, Math.min(cardW - 36, (r.left + r.width / 2) - cardLeft));
                    flecha.style.left = `${Math.round(flechaX)}px`;
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

        const rect = target.getBoundingClientRect();
        const yaVisible = rect.top >= 0 && rect.bottom <= window.innerHeight;

        if (yaVisible || esPrimerRender) {
            posicionar();
        } else {
            target.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            setTimeout(posicionar, 100);
        }
    }

    function cerrarGuiaInteractiva() {
        if (tourKeyHandler) {
            window.removeEventListener('keydown', tourKeyHandler);
            tourKeyHandler = null;
        }
        const o = document.getElementById('tour-overlay');
        if (o) o.remove();
    }

    function mostrarToastFinal() {
        const toast = document.createElement('div');
        toast.style.cssText = `
            position: fixed;
            bottom: 24px;
            right: 24px;
            background: #0f172a;
            color: #ffffff;
            border-left: 4px solid #10b981;
            border-radius: 8px;
            padding: 12px 20px;
            font-size: 14px;
            font-weight: 600;
            box-shadow: 0 10px 25px rgba(0,0,0,0.3);
            z-index: 100000;
            opacity: 0;
            transform: translateY(10px);
            transition: all 0.3s ease;
        `;
        toast.innerHTML = '🎉 ¡Guía interactiva completada! Ya conoces el flujo del sistema.';
        document.body.appendChild(toast);
        requestAnimationFrame(() => {
            toast.style.opacity = '1';
            toast.style.transform = 'translateY(0)';
        });
        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateY(10px)';
            setTimeout(() => toast.remove(), 300);
        }, 4000);
    }

    /* ================= INICIALIZACIÓN DE TOOLTIPS GLOBALES ================= */
    function iniciarTooltipsGlobales() {
        let tip = document.getElementById('tooltip-flotante');
        if (!tip) {
            tip = document.createElement('div');
            tip.id = 'tooltip-flotante';
            tip.className = 'tooltip-flotante';
            document.body.appendChild(tip);
        }

        document.addEventListener('mouseover', function (e) {
            const el = e.target.closest('[data-tooltip]');
            if (!el) {
                tip.classList.remove('visible');
                return;
            }
            const texto = el.getAttribute('data-tooltip');
            if (!texto) return;
            tip.textContent = texto;
            tip.classList.add('visible');

            const rect = el.getBoundingClientRect();
            const tipRect = tip.getBoundingClientRect();
            let left = rect.left + rect.width / 2 - tipRect.width / 2;
            if (left < 10) left = 10;
            if (left + tipRect.width > window.innerWidth - 10) left = window.innerWidth - tipRect.width - 10;
            let top = rect.bottom + 8;
            if (top + tipRect.height > window.innerHeight - 8) {
                top = rect.top - tipRect.height - 8;
                tip.classList.add('pos-arriba');
            } else {
                tip.classList.remove('pos-arriba');
            }
            tip.style.left = `${Math.round(left)}px`;
            tip.style.top = `${Math.round(top)}px`;
        });

        document.addEventListener('mouseout', function (e) {
            const el = e.target.closest('[data-tooltip]');
            if (el && !e.relatedTarget?.closest('[data-tooltip]')) {
                tip.classList.remove('visible');
            }
        });
    }

    // Inicializar listeners al cargar el DOM
    document.addEventListener('DOMContentLoaded', function () {
        iniciarTooltipsGlobales();

        // Enlazar botones de inicio de la guía
        document.querySelectorAll('#btn-guia-global, .btn-guia-accion, .btn-start-tour').forEach(btn => {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                const path = window.location.pathname.toLowerCase();
                const esPrincipal = path === '/' || path === '' || path === '/index';

                if (!esPrincipal) {
                    window.location.href = '/?simulador=1';
                } else {
                    iniciarGuiaInteractiva();
                }
            });
        });

        // Detectar si la URL solicita iniciar el simulador
        const params = new URLSearchParams(window.location.search);
        if (params.has('simulador') || params.has('tour') || params.has('guia')) {
            // Limpiar parámetro de URL sin recargar
            params.delete('simulador');
            params.delete('tour');
            params.delete('guia');
            const newSearch = params.toString();
            const newUrl = window.location.pathname + (newSearch ? '?' + newSearch : '') + window.location.hash;
            window.history.replaceState({}, '', newUrl);

            setTimeout(iniciarGuiaInteractiva, 200);
        }
    });

    // Exponer API global
    window.iniciarGuiaInteractiva = iniciarGuiaInteractiva;
    window.cerrarGuiaInteractiva = cerrarGuiaInteractiva;
    window.startSystemTour = function () {
        const path = window.location.pathname.toLowerCase();
        if (path === '/' || path === '' || path === '/index') {
            iniciarGuiaInteractiva();
        } else {
            window.location.href = '/?simulador=1';
        }
    };
    window.exitSystemTour = cerrarGuiaInteractiva;
})();
