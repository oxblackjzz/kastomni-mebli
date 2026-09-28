# Кастомні Меблі — контекст проєкту

Спілкуватись з власником українською. Інтерфейс, тексти сторінок, повідомлення користувачам — українською.

## Хто і що

Власник — конструктор корпусних меблів (AutoCAD + 3D-Constructor), за основною роботою начальник відділу автоматизації на меблевому виробництві; сам підтримує свій MES.

«Кастомні Меблі» — онлайн-канал пошуку клієнтів на індивідуальні корпусні меблі (шафи-купе, гардеробні, кухні, передпокої, дитячі, нестандартні ніші).

Команда (3 людини, 2 роки разом):
- **Брат** — замірник, менеджер з продажу, бухгалтер. Веде клієнта й гроші.
- **Кум брата** — збирання й монтаж.
- **Власник** — конструктор: креслення, фурнітура, вписування модулів під техніку й комунікації.

Мета: власний канал заявок (усі креслення — власник) і прозорий облік замовлень та грошей із видимою часткою кожного.

## Дорожня карта (СТРОГО по одному етапу)

1. Візитка + прийом заявок: лендінг → заявка в БД → миттєве сповіщення в Telegram.
2. CRM: замовлення, статуси, клієнти, оплати, витрати, частки учасників.
3. Сторінка для сторонніх меблярів (B2B-послуги конструктора), заявки в ту саму CRM з окремим типом.
4. Автопостинг: Telegram-канал, потім Facebook/Instagram (Meta Graph API).
5. Онлайн-конструктор меблів — пізніше, зараз НЕ чіпати.

**Поточний стан (2026-09-27):** етапи 1–4 реалізовано й задеплоєно на https://kastomni-mebli.onrender.com (репо `oxblackjzz/kastomni-mebli`, гілка `main`; стара історія — `archive/prototype-2026-01`). Плюс портфоліо (`/roboty`, 6 місць на головній). Відкрито від власника: Telegram-бот і канал, Meta (Facebook/Instagram) — ключі, телефон/Telegram для сайту, ціни B2B, перевірка FAQ. **TikTok** — чекає рішення власника (див. нижче). Етап 5 не чіпати.

## Портфоліо, B2B, автопостинг (етапи 3–4)

- Файлове сховище — `Crm/FileStore.cs` (Render Disk). Портфоліо: `PortfolioWork/PortfolioPhoto`, фото стискаються в браузері (`RequestImageFileAsync`: 1920 + мініатюра 720), публічна видача `/roboty/foto/{id}/{t|l}` (чернетки — лише для CRM).
- B2B: `Order.Kind = b2b`, свій цикл статусів (`OrderStatuses.B2bFlow`, фінальний — `paid`), без часток; форма `/dlya-meblyariv` → `B2b/B2bEndpoints.cs` (форма читається вручну, ліміт тіла піднято до ~65 МБ). Ціни — JSON у `settings` (`b2b_pricing`), в коді жодних цифр. Дашборд: B2B окремо (`B2bReport`), у гроші команди не входить.
- Автопостинг: `Posting/` — `IPostChannel` на мережу (Telegram Bot API; Meta Graph **v26.0**: Facebook /feed|/photos, Instagram контейнери + карусель). `PostPublisher` (раз на 30 с) публікує `post_targets` окремо; помилка → журнал + `AdminNotifier`. Перервана перезапуском публікація → failed (щоб не задублювати). Фото постів готує `wwwroot/js/crm-posts.js` (обрізка 4:5…1.91:1, ≤1440 px) і віддаються публічно за випадковим ключем `/media/posty/{key}.jpg` (Meta забирає за URL).
- Meta: застосунок Business у режимі Development, Standard access, токен System User «Never» — App Review не потрібен (лише власні акаунти).
- TikTok (досліджено 2026-09-27): Direct Post без аудиту — лише приватні пости, а аудит TikTok забороняє для «утиліт публікації у власні акаунти». Реальний шлях — Upload/inbox (`video.upload`, `post_mode=MEDIA_UPLOAD`, фото лише PULL_FROM_URL з підтвердженого URL-префікса, JPEG ≤1080p, ≤5 чернеток на добу): сервер кладе чернетку, людина публікує в застосунку TikTok. Потрібні: developer-застосунок, Login Kit (OAuth, access 24 год / refresh 365 днів), сторінки Privacy/Terms, верифікація URL, app review. Не реалізовано — чекає рішення власника.

