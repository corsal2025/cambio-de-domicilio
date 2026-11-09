/* Presentation-only enhancements. No network requests or case mutations. */
(function () {
    'use strict';
    var imageMeta = document.querySelector('meta[name="lico-image"]');
    var header = document.querySelector('header.app-header');
    if (!imageMeta || !header) return;
    var reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    var focusMode = false;
    try { focusMode = localStorage.getItem('spatial-focus') === 'true'; } catch (_) {}

    function canAnimate() { return !reducedMotion.matches && !focusMode; }
    function animate(element, frames, options) {
        if (canAnimate() && element && typeof element.animate === 'function') {
            return element.animate(frames, options);
        }
    }

    var tools = document.createElement('div');
    tools.className = 'spatial-tools';
    var focusButton = document.createElement('button');
    focusButton.type = 'button';
    focusButton.className = 'spatial-focus';
    focusButton.textContent = 'Modo enfoque';
    focusButton.title = 'Reducir la decoración y desactivar el movimiento';
    tools.appendChild(focusButton);
    header.appendChild(tools);
    var filters = header.querySelector('.nav-group-filters');
    var pageHead = document.querySelector('main > .page-head');
    if (filters && pageHead) {
        var filterNav = document.createElement('nav');
        filterNav.className = 'spatial-filters';
        filterNav.setAttribute('aria-label', 'Filtrar solicitudes por estado');
        filterNav.appendChild(filters);
        pageHead.insertAdjacentElement('afterend', filterNav);
    }
    function applyFocus() {
        document.body.classList.toggle('focus-mode', focusMode);
        focusButton.setAttribute('aria-pressed', String(focusMode));
        if (focusMode) {
            document.querySelectorAll('.head-lico .lico-volume').forEach(function (image) {
                image.style.removeProperty('--lico-x');
                image.style.removeProperty('--lico-y');
            });
        }
        window.dispatchEvent(new Event('resize'));
    }
    focusButton.addEventListener('click', function () {
        focusMode = !focusMode;
        try { localStorage.setItem('spatial-focus', String(focusMode)); } catch (_) {}
        applyFocus();
    });
    applyFocus();

    var nav = document.querySelector('body > .app-subnav');
    if (nav) {
        var signature = document.createElement('div');
        signature.className = 'spatial-signature';
        signature.innerHTML = '<b>RS /</b>MODELO Y SISTEMA<strong>RAÚL SALAZAR</strong>';
        nav.appendChild(signature);
    }

    document.querySelectorAll('.head-lico').forEach(function (mascot) {
        var bubble = mascot.querySelector('.head-lico-burbuja');
        if (bubble) bubble.textContent = 'LICO / Abrir guía';
        var pendingFrame = 0;
        var latestX = 0;
        var latestY = 0;
        mascot.addEventListener('pointermove', function (event) {
            if (!canAnimate() || event.pointerType === 'touch') return;
            var bounds = mascot.getBoundingClientRect();
            latestX = ((event.clientX - bounds.left) / bounds.width - 0.5) * 22;
            latestY = -((event.clientY - bounds.top) / bounds.height - 0.5) * 14;
            if (!pendingFrame) pendingFrame = requestAnimationFrame(function () {
                pendingFrame = 0;
                var image = mascot.querySelector('.lico-volume');
                if (!image || !canAnimate()) return;
                image.style.setProperty('--lico-x', latestY + 'deg');
                image.style.setProperty('--lico-y', latestX + 'deg');
            });
        });
        mascot.addEventListener('pointerleave', function () {
            cancelAnimationFrame(pendingFrame);
            pendingFrame = 0;
            var image = mascot.querySelector('.lico-volume');
            if (image) {
                image.style.removeProperty('--lico-x');
                image.style.removeProperty('--lico-y');
            }
        });
        mascot.addEventListener('pointerenter', function () {
            animate(mascot.querySelector('.lico-volume'), [
                { transform: 'translateY(0) rotate(-2deg)' },
                { transform: 'translateY(-7px) rotate(3deg)', offset: 0.45 },
                { transform: 'translateY(-4px) rotate(0)' }
            ], { duration: 520, easing: 'cubic-bezier(.2,.8,.2,1)' });
        });
    });

    // Native dialog retains Escape, focus trapping and return focus automatically.
    var dialog = document.createElement('dialog');
    if (typeof dialog.showModal !== 'function') return;
    dialog.className = 'case-dialog';
    dialog.id = 'case-detail-dialog';
    dialog.setAttribute('aria-labelledby', 'case-detail-title');
    dialog.innerHTML = '<div class="case-dialog-head"><div><small>RS / VISTA DEL REGISTRO</small><h2 id="case-detail-title"></h2></div><button type="button" class="case-dialog-close" aria-label="Cerrar ficha">×</button></div><dl></dl><div class="case-dialog-footer"><img alt="" width="52" height="52"><p>Consulta los datos sin salir de la lista. Para modificarlos, cierra esta ficha y utiliza los campos de la tabla.</p></div>';
    dialog.querySelector('img').src = imageMeta.content;
    document.body.appendChild(dialog);
    dialog.querySelector('.case-dialog-close').addEventListener('click', function () { dialog.close(); });
    dialog.addEventListener('click', function (event) {
        if (event.target !== dialog) return;
        var bounds = dialog.getBoundingClientRect();
        if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) dialog.close();
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
