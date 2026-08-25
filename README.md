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

**С другого ПК / снаружи Railway:** во внутренний `postgres.railway.internal` не попасть. Два варианта:

| Способ | Когда | Как |
|--------|--------|-----|
| **SSH-туннель** (предпочтительно) | Локальная разработка, DBeaver, миграции | См. ниже |
| **TCP Proxy / public URL** | Когда туннель неудобен | Postgres → **Settings → Networking → TCP Proxy** → `DATABASE_PUBLIC_URL` в `ConnectionStrings:DefaultConnection` |

Public URL (`altaria.proxy.rlwy.net`) с домашнего канала часто таймаутит — для повседневной работы бери туннель.

### Postgres через SSH-туннель (с другого компа)

Цель: Railway Postgres оказывается на `127.0.0.1:5432`. Appsettings Development уже настроен на этот адрес.

**Один раз на новом ПК:**

1. Установи [Railway CLI](https://docs.railway.com/guides/cli):
   ```powershell
   iwr https://railway.com/install.ps1 | iex
   ```
2. Войди и привяжи проект (из корня репо):
   ```powershell
   railway login
   railway link
   ```
   Выбери проект **glowbook**, сервис **Postgres** (имя как на канвасе).
3. Добавь SSH-ключ (если ещё не добавлял на этом аккаунте / машине):
   ```powershell
   railway ssh keys add
   ```

**Каждый раз, когда нужна БД:**

1. В отдельном окне PowerShell (не закрывать):
   ```powershell
   .\scripts\postgres-tunnel.ps1
   ```
   Эквивалент: `railway connect Postgres --tunnel-only -P 5432`
2. Пока окно открыто — подключайся к локальному порту:

| | |
|--|--|
| Host | `127.0.0.1` |
| Port | `5432` |
| Database | `railway` |
| User | `postgres` |
| Password | из [`railway-postgres.env`](railway-postgres.env) (`POSTGRES_PASSWORD`) или Variables сервиса Postgres |
| SSL | `Disable` |

3. Запуск приложения с Dev-конфигом (уже смотрит в туннель):
   ```powershell
   cd src/GlowBook.Web
   dotnet run
   ```
   Connection string: [`appsettings.Development.json`](src/GlowBook.Web/appsettings.Development.json).

**DBeaver / другой клиент:** те же Host/Port/DB/User/Password, SSL off. Туннель должен быть запущен.

**Миграция SQLite → Postgres через туннель:** держи туннель открытым, затем `tools/MigrateNow` (см. комментарии в `tools/MigrateNow/Program.cs`).

**Если порт 5432 занят** (локальный Postgres):
```powershell
.\scripts\postgres-tunnel.ps1 -LocalPort 5433
```
и поправь Port в connection string / клиенте.

## Дальше
- MAUI/Capacitor WebView-приложение
- SMS/Telegram-напоминания
