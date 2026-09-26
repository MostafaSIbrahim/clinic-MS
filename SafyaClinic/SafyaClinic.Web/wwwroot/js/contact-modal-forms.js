(() => {
    document.querySelectorAll('form[data-contact-modal]').forEach(form => {
        if (form.dataset.bound === 'true') return;
        const modal = form.closest('.modal');
        const errorBox = form.querySelector('[data-modal-error]');
        const submit = form.querySelector('button[type="submit"]');
        if (!modal || !errorBox || !submit) return;
        form.dataset.bound = 'true';
        const originalText = submit.textContent;
        let saving = false;
        modal.addEventListener('shown.bs.modal', () => {
            form.querySelector('[data-modal-focus]')?.focus();
        });
        modal.addEventListener('hide.bs.modal', event => {
            if (saving) event.preventDefault();
        });
        form.addEventListener('submit', async event => {
            event.preventDefault();
            if (saving || !form.reportValidity()) return;
            const body = new FormData(form);
            saving = true;
            submit.disabled = true;
            submit.textContent = 'Saving…';
            form.setAttribute('aria-busy', 'true');
            errorBox.hidden = true;
            let saved = false;
            try {
                const response = await fetch(form.action, {
                    method: 'POST', body,
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest',
                        'Accept': 'application/json'
                    }
                });
                if (response.redirected ||
                    !(response.headers.get('content-type') || '')
                        .includes('application/json')) {
                    throw new Error('Could not confirm the save. Refresh and check ' +
                        'the contact list and your sign-in before submitting again.');
                }
                let data;
                try { data = await response.json(); }
                catch {
                    throw new Error('Could not confirm the save. Refresh and check ' +
                        'the contact list before submitting again.');
                }
                if (!response.ok || data.success !== true) {
                    const errors = Array.isArray(data.errors) && data.errors.length
                        ? data.errors : ['Could not save. Check the values and try again.'];
                    throw new Error(errors.join(' '));
                }
                saved = true;
                window.location.reload();
            } catch (error) {
                errorBox.textContent = error instanceof TypeError
                    ? 'The connection was interrupted. Refresh and check the contact ' +
                      'list before submitting again.'
                    : error.message;
                errorBox.hidden = false;
                errorBox.focus();
            } finally {
                if (!saved) {
                    saving = false;
                    submit.disabled = false;
                    submit.textContent = originalText;
                    form.removeAttribute('aria-busy');
                }
            }
        });
    });
})();
