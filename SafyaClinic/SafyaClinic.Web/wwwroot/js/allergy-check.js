// P5a: warns while the doctor types when a medicine matches the patient's recorded allergies.
// Mirrors SafyaClinic.Application.Clinical.AllergyCheck; the server's check is the one that counts.
// A form opts in with data-allergy-check; the allergies come from the patient header (data-patient-allergies).
(function (root) {
    'use strict';

    var SEPARATORS = /[,;،\/\r\n]+|\s+and\s+|\s+و\s+/i;
    var NO_ALLERGY = ['none', 'no', 'nil', 'n/a', 'na', 'nka', 'nkda', 'no known allergies', 'no known allergy',
        'لا يوجد', 'لا توجد'];

    function terms(allergies) {
        if (!allergies || !allergies.trim()) return [];
        var out = [];
        allergies.split(SEPARATORS).forEach(function (raw) {
            var t = (raw || '').trim().toLowerCase();
            if (t.length >= 3 && NO_ALLERGY.indexOf(t) < 0 && out.indexOf(t) < 0) out.push(t);
        });
        return out;
    }

    function matches(allergies, medications) {
        var list = terms(allergies);
        if (list.length === 0) return [];
        var found = [];
        (medications || []).forEach(function (m) {
            if (!m || !m.trim()) return;
            var medication = m.trim();
            var name = medication.toLowerCase();
            var firstWord = name.split(/\s+/)[0] || '';
            for (var i = 0; i < list.length; i++) {
                var t = list[i];
                if (name.indexOf(t) >= 0 || (firstWord.length >= 4 && t.indexOf(firstWord) >= 0)) {
                    found.push({ medication: medication, allergy: t });
                    break;
                }
            }
        });
        return found;
    }

    root.AllergyCheck = { terms: terms, matches: matches };
    if (typeof document === 'undefined') return;

    var header = document.querySelector('[data-patient-allergies]');
    var allergies = header ? header.getAttribute('data-patient-allergies') : '';
    if (!terms(allergies).length) return;

    document.querySelectorAll('form[data-allergy-check]').forEach(function (form) {
        var box = form.querySelector('[data-allergy-confirm]');
        if (!box) return;
        var list = box.querySelector('[data-allergy-list]');
        var tick = box.querySelector('[data-allergy-override]');
        var template = box.getAttribute('data-template') || '{0}: {1}';

        function current() {
            var names = [];
            form.querySelectorAll('input[name$="MedicationName"]').forEach(function (i) { names.push(i.value); });
            return matches(allergies, names);
        }

        function update() {
            var found = current();
            box.hidden = found.length === 0;
            if (found.length === 0 && tick) tick.checked = false;
            if (list) {
                list.textContent = '';
                found.forEach(function (f) {
                    var li = document.createElement('li');
                    li.setAttribute('dir', 'auto');
                    li.textContent = template.replace('{0}', f.medication).replace('{1}', f.allergy);
                    list.appendChild(li);
                });
            }
            return found;
        }

        form.addEventListener('input', update);
        form.addEventListener('change', update);
        // The medicine picker can fill a name without an input event, so check again on submit.
        form.addEventListener('submit', function (e) {
            if (update().length > 0 && tick && !tick.checked) {
                e.preventDefault();
                box.scrollIntoView({ block: 'center' });
                tick.focus();
            }
        });
        update();
    });
})(typeof window !== 'undefined' ? window : globalThis);
