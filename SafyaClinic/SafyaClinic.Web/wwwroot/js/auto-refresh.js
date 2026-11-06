// P4: reloads a page (the queue) every N seconds while it's visible and nobody is in the middle of something:
// no form field focused, no dialog open. A toggle button pauses it; the choice is remembered on this browser.
(function (root) {
    'use strict';

    function shouldRefresh(state) {
        return state.enabled && state.visible && !state.editing && !state.dialogOpen;
    }

    root.AutoRefresh = { shouldRefresh: shouldRefresh };
    if (typeof document === 'undefined') return;

    var host = document.querySelector('[data-auto-refresh]');
    if (!host) return;
    var seconds = parseInt(host.getAttribute('data-auto-refresh'), 10) || 30;
    var toggle = host.querySelector('[data-auto-refresh-toggle]');
    var key = 'safya.autoRefresh.' + location.pathname;
    var enabled = true;
    try { enabled = localStorage.getItem(key) !== 'off'; } catch (e) { /* storage unavailable: stay on */ }

    function show() {
        if (!toggle) return;
        toggle.setAttribute('aria-pressed', enabled ? 'true' : 'false');
        var label = toggle.querySelector('[data-label]');
        if (label) label.textContent = enabled ? toggle.getAttribute('data-on') : toggle.getAttribute('data-off');
    }

    if (toggle) {
        toggle.addEventListener('click', function () {
            enabled = !enabled;
            try { localStorage.setItem(key, enabled ? 'on' : 'off'); } catch (e) { /* ignore */ }
            show();
        });
    }
    show();

    setInterval(function () {
        var active = document.activeElement;
        var editing = !!active && /^(INPUT|SELECT|TEXTAREA)$/.test(active.tagName);
        var dialogOpen = !!document.querySelector('.modal.show');
        if (shouldRefresh({ enabled: enabled, visible: document.visibilityState === 'visible', editing: editing, dialogOpen: dialogOpen }))
            location.reload();
    }, seconds * 1000);
})(typeof window !== 'undefined' ? window : globalThis);
