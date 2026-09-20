using System;
using UnityEngine;
using UnityEngine.UI;

// Поля с [SerializeField] заполняет инспектор.
#pragma warning disable 0649

namespace Hooligapps.SupportGate.UI.UGUI
{
    /// <summary>
    /// Строка доп. поля с текстовым вводом. Набор доп. полей зависит от категории и
    /// приходит с сервера, поэтому строки создаются из префаба-шаблона.
    /// </summary>
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
                // Дата вводится текстом: сервер ждёт строку и проверяет формат сам.
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

    /// <summary>Строка доп. поля со списком значений.</summary>
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

        /// <summary>Пустая строка, пока выбран пункт-заглушка.</summary>
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

    /// <summary>
    /// Плитка вложения: превью картинки или расширение, имя, размер или
    /// «Загружается…» и крестик. Превью и расширение необязательны — строка без
    /// них тоже работает.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupportGateAttachmentRow : MonoBehaviour
    {
        [SerializeField] private Text _name;
        [SerializeField] private Text _status;
        [SerializeField] private Button _remove;
        [SerializeField] private RawImage _preview;
        [SerializeField] private Text _extension;
        [SerializeField] private CanvasGroup _pending;

        private int _index;
        private bool _fitPending;

        public event Action<int> RemoveRequested;

        private void LateUpdate()
        {
            if (_fitPending && _preview != null && _preview.rectTransform.rect.width > 0f)
            {
                _fitPending = false;
                FitPreview(_preview.texture);
            }
        }

        public void Bind(int index, SupportGateAttachmentItem item, string uploadingText)
        {
            _index = index;

            if (_name != null)
            {
                _name.supportRichText = false;
                _name.text = item.FileName;
            }

            if (_status != null)
            {
                _status.text = item.State == SupportGateUploadState.Uploading
                    ? uploadingText
                    : Size(item.Size);
            }

            if (_remove != null)
            {
                _remove.onClick.RemoveAllListeners();
                _remove.onClick.AddListener(() => RemoveRequested?.Invoke(_index));
            }

            if (_preview != null)
            {
                _preview.texture = item.Preview;
                _preview.gameObject.SetActive(item.Preview != null);
                // Размер плитки известен только после раскладки — кадрируем в LateUpdate.
                _fitPending = item.Preview != null;
            }

            if (_extension != null)
            {
                _extension.text = item.Extension;
                _extension.gameObject.SetActive(item.Preview == null);
            }

            if (_pending != null)
            {
                _pending.alpha = item.State == SupportGateUploadState.Uploading ? 0.5f : 1f;
            }
        }

        /// <summary>Кадрирует превью по центру, как object-fit: cover в вебе.</summary>
        private void FitPreview(Texture texture)
        {
            if (texture == null)
            {
                return;
            }

            var rect = _preview.rectTransform.rect;
            var textureAspect = (float)texture.width / texture.height;
            var frameAspect = rect.width / rect.height;
            if (textureAspect > frameAspect)
            {
                var w = frameAspect / textureAspect;
                _preview.uvRect = new Rect((1f - w) / 2f, 0f, w, 1f);
            }
            else
            {
                var h = textureAspect / frameAspect;
                _preview.uvRect = new Rect(0f, (1f - h) / 2f, 1f, h);
            }
        }

        private static string Size(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes + " B";
            }

            if (bytes < 1024 * 1024)
            {
                return bytes / 1024 + " KB";
            }

            return bytes / 1024 / 1024 + " MB";
        }
    }
}
