/**
 * system-tour.js — Tour Guiado Interactivo por las Pantallas Reales del Sistema
 * Municipalidad de Valparaíso — CambioDeDomicilio
 */

(function () {
    const TOUR_STORAGE_KEY = 'cdd_system_tour_active';
    const TOUR_STEP_KEY = 'cdd_system_tour_step';

    const STEPS = [
        {
            step: 1,
            path: '/',
            altPaths: ['/Index'],
            navClass: '.nav-casos',
            badge: 'Mesa de Entrada',
            title: '1. Cambio de Domicilio (Bandeja Principal)',
            summary: 'Aquí ingresan automáticamente las solicitudes recibidas por correo desde las distintas comunas.',
            keyPoints: [
                '<strong>Fecha última carpeta:</strong> Ingresa la fecha en formato <code>dd/mm/aaaa</code> para clasificar el sector (Archivo histórico u Oficina 43).',
                '<strong>Marcar subida:</strong> Cuando la carpeta se sube a Conaset, pulsa este botón. El caso <em>desaparece de esta bandeja</em> y viaja directo a <strong>Subidas a Sistema</strong>.',
                '<strong>Traspaso a F8:</strong> Si la carpeta no se encuentra en las estanterías, pulsa este botón para enviarla al módulo urgente de búsqueda F8.'
            ],
            nextUrl: '/F8?tour=2',
            prevUrl: null,
            nextLabel: 'Siguiente: Casos F8 ➔'
        },
        {
            step: 2,
            path: '/F8',
            altPaths: ['/F8/'],
            navClass: '.nav-f8',
            badge: 'Búsqueda Especial',
            title: '2. Casos F8 (Urgencias / Sin Carpeta Inicial)',
            summary: 'Módulo dedicado para cuando la carpeta física no se encontró a la primera en el archivo físico.',
            keyPoints: [
                '<strong>Código F8:</strong> Asigna un código único (ej. <code>F8-1234</code>) para que los bodegueros rastreen la carpeta en terreno.',
                '<strong>Botón Caja:</strong> Si la carpeta física finalmente aparece, pulsa <strong>Caja</strong> para derivarla al embalaje.',
                '<strong>Botón Sin Carpeta:</strong> Si definitivamente no existe carpeta física, pulsa <strong>Sin carpeta</strong>. El caso se archiva directo en <strong>Sin Carpetas</strong>.'
            ],
            nextUrl: '/SubidasASistema?tour=3',
            prevUrl: '/?tour=1',
            nextLabel: 'Siguiente: Subidas a Sistema ➔'
        },
        {
            step: 3,
            path: '/SubidasASistema',
            altPaths: ['/SubidasASistema/'],
            navClass: '.nav-subidas',
            badge: 'Estación de Derivación',
            title: '3. Subidas a Sistema',
            summary: 'Aquí se concentran todas las solicitudes que ya fueron marcadas como subidas en Cambio de Domicilio.',
            keyPoints: [
                '<strong>Bandeja limpia:</strong> Esta sección evita que Cambio de Domicilio se llene de trámites ya subidos.',
                '<strong>Derivación a Caja:</strong> Si tienes la carpeta física en mano, pulsa <strong>Caja</strong> para enviarla a embalaje oficial.',
                '<strong>Derivación Sin Carpeta:</strong> Si el contribuyente no requirió carpeta de papel, pulsa <strong>Sin carpeta</strong> para archivarlo en Sin Carpetas.'
            ],
            nextUrl: '/SinCarpetas?tour=4',
            prevUrl: '/F8?tour=2',
            nextLabel: 'Siguiente: Sin Carpetas ➔'
        },
        {
            step: 4,
            path: '/SinCarpetas',
            altPaths: ['/SinCarpetas/'],
            navClass: '.nav-sin-carpetas',
            badge: 'Archivo Digital',
            title: '4. Sin Carpetas (Histórico y Auditoría)',
            summary: 'Repositorio histórico permanente de todos los casos cerrados que no contaron con carpeta física de papel.',
            keyPoints: [
                '<strong>Auditoría completa:</strong> Consulta en segundos la fecha exacta de cierre y el funcionario que gestionó el caso.',
                '<strong>Revertir a F8:</strong> Si en el futuro aparece la carpeta física en bodega, puedes pulsar <strong>Revertir a F8</strong> para reabrir la búsqueda.'
            ],
            nextUrl: '/Caja?tour=5',
            prevUrl: '/SubidasASistema?tour=3',
            nextLabel: 'Siguiente: Embalaje en Caja ➔'
        },
        {
            step: 5,
            path: '/Caja',
            altPaths: ['/Caja/'],
            navClass: '.nav-caja',
            badge: 'Embalaje y Bodega',
            title: '5. Caja (Control Físico y Embalaje)',
            summary: 'Gestiona la cola de carpetas físicas que serán embaladas en cajas foliadas oficiales para su entrega a bodega.',
            keyPoints: [
                '<strong>Cola de espera:</strong> Las carpetas enviadas desde Casos, F8 o Subidas a Sistema se van acumulando aquí.',
                '<strong>Cerrar Caja:</strong> Al completar la cantidad de carpetas (ej. 40 o 50), ingresa el número de caja y pulsa <strong>Cerrar Caja</strong> para generar el rótulo oficial numerado (ej. <code>A1-CD</code>).',
                '<strong>Historial de Cajas:</strong> Consulta e imprime el listado de cualquier caja cerrada en el panel lateral.'
            ],
            nextUrl: '/Estadisticas?tour=6',
            prevUrl: '/SinCarpetas?tour=4',
            nextLabel: 'Siguiente: Estadísticas ➔'
        },
        {
            step: 6,
            path: '/Estadisticas',
            altPaths: ['/Estadisticas/'],
            navClass: '.nav-estadisticas',
            badge: 'Control Legal',
            title: '6. Estadísticas y Semáforo de Plazos (15 Días)',
            summary: 'Panel de monitoreo en tiempo real del cumplimiento de la normativa legal de plazos.',
            keyPoints: [
                '<strong>Semáforo legal:</strong> 🟢 Verde (&gt; 5 días restantes), 🟡 Amarillo (&lt; 5 días restantes, urgencia), 🔴 Rojo (plazo de 15 días vencido).',
                '<strong>Ranking comunal:</strong> Visualiza qué comunas generan mayor demanda de carpetas para planificar la carga de trabajo.'
            ],
            nextUrl: '/Manual?tour=done',
            prevUrl: '/Caja?tour=5',
            nextLabel: 'Finalizar Tour ✓'
        }
    ];

    function injectStyles() {
        if (document.getElementById('system-tour-styles')) return;
        const style = document.createElement('style');
        style.id = 'system-tour-styles';
        style.textContent = `
            /* Tour Floating HUD */
            .tour-hud {
                position: fixed;
                bottom: 20px;
                left: 50%;
                transform: translateX(-50%) translateY(20px);
                width: calc(100% - 32px);
                max-width: 900px;
                background: linear-gradient(135deg, rgba(8, 26, 43, 0.96) 0%, rgba(15, 38, 64, 0.98) 100%);
                backdrop-filter: blur(14px);
                -webkit-backdrop-filter: blur(14px);
                border: 2px solid #0284c7;
                border-radius: 16px;
                box-shadow: 0 16px 40px rgba(0, 0, 0, 0.5), 0 0 24px rgba(2, 132, 199, 0.35);
                color: #ffffff;
                z-index: 999999;
                padding: 18px 24px;
                font-family: inherit;
                opacity: 0;
                transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
                pointer-events: auto;
            }
            .tour-hud.visible {
                opacity: 1;
                transform: translateX(-50%) translateY(0);
            }
            .tour-hud-header {
                display: flex;
                align-items: center;
                justify-content: space-between;
                margin-bottom: 12px;
                gap: 12px;
                flex-wrap: wrap;
            }
            .tour-hud-badge {
                background: #0284c7;
                color: #ffffff;
                padding: 4px 10px;
                border-radius: 20px;
                font-size: 0.72rem;
                font-weight: 800;
                letter-spacing: 0.05em;
                text-transform: uppercase;
                display: inline-flex;
                align-items: center;
                gap: 6px;
            }
            .tour-hud-title {
                font-size: 1.15rem;
                font-weight: 800;
                color: #f8fafc;
                margin: 0;
                flex: 1;
                letter-spacing: -0.01em;
            }
            .tour-hud-close {
                background: rgba(255, 255, 255, 0.1);
                border: 1px solid rgba(255, 255, 255, 0.2);
                color: #cbd5e1;
                padding: 5px 12px;
                border-radius: 8px;
                font-size: 0.8rem;
                cursor: pointer;
                transition: all 0.2s ease;
                display: inline-flex;
                align-items: center;
                gap: 4px;
            }
            .tour-hud-close:hover {
                background: #ef4444;
                border-color: #ef4444;
                color: #ffffff;
            }
            .tour-hud-summary {
                font-size: 0.9rem;
                color: #94a3b8;
                margin-bottom: 12px;
                line-height: 1.45;
            }
            .tour-hud-points {
                display: grid;
                grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
                gap: 10px;
                background: rgba(0, 0, 0, 0.25);
                border: 1px solid rgba(255, 255, 255, 0.08);
                border-radius: 10px;
                padding: 12px 14px;
                margin-bottom: 14px;
                font-size: 0.82rem;
                line-height: 1.4;
                color: #e2e8f0;
            }
            .tour-hud-points code {
                background: rgba(2, 132, 199, 0.25);
                color: #38bdf8;
                padding: 1px 4px;
                border-radius: 4px;
            }
            .tour-hud-footer {
                display: flex;
                align-items: center;
                justify-content: space-between;
                gap: 12px;
                flex-wrap: wrap;
            }
            .tour-hud-progress {
                font-size: 0.78rem;
                color: #64748b;
                display: flex;
                align-items: center;
                gap: 8px;
            }
            .tour-hud-dots {
                display: flex;
                gap: 4px;
            }
            .tour-hud-dot {
                width: 8px;
                height: 8px;
                border-radius: 50%;
                background: rgba(255, 255, 255, 0.2);
                transition: all 0.2s ease;
            }
            .tour-hud-dot.active {
                background: #38bdf8;
                width: 22px;
                border-radius: 10px;
            }
            .tour-hud-actions {
                display: flex;
                gap: 8px;
                align-items: center;
            }
            .tour-hud-btn {
                background: rgba(255, 255, 255, 0.1);
                border: 1px solid rgba(255, 255, 255, 0.2);
                color: #f1f5f9;
                padding: 8px 16px;
                border-radius: 8px;
                font-size: 0.84rem;
                font-weight: 700;
                cursor: pointer;
                text-decoration: none;
                display: inline-flex;
                align-items: center;
                gap: 6px;
                transition: all 0.2s ease;
            }
            .tour-hud-btn:hover {
                background: rgba(255, 255, 255, 0.2);
                color: #ffffff;
            }
            .tour-hud-btn-primary {
                background: #0284c7;
                border-color: #38bdf8;
                color: #ffffff;
                box-shadow: 0 4px 12px rgba(2, 132, 199, 0.4);
            }
            .tour-hud-btn-primary:hover {
                background: #0369a1;
                transform: translateY(-1px);
            }
            /* Highlight target animation */
            .tour-highlight-active {
                position: relative !important;
                outline: 3px solid #38bdf8 !important;
                outline-offset: 4px !important;
                box-shadow: 0 0 20px rgba(56, 189, 248, 0.5) !important;
                animation: tourPulse 2s infinite ease-in-out !important;
            }
            @keyframes tourPulse {
                0% { outline-color: #38bdf8; box-shadow: 0 0 15px rgba(56, 189, 248, 0.4); }
                50% { outline-color: #0284c7; box-shadow: 0 0 28px rgba(2, 132, 199, 0.7); }
                100% { outline-color: #38bdf8; box-shadow: 0 0 15px rgba(56, 189, 248, 0.4); }
            }
        `;
        document.head.appendChild(style);
    }

    function getCurrentStepIndex() {
        const urlParams = new URLSearchParams(window.location.search);
        const tourParam = urlParams.get('tour');
        if (tourParam) {
            const parsed = parseInt(tourParam, 10);
            if (!isNaN(parsed) && parsed >= 1 && parsed <= STEPS.length) {
                return parsed - 1;
            }
        }

        const currentPath = window.location.pathname;
        for (let i = 0; i < STEPS.length; i++) {
            const s = STEPS[i];
            if (s.path === currentPath || (s.altPaths && s.altPaths.includes(currentPath))) {
                return i;
            }
        }
        return -1;
    }

    function isTourActive() {
        const urlParams = new URLSearchParams(window.location.search);
        if (urlParams.get('tour') === 'done') {
            sessionStorage.removeItem(TOUR_STORAGE_KEY);
            return false;
        }
        if (urlParams.has('tour')) {
            sessionStorage.setItem(TOUR_STORAGE_KEY, 'true');
            return true;
        }
        return sessionStorage.getItem(TOUR_STORAGE_KEY) === 'true';
    }

    function renderTourHud() {
        if (!isTourActive()) return;

        const stepIdx = getCurrentStepIndex();
        if (stepIdx < 0) return;

        const data = STEPS[stepIdx];
        injectStyles();

        // Remove existing HUD if present
        const existing = document.getElementById('system-tour-hud');
        if (existing) existing.remove();

        const hud = document.createElement('div');
        hud.id = 'system-tour-hud';
        hud.className = 'tour-hud';

        const dotsHtml = STEPS.map((s, idx) =>
            `<span class="tour-hud-dot ${idx === stepIdx ? 'active' : ''}" title="${s.title}"></span>`
        ).join('');

        const pointsHtml = data.keyPoints.map(p => `<div>• ${p}</div>`).join('');

        hud.innerHTML = `
            <div class="tour-hud-header">
                <span class="tour-hud-badge">🧭 Paso ${data.step} de ${STEPS.length} • ${data.badge}</span>
                <h3 class="tour-hud-title">${data.title}</h3>
                <button type="button" class="tour-hud-close" id="tour-hud-close-btn" title="Cerrar tour guiado">
                    ✕ Salir del tour
                </button>
            </div>
            <div class="tour-hud-summary">${data.summary}</div>
            <div class="tour-hud-points">${pointsHtml}</div>
            <div class="tour-hud-footer">
                <div class="tour-hud-progress">
                    <span>Avance:</span>
                    <div class="tour-hud-dots">${dotsHtml}</div>
                </div>
                <div class="tour-hud-actions">
                    ${data.prevUrl ? `<a href="${data.prevUrl}" class="tour-hud-btn">◀ Anterior</a>` : ''}
                    <a href="${data.nextUrl}" class="tour-hud-btn tour-hud-btn-primary">${data.nextLabel}</a>
                </div>
            </div>
        `;

        document.body.appendChild(hud);

        // Highlight active subnav tab
        if (data.navClass) {
            const targetEl = document.querySelector(data.navClass);
            if (targetEl) {
                targetEl.classList.add('tour-highlight-active');
            }
        }

        // Show HUD with transition
        requestAnimationFrame(() => {
            hud.classList.add('visible');
        });

        // Close button handler
        document.getElementById('tour-hud-close-btn')?.addEventListener('click', () => {
            sessionStorage.removeItem(TOUR_STORAGE_KEY);
            hud.classList.remove('visible');
            setTimeout(() => hud.remove(), 300);
            if (data.navClass) {
                document.querySelector(data.navClass)?.classList.remove('tour-highlight-active');
            }
        });
    }

    // Expose global starter function
    window.startSystemTour = function () {
        sessionStorage.setItem(TOUR_STORAGE_KEY, 'true');
        window.location.href = '/?tour=1';
    };

    window.exitSystemTour = function () {
        sessionStorage.removeItem(TOUR_STORAGE_KEY);
        const hud = document.getElementById('system-tour-hud');
        if (hud) {
            hud.classList.remove('visible');
            setTimeout(() => hud.remove(), 300);
        }
    };

    document.addEventListener('DOMContentLoaded', () => {
        renderTourHud();
    });
})();
