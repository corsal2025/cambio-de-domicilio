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
            titulo: '1. Cómo funciona el sistema completo',
            icono: '🧭',
            descripcion: 'Las comunas piden carpetas por correo. El sistema <b>lee ese correo</b>, crea un <b>caso por contribuyente</b> y lo acompaña por: <b>Cambio de Domicilio</b> → <b>Subidas a Sistema</b> o <b>F8</b> → <b>Caja</b> o <b>Sin Carpetas</b>. Esta guía recorre cada pieza.'
        },
        {
            target: '.nav-sync, .app-header',
            titulo: '2. Sincronizar ahora (la entrada de datos)',
            icono: '🔄',
            descripcion: 'La lectura del correo es <b>solo manual</b>: no hay sincronización automática. Al pulsar este botón el sistema lee <b>CARP. PARA PEDIR</b> (casos nuevos), <b>CARP. YA SUBIDAS</b> (marca subidos), revisa la bandeja por <b>rebotes</b> y actualiza el reporte CSV.'
        },
        {
            target: '.nav-group-tools a:first-child, .nav-group-tools, .app-header',
            titulo: '3. Comunas: a quién se reconoce',
            icono: '🗺️',
            descripcion: 'Es el directorio de comunas (nombre, correo de contacto y dominio). El sistema reconoce una solicitud por el <b>dominio del remitente</b>. Aquí puedes <b>agregar, editar o eliminar</b> comunas; los cambios se usan en la próxima sincronización.'
        },
        {
            target: '.nav-group-tools a:nth-child(2), .nav-group-tools, .app-header',
            titulo: '4. Descartados: correos no reconocidos',
            icono: '🗑️',
            descripcion: 'Si el remitente no está en el directorio (o comparte dominio con varias comunas sin estar registrado), el correo queda aquí con su <b>motivo</b>. Al registrar la comuna, la próxima sincronización crea el caso y lo quita de Descartados.'
        },
        {
            target: '.nav-group-filters, .app-header',
            titulo: '5. Filtros y "Requiere revisión"',
            icono: '🏷️',
            descripcion: 'Filtra por <b>Todos</b>, <b>Pendientes</b>, <b>Subidos</b>, <b>Confirmados</b>, <b>Requiere revisión</b> y <b>Rebotados</b>. En revisión quedan los casos donde el correo no trajo nombre o RUT válido: debes digitarlos y no se pueden confirmar hasta corregirlos.'
        },
        {
            target: '.nav-search, .app-header',
            titulo: '6. Búsqueda por RUT o nombre',
            icono: '🔍',
            descripcion: 'Escribe un <b>RUT</b> (se formatea solo) o un nombre. Si el caso ya avanzó a F8, Subidas, Caja o Sin Carpetas, el sistema te indica <b>dónde está</b> (incluso la caja y el N°) y te lleva a su fila resaltada.'
        },
        {
            target: '.legacy-caja-option, .table-card',
            titulo: '7. Agregar casos a mano',
            icono: '➕',
            descripcion: 'Con <b>Agregar caso(s)</b> ingresas contribuyentes manualmente. Marcando <b>Carpeta antigua</b> el caso va <b>directo a la cola de Caja</b> sin enviar correo a la comuna: sirve para carpetas viejas que nunca se registraron.'
        },
        {
            target: '.table-card',
            titulo: '8. Colores de las filas',
            icono: '🎨',
            descripcion: '<b>Blanco</b>: sin ninguna acción. <b>Gris</b>: acción terminada (subido, confirmado, en Caja o cerrado). <b>Amarillo</b>: casilla <b>Marcar</b>. <b>Morado</b>: <b>Pendiente carpeta</b>. <b>Rojo</b>: requiere revisión o rebotó. Marcar y Pendiente son solo para organizarte.'
        },
        {
            target: '.table-card',
            titulo: '9. Plazo de 15 días hábiles',
            icono: '⏳',
            descripcion: 'Cada caso tiene <b>15 días hábiles</b> (lunes a viernes) desde que se recibió. El indicador va en <b>verde</b>; pasa a <b>ámbar</b> con 7 días o menos y a <b>rojo</b> con 3 o menos o vencido. Deja de contar cuando el caso ya está subido o confirmado.'
        },
        {
            target: '.fecha-input, .sector-cell, .table-card',
            titulo: '10. Fecha de última carpeta y sector',
            icono: '📅',
            descripcion: 'Escribe la fecha como <b>15 marzo 2024</b> o <b>15/03/2024</b>. El sector sale solo: antes del <b>1 de julio de 2023</b> es <b>Archivo</b>; desde esa fecha, <b>Oficina 43</b>. Con <b>S/C</b> en F8 el caso se cierra sin carpeta.'
        },
        {
            target: '.btn-action--upload, .action-group, .table-card',
            titulo: '11. Marcar subida (confirmar a la comuna)',
            icono: '📤',
            descripcion: 'Al pulsar <b>Marcar subida</b> el sistema mueve el correo a <b>CARP. YA SUBIDAS</b>, marca el caso como Subido y Confirmado y <b>envía el correo de confirmación</b> a la comuna. Si fue un error existe <b>Rectificar</b>: envía una rectificación y vuelve el caso a Pendiente.'
        },
        {
            target: '.table-card',
            titulo: '12. Rebotes y avisos',
            icono: '📬',
            descripcion: 'En cada sincronización se buscan <b>correos devueltos</b> de las confirmaciones. Los casos afectados muestran la marca <b>REBOTÓ</b> y el filtro Rebotados; cuando lo corriges pulsas <b>Resuelto</b>. También llega un aviso interno por cada confirmación enviada.'
        },
        {
            target: '.nav-f8, .app-subnav-main',
            titulo: '13. F8: carpeta física no encontrada',
            icono: '🚨',
            descripcion: 'Si la carpeta no aparece, marca <b>F8</b> y usa <b>Traspaso a F8</b>. Allí registras el <b>código F8</b> y la fecha de la penúltima carpeta. Desde F8 puedes <b>Marcar subida</b>, enviar a <b>Caja</b> (si la encuentras), cerrar <b>Sin carpeta</b> o <b>Revertir</b> sin perder lo digitado.'
        },
        {
            target: '.nav-subidas, .app-subnav-main',
            titulo: '14. Subidas a Sistema',
            icono: '☁️',
            descripcion: 'Lista de <b>solo lectura</b> con los casos ya subidos o confirmados: fechas, estado digital y confirmación. Desde aquí decides su destino físico: <b>Caja</b>, <b>Sin carpeta</b> o <b>Rectificar</b>.'
        },
        {
            target: '.nav-caja, .app-subnav-main',
            titulo: '15. Caja: embalaje físico',
            icono: '📦',
            descripcion: 'Las carpetas llegan a una <b>cola</b> en orden de llegada. Escribes el <b>rótulo o N° de caja</b> y pulsas <b>Cerrar Caja</b>. Las cajas cerradas se pueden <b>imprimir</b>, <b>reabrir</b> o quitarles una carpeta; <b>Devolver a casos</b> saca carpetas de la cola.'
        },
        {
            target: '.nav-sin-carpetas, .app-subnav-main',
            titulo: '16. Sin Carpetas: cierre definitivo',
            icono: '🗄️',
            descripcion: 'Aquí quedan los casos en que <b>no se encontró la carpeta física</b>. Es un listado final <b>sin acciones</b>: solo se puede <b>imprimir o guardar como PDF</b>. Es el cierre del trámite.'
        },
        {
            target: '.app-subnav-docs, .app-subnav-main',
            titulo: '17. Listados PDF: Archivo y Oficina 43',
            icono: '📄',
            descripcion: 'Genera las nóminas para ir a buscar carpetas, separadas por <b>Archivo</b> u <b>Oficina 43</b>. Solo entran los casos con la casilla <b>Marcar</b>. Se imprimen o guardan como PDF desde el navegador.'
        },
        {
            target: '.nav-estadisticas, .app-subnav-main',
            titulo: '18. Estadísticas',
            icono: '📊',
            descripcion: 'Gráficos de casos por estado, ingresos por semana, tiempo promedio de confirmación, comunas con más volumen, sector Archivo vs Oficina 43, plazos F8 y correos descartados por motivo.'
        },
        {
            target: '.app-header',
            titulo: '19. Dependencias técnicas',
            icono: '⚙️',
            descripcion: 'El sistema usa: <b>base SQLite</b> (data/router.db), directorio <b>comunas.csv</b>, el buzón <b>Exchange</b> cambiodedomicilio@munivalpo.cl (EWS), el reporte <b>reporte.csv</b> y avisos de Windows. Se abre en <b>https://localhost:5001</b>, sin clave: el acceso lo controla la red municipal.'
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
