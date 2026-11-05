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

// P1c: on the date-change form, show the time rows only for "Different times that day".
(function () {
    'use strict';
    var form = document.querySelector('[data-date-change]');
    if (!form) return;
    var times = form.querySelector('[data-change-times]');

    function update() {
        var dayOff = form.querySelector('input[name="IsDayOff"]:checked');
        if (times) times.hidden = !!dayOff && dayOff.value === 'true';
    }

    form.addEventListener('change', function (e) {
        if (e.target.name === 'IsDayOff') update();
    });
    update();
})();
