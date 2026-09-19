using System.Collections.Generic;

namespace Hooligapps.SupportGate
{
    /// <summary>Тип поля из справочника. Неизвестные типы приходят как Text.</summary>
    public enum SupportGateFieldType
    {
        Text = 0,
        Select = 1,
        Date = 2
    }

    public sealed class SupportGateOption
    {
        public string Id { get; }

        /// <summary>Подпись уже на языке игрока: локализует сервер.</summary>
        public string Label { get; }

        public SupportGateOption(string id, string label)
        {
            Id = id;
            Label = label;
        }
    }

    public sealed class SupportGateExtraField
    {
        private static readonly IReadOnlyList<SupportGateOption> NoOptions = new SupportGateOption[0];

        public string Id { get; }
        public string Label { get; }
        public SupportGateFieldType Type { get; }
        public bool Required { get; }
        public IReadOnlyList<SupportGateOption> Options { get; }

        /// <summary>0, если сервер длину не ограничил.</summary>
        public int MaxLength { get; }

        public SupportGateExtraField(string id, string label, SupportGateFieldType type, bool required,
            IReadOnlyList<SupportGateOption> options = null, int maxLength = 0)
        {
            Id = id;
            Label = label;
            Type = type;
            Required = required;
            Options = options ?? NoOptions;
            MaxLength = maxLength;
        }
    }

    public sealed class SupportGateCategory
    {
        private static readonly IReadOnlyList<SupportGateExtraField> NoFields = new SupportGateExtraField[0];
        private static readonly IReadOnlyList<SupportGateOption> NoSubcategories = new SupportGateOption[0];

        public string Id { get; }
        public string Label { get; }
        public IReadOnlyList<SupportGateOption> Subcategories { get; }

        /// <summary>Поля, которые показываются только для этой категории.</summary>
        public IReadOnlyList<SupportGateExtraField> ExtraFields { get; }

        public SupportGateCategory(string id, string label, IReadOnlyList<SupportGateOption> subcategories,
            IReadOnlyList<SupportGateExtraField> extraFields = null)
        {
            Id = id;
            Label = label;
            Subcategories = subcategories ?? NoSubcategories;
            ExtraFields = extraFields ?? NoFields;
        }
    }

    public sealed class SupportGateAttachmentLimits
    {
        public int MaxFiles { get; }
        public long MaxFileSize { get; }
        public IReadOnlyList<string> AllowedTypes { get; }

        public SupportGateAttachmentLimits(int maxFiles, long maxFileSize, IReadOnlyList<string> allowedTypes)
        {
            MaxFiles = maxFiles;
            MaxFileSize = maxFileSize;
            AllowedTypes = allowedTypes ?? new string[0];
        }

        public bool Accepts(string contentType)
        {
            if (AllowedTypes.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < AllowedTypes.Count; i++)
            {
                if (string.Equals(AllowedTypes[i], contentType, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Справочник и настройки формы. Приходит с сервера, поэтому категории и лимиты
    /// меняются без релиза игры.
    /// </summary>
    public sealed class SupportGateFormConfig
    {
        private static readonly IReadOnlyDictionary<string, string> NoStrings = new Dictionary<string, string>();

        public string Game { get; }
        public string Locale { get; }

        /// <summary>"ltr" или "rtl" — направление письма для выбранного языка.</summary>
        public string Direction { get; }

        /// <summary>
        /// Подписи окна на языке игрока, ключи ui.*. Переводы живут на шлюзе, в
        /// пакете только английский на случай, когда форму получить не удалось.
        /// </summary>
        public IReadOnlyDictionary<string, string> Strings { get; }

        /// <summary>Почта из токена или null.</summary>
        public string Email { get; }

        /// <summary>Почта подтверждена бэкендом игры: игрок её не правит.</summary>
        public bool EmailLocked { get; }

        public IReadOnlyList<SupportGateCategory> Categories { get; }
        public SupportGateAttachmentLimits Attachments { get; }

        public SupportGateFormConfig(string game, string locale, string email, bool emailLocked,
            IReadOnlyList<SupportGateCategory> categories, SupportGateAttachmentLimits attachments,
            IReadOnlyDictionary<string, string> strings = null, string direction = null)
        {
            Game = game;
            Locale = locale;
            Direction = direction == "rtl" ? "rtl" : "ltr";
            Strings = strings ?? NoStrings;
            Email = email;
            EmailLocked = emailLocked;
            Categories = categories ?? new SupportGateCategory[0];
            Attachments = attachments;
        }

        public bool IsRightToLeft
        {
            get { return Direction == "rtl"; }
        }

        public SupportGateCategory FindCategory(string id)
        {
            for (var i = 0; i < Categories.Count; i++)
            {
                if (Categories[i].Id == id)
                {
                    return Categories[i];
                }
            }

            return null;
        }
    }

    public sealed class SupportGateAttachment
    {
        public string AttachmentId { get; }
        public string FileName { get; }
        public string ContentType { get; }
        public long Size { get; }

        public SupportGateAttachment(string attachmentId, string fileName, string contentType, long size)
        {
            AttachmentId = attachmentId;
            FileName = fileName;
            ContentType = contentType;
            Size = size;
        }
    }

    /// <summary>Ссылка на загрузку: выдаётся сервисом, файл уходит мимо него.</summary>
    public sealed class SupportGateUploadTicket
    {
        public string AttachmentId { get; }
        public string UploadUrl { get; }
        public string Method { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }
        public int ExpiresIn { get; }

        public SupportGateUploadTicket(string attachmentId, string uploadUrl, string method,
            IReadOnlyDictionary<string, string> headers, int expiresIn)
        {
            AttachmentId = attachmentId;
            UploadUrl = uploadUrl;
            Method = string.IsNullOrEmpty(method) ? "PUT" : method;
            Headers = headers ?? new Dictionary<string, string>();
            ExpiresIn = expiresIn;
        }
    }

    /// <summary>
    /// Что игра знает об устройстве. Только для диагностики: на идентификацию игрока
    /// не влияет, сервер режет значения по длине.
    /// </summary>
    public sealed class SupportGateClientInfo
    {
        public string DeviceModel { get; set; }
        public string Screen { get; set; }
        public string UserAgent { get; set; }
    }

    public sealed class SupportGateTicketDraft
    {
        public string Category { get; set; }
        public string Subcategory { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }

        /// <summary>Нужен, только если почта не пришла в токене.</summary>
        public string Email { get; set; }

        public Dictionary<string, string> Extra { get; } = new Dictionary<string, string>();
        public List<string> AttachmentIds { get; } = new List<string>();
        public SupportGateClientInfo Client { get; set; }
    }

    public sealed class SupportGateTicket
    {
        public string TicketId { get; }

        /// <summary>Ключ задачи в JSM. null, пока доставка не прошла.</summary>
        public string IssueKey { get; }

        public string Status { get; }
        public string CreatedAt { get; }

        public SupportGateTicket(string ticketId, string issueKey, string status, string createdAt)
        {
            TicketId = ticketId;
            IssueKey = issueKey;
            Status = status;
            CreatedAt = createdAt;
        }

        /// <summary>Что показать игроку как номер обращения.</summary>
        public string DisplayId
        {
            get { return string.IsNullOrEmpty(IssueKey) ? TicketId : IssueKey; }
        }
    }
}
