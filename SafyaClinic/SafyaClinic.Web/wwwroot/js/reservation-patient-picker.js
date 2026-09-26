(() => {
    const form = document.getElementById('reservationCreateForm');
    if (!form) return;
    const input = document.getElementById('patientSearchInput');
    const hidden = document.getElementById('patientIdHidden');
    const status = document.getElementById('patientSearchStatus');
    const register = document.getElementById('registerPatientButton');
    const list = document.createElement('div');
    list.id = 'patientSearchResults';
    list.className = 'list-group shadow-sm position-absolute w-100';
    list.style.cssText = 'z-index:1050;max-height:260px;overflow-y:auto';
    list.setAttribute('role', 'listbox');
    list.setAttribute('aria-label', 'Matching patients');
    list.hidden = true;
    input.parentElement.appendChild(list);
    input.setAttribute('role', 'combobox');
    input.setAttribute('aria-autocomplete', 'list');
    input.setAttribute('aria-controls', list.id);
    input.setAttribute('aria-expanded', 'false');
    let timer, request, revision = 0, popup = null, items = [], active = -1;

    function close() {
        list.hidden = true;
        list.replaceChildren();
        items = []; active = -1;
        input.setAttribute('aria-expanded', 'false');
        input.removeAttribute('aria-activedescendant');
    }
    function invalidateRequest() {
        clearTimeout(timer);
        request?.abort();
        revision++;
    }
    function validateSelection() {
        const valid = Number.isSafeInteger(Number(hidden.value)) && Number(hidden.value) > 0
            && input.value.trim().length > 0;
        input.setCustomValidity(valid ? '' : 'Select a patient from the search results.');
        // Prevent accidental registration while a patient is selected, including initial preselection.
        register.disabled = valid;
        return valid;
    }
    function select(patient) {
        const id = Number(patient?.id);
        if (!Number.isSafeInteger(id) || id <= 0 || typeof patient.fullName !== 'string') return;
        invalidateRequest();
        hidden.value = String(id);
        input.value = patient.fullName + (patient.primaryPhone ? ' — ' + patient.primaryPhone : '');
        validateSelection(); close();
        status.textContent = 'Selected patient #' + id + ': ' + patient.fullName;
        input.focus();
    }
    function highlight(index) {
        active = index;
        [...list.children].forEach((el, i) => {
            el.classList.toggle('active', i === index);
            el.setAttribute('aria-selected', String(i === index));
        });
        if (list.children[index]) {
            input.setAttribute('aria-activedescendant', list.children[index].id);
            list.children[index].scrollIntoView({ block: 'nearest' });
        }
    }
    function render(results) {
        close();
        items = results.filter(p => Number.isSafeInteger(Number(p.id))
            && Number(p.id) > 0 && typeof p.fullName === 'string');
        if (!items.length) { status.textContent = 'No matching patients. Try another search or register a patient.'; return; }
        items.forEach((p, i) => {
            const option = document.createElement('button');
            option.type = 'button'; option.tabIndex = -1;
            option.id = 'patientOption-' + i;
            option.className = 'list-group-item list-group-item-action text-start';
            option.setAttribute('role', 'option');
            option.setAttribute('aria-selected', 'false');
            option.textContent = p.fullName + ' · #' + p.id +
                (p.primaryPhone ? ' · ' + p.primaryPhone : '') +
                (p.nationalId ? ' · ID: ' + p.nationalId : '');
            option.addEventListener('mousedown', e => e.preventDefault());
            option.addEventListener('click', () => select(p));
            list.appendChild(option);
        });
        list.hidden = false;
        input.setAttribute('aria-expanded', 'true');
        status.textContent = items.length + ' matches. Use arrow keys and Enter to select.';
    }
    input.addEventListener('input', () => {
        invalidateRequest(); close();
        hidden.value = '';
        validateSelection();
        const term = input.value.trim();
        if (term.length < 2) { status.textContent = 'Type at least 2 characters.'; return; }
        const current = revision;
        status.textContent = 'Searching…';
        timer = setTimeout(async () => {
            const controller = new AbortController(); request = controller;
            try {
                const url = new URL(input.dataset.searchUrl, location.origin);
                url.searchParams.set('query', term);
                const response = await fetch(url, {
                    signal: controller.signal,
                    credentials: 'same-origin', headers: { Accept: 'application/json' }
                });
                if (!response.ok || response.redirected ||
                    !(response.headers.get('content-type') || '').includes('application/json'))
                    throw new Error('Search unavailable');
                const results = await response.json();
                if (current !== revision || input.value.trim() !== term || document.activeElement !== input) return;
                if (!Array.isArray(results)) throw new Error('Invalid response');
                render(results);
            } catch (error) {
                if (error.name === 'AbortError' || current !== revision) return;
                close(); status.textContent = 'Patient search is unavailable. Check your connection/sign-in, then try again.';
            }
        }, 300);
    });
    input.addEventListener('keydown', e => {
        if (e.key === 'Escape') { invalidateRequest(); close(); return; }
        if (list.hidden) return;
        if (e.key === 'ArrowDown') { e.preventDefault(); highlight(Math.min(active + 1, items.length - 1)); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); highlight(Math.max(active - 1, 0)); }
        else if (e.key === 'Enter') {
            e.preventDefault();
            if (active >= 0) select(items[active]);
            else status.textContent = 'Choose a result with the arrow keys or click a patient.';
        }
    });
    input.addEventListener('blur', () => setTimeout(() => {
        // Review fix: a delayed blur must not cancel a new search after the user refocuses the input.
        if (document.activeElement !== input && !list.contains(document.activeElement)) {
            invalidateRequest(); close();
        }
    }, 150));
    form.addEventListener('submit', e => {
        if (!validateSelection()) { e.preventDefault(); input.reportValidity(); input.focus(); }
    });
    register.addEventListener('click', () => {
        popup = window.open(register.dataset.popupUrl, 'NewPatient',
            'width=900,height=700,scrollbars=yes,resizable=yes');
        if (!popup) status.textContent = 'Allow popups to register a patient, then try again.';
    });
    window.addEventListener('message', e => {
        if (e.origin !== location.origin || !popup || e.source !== popup ||
            e.data?.type !== 'NEW_PATIENT_CREATED') return;
        select(e.data.patient);
    });
    validateSelection();
    if (Number(hidden.value) > 0) status.textContent = 'Selected patient #' + hidden.value;
})();
