# Подключение игры к Support Gateway

Сервис принимает обращения игроков и доставляет их в Jira Service Management.
Игра к Atlassian не обращается: ей нужен только этот сервис и один секрет.

Прод: `https://support.flushee.work`. Swagger — `/docs`.

## Что нужно сделать один раз

1. Завести игру в админке (`/admin` → Игры): `game_id`, название, значение
   поля Game в JSM (буква в букву, как в списке Jira), origins веб-клиента.
2. Забрать **секрет** — показывается один раз. Хранится только на бэкенде игры.
3. Для веб-игр: origin должен быть и в игре в админке (CORS сервиса), и в
   CORS-правиле бакета — иначе браузер не загрузит вложение.

## Токен сессии

Единственный код, который пишет бэкенд игры. JWT HS256, подписан секретом
игры, срок жизни 15 минут; клиент получает готовый токен и передаёт его в SDK
или в заголовок `Authorization: Bearer …`.

```json
{
  "iss": "booty_heroes",
  "sub": "48213",
  "iat": 1757318400,
  "exp": 1757319300,
  "jti": "e1c0…",
  "ctx": {
    "email": "player@example.com",
    "platform": "web",
    "locale": "en-US",
    "game_version": "3.14.2",
    "tags": ["payer", "steam"]
  }
}
```

| Поле | Что это |
| --- | --- |
| `iss` | `game_id`, как в админке |
| `sub` | id игрока в игре; пустая строка — аноним (если игре разрешено) |
| `jti` | id сессии окна, случайный UUID: вложения принимаются только с тем `jti`, под которым выдавался presign. Новый — на каждое открытие окна; при обновлении токена внутри сессии — тот же |
| `ctx.email` | подтверждённая почта: поле в форме заблокировано, введённое руками игнорируется |
| `ctx.platform` | `web` / `steam` / `nutaku` — попадает в поле Platform тикета |
| `ctx.tags` | метки тикета в Jira: сегмент, плательщик, канал. Игроку не видны |
| `ctx.*` | `locale`, `region`, `game_version`, `build`, `env`, `session_id`, `device_id`, `transaction_hash` — в техблок описания |

Всё в `ctx` необязательно. Пароли, токены, данные карт в `ctx` отбрасываются
и в тикет не попадают ни при каких условиях.

Python:

```python
import jwt, time, uuid

def support_token(game_id: str, secret: str, user_id: str, jti: str, ctx: dict) -> str:
    now = int(time.time())
    return jwt.encode(
        {"iss": game_id, "sub": str(user_id), "iat": now, "exp": now + 900,
         "jti": jti, "ctx": ctx},
        secret, algorithm="HS256",
    )
```

`jti` удобнее выбирать клиенту при открытии окна и передавать в запрос токена:
тогда при обновлении токена он остаётся тем же без состояния на сервере.
Бэкенду достаточно проверить формат (`^[0-9a-f-]{16,64}$`).

Node:

```js
const jwt = require('jsonwebtoken');
const token = jwt.sign({iss: gameId, sub: String(userId), jti, ctx},
                       secret, {algorithm: 'HS256', expiresIn: '15m'});
```

Токен выдаётся по запросу клиента, когда игрок открывает окно поддержки —
не при логине, иначе к моменту отправки он протухнет.

### Продление сессии

Игрок может писать дольше 15 минут. Оба SDK на `401` сначала просят у игры
свежий токен (JS — вызов `token({reason: 'expired'})`, Unity — провайдер с
`SupportGateTokenReason.Expired`), а если токен задан строкой или игра вернула
тот же — продлевают его через шлюз и повторяют запрос с тем же
`Idempotency-Key`. Ошибку игрок видит, только когда не помогло и это.

```
POST /v1/session/refresh
Authorization: Bearer <старый токен>
```

```json
{"token": "eyJ…", "expires_in": 900}
```

Шлюз проверяет подпись, переподписывает те же `iss`/`sub`/`jti`/`ctx` с новым
`exp`; сам токен не меняет прав. Ограничения: старый токен принимается не
позже 30 минут после `exp`, а всей сессии — не дольше 3 часов от первого `iat`
(`session_refresh_grace_seconds`, `session_max_lifetime_seconds`). Дальше
только новый токен от игры.

