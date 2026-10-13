// PropertyRentalPortal JavaScript Handler

document.addEventListener('DOMContentLoaded', function () {
    const modalEl = document.getElementById('appModal');
    let bsModal = null;
    if (modalEl) {
        bsModal = new bootstrap.Modal(modalEl);
    }

    const modalContent = document.getElementById('appModalContent');

    // 1. Open Dynamic Modal via Partial View
    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('.open-modal-btn');
        if (!btn) return;

        e.preventDefault();
        const url = btn.getAttribute('data-url');
        if (!url) return;

        try {
            modalContent.innerHTML = `
                <div class="modal-body text-center py-5">
                    <div class="spinner-border text-primary" role="status"></div>
                    <p class="mt-2 text-muted mb-0">Loading form...</p>
                </div>
            `;
            bsModal.show();

            const response = await fetch(url, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (!response.ok) {
                modalContent.innerHTML = `
                    <div class="modal-body text-center py-5 text-danger">
                        <i class="bi bi-exclamation-triangle fs-1"></i>
                        <p class="mt-2">Error loading dialog. Please try again.</p>
                    </div>
                `;
                return;
            }

            const html = await response.text();
            modalContent.innerHTML = html;

            // Re-bind jQuery unobtrusive validation if present
            if (window.jQuery && window.jQuery.validator && window.jQuery.validator.unobtrusive) {
                window.jQuery.validator.unobtrusive.parse('#appModalContent');
            }
        } catch (err) {
            console.error('Modal load error:', err);
            modalContent.innerHTML = `
                <div class="modal-body text-center py-5 text-danger">
                    <p>Failed to load modal.</p>
                </div>
            `;
        }
    });

    // 2. Intercept Modal Form Submissions
    // Requirement 1b: When the submitted form fails validation, return the same partial with the validation messages
    // so the modal re-renders in place; when it succeeds, close the modal and refresh the affected part of the page.
    if (modalContent) {
        modalContent.addEventListener('submit', async function (e) {
            const form = e.target.closest('form');
            if (!form) return;

            e.preventDefault();
            const submitBtn = form.querySelector('button[type="submit"]');
            const originalBtnHtml = submitBtn ? submitBtn.innerHTML : '';
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Saving...';
            }

            try {
                const formData = new FormData(form);
                const response = await fetch(form.action, {
                    method: 'POST',
                    body: formData,
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                const contentType = response.headers.get('content-type') || '';

                if (contentType.includes('application/json')) {
                    const result = await response.json();
                    if (result.success) {
                        bsModal.hide();
                        // Refresh affected part or full page
                        window.location.reload();
                    } else {
                        // Display error in modal
                        if (submitBtn) {
                            submitBtn.disabled = false;
                            submitBtn.innerHTML = originalBtnHtml;
                        }
                        alert(result.message || 'Operation failed.');
                    }
                } else {
                    // HTML response: Validation failed on server, partial view returned with ModelState errors
                    const html = await response.text();
                    modalContent.innerHTML = html;
                    if (window.jQuery && window.jQuery.validator && window.jQuery.validator.unobtrusive) {
                        window.jQuery.validator.unobtrusive.parse('#appModalContent');
                    }
                }
            } catch (err) {
                console.error('Modal submit error:', err);
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalBtnHtml;
                }
                alert('A network error occurred. Please try again.');
            }
        });
    }

    // 3. Delete Residence via AJAX
    document.addEventListener('click', async function (e) {
        const delBtn = e.target.closest('.delete-residence-btn');
        if (!delBtn) return;

        e.preventDefault();
        if (!confirm('Are you sure you want to remove this prior residence record?')) {
            return;
        }

        const url = delBtn.getAttribute('data-url');
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput ? tokenInput.value : '';

        try {
            const formData = new FormData();
            formData.append('__RequestVerificationToken', token);

            const response = await fetch(url, {
                method: 'POST',
                body: formData,
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            const result = await response.json();
            if (result.success) {
                const row = delBtn.closest('tr');
                if (row) {
                    row.remove();
                } else {
                    window.location.reload();
                }
            } else {
                alert(result.message || 'Failed to remove residence.');
            }
        } catch (err) {
            console.error('Delete residence error:', err);
            alert('An error occurred while removing the residence.');
        }
    });
});
