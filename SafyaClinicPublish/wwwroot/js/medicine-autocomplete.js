// Lightweight medication typeahead against /api/medicines/search.
// Usage: attachMedicineAutocomplete(inputEl, { onSelect: (med) => {...} })
// No external dependency (no jQuery UI) — plain fetch + DOM.
(function () {
    function debounce(fn, delayMs) {
        let timer = null;
        return function (...args) {
            clearTimeout(timer);
            timer = setTimeout(() => fn.apply(this, args), delayMs);
        };
    }

    window.attachMedicineAutocomplete = function (inputEl, options) {
        options = options || {};
        const minLength = options.minLength || 2;
        const onSelect = options.onSelect || function () {};

        // Wrap the input so the suggestion list can be positioned under it.
        if (getComputedStyle(inputEl.parentElement).position === 'static') {
            inputEl.parentElement.style.position = 'relative';
        }

        const list = document.createElement('div');
        list.className = 'list-group shadow-sm';
        list.style.cssText = 'position:absolute;z-index:1050;top:100%;left:0;right:0;max-height:260px;overflow-y:auto;display:none;';
        inputEl.parentElement.appendChild(list);
        inputEl.setAttribute('autocomplete', 'off');

        let activeItems = [];
        let activeIndex = -1;

        function hide() {
            list.style.display = 'none';
            list.innerHTML = '';
            activeItems = [];
            activeIndex = -1;
        }

        function highlight(index) {
            const children = Array.from(list.children);
            children.forEach((c, i) => c.classList.toggle('active', i === index));
            activeIndex = index;
        }

        function render(items) {
            activeItems = items;
            if (!items.length) { hide(); return; }

            list.innerHTML = '';
            items.forEach((med, i) => {
                const item = document.createElement('button');
                item.type = 'button';
                item.className = 'list-group-item list-group-item-action py-2';
                item.innerHTML =
                    '<div class="d-flex justify-content-between">' +
                    '<span><strong>' + escapeHtml(med.tradeName) + '</strong>' +
                    '<span class="text-muted"> — ' + escapeHtml(med.scientificName) + '</span></span>' +
                    '<span class="text-muted small">' + escapeHtml(med.publicPrice != null ? med.publicPrice + ' EGP' : '') + '</span>' +
                    '</div>' +
                    '<div class="small text-muted">' + escapeHtml(med.description) + (med.strength ? ' • ' + escapeHtml(med.strength) : '') +
                    (med.manufacturer ? ' • ' + escapeHtml(med.manufacturer) : '') + '</div>';
                item.addEventListener('mousedown', function (e) {
                    e.preventDefault(); // keep focus so hide-on-blur doesn't race the click
                    onSelect(med);
                    hide();
                });
                list.appendChild(item);
            });
            list.style.display = 'block';
            activeIndex = -1;
        }

        function escapeHtml(s) {
            if (s === null || s === undefined) return '';
            return String(s)
                .replace(/&/g, '&amp;').replace(/</g, '&lt;')
                .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
        }

        const search = debounce(function (term) {
            fetch('/api/medicines/search?query=' + encodeURIComponent(term), { credentials: 'same-origin' })
                .then(r => r.ok ? r.json() : [])
                .then(render)
                .catch(() => hide());
        }, 300);

        inputEl.addEventListener('input', function () {
            const term = inputEl.value.trim();
            if (term.length < minLength) { hide(); return; }
            search(term);
        });

        inputEl.addEventListener('keydown', function (e) {
            if (list.style.display === 'none') return;
            if (e.key === 'ArrowDown') { e.preventDefault(); highlight(Math.min(activeIndex + 1, activeItems.length - 1)); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); highlight(Math.max(activeIndex - 1, 0)); }
            else if (e.key === 'Enter') {
                if (activeIndex >= 0 && activeItems[activeIndex]) {
                    e.preventDefault();
                    onSelect(activeItems[activeIndex]);
                    hide();
                }
            } else if (e.key === 'Escape') {
                hide();
            }
        });

        inputEl.addEventListener('blur', function () {
            // Slight delay so a mousedown-selection above can complete first.
            setTimeout(hide, 150);
        });
    };
})();
