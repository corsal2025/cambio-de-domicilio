// Keeps the horizontal scrollbar of the case tables on screen.
//
// The table card used to be as tall as all its rows, so on a small (restored/"minimized") window its
// scrollbar sat at the very bottom of a card far taller than the viewport and the operator had to scroll
// the page just to find it. The card is now limited to the room left below the header, so rows scroll
// inside the card and the horizontal bar stays at the card's bottom edge, in view.
//
// Doc sheets (printable listings such as Sin Carpetas / Última Carpeta) keep their natural height.
(function () {
    var MIN_HEIGHT = 220;   // never squeeze the table below this, even on a very short window
    var BOTTOM_GAP = 36;    // room kept for the page padding / author footer under the card
    var frame = 0;

    function fit() {
        frame = 0;
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
        });
    }

    function schedule() {
        // A timer instead of requestAnimationFrame: rAF is paused in hidden/background tabs and the fit
        // must still be right the moment the tab is shown again.
        if (!frame) {
            frame = window.setTimeout(fit, 30);
        }
    }

    window.addEventListener('resize', schedule);
    window.addEventListener('load', schedule);
    if (document.fonts && document.fonts.ready) {
        document.fonts.ready.then(schedule);
    }

    // The header/sub-nav wrap onto more rows as the window narrows; re-fit whenever they change height.
    if (window.ResizeObserver) {
        var observer = new ResizeObserver(schedule);
        ['.app-header', '.app-subnav', '.page-head'].forEach(function (selector) {
            var element = document.querySelector(selector);
            if (element) {
                observer.observe(element);
            }
        });
    }

    schedule();
})();