## Три способа отправить обращение

### JS SDK — веб-игры

Один файл без зависимостей, отдаётся самим сервисом — в репо игры ничего не
копируется, правки формы доезжают без релиза игры. Путь версионный: ломающее
изменение выйдет как `/sdk/v2/`, `v1` останется.

```html
<script src="https://support.flushee.work/sdk/v1/support_gate.js"></script>
<script>
  SupportGate.init({endpoint: 'https://support.flushee.work', token: () => game.freshSupportToken()});
  helpButton.onclick = () => SupportGate.open();
</script>
```

`token` вызывается на каждый запрос — функция должна кэшировать токен и
брать новый только незадолго до `exp`, с тем же `jti`. `open({onResult})`
сообщает итог: `sent` (`ticketId`, `issueKey`), `error` (`stage`, `message`,
`httpStatus`, `code`) или `closed` — `sent`/`error` полезно писать в лог
игрока, чтобы обращение было видно рядом с его действиями. Скрипт можно
грузить и лениво, по клику.

Уровни глубже — форма в своём попапе (`mount`), только клиент под свою
вёрстку, голый транспорт — в [`sdk/js/README.md`](../js/README.md).

### Unity SDK

UPM-пакет `com.hooligapps.supportgate` из `sdk/unity`,
[`sdk/unity/README.md`](../unity/README.md). Два пути:

- **Форма в браузере** — `SupportGateBrowser.Open(endpoint, token)` открывает
  страницу шлюза `/form` в системном браузере: та же веб-форма, файлы через
  диалог браузера, без плагинов. Токен во фрагменте адреса; страница сама
  продлевает сессию через шлюз до 3 часов. Итог отправки в игру не
  возвращается.
- **Форма в игре** — готовые uGUI и UI Toolkit, презентер под свой View или
  только `SupportGateClient`. Строки и направление письма берутся из
  `/v1/form`, итог показа — событие `Completed` с теми же `sent` / `error` /
  `closed`, что у `onResult` в JS. Стенд со всеми состояниями на обоих
  рендерерах и без сети — `sdk/unity-demo`.

### REST — всё остальное

Бэкенд игры, Electron, нативный клиент. Три вызова, все с
`Authorization: Bearer <token>`, тела — JSON.

**1. Справочник формы** — категории, подкатегории, дополнительные поля,
лимиты вложений, локализованные подписи. Нужен, чтобы не хардкодить категории:
их меняют в сервисе без релиза игры.

```
GET /v1/form
```

```json
{
  "game": "booty_heroes",
  "locale": "en",
  "direction": "ltr",
  "strings": {"ui.title": "Contact support", "ui.submit": "Send", …},
  "prefill": {"email": "player@example.com", "email_locked": true},
  "categories": [
    {"id": "payment", "label": "Payment",
     "subcategories": [{"id": "missing_purchase", "label": "Missing Purchase"}, …],
     "extra_fields": [
       {"id": "payment_method", "type": "select", "label": "Payment Method",
        "options": [{"id": "apple", "label": "Apple"}, …]},
       {"id": "purchase_date", "type": "date", "label": "Purchase Date"},
       {"id": "transaction_id", "type": "text", "label": "Transaction ID"}
     ]},
    …
  ],
  "attachments": {"max_files": 5, "max_file_size": 26214400,
                  "allowed_types": ["image/png", "image/jpeg", "image/webp", "image/gif", "video/mp4", "video/webm"]}
}
```

`locale` — язык, который выбрал сервис по `ctx.locale` (иначе по
`Accept-Language`), строчный BCP47 как `ru`, `pt-br`; `direction` — `ltr`/`rtl`;
`strings` — подписи окна на этом языке. Плейсхолдеры — ICU MessageFormat:
`{id}`, `{mb}`, а числа с формой слова — `{max, plural, one {# file} other {# files}}`
(JS SDK подставляет сам; для своей вёрстки нужен ICU-форматтер или его подмножество).
Подписи категорий уже переведены в `label`. Языки добавляются на сервисе,
у игры ничего не меняется.

**2. Вложение** (если есть) — сервис файлы не принимает, только выдаёт
подписанную ссылку в хранилище.

```
POST /v1/attachments/presign
{"filename": "screenshot.png", "content_type": "image/png", "size": 184320}
```

