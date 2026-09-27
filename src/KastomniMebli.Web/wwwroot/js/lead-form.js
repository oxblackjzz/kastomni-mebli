// Відправка форм (заявка на замір, завдання від мебляра) без перезавантаження сторінки.
// Без JS форми теж працюють: звичайний POST і редирект назад із ?zayavka=...
(() => {
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

    const setup = (form) => {
        const status = form.querySelector('[data-status]');
        const button = form.querySelector('button[type=submit]');
        const fallbackError = form.dataset.errorMessage;

        const setError = (field, message) => {
            const slot = form.querySelector(`[data-error-for="${field}"]`);
            const input = form.elements[field];
            if (slot) slot.textContent = message || '';
            if (input && input.setAttribute) {
                if (message) input.setAttribute('aria-invalid', 'true');
                else input.removeAttribute('aria-invalid');
            }
        };

        const clearErrors = () =>
            form.querySelectorAll('[data-error-for]').forEach((el) => setError(el.dataset.errorFor, ''));

        const showStatus = (message, kind) => {
            status.textContent = message;
            status.className = `form-status form-status-${kind}`;
            status.hidden = false;
        };

        const validate = () => {
            let ok = true;
            const name = form.elements.name;
            if (name && !name.value.trim()) {
                setError('name', form.id === 'b2b-form' ? "Вкажіть назву цеху або ім'я." : "Вкажіть ім'я.");
                ok = false;
            }
            const phone = form.elements.phone;
            if (phone) {
                if (!phone.value.trim()) {
                    setError('phone', 'Вкажіть телефон.');
                    ok = false;
                } else if (!isValidPhone(phone.value)) {
                    setError('phone', 'Перевірте номер: потрібен український номер, наприклад 067 123 45 67.');
                    ok = false;
                }
            }
            const files = form.elements.files;
            if (files && files.files) {
                const list = [...files.files];
                const maxFile = Number(form.dataset.maxFileMb) * 1024 * 1024;
                const maxTotal = Number(form.dataset.maxTotalMb) * 1024 * 1024;
                const big = list.find((f) => f.size > maxFile);
                if (list.length > Number(form.dataset.maxFiles)) {
                    setError('files', `Не більше ${form.dataset.maxFiles} файлів.`);
                    ok = false;
                } else if (big) {
                    setError('files', `«${big.name}» завеликий — до ${form.dataset.maxFileMb} МБ.`);
                    ok = false;
                } else if (list.reduce((s, f) => s + f.size, 0) > maxTotal) {
                    setError('files', `Разом файли — до ${form.dataset.maxTotalMb} МБ. Більше — надішліть у Telegram.`);
                    ok = false;
                }
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
            button.textContent = form.elements.files?.files?.length ? 'Надсилаємо файли…' : 'Надсилаємо…';

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
    };

    document.querySelectorAll('#lead-form, .js-form').forEach(setup);
})();
