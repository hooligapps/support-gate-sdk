using System;
using UnityEngine;

namespace Hooligapps.SupportGate
{
    /// <summary>
    /// Форма в системном браузере: игра открывает страницу шлюза `/form`, где
    /// работает та же веб-форма, что у веб-игр, — с выбором файлов диалогом
    /// браузера и без вёрстки в Unity. Ни плагинов, ни WebView не нужно.
    ///
    /// Токен передаётся во фрагменте адреса (`#token=…`): фрагмент не уходит в
    /// запрос и не попадает в логи сервера, а страница сразу стирает его из
    /// адресной строки. Итог отправки обратно в игру не приходит — браузер
    /// отдельный процесс; обращение при этом записано на шлюзе с игрой и игроком.
    /// </summary>
    public static class SupportGateBrowser
    {
        public const string PagePath = "/form";

        /// <summary>
        /// Открывает форму в браузере. Токен — тот же JWT сессии, что и для клиента;
        /// игроку в браузере нужно время написать, поэтому для этого пути бэкенду
        /// игры имеет смысл выпускать токен на 30–60 минут вместо 15.
        /// </summary>
        public static void Open(string endpoint, string token)
        {
            Application.OpenURL(Url(endpoint, token));
        }

        public static string Url(string endpoint, string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("token обязателен", nameof(token));
            }

            return SupportGateUrls.NormalizeEndpoint(endpoint) + PagePath + "#token=" + Uri.EscapeDataString(token);
        }
    }
}
