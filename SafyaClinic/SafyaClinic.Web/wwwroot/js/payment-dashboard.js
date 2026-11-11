// M2b: payment dashboard line details. Each "Details" button opens a dialog: choose dates (pre-filled with the
// dashboard's own period), then the details load inside the dialog from Payments/DashboardLineDetails.
// Texts and the address come from data attributes on #drilldownModal, so they follow the page's language.
(function () {
    'use strict';
    var modalEl = document.getElementById('drilldownModal');
    if (!modalEl || typeof bootstrap === 'undefined') return;
    var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    var d = modalEl.dataset;
    var titleEl = document.getElementById('drilldownModalTitle');
    var steps = {
        duration: document.getElementById('drilldownStepDuration'),
        loading: document.getElementById('drilldownStepLoading'),
        error: document.getElementById('drilldownStepError'),
        report: document.getElementById('drilldownStepReport')
    };
    var backBtn = document.getElementById('drilldownBackBtn');
    var fromInput = document.getElementById('drilldownFrom');
    var toInput = document.getElementById('drilldownTo');
    var groupType = null, groupId = '';

    function show(name) {
        Object.keys(steps).forEach(function (k) { steps[k].classList.toggle('d-none', k !== name); });
        backBtn.classList.toggle('d-none', name !== 'report' && name !== 'error');
    }

    function fail(message) {
        steps.error.textContent = message;
        show('error');
    }

    async function load() {
        if (fromInput.value && toInput.value && fromInput.value > toInput.value) { fail(d.errorOrder); return; }
        show('loading');
        var params = new URLSearchParams({ groupType: groupType });
        if (groupId) params.set('groupId', groupId);
        if (fromInput.value) params.set('from', fromInput.value);
        if (toInput.value) params.set('to', toInput.value);
        try {
            var response = await fetch(d.url + '?' + params.toString(), { credentials: 'same-origin' });
            if (!response.ok) throw new Error();
            steps.report.innerHTML = await response.text();
            show('report');
        } catch (e) {
            fail(d.errorLoad);
        }
    }

    document.querySelectorAll('.js-drilldown').forEach(function (button) {
        button.addEventListener('click', function () {
            groupType = button.dataset.groupType;
            groupId = button.dataset.groupId || '';
            titleEl.textContent = (d.title || '{0}').replace('{0}', button.dataset.groupLabel || '');
            fromInput.value = d.from || '';
            toInput.value = d.to || '';
            show('duration');
            modal.show();
        });
    });

    document.getElementById('drilldownDurationForm').addEventListener('submit', function (e) { e.preventDefault(); load(); });
    document.getElementById('drilldownAllTimeBtn').addEventListener('click', function () { fromInput.value = ''; toInput.value = ''; load(); });
    backBtn.addEventListener('click', function () { show('duration'); });

    // The details partial may offer "view all time" (see _DashboardLineDetails.cshtml).
    window.paymentDrilldown = { viewAllTime: function () { fromInput.value = ''; toInput.value = ''; load(); } };
})();
