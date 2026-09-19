using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Hooligapps.SupportGate.UI.UIToolkit
{
    /// <summary>
    /// Форма на UI Toolkit. Строится кодом, без uxml-шаблона: разметка зависит от
    /// справочника, который приходит с сервера, и заранее её всё равно не разложить.
    /// Оформление — в Uss/SupportGate.uss, игра может подменить его своим.
    /// </summary>
    public sealed class SupportGateFormElement : VisualElement, ISupportGateFormView<VisualElement>
    {
        private readonly Label _banner = new Label();
        private readonly Label _stateLabel = new Label();
        private readonly VisualElement _form = new VisualElement();
        private readonly VisualElement _extra = new VisualElement();
        private readonly VisualElement _attachments = new VisualElement();
        private readonly Label _attachmentsHint = new Label();
        private readonly Button _attach = new Button();
        private readonly Button _submit = new Button();
        private readonly Button _cancel = new Button();
        private readonly Label _title = new Label();

        private readonly DropdownField _category = new DropdownField();
        private readonly DropdownField _subcategory = new DropdownField();
        private readonly TextField _email = new TextField();
        private readonly TextField _subject = new TextField();
        private readonly TextField _description = new TextField();

        private readonly Dictionary<string, Label> _errors = new Dictionary<string, Label>();
        private readonly Dictionary<string, VisualElement> _extraControls =
            new Dictionary<string, VisualElement>();

        private readonly List<SupportGateOption> _categoryOptions = new List<SupportGateOption>();
        private readonly List<SupportGateOption> _subcategoryOptions = new List<SupportGateOption>();
        private readonly Dictionary<string, List<SupportGateOption>> _extraOptions =
            new Dictionary<string, List<SupportGateOption>>();

        private SupportGateStrings _text = SupportGateStrings.English();
        private SupportGateViewState _state = SupportGateViewState.Hidden;
        private bool _emailLocked;

        public SupportGateFormElement()
        {
            AddToClassList("sg-root");

            _title.AddToClassList("sg-title");
            _banner.AddToClassList("sg-banner");
            _banner.style.display = DisplayStyle.None;
            _stateLabel.AddToClassList("sg-state");
            _form.AddToClassList("sg-form");

            Add(_title);
            Add(_banner);
            Add(_stateLabel);
            Add(_form);

            BuildField(_category, "category");
            BuildField(_subcategory, "subcategory");
            _form.Add(_extra);
            BuildField(_email, "email");
            BuildField(_subject, "subject");
            BuildField(_description, "description");

            _description.multiline = true;
            _description.AddToClassList("sg-multiline");

            _attachments.AddToClassList("sg-attachments");
            _attachmentsHint.AddToClassList("sg-hint");
            _attach.AddToClassList("sg-button");
            _attach.clicked += () => AttachRequested?.Invoke();
            _form.Add(_attachments);
            _form.Add(_attach);
            _form.Add(_attachmentsHint);

            var footer = new VisualElement();
            footer.AddToClassList("sg-footer");
            _cancel.AddToClassList("sg-button");
            _cancel.clicked += () => CloseRequested?.Invoke();
            _submit.AddToClassList("sg-button");
            _submit.AddToClassList("sg-primary");
            _submit.clicked += OnSubmitClicked;
            footer.Add(_cancel);
            footer.Add(_submit);
            Add(footer);

            _category.RegisterValueChangedCallback(evt =>
                CategoryChanged?.Invoke(IdOf(_categoryOptions, evt.newValue)));

            SetState(SupportGateViewState.Hidden);
        }

        public VisualElement Root
        {
            get { return this; }
        }

        public event Action CloseRequested;
        public event Action RetryRequested;
        public event Action SubmitRequested;
        public event Action<string> CategoryChanged;
        public event Action AttachRequested;
        public event Action<int> AttachmentRemoved;

        public void SetState(SupportGateViewState state)
        {
            _state = state;
            style.display = state == SupportGateViewState.Hidden ? DisplayStyle.None : DisplayStyle.Flex;

            var showsForm = state == SupportGateViewState.Form || state == SupportGateViewState.Sending;
            _form.style.display = showsForm ? DisplayStyle.Flex : DisplayStyle.None;
            _stateLabel.style.display = showsForm ? DisplayStyle.None : DisplayStyle.Flex;

            _submit.SetEnabled(state == SupportGateViewState.Form);
            _attach.SetEnabled(state == SupportGateViewState.Form);

            switch (state)
            {
                case SupportGateViewState.Loading:
                    _stateLabel.text = _text.Loading;
                    _submit.style.display = DisplayStyle.None;
                    _cancel.text = _text.Cancel;
                    break;
                case SupportGateViewState.LoadFailed:
                    _stateLabel.text = _text.LoadFailed;
                    // На этом экране кнопка отправки превращается в «Повторить».
                    _submit.style.display = DisplayStyle.Flex;
                    _submit.SetEnabled(true);
                    _submit.text = _text.Retry;
                    _cancel.text = _text.Close;
                    break;
                case SupportGateViewState.Sending:
                    _submit.style.display = DisplayStyle.Flex;
                    _submit.text = _text.Submitting;
                    break;
                case SupportGateViewState.Sent:
                    _submit.style.display = DisplayStyle.None;
                    _cancel.text = _text.Close;
                    break;
                case SupportGateViewState.Form:
                    _submit.style.display = DisplayStyle.Flex;
                    _submit.text = _text.Submit;
                    _cancel.text = _text.Cancel;
                    break;
            }
        }

        public void BindConfig(SupportGateFormConfig config, SupportGateStrings text)
        {
            _text = text ?? SupportGateStrings.English();
            _emailLocked = config.EmailLocked;

            _title.text = _text.Title;
            _attach.text = _text.AddFile;
            _attachmentsHint.text = config.Attachments != null
                ? _text.AttachmentsHint(config.Attachments.MaxFiles, config.Attachments.MaxFileSize)
                : string.Empty;

            // Направление письма приходит с сервера вместе с языком; USS зеркалит
            // подписи и ряды по классу sg-rtl.
            EnableInClassList("sg-rtl", config.IsRightToLeft);

            SetLabel(_category, _text.Category);
            SetLabel(_subcategory, _text.Subcategory);
            SetLabel(_email, _text.Email);
            SetLabel(_subject, _text.Subject);
            SetLabel(_description, _text.Description);

            _categoryOptions.Clear();
            _categoryOptions.AddRange(config.Categories.Count > 0
                ? Options(config.Categories)
                : new List<SupportGateOption>());
            FillDropdown(_category, _categoryOptions);

            _email.value = config.Email ?? string.Empty;
            _email.SetEnabled(!config.EmailLocked);

            ClearErrors();
        }

        public void BindSubcategories(IReadOnlyList<SupportGateOption> options)
        {
            _subcategoryOptions.Clear();
            for (var i = 0; i < options.Count; i++)
            {
                _subcategoryOptions.Add(options[i]);
            }

            FillDropdown(_subcategory, _subcategoryOptions);
        }

        public void BindExtraFields(IReadOnlyList<SupportGateExtraField> fields)
        {
            _extra.Clear();
            _extraControls.Clear();
            _extraOptions.Clear();

            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];

                if (field.Type == SupportGateFieldType.Select)
                {
                    var dropdown = new DropdownField { label = field.Label };
                    var options = new List<SupportGateOption>(field.Options);
                    _extraOptions[field.Id] = options;
                    FillDropdown(dropdown, options);
                    Register(field.Id, dropdown);
                    continue;
                }

                var input = new TextField { label = field.Label };
                if (field.MaxLength > 0)
                {
                    input.maxLength = field.MaxLength;
                }

                Register(field.Id, input);
            }
        }

        public void BindAttachments(IReadOnlyList<SupportGateAttachmentItem> attachments)
        {
            _attachments.Clear();

            for (var i = 0; i < attachments.Count; i++)
            {
                var index = i;
                var item = attachments[i];

                var row = new VisualElement();
                row.AddToClassList("sg-attachment");

                var name = new Label(item.FileName);
                name.AddToClassList("sg-attachment-name");

                var status = new Label(item.State == SupportGateUploadState.Uploading
                    ? _text.Uploading
                    : Size(item.Size));
                status.AddToClassList("sg-attachment-status");

                var remove = new Button(() => AttachmentRemoved?.Invoke(index)) { text = _text.Remove };
                remove.AddToClassList("sg-button");

                row.Add(name);
                row.Add(status);
                row.Add(remove);
                _attachments.Add(row);
            }
        }

        public void ShowMessage(string message)
        {
            _banner.text = message ?? string.Empty;
            _banner.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void ShowFieldErrors(IReadOnlyDictionary<string, string> errors)
        {
            ClearErrors();

            foreach (var error in errors)
            {
                if (_errors.TryGetValue(error.Key, out var label))
                {
                    label.text = error.Value;
                    label.style.display = DisplayStyle.Flex;
                }
            }
        }

        public void ShowSent(string message)
        {
            _stateLabel.text = message;
        }

        public SupportGateFormValues ReadValues()
        {
            var values = new SupportGateFormValues
            {
                Category = IdOf(_categoryOptions, _category.value),
                Subcategory = IdOf(_subcategoryOptions, _subcategory.value),
                Email = _emailLocked ? null : _email.value,
                Subject = _subject.value,
                Description = _description.value
            };

            foreach (var control in _extraControls)
            {
                if (control.Value is DropdownField dropdown)
                {
                    values.Extra[control.Key] = IdOf(_extraOptions[control.Key], dropdown.value);
                    continue;
                }

                values.Extra[control.Key] = ((TextField)control.Value).value;
            }

            return values;
        }

        private void OnSubmitClicked()
        {
            // На экране ошибки загрузки та же кнопка повторяет запрос справочника.
            if (_state == SupportGateViewState.LoadFailed)
            {
                RetryRequested?.Invoke();
                return;
            }

            SubmitRequested?.Invoke();
        }

        private void BuildField(VisualElement control, string name)
        {
            var holder = new VisualElement();
            holder.AddToClassList("sg-field");
            control.AddToClassList("sg-control");

            var error = new Label();
            error.AddToClassList("sg-error");
            error.style.display = DisplayStyle.None;

            holder.Add(control);
            holder.Add(error);
            _form.Add(holder);
            _errors[name] = error;
        }

        private void Register(string id, VisualElement control)
        {
            var holder = new VisualElement();
            holder.AddToClassList("sg-field");
            control.AddToClassList("sg-control");

            var error = new Label();
            error.AddToClassList("sg-error");
            error.style.display = DisplayStyle.None;

            holder.Add(control);
            holder.Add(error);
            _extra.Add(holder);

            _extraControls[id] = control;
            _errors["extra." + id] = error;
        }

        private void ClearErrors()
        {
            foreach (var label in _errors.Values)
            {
                label.text = string.Empty;
                label.style.display = DisplayStyle.None;
            }
        }

        private void FillDropdown(DropdownField dropdown, IReadOnlyList<SupportGateOption> options)
        {
            var choices = new List<string> { _text.Choose };
            for (var i = 0; i < options.Count; i++)
            {
                choices.Add(options[i].Label);
            }

            dropdown.choices = choices;
            dropdown.index = 0;
        }

        private static void SetLabel(VisualElement control, string text)
        {
            if (control is DropdownField dropdown)
            {
                dropdown.label = text;
                return;
            }

            ((TextField)control).label = text;
        }

        private static List<SupportGateOption> Options(IReadOnlyList<SupportGateCategory> categories)
        {
            var options = new List<SupportGateOption>();
            for (var i = 0; i < categories.Count; i++)
            {
                options.Add(new SupportGateOption(categories[i].Id, categories[i].Label));
            }

            return options;
        }

        /// <summary>
        /// Dropdown хранит подпись, а сервису нужен идентификатор: первый пункт —
        /// «Выберите…», ему соответствует пустое значение.
        /// </summary>
        private static string IdOf(IReadOnlyList<SupportGateOption> options, string label)
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i].Label == label)
                {
                    return options[i].Id;
                }
            }

            return null;
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

            return (bytes / 1024 / 1024) + " MB";
        }
    }
}
