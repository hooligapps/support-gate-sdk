using System.Collections.Generic;

namespace Hooligapps.SupportGate
{
    public enum SupportGateErrorKind
    {
        None = 0,

        /// <summary>Запрос отклонён локально и до сети не дошёл.</summary>
        InvalidRequest = 1,

        /// <summary>Другой запрос ещё выполняется.</summary>
        Busy = 2,

        /// <summary>Транспортная ошибка: соединение, DNS, таймаут.</summary>
        Network = 3,

        /// <summary>Ответ с HTTP-статусом ошибки.</summary>
        Http = 4,

        /// <summary>Тело ответа не разобралось.</summary>
        Parse = 5
    }

    public sealed class SupportGateError
    {
        private static readonly IReadOnlyDictionary<string, string> NoFields =
            new Dictionary<string, string>();

        public SupportGateErrorKind Kind { get; }
        public string Message { get; }

        /// <summary>HTTP-статус; 0, если запроса не было.</summary>
        public long HttpStatus { get; }

        /// <summary>Машинный код из тела ответа сервиса, например validation_error.</summary>
        public string Code { get; }

        /// <summary>Ошибки по полям формы: имя поля — сообщение.</summary>
        public IReadOnlyDictionary<string, string> Fields { get; }

        /// <summary>Сырое тело ответа. Нужно, когда разбор не удался.</summary>
        public string RawBody { get; }

        public SupportGateError(SupportGateErrorKind kind, string message, long httpStatus = 0,
            string code = null, IReadOnlyDictionary<string, string> fields = null, string rawBody = null)
        {
            Kind = kind;
            Message = message;
            HttpStatus = httpStatus;
            Code = code;
            Fields = fields ?? NoFields;
            RawBody = rawBody;
        }

        /// <summary>Повторять запрос имеет смысл только при этих ошибках.</summary>
        public bool IsTransient
        {
            get { return Kind == SupportGateErrorKind.Network || HttpStatus == 429 || HttpStatus >= 500; }
        }

        /// <summary>Токен просрочен или не принят: игре нужно выпустить новый.</summary>
        public bool IsUnauthorized
        {
            get { return HttpStatus == 401 || HttpStatus == 403; }
        }

        public override string ToString()
        {
            return "SupportGateError(" + Kind + ", http=" + HttpStatus + "): " + Message;
        }
    }

    public readonly struct SupportGateResult<T>
    {
        public bool Success { get; }
        public T Value { get; }
        public SupportGateError Error { get; }

        private SupportGateResult(bool success, T value, SupportGateError error)
        {
            Success = success;
            Value = value;
            Error = error;
        }

        public static SupportGateResult<T> Ok(T value)
        {
            return new SupportGateResult<T>(true, value, null);
        }

        public static SupportGateResult<T> Fail(SupportGateError error)
        {
            return new SupportGateResult<T>(false, default, error);
        }

        public static SupportGateResult<T> Fail(SupportGateErrorKind kind, string message)
        {
            return Fail(new SupportGateError(kind, message));
        }
    }
}
