using System;
using System.Collections;
using Hooligapps.SupportGate.Prefabs;
using Hooligapps.SupportGate.UI;
using UnityEngine;

namespace Hooligapps.SupportGate.Samples.UGUI
{
    public sealed class SupportGateUGuiSample : MonoBehaviour
    {
        [SerializeField] private SupportGateForm _prefab;
        [SerializeField] private Transform _container;
        [SerializeField] private SupportGateCanvasHost _host;
        private SupportGateForm _instance;
        private SupportGateFormPresenter<GameObject> _presenter;

        // The game configures its client, token provider and file picker before opening.
        public bool OpenSupport(SupportGateClient client, Action<SupportGateFormResult> completed,
            Action<Action<string, string, byte[]>> pickFile = null)
        {
            if (_presenter != null) return false;
            _instance = Instantiate(_prefab, _container, false);
            _instance.gameObject.SetActive(false);
            _presenter = _instance.CreatePresenter(client, _host, this, completed, pickFile);
            if (_presenter.Open()) return true;
            Release();
            return false;
        }

        private void LateUpdate()
        {
            // Closed precedes Completed in the presenter. Keep the result callback alive until Close returns.
            if (_presenter != null && !_presenter.IsVisible) Release();
        }

        public void CloseSupport()
        {
            _presenter?.Close();
            Release();
        }

        private void Release()
        {
            var presenter = _presenter;
            _presenter = null;
            presenter?.Dispose();
            if (_instance != null) Destroy(_instance.gameObject);
            _instance = null;
        }

        private void OnDisable() => CloseSupport();
        private void OnDestroy() => CloseSupport();
    }
}
