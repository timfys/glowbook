# GlowBook

CRM для косметологов на **ASP.NET Core 8 + SQLite**.

## Запуск

```bash
cd src/GlowBook.Web
dotnet run
```

Страницы входа: `/auth/login`, регистрация: `/auth/register`

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

SQLite = один файл `Data/glowbook.db`. На сервере он должен лежать на **persistent disk** (volume), иначе при перезапуске контейнера данные пропадут.

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
