// Перемикач теми: типово сайт світлий; темну вмикає сама людина кнопкою ☾, вибір пам'ятає браузер.
// Початкова тема ставиться маленьким скриптом у <head> (App.razor) — щоб не було спалаху.
(() => {
    const KEY = 'km_theme';

    const current = () => document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light';

    const apply = (theme) => {
        if (theme === 'dark') document.documentElement.dataset.theme = 'dark';
        else delete document.documentElement.dataset.theme;
        document.querySelectorAll('[data-theme-toggle]').forEach((b) => {
            b.textContent = theme === 'dark' ? '☀' : '☾';
            b.setAttribute('aria-label', theme === 'dark' ? 'Світла тема' : 'Темна тема');
            b.title = b.getAttribute('aria-label');
        });
    };

    const saved = () => { try { return localStorage.getItem(KEY); } catch { return null; } };

    document.addEventListener('click', (e) => {
        const button = e.target.closest('[data-theme-toggle]');
        if (!button) return;
        const next = current() === 'dark' ? 'light' : 'dark';
        try { localStorage.setItem(KEY, next); } catch { /* приватний режим */ }
        apply(next);
    });

    apply(saved() === 'dark' ? 'dark' : 'light');
    // CRM (Blazor) при переходах може оновити сторінку — повертаємо вибрану тему.
    window.Blazor?.addEventListener?.('enhancedload', () => apply(saved() === 'dark' ? 'dark' : 'light'));
    document.addEventListener('DOMContentLoaded', () => apply(saved() === 'dark' ? 'dark' : 'light'));
})();
