using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace Hooligapps.SupportGate
{
    public sealed class SupportGateHttpRequest
    {
        public string Method { get; }
        public string Url { get; }

        /// <summary>Тело запроса или null. Для загрузки файла это его байты.</summary>
        public byte[] Body { get; }

        public string ContentType { get; }
        public IDictionary<string, string> Headers { get; }

        public SupportGateHttpRequest(string method, string url, byte[] body = null,
            string contentType = null, IDictionary<string, string> headers = null)
        {
            Method = method;
            Url = url;
            Body = body;
            ContentType = contentType;
            Headers = headers ?? new Dictionary<string, string>();
        }
    }

    public sealed class SupportGateHttpResponse
    {
        public long StatusCode { get; }
        public string Body { get; }

        /// <summary>Заполнено, если запрос не дошёл до ответа.</summary>
        public string TransportError { get; }

        public bool HasTransportError
        {
            get { return !string.IsNullOrEmpty(TransportError); }
        }

        public SupportGateHttpResponse(long statusCode, string body, string transportError = null)
        {
            StatusCode = statusCode;
            Body = body;
            TransportError = transportError;
        }
    }

    /// <summary>
    /// Транспорт вынесен интерфейсом, чтобы контрактные тесты обходились без сети.
    /// </summary>
    public interface ISupportGateTransport
    {
        IEnumerator Send(SupportGateHttpRequest request, Action<SupportGateHttpResponse> onDone);
    }

    public sealed class SupportGateUnityWebRequestTransport : ISupportGateTransport
    {
        private readonly int _timeoutSeconds;

        public SupportGateUnityWebRequestTransport(int timeoutSeconds = 15)
        {
            _timeoutSeconds = timeoutSeconds;
        }

        public IEnumerator Send(SupportGateHttpRequest request, Action<SupportGateHttpResponse> onDone)
        {
            using (var http = new UnityWebRequest(request.Url, request.Method))
            {
                if (request.Body != null)
                {
                    http.uploadHandler = new UploadHandlerRaw(request.Body);
                }

                http.downloadHandler = new DownloadHandlerBuffer();
                http.timeout = _timeoutSeconds;

                if (!string.IsNullOrEmpty(request.ContentType))
                {
                    http.SetRequestHeader("Content-Type", request.ContentType);
                }

                foreach (var header in request.Headers)
                {
                    http.SetRequestHeader(header.Key, header.Value);
                }

                yield return http.SendWebRequest();

                var transportError = IsTransportError(http) ? http.error : null;
                var body = http.downloadHandler != null ? http.downloadHandler.text : null;

                if (onDone != null)
                {
                    onDone(new SupportGateHttpResponse(http.responseCode, body, transportError));
                }
            }
        }

        private static bool IsTransportError(UnityWebRequest request)
        {
#if UNITY_2020_2_OR_NEWER
            return request.result == UnityWebRequest.Result.ConnectionError ||
                   request.result == UnityWebRequest.Result.DataProcessingError;
#else
            return request.isNetworkError;
#endif
        }
    }
}
