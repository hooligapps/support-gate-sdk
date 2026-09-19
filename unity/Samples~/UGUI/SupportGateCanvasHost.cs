using Hooligapps.SupportGate.UI;
using UnityEngine;

namespace Hooligapps.SupportGate.Samples.UGUI
{
    /// <summary>
    /// Простейший хост: включает готовый объект формы под указанным контейнером.
    /// В игре на его месте обычно свой менеджер окон — он и решает, можно ли
    /// показать окно сейчас.
    /// </summary>
    public sealed class SupportGateCanvasHost : MonoBehaviour, ISupportGateModalHost<GameObject>
    {
        [SerializeField] private Transform _container;
        [SerializeField] private GameObject _blocker;

        private GameObject _shown;

        public bool TryShow(GameObject content)
        {
            if (content == null || _shown != null)
            {
                return false;
            }

            if (_container != null)
            {
                content.transform.SetParent(_container, false);
                content.transform.SetAsLastSibling();
            }

            if (_blocker != null)
            {
                _blocker.SetActive(true);
            }

            content.SetActive(true);
            _shown = content;
            return true;
        }

        public void Close(GameObject content)
        {
            if (content != null)
            {
                content.SetActive(false);
            }

            if (_blocker != null)
            {
                _blocker.SetActive(false);
            }

            _shown = null;
        }
    }
}
