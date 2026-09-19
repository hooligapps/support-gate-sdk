using Hooligapps.SupportGate.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hooligapps.SupportGate.Samples.UIToolkit
{
    /// <summary>
    /// Простейший хост: кладёт форму в UIDocument поверх остального интерфейса.
    /// В игре на его месте обычно свой менеджер окон — он и решает, можно ли
    /// показать окно сейчас.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SupportGatePanelHost : MonoBehaviour, ISupportGateModalHost<VisualElement>
    {
        [SerializeField] private StyleSheet _styleSheet;

        private VisualElement _overlay;

        public bool TryShow(VisualElement content)
        {
            var document = GetComponent<UIDocument>();
            if (document == null || document.rootVisualElement == null || _overlay != null)
            {
                return false;
            }

            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0f;
            _overlay.style.top = 0f;
            _overlay.style.right = 0f;
            _overlay.style.bottom = 0f;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);

            if (_styleSheet != null)
            {
                _overlay.styleSheets.Add(_styleSheet);
            }

            _overlay.Add(content);
            document.rootVisualElement.Add(_overlay);
            return true;
        }

        public void Close(VisualElement content)
        {
            if (_overlay == null)
            {
                return;
            }

            _overlay.Remove(content);
            _overlay.RemoveFromHierarchy();
            _overlay = null;
        }
    }
}
