// F8c: keyboard and click behaviour for <app-tabs> (ARIA tabs pattern).
// Arrow keys move between tabs (mirrored in right-to-left pages), Home and End jump to the ends.
(function (root) {
    'use strict';

    function target(index, count, key, rtl) {
        var next = rtl ? 'ArrowLeft' : 'ArrowRight';
        var prev = rtl ? 'ArrowRight' : 'ArrowLeft';
        if (key === next) return (index + 1) % count;
        if (key === prev) return (index - 1 + count) % count;
        if (key === 'Home') return 0;
        if (key === 'End') return count - 1;
        return -1;
    }

    // P5b: the key of a tab from its panel id ("visit-treatments-panel" in tabs "visit" → "treatments").
    function keyOf(panelId, prefix) {
        var start = prefix + '-', end = '-panel';
        if (!panelId || panelId.indexOf(start) !== 0 || panelId.slice(-end.length) !== end) return null;
        return panelId.slice(start.length, panelId.length - end.length) || null;
    }

    root.AppTabs = { target: target, keyOf: keyOf };
    if (typeof document === 'undefined') return;

    // P5b: <app-tabs data-tab-param="tab"> keeps the chosen tab in the address, so reloading
    // (or coming back from another page) shows the same tab.
    function remember(tab) {
        var host = tab.closest('.app-tabs');
        var param = host && host.getAttribute('data-tab-param');
        if (!param || !window.history || !window.URL) return;
        var key = keyOf(tab.getAttribute('aria-controls'), host.id);
        if (!key) return;
        var url = new URL(window.location.href);
        url.searchParams.set(param, key);
        url.hash = '';
        window.history.replaceState(null, '', url.toString());
    }

    function tabsOf(tab) {
        return Array.prototype.slice.call(tab.closest('[role="tablist"]').querySelectorAll('[role="tab"]'));
    }

    function select(tabs, index) {
        tabs.forEach(function (tab, i) {
            var on = i === index;
            tab.setAttribute('aria-selected', on ? 'true' : 'false');
            tab.tabIndex = on ? 0 : -1;
            var panel = document.getElementById(tab.getAttribute('aria-controls'));
            if (panel) panel.hidden = !on;
        });
        tabs[index].focus();
        remember(tabs[index]);
    }

    document.addEventListener('click', function (e) {
        var tab = e.target.closest('.app-tabs [role="tab"]');
        if (!tab) return;
        var tabs = tabsOf(tab);
        select(tabs, tabs.indexOf(tab));
    });

    document.addEventListener('keydown', function (e) {
        var tab = e.target.closest && e.target.closest('.app-tabs [role="tab"]');
        if (!tab) return;
        var tabs = tabsOf(tab);
        var rtl = getComputedStyle(tab).direction === 'rtl';
        var index = target(tabs.indexOf(tab), tabs.length, e.key, rtl);
        if (index < 0) return;
        e.preventDefault();
        select(tabs, index);
    });
})(typeof window !== 'undefined' ? window : globalThis);
