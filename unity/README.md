# Support Gate — Unity

Клиент Support Gateway и форма обращения в поддержку. Справочник категорий,
лимиты вложений и язык приходят с сервера (`GET /v1/form`), поэтому меняются без
релиза игры.

## Сборки

| Сборка | Что в ней | От чего зависит |
| --- | --- | --- |
| `Hooligapps.SupportGate.Core` | клиент, модели, транспорт, токен | — |
| `Hooligapps.SupportGate.UI` | презентер и интерфейсы вью | Core |
| `Hooligapps.SupportGate.UI.UGUI` | форма на uGUI | Core, UI |
| `Hooligapps.SupportGate.UI.UIToolkit` | форма на UI Toolkit | Core, UI |

Зависимость только вниз: рендерер знает про презентер, презентер — про клиент,
клиент — ни про что. Если форма в игре своя, берите один Core.

## Установка

В `Packages/manifest.json`:

```json
"com.hooligapps.supportgate": "https://github.com/hooligapps/support-gate-sdk.git?path=/unity"
```

Пакету нужен `com.unity.nuget.newtonsoft-json` — он подтянется как зависимость.

## Самый короткий путь: форма в браузере

Шлюз держит страницу `/form` с той же веб-формой, что у веб-игр. Игра открывает
её в системном браузере — работает на iOS, Android, десктопе и в WebGL, без
плагинов и WebView, а файлы игрок выбирает диалогом браузера:

```csharp
SupportGateBrowser.Open("https://support.flushee.work", sessionToken);
```

Токен уходит во фрагменте адреса (`/form#token=…`): в запрос и в логи сервера
он не попадает, страница стирает его из адресной строки сразу. Пока игрок
пишет, страница сама продлевает сессию через шлюз (до 3 часов); если и это
не удалось — просит открыть страницу из игры заново. Чего нет: результата
отправки в игре (браузер — отдельный процесс) и скриншота игры — снимок делает
сам игрок.

Форма внутри игры (uGUI / UI Toolkit ниже) нужна, когда окно должно быть в
самой игре: консоли, свой UI, событие `Completed` для аналитики.

## Минимум

```csharp
var session = SupportGateSession.FromProvider(RequestToken);
var client = new SupportGateClient("https://support.flushee.work", session);
var view = new SupportGateFormElement();            // или SupportGateFormView для uGUI

_presenter = new SupportGateFormPresenter<VisualElement>(client, view, host, this);
_presenter.Completed += result => Analytics.Log("support", result.ToString());
_presenter.Open();
```

`host` — адаптер к модальной системе игры (`ISupportGateModalHost<TRoot>`).
Готовые примеры — в Samples, стенд со всеми состояниями — `../unity-demo`.

### Итог показа

`Completed` приходит с `SupportGateFormResult`: `Sent` — один раз, с `Ticket`
(`TicketId`, `IssueKey`, если доставка в Jira уже прошла); `Error` — на каждую
неудачную попытку, со `Stage` (`Load` — не загрузился справочник, `Submit` — не
ушло обращение) и `Error` (`Kind`, `HttpStatus`, `Code`, `Message`); `Closed` —
только если окно закрыли, так ничего и не отправив. Ошибки вложений сюда не
попадают: игрок убирает файл и продолжает. Отдельные события `Submitted`,
`Failed`, `HostRefused`, `Closed` тоже есть.

### Язык

Строки окна приходят с сервера вместе со справочником (`strings` в
`/v1/form`) уже на языке игрока — по `locale` из токена и `Accept-Language`;
в пакете только английский, на экран загрузки и на случай, когда форму получить
не удалось. Множественное число и подстановки — ICU MessageFormat
(`SupportGateMessageFormat`), категории plural считаются по CLDR для языков,
которые отдаёт шлюз. `direction: rtl` (арабский) зеркалит форму: UI Toolkit —
классом `sg-rtl` в USS, uGUI — выравниванием подписей; раскладку рядов префаба
игра зеркалит сама.

Если у игры своя локализация — присвоить `presenter.Strings` до `Open()`, тогда
серверные строки игнорируются.

### Токен сессии

Токен выпускает **бэкенд игры** своим секретом: JWT HS256, 15 минут. Секрет игры
и учётки Jira в клиент не попадают никогда.

```csharp
private IEnumerator RequestToken(Action<string> onToken)
{
    // запрос к своему бэкенду
    yield return ...;
    onToken(token);
}
```

Если сервис ответил 401, клиент спросит токен ещё раз и повторит запрос сам:
провайдер с причиной (`SupportGateTokenRenewal`) на
`SupportGateTokenReason.Expired` должен сбросить кэш и выпустить новый. Когда
игра отдаёт тот же токен или сессия создана `FromToken`, клиент продлевает её
через `POST /v1/session/refresh` (до 3 часов от выпуска). Ошибка доходит до
формы только если не помогло и это.

В токене: `iss` — игра, `sub` — игрок, `ctx` — контекст (`locale`, `platform`,
`game_version`, …). Если в `ctx` есть подтверждённый `email`, поле почты в форме
заполнено и заблокировано, и обратно на сервер оно не отправляется. Пароли,
токены и данные карт в `ctx` не принимаются. `tags` (список) становятся метками
тикета в Jira — сегмент, плательщик, канал: это знает бэкенд игры, а не форма.
Игроку метки не видны.

### Вложения

В Unity нет встроенного диалога выбора файла в рантайме, а плагины у каждой игры
свои, поэтому пакет его не делает: плитка «Добавить файлы» поднимает событие,
а игра отвечает файлом.

```csharp
_presenter.AttachRequested += () => StartCoroutine(Capture());
...
_presenter.AttachFile("screenshot.png", "image/png", bytes);
```

Откуда взять байты — решает игра: на мобильных это галерея или файловый пикер
(NativeGallery, NativeFilePicker), на десктопе — StandaloneFileBrowser, в
редакторе — `EditorUtility.OpenFilePanel` (так сделано в `../unity-demo`),
самый простой вариант — скриншот `ScreenCapture.CaptureScreenshotAsTexture()`.
PNG и JPEG форма показывает плиткой с превью, остальные — расширением.

Файл уходит прямо в хранилище по подписанной ссылке — сервис его не проксирует,
в обращение попадает только идентификатор.

### Своя форма

Если вёрстка формы своя, нужен только клиент:

```csharp
yield return client.LoadForm(result => config = result.Value);
yield return client.UploadAttachment(name, type, bytes, result => id = result.Value.AttachmentId);
yield return client.SubmitTicket(draft, key, result => ticket = result.Value);
```

Все методы возвращают `IEnumerator`: клиент не MonoBehaviour, корутину запускает
вызывающая сторона. Результат — `SupportGateResult<T>` с `Error`, у ошибки есть
`Kind`, `HttpStatus`, `Code`, `Fields` (ошибки по полям формы), `IsTransient` и
`IsUnauthorized`.

`key` — ключ идемпотентности: повтор после сетевой ошибки с тем же ключом вернёт
то же обращение, а не создаст второе. Презентер держит ключ до успешной отправки
сам.

## Тесты

`Tests/Editor` гоняются Test Runner'ом. Контрактные тесты клиента идут через
`FakeTransport` и не ходят в сеть; тесты презентера запускаются как `[UnityTest]`
в play mode — им нужен MonoBehaviour для корутин.
