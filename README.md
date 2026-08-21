# GlowBook

CRM для косметологов на **ASP.NET Core 8 + SQLite**.

## Запуск

```bash
cd src/GlowBook.Web
dotnet run
```

Страницы входа: `/auth/login`, регистрация: `/auth/register`

Профиль мастера (карточка как в Telegram, аватар, редактирование): `/profile`

## Авторизация (Google / Mail.ru / Telegram)

Ключи храни в `appsettings.Development.json` или User Secrets:

```bash
dotnet user-secrets set "Authentication:Google:ClientId" "..."
dotnet user-secrets set "Authentication:Google:ClientSecret" "..."
dotnet user-secrets set "Authentication:MailRu:ClientId" "..."
dotnet user-secrets set "Authentication:MailRu:ClientSecret" "..."
dotnet user-secrets set "Authentication:Telegram:BotToken" "..."
dotnet user-secrets set "Authentication:Telegram:BotUsername" "your_bot_name"
```

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
| **Fly.io** | Docker, persistent volume для SQLite, не засыпает | Нужна карта для регистрации |
| **Railway** | Простой деплой .NET, volume для БД | Лимит free credits |
| **Render** | Бесплатный web service | Засыпает через ~15 мин без трафика |
| **Timeweb / Beget VPS** | РФ, рубли, полный контроль | Платно, настраивать самому |

SQLite = один файл `glowbook.db`. На сервере он должен лежать на **persistent disk** (volume), иначе при перезапуске/деплое контейнера данные пропадут.

### Railway: почему данные затираются и куда класть БД

На Railway контейнер **эфемерный**: каждый деплой собирает новый образ, локальный диск сбрасывается. Файл SQLite внутри `/app` поэтому исчезает.

**Что сделать (обязательно):**

1. В проекте Railway открой сервис GlowBook → **Settings → Volumes → Add Volume**.
2. Mount path укажи `/data` (или любой путь — Railway сам проставит `RAILWAY_VOLUME_MOUNT_PATH`).
3. Размер: **1 GB** достаточно.
4. Задеплой ещё раз.

Приложение само кладёт `glowbook.db` на volume:
- если есть `RAILWAY_VOLUME_MOUNT_PATH` (после добавления volume) — туда;
- иначе если существует `/data` — туда;
- иначе `DATA_DIR` из переменных окружения;
- локально — `src/GlowBook.Web/Data/glowbook.db`.

Можно явно задать переменную:

```
DATA_DIR=/data
```

После этого клиенты, записи, аккаунты и аватары переживают деплой.

**Важно:** volume не восстанавливает уже потерянные данные — только защищает новые. Сделай volume до того, как снова заведёшь мастеров.

**Надёжнее на проде:** Railway **PostgreSQL** (отдельный плагин) + строка `ConnectionStrings__DefaultConnection`. SQLite + volume проще и нормально для старта; Postgres лучше, если будет несколько инстансов или хочется бэкапы из коробки.

### Деплой на Fly.io (кратко)

```bash
cd C:\Users\timofey\RiderProjects\GlowBook
fly launch
fly volumes create glowbook_data --size 1 --region ams
# в fly.toml: mount source=glowbook_data, destination=/app/Data
fly secrets set YooKassa__ShopId=... YooKassa__SecretKey=...
fly deploy
```

Dockerfile уже есть — БД монтируется в `/app/Data`.

## Дальше
- MAUI/Capacitor WebView-приложение
- SMS/Telegram-напоминания
