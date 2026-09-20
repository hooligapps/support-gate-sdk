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

    /// <summary>
    /// Зачем клиент просит токен: <see cref="Expired"/> — сервис ответил 401,
    /// кэш игры пора сбросить и выпустить новый.
    /// </summary>
    public enum SupportGateTokenReason
    {
        Request,
        Expired
    }

    /// <summary>Поставщик токена, которому сообщают причину запроса.</summary>
    public delegate IEnumerator SupportGateTokenRenewal(SupportGateTokenReason reason, Action<string> onToken);

    public sealed class SupportGateSession
    {
        private readonly SupportGateTokenRenewal _request;
        private string _token;

        private SupportGateSession(string token, SupportGateTokenRenewal request)
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
        /// После 401 клиент спросит ещё раз; если игра отдаст тот же токен, клиент
        /// продлит сессию сам через шлюз.
        /// </summary>
        public static SupportGateSession FromProvider(SupportGateTokenRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return new SupportGateSession(null, (reason, onToken) => request(onToken));
        }

        /// <summary>
        /// То же, но провайдер узнаёт причину: на <see cref="SupportGateTokenReason.Expired"/>
        /// кэшированный токен надо выбросить и выпустить новый.
        /// </summary>
        public static SupportGateSession FromProvider(SupportGateTokenRenewal request)
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
            return Ask(SupportGateTokenReason.Request, onToken);
        }

        /// <summary>
        /// Сессия истекла: просим у игры новый токен. Отдаёт true, если он отличается
        /// от прежнего — иначе повторять запрос смысла нет.
        /// </summary>
        public IEnumerator Renew(string stale, Action<bool> onDone)
        {
            if (_request == null)
            {
                onDone(false);
                yield break;
            }

            string issued = null;
            var ask = Ask(SupportGateTokenReason.Expired, value => issued = value);
            while (ask.MoveNext())
            {
                yield return ask.Current;
            }

            onDone(!string.IsNullOrEmpty(issued) && issued != stale);
        }

        private IEnumerator Ask(SupportGateTokenReason reason, Action<string> onToken)
        {
            if (_request == null)
            {
                onToken(_token);
                yield break;
            }

            string issued = null;
            var routine = _request(reason, value => issued = value);
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
