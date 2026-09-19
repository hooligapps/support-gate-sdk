using System;
using System.Collections;
using Hooligapps.SupportGate.UI;
using Hooligapps.SupportGate.UI.UIToolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hooligapps.SupportGate.Samples.UIToolkit
{
    /// <summary>
    /// Склейка для UI Toolkit: клиент, форма, хост и презентер. Токен здесь берётся
    /// из инспектора — в игре его выпускает бэкенд своим секретом.
    /// </summary>
    public sealed class SupportGateUIToolkitSample : MonoBehaviour
    {
        [SerializeField] private string _endpoint = "https://support.flushee.work";
        [SerializeField] private string _sessionToken;
        [SerializeField] private SupportGatePanelHost _host;

        private SupportGateFormPresenter<VisualElement> _presenter;

        private void Start()
        {
            var session = SupportGateSession.FromProvider(RequestToken);
            var client = new SupportGateClient(_endpoint, session);
            var view = new SupportGateFormElement();

            _presenter = new SupportGateFormPresenter<VisualElement>(client, view, _host, this);
            _presenter.Submitted += ticket => Debug.Log("обращение создано: " + ticket.DisplayId);
            // Итог для аналитики игры: sent с номером, error со стадией, closed без отправки.
            _presenter.Completed += result => Debug.Log("support: " + result);
            _presenter.Failed += error => Debug.LogWarning(error.ToString());

            // Выбор файла зависит от платформы и плагинов игры, поэтому его делает игра.
            _presenter.AttachRequested += AttachScreenshot;
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

        private void AttachScreenshot()
        {
            StartCoroutine(CaptureScreenshot());
        }

        private IEnumerator CaptureScreenshot()
        {
            yield return new WaitForEndOfFrame();

            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                _presenter.AttachFile("screenshot.png", "image/png", texture.EncodeToPNG());
            }
            finally
            {
                Destroy(texture);
            }
        }
    }
}