```json
{"attachment_id": "att_01J8…", "upload_url": "https://s3…", "method": "PUT",
 "headers": {"Content-Type": "image/png"}, "expires_in": 900}
```

Дальше `PUT upload_url` с телом файла и ровно теми `headers`, без
`Authorization`. Ссылка живёт 15 минут. Тип и размер проверяются до выдачи
ссылки и ещё раз по факту при создании обращения.

**3. Обращение.** Заголовок `Idempotency-Key` обязателен — любая уникальная
строка на одну попытку отправки; повтор с тем же ключом после обрыва сети
вернёт то же обращение, а не создаст второе.

```
POST /v1/tickets
Idempotency-Key: 6f1c…
```

```json
{
  "category": "payment",
  "subcategory": "missing_purchase",
  "subject": "Bought gems, nothing arrived",
  "description": "I purchased the 500 gems pack and got nothing.",
  "email": "player@example.com",
  "extra": {"payment_method": "apple", "purchase_date": "2026-09-05", "transaction_id": "GPA.1234"},
  "attachment_ids": ["att_01J8…"],
  "client": {"device_model": "iPhone 15 Pro", "browser": "Safari 18", "screen": "1179x2556"}
}
```

`email` нужен, только если его нет в токене. `extra` — значения полей
выбранной категории из `/v1/form`; чужие поля отбрасываются. `client` —
диагностика, всё необязательно.

Ответ `201`:

```json
{"ticket_id": "tkt_01J8…", "issue_key": null, "status": "queued",
 "created_at": "2026-09-18T12:00:00Z"}
```

Новое обращение возвращается с `issue_key=null` и `status=queued`: его уже
сохранили, доставкой занимается воркер. Для игрока это успех; показывайте
`ticket_id`, пока номера Jira нет. `GET /v1/tickets/{ticket_id}` отдаёт актуальный
статус (той же сессии, что отправляла); встроенная форма JS SDK опрашивает его
~12 секунд после отправки и подменяет `tkt_…` на `SUP-…`, как только воркер
доставит обращение. Ключ идемпотентности ограничен 128 символами и действует в пределах
игры и владельца (user_id, а для анонимного игрока — jti).

### Ошибки

Тело всегда одно и то же, внутри `detail`:

```json
{"detail": {"error": "validation_failed", "message": "some fields are invalid",
            "fields": {"email": "email is required"}}}
```

| HTTP | `error` | Что делать |
| --- | --- | --- |
| 400 | `missing_idempotency_key` | добавить заголовок |
| 401 | `invalid_token` | продлить через `POST /v1/session/refresh` или взять свежий токен у бэкенда игры, повторить с тем же `Idempotency-Key` |
| 404 | `not_found` | обращение не этой игры или не существует |
| 413 | `file_too_large` | показать у поля вложения |
| 422 | `unsupported_type` | показать у поля вложения |
| 422 | `validation_failed` | подсветить поля из `fields` |
| 429 | `rate_limited` | «подождите перед следующим обращением» |
| 5xx | `internal_error` | показать ошибку отправки, данные формы не терять; повторить с тем же `Idempotency-Key` |

Лимиты на игрока: 5 обращений в час, 20 в сутки, 30 presign в час; на IP —
20 обращений в час.

## Что уезжает в Jira

Тема `[Название игры] <subject>` — **видна игроку** в портале и письмах, ничего
служебного в неё не попадает. В кастомные поля: Game, User ID, Platform. В
описание: текст игрока, категория и подкатегория, техданные из токена и
`client`, платёжные поля, ссылка на карточку в нашей админке. Вложения
переносятся после создания тикета. Если в токене есть `email`, игрок
становится репортером и получает ответы на почту; `tags` — метками.

## Локальная отладка

При `SG_ENV=local` сервис сам выпускает токен, без бэкенда игры:

```sh
curl -s -X POST localhost:8000/v1/dev/token -H 'Content-Type: application/json' \
  -d '{"game_id":"booty_heroes","user_id":"48213","context":{"platform":"web","locale":"en-US"}}'
```

В проде этой ручки нет. `sdk/js/demo/index.html` умеет получать токен этой
ручкой и открывать окно против локального сервиса.