## Покращення (2026-09-28)

- SEO: `Components/Shared/SeoMeta.razor` (description, canonical, og:*) — **на сторінці лише один HeadContent** (другий затирає перший), додаткові теги — дочірнім вмістом SeoMeta. `/robots.txt`, `/sitemap.xml` (`Settings/SeoEndpoints.cs`), JSON-LD HomeAndConstructionBusiness на головній, `/og.png`. Політика конфіденційності `/konfidentsiinist` + згода під формами. Кнопки Telegram/Viber (`ContactButtons`).
- Мітки: `wwwroot/js/attribution.js` (перший візит у localStorage → приховані поля форм) → `Leads/Attribution.cs` → `leads.utm_*`, `orders.channel/campaign`. Дашборд: `SiteChannels`, `AdsReport` (реклама з загальних витрат категорії `ads`).
- Сайт із CRM: `Settings/SiteContent.cs` — власна реалізація `IOptions<SiteSettings>` (конфіг + JSON `site_content` у `settings`), зміни діють одразу. `settings.value` — без обмеження довжини; JSON пишеться з `SettingsService.Json` (кирилиця без \uXXXX — інакше в 6 разів довше).
- Облік: `audit_log` (лише додається; `OrderService.Audit` у тій самій транзакції), `PayAllOwedAsync`, `orders.cancel_reason/cancel_note` (`Catalog.CancelReasons`), `company_expenses` (`CompanyExpenseService`), CSV-вивантаження `ExportService` (BOM, «;», кома, захист від формул).
- Календар `/crm/kalendar` (`OrderService.CalendarAsync`), PWA-маніфест `/crm.webmanifest`, `/healthz` перевіряє БД.
- Урок: тести на EF InMemory не ловлять обмеження довжини колонок — критичні форми перевіряти на справжньому Postgres (портативний у scratchpad).

## CRM (етап 2)

