# GlowBook

CRM для косметологов на **ASP.NET Core 8**. Локально — **SQLite**, на Railway — **PostgreSQL**.

## Запуск

```bash
cd src/GlowBook.Web
dotnet run
```

Локально используется SQLite (`Data/glowbook.db`). В Production / при `DATABASE_URL` — Postgres.

Страницы входа: `/auth/login`, регистрация: `/auth/register`

Профиль мастера (карточка как в Telegram, аватар, редактирование): `/profile`

## Авторизация (сейчас: Mail.ru)

Ключи Mail.ru лежат в `appsettings.json` / `appsettings.Production.json` / `appsettings.Development.json`.

В кабинете приложения на [o2.mail.ru](https://o2.mail.ru/) redirect URI должен **точно** совпасть (схема, хост, порт, путь, без лишнего `/` в конце):

| Где | Redirect URI |
|-----|----------------|
| Локально (профиль `https`) | `https://localhost:7159/signin-mailru` |
| Локально (профиль `http`) | `http://localhost:5107/signin-mailru` |
| Railway / прод | `https://ВАШ-ДОМЕН.up.railway.app/signin-mailru` |

Можно указать несколько URI в приложении Mail.ru — и localhost, и прод.

Ошибка `bad redirect_uri` = URI в запросе ≠ URI в кабинете Mail.ru (не из‑за «локали само по себе»).

Google / VK ID / Telegram пока выключены (пустые ключи + скрыты на логине).

## Клинический кабинет (как CosmoCare)

- **Клиенты** — досье: аллергии, проблемы кожи, заметки, быстрый поиск
- **Карточка клиента** — история процедур (препараты/аппарат), фото до/после, домашний уход
- **Календарь** — недельный обзор записей, быстрый статус «Готово»
- **Обзор** — выручка за день и месяц (завершённые записи + процедуры с ценой)

## Premium + ЮKassa (500 ₽/мес)

1. Зарегистрируйся на [yookassa.ru](https://yookassa.ru) (ИП / самозанятый)
2. В личном кабинете возьми **Shop ID** и **Secret Key** (для теста — тестовые ключи)
3. Добавь в User Secrets:

```bash
dotnet user-secrets set "YooKassa:ShopId" "..."
dotnet user-secrets set "YooKassa:SecretKey" "..."
```

4. В настройках ЮKassa укажи webhook: `https://ВАШ-ДОМЕН/api/yookassa/webhook`
5. После оплаты Premium активируется на 30 дней — в кабинете появится ссылка `/book/{slug}`

### Онлайн-запись

- Публичная страница: `/book/{slug}` (slug создаётся при регистрации мастера)
- Работает только с активным Premium
- Клиент выбирает услугу, дату, время, оставляет имя и телефон

## GitHub — можно ли «чтобы открывалось в браузере»?

**GitHub Pages — нет.** Там только статические HTML/CSS/JS. ASP.NET Core сервер и SQLite там не запустятся.

**GitHub — да, но только как хранилище кода** (git push). Чтобы сайт открывался в браузере, нужен хостинг с сервером.

## Куда выкладывать приложение и БД

| Платформа | Плюсы | Минус |
|-----------|-------|-------|
| **Railway** | Простой деплой .NET, Postgres | Usage credits |
| **Render** | Бесплатный web service | Засыпает через ~15 мин без трафика |
| **Timeweb / Beget VPS** | РФ, рубли, полный контроль | Платно, настраивать самому |

### Railway + PostgreSQL (текущий прод)

Креды лежат в репо: [`railway-postgres.env`](railway-postgres.env) и [`src/GlowBook.Web/appsettings.Production.json`](src/GlowBook.Web/appsettings.Production.json).

**Привязать web-сервис `glowbook` к БД:**

1. Открой сервис **glowbook** → **Variables** → **+ New Variable**.
2. Добавь reference (сервис БД называется **Postgres**):

```
DATABASE_URL=${{Postgres.DATABASE_URL}}
```

Хост: `postgres.railway.internal`. Полный набор переменных — в [`railway-postgres.env`](railway-postgres.env).

3. Redeploy `glowbook`. При старте `MigrateAsync()` создаст таблицы в Postgres.

Данные из старого SQLite на volume **сами не переносятся** — Postgres стартует пустым (аккаунты/клиенты заводишь заново). Volume `glowbook-volume` для SQLite после перехода можно удалить.

**С другого ПК / снаружи Railway:** во внутренний `postgres.railway.internal` не попасть. В сервисе Postgres → **Settings → Networking → TCP Proxy** → бери `DATABASE_PUBLIC_URL` и подставляй в `ConnectionStrings:DefaultConnection`.

## Дальше
- MAUI/Capacitor WebView-приложение
- SMS/Telegram-напоминания
