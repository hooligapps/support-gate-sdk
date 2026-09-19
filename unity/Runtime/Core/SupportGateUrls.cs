using System;

namespace Hooligapps.SupportGate
{
    public static class SupportGateUrls
    {
        /// <summary>
        /// Приводит адрес сервиса к виду без хвостового слэша: пути клиент добавляет
        /// сам. Схема обязательна — без неё UnityWebRequest молча не отправит запрос.
        /// </summary>
        public static string NormalizeEndpoint(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentException("endpoint обязателен", nameof(endpoint));
            }

            var value = endpoint.Trim();
            if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("endpoint должен начинаться с http:// или https://", nameof(endpoint));
            }

            return value.TrimEnd('/');
        }
    }
}
