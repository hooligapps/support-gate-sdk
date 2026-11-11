# Готовые окна поддержки на uGUI

Комплект содержит белый `SupportGateForm.prefab` и чёрный
`SupportGateFormDark.prefab`. Чёрное окно — Prefab Variant белого. Оба используют
`UnityEngine.UI.Text`, `Dropdown` и `InputField`.

## Установка

1. Установите UPM-пакет версии `0.4.0` через **Add package from git URL**:
   `https://github.com/hooligapps/support-gate-sdk.git?path=/unity#v0.4.0`.
   Этот тег содержит сборку `Hooligapps.SupportGate.Prefabs`.
2. Импортируйте [SupportGate-UGUI.unitypackage](../dist/SupportGate-UGUI.unitypackage)
   через **Assets → Import Package → Custom Package**, выбрав все файлы.
3. Ассеты появятся в `Assets/SupportGateUGUI`. Оба префаба, спрайт, Noto Sans
   и его лицензия включены в архив. Компоненты находятся в UPM-пакете.
4. Подготовьте Canvas с GraphicRaycaster, EventSystem с подходящим игре input
   module, контейнер модальных окон и блокирующую ввод подложку.
5. Через Package Manager импортируйте sample **uGUI host**. Назначьте в
   `SupportGateUGuiSample` белый или чёрный префаб, контейнер и
   `SupportGateCanvasHost`. У хоста назначьте контейнер и подложку.

Архив создан и проверен в Unity 6000.6.5f1. Проверены повторный импорт,
ссылки контролов и связь чёрного варианта с базовым префабом. На импортированном
архиве прошли 6 Play Mode тестов демо, включая отправку с вложением в обеих темах. Совместимость готовых ассетов с
2021.3 и 2022.3 пока не проверена. Минимум 2021.3 в манифесте SDK относится
к пакету кода. Проект использует совместимую с его редактором версию uGUI.

При переносе из первоначальной интеграции Order удалите локальные шесть скриптов
`Assets/SupportGateUGUI/*.cs` вместе с их `.meta` при установке обновлённого SDK.
Их типы и GUID сохранены в пакете. Одновременное наличие локальной и пакетной
копий создаёт дубликаты классов и GUID. Префабы и ваши варианты сохраните.
Архив обновляет базовые ассеты, поэтому локальный редизайн следует делать в вариантах.

## Открытие с зависимостями игры

Игра создаёт клиент и передаёт провайдер токена, который обращается к её бэкенду.
Секрет Support Gate хранится на бэкенде. Форма получает категории и локализацию
через клиент SDK.

```csharp
using Hooligapps.SupportGate;

// RequestToken соответствует SupportGateTokenRenewal или SupportGateTokenRequest.
var session = SupportGateSession.FromProvider(RequestToken);
var client = new SupportGateClient(endpoint, session);
supportWindow.OpenSupport(client, result => OnSupportResult(result), PickFile);
```

`supportWindow` — настроенный `SupportGateUGuiSample`. Его `OpenSupport`
создаёт экземпляр, подключает презентер и возвращает успешность открытия.
`CloseSupport()` закрывает окно и освобождает экземпляр. При закрытии через UI
sample освобождает его после возврата из `Close`, чтобы получить итоговый
`Completed`. При отключении или уничтожении sample окно также закрывается.

Для собственной системы окон используйте компонент напрямую:

```csharp
using Hooligapps.SupportGate.Prefabs;
using Hooligapps.SupportGate.UI;

var instance = Instantiate(prefab, container, false);
instance.gameObject.SetActive(false);
var presenter = instance.CreatePresenter(client, host, coroutineRunner, OnSupportResult, PickFile);
if (!presenter.Open())
{
    presenter.Dispose();
    Destroy(instance.gameObject);
}
```

Игра владеет экземпляром и презентером. После закрытия вызовите `Dispose()`
и уничтожьте экземпляр. `Closed` испускается раньше финального `Completed`,
поэтому обработчик результата должен оставаться подключённым до возврата из
`Close()`. Sample показывает полный жизненный цикл. При смене игрока игра
закрывает окно и создаёт новый клиент с новой сессией.

## Вложения

Необязательный обработчик имеет сигнатуру:

```csharp
void PickFile(System.Action<string, string, byte[]> attach)
{
    // Открыть файловый пикер игры. После выбора:
    attach(fileName, contentType, bytes);
    // При отмене ничего не вызывать.
}
```

Обработчик передаёт имя, MIME-тип и содержимое выбранного файла. SDK проверяет
лимиты и загружает файл. PNG/JPEG отображаются с предпросмотром. При закрытии
и уничтожении исходного презентера запоздалый callback не добавит файл в новое окно.
Если `pickFile` равен `null`, блок вложений скрыт. Демо использует
выбор файла в Editor и скриншот в сборке.

## Редизайн

Создайте **Prefab Variant** от белого либо чёрного префаба и сохраните его
в каталоге своей игры. Настройте шрифты, цвета, спрайты, размеры и отступы.
Назначьте вариант в то же поле sample или собственного адаптера.

Для динамических дополнительных полей и вложений меняйте также дочерние
шаблоны, указанные в `SupportGateForm`. Сохраняйте ссылки на контролы и контейнеры.
При обновлении базы сохраняются GUID и существующие идентификаторы объектов,
от которых зависят варианты. Новые версии архива могут менять базовое оформление,
поэтому проверяйте собственные overrides после импорта.

## Демо, проверка и повторный экспорт

Откройте `unity-demo`, сцену `SupportGateDemo`, нажмите Play. Переключатель
**Light / Dark** выбирает настоящий префаб, а панель позволяет проверить ошибки,
языки, отправку и вложения на тестовом транспорте.

Исходники оформления находятся в `unity-demo/Assets/SupportGateUGUI`,
компоненты — в `unity/Runtime/Prefabs`.
Для экспорта используйте **Tools → Support Gate → Export uGUI package**.
Команда проверяет ссылки и внешние зависимости, затем записывает
`dist/SupportGate-UGUI.unitypackage`. Архив включает только ассеты окна.
Шрифт распространяется по лицензии SIL OFL, её текст лежит рядом со шрифтом.
