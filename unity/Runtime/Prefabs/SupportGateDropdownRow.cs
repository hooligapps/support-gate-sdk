using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hooligapps.SupportGate.Prefabs
{
    [DisallowMultipleComponent]
    public sealed class SupportGateDropdownRow : MonoBehaviour
    {
        [SerializeField] private Text _label;
        [SerializeField] private Dropdown _dropdown;
        [SerializeField] private Text _error;

        private SupportGateExtraField _field;

        public string FieldId { get; private set; }

        public void Bind(SupportGateExtraField field, string placeholder)
        {
            _field = field;
            FieldId = field.Id;

            if (_label != null)
            {
                _label.supportRichText = false;
                _label.text = field.Label;
            }

            if (_dropdown != null)
            {
                var options = new System.Collections.Generic.List<string> { placeholder };
                for (var i = 0; i < field.Options.Count; i++)
                {
                    options.Add(field.Options[i].Label);
                }

                _dropdown.ClearOptions();
                _dropdown.AddOptions(options);
                _dropdown.value = 0;
            }

            SetError(null);
        }

        public string Value
        {
            get
            {
                if (_dropdown == null || _field == null || _dropdown.value <= 0)
                {
                    return null;
                }

                return _field.Options[_dropdown.value - 1].Id;
            }
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
