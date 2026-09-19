namespace Hooligapps.SupportGate.UI
{
    public enum SupportGateResultStatus
    {
        /// <summary>Обращение принято сервисом.</summary>
        Sent = 0,

        /// <summary>Не загрузился справочник или не ушло обращение; игрок может повторить.</summary>
        Error = 1,

        /// <summary>Окно закрыли, так ничего и не отправив.</summary>
        Closed = 2
    }

    public enum SupportGateResultStage
    {
        None = 0,

        /// <summary>Не загрузился справочник формы.</summary>
        Load = 1,

        /// <summary>Не ушло обращение.</summary>
        Submit = 2
    }

    /// <summary>
    /// Итог показа окна для лога игры: <c>Sent</c> — один раз с номером обращения,
    /// <c>Error</c> — на каждую неудачную попытку, <c>Closed</c> — только если окно
    /// закрыли без отправки. Ошибки вложений сюда не попадают: игрок убирает файл
    /// и продолжает.
    /// </summary>
    public sealed class SupportGateFormResult
    {
        public SupportGateResultStatus Status { get; }
        public SupportGateResultStage Stage { get; }

        /// <summary>Заполнен при Sent.</summary>
        public SupportGateTicket Ticket { get; }

        /// <summary>Заполнена при Error.</summary>
        public SupportGateError Error { get; }

        private SupportGateFormResult(SupportGateResultStatus status, SupportGateResultStage stage,
            SupportGateTicket ticket, SupportGateError error)
        {
            Status = status;
            Stage = stage;
            Ticket = ticket;
            Error = error;
        }

        public static SupportGateFormResult Sent(SupportGateTicket ticket)
        {
            return new SupportGateFormResult(SupportGateResultStatus.Sent, SupportGateResultStage.None, ticket, null);
        }

        public static SupportGateFormResult Failed(SupportGateResultStage stage, SupportGateError error)
        {
            return new SupportGateFormResult(SupportGateResultStatus.Error, stage, null, error);
        }

        public static SupportGateFormResult Closed()
        {
            return new SupportGateFormResult(SupportGateResultStatus.Closed, SupportGateResultStage.None, null, null);
        }

        public override string ToString()
        {
            switch (Status)
            {
                case SupportGateResultStatus.Sent:
                    return "sent " + Ticket.TicketId + " " + (Ticket.IssueKey ?? "-");
                case SupportGateResultStatus.Error:
                    return "error " + Stage.ToString().ToLowerInvariant() + ": " + Error;
                default:
                    return "closed";
            }
        }
    }
}
