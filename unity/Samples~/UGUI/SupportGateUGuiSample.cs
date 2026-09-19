using System;
using System.Collections;
using System.IO;
using Hooligapps.SupportGate.UI;
using Hooligapps.SupportGate.UI.UGUI;
using UnityEngine;

namespace Hooligapps.SupportGate.Samples.UGUI
{
    /// <summary>
    /// Склейка для uGUI: клиент, форма из префаба, хост и презентер. Токен здесь
    /// берётся из инспектора — в игре его выпускает бэкенд своим секретом.
    /// </summary>
    public sealed class SupportGateUGuiSample : MonoBehaviour
    {
        [SerializeField] private string _endpoint = "https://support.flushee.work";
        [SerializeField] private string _sessionToken;
        [SerializeField] private SupportGateFormView _view;
        [SerializeField] private SupportGateCanvasHost _host;

        private SupportGateFormPresenter<GameObject> _presenter;

        private void Start()
        {
            var session = SupportGateSession.FromProvider(RequestToken);
            var client = new SupportGateClient(_endpoint, session);

            _presenter = new SupportGateFormPresenter<GameObject>(client, _view, _host, this);
            _presenter.Submitted += ticket => Debug.Log("обращение создано: " + ticket.DisplayId);
            // Итог для аналитики игры: sent с номером, error со стадией, closed без отправки.
            _presenter.Completed += result => Debug.Log("support: " + result);
            _presenter.Failed += error => Debug.LogWarning(error.ToString());

            // Выбор файла зависит от платформы и плагинов игры, поэтому его делает игра.
            _presenter.AttachRequested += AttachLastScreenshot;
        }

        private void OnDestroy()
        {
            _presenter?.Dispose();
        }

        /// <summary>Вызывается кнопкой «Поддержка» в интерфейсе игры.</summary>
        public void OpenSupport()
        {
            _presenter.Open();
        }

        private IEnumerator RequestToken(Action<string> onToken)
        {
            // В игре здесь запрос к своему бэкенду: он подписывает токен секретом игры.
            onToken(_sessionToken);
            yield break;
        }

        private void AttachLastScreenshot()
        {
            var path = Path.Combine(Application.persistentDataPath, "support-screenshot.png");
            if (!File.Exists(path))
            {
                Debug.LogWarning("нечего прикладывать: " + path);
                return;
            }

            _presenter.AttachFile(Path.GetFileName(path), "image/png", File.ReadAllBytes(path));
        }
    }
}
