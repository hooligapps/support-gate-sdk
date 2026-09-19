using System.Collections.Generic;

namespace Hooligapps.SupportGate.UI
{
    /// <summary>
    /// Подписи самого окна. Переводы живут на шлюзе и приходят вместе с
    /// <c>/v1/form</c> в <see cref="SupportGateFormConfig.Strings"/> уже на языке
    /// игрока; здесь только английский — на экран загрузки и на случай, когда
    /// форму получить не удалось. Категории и поля переводит сервер сам.
    ///
    /// Игра может подставить свои строки, если у неё своя система локализации:
    /// присвоить <see cref="SupportGateFormPresenter{TRoot}.Strings"/> до Open.
    /// </summary>
    public sealed class SupportGateStrings
    {
        /// <summary>Язык строк: по нему считаются формы множественного числа.</summary>
        public string Locale = "en";

        public string Title = "Contact support";
        public string Category = "Category";
        public string Subcategory = "Topic";
        public string Choose = "Choose…";
        public string Email = "Email";
        public string EmailHint = "We will reply to this address.";
        public string Subject = "Subject";
        public string Description = "Describe the problem";
        public string Attachments = "Attachments";
        public string AddFile = "Add files";
        public string UnsupportedType = "This file type is not supported";
        public string Remove = "Remove";
        public string Uploading = "Uploading…";
        public string Submit = "Send";
        public string Submitting = "Sending…";
        public string Cancel = "Cancel";
        public string Close = "Close";
        public string Required = "Required field";
        public string InvalidEmail = "Enter a valid email address";
        public string Loading = "Loading…";
        public string LoadFailed = "Could not load the form.";
        public string Retry = "Try again";
        public string SentTitle = "Request sent";
        public string FailedTitle = "Could not send the request";
        public string RateLimited = "Too many requests. Please try again later.";
        public string SessionExpired = "The session has expired. Reopen the window.";
        public string Offline = "No connection to the server. Check the network and try again.";
        public string WaitForUploads = "Wait until the attachments finish uploading.";

        /// <summary>Шаблоны ICU MessageFormat — те же, что в каталоге шлюза.</summary>
        public string SentTextFormat = "Your request number is {id}. We will reply by email.";
        public string AttachmentsHintFormat = "Up to {max, plural, one {# file} other {# files}}, {mb} MB each.";
        public string TooManyFilesFormat = "No more than {max, plural, one {# file} other {# files}}";

        public string SentText(string id)
        {
            return Format(SentTextFormat, "id", id);
        }

        public string AttachmentsHint(int maxFiles, long maxFileSize)
        {
            return SupportGateMessageFormat.Format(AttachmentsHintFormat,
                new Dictionary<string, object> { { "max", maxFiles }, { "mb", maxFileSize / 1024 / 1024 } },
                Locale);
        }

        public string TooManyFiles(int maxFiles)
        {
            return Format(TooManyFilesFormat, "max", maxFiles);
        }

        public static SupportGateStrings English()
        {
            return new SupportGateStrings();
        }

        /// <summary>
        /// Строки из ответа сервера. Ключа нет или он пустой — остаётся английский:
        /// неполный перевод не должен оставлять пустых кнопок.
        /// </summary>
        public static SupportGateStrings FromConfig(SupportGateFormConfig config)
        {
            return FromCatalog(config != null ? config.Strings : null, config != null ? config.Locale : null);
        }

        public static SupportGateStrings FromCatalog(IReadOnlyDictionary<string, string> catalog, string locale)
        {
            var text = new SupportGateStrings();
            if (!string.IsNullOrEmpty(locale))
            {
                text.Locale = locale;
            }

            if (catalog == null)
            {
                return text;
            }

            text.Title = Pick(catalog, "ui.title", text.Title);
            text.Category = Pick(catalog, "ui.category", text.Category);
            text.Subcategory = Pick(catalog, "ui.subcategory", text.Subcategory);
            text.Choose = Pick(catalog, "ui.choose", text.Choose);
            text.Email = Pick(catalog, "ui.email", text.Email);
            text.EmailHint = Pick(catalog, "ui.email_hint", text.EmailHint);
            text.Subject = Pick(catalog, "ui.subject", text.Subject);
            text.Description = Pick(catalog, "ui.description", text.Description);
            text.Attachments = Pick(catalog, "ui.attachments", text.Attachments);
            text.AddFile = Pick(catalog, "ui.add_files", text.AddFile);
            text.UnsupportedType = Pick(catalog, "ui.unsupported_type", text.UnsupportedType);
            text.Remove = Pick(catalog, "ui.remove", text.Remove);
            text.Uploading = Pick(catalog, "ui.uploading", text.Uploading);
            text.Submit = Pick(catalog, "ui.submit", text.Submit);
            text.Submitting = Pick(catalog, "ui.submitting", text.Submitting);
            text.Cancel = Pick(catalog, "ui.cancel", text.Cancel);
            text.Close = Pick(catalog, "ui.close", text.Close);
            text.Required = Pick(catalog, "ui.required", text.Required);
            text.InvalidEmail = Pick(catalog, "ui.invalid_email", text.InvalidEmail);
            text.Loading = Pick(catalog, "ui.loading", text.Loading);
            text.LoadFailed = Pick(catalog, "ui.load_failed", text.LoadFailed);
            text.Retry = Pick(catalog, "ui.retry", text.Retry);
            text.SentTitle = Pick(catalog, "ui.sent_title", text.SentTitle);
            text.FailedTitle = Pick(catalog, "ui.failed_title", text.FailedTitle);
            text.RateLimited = Pick(catalog, "ui.rate_limited", text.RateLimited);
            text.SessionExpired = Pick(catalog, "ui.session_expired", text.SessionExpired);
            text.Offline = Pick(catalog, "ui.offline", text.Offline);
            text.SentTextFormat = Pick(catalog, "ui.sent_text", text.SentTextFormat);
            text.AttachmentsHintFormat = Pick(catalog, "ui.attachments_hint", text.AttachmentsHintFormat);
            text.TooManyFilesFormat = Pick(catalog, "ui.too_many_files", text.TooManyFilesFormat);
            // ui.wait_for_uploads в каталоге нет: веб-форма не даёт нажать «Отправить»
            // во время загрузки, здесь это сообщение, и оно остаётся английским.

            return text;
        }

        private static string Pick(IReadOnlyDictionary<string, string> catalog, string key, string fallback)
        {
            string value;
            return catalog.TryGetValue(key, out value) && !string.IsNullOrEmpty(value) ? value : fallback;
        }

        private string Format(string template, string name, object value)
        {
            return SupportGateMessageFormat.Format(template, new Dictionary<string, object> { { name, value } },
                Locale);
        }
    }
}
