using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Hooligapps.SupportGate.UI
{
    /// <summary>
    /// Связывает клиент, вью и модальную систему игры. Рендерер-агностичен:
    /// TRoot задаёт uGUI- или UI Toolkit-реализация.
    ///
    /// Правила, за которые отвечает презентер: одна отправка одновременно, повторные
    /// нажатия во время отправки игнорируются, поздние коллбэки после закрытия
    /// отбрасываются, ключ идемпотентности не меняется между попытками отправить
    /// одно и то же обращение.
    /// </summary>
    public sealed class SupportGateFormPresenter<TRoot> : IDisposable
    {
        private static readonly Regex EmailPattern = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

        private readonly SupportGateClient _client;
        private readonly ISupportGateFormView<TRoot> _view;
        private readonly ISupportGateModalHost<TRoot> _host;
        private readonly MonoBehaviour _runner;
        private readonly List<SupportGateAttachmentItem> _attachments = new List<SupportGateAttachmentItem>();

        private SupportGateStrings _text = SupportGateStrings.English();
        private bool _customStrings;
        private SupportGateFormConfig _config;
        private string _idempotencyKey;
        private int _generation;
        private bool _visible;
        private bool _sending;
        private bool _sent;
        private bool _disposed;

        public SupportGateFormPresenter(SupportGateClient client, ISupportGateFormView<TRoot> view,
            ISupportGateModalHost<TRoot> host, MonoBehaviour runner)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));

            _view.CloseRequested += Close;
            _view.RetryRequested += OnRetryRequested;
            _view.SubmitRequested += OnSubmitRequested;
            _view.CategoryChanged += OnCategoryChanged;
            _view.AttachRequested += OnAttachRequested;
            _view.AttachmentRemoved += OnAttachmentRemoved;
        }

        /// <summary>Обращение принято сервисом.</summary>
        public event Action<SupportGateTicket> Submitted;

        /// <summary>Игра отказалась показывать окно.</summary>
        public event Action HostRefused;

        public event Action<SupportGateError> Failed;

        public event Action Closed;

        /// <summary>
        /// Итог показа для лога игры: Sent один раз с номером, Error на каждую
        /// неудачную загрузку справочника или отправку, Closed — если окно закрыли,
        /// так и не отправив. См. <see cref="SupportGateFormResult"/>.
        /// </summary>
        public event Action<SupportGateFormResult> Completed;

        /// <summary>Игра выбирает файл сама: обработчик должен вызвать AttachFile.</summary>
        public event Action AttachRequested;

        public bool IsVisible
        {
            get { return _visible; }
        }

        public bool IsSending
        {
            get { return _sending; }
        }

        public IReadOnlyList<SupportGateAttachmentItem> Attachments
        {
            get { return _attachments; }
        }

        /// <summary>
        /// Строки окна. По умолчанию приходят с сервера вместе со справочником на
        /// языке игрока; присвоить свои — если у игры своя локализация, тогда
        /// серверные игнорируются.
        /// </summary>
        public SupportGateStrings Strings
        {
            get { return _text; }
            set
            {
                _customStrings = value != null;
                _text = value ?? SupportGateStrings.English();
            }
        }

        /// <summary>Открывает окно и загружает справочник.</summary>
        public bool Open()
        {
            if (_disposed || _visible || _runner == null)
            {
                return false;
            }

            if (!_host.TryShow(_view.Root))
            {
                HostRefused?.Invoke();
                return false;
            }

            _visible = true;
            _sending = false;
            _sent = false;
            _idempotencyKey = null;
            ClearAttachments();
            _view.ShowMessage(null);
            LoadForm();

            return true;
        }

        public void Close()
        {
            if (!_visible)
            {
                return;
            }

            // Поздние ответы после закрытия трогать вью уже не должны.
            _generation++;
            _visible = false;
            _sending = false;

            _view.SetState(SupportGateViewState.Hidden);
            _host.Close(_view.Root);
            ClearAttachments();
            Closed?.Invoke();

            if (!_sent)
            {
                Completed?.Invoke(SupportGateFormResult.Closed());
            }
        }

        /// <summary>
        /// Прикладывает файл: он сразу уходит в хранилище, а в обращение попадает
        /// только идентификатор. Выбор файла делает игра — на мобильных это её
        /// нативный пикер.
        /// </summary>
        public void AttachFile(string fileName, string contentType, byte[] data)
        {
            if (_disposed || !_visible || _config == null)
            {
                return;
            }

            if (_attachments.Count >= _config.Attachments.MaxFiles)
            {
                _view.ShowMessage(_text.TooManyFiles(_config.Attachments.MaxFiles));
                return;
            }

            var item = new SupportGateAttachmentItem(fileName, contentType, data == null ? 0 : data.LongLength)
            {
                Preview = SupportGatePreview.Create(contentType, data)
            };
            _attachments.Add(item);
            _view.BindAttachments(_attachments);

            var generation = _generation;
            _runner.StartCoroutine(_client.UploadAttachment(fileName, contentType, data, result =>
            {
                if (!IsCurrent(generation))
                {
                    return;
                }

                if (!result.Success)
                {
                    // Файл не ушёл — убираем строку, иначе отправка молча потеряет вложение.
                    _attachments.Remove(item);
                    item.ReleasePreview();
                    _view.BindAttachments(_attachments);
                    _view.ShowMessage(Describe(result.Error));
                    Failed?.Invoke(result.Error);
                    return;
                }

                item.AttachmentId = result.Value.AttachmentId;
                item.State = SupportGateUploadState.Ready;
                _view.BindAttachments(_attachments);
            }));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _generation++;

            _view.CloseRequested -= Close;
            _view.RetryRequested -= OnRetryRequested;
            _view.SubmitRequested -= OnSubmitRequested;
            _view.CategoryChanged -= OnCategoryChanged;
            _view.AttachRequested -= OnAttachRequested;
            _view.AttachmentRemoved -= OnAttachmentRemoved;

            if (_visible)
            {
                _visible = false;
                _view.SetState(SupportGateViewState.Hidden);
                _host.Close(_view.Root);
            }

            ClearAttachments();
        }

        private void ClearAttachments()
        {
            for (var i = 0; i < _attachments.Count; i++)
            {
                _attachments[i].ReleasePreview();
            }

            _attachments.Clear();
        }

        private void LoadForm()
        {
            _view.SetState(SupportGateViewState.Loading);

            var generation = _generation;
            _runner.StartCoroutine(_client.LoadForm(result =>
            {
                if (!IsCurrent(generation))
                {
                    return;
                }

                if (!result.Success)
                {
                    _view.ShowMessage(Describe(result.Error));
                    _view.SetState(SupportGateViewState.LoadFailed);
                    Failed?.Invoke(result.Error);
                    Completed?.Invoke(SupportGateFormResult.Failed(SupportGateResultStage.Load, result.Error));
                    return;
                }

                ApplyConfig(result.Value);
            }));
        }

        private void ApplyConfig(SupportGateFormConfig config)
        {
            _config = config;
            if (!_customStrings)
            {
                _text = SupportGateStrings.FromConfig(config);
            }

            _view.BindConfig(config, _text);
            _view.BindSubcategories(new SupportGateOption[0]);
            _view.BindExtraFields(new SupportGateExtraField[0]);
            _view.BindAttachments(_attachments);
            _view.SetState(SupportGateViewState.Form);
        }

        private void OnRetryRequested()
        {
            if (!_visible || _config != null)
            {
                return;
            }

            _view.ShowMessage(null);
            LoadForm();
        }

        private void OnCategoryChanged(string categoryId)
        {
            if (_config == null)
            {
                return;
            }

            var category = _config.FindCategory(categoryId);
            _view.BindSubcategories(category != null
                ? category.Subcategories
                : new SupportGateOption[0]);
            _view.BindExtraFields(category != null
                ? category.ExtraFields
                : new SupportGateExtraField[0]);
        }

        private void OnAttachRequested()
        {
            AttachRequested?.Invoke();
        }

        private void OnAttachmentRemoved(int index)
        {
            if (index < 0 || index >= _attachments.Count)
            {
                return;
            }

            _attachments[index].ReleasePreview();
            _attachments.RemoveAt(index);
            _view.BindAttachments(_attachments);
        }

        private void OnSubmitRequested()
        {
            if (_disposed || !_visible || _sending || _config == null)
            {
                return;
            }

            var values = _view.ReadValues();
            var errors = Validate(values);
            if (errors.Count > 0)
            {
                _view.ShowFieldErrors(errors);
                return;
            }

            foreach (var attachment in _attachments)
            {
                if (attachment.State != SupportGateUploadState.Ready)
                {
                    _view.ShowMessage(_text.WaitForUploads);
                    return;
                }
            }

            _view.ShowFieldErrors(new Dictionary<string, string>());
            _view.ShowMessage(null);

            var draft = BuildDraft(values);

            // Ключ живёт до успешной отправки: повтор после сбоя не создаёт дубль.
            if (string.IsNullOrEmpty(_idempotencyKey))
            {
                _idempotencyKey = SupportGateClient.NewIdempotencyKey();
            }

            _sending = true;
            _view.SetState(SupportGateViewState.Sending);

            var generation = _generation;
            _runner.StartCoroutine(_client.SubmitTicket(draft, _idempotencyKey, result =>
            {
                if (!IsCurrent(generation))
                {
                    return;
                }

                _sending = false;

                if (!result.Success)
                {
                    _view.SetState(SupportGateViewState.Form);
                    _view.ShowMessage(Describe(result.Error));
                    _view.ShowFieldErrors(result.Error.Fields);
                    Failed?.Invoke(result.Error);
                    Completed?.Invoke(SupportGateFormResult.Failed(SupportGateResultStage.Submit, result.Error));
                    return;
                }

                _idempotencyKey = null;
                _sent = true;
                _view.ShowSent(_text.SentText(result.Value.DisplayId));
                _view.SetState(SupportGateViewState.Sent);
                Submitted?.Invoke(result.Value);
                Completed?.Invoke(SupportGateFormResult.Sent(result.Value));
            }));
        }

        private SupportGateTicketDraft BuildDraft(SupportGateFormValues values)
        {
            var draft = new SupportGateTicketDraft
            {
                Category = values.Category,
                Subcategory = values.Subcategory,
                Subject = Trim(values.Subject),
                Description = Trim(values.Description),
                // Подтверждённую почту сервис берёт из токена: слать её обратно незачем.
                Email = _config.EmailLocked ? null : Trim(values.Email)
            };

            foreach (var pair in values.Extra)
            {
                var value = Trim(pair.Value);
                if (!string.IsNullOrEmpty(value))
                {
                    draft.Extra[pair.Key] = value;
                }
            }

            foreach (var attachment in _attachments)
            {
                draft.AttachmentIds.Add(attachment.AttachmentId);
            }

            return draft;
        }

        private Dictionary<string, string> Validate(SupportGateFormValues values)
        {
            var errors = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(values.Category))
            {
                errors["category"] = _text.Required;
            }

            if (string.IsNullOrEmpty(values.Subcategory))
            {
                errors["subcategory"] = _text.Required;
            }

            if (string.IsNullOrEmpty(Trim(values.Subject)))
            {
                errors["subject"] = _text.Required;
            }

            if (string.IsNullOrEmpty(Trim(values.Description)))
            {
                errors["description"] = _text.Required;
            }

            if (!_config.EmailLocked)
            {
                var email = Trim(values.Email);
                if (string.IsNullOrEmpty(email))
                {
                    errors["email"] = _text.Required;
                }
                else if (!EmailPattern.IsMatch(email))
                {
                    errors["email"] = _text.InvalidEmail;
                }
            }

            var category = _config.FindCategory(values.Category);
            if (category != null)
            {
                foreach (var field in category.ExtraFields)
                {
                    if (!field.Required)
                    {
                        continue;
                    }

                    values.Extra.TryGetValue(field.Id, out var value);
                    if (string.IsNullOrEmpty(Trim(value)))
                    {
                        errors["extra." + field.Id] = _text.Required;
                    }
                }
            }

            return errors;
        }

        /// <summary>Человеческий текст ошибки: понятные случаи переводим, остальное — как есть.</summary>
        private string Describe(SupportGateError error)
        {
            if (error == null)
            {
                return _text.FailedTitle;
            }

            if (error.Kind == SupportGateErrorKind.Network)
            {
                return _text.Offline;
            }

            if (error.HttpStatus == 429)
            {
                return _text.RateLimited;
            }

            if (error.IsUnauthorized)
            {
                return _text.SessionExpired;
            }

            return string.IsNullOrEmpty(error.Message) ? _text.FailedTitle : error.Message;
        }

        private bool IsCurrent(int generation)
        {
            return !_disposed && _visible && generation == _generation;
        }

        private static string Trim(string value)
        {
            return value == null ? null : value.Trim();
        }
    }
}
