using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Поля с [SerializeField] заполняет инспектор.
#pragma warning disable 0649

namespace Hooligapps.SupportGate.UI.UGUI
{
    /// <summary>
    /// Форма обращения на uGUI. Собственных Canvas и EventSystem не создаёт: префаб
    /// вставляется в иерархию игры.
    ///
    /// Категории и доп. поля приходят с сервера, поэтому строки доп. полей и
    /// вложений создаются из префабов-шаблонов, а не лежат в иерархии заранее.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupportGateFormView : MonoBehaviour, ISupportGateFormView<GameObject>
    {
        [Header("Поля")]
        [SerializeField] private Dropdown _category;
        [SerializeField] private Dropdown _subcategory;
        [SerializeField] private InputField _email;
        [SerializeField] private InputField _subject;
        [SerializeField] private InputField _description;

        [Header("Подписи")]
        [SerializeField] private Text _title;
        [SerializeField] private Text _banner;
        [Tooltip("Подложка баннера; если не задана, включается и выключается сам текст.")]
        [SerializeField] private GameObject _bannerRoot;
        [SerializeField] private Text _state;
        [SerializeField] private Text _categoryLabel;
        [SerializeField] private Text _subcategoryLabel;
        [SerializeField] private Text _emailLabel;
        [SerializeField] private Text _subjectLabel;
        [SerializeField] private Text _descriptionLabel;
        [SerializeField] private Text _attachmentsLabel;
        [SerializeField] private Text _attachmentsHint;

        [Header("Ошибки полей")]
        [SerializeField] private Text _categoryError;
        [SerializeField] private Text _subcategoryError;
        [SerializeField] private Text _emailError;
        [SerializeField] private Text _subjectError;
        [SerializeField] private Text _descriptionError;

        [Header("Кнопки")]
        [SerializeField] private Button _submit;
        [SerializeField] private Button _cancel;
        [SerializeField] private Button _attach;
        [SerializeField] private Button _retry;
        [SerializeField] private Text _submitText;
        [SerializeField] private Text _cancelText;
        [SerializeField] private Text _attachText;
        [SerializeField] private Text _retryText;

        [Header("Контейнеры и шаблоны")]
        [SerializeField] private GameObject _formRoot;
        [SerializeField] private GameObject _stateRoot;
        [SerializeField] private Transform _extraRoot;
        [SerializeField] private Transform _attachmentsRoot;
        [SerializeField] private SupportGateTextRow _textRowTemplate;
        [SerializeField] private SupportGateDropdownRow _dropdownRowTemplate;
        [SerializeField] private SupportGateAttachmentRow _attachmentRowTemplate;

        private readonly List<SupportGateOption> _categoryOptions = new List<SupportGateOption>();
        private readonly List<SupportGateOption> _subcategoryOptions = new List<SupportGateOption>();
        private readonly List<SupportGateTextRow> _textRows = new List<SupportGateTextRow>();
        private readonly List<SupportGateDropdownRow> _dropdownRows = new List<SupportGateDropdownRow>();
        private readonly List<SupportGateAttachmentRow> _attachmentRows = new List<SupportGateAttachmentRow>();
        private readonly Dictionary<string, Text> _errors = new Dictionary<string, Text>();

        private SupportGateStrings _text = SupportGateStrings.English();
        private bool _emailLocked;

        public GameObject Root
        {
            get { return gameObject; }
        }

        public event Action CloseRequested;
        public event Action RetryRequested;
        public event Action SubmitRequested;
        public event Action<string> CategoryChanged;
        public event Action AttachRequested;
        public event Action<int> AttachmentRemoved;

        private void Awake()
        {
            // Тексты с сервера — данные, не разметка.
            foreach (var label in new[]
                     {
                         _title, _banner, _state, _categoryLabel, _subcategoryLabel, _emailLabel,
                         _subjectLabel, _descriptionLabel, _attachmentsLabel, _attachmentsHint
                     })
            {
                if (label != null)
                {
                    label.supportRichText = false;
                }
            }

            Register("category", _categoryError);
            Register("subcategory", _subcategoryError);
            Register("email", _emailError);
            Register("subject", _subjectError);
            Register("description", _descriptionError);

            if (_submit != null) _submit.onClick.AddListener(() => SubmitRequested?.Invoke());
            if (_cancel != null) _cancel.onClick.AddListener(() => CloseRequested?.Invoke());
            if (_attach != null) _attach.onClick.AddListener(() => AttachRequested?.Invoke());
            if (_retry != null) _retry.onClick.AddListener(() => RetryRequested?.Invoke());

            if (_category != null)
            {
                _category.onValueChanged.AddListener(OnCategorySelected);
            }

            HideTemplates();
        }

        public void SetState(SupportGateViewState state)
        {
            gameObject.SetActive(state != SupportGateViewState.Hidden);

            var showsForm = state == SupportGateViewState.Form || state == SupportGateViewState.Sending;
            if (_formRoot != null) _formRoot.SetActive(showsForm);
            if (_stateRoot != null) _stateRoot.SetActive(!showsForm && state != SupportGateViewState.Hidden);

            if (_submit != null)
            {
                _submit.gameObject.SetActive(showsForm);
                _submit.interactable = state == SupportGateViewState.Form;
            }

            if (_attach != null)
            {
                _attach.interactable = state == SupportGateViewState.Form;
            }

            if (_retry != null)
            {
                _retry.gameObject.SetActive(state == SupportGateViewState.LoadFailed);
            }

            if (_retryText != null)
            {
                _retryText.text = _text.Retry;
            }

            if (_submitText != null)
            {
                _submitText.text = state == SupportGateViewState.Sending ? _text.Submitting : _text.Submit;
            }

            if (_cancelText != null)
            {
                _cancelText.text = state == SupportGateViewState.Sent || state == SupportGateViewState.LoadFailed
                    ? _text.Close
                    : _text.Cancel;
            }

            if (_state == null)
            {
                return;
            }

            if (state == SupportGateViewState.Loading)
            {
                _state.text = _text.Loading;
            }
            else if (state == SupportGateViewState.LoadFailed)
            {
                _state.text = _text.LoadFailed;
            }
        }

        public void BindConfig(SupportGateFormConfig config, SupportGateStrings text)
        {
            _text = text ?? SupportGateStrings.English();
            _emailLocked = config.EmailLocked;

            SetText(_title, _text.Title);
            SetText(_categoryLabel, _text.Category);
            SetText(_subcategoryLabel, _text.Subcategory);
            SetText(_emailLabel, _text.Email);
            SetText(_subjectLabel, _text.Subject);
            SetText(_descriptionLabel, _text.Description);
            SetText(_attachmentsLabel, _text.Attachments);
            SetText(_attachText, _text.AddFile);
            SetText(_attachmentsHint, config.Attachments != null
                ? _text.AttachmentsHint(config.Attachments.MaxFiles, config.Attachments.MaxFileSize)
                : string.Empty);

            // Направление письма приходит с сервера: подписи прижимаются к правому
            // краю, раскладку рядов префаба игра зеркалит сама.
            foreach (var label in new[]
                     {
                         _title, _banner, _categoryLabel, _subcategoryLabel, _emailLabel, _subjectLabel,
                         _descriptionLabel, _attachmentsLabel, _attachmentsHint
                     })
            {
                Align(label, config.IsRightToLeft);
            }

            _categoryOptions.Clear();
            for (var i = 0; i < config.Categories.Count; i++)
            {
                _categoryOptions.Add(new SupportGateOption(config.Categories[i].Id, config.Categories[i].Label));
            }

            Fill(_category, _categoryOptions);

            if (_email != null)
            {
                _email.text = config.Email ?? string.Empty;
                _email.interactable = !config.EmailLocked;
            }

            if (_subject != null) _subject.text = string.Empty;
            if (_description != null) _description.text = string.Empty;

            ShowFieldErrors(new Dictionary<string, string>());
        }

        public void BindSubcategories(IReadOnlyList<SupportGateOption> options)
        {
            _subcategoryOptions.Clear();
            for (var i = 0; i < options.Count; i++)
            {
                _subcategoryOptions.Add(options[i]);
            }

            Fill(_subcategory, _subcategoryOptions);
        }

        public void BindExtraFields(IReadOnlyList<SupportGateExtraField> fields)
        {
            foreach (var row in _textRows) Destroy(row.gameObject);
            foreach (var row in _dropdownRows) Destroy(row.gameObject);
            _textRows.Clear();
            _dropdownRows.Clear();

            if (_extraRoot == null)
            {
                return;
            }

            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];

                if (field.Type == SupportGateFieldType.Select && _dropdownRowTemplate != null)
                {
                    var row = Instantiate(_dropdownRowTemplate, _extraRoot);
                    row.gameObject.SetActive(true);
                    row.Bind(field, _text.Choose);
                    _dropdownRows.Add(row);
                    continue;
                }

                if (_textRowTemplate == null)
                {
                    continue;
                }

                var text = Instantiate(_textRowTemplate, _extraRoot);
                text.gameObject.SetActive(true);
                text.Bind(field);
                _textRows.Add(text);
            }
        }

        public void BindAttachments(IReadOnlyList<SupportGateAttachmentItem> attachments)
        {
            foreach (var row in _attachmentRows) Destroy(row.gameObject);
            _attachmentRows.Clear();

            if (_attachmentsRoot == null || _attachmentRowTemplate == null)
            {
                return;
            }

            for (var i = 0; i < attachments.Count; i++)
            {
                var row = Instantiate(_attachmentRowTemplate, _attachmentsRoot);
                row.gameObject.SetActive(true);
                row.Bind(i, attachments[i], _text.Uploading);
                row.RemoveRequested += index => AttachmentRemoved?.Invoke(index);
                _attachmentRows.Add(row);
            }

            // Если кнопка «Добавить файлы» лежит в той же сетке, она остаётся
            // последней плиткой, как в вебе.
            if (_attach != null && _attach.transform.parent == _attachmentsRoot)
            {
                _attach.transform.SetAsLastSibling();
            }
        }

        public void ShowMessage(string message)
        {
            if (_banner == null)
            {
                return;
            }

            _banner.text = message ?? string.Empty;
            (_bannerRoot != null ? _bannerRoot : _banner.gameObject).SetActive(!string.IsNullOrEmpty(message));
        }

        public void ShowFieldErrors(IReadOnlyDictionary<string, string> errors)
        {
            foreach (var error in _errors)
            {
                error.Value.text = string.Empty;
                error.Value.gameObject.SetActive(false);
            }

            foreach (var row in _textRows) row.SetError(null);
            foreach (var row in _dropdownRows) row.SetError(null);

            foreach (var error in errors)
            {
                if (_errors.TryGetValue(error.Key, out var label))
                {
                    label.text = error.Value;
                    label.gameObject.SetActive(true);
                    continue;
                }

                if (!error.Key.StartsWith("extra."))
                {
                    continue;
                }

                var id = error.Key.Substring("extra.".Length);
                foreach (var row in _textRows)
                {
                    if (row.FieldId == id) row.SetError(error.Value);
                }

                foreach (var row in _dropdownRows)
                {
                    if (row.FieldId == id) row.SetError(error.Value);
                }
            }
        }

        public void ShowSent(string message)
        {
            SetText(_state, message);
        }

        public SupportGateFormValues ReadValues()
        {
            var values = new SupportGateFormValues
            {
                Category = Selected(_category, _categoryOptions),
                Subcategory = Selected(_subcategory, _subcategoryOptions),
                Email = _emailLocked || _email == null ? null : _email.text,
                Subject = _subject != null ? _subject.text : null,
                Description = _description != null ? _description.text : null
            };

            foreach (var row in _textRows)
            {
                values.Extra[row.FieldId] = row.Value;
            }

            foreach (var row in _dropdownRows)
            {
                values.Extra[row.FieldId] = row.Value;
            }

            return values;
        }

        private void OnCategorySelected(int index)
        {
            CategoryChanged?.Invoke(Selected(_category, _categoryOptions));
        }

        private void Register(string field, Text label)
        {
            if (label != null)
            {
                _errors[field] = label;
                label.gameObject.SetActive(false);
            }
        }

        private void HideTemplates()
        {
            if (_textRowTemplate != null) _textRowTemplate.gameObject.SetActive(false);
            if (_dropdownRowTemplate != null) _dropdownRowTemplate.gameObject.SetActive(false);
            if (_attachmentRowTemplate != null) _attachmentRowTemplate.gameObject.SetActive(false);
        }

        private void Fill(Dropdown dropdown, IReadOnlyList<SupportGateOption> options)
        {
            if (dropdown == null)
            {
                return;
            }

            var labels = new List<string> { _text.Choose };
            for (var i = 0; i < options.Count; i++)
            {
                labels.Add(options[i].Label);
            }

            dropdown.ClearOptions();
            dropdown.AddOptions(labels);
            dropdown.SetValueWithoutNotify(0);
        }

        /// <summary>Первый пункт — «Выберите…», ему соответствует пустое значение.</summary>
        private static string Selected(Dropdown dropdown, IReadOnlyList<SupportGateOption> options)
        {
            if (dropdown == null || dropdown.value <= 0 || dropdown.value > options.Count)
            {
                return null;
            }

            return options[dropdown.value - 1].Id;
        }

        private static void Align(Text label, bool rightToLeft)
        {
            if (label == null)
            {
                return;
            }

            var line = (int)label.alignment / 3 * 3;
            label.alignment = (TextAnchor)(line + (rightToLeft ? 2 : 0));
        }

        private static void SetText(Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }
    }
}
