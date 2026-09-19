using System;
using System.Collections;
using System.Collections.Generic;

namespace Hooligapps.SupportGate.Tests
{
    /// <summary>Транспорт-заглушка: тесты контракта не ходят в сеть.</summary>
    public sealed class FakeTransport : ISupportGateTransport
    {
        private readonly Func<SupportGateHttpRequest, SupportGateHttpResponse> _handler;

        public FakeTransport(SupportGateHttpResponse response)
            : this(request => response)
        {
        }

        public FakeTransport(Func<SupportGateHttpRequest, SupportGateHttpResponse> handler)
        {
            _handler = handler;
        }

        public List<SupportGateHttpRequest> Requests { get; } = new List<SupportGateHttpRequest>();

        /// <summary>Сколько кадров держать запрос незавершённым.</summary>
        public int DelayFrames { get; set; }

        public SupportGateHttpRequest Last
        {
            get { return Requests.Count == 0 ? null : Requests[Requests.Count - 1]; }
        }

        public int CountOf(string path)
        {
            var count = 0;
            foreach (var request in Requests)
            {
                if (request.Url.EndsWith(path, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        public IEnumerator Send(SupportGateHttpRequest request, Action<SupportGateHttpResponse> onDone)
        {
            Requests.Add(request);

            for (var i = 0; i < DelayFrames; i++)
            {
                yield return null;
            }

            onDone?.Invoke(_handler(request));
        }
    }

    public static class Json
    {
        public const string Form = @"{
            ""game"": ""demo"",
            ""locale"": ""ru"",
            ""direction"": ""ltr"",
            ""strings"": {""ui.title"": ""Обращение в поддержку"", ""ui.submit"": ""Отправить"",
                          ""ui.sent_text"": ""Номер обращения — {id}."",
                          ""ui.too_many_files"": ""Не больше {max, plural, one {# файла} few {# файлов} many {# файлов} other {# файла}}"",
                          ""ui.cancel"": """"},
            ""prefill"": {""email"": null, ""email_locked"": false},
            ""categories"": [
                {""id"": ""gameplay"", ""label"": ""Gameplay"",
                 ""subcategories"": [{""id"": ""progress"", ""label"": ""Progress""}]},
                {""id"": ""payment"", ""label"": ""Payment"",
                 ""subcategories"": [{""id"": ""refund"", ""label"": ""Refund""}],
                 ""extra_fields"": [
                    {""id"": ""payment_method"", ""type"": ""select"", ""label"": ""Payment Method"",
                     ""required"": true,
                     ""options"": [{""id"": ""apple"", ""label"": ""Apple""}]}]}
            ],
            ""attachments"": {""max_files"": 2, ""max_file_size"": 5242880,
                              ""allowed_types"": [""image/png""]}
        }";

        public const string Presign = @"{
            ""attachment_id"": ""att-1"",
            ""upload_url"": ""https://storage.example/att-1"",
            ""method"": ""PUT"",
            ""headers"": {""Content-Type"": ""image/png""},
            ""expires_in"": 600
        }";

        public const string Ticket = @"{
            ""ticket_id"": ""tkt-1"",
            ""issue_key"": ""SUP-1"",
            ""status"": ""queued"",
            ""created_at"": ""2026-01-01T00:00:00Z""
        }";
    }

    public static class Pump
    {
        /// <summary>Прогоняет корутину целиком, разворачивая вложенные IEnumerator.</summary>
        public static void RunToEnd(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);

            while (stack.Count > 0)
            {
                var top = stack.Peek();

                if (!top.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                if (top.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                }
            }
        }
    }
}
