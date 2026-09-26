// Safya Clinic JavaScript

// Auto-hide success messages; keep errors and warnings visible.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.alert-success.alert-dismissible')
        .forEach(function (alert) {
            setTimeout(function () {
                alert.querySelector('.btn-close')?.click();
            }, 5000);
        });
});

// Confirm delete actions
function confirmDelete(message) {
    return confirm(message || 'Are you sure you want to delete this item?');
}

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