using Hooligapps.SupportGate.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hooligapps.SupportGate.Demo
{
    /// <summary>
    /// Демо-хост для uGUI. В игре его роль играет менеджер попапов: пакет сам
    /// ничего не открывает и в очередь не ставит.
    /// </summary>
    public sealed class SupportGateDemoCanvasHost : ISupportGateModalHost<GameObject>
    {
        private readonly RectTransform _container;
        private readonly GameObject _blocker;

        private GameObject _shown;

        public SupportGateDemoCanvasHost(RectTransform container, GameObject blocker)
        {
            _container = container;
            _blocker = blocker;
        }

        /// <summary>Переключатель демо: игра может запретить показ.</summary>
        public bool CanShow { get; set; } = true;

        public bool TryShow(GameObject content)
        {
            if (content == null || _container == null || !CanShow || _shown != null)
            {
                return false;
            }

            content.transform.SetParent(_container, false);
            content.transform.SetAsLastSibling();
            _blocker.SetActive(true);
            content.SetActive(true);

            _shown = content;
            UpdateLayout();

            return true;
        }

        public void UpdateLayout()
        {
            if (_shown == null || _container == null)
            {
                return;
            }

            var rect = (RectTransform)_shown.transform;
            rect.sizeDelta = new Vector2(
                Mathf.Min(640f, Mathf.Max(0f, _container.rect.width - 32f)),
                Mathf.Min(860f, Mathf.Max(0f, _container.rect.height - 32f)));
        }

        public void Close(GameObject content)
        {
            if (content != null)
            {
                content.SetActive(false);
            }

            _blocker.SetActive(false);

            if (_shown == content)
            {
                _shown = null;
            }
        }
    }

    /// <summary>Демо-хост для UI Toolkit: свой UIDocument и PanelSettings не создаёт.</summary>
    public sealed class SupportGateDemoPanelHost : ISupportGateModalHost<VisualElement>
    {
        private readonly VisualElement _container;

        private VisualElement _overlay;

        public SupportGateDemoPanelHost(VisualElement container)
        {
            _container = container;
        }

        public bool CanShow { get; set; } = true;

        public bool TryShow(VisualElement content)
        {
            if (content == null || _container == null || !CanShow || _overlay != null)
            {
                return false;
            }

            _overlay = new VisualElement { name = "sg-demo-overlay" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0f;
            _overlay.style.top = 0f;
            _overlay.style.right = 0f;
            _overlay.style.bottom = 0f;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);

            content.style.maxHeight = new Length(92, LengthUnit.Percent);
            content.style.display = DisplayStyle.Flex;

            _overlay.Add(content);
            _container.Add(_overlay);

            return true;
        }

        public void Close(VisualElement content)
        {
            if (_overlay == null)
            {
                return;
            }

            if (content != null && content.parent == _overlay)
            {
                _overlay.Remove(content);
            }

            _overlay.RemoveFromHierarchy();
            _overlay = null;
        }
    }
}
