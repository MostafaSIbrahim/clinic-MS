// P2e: a booking form can't pick a past date, or a time earlier than now on today (clinic time).
// The server refuses the same (BookingRules); this only says so before submitting.
// <input type="date" data-not-past="ReservationTime" data-today="2026-11-09" data-now="12:50" [data-keep-original]>
// data-keep-original (Edit): an unchanged date and time stay allowed, so an old visit can still be edited.
(function (root) {
    'use strict';

    function addMinutes(hhmm, minutes) {
        var parts = (hhmm || '00:00').split(':');
        var total = Math.min(23 * 60 + 59, parseInt(parts[0], 10) * 60 + parseInt(parts[1], 10) + minutes);
        var h = Math.floor(total / 60), m = total % 60;
        return (h < 10 ? '0' : '') + h + ':' + (m < 10 ? '0' : '') + m;
    }

    // 'past-date', 'past-time' or null. Dates are yyyy-MM-dd and times HH:mm, so text order is time order.
    function problem(date, time, today, now) {
        if (!date) return null;
        if (date < today) return 'past-date';
        if (date === today && time && time.slice(0, 5) < now) return 'past-time';
        return null;
    }

    root.BookingTimeGuard = { problem: problem, addMinutes: addMinutes };
    if (typeof document === 'undefined') return;

    document.querySelectorAll('input[type="date"][data-not-past]').forEach(function (dateInput) {
        var timeInput = document.getElementById(dateInput.getAttribute('data-not-past'));
        var today = dateInput.getAttribute('data-today');
        var loadedNow = dateInput.getAttribute('data-now');
        var loadedAt = Date.now();
        var keepOriginal = dateInput.hasAttribute('data-keep-original');
        var originalDate = dateInput.value, originalTime = timeInput ? timeInput.value : '';
        var message = dateInput.getAttribute('data-message') || 'Choose a current or future time.';

        function check() {
            var now = addMinutes(loadedNow, Math.floor((Date.now() - loadedAt) / 60000));
            var time = timeInput ? timeInput.value : '';
            var unchanged = keepOriginal && dateInput.value === originalDate && time === originalTime;
            var found = unchanged ? null : problem(dateInput.value, time, today, now);
            dateInput.setCustomValidity(found === 'past-date' ? message : '');
            if (timeInput) {
                timeInput.setCustomValidity(found === 'past-time' ? message : '');
                if (dateInput.value === today && !unchanged) timeInput.min = now; else timeInput.removeAttribute('min');
            }
        }

        if (!keepOriginal || originalDate >= today) dateInput.min = today;
        dateInput.addEventListener('input', check);
        dateInput.addEventListener('change', check);
        if (timeInput) { timeInput.addEventListener('input', check); timeInput.addEventListener('change', check); }
        var form = dateInput.form;
        if (form) form.addEventListener('submit', function (e) {
            check();
            if (!form.checkValidity()) { e.preventDefault(); form.reportValidity(); }
        });
        check();
    });
})(typeof window !== 'undefined' ? window : globalThis);
