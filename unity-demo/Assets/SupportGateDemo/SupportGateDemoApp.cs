using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Hooligapps.SupportGate;
using Hooligapps.SupportGate.UI;
using Hooligapps.SupportGate.UI.UIToolkit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UGUI = Hooligapps.SupportGate.UI.UGUI;
using Button = UnityEngine.UI.Button;

// Поля с [SerializeField] заполняет сцена.
#pragma warning disable 0649

namespace Hooligapps.SupportGate.Demo
{
    /// <summary>
    /// Демо-стенд: форма обращения на обоих рендерерах и панель управления на
    /// IMGUI. Данные фиктивные — их отдаёт <see cref="SupportGateDemoTransport"/>.
    /// Чтобы работать с настоящим шлюзом, достаточно снять «Use Demo Server» и
    /// вписать endpoint и токен сессии.
    /// </summary>
    public sealed class SupportGateDemoApp : MonoBehaviour
    {
        private const int LogLines = 14;

        [Header("UI Toolkit")]
        [SerializeField] private UIDocument _document;
        [SerializeField] private StyleSheet _styleSheet;

        [Header("Сервер")]
        [SerializeField] private bool _useDemoServer = true;
        [SerializeField] private string _endpoint = "https://support.flushee.work";

        [Tooltip("JWT сессии, выпущенный бэкендом игры. Нужен только без демо-сервера.")]
        [SerializeField] private string _sessionToken = "";

        private readonly List<string> _log = new List<string>();
        private readonly SupportGateDemoTransport _transport = new SupportGateDemoTransport();

        private SupportGateClient _client;

        private GameObject _canvasRoot;
        private SupportGateDemoCanvasHost _uguiHost;
        private SupportGateFormPresenter<GameObject> _ugui;
        private Button _uguiEntry;

        private VisualElement _toolkitRoot;
        private SupportGateDemoPanelHost _toolkitHost;
        private SupportGateFormPresenter<VisualElement> _toolkit;

        private bool _useUGui = true;
        private bool _hostAllowsShow = true;
        private int _attachmentsMade;
        private Vector2 _logScroll;

        /// <summary>Активный презентер: стенд показывает один рендерер за раз.</summary>
        public bool UsesUGui
        {
            get { return _useUGui; }
            set
            {
                if (_useUGui == value) return;
                _useUGui = value;
                ApplyRenderer();
            }
        }

        public SupportGateDemoTransport Transport
        {
            get { return _transport; }
        }

        public SupportGateFormPresenter<GameObject> UGuiPresenter
        {
            get { return _ugui; }
        }

        public SupportGateFormPresenter<VisualElement> ToolkitPresenter
        {
            get { return _toolkit; }
        }

        public IReadOnlyList<string> Log
        {
            get { return _log; }
        }

        private void Start()
        {
            _transport.Log = Write;

            var session = _useDemoServer
                ? SupportGateSession.FromToken("demo-token")
                : SupportGateSession.FromProvider(RequestToken);

            _client = new SupportGateClient(
                _useDemoServer ? "https://gateway.demo.local" : _endpoint,
                session,
                _useDemoServer ? _transport : null);

            BuildUGui();
            BuildToolkit();
            ApplyRenderer();

            Write(_useDemoServer ? "демо-сервер: сеть не используется" : "сервер: " + _endpoint);
        }

        private void OnDestroy()
        {
            _ugui?.Dispose();
            _toolkit?.Dispose();
        }

        private void LateUpdate()
        {
            _uguiHost?.UpdateLayout();
        }

        private IEnumerator RequestToken(Action<string> onToken)
        {
            // В игре здесь запрос к своему бэкенду: он подписывает токен секретом игры.
            onToken(_sessionToken);
            yield break;
        }

