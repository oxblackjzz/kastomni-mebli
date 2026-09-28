// Звідки прийшов відвідувач: запам'ятовуємо ПЕРШИЙ візит (мітки utm_* з посилання або сайт-реферер)
// і додаємо це прихованими полями до форм заявки. Жодних сторонніх трекерів — лише localStorage цього сайту.
(() => {
    const KEY = 'km_first_touch';
    const params = new URLSearchParams(location.search);
    const utm = {
        utm_source: params.get('utm_source'),
        utm_medium: params.get('utm_medium'),
        utm_campaign: params.get('utm_campaign'),
    };
    let ref = '';
    try {
        const r = document.referrer ? new URL(document.referrer) : null;
        if (r && r.host !== location.host) ref = r.host;
    } catch { /* немає реферера */ }

    let stored = null;
    try { stored = JSON.parse(localStorage.getItem(KEY) || 'null'); } catch { stored = null; }

    // Мітки в посиланні важливіші за збережене «напряму» — людина прийшла з реклами.
    const hasUtm = Object.values(utm).some(Boolean);
    if (!stored || (hasUtm && !stored.utm_source)) {
        stored = { ...utm, ref, at: new Date().toISOString() };
        try { localStorage.setItem(KEY, JSON.stringify(stored)); } catch { /* приватний режим */ }
    }

    const fill = (form) => {
        for (const name of ['utm_source', 'utm_medium', 'utm_campaign', 'ref']) {
            if (!stored[name] || form.elements[name]) continue;
            const input = document.createElement('input');
            input.type = 'hidden';
            input.name = name;
            input.value = stored[name];
            form.appendChild(input);
        }
    };
    document.querySelectorAll('#lead-form, .js-form').forEach(fill);
})();
