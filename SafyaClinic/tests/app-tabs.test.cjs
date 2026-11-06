// F8c: tab keyboard movement, including right-to-left pages.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../SafyaClinic.Web/wwwroot/js/app-tabs.js'), 'utf8');
const sandbox = {};
sandbox.globalThis = sandbox;
vm.runInNewContext(source, sandbox);
const { target, keyOf } = sandbox.AppTabs;

test('P5b: the tab key comes from its panel id', () => {
    assert.equal(keyOf('visit-treatments-panel', 'visit'), 'treatments');
    assert.equal(keyOf('visit-lab-panel', 'visit'), 'lab');
    assert.equal(keyOf('other-lab-panel', 'visit'), null);
    assert.equal(keyOf('visit--panel', 'visit'), null);
    assert.equal(keyOf(null, 'visit'), null);
});

test('arrow keys move to the next and previous tab and wrap around', () => {
    assert.equal(target(0, 3, 'ArrowRight', false), 1);
    assert.equal(target(2, 3, 'ArrowRight', false), 0);
    assert.equal(target(0, 3, 'ArrowLeft', false), 2);
});

test('Home and End jump to the first and last tab', () => {
    assert.equal(target(1, 4, 'Home', false), 0);
    assert.equal(target(1, 4, 'End', false), 3);
});

test('arrows are mirrored in right-to-left pages', () => {
    assert.equal(target(0, 3, 'ArrowLeft', true), 1);
    assert.equal(target(0, 3, 'ArrowRight', true), 2);
});

test('other keys do nothing', () => {
    assert.equal(target(1, 3, 'Enter', false), -1);
});