- Усе в одному застосунку: `/crm`. Сторінки CRM — Interactive Server без пререндеру (`CrmRender.Mode`), `blazor.web.js` підключається лише на `/crm` (App.razor), лендінг лишається статичним.
- `Components/Crm/_Imports.razor` задає `@layout CrmLayout` і `[Authorize]` — тому **макети лежать у `Components/Layout`** (у папці Crm макет загортав би сам себе → нескінченний цикл). Сторінки входу/403 — `Components/Pages/CrmLogin|CrmDenied` (поза Crm, без [Authorize]).
- Вхід: cookie-автентифікація, хеш паролів — `PasswordHasher` з ASP.NET Identity (без нових пакетів). Форма входу постить на `/crm/uviyty` (окремо від сторінки `/crm/vhid`, бо сторінка Blazor теж приймає POST), антифорджері перевіряється вручну (протермінований токен → `?error=expired`, не 500). Кожен запит звіряє cookie з БД (активний, роль, `security_stamp`). Перший адмін — з `ADMIN_LOGIN`/`ADMIN_PASSWORD`, лише якщо користувачів немає.
- Ключі DataProtection — у таблиці `data_protection_keys` (`DbXmlRepository`), щоб редеплой не викидав з CRM.
- Ролі: admin / manager / installer. Монтажнику гроші не вантажаться з БД взагалі (`OrderService.GetAsync`), перевірки прав — у сервісах, не лише в UI.
- Гроші — `Crm/OrderFinance.cs` (чисті функції, тести), дашборд — `Crm/Dashboard.cs` (`DashboardCalc.Build`, тести). Правила обліку місяця (погоджено власником): виручка — за датою оплати; прибуток і заробіток — за завершеними в місяці; прибуток = отримано − витрати − частки; прибуток не ділиться (спільні гроші). Частки — з шаблону (`share_templates`), у замовленні редагуються; «виплачено» фіксує суму.
- Заявка з сайту → клієнт (пошук за телефоном) + замовлення «Нова заявка» (`OrderService.CreateFromLeadAsync`); при старті `BackfillLeadsAsync` добирає заявки без замовлень.
- Файли — Render Disk `/var/data` (`Files__Root=/var/data/files`), у БД лише опис (`order_files`). Тому в Dockerfile немає `USER $APP_UID` (диск монтується з правами root). Видача: `GET /crm/fajly/{id}` з перевіркою доступу.
- Telegram: `ITelegramSender` (один клієнт Bot API), `TelegramNotifier` — заявки, `CrmNotifier` — нове замовлення, «Монтаж» монтажнику, нагадування (`ReminderService`, щодня з 18:00 Києва, раз на день — ключ `reminders_last_run` у `settings`). Збій Telegram ніколи не ламає дію.
- Dockerfile: версії .NET зафіксовані (SDK 10.0.401, aspnet 10.0.12) і збірка падає, якщо в `/app/wwwroot/_framework/` немає `blazor.web.js`. Причина: з `sdk:10.0` + окремим кешованим `restore` на Render скрипт Blazor не потрапив в образ → сторінки CRM були порожні (локально й у тестах цього не видно). Після деплою перевіряти, що `/_framework/blazor.web.*.js` віддає 200.
- Тестова інфраструктура — `tests/.../TestInfra.cs` (`SiteFactory`, `CrmTestContext`, фейки Telegram і часу).

## Цей проєкт

