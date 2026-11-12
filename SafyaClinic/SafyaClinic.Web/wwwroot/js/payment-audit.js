// M1c: payment audit history in a dialog, on the patient's Payments tab (moved from Payments/PatientSummary).
(function () {
    if (!document.getElementById('paymentAuditModal')) return;
(() => {
            const modalElement = document.getElementById('paymentAuditModal');
            const body = document.getElementById('paymentAuditBody');
            const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
            let activeRequest = null;

            async function loadAudit(url) {
                activeRequest?.abort();
                const request = new AbortController();
                activeRequest = request;

                body.innerHTML = `
                            <div class="text-center text-muted py-5" role="status">
                                <span class="spinner-border spinner-border-sm me-2"
                                      aria-hidden="true"></span>
                                ${body.dataset.loading || 'Loading…'}
                            </div>`;

                try {
                    const response = await fetch(url, {
                        credentials: 'same-origin',
                        signal: request.signal
                    });

                    if (response.redirected ||
                        response.status === 401 ||
                        response.status === 403) {
                        throw new Error(
                            body.dataset.expired || 'Your session expired or you do not have access. Refresh the page and sign in again.');
                    }

                    if (!response.ok) {
                        throw new Error(body.dataset.error || 'Could not load payment audit history.');
                    }

                    const html = await response.text();

                    if (activeRequest !== request) return;

                    body.innerHTML = html;
                    body.scrollTop = 0;
                } catch (error) {
                    if (request.signal.aborted || activeRequest !== request) return;

                    body.replaceChildren();
                    const alert = document.createElement('div');
                    alert.className = 'alert alert-danger mb-0';
                    alert.setAttribute('role', 'alert');
                    alert.textContent = error.message ||
                        body.dataset.error || 'Could not load payment audit history.';
                    body.appendChild(alert);
                }
            }

            document.querySelectorAll('.js-payment-audit').forEach(button => {
                button.addEventListener('click', () => {
                    modal.show();
                    loadAudit(button.dataset.auditUrl);
                });
            });

            body.addEventListener('click', event => {
                const link = event.target.closest('nav a');

                if (!link || !body.contains(link)) return;

                event.preventDefault();
                loadAudit(link.href);
            });

            modalElement.addEventListener('hidden.bs.modal', () => {
                activeRequest?.abort();
                activeRequest = null;
                body.replaceChildren();
            });
        })();
})();
