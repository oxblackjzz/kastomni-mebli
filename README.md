# Кастомні Меблі

Сайт-візитка з прийомом заявок: заявка → база PostgreSQL → повідомлення в Telegram.
Стек: ASP.NET Core 10 (Blazor, серверний рендер), EF Core + Npgsql, Docker, Render.

```
src/KastomniMebli.Web/     сайт
  Components/Landing/      блоки лендінгу (креслення — SVG у WardrobeDrawing / KitchenDrawing)
  Leads/                   форма заявки: валідація, телефон, спам-захист, ендпоінт POST /zayavka
  Notifications/           Telegram
  Data/                    модель, DbContext, міграції
  appsettings.json         контакти, райони, команда, FAQ (секція "Site")
tests/KastomniMebli.Tests/ тести
```

## Запуск локально

Потрібно: .NET SDK 10 і PostgreSQL (локальний або зовнішній URL бази з Render).

```powershell
copy .env.example .env      # і вписати DATABASE_URL, TELEGRAM_BOT_TOKEN, TELEGRAM_CHAT_ID
dotnet run --project src/KastomniMebli.Web
```

Сайт відкриється на http://localhost:5155. Таблиці створюються самі при старті (EF-міграції).

Тести (база не потрібна):

```powershell
dotnet test
```

## Telegram-бот

1. У Telegram написати [@BotFather](https://t.me/BotFather) → `/newbot` → отримати токен → це `TELEGRAM_BOT_TOKEN`.
2. Створити групу (ти + брат), додати туди бота, написати в групі будь-що.
3. Відкрити `https://api.telegram.org/bot<ТОКЕН>/getUpdates` — у відповіді `"chat":{"id":-100...}` → це `TELEGRAM_CHAT_ID`.
   Замість групи можна вказати кілька особистих id через кому (кожен має спершу написати боту `/start`).

## Ручна перевірка

1. Відкрити сайт на комп'ютері й на телефоні, пройтись по всіх розділах.
2. Надіслати заявку → має з'явитись «Дякуємо, <ім'я>!…», повідомлення в Telegram і рядок у таблиці `leads`:
   `select id, created_at, name, phone, furniture_types, telegram_sent_at, telegram_error from leads order by id desc;`
3. Невірний телефон (`123`) → червона підказка під полем.
4. Шоста заявка за годину з однієї адреси → «Забагато заявок…».
5. Тимчасово зіпсувати `TELEGRAM_BOT_TOKEN` → заявка все одно в базі, у `telegram_error` — причина.

## Деплой на Render

Автодеплой: push у гілку `main` → Render сам збирає Docker-образ.

Перший раз:
1. Render → **New → Blueprint** → вибрати репозиторій `kastomni-mebli` → Render прочитає `render.yaml`
   і створить сайт + базу. (Або створити Web Service вручну з Dockerfile і вказати `DATABASE_URL` існуючої бази.)
2. У налаштуваннях сервісу → **Environment** заповнити `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID`, `Site__Phone`, `Site__Telegram`.
3. Після деплою відкрити `https://<сервіс>.onrender.com/healthz` → має бути `ok`.
4. Надіслати тестову заявку з телефона (мобільний інтернет) і з Wi-Fi: у таблиці `leads` поле `ip_hash`
   має відрізнятися — отже обмеження частоти рахує кожного клієнта окремо.

## Налаштування

| Що | Де |
|---|---|
| Телефон, Telegram на сайті | `Site__Phone`, `Site__Telegram` (Render) або `appsettings.json` |
| Райони, ролі команди, FAQ | `appsettings.json` → `Site` |
| Ліміт заявок з однієї IP за годину | `Leads:RateLimitPerHour` (5) |
| Мінімальний час заповнення форми, с | `Leads:MinFillSeconds` (3) |
