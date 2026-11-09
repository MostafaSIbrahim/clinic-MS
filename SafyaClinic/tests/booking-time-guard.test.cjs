// P2e: booking forms refuse past dates and earlier-than-now times today, like the server.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../SafyaClinic.Web/wwwroot/js/booking-time-guard.js'), 'utf8');
const sandbox = {};
sandbox.globalThis = sandbox;
vm.runInNewContext(source, sandbox);
const { problem, addMinutes } = sandbox.BookingTimeGuard;

test('a past date is refused', () => {
  assert.equal(problem('2026-11-08', '15:00', '2026-11-09', '12:50'), 'past-date');
});

test('today before now is refused, now and later are fine', () => {
  assert.equal(problem('2026-11-09', '12:30', '2026-11-09', '12:50'), 'past-time');
  assert.equal(problem('2026-11-09', '12:50', '2026-11-09', '12:50'), null);
  assert.equal(problem('2026-11-09', '13:00:00', '2026-11-09', '12:50'), null);
});

test('a future date allows any time; an empty date is left to the required check', () => {
  assert.equal(problem('2026-11-10', '08:00', '2026-11-09', '12:50'), null);
  assert.equal(problem('', '08:00', '2026-11-09', '12:50'), null);
});

test('now moves on while the page is open, and stops at the end of the day', () => {
  assert.equal(addMinutes('12:50', 15), '13:05');
  assert.equal(addMinutes('09:05', 0), '09:05');
  assert.equal(addMinutes('23:50', 30), '23:59');
});
