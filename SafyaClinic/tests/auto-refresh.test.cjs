// P4: the queue reloads itself only when nobody is in the middle of something.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../SafyaClinic.Web/wwwroot/js/auto-refresh.js'), 'utf8');
const sandbox = {};
sandbox.globalThis = sandbox;
vm.runInNewContext(source, sandbox);
const { shouldRefresh } = sandbox.AutoRefresh;
const ready = { enabled: true, visible: true, editing: false, dialogOpen: false };

test('refreshes when on, visible and idle', () => {
    assert.equal(shouldRefresh(ready), true);
});

test('waits while typing, while a dialog is open, when hidden or paused', () => {
    assert.equal(shouldRefresh({ ...ready, editing: true }), false);
    assert.equal(shouldRefresh({ ...ready, dialogOpen: true }), false);
    assert.equal(shouldRefresh({ ...ready, visible: false }), false);
    assert.equal(shouldRefresh({ ...ready, enabled: false }), false);
});
