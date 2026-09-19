using System;
using System.Collections.Generic;

namespace Hooligapps.SupportGate.UI
{
    public enum SupportGateViewState
    {
        Hidden = 0,
        Loading = 1,

        /// <summary>Форма заполняется игроком.</summary>
        Form = 2,

        /// <summary>Отправка идёт: поля заблокированы.</summary>
        Sending = 3,

        /// <summary>Обращение принято, показан номер.</summary>
        Sent = 4,

        /// <summary>Справочник не загрузился: есть только «Повторить».</summary>
        LoadFailed = 5
    }

    public enum SupportGateUploadState
    {
        Uploading = 0,
        Ready = 1
    }

    /// <summary>Вложение в том виде, в каком его показывает форма.</summary>
    public sealed class SupportGateAttachmentItem
    {
        public string FileName { get; }
        public long Size { get; }
        public SupportGateUploadState State { get; internal set; }

        /// <summary>Заполняется, когда файл долетел до хранилища.</summary>
        public string AttachmentId { get; internal set; }

        public SupportGateAttachmentItem(string fileName, long size)
        {
            FileName = fileName;
            Size = size;
            State = SupportGateUploadState.Uploading;
        }
    }

    /// <summary>Что игрок ввёл. Собирает вью, проверяет презентер.</summary>
    public sealed class SupportGateFormValues
    {
        public string Category;
        public string Subcategory;
        public string Email;
        public string Subject;
        public string Description;
        public Dictionary<string, string> Extra = new Dictionary<string, string>();
    }

    /// <summary>
    /// Вью формы независимо от рендерера. TRoot — GameObject у uGUI и VisualElement
    /// у UI Toolkit.
    /// </summary>
    public interface ISupportGateFormView<out TRoot>
    {
        TRoot Root { get; }

        /// <summary>Закрытие окна игроком.</summary>
        event Action CloseRequested;

        /// <summary>Повторная загрузка справочника после ошибки.</summary>
        event Action RetryRequested;

        event Action SubmitRequested;

        /// <summary>Игрок выбрал категорию: подкатегории и доп. поля зависят от неё.</summary>
        event Action<string> CategoryChanged;

        /// <summary>
        /// Просьба выбрать файл. Выбор файла зависит от платформы и плагинов игры,
        /// поэтому пакет его не делает — игра отвечает и зовёт AttachFile.
        /// </summary>
        event Action AttachRequested;

        event Action<int> AttachmentRemoved;

        void SetState(SupportGateViewState state);

        /// <summary>Рисует форму по справочнику. Вызывается один раз за открытие.</summary>
        void BindConfig(SupportGateFormConfig config, SupportGateStrings text);

        void BindSubcategories(IReadOnlyList<SupportGateOption> options);

        void BindExtraFields(IReadOnlyList<SupportGateExtraField> fields);

        void BindAttachments(IReadOnlyList<SupportGateAttachmentItem> attachments);

        /// <summary>Сообщение над формой. null — убрать.</summary>
        void ShowMessage(string message);

        /// <summary>Ошибки у полей: ключ — имя поля, как его называет сервер.</summary>
        void ShowFieldErrors(IReadOnlyDictionary<string, string> errors);

        /// <summary>Экран «отправлено» с номером обращения.</summary>
        void ShowSent(string message);

        SupportGateFormValues ReadValues();
    }

    /// <summary>
    /// Адаптер к модальной системе игры. TryShow возвращает false, если показывать
    /// сейчас нельзя: пакет не ставит окно в очередь и не повторяет попытку.
    /// </summary>
    public interface ISupportGateModalHost<in TRoot>
    {
        bool TryShow(TRoot content);

        void Close(TRoot content);
    }
}
