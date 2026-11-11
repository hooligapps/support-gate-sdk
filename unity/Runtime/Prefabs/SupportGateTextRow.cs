using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hooligapps.SupportGate.Prefabs
{
    [DisallowMultipleComponent]
    public sealed class SupportGateTextRow : MonoBehaviour
    {
        [SerializeField] private Text _label;
        [SerializeField] private InputField _input;
        [SerializeField] private Text _error;

        public string FieldId { get; private set; }

        public void Bind(SupportGateExtraField field)
        {
            FieldId = field.Id;

            if (_label != null)
            {
                _label.supportRichText = false;
                _label.text = field.Label;
            }

            if (_input != null)
            {
                _input.characterLimit = field.MaxLength;
                _input.text = string.Empty;
            }

            SetError(null);
        }

        public string Value
        {
            get { return _input != null ? _input.text : null; }
        }

        public void SetError(string message)
        {
            if (_error == null)
            {
                return;
            }

            _error.text = message ?? string.Empty;
            _error.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }
}
