// The page reloads constantly (every table action submits a form, plus any auto-refresh) —
// without this, the browser resets scroll to the top AND back to the leftmost column every
// time, forcing the operator to scroll back down and right to wherever they were working. The
// table also scrolls independently of the window (see .table-card's own overflow-x/y), so its
// scrollLeft/Top needs saving separately from window.scrollY. beforeunload covers every way the
// page can go away: form submits, auto-refresh, manual reloads. Keys are namespaced per path so
// pages don't clobber each other's saved position.
(function () {
    var pageKey = location.pathname;
    var scrollKey = 'scrollY:' + pageKey;
    var tableScrollXKey = 'tableScrollX:' + pageKey;
    var tableScrollYKey = 'tableScrollY:' + pageKey;
    var table = document.querySelector('.table-card');

    var savedScroll = sessionStorage.getItem(scrollKey);
    var savedTableX = table ? sessionStorage.getItem(tableScrollXKey) : null;
    var savedTableY = table ? sessionStorage.getItem(tableScrollYKey) : null;
    var messageBanner = document.querySelector('.message-banner');

    // Lets page scripts that jump to a highlighted row (search result) skip that jump when the
    // page is only reloading after an action — otherwise the jump overrides the restored position
    // and the table leaps sideways/away from where the operator was working.
    window.scrollPositionRestored = savedScroll !== null || savedTableX !== null || savedTableY !== null;

    // Restoring here is a no-op if the window is minimized: the browser hasn't laid out the
    // page (table.scrollWidth is still 0), so scrollLeft/scrollTop assignments get clamped back
    // to 0 and the values are lost since we already removed them from storage. Keep the saved
    // values around and re-apply on visibilitychange so a restore-while-minimized retries once
    // the tab is actually visible and laid out.
    //
    // A message banner (success or error feedback from the action that just ran, e.g.
    // OnPostSetPersonData rejecting an invalid RUT) sits above the table — restoring a deep
    // scroll position here would silently carry the operator right past it, so they'd see their
    // edit "revert" with no visible explanation. Feedback wins over scroll restoration that one
    // time; the table's own internal scroll still gets restored below either way.
    function applyRestore() {
        if (savedScroll !== null) {
            window.scrollTo(0, parseInt(savedScroll, 10));
        }
        if (table) {
            if (savedTableX !== null) {
                table.scrollLeft = parseInt(savedTableX, 10);
            }
            if (savedTableY !== null) {
                table.scrollTop = parseInt(savedTableY, 10);
            }
        }
    }

    function clearSaved() {
        sessionStorage.removeItem(scrollKey);
        sessionStorage.removeItem(tableScrollXKey);
        sessionStorage.removeItem(tableScrollYKey);
    }

    if (document.hidden) {
        document.addEventListener('visibilitychange', function onVisible() {
            if (!document.hidden) {
                document.removeEventListener('visibilitychange', onVisible);
                applyRestore();
                clearSaved();
            }
        });
    } else {
        applyRestore();
        clearSaved();
    }

    function savePositions() {
        sessionStorage.setItem(scrollKey, window.scrollY);
        if (table) {
            sessionStorage.setItem(tableScrollXKey, table.scrollLeft);
            sessionStorage.setItem(tableScrollYKey, table.scrollTop);
        }
    }

    window.addEventListener('beforeunload', savePositions);
    window.addEventListener('pagehide', savePositions);
    document.addEventListener('submit', savePositions, true);

    // Reintentar restauración tras la carga completa y layout de fuentes para evitar desajustes
    window.addEventListener('DOMContentLoaded', applyRestore);
    window.addEventListener('load', function () {
        setTimeout(applyRestore, 50);
        setTimeout(clearSaved, 300);
    });
})();
