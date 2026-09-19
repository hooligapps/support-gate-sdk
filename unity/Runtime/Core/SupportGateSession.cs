using System;
using System.Collections;

namespace Hooligapps.SupportGate
{
    /// <summary>
    /// Поставщик токена сессии. Выпускает токен бэкенд игры своим секретом; секрет
    /// игры и учётки Jira в клиент не попадают никогда.
    ///
    /// Корутина, а не свойство: токен живёт 15 минут, и за ним обычно надо сходить
    /// на свой сервер.
    /// </summary>
    public delegate IEnumerator SupportGateTokenRequest(Action<string> onToken);

    public sealed class SupportGateSession
    {
        private readonly SupportGateTokenRequest _request;
        private string _token;

        private SupportGateSession(string token, SupportGateTokenRequest request)
        {
            _token = token;
            _request = request;
        }

        /// <summary>Готовый токен: игра сама следит за его сроком.</summary>
        public static SupportGateSession FromToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("token обязателен", nameof(token));
            }

            return new SupportGateSession(token, null);
        }

        /// <summary>
        /// Токен запрашивается у игры перед каждым обращением к сервису. Кэшировать
        /// его — задача игры: сервис по одному токену обслуживает всю сессию окна.
        /// </summary>
        public static SupportGateSession FromProvider(SupportGateTokenRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return new SupportGateSession(null, request);
        }

        /// <summary>Последний известный токен. Пустой, пока провайдер не отработал.</summary>
        public string Token
        {
            get { return _token; }
        }

        public void SetToken(string token)
        {
            _token = token;
        }

        public IEnumerator Resolve(Action<string> onToken)
        {
            if (_request == null)
            {
                onToken(_token);
                yield break;
            }

            string issued = null;
            var routine = _request(value => issued = value);
            while (routine.MoveNext())
            {
                yield return routine.Current;
            }

            if (!string.IsNullOrEmpty(issued))
            {
                _token = issued;
            }

            onToken(_token);
        }
    }
}
