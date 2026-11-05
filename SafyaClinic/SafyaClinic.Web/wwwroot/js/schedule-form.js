// P1: on the calendar form, show only the section for the chosen mode (both show without JavaScript).
(function () {
    'use strict';
    var form = document.querySelector('[data-schedule-form]');
    if (!form) return;

    function update() {
        var chosen = form.querySelector('input[name="Mode"]:checked');
        form.querySelectorAll('[data-mode-section]').forEach(function (section) {
            section.hidden = !!chosen && section.getAttribute('data-mode-section') !== chosen.value;
        });
    }

    form.addEventListener('change', function (e) {
        if (e.target.name === 'Mode') update();
    });
    update();
})();
