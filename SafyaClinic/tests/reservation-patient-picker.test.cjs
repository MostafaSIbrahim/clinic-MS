// Review regression tests: exercise selection, request ordering and popup trust without a database or browser.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname,
    '../SafyaClinic.Web/wwwroot/js/reservation-patient-picker.js'), 'utf8');

// Minimal DOM/event adapter; assertions below check observable patient IDs and rendered results.
function setup() {
    const document = { activeElement: null };
    class Element {
        constructor() {
            this.listeners = {}; this.children = []; this.attributes = {};
            this.dataset = {}; this.style = {}; this.value = ''; this.textContent = '';
            this.classList = { toggle() {} };
        }
        addEventListener(name, callback) { (this.listeners[name] ??= []).push(callback); }
        fire(name, data = {}) {
            const e = { preventDefault() { this.defaultPrevented = true; }, ...data };
            for (const callback of this.listeners[name] || []) callback(e);
            return e;
        }
        appendChild(child) { child.parentElement = this; this.children.push(child); }
        replaceChildren() { this.children = []; }
        setAttribute(name, value) { this.attributes[name] = value; }
        removeAttribute(name) { delete this.attributes[name]; }
        setCustomValidity(message) { this.validationMessage = message; }
        reportValidity() { return !this.validationMessage; }
        focus() { document.activeElement = this; }
        scrollIntoView() {}
        contains(element) { return this.children.includes(element); }
    }
    const ids = Object.fromEntries(['reservationCreateForm', 'patientSearchInput', 'patientIdHidden',
        'patientSearchStatus', 'registerPatientButton'].map(id => [id, new Element()]));
    const input = ids.patientSearchInput;
    input.parentElement = new Element();
    input.dataset.searchUrl = '/api/patients/search';
    ids.registerPatientButton.dataset.popupUrl = '/Patients/Create?popup=true';
    document.getElementById = id => ids[id];
    document.createElement = () => new Element();
    const window = new Element(), popup = {};
    window.open = () => popup;
    const requests = [], timers = new Map();
    let timerId = 0;
    vm.runInNewContext(source, {
        document, window, location: { origin: 'https://clinic.test' }, URL, AbortController,
        setTimeout(fn, delay) { timers.set(++timerId, { fn, delay }); return timerId; },
        clearTimeout(id) { timers.delete(id); },
        fetch(url, options) { return new Promise(resolve => requests.push({ url, options, resolve })); }
    });
    function runTimer(delay) {
        const entry = [...timers].find(([, timer]) => timer.delay === delay);
        assert.ok(entry, 'Expected scheduled timer');
        timers.delete(entry[0]); return entry[1].fn();
    }
    function type(text) { input.focus(); input.value = text; input.fire('input'); }
    function reply(index, patients) {
        requests[index].resolve({ ok: true, redirected: false,
            headers: { get: () => 'application/json' }, json: async () => patients });
    }
    return { ids, input, window, popup, document, requests, type, reply, runTimer,
        list: input.parentElement.children[0] };
}

test('typing another name clears the selected ID and prevents an unselected submission', async () => {
    const ui = setup(); ui.type('Alice');
    const pending = ui.runTimer(300); ui.reply(0, [{ id: 7, fullName: 'Alice' }]); await pending;
    ui.list.children[0].fire('click');
    assert.equal(ui.ids.patientIdHidden.value, '7');
    ui.type('Bob');
    assert.equal(ui.ids.patientIdHidden.value, '');
    assert.ok(ui.input.validationMessage);
    assert.equal(ui.ids.reservationCreateForm.fire('submit').defaultPrevented, true);
});

test('an older response cannot replace results for the latest query', async () => {
    const ui = setup(); ui.type('Alice'); const first = ui.runTimer(300);
    ui.type('Bob'); const second = ui.runTimer(300);
    ui.reply(1, [{ id: 8, fullName: 'Bob' }]); await second;
    ui.reply(0, [{ id: 7, fullName: 'Alice' }]); await first;
    assert.match(ui.list.children[0].textContent, /Bob/);
    assert.equal(ui.requests[0].options.signal.aborted, true);
});

test('a delayed blur does not abort a search started after refocusing', async () => {
    const ui = setup(); ui.type('Alice');
    ui.input.fire('blur'); ui.document.activeElement = null;
    ui.type('Bob'); const pending = ui.runTimer(300);
    ui.runTimer(150);
    assert.equal(ui.requests[0].options.signal.aborted, false);
    ui.reply(0, [{ id: 8, fullName: 'Bob' }]); await pending;
    assert.equal(ui.list.hidden, false);
});

test('popup selection requires the correct origin and the window opened by the picker', () => {
    const ui = setup(); ui.ids.registerPatientButton.fire('click');
    const data = { type: 'NEW_PATIENT_CREATED', patient: { id: 9, fullName: 'New patient' } };
    ui.window.fire('message', { origin: 'https://other.test', source: ui.popup, data });
    ui.window.fire('message', { origin: 'https://clinic.test', source: {}, data });
    assert.equal(ui.ids.patientIdHidden.value, '');
    ui.window.fire('message', { origin: 'https://clinic.test', source: ui.popup, data });
    assert.equal(ui.ids.patientIdHidden.value, '9');
    assert.equal(ui.input.validationMessage, '');
});

test('keyboard selection records the highlighted patient, not an arbitrary first result', async () => {
    const ui = setup(); ui.type('Patient'); const pending = ui.runTimer(300);
    ui.reply(0, [{ id: 7, fullName: 'Patient A' }, { id: 8, fullName: 'Patient B' }]); await pending;
    ui.input.fire('keydown', { key: 'ArrowDown' });
    ui.input.fire('keydown', { key: 'ArrowDown' });
    ui.input.fire('keydown', { key: 'Enter' });
    assert.equal(ui.ids.patientIdHidden.value, '8');
    assert.equal(ui.list.hidden, true);
});
