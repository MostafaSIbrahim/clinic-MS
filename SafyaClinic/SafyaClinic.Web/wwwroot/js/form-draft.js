// M3b: keeps what someone has typed in a form when the page reloads before it is saved, for example when the
// language is switched. Forms opt in with data-draft-key="<unique key>". Values are kept in this browser tab only
// (sessionStorage), for two hours, and are removed when the form is submitted or Cancel (data-draft-discard) is used.
// Not kept: the anti-forgery token, hidden fields, files, passwords, and fields marked data-draft-ignore
// (such as "Prescribe anyway", which must always be ticked deliberately).
// A form can set data-draft-prepare="functionName": that function is called with the saved names first, so it can
// add rows (the drug rows of a new prescription) before the values are put back.
// Restoring happens only when the page was opened normally (data-draft-restore="true"), not when the server shows
// the form again with what was submitted.
(function (global) {
    'use strict';
    var PREFIX = 'safya-draft:';
    var MAX_AGE_MS = 2 * 60 * 60 * 1000;

    function storage() {
        try { return global.sessionStorage; } catch (e) { return null; }
    }
    function read(key) {
        var s = storage(); if (!s) return null;
        try {
            var draft = JSON.parse(s.getItem(PREFIX + key) || 'null');
            if (!draft || !draft.values || Date.now() - draft.savedAt > MAX_AGE_MS) { s.removeItem(PREFIX + key); return null; }
            return draft.values;
        } catch (e) { return null; }
    }
    function write(key, values) {
        var s = storage(); if (!s) return;
        try { s.setItem(PREFIX + key, JSON.stringify({ savedAt: Date.now(), values: values })); } catch (e) { /* full or blocked */ }
    }
    function remove(key) {
        var s = storage(); if (!s) return;
        try { s.removeItem(PREFIX + key); } catch (e) { /* blocked */ }
    }

    function kept(el) {
        if (!el.name || el.disabled || el.name === '__RequestVerificationToken') return false;
        if (el.hasAttribute('data-draft-ignore')) return false;
        var type = (el.type || '').toLowerCase();
        return type !== 'hidden' && type !== 'file' && type !== 'password' && type !== 'submit' && type !== 'button';
    }

    function collect(form) {
        var values = {}, any = false;
        Array.prototype.forEach.call(form.elements, function (el) {
            if (!kept(el)) return;
            var type = (el.type || '').toLowerCase();
            if (type === 'checkbox' || type === 'radio') {
                if (el.checked) { values[el.name] = el.value; any = true; }
            } else {
                values[el.name] = el.value;
                if (el.value) any = true;
            }
        });
        return any ? values : null;
    }

    function restore(form, values) {
        var prepare = form.getAttribute('data-draft-prepare');
        if (prepare && typeof global[prepare] === 'function') global[prepare](Object.keys(values));
        Array.prototype.forEach.call(form.elements, function (el) {
            if (!kept(el) || !Object.prototype.hasOwnProperty.call(values, el.name)) return;
            var type = (el.type || '').toLowerCase();
            if (type === 'checkbox' || type === 'radio') el.checked = values[el.name] === el.value;
            else el.value = values[el.name];
        });
    }

    function init(form) {
        var key = form.getAttribute('data-draft-key');
        if (!key) return;
        if (form.getAttribute('data-draft-restore') === 'true') {
            var values = read(key);
            if (values) restore(form, values);
        }
        var save = function () {
            var values = collect(form);
            if (values) write(key, values); else remove(key);
        };
        form.addEventListener('input', save);
        form.addEventListener('change', save);
        // Rows removed with a button change no field, so save again after any click inside the form.
        form.addEventListener('click', function () { setTimeout(save, 0); });
        form.addEventListener('submit', function (e) {
            // Only forget the draft if nothing (allergy check, validation) stopped the submit.
            setTimeout(function () { if (!e.defaultPrevented) remove(key); }, 0);
        });
        Array.prototype.forEach.call(document.querySelectorAll('[data-draft-discard="' + key + '"]'), function (link) {
            link.addEventListener('click', function () { remove(key); });
        });
    }

    Array.prototype.forEach.call(document.querySelectorAll('form[data-draft-key]'), init);
    global.FormDraft = { read: read, remove: remove };
})(typeof window !== 'undefined' ? window : globalThis);
