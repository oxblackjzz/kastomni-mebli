// Відправка заявки без перезавантаження сторінки.
// Без JS форма теж працює: звичайний POST і редирект назад із ?zayavka=...
(() => {
    const form = document.getElementById('lead-form');
    if (!form) return;

    const status = form.querySelector('[data-status]');
    const button = form.querySelector('button[type=submit]');
    const fallbackError = form.dataset.errorMessage;

    // Та сама логіка, що й PhoneNumber.TryNormalize на сервері (сервер — головний).
    const isValidPhone = (value) => {
        const v = value.trim();
        if (!/^\+?[\d\s\-().]+$/.test(v)) return false;
        const d = v.replace(/\D/g, '');
        let national = null;
        if (d.length === 12 && d.startsWith('380')) national = d.slice(3);
        else if (d.length === 11 && d.startsWith('80')) national = d.slice(2);
        else if (d.length === 10 && d.startsWith('0')) national = d.slice(1);
        else if (d.length === 9) national = d;
        return national !== null && national[0] >= '3' && national[0] <= '9';
    };

    const setError = (field, message) => {
        const slot = form.querySelector(`[data-error-for="${field}"]`);
        const input = form.elements[field];
        if (slot) slot.textContent = message || '';
        if (input && input.setAttribute) {
            if (message) input.setAttribute('aria-invalid', 'true');
            else input.removeAttribute('aria-invalid');
        }
    };

    const clearErrors = () => {
        form.querySelectorAll('[data-error-for]').forEach((el) => setError(el.dataset.errorFor, ''));
    };

    const showStatus = (message, kind) => {
        status.textContent = message;
        status.className = `form-status form-status-${kind}`;
        status.hidden = false;
    };

    const validate = () => {
        let ok = true;
        if (!form.elements.name.value.trim()) {
            setError('name', "Вкажіть ім'я.");
            ok = false;
        }
        const phone = form.elements.phone.value;
        if (!phone.trim()) {
            setError('phone', 'Вкажіть телефон.');
            ok = false;
        } else if (!isValidPhone(phone)) {
            setError('phone', 'Перевірте номер: потрібен український номер, наприклад 067 123 45 67.');
            ok = false;
        }
        return ok;
    };

    form.addEventListener('submit', async (event) => {
        event.preventDefault();
        clearErrors();
        status.hidden = true;

        if (!validate()) {
            form.querySelector('[aria-invalid=true]')?.focus();
            return;
        }

        const label = button.textContent;
        button.disabled = true;
        button.textContent = 'Надсилаємо…';

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: { Accept: 'application/json' },
            });
            const data = await response.json().catch(() => null);

            if (response.ok && data?.ok) {
                const done = document.createElement('div');
                done.className = 'form-status form-status-ok';
                done.setAttribute('role', 'status');
                done.tabIndex = -1;
                done.textContent = data.message;
                form.replaceWith(done);
                done.focus();
                return;
            }

            if (data?.errors) {
                Object.entries(data.errors).forEach(([field, message]) => setError(field, message));
            }
            showStatus(data?.message || fallbackError, 'error');
        } catch {
            showStatus(fallbackError, 'error');
        } finally {
            button.disabled = false;
            button.textContent = label;
        }
    });
})();
