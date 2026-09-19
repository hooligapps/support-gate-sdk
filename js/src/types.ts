// Формы контракта Support Gateway. Справочник категорий и лимиты приходят с
// сервера (GET /v1/form): игра их у себя не хранит и не обновляет релизом.

export interface Localized {
  id: string;
  label: string;
}

export type FieldType = 'text' | 'select' | 'date';

export interface ExtraField extends Localized {
  type: FieldType;
  required: boolean;
  options?: Localized[];
  max_length?: number;
}

export interface Category extends Localized {
  subcategories: Localized[];
  extra_fields?: ExtraField[];
}

export interface AttachmentLimits {
  max_files: number;
  max_file_size: number;
  allowed_types: string[];
}

export interface Prefill {
  email: string | null;
  email_locked: boolean;
}

export interface FormConfig {
  game: string;
  /** Строчный BCP47, как файлы каталогов на шлюзе: `ru`, `pt-br`. */
  locale: string;
  direction: 'ltr' | 'rtl';
  /** Подписи окна (`ui.*`) на языке игрока, ICU-строки. */
  strings: Record<string, string>;
  prefill: Prefill;
  categories: Category[];
  attachments: AttachmentLimits;
}

export interface Attachment {
  attachment_id: string;
  filename: string;
  content_type: string;
  size: number;
}

export interface ClientInfo {
  device_model?: string;
  browser?: string;
  screen?: string;
  user_agent?: string;
}

export interface TicketDraft {
  category: string;
  subcategory: string;
  subject: string;
  description: string;
  email?: string;
  extra?: Record<string, string>;
  attachment_ids?: string[];
  client?: ClientInfo;
}

export interface Ticket {
  ticket_id: string;
  issue_key: string | null;
  status: string;
  created_at: string;
}

/**
 * Итог окна для игры — один колбэк вместо трёх. `sent` приходит один раз;
 * `error` — на каждую неудачную попытку (игрок может повторить); `closed` —
 * только если окно закрыли, так и не отправив.
 */
export type SupportResult =
  | { status: 'sent'; ticket: Ticket; ticketId: string; issueKey: string | null }
  | {
      status: 'error';
      /** `load` — не загрузился справочник формы, `submit` — не ушло обращение. */
      stage: 'load' | 'submit';
      message: string;
      kind: ErrorKind | null;
      httpStatus: number;
      code: string | null;
    }
  | { status: 'closed' };

export type ErrorKind =
  /** Запрос отклонён на клиенте и до сети не дошёл. */
  | 'invalid_request'
  /** Соединение, DNS, таймаут, отменённый запрос. */
  | 'network'
  /** Ответ с HTTP-статусом ошибки. */
  | 'http'
  /** Тело ответа не разобралось. */
  | 'parse';

/**
 * Единственный тип ошибки клиента: код из тела ответа сервиса доступен в
 * `code`, а неверные поля формы — в `fields`, чтобы UI показал их у полей.
 */
export class SupportGateError extends Error {
  constructor(
    readonly kind: ErrorKind,
    message: string,
    readonly status = 0,
    readonly code: string | null = null,
    readonly fields: Record<string, string> = {},
    readonly rawBody: string | null = null,
  ) {
    super(message);
    this.name = 'SupportGateError';
  }

  /** Повторять запрос имеет смысл только при этих ошибках. */
  get transient(): boolean {
    return this.kind === 'network' || this.status === 429 || this.status >= 500;
  }
}

/** Токен сессии выпускает бэкенд игры; строкой или функцией, если он обновляется. */
export type TokenSource = string | (() => string | Promise<string>);

export interface SupportGateOptions {
  /** Адрес сервиса, например https://support.flushee.work */
  endpoint: string;
  token: TokenSource;
  /** Таймаут одного запроса, мс. */
  timeout?: number;
}
