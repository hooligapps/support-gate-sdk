# Support Gate — веб-клиент

UMD-бандл с окном обращения в поддержку: справочник категорий, вложения и
отправка через Support Gateway. Категории, лимиты и язык приходят с сервера
(`GET /v1/form`), поэтому менять их можно без релиза игры.

## Подключение

Бандл отдаёт сам сервис: `https://support.flushee.work/sdk/v1/support_gate.js`
Копировать его в репо игры не нужно.

```html
<script src="https://support.flushee.work/sdk/v1/support_gate.js"></script>
<script>
  SupportGate.init({
    endpoint: 'https://support.flushee.work',
    token: sessionToken,        // строка или функция, возвращающая свежий токен
  });

  document.getElementById('help').addEventListener('click', function () {
    SupportGate.open();
  });
</script>
```

### Токен сессии

`token` — JWT HS256, который выпускает **бэкенд игры** своим секретом (срок
жизни 15 минут). Секрет игры и учётки Jira в клиент не попадают никогда. Если
токен живёт меньше сессии окна, передавайте функцию:

```js
SupportGate.init({endpoint: ..., token: () => myGame.freshSupportToken()});
```

В токене: `iss` — идентификатор игры, `sub` — игрок (пустой для анонима), `ctx` —
контекст (`locale`, `platform`, `game_version`, …). Если в `ctx` есть
подтверждённый `email`, поле почты в форме заполнено и заблокировано. Пароли,
токены и данные карт в `ctx` не принимаются. `tags` (список) становятся метками
тикета в Jira — сегмент, плательщик, канал: это знает бэкенд игры, а не форма.
Игроку метки не видны.

## API

| Метод | Что делает |
| --- | --- |
| `SupportGate.init(options)` | создаёт клиента; повторный вызов заменяет настройки |
| `SupportGate.open(options?)` | открывает модальное окно с формой |
| `SupportGate.close()` | закрывает окно |
| `SupportGate.mount(host, options?)` | встраивает форму в свой контейнер |
| `SupportGate.getInstance()` | клиент для своей вёрстки |

`options`: `onResult(result)` — итог окна, `onClose()` — при закрытии окна,
`onSubmitted(ticket)` — после успешной отправки (то же, что `sent`).

`result.status`:

| | Когда | Поля |
| --- | --- | --- |
| `sent` | обращение принято, один раз | `ticketId` (`tkt_…`), `issueKey` (`SUP-…` или `null`, пока воркер не доставил), `ticket` |
| `error` | каждая неудачная попытка — игрок может повторить | `stage` (`load` — форма не загрузилась, `submit` — не ушло), `message`, `kind`, `httpStatus`, `code` |
| `closed` | окно закрыли, ничего не отправив | — |

Текст `message` не переведён и для показа не предназначен — это для лога игры.

### Своя вёрстка

Если форма игры своя, нужен только клиент — DOM он не трогает:

```js
const client = SupportGate.init({endpoint, token});

const config = await client.form();              // категории, лимиты, prefill
const attachment = await client.upload(file);    // файл уходит прямо в хранилище
const ticket = await client.submit({
  category: 'payment',
  subcategory: 'refund',
  subject: 'Не пришла покупка',
  description: '…',
  email: 'player@example.com',
  extra: {payment_method: 'apple'},
  attachment_ids: [attachment.attachment_id],
});
```

`submit` вторым аргументом принимает ключ идемпотентности: повтор после сетевой
ошибки с тем же ключом вернёт то же обращение, а не создаст второе. Без
аргумента ключ генерируется на каждый вызов.

Ошибки — всегда `SupportGateError`: `kind` (`invalid_request` / `network` /
`http` / `parse`), `status`, `code`, `fields` (ошибки по полям формы) и
`transient` — признак того, что повтор имеет смысл.

## Разработка

```sh
npm install
npm run typecheck
npm test          # собирает dev-бандл и гоняет тесты по нему (jsdom)
npm run build     # прод-сборка в dist/support_gate.js
```

`demo/index.html` открывает окно против локального сервиса и умеет сам получить
токен через `POST /v1/dev/token` (ручка живёт только при `SG_ENV=local`).

Сборка — Vite в библиотечном режиме (`vite.config.mts`), как и в `admin-ui`:
один UMD-файл с глобальным `SupportGate`, без внешних зависимостей. В прод-сборке
`__SG_DEBUG__` становится литералом `false`, и отладочный лог из бандла вырезается.
