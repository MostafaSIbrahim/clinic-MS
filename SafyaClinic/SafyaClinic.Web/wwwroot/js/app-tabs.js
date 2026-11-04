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

    root.AppTabs = { target: target };
    if (typeof document === 'undefined') return;

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
