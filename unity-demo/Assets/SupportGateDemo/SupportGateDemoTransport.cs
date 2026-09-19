using System;
using System.Collections;
using UnityEngine;

namespace Hooligapps.SupportGate.Demo
{
    /// <summary>Что отвечать на GET /v1/form.</summary>
    public enum SupportGateDemoFormScenario
    {
        English = 0,
        Russian = 1,
        Arabic = 2,
        LockedEmail = 3,
        HttpError = 4,
        Unauthorized = 5,
        NetworkError = 6
    }

    /// <summary>Что отвечать на POST /v1/tickets.</summary>
    public enum SupportGateDemoSubmitScenario
    {
        Accepted = 0,
        Queued = 1,
        ValidationError = 2,
        RateLimited = 3,
        HttpError = 4,
        NetworkError = 5
    }

    /// <summary>
    /// Демо-сервер: отвечает фиктивными телами вместо сети, включая «хранилище»
    /// для PUT по подписанной ссылке. Реализует тот же ISupportGateTransport, что и
    /// боевой транспорт, поэтому переключение на настоящий шлюз — замена одного
    /// объекта, без правок в UI.
    /// </summary>
    public sealed class SupportGateDemoTransport : ISupportGateTransport
    {
        public SupportGateDemoFormScenario Form { get; set; } = SupportGateDemoFormScenario.English;
        public SupportGateDemoSubmitScenario Submit { get; set; } = SupportGateDemoSubmitScenario.Accepted;

        /// <summary>Искусственная задержка ответа: состояние загрузки должно быть видно.</summary>
        public float DelaySeconds { get; set; } = 0.6f;

        /// <summary>Хранилище отвергает файл: строка вложения должна исчезнуть.</summary>
        public bool StorageRejects { get; set; }

        /// <summary>Куда писать запросы и ответы демо-лога.</summary>
        public Action<string> Log { get; set; }

        private int _tickets;
        private int _attachments;

        public IEnumerator Send(SupportGateHttpRequest request, Action<SupportGateHttpResponse> onDone)
        {
            Write("→ " + request.Method + " " + request.Url + Describe(request));

            if (DelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(DelaySeconds);
            }

            var response = Respond(request);

            Write(response.HasTransportError
                ? "← transport error: " + response.TransportError
                : "← " + response.StatusCode + " " + Short(response.Body));

            onDone?.Invoke(response);
        }

        private SupportGateHttpResponse Respond(SupportGateHttpRequest request)
        {
            if (request.Url.StartsWith("https://storage.demo.local/", StringComparison.Ordinal))
            {
                return StorageRejects
                    ? new SupportGateHttpResponse(403, "<Error>SignatureDoesNotMatch</Error>")
                    : new SupportGateHttpResponse(200, string.Empty);
            }

            if (request.Url.EndsWith("/v1/form", StringComparison.Ordinal))
            {
                return RespondForm();
            }

            if (request.Url.EndsWith("/v1/attachments/presign", StringComparison.Ordinal))
            {
                _attachments++;
                return Ok(SupportGateDemoCatalog.PresignBody("att-" + _attachments, request.Body));
            }

            if (request.Url.EndsWith("/v1/tickets", StringComparison.Ordinal))
            {
                return RespondSubmit();
            }

            return new SupportGateHttpResponse(404, @"{""error"":""not_found"",""message"":""Unknown route""}");
        }

        private SupportGateHttpResponse RespondForm()
        {
            switch (Form)
            {
                case SupportGateDemoFormScenario.HttpError:
                    return new SupportGateHttpResponse(500, "Internal Server Error");
                case SupportGateDemoFormScenario.Unauthorized:
                    return new SupportGateHttpResponse(401,
                        @"{""error"":""unauthorized"",""message"":""Session token expired""}");
                case SupportGateDemoFormScenario.NetworkError:
                    return new SupportGateHttpResponse(0, null, "Cannot connect to destination host");
                default:
                    return Ok(SupportGateDemoCatalog.FormBody(Form));
            }
        }

        private SupportGateHttpResponse RespondSubmit()
        {
            switch (Submit)
            {
                case SupportGateDemoSubmitScenario.ValidationError:
                    return new SupportGateHttpResponse(422,
                        @"{""error"":""validation_error"",""message"":""Check the highlighted fields"",
                          ""fields"":{""subject"":""Too short"",""extra.transaction_id"":""Unknown transaction""}}");
                case SupportGateDemoSubmitScenario.RateLimited:
                    return new SupportGateHttpResponse(429,
                        @"{""error"":""rate_limited"",""message"":""Too many requests""}");
                case SupportGateDemoSubmitScenario.HttpError:
                    return new SupportGateHttpResponse(503,
                        @"{""error"":""unavailable"",""message"":""Try again later""}");
                case SupportGateDemoSubmitScenario.NetworkError:
                    return new SupportGateHttpResponse(0, null, "Request timeout");
                default:
                    _tickets++;
                    return Ok(SupportGateDemoCatalog.TicketBody(_tickets,
                        Submit == SupportGateDemoSubmitScenario.Queued));
            }
        }

        private static SupportGateHttpResponse Ok(string body)
        {
            return new SupportGateHttpResponse(200, body);
        }

        private static string Describe(SupportGateHttpRequest request)
        {
            if (request.Body == null)
            {
                return string.Empty;
            }

            return request.ContentType == "application/json"
                ? " " + Short(System.Text.Encoding.UTF8.GetString(request.Body))
                : " [" + request.Body.Length + " bytes " + request.ContentType + "]";
        }

        private static string Short(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return string.Empty;
            }

            var flat = body.Replace("\n", " ").Replace("\r", string.Empty);
            return flat.Length > 160 ? flat.Substring(0, 160) + "…" : flat;
        }

        private void Write(string message)
        {
            Log?.Invoke(message);
        }
    }
}
