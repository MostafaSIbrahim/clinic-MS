// P5a: the browser's allergy warning follows the same rules as the server (AllergyCheckTests).
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../SafyaClinic.Web/wwwroot/js/allergy-check.js'), 'utf8');
const sandbox = {};
sandbox.globalThis = sandbox;
vm.runInNewContext(source, sandbox);
const { terms, matches } = sandbox.AllergyCheck;
const names = (found) => found.map((f) => `${f.medication}=${f.allergy}`).join('|');

test('splits allergies on commas, semicolons, the Arabic comma, new lines and "and"', () => {
  assert.equal(JSON.stringify(terms('Penicillin, Sulfa; aspirin، ibuprofen\nlatex and codeine')),
    JSON.stringify(['penicillin', 'sulfa', 'aspirin', 'ibuprofen', 'latex', 'codeine']));
});

test('ignores "none" entries and very short words', () => {
  assert.equal(terms('NKDA').length, 0);
  assert.equal(terms('none').length, 0);
  assert.equal(terms('No known allergies').length, 0);
  assert.equal(terms('لا يوجد').length, 0);
  assert.equal(terms('ab, x').length, 0);
});

test('a medicine containing an allergy matches, ignoring case', () => {
  assert.equal(names(matches('Penicillin', ['PENICILLIN V 500mg', 'Paracetamol'])), 'PENICILLIN V 500mg=penicillin');
});

test('an allergy naming the medicine matches', () => {
  assert.equal(names(matches('Augmentin tablets', ['Augmentin 1g'])), 'Augmentin 1g=augmentin tablets');
});

test('no allergies or unrelated medicines give no warning', () => {
  assert.equal(matches('', ['Penicillin']).length, 0);
  assert.equal(matches('Penicillin', ['Paracetamol', '', null]).length, 0);
});