        private void BuildUGui()
        {
            _canvasRoot = new GameObject("SupportGateDemoCanvas", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            _canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            if (EventSystem.current == null)
            {
                // В игре EventSystem уже есть; пакет его не создаёт, а демо — сцена целиком.
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            _uguiEntry = SupportGateDemoUGuiFactory.CreateEntryButton(_canvasRoot.transform);

            var layerGo = new GameObject("PopupLayer", typeof(RectTransform));
            var layer = (RectTransform)layerGo.transform;
            layer.SetParent(_canvasRoot.transform, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = Vector2.zero;
            layer.offsetMax = Vector2.zero;

            var blocker = SupportGateDemoUGuiFactory.CreateBlocker(layer);
            var view = SupportGateDemoUGuiFactory.CreateFormView(layer);

            _uguiHost = new SupportGateDemoCanvasHost(layer, blocker);
            _ugui = new SupportGateFormPresenter<GameObject>(_client, view, _uguiHost, this);
            Subscribe(_ugui, "uGUI");

            _uguiEntry.onClick.AddListener(() =>
            {
                Write("uGUI: клик по кнопке «Support»");
                _ugui.Open();
            });
        }

        private void BuildToolkit()
        {
            if (_document == null)
            {
                Write("UI Toolkit: в сцене нет UIDocument — Tools → Support Gate → Собрать демо-сцену");
                return;
            }

            // Всё демо живёт в одном контейнере: UIDocument выключать нельзя — при
            // выключении он пересоздаёт rootVisualElement и добавленное в рантайме
            // пропадает.
            _toolkitRoot = new VisualElement { name = "sg-demo-root", pickingMode = PickingMode.Ignore };
            _toolkitRoot.style.flexGrow = 1f;
            if (_styleSheet != null)
            {
                _toolkitRoot.styleSheets.Add(_styleSheet);
            }

            _document.rootVisualElement.Add(_toolkitRoot);

            var entry = new UnityEngine.UIElements.Button { text = "Support" };
            entry.style.position = Position.Absolute;
            entry.style.top = 24f;
            entry.style.right = 24f;
            entry.style.width = 220f;
            entry.style.height = 88f;
            entry.style.fontSize = 26f;
            _toolkitRoot.Add(entry);

            var view = new SupportGateFormElement();
            _toolkitHost = new SupportGateDemoPanelHost(_toolkitRoot);
            _toolkit = new SupportGateFormPresenter<VisualElement>(_client, view, _toolkitHost, this);
            Subscribe(_toolkit, "UI Toolkit");

            entry.clicked += () =>
            {
                Write("UI Toolkit: клик по кнопке «Support»");
                _toolkit.Open();
            };
        }

        private void Subscribe<TRoot>(SupportGateFormPresenter<TRoot> presenter, string tag)
        {
            presenter.Submitted += ticket => Write(tag + ": обращение принято " + ticket.DisplayId);
            presenter.Failed += error => Write(tag + ": ошибка — " + error);
            presenter.HostRefused += () => Write(tag + ": хост запретил показ");
            presenter.Closed += () => Write(tag + ": окно закрыто");
            presenter.Completed += result => Write(tag + ": итог → " + result);
            presenter.AttachRequested += () => PickFile(presenter);
        }

        /// <summary>
        /// «Добавить файлы» в форме. В Unity нет встроенного диалога выбора файла в
        /// рантайме, поэтому пакет только поднимает событие, а откуда взять байты —
        /// решает игра: в редакторе это системный диалог, в билде — плагин
        /// (NativeFilePicker / NativeGallery на мобильных, StandaloneFileBrowser на
        /// десктопе) или, как здесь, скриншот экрана.
        /// </summary>
        private void PickFile<TRoot>(SupportGateFormPresenter<TRoot> presenter)
        {
#if UNITY_EDITOR
            if (!Application.isBatchMode)
            {
                var path = UnityEditor.EditorUtility.OpenFilePanel("Приложить файл", string.Empty, string.Empty);
                if (string.IsNullOrEmpty(path))
                {
                    Write("выбор файла отменён");
                    return;
                }

                presenter.AttachFile(System.IO.Path.GetFileName(path), ContentTypeOf(path), System.IO.File.ReadAllBytes(path));
                return;
            }
#endif
            StartCoroutine(CaptureScreenshot(presenter));
        }

        private static string ContentTypeOf(string path)
        {
            switch (System.IO.Path.GetExtension(path).ToLowerInvariant())
            {
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                case ".mp4": return "video/mp4";
                case ".txt":
                case ".log": return "text/plain";
                default: return "application/octet-stream";
            }
        }

        private IEnumerator CaptureScreenshot<TRoot>(SupportGateFormPresenter<TRoot> presenter)
        {
            yield return new WaitForEndOfFrame();

            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                presenter.AttachFile("screenshot-" + (++_attachmentsMade) + ".png", "image/png", texture.EncodeToPNG());
            }
            finally
            {
                Destroy(texture);
            }
        }

        private void ApplyRenderer()
        {
            CloseAll();

            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(_useUGui);
            }

            if (_toolkitRoot != null)
            {
                _toolkitRoot.style.display = _useUGui ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void CloseAll()
        {
            _ugui?.Close();
            _toolkit?.Close();
        }

        private void Attach(string name, string type, byte[] data)
        {
            if (_useUGui) _ugui?.AttachFile(name, type, data);
            else _toolkit?.AttachFile(name, type, data);
        }

        private void Write(string message)
        {
            _log.Add(message);

            if (_log.Count > 300)
            {
                _log.RemoveRange(0, _log.Count - 300);
            }

            _logScroll.y = float.MaxValue;

            Debug.Log("Support Gate demo: " + message);
        }

        private void OnGUI()
        {
            const float width = 400f;

            GUILayout.BeginArea(new Rect(12f, 12f, width, Screen.height - 24f), GUI.skin.box);

            GUILayout.Label("Support Gate demo");

            UsesUGui = GUILayout.Toolbar(_useUGui ? 0 : 1, new[] { "uGUI", "UI Toolkit" }) == 0;

            if (!_useUGui && _toolkitRoot == null)
            {
                GUILayout.Label("UI Toolkit не собран: в сцене нет UIDocument. " +
                                "Tools → Support Gate → Собрать демо-сцену.");
            }

            if (_useDemoServer)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Ответ на /v1/form");
                _transport.Form = (SupportGateDemoFormScenario)GUILayout.SelectionGrid(
                    (int)_transport.Form,
                    new[] { "английский", "русский", "арабский (RTL)", "почта из токена", "HTTP 500", "401", "нет сети" },
                    2);

                GUILayout.Space(8f);
                GUILayout.Label("Ответ на /v1/tickets");
                _transport.Submit = (SupportGateDemoSubmitScenario)GUILayout.SelectionGrid(
                    (int)_transport.Submit,
                    new[] { "принято", "в очереди (без ключа)", "ошибки полей", "429", "503", "нет сети" },
                    2);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Задержка " + _transport.DelaySeconds.ToString("0.0") + " c", GUILayout.Width(110f));
                _transport.DelaySeconds = GUILayout.HorizontalSlider(_transport.DelaySeconds, 0f, 3f);
                GUILayout.EndHorizontal();

                _transport.StorageRejects = GUILayout.Toggle(_transport.StorageRejects, "Хранилище отвергает файлы");
            }

            var allows = GUILayout.Toggle(_hostAllowsShow, "Хост разрешает показ");
            if (allows != _hostAllowsShow)
            {
                _hostAllowsShow = allows;
                if (_uguiHost != null) _uguiHost.CanShow = allows;
                if (_toolkitHost != null) _toolkitHost.CanShow = allows;
            }

            GUILayout.Space(8f);
            GUILayout.Label("Окно");

            if (GUILayout.Button("Открыть форму"))
            {
                if (_useUGui) _ugui?.Open();
                else _toolkit?.Open();
            }

            if (GUILayout.Button("Открыть в браузере"))
            {
                // Веб-форма шлюза в системном браузере — без вёрстки в Unity.
                // Демо-сервер такой страницы не держит: нужен настоящий шлюз и токен.
                if (_useDemoServer || string.IsNullOrEmpty(_sessionToken))
                {
                    Write("браузер: снимите Use Demo Server и впишите Session Token");
                }
                else
                {
                    Write("браузер: " + _endpoint + SupportGateBrowser.PagePath);
                    SupportGateBrowser.Open(_endpoint, _sessionToken);
                }
            }

            if (GUILayout.Button("Приложить PNG (скриншот)"))
            {
                StartCoroutine(_useUGui ? CaptureScreenshot(_ugui) : CaptureScreenshot(_toolkit));
            }

            if (GUILayout.Button("Приложить текстовый файл"))
            {
                Attach("log-" + (++_attachmentsMade) + ".txt", "text/plain", Encoding.UTF8.GetBytes("demo log\n"));
            }

            if (GUILayout.Button("Приложить неподдерживаемый тип"))
            {
                Attach("archive.zip", "application/zip", new byte[] { 0x50, 0x4b, 0x03, 0x04 });
            }

            if (GUILayout.Button("Приложить слишком большой файл"))
            {
                // Лимит демо-сервера — 5 МБ; клиент откажет сам, не ходя в сеть.
                Attach("huge.png", "image/png", new byte[6 * 1024 * 1024]);
            }

            if (GUILayout.Button("Закрыть"))
            {
                CloseAll();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Лог");

            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));

            for (var i = Mathf.Max(0, _log.Count - LogLines * 4); i < _log.Count; i++)
            {
                GUILayout.Label(_log[i]);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
