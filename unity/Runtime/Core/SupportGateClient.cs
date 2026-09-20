using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Hooligapps.SupportGate
{
    /// <summary>
    /// Клиент Support Gateway. Не MonoBehaviour: методы возвращают IEnumerator,
    /// корутину запускает вызывающая сторона.
    ///
    /// Чего клиент не делает намеренно: не ретраит отправку при сетевых ошибках
    /// (повтор с тем же ключом идемпотентности — решение UI) и не рисует форму.
    /// Единственный повтор — после 401: истёкшая сессия обновляется, и запрос
    /// уходит снова с теми же заголовками.
    /// </summary>
    public sealed class SupportGateClient
    {
        private const string FormPath = "/v1/form";
        private const string PresignPath = "/v1/attachments/presign";
        private const string TicketsPath = "/v1/tickets";
        private const string RefreshPath = "/v1/session/refresh";

        private readonly string _endpoint;
        private readonly ISupportGateTransport _transport;

        private SupportGateSession _session;
        private SupportGateFormConfig _config;

        public SupportGateClient(string endpoint, SupportGateSession session,
            ISupportGateTransport transport = null)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            _endpoint = SupportGateUrls.NormalizeEndpoint(endpoint);
            _session = session;
            _transport = transport ?? new SupportGateUnityWebRequestTransport();
        }

        public string Endpoint
        {
            get { return _endpoint; }
        }

        public SupportGateSession Session
        {
            get { return _session; }
        }

        /// <summary>Справочник, если он уже загружен. Иначе null.</summary>
        public SupportGateFormConfig Config
        {
            get { return _config; }
        }

        /// <summary>Смена игрока: справочник сбрасывается, он зависит от контекста токена.</summary>
        public void SetSession(SupportGateSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _config = null;
        }

        /// <summary>
        /// Справочник категорий, лимиты и prefill. Берётся один раз на сессию:
        /// повторный вызов отдаёт готовый ответ без запроса.
        /// </summary>
        public IEnumerator LoadForm(Action<SupportGateResult<SupportGateFormConfig>> onDone)
        {
            if (_config != null)
            {
                Complete(onDone, SupportGateResult<SupportGateFormConfig>.Ok(_config));
                yield break;
            }

            var routine = Call("GET", _endpoint + FormPath, null, null, onDone,
                body =>
                {
                    _config = SupportGateJson.ReadFormConfig(body);
                    return _config;
                });

            while (routine.MoveNext())
            {
                yield return routine.Current;
            }
        }

        /// <summary>
        /// Кладёт файл в хранилище и возвращает вложение для <see cref="SubmitTicket"/>.
        /// Сервис файл не проксирует: байты уходят прямо в хранилище по подписанной
        /// ссылке. Лимиты проверяются до запроса — сервер откажет теми же числами.
        /// </summary>
        public IEnumerator UploadAttachment(string fileName, string contentType, byte[] data,
            Action<SupportGateResult<SupportGateAttachment>> onDone)
        {
            if (string.IsNullOrEmpty(fileName) || data == null || data.Length == 0)
            {
                Complete(onDone, SupportGateResult<SupportGateAttachment>.Fail(
                    SupportGateErrorKind.InvalidRequest, "файл пустой"));
                yield break;
            }

            var limits = _config != null ? _config.Attachments : null;
            if (limits != null && !limits.Accepts(contentType))
            {
                Complete(onDone, SupportGateResult<SupportGateAttachment>.Fail(new SupportGateError(
                    SupportGateErrorKind.InvalidRequest,
                    "тип " + contentType + " не поддерживается", 0, "unsupported_type")));
                yield break;
            }

            if (limits != null && limits.MaxFileSize > 0 && data.LongLength > limits.MaxFileSize)
            {
                Complete(onDone, SupportGateResult<SupportGateAttachment>.Fail(new SupportGateError(
                    SupportGateErrorKind.InvalidRequest,
                    "файл больше " + limits.MaxFileSize / 1024 / 1024 + " МБ", 0, "file_too_large")));
                yield break;
            }

            SupportGateUploadTicket ticket = null;
            SupportGateError error = null;

            var presign = Call("POST", _endpoint + PresignPath,
                SupportGateJson.BuildPresignBody(fileName, contentType, data.LongLength), null,
                (SupportGateResult<SupportGateUploadTicket> result) =>
                {
                    ticket = result.Value;
                    error = result.Error;
                },
                SupportGateJson.ReadUploadTicket);

            while (presign.MoveNext())
            {
                yield return presign.Current;
            }

            if (ticket == null)
            {
                Complete(onDone, SupportGateResult<SupportGateAttachment>.Fail(error));
                yield break;
            }

            var headers = new Dictionary<string, string>();
            string uploadContentType = null;
            foreach (var header in ticket.Headers)
            {
                // Content-Type подписан вместе со ссылкой: подменять его нельзя.
                if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    uploadContentType = header.Value;
                    continue;
                }

                headers[header.Key] = header.Value;
            }

            SupportGateHttpResponse response = null;
            var upload = _transport.Send(
                new SupportGateHttpRequest(ticket.Method, ticket.UploadUrl, data,
                    uploadContentType ?? contentType, headers),
                r => response = r);

            while (upload.MoveNext())
            {
                yield return upload.Current;
            }

            if (response == null || response.HasTransportError)
            {
                Complete(onDone, SupportGateResult<SupportGateAttachment>.Fail(new SupportGateError(
                    SupportGateErrorKind.Network,
                    response == null ? "Ответ не получен" : response.TransportError)));
                yield break;
            }

            if (response.StatusCode >= 400)
            {
                Complete(onDone, SupportGateResult<SupportGateAttachment>.Fail(new SupportGateError(
                    SupportGateErrorKind.Http, "хранилище отклонило файл: HTTP " + response.StatusCode,
                    response.StatusCode, "upload_failed", null, response.Body)));
                yield break;
            }

            Complete(onDone, SupportGateResult<SupportGateAttachment>.Ok(new SupportGateAttachment(
                ticket.AttachmentId, fileName, contentType, data.LongLength)));
        }

        /// <summary>
        /// Отправляет обращение. <paramref name="idempotencyKey"/> живёт на одну
        /// попытку: повтор после сетевой ошибки с тем же ключом вернёт то же
        /// обращение, а не создаст второе.
        /// </summary>
        public IEnumerator SubmitTicket(SupportGateTicketDraft draft, string idempotencyKey,
            Action<SupportGateResult<SupportGateTicket>> onDone)
        {
            if (draft == null)
            {
                Complete(onDone, SupportGateResult<SupportGateTicket>.Fail(
                    SupportGateErrorKind.InvalidRequest, "draft обязателен"));
                yield break;
            }

            if (draft.Client == null)
            {
                draft.Client = CollectClientInfo();
            }

            var headers = new Dictionary<string, string>
            {
                { "Idempotency-Key", string.IsNullOrEmpty(idempotencyKey) ? NewIdempotencyKey() : idempotencyKey }
            };

            var routine = Call("POST", _endpoint + TicketsPath, SupportGateJson.BuildTicketBody(draft),
                headers, onDone, SupportGateJson.ReadTicket);

            while (routine.MoveNext())
            {
                yield return routine.Current;
            }
        }

        public static string NewIdempotencyKey()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>Данные устройства для диагностики; на идентификацию игрока не влияют.</summary>
        public static SupportGateClientInfo CollectClientInfo()
        {
            return new SupportGateClientInfo
            {
                DeviceModel = SystemInfo.deviceModel,
                Screen = UnityEngine.Screen.width + "x" + UnityEngine.Screen.height,
                UserAgent = "Unity/" + Application.unityVersion + " " + Application.platform + " " +
                            SystemInfo.operatingSystem
            };
        }

        private IEnumerator Call<T>(string method, string url, string jsonBody,
            IDictionary<string, string> headers, Action<SupportGateResult<T>> onDone, Func<string, T> read)
        {
            string token = null;
            var resolve = _session.Resolve(issued => token = issued);
            while (resolve.MoveNext())
            {
                yield return resolve.Current;
            }

            if (string.IsNullOrEmpty(token))
            {
                Complete(onDone, SupportGateResult<T>.Fail(
                    SupportGateErrorKind.InvalidRequest, "session token пустой"));
                yield break;
            }

            var body = jsonBody == null ? null : Encoding.UTF8.GetBytes(jsonBody);
            SupportGateHttpResponse response = null;
            var send = Send(method, url, body, jsonBody, headers, token, r => response = r);
            while (send.MoveNext())
            {
                yield return send.Current;
            }

            if (response != null && !response.HasTransportError && response.StatusCode == 401)
            {
                var renewed = false;
                var renew = Renew(token, ok => renewed = ok);
                while (renew.MoveNext())
                {
                    yield return renew.Current;
                }

                if (renewed)
                {
                    // Те же заголовки и ключ идемпотентности — дубля не будет.
                    SupportGateHttpResponse retried = null;
                    send = Send(method, url, body, jsonBody, headers, _session.Token, r => retried = r);
                    while (send.MoveNext())
                    {
                        yield return send.Current;
                    }

                    response = retried;
                }
            }

            Complete(onDone, Interpret(response, read));
        }

        /// <summary>
        /// Сессия истекла: сначала свежий токен у игры, а если она отдаёт тот же
        /// (или токен задан строкой) — продление через шлюз, пока не вышел его срок.
        /// Ошибка продления не важна: наружу уйдёт исходный 401.
        /// </summary>
        private IEnumerator Renew(string stale, Action<bool> onDone)
        {
            var fromGame = false;
            var ask = _session.Renew(stale, ok => fromGame = ok);
            while (ask.MoveNext())
            {
                yield return ask.Current;
            }

            if (fromGame)
            {
                onDone(true);
                yield break;
            }

            SupportGateHttpResponse response = null;
            var send = Send("POST", _endpoint + RefreshPath, null, null, null, stale, r => response = r);
            while (send.MoveNext())
            {
                yield return send.Current;
            }

            var result = Interpret(response, SupportGateJson.ReadRefreshedToken);
            if (!result.Success || string.IsNullOrEmpty(result.Value))
            {
                Debug.Log("[SupportGate] шлюз не продлил сессию: " + (result.Error?.Message ?? "пустой токен"));
                onDone(false);
                yield break;
            }

            _session.SetToken(result.Value);
            onDone(true);
        }

        private IEnumerator Send(string method, string url, byte[] body, string jsonBody,
            IDictionary<string, string> headers, string token, Action<SupportGateHttpResponse> onDone)
        {
            var requestHeaders = new Dictionary<string, string>
            {
                { "Accept", "application/json" },
                { "Authorization", "Bearer " + token }
            };

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    requestHeaders[header.Key] = header.Value;
                }
            }

            return _transport.Send(
                new SupportGateHttpRequest(method, url, body, jsonBody == null ? null : "application/json",
                    requestHeaders),
                onDone);
        }

        private static SupportGateResult<T> Interpret<T>(SupportGateHttpResponse response, Func<string, T> read)
        {
            if (response == null)
            {
                return SupportGateResult<T>.Fail(SupportGateErrorKind.Network, "Ответ не получен");
            }

            if (response.HasTransportError)
            {
                return SupportGateResult<T>.Fail(new SupportGateError(
                    SupportGateErrorKind.Network, response.TransportError, response.StatusCode,
                    null, null, response.Body));
            }

            if (response.StatusCode >= 400)
            {
                return SupportGateResult<T>.Fail(SupportGateJson.ReadError(response.StatusCode, response.Body));
            }

            try
            {
                return SupportGateResult<T>.Ok(read(response.Body));
            }
            catch (Exception e)
            {
                return SupportGateResult<T>.Fail(new SupportGateError(
                    SupportGateErrorKind.Parse, e.Message, response.StatusCode, null, null, response.Body));
            }
        }

        private static void Complete<T>(Action<SupportGateResult<T>> onDone, SupportGateResult<T> result)
        {
            if (onDone == null)
            {
                return;
            }

            try
            {
                onDone(result);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
