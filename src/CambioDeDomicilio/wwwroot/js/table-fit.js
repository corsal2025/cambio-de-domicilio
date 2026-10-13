// Keeps the case tables usable on any window size:
//
//  1. The table card is limited to the room left below the header, so rows scroll INSIDE the card instead
//     of making the whole page taller than the screen.
//  2. Its horizontal scrollbar is mirrored by a bar pinned to the bottom edge of the window (position:
//     sticky), so the operator can move sideways at any moment, however far the page is scrolled up or down.
//
// Doc sheets (printable listings such as Sin Carpetas / Última Carpeta) keep their natural height.
(function () {
    var MIN_HEIGHT = 220;   // never squeeze the table below this, even on a very short window
    var BOTTOM_GAP = 40;    // room kept for the pinned bar, the page padding and the author footer
    var timer = 0;

    function pinnedBarFor(card) {
        if (card._pinnedBar) {
            return card._pinnedBar;
        }

        var bar = document.createElement('div');
        bar.className = 'hscroll-pinned';
        bar.setAttribute('aria-hidden', 'true');
        var filler = document.createElement('div');
        bar.appendChild(filler);
        card.insertAdjacentElement('afterend', bar);

        // Two-way sync; the flag stops one scroll event from bouncing back and forth.
        var syncing = false;
        card.addEventListener('scroll', function () {
            if (syncing) { return; }
            syncing = true;
            bar.scrollLeft = card.scrollLeft;
            syncing = false;
        });
        bar.addEventListener('scroll', function () {
            if (syncing) { return; }
            syncing = true;
            card.scrollLeft = bar.scrollLeft;
            syncing = false;
        });

        // The card's own horizontal bar is hidden (overflow-x: hidden), so sideways wheel / trackpad
        // gestures over the table are forwarded to it.
        card.addEventListener('wheel', function (event) {
            var delta = event.deltaX !== 0 ? event.deltaX : (event.shiftKey ? event.deltaY : 0);
            if (delta !== 0) {
                card.scrollLeft += delta;
                event.preventDefault();
            }
        }, { passive: false });

        card._pinnedBar = bar;
        return bar;
    }

    function fit() {
        timer = 0;
        document.querySelectorAll('.card.table-card').forEach(function (card) {
            if (card.closest('.doc-sheet')) {
                return;
            }

            // Measure the card's top with no cap applied, so the result does not depend on the previous cap.
            card.style.removeProperty('max-height');
            var top = card.getBoundingClientRect().top + window.scrollY;
            var available = Math.floor(window.innerHeight - top - BOTTOM_GAP);
            card.style.setProperty('max-height', Math.max(MIN_HEIGHT, available) + 'px', 'important');
            // The stylesheet forces overflow-y: visible !important on the card; an inline !important wins.
            card.style.setProperty('overflow-y', 'auto', 'important');
            card.style.setProperty('overflow-x', 'hidden', 'important');

            var bar = pinnedBarFor(card);
            var overflows = card.scrollWidth > card.clientWidth + 1;
            bar.style.display = overflows ? '' : 'none';
            bar.firstChild.style.width = card.scrollWidth + 'px';
            // Match the card horizontally (the card may have a vertical bar of its own).
            bar.style.width = card.clientWidth + 'px';
            bar.scrollLeft = card.scrollLeft;
        });
    }

    function schedule() {
        // A timer instead of requestAnimationFrame: rAF is paused in hidden/background tabs and the fit
        // must still be right the moment the tab is shown again.
        if (!timer) {
            timer = window.setTimeout(fit, 30);
        }
    }

    window.addEventListener('resize', schedule);
    window.addEventListener('load', schedule);
    if (document.fonts && document.fonts.ready) {
        document.fonts.ready.then(schedule);
    }

    // The header/sub-nav wrap onto more rows as the window narrows, and the table width changes when
    // columns are filled in; re-fit whenever any of them changes size.
    if (window.ResizeObserver) {
        var observer = new ResizeObserver(schedule);
        ['.app-header', '.app-subnav', '.page-head'].forEach(function (selector) {
            var element = document.querySelector(selector);
            if (element) {
                observer.observe(element);
            }
        });
        document.querySelectorAll('.card.table-card table').forEach(function (table) {
            observer.observe(table);
        });
    }

    schedule();
})();
