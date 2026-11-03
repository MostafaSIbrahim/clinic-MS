// Safya Clinic JavaScript

// F7c: show the messages from the last action (success closes by itself; errors stay).
document.addEventListener('DOMContentLoaded', function () {
    if (!window.bootstrap) return;
    document.querySelectorAll('.app-toast').forEach(function (el) {
        bootstrap.Toast.getOrCreateInstance(el).show();
    });
});

// F7c: one confirmation dialog for every form, submit button, link or select marked
// with data-confirm="question". A select also needs data-confirm-submit (submit on change).
(function () {
    var dialogEl = null;
    var pending = null;

    function dialog() {
        if (dialogEl || !window.bootstrap) return dialogEl;
        dialogEl = document.getElementById('appConfirmDialog');
        if (!dialogEl) return null;
        dialogEl.querySelector('[data-confirm-ok]').addEventListener('click', function () {
            var action = pending;
            pending = null;
            bootstrap.Modal.getOrCreateInstance(dialogEl).hide();
            if (action) action.proceed();
        });
        dialogEl.addEventListener('hidden.bs.modal', function () {
            if (!pending) return;
            var action = pending;
            pending = null;
            action.cancel();
        });
        return dialogEl;
    }

    function ask(message, proceed, cancel) {
        cancel = cancel || function () { };
        var el = dialog();
        if (!el) {   // pages without the shared layout
            if (window.confirm(message)) proceed(); else cancel();
            return;
        }
        el.querySelector('[data-confirm-message]').textContent = message;
        pending = { proceed: proceed, cancel: cancel };
        bootstrap.Modal.getOrCreateInstance(el).show();
    }

    window.appConfirm = ask;

    document.addEventListener('submit', function (e) {
        var form = e.target;
        var submitter = e.submitter || null;
        var message = (submitter && submitter.dataset.confirm) || form.dataset.confirm;
        if (!message || form.dataset.confirmed === 'true') return;
        e.preventDefault();
        ask(message, function () {
            form.dataset.confirmed = 'true';
            if (form.requestSubmit) form.requestSubmit(submitter || undefined); else form.submit();
            setTimeout(function () { delete form.dataset.confirmed; }, 0);
        });
    }, true);

    document.addEventListener('click', function (e) {
        var link = e.target.closest('a[data-confirm]');
        if (!link) return;
        e.preventDefault();
        ask(link.dataset.confirm, function () { window.location.href = link.href; });
    });

    document.addEventListener('change', function (e) {
        var select = e.target.closest('select[data-confirm][data-confirm-submit]');
        if (!select) return;
        ask(select.dataset.confirm,
            function () { select.form.submit(); },
            function () { select.form.reset(); });
    });
})();

// Format currency
function formatCurrency(amount) {
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'EGP'
    }).format(amount);
}

// Toggle password visibility
function togglePassword(inputId) {
    const input = document.getElementById(inputId);
    const icon = document.querySelector(`[data-toggle="${inputId}"]`);
    if (input.type === 'password') {
        input.type = 'text';
        icon.classList.remove('bi-eye');
        icon.classList.add('bi-eye-slash');
    } else {
        input.type = 'password';
        icon.classList.remove('bi-eye-slash');
        icon.classList.add('bi-eye');
    }
}