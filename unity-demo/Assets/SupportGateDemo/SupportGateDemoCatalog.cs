using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hooligapps.SupportGate.Demo
{
    /// <summary>
    /// Фиктивные ответы шлюза. Форма и строки повторяют реальный /v1/form: строки
    /// ui.* взяты из каталогов locales/ru.json и locales/ar.json, категории — из
    /// справочника по умолчанию.
    /// </summary>
    public static class SupportGateDemoCatalog
    {
        private static readonly Dictionary<string, string> Russian = new Dictionary<string, string>
        {
            { "ui.title", "Обращение в поддержку" },
            { "ui.category", "Категория" },
            { "ui.subcategory", "Тема" },
            { "ui.choose", "Выберите…" },
            { "ui.email", "Email" },
            { "ui.email_hint", "На этот адрес придёт ответ." },
            { "ui.subject", "Тема обращения" },
            { "ui.description", "Опишите проблему" },
            { "ui.attachments", "Вложения" },
            { "ui.attachments_hint", "До {max, plural, one {# файла} few {# файлов} many {# файлов} other {# файла}}, по {mb} МБ." },
            { "ui.add_files", "Добавить файлы" },
            { "ui.unsupported_type", "Такой тип файла не поддерживается" },
            { "ui.too_many_files", "Не больше {max, plural, one {# файла} few {# файлов} many {# файлов} other {# файла}}" },
            { "ui.remove", "Удалить" },
            { "ui.uploading", "Загружается…" },
            { "ui.submit", "Отправить" },
            { "ui.submitting", "Отправляем…" },
            { "ui.cancel", "Отмена" },
            { "ui.close", "Закрыть" },
            { "ui.required", "Обязательное поле" },
            { "ui.invalid_email", "Введите корректный адрес почты" },
            { "ui.loading", "Загрузка…" },
            { "ui.load_failed", "Не удалось загрузить форму." },
            { "ui.retry", "Повторить" },
            { "ui.sent_title", "Обращение отправлено" },
            { "ui.sent_text", "Номер обращения — {id}. Ответ придёт на почту." },
            { "ui.failed_title", "Не удалось отправить обращение" },
            { "ui.rate_limited", "Слишком много обращений. Попробуйте позже." },
            { "ui.session_expired", "Сессия истекла. Откройте окно заново." },
            { "ui.offline", "Нет связи с сервером. Проверьте соединение и попробуйте снова." },
            { "category.gameplay", "Геймплей" },
            { "category.payment", "Платежи" },
            { "category.account", "Аккаунт" },
            { "subcategory.gameplay.progress", "Прогресс" },
            { "subcategory.gameplay.rewards", "Награды" },
            { "subcategory.payment.missing_purchase", "Не получил покупку" },
            { "subcategory.payment.refund", "Возврат" },
            { "subcategory.account.login", "Вход" },
            { "field.payment_method", "Способ оплаты" },
            { "field.purchase_date", "Дата покупки" },
            { "field.transaction_id", "ID транзакции" },
            { "option.payment_method.apple", "Apple" },
            { "option.payment_method.google", "Google" },
            { "option.payment_method.card", "Банковская карта" },
            { "option.payment_method.other", "Другое" }
        };

        private static readonly Dictionary<string, string> Arabic = new Dictionary<string, string>
        {
            { "ui.title", "الاتصال بالدعم" },
            { "ui.category", "الفئة" },
            { "ui.subcategory", "الموضوع" },
            { "ui.choose", "اختر…" },
            { "ui.email", "البريد الإلكتروني" },
            { "ui.email_hint", "سنرد على هذا العنوان." },
            { "ui.subject", "الموضوع" },
            { "ui.description", "صف المشكلة" },
            { "ui.attachments", "المرفقات" },
            { "ui.attachments_hint", "حتى {max, plural, zero {# ملف} one {ملف واحد} two {ملفان} few {# ملفات} many {# ملفًا} other {# ملف}}، بحد أقصى {mb} ميغابايت لكل ملف." },
            { "ui.add_files", "إضافة ملفات" },
            { "ui.unsupported_type", "نوع الملف هذا غير مدعوم" },
            { "ui.too_many_files", "لا يمكن إضافة أكثر من {max, plural, zero {# ملف} one {ملف واحد} two {ملفين} few {# ملفات} many {# ملفًا} other {# ملف}}" },
            { "ui.remove", "إزالة" },
            { "ui.uploading", "جارٍ الرفع…" },
            { "ui.submit", "إرسال" },
            { "ui.submitting", "جارٍ الإرسال…" },
            { "ui.cancel", "إلغاء" },
            { "ui.close", "إغلاق" },
            { "ui.required", "حقل مطلوب" },
            { "ui.invalid_email", "أدخل عنوان بريد إلكتروني صالحًا" },
            { "ui.loading", "جارٍ التحميل…" },
            { "ui.load_failed", "تعذر تحميل النموذج." },
            { "ui.retry", "إعادة المحاولة" },
            { "ui.sent_title", "تم إرسال الطلب" },
            { "ui.sent_text", "رقم طلبك هو {id}. سنرد عليك عبر البريد الإلكتروني." },
            { "ui.failed_title", "تعذر إرسال الطلب" },
            { "ui.rate_limited", "طلبات كثيرة جدًا. يُرجى المحاولة مرة أخرى لاحقًا." },
            { "ui.session_expired", "انتهت صلاحية الجلسة. أعد فتح النافذة." },
            { "ui.offline", "لا يوجد اتصال بالخادم. تحقق من الشبكة وحاول مرة أخرى." },
            { "category.gameplay", "أسلوب اللعب" },
            { "category.payment", "الدفع" },
            { "category.account", "الحساب" },
            { "subcategory.gameplay.progress", "التقدم" },
            { "subcategory.gameplay.rewards", "المكافآت" },
            { "subcategory.payment.missing_purchase", "عملية شراء مفقودة" },
            { "subcategory.payment.refund", "استرداد الأموال" },
            { "subcategory.account.login", "تسجيل الدخول" },
            { "field.payment_method", "طريقة الدفع" },
            { "field.purchase_date", "تاريخ الشراء" },
            { "field.transaction_id", "معرّف المعاملة" },
            { "option.payment_method.apple", "Apple" },
            { "option.payment_method.google", "Google" },
            { "option.payment_method.card", "بطاقة ائتمان" },
            { "option.payment_method.other", "أخرى" }
        };

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "category.gameplay", "Gameplay" },
            { "category.payment", "Payment" },
            { "category.account", "Account" },
            { "subcategory.gameplay.progress", "Progress" },
            { "subcategory.gameplay.rewards", "Rewards" },
            { "subcategory.payment.missing_purchase", "Missing Purchase" },
            { "subcategory.payment.refund", "Refund" },
            { "subcategory.account.login", "Login" },
            { "field.payment_method", "Payment Method" },
            { "field.purchase_date", "Purchase Date" },
            { "field.transaction_id", "Transaction ID" },
            { "option.payment_method.apple", "Apple" },
            { "option.payment_method.google", "Google" },
            { "option.payment_method.card", "Credit Card" },
            { "option.payment_method.other", "Other" }
        };

        public static string FormBody(SupportGateDemoFormScenario scenario)
        {
            string locale;
            Dictionary<string, string> catalog;
            switch (scenario)
            {
                case SupportGateDemoFormScenario.Russian:
                    locale = "ru";
                    catalog = Russian;
                    break;
                case SupportGateDemoFormScenario.Arabic:
                    locale = "ar";
                    catalog = Arabic;
                    break;
                default:
                    locale = "en";
                    catalog = English;
                    break;
            }

            var strings = new JObject();
            foreach (var pair in catalog)
            {
                if (pair.Key.StartsWith("ui."))
                {
                    strings[pair.Key] = pair.Value;
                }
            }

            var locked = scenario == SupportGateDemoFormScenario.LockedEmail;

            var root = new JObject
            {
                ["game"] = "demo-game",
                ["locale"] = locale,
                ["direction"] = locale == "ar" ? "rtl" : "ltr",
                ["strings"] = strings,
                ["prefill"] = new JObject
                {
                    ["email"] = locked ? "player@example.com" : null,
                    ["email_locked"] = locked
                },
                ["categories"] = new JArray
                {
                    Category(catalog, "gameplay", new[] { "progress", "rewards" }),
                    Category(catalog, "payment", new[] { "missing_purchase", "refund" },
                        new JArray
                        {
                            Field(catalog, "payment_method", "select", true,
                                new[] { "apple", "google", "card", "other" }),
                            Field(catalog, "purchase_date", "date", false),
                            Field(catalog, "transaction_id", "text", false, null, 64)
                        }),
                    Category(catalog, "account", new[] { "login" })
                },
                ["attachments"] = new JObject
                {
                    ["max_files"] = 3,
                    ["max_file_size"] = 5 * 1024 * 1024,
                    ["allowed_types"] = new JArray("image/png", "image/jpeg", "text/plain")
                }
            };

            return root.ToString(Formatting.None);
        }

        public static string PresignBody(string attachmentId, byte[] requestBody)
        {
            var contentType = "application/octet-stream";
            if (requestBody != null)
            {
                var request = JObject.Parse(System.Text.Encoding.UTF8.GetString(requestBody));
                contentType = (string)request["content_type"] ?? contentType;
            }

            return new JObject
            {
                ["attachment_id"] = attachmentId,
                ["upload_url"] = "https://storage.demo.local/" + attachmentId + "?signature=demo",
                ["method"] = "PUT",
                ["headers"] = new JObject { ["Content-Type"] = contentType },
                ["expires_in"] = 600
            }.ToString(Formatting.None);
        }

        public static string TicketBody(int number, bool queued)
        {
            return new JObject
            {
                ["ticket_id"] = "tkt-demo-" + number,
                ["issue_key"] = queued ? null : "SUP-" + (100 + number),
                ["status"] = queued ? "queued" : "delivered",
                ["created_at"] = System.DateTime.UtcNow.ToString("o")
            }.ToString(Formatting.None);
        }

        private static JObject Category(Dictionary<string, string> catalog, string id, string[] subcategories,
            JArray extraFields = null)
        {
            var subs = new JArray();
            foreach (var sub in subcategories)
            {
                subs.Add(new JObject { ["id"] = sub, ["label"] = catalog["subcategory." + id + "." + sub] });
            }

            var category = new JObject
            {
                ["id"] = id,
                ["label"] = catalog["category." + id],
                ["subcategories"] = subs
            };

            if (extraFields != null)
            {
                category["extra_fields"] = extraFields;
            }

            return category;
        }

        private static JObject Field(Dictionary<string, string> catalog, string id, string type, bool required,
            string[] options = null, int maxLength = 0)
        {
            var field = new JObject
            {
                ["id"] = id,
                ["label"] = catalog["field." + id],
                ["type"] = type,
                ["required"] = required
            };

            if (options != null)
            {
                var items = new JArray();
                foreach (var option in options)
                {
                    items.Add(new JObject { ["id"] = option, ["label"] = catalog["option." + id + "." + option] });
                }

                field["options"] = items;
            }

            if (maxLength > 0)
            {
                field["max_length"] = maxLength;
            }

            return field;
        }
    }
}
