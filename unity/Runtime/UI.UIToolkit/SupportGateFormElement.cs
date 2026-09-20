using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hooligapps.SupportGate.UI.UIToolkit
{
    /// <summary>
    /// Форма на UI Toolkit. Строится кодом, без uxml-шаблона: разметка зависит от
    /// справочника, который приходит с сервера, и заранее её всё равно не разложить.
    ///
    /// Раскладка повторяет веб-версию (sdk/js): шапка с заголовком и крестиком,
    /// тело в две колонки — слева категория, тема и доп. поля, справа почта, тема
    /// письма, описание и плитки вложений, — подвал с кнопками. На узком экране
    /// колонки складываются в одну (класс sg-narrow). Оформление — в
    /// Uss/SupportGate.uss, игра может подменить его своим.
    /// </summary>
    public sealed class SupportGateFormElement : VisualElement, ISupportGateFormView<VisualElement>
    {
        /// <summary>Уже колонки складываются, как @media (max-width: 680px) в вебе.</summary>
        public const float NarrowWidth = 1000f;

        private readonly Label _title = new Label();
        private readonly Button _close = new Button();
        private readonly Label _banner = new Label();
        private readonly VisualElement _stateBox = new VisualElement();
        private readonly Label _stateLabel = new Label();
        private readonly ScrollView _body = new ScrollView(ScrollViewMode.Vertical);
        private readonly VisualElement _columns = new VisualElement();
        private readonly VisualElement _extra = new VisualElement();
        private readonly VisualElement _files = new VisualElement();
        private readonly VisualElement _drop = new VisualElement();
        private readonly Label _dropText = new Label();
        private readonly Label _attachmentsLabel = new Label();
        private readonly Label _attachmentsHint = new Label();
        private readonly Button _submit = new Button();
        private readonly Button _cancel = new Button();

        private readonly DropdownField _category = new DropdownField();
        private readonly DropdownField _subcategory = new DropdownField();
        private readonly TextField _email = new TextField();
        private readonly TextField _subject = new TextField();
        private readonly TextField _description = new TextField();

        private readonly Dictionary<string, Label> _errors = new Dictionary<string, Label>();
        private readonly Dictionary<string, VisualElement> _holders = new Dictionary<string, VisualElement>();
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

            var header = new VisualElement();
            header.AddToClassList("sg-header");
            _title.AddToClassList("sg-title");
            _close.AddToClassList("sg-close");
            _close.text = "×";
            _close.clicked += () => CloseRequested?.Invoke();
            header.Add(_title);
            header.Add(_close);
            Add(header);

            // Баннер над телом и над экраном состояния: сообщение о сбое загрузки
            // видно рядом с «Повторить».
            _banner.AddToClassList("sg-banner");
            _banner.style.display = DisplayStyle.None;
            Add(_banner);

            _stateBox.AddToClassList("sg-state-box");
            _stateLabel.AddToClassList("sg-state");
            _stateBox.Add(_stateLabel);
            Add(_stateBox);

            _body.AddToClassList("sg-body");
            Add(_body);

            _columns.AddToClassList("sg-columns");
            _body.Add(_columns);

            var left = new VisualElement();
            left.AddToClassList("sg-col");
            var right = new VisualElement();
            right.AddToClassList("sg-col");
            right.AddToClassList("sg-col-text");
            _columns.Add(left);
            _columns.Add(right);

            left.Add(BuildField(_category, "category"));
            left.Add(BuildField(_subcategory, "subcategory"));
            left.Add(_extra);

            right.Add(BuildField(_email, "email"));
            right.Add(BuildField(_subject, "subject"));
            right.Add(BuildField(_description, "description"));
            _description.multiline = true;
            _description.AddToClassList("sg-multiline");
            _holders["description"].AddToClassList("sg-field-description");

            right.Add(BuildAttachments());

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

            RegisterCallback<GeometryChangedEvent>(evt =>
                EnableInClassList("sg-narrow", evt.newRect.width > 0 && evt.newRect.width < NarrowWidth));

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
            _body.style.display = showsForm ? DisplayStyle.Flex : DisplayStyle.None;
            _stateBox.style.display = showsForm ? DisplayStyle.None : DisplayStyle.Flex;

            _submit.SetEnabled(state == SupportGateViewState.Form);
            _drop.SetEnabled(state == SupportGateViewState.Form);

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
            _dropText.text = _text.AddFile;
            _attachmentsLabel.text = _text.Attachments;
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
            _subject.value = string.Empty;
            _description.value = string.Empty;

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
            foreach (var id in _extraControls.Keys)
            {
                _errors.Remove("extra." + id);
                _holders.Remove("extra." + id);
            }

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
                    _extra.Add(BuildField(dropdown, "extra." + field.Id));
                    _extraControls[field.Id] = dropdown;
                    continue;
                }

                var input = new TextField { label = field.Label };
                if (field.MaxLength > 0)
                {
                    input.maxLength = field.MaxLength;
                }

                _extra.Add(BuildField(input, "extra." + field.Id));
                _extraControls[field.Id] = input;
            }
        }

        public void BindAttachments(IReadOnlyList<SupportGateAttachmentItem> attachments)
        {
            _files.Clear();

            for (var i = 0; i < attachments.Count; i++)
            {
                _files.Add(BuildTile(i, attachments[i]));
            }

            // Плитка «Добавить файлы» всегда последняя, как в вебе.
            _files.Add(_drop);
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
                    _holders[error.Key].AddToClassList("sg-invalid");
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

        /// <summary>
        /// Поле с подписью над контролом и строкой ошибки под ним. Встроенная подпись
        /// DropdownField/TextField стоит слева от контрола; в вебе она сверху, поэтому
        /// подпись вынесена в отдельный Label, а встроенная скрыта через USS.
        /// </summary>
        private VisualElement BuildField(VisualElement control, string name)
        {
            var holder = new VisualElement();
            holder.AddToClassList("sg-field");

            var label = new Label();
            label.AddToClassList("sg-label");
            control.AddToClassList("sg-control");

            var error = new Label();
            error.AddToClassList("sg-error");
            error.style.display = DisplayStyle.None;

            holder.Add(label);
            holder.Add(control);
            holder.Add(error);

            _errors[name] = error;
            _holders[name] = holder;
            SetLabel(control, LabelOf(control));

            return holder;
        }

        private VisualElement BuildAttachments()
        {
            var holder = new VisualElement();
            holder.AddToClassList("sg-field");
            holder.AddToClassList("sg-field-attachments");

            _attachmentsLabel.AddToClassList("sg-label");
            _files.AddToClassList("sg-files");
            _attachmentsHint.AddToClassList("sg-hint");

            _drop.AddToClassList("sg-drop");
            var icon = new Label("+");
            icon.AddToClassList("sg-drop-icon");
            _dropText.AddToClassList("sg-drop-text");
            _drop.Add(icon);
            _drop.Add(_dropText);
            _drop.RegisterCallback<ClickEvent>(_ =>
            {
                if (_drop.enabledSelf) AttachRequested?.Invoke();
            });
            _files.Add(_drop);

            holder.Add(_attachmentsLabel);
            holder.Add(_files);
            holder.Add(_attachmentsHint);
            return holder;
        }

        private VisualElement BuildTile(int index, SupportGateAttachmentItem item)
        {
            var tile = new VisualElement();
            tile.AddToClassList("sg-file");
            tile.EnableInClassList("sg-file-pending", item.State == SupportGateUploadState.Uploading);

            var thumb = new VisualElement();
            thumb.AddToClassList("sg-thumb");
            if (item.Preview != null)
            {
                var image = new Image { image = item.Preview, scaleMode = ScaleMode.ScaleAndCrop };
                image.AddToClassList("sg-thumb-image");
                thumb.Add(image);
            }
            else
            {
                var ext = new Label(item.Extension);
                ext.AddToClassList("sg-thumb-ext");
                thumb.Add(ext);
            }

            var meta = new VisualElement();
            meta.AddToClassList("sg-file-meta");
            var name = new Label(item.FileName);
            name.AddToClassList("sg-name");
            var status = new Label(item.State == SupportGateUploadState.Uploading
                ? _text.Uploading
                : Size(item.Size));
            status.AddToClassList("sg-size");
            meta.Add(name);
            meta.Add(status);

            var remove = new Button(() => AttachmentRemoved?.Invoke(index)) { text = "×", tooltip = _text.Remove };
            remove.AddToClassList("sg-remove");

            tile.Add(thumb);
            tile.Add(meta);
            tile.Add(remove);
            return tile;
        }

        private void ClearErrors()
        {
            foreach (var label in _errors.Values)
            {
                label.text = string.Empty;
                label.style.display = DisplayStyle.None;
            }

            foreach (var holder in _holders.Values)
            {
                holder.RemoveFromClassList("sg-invalid");
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

        /// <summary>
        /// Подпись живёт в двух местах: во встроенном label контрола (по нему
        /// работают тесты и доступность) и в видимом Label над ним.
        /// </summary>
        private void SetLabel(VisualElement control, string text)
        {
            if (control is DropdownField dropdown)
            {
                dropdown.label = text;
            }
            else
            {
                ((TextField)control).label = text;
            }

            var visible = control.parent?.Q<Label>(className: "sg-label");
            if (visible != null)
            {
                visible.text = text ?? string.Empty;
            }
        }

        private static string LabelOf(VisualElement control)
        {
            return control is DropdownField dropdown ? dropdown.label : ((TextField)control).label;
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