- ASP.NET Core **net10.0** (LTS), Blazor Web App. Лендінг — статичний серверний рендер (без SignalR); Interactive Server — для CRM.
- PostgreSQL, EF Core 10 + Npgsql, **нормальні EF-міграції** (`Data/Migrations`), застосовуються при старті (`MigrateAsync`). Snake_case-мапінг у `AppDbContext`.
- Нова міграція: `dotnet ef migrations add <Назва> --project src/KastomniMebli.Web --output-dir Data/Migrations` (інструмент у `dotnet-tools.json`, `dotnet tool restore`).
- Тести: xUnit + WebApplicationFactory + EF InMemory (`dotnet test`). Telegram у тестах підміняється `FakeNotifier`.
- Налаштування сайту — секція `Site` в `appsettings.json` (перевизначення env `Site__Phone` тощо). На етапі 2 — у БД з адмінкою.
- Секрети: `DATABASE_URL`, `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID` (кілька id через кому). Локально — `.env` (читає `DotEnv.cs`).
- Заявка: `POST /zayavka` (`Leads/LeadEndpoints.cs`) — JSON для JS-форми, редирект `/?zayavka=...` без JS. Спам: honeypot `website`, мітка часу `t` (DataProtection, м'яка — невалідна мітка не блокує), rate limit 5/год з IP.
- IP клієнта за проксі Render: `CF-Connecting-IP` → перший у `X-Forwarded-For` (лише коли `Proxy__TrustForwardedHeaders=true`). Перевірити після деплою, що `ip_hash` різний з різних мереж.
- Таблиця `leads`: source=`site`, status=`new` — з кожної заявки створюється замовлення в CRM.
- Локально немає Docker і Postgres; для перевірки використовувався портативний Postgres у тимчасовій папці.
- Кольори дизайну в `wwwroot/app.css` (`:root`). «Дуб» для темної теми (`#8A6A48`) у ТЗ не було — підібрано.

## Правила роботи

- Перед кожним етапом: короткий план (файли, моделі, маршрути, міграції) → чекати «ок» → реалізувати.
- Нові залежності — лише з поясненням і після згоди власника.
- Секрети — лише через змінні оточення / `.env`; `.env` у `.gitignore`, у репо — `.env.example`.
- Малі логічні коміти. Після етапу — інструкція українською: запуск локально, ручна перевірка, деплой.
- Тести на критичну логіку (валідація заявки, гроші, частки).
- Мобільна версія обов'язкова (брат і монтажник — з телефона).
- Не вигадувати ціни, терміни, гарантії, бренди фурнітури в текстах для клієнтів — поле в налаштуваннях або TODO + сказати власнику.
- Відсотки часток НЕ зашивати в код — лише налаштування.
- MES не ламати; таблиці MES не змінювати без прямого дозволу. Цей проєкт — окремий застосунок.

## Референс: MES власника (той самий стек)

- Репо: `D:\tablitsya3` (github `oxblackjzz/tablitsya3`, гілка master).
- ASP.NET Core **net9.0**, Blazor Web App (Interactive Server), Bootstrap 5, SignalR.
- PostgreSQL через **Npgsql EF Core 9**, явний snake_case-мапінг таблиць/колонок.
- Рядок підключення: `ConnectionStrings:DefaultConnection` або `DATABASE_URL` (формат `postgres://` конвертується в Npgsql, `SSL Mode=Require`) — див. `Program.cs` MES.
- Деплой: **Render** (Docker web service, `render.yaml`, Frankfurt, Render Postgres, автодеплой з push у master). CI немає.
- У MES немає тестів, авторизації, Telegram, rate limiting — у цьому проєкті робимо це як слід.
- Відомі граблі MES: кодування кирилиці (тримати всі файли в UTF-8, `LANG=C.UTF-8` у Docker), саморобні SQL-міграції замість EF-міграцій (тут — нормальні EF-міграції), немає ForwardedHeaders за проксі Render.

## Стара спроба (не використовувати як код)

`C:\Users\pvakb\source\repos\CustomniMebli` (github `oxblackjzz/kastomni-mebli`) — прототип React + ASP.NET + SQLite від 2026-01-03, покинутий. Небезпечний (немає справжньої авторизації, публічний reset адміна). Можна брати лише як референс текстів/структури секцій.

## Дизайн (етап 1, далі — той самий стиль)

- Стиль «креслярський аркуш»: розмірні лінії зі стрілками, моно-шрифт для цифр/розмірів, тонкі лінії, мінімум тіней.
- Шрифти (Google Fonts, кирилиця): Unbounded — заголовки (стримано), Geologica — текст, JetBrains Mono — цифри, розміри, мітки.
- Світла тема: фон `#F2F3F0`, поверхня `#FFFFFF`, текст `#1B2226`, другорядний `#5B666B`, лінії `#D3D8D5`, акцент `#2C5A4C`, дуб `#C49A6C`, світлий дуб `#EBDCC8`, дзеркало `#DCE7E9`.
- Темна тема: фон `#141819`, поверхня `#1C2123`, текст `#E6E9E6`, другорядний `#9BA5A8`, лінії `#2F373A`, акцент `#86BBA8`, світлий дуб `#3A3024`, дзеркало `#2A3538`.
- Кольори — CSS-змінні; темна тема через `prefers-color-scheme`.

## Рішення (журнал)

- 2026-09-27: стек — як у MES (Postgres/EF Core, Docker, Render), але **net10.0** замість net9.0: на машині лише SDK 10, а підтримка .NET 9 закінчується в листопаді 2026. Стару спробу CustomniMebli не продовжуємо.
- 2026-09-27: хостинг — платний Render (сайт не засинає). Репозиторій `oxblackjzz/kastomni-mebli` перезаписуємо новим кодом. Домен — пізніше.
- 2026-09-27: географія — Звягель та район, Житомир, Рівне. Імен команди на сайті не показуємо (лише ролі).
- Відкрито (TODO від власника): телефон і Telegram для сайту; перевірити тексти FAQ (особливо про матеріали).
