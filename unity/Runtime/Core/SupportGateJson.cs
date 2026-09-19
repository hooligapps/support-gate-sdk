using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hooligapps.SupportGate
{
    /// <summary>
    /// Разбор ответов и сборка тел запросов. Ручной обход JObject, а не атрибуты:
    /// поля справочника приходят от сервера и могут пополняться, ломать разбор
    /// незнакомый ключ не должен.
    /// </summary>
    public static class SupportGateJson
    {
        public static SupportGateFormConfig ReadFormConfig(string body)
        {
            var root = JObject.Parse(body);
            var prefill = root["prefill"] as JObject;

            var categories = new List<SupportGateCategory>();
            foreach (var item in Items(root["categories"]))
            {
                categories.Add(ReadCategory((JObject)item));
            }

            var attachments = root["attachments"] as JObject;

            var strings = new Dictionary<string, string>();
            if (root["strings"] is JObject catalog)
            {
                foreach (var pair in catalog)
                {
                    strings[pair.Key] = Text(pair.Value);
                }
            }

            return new SupportGateFormConfig(
                Text(root["game"]),
                Text(root["locale"]),
                Text(prefill?["email"]),
                Flag(prefill?["email_locked"]),
                categories,
                new SupportGateAttachmentLimits(
                    (int)Number(attachments?["max_files"], 1),
                    Number(attachments?["max_file_size"], 0),
                    Strings(attachments?["allowed_types"])),
                strings,
                Text(root["direction"]));
        }

        public static SupportGateUploadTicket ReadUploadTicket(string body)
        {
            var root = JObject.Parse(body);
            var headers = new Dictionary<string, string>();

            if (root["headers"] is JObject raw)
            {
                foreach (var header in raw)
                {
                    headers[header.Key] = Text(header.Value);
                }
            }

            return new SupportGateUploadTicket(
                Text(root["attachment_id"]),
                Text(root["upload_url"]),
                Text(root["method"]),
                headers,
                (int)Number(root["expires_in"], 0));
        }

        public static SupportGateTicket ReadTicket(string body)
        {
            var root = JObject.Parse(body);

            return new SupportGateTicket(
                Text(root["ticket_id"]),
                Text(root["issue_key"]),
                Text(root["status"]),
                Text(root["created_at"]));
        }

        public static string BuildPresignBody(string fileName, string contentType, long size)
        {
            return JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                { "filename", fileName },
                { "content_type", contentType },
                { "size", size }
            });
        }

        public static string BuildTicketBody(SupportGateTicketDraft draft)
        {
            var payload = new Dictionary<string, object>
            {
                { "category", draft.Category },
                { "subcategory", draft.Subcategory },
                { "subject", draft.Subject },
                { "description", draft.Description }
            };

            if (!string.IsNullOrEmpty(draft.Email))
            {
                payload["email"] = draft.Email;
            }

            if (draft.Extra.Count > 0)
            {
                payload["extra"] = new Dictionary<string, string>(draft.Extra);
            }

            if (draft.AttachmentIds.Count > 0)
            {
                payload["attachment_ids"] = new List<string>(draft.AttachmentIds);
            }

            if (draft.Client != null)
            {
                var client = new Dictionary<string, object>();
                Put(client, "device_model", draft.Client.DeviceModel);
                Put(client, "screen", draft.Client.Screen);
                Put(client, "user_agent", draft.Client.UserAgent);
                payload["client"] = client;
            }

            return JsonConvert.SerializeObject(payload);
        }

        /// <summary>
        /// Ошибка сервиса приходит как {error, message, fields}, но FastAPI заворачивает
        /// её в detail — разбираем оба вида.
        /// </summary>
        public static SupportGateError ReadError(long status, string body)
        {
            var message = "HTTP " + status;
            string code = null;
            var fields = new Dictionary<string, string>();

            try
            {
                var root = JObject.Parse(body ?? string.Empty);
                var detail = root["detail"] as JObject ?? root;

                code = Text(detail["error"]);
                var reported = Text(detail["message"]);
                if (!string.IsNullOrEmpty(reported))
                {
                    message = reported;
                }

                if (detail["fields"] is JObject raw)
                {
                    foreach (var field in raw)
                    {
                        fields[field.Key] = Text(field.Value);
                    }
                }
            }
            catch (JsonException)
            {
                // Тело не разобралось — оставляем статус и сырой ответ.
            }

            return new SupportGateError(SupportGateErrorKind.Http, message, status, code, fields, body);
        }

        private static SupportGateCategory ReadCategory(JObject node)
        {
            var subcategories = new List<SupportGateOption>();
            foreach (var item in Items(node["subcategories"]))
            {
                subcategories.Add(new SupportGateOption(Text(item["id"]), Text(item["label"])));
            }

            var extra = new List<SupportGateExtraField>();
            foreach (var item in Items(node["extra_fields"]))
            {
                extra.Add(ReadExtraField((JObject)item));
            }

            return new SupportGateCategory(Text(node["id"]), Text(node["label"]), subcategories, extra);
        }

        private static SupportGateExtraField ReadExtraField(JObject node)
        {
            var options = new List<SupportGateOption>();
            foreach (var item in Items(node["options"]))
            {
                options.Add(new SupportGateOption(Text(item["id"]), Text(item["label"])));
            }

            return new SupportGateExtraField(
                Text(node["id"]),
                Text(node["label"]),
                ReadFieldType(Text(node["type"])),
                Flag(node["required"]),
                options,
                (int)Number(node["max_length"], 0));
        }

        private static SupportGateFieldType ReadFieldType(string value)
        {
            switch (value)
            {
                case "select":
                    return SupportGateFieldType.Select;
                case "date":
                    return SupportGateFieldType.Date;
                default:
                    return SupportGateFieldType.Text;
            }
        }

        private static IEnumerable<JToken> Items(JToken node)
        {
            return node as JArray ?? (IEnumerable<JToken>)new JToken[0];
        }

        private static string Text(JToken node)
        {
            if (node == null || node.Type == JTokenType.Null)
            {
                return null;
            }

            return node.Value<string>();
        }

        private static bool Flag(JToken node)
        {
            return node != null && node.Type != JTokenType.Null && node.Value<bool>();
        }

        private static long Number(JToken node, long fallback)
        {
            if (node == null || node.Type == JTokenType.Null)
            {
                return fallback;
            }

            return long.Parse(node.ToString(), CultureInfo.InvariantCulture);
        }

        private static IReadOnlyList<string> Strings(JToken node)
        {
            var values = new List<string>();
            foreach (var item in Items(node))
            {
                values.Add(Text(item));
            }

            return values;
        }

        private static void Put(IDictionary<string, object> target, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                target[key] = value;
            }
        }
    }
}
