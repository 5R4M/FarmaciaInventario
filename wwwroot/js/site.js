// Toggle password visibility
document.addEventListener('DOMContentLoaded', function() {
    const passwordInput = document.querySelector('input[type="password"]');
    if (passwordInput) {
        // Find parent input group
        const inputGroup = passwordInput.closest('.input-group');
        if (inputGroup) {
            const toggleBtn = document.createElement('button');
            toggleBtn.type = 'button';
            toggleBtn.className = 'btn btn-outline-secondary';
            toggleBtn.innerHTML = '<i class="bi bi-eye"></i>';
            toggleBtn.style.cssText = 'border-color: #475569; color: #94a3b8;';
            toggleBtn.onclick = function() {
                const type = passwordInput.getAttribute('type') === 'password' ? 'text' : 'password';
                passwordInput.setAttribute('type', type);
                this.querySelector('i').className = type === 'password' ? 'bi bi-eye' : 'bi bi-eye-slash';
            };
            inputGroup.appendChild(toggleBtn);
        }
    }
});

// Enable tooltips
var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'))
var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
  return new bootstrap.Tooltip(tooltipTriggerEl)
})