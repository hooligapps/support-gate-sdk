// Прикладной слой поверх транспорта: справочник, загрузка файлов и отправка
// обращения. Тем же классом пользуется и наше окно, и своя вёрстка игры.

import { Api, randomKey } from './api';
import { Logger } from './logger';
import {
  Attachment,
  AttachmentLimits,
  FormConfig,
  SupportGateError,
  SupportGateOptions,
  Ticket,
  TicketDraft,
} from './types';

export class SupportGateClient {
  readonly api: Api;

  private config: FormConfig | null = null;
  private configPromise: Promise<FormConfig> | null = null;

  constructor(options: SupportGateOptions) {
    this.api = new Api(options);
  }

  /** Справочник берётся один раз на сессию окна. */
  form(signal?: AbortSignal): Promise<FormConfig> {
    if (this.config) {
      return Promise.resolve(this.config);
    }
    if (!this.configPromise) {
      this.configPromise = this.api
        .form(signal)
        .then((config) => {
          this.config = config;
          return config;
        })
        .catch((error) => {
          // Иначе неудачная загрузка залипнет в кэше на всю сессию.
          this.configPromise = null;
          throw error;
        });
    }
    return this.configPromise;
  }

  /**
   * Кладёт файл в хранилище и возвращает вложение, которое надо передать в
   * submit. Лимиты проверяются до запроса: сервер откажет теми же числами.
   */
  async upload(file: File, signal?: AbortSignal): Promise<Attachment> {
    const limits = (await this.form(signal)).attachments;
    checkFile(file, limits);

    const presigned = await this.api.presign(file.name, file.type, file.size, signal);
    await this.api.upload(file, presigned, signal);

    Logger.log('Client.upload', 'файл загружен: %s', [presigned.attachment_id]);
    return {
      attachment_id: presigned.attachment_id,
      filename: file.name,
      content_type: file.type,
      size: file.size,
    };
  }

  /**
   * Отправляет обращение. `idempotencyKey` живёт на одну попытку отправки:
   * повтор после сетевой ошибки с тем же ключом вернёт то же обращение, а не
   * создаст второе.
   */
  async submit(
    draft: TicketDraft,
    idempotencyKey: string = randomKey(),
    signal?: AbortSignal,
  ): Promise<Ticket> {
    const payload: TicketDraft = {
      ...draft,
      client: draft.client ?? collectClientInfo(),
    };
    return this.api.createTicket(payload, idempotencyKey, signal);
  }

  /**
   * Текущее состояние обращения. Сразу после submit номера Jira ещё нет —
   * его проставляет воркер; доступно только той же сессии, что отправляла.
   */
  async ticket(ticketId: string, signal?: AbortSignal): Promise<Ticket> {
    return this.api.ticket(ticketId, signal);
  }
}

function checkFile(file: File, limits: AttachmentLimits): void {
  if (limits.allowed_types.length && limits.allowed_types.indexOf(file.type) === -1) {
    throw new SupportGateError(
      'invalid_request',
      `тип ${file.type || 'неизвестен'} не поддерживается`,
      0,
      'unsupported_type',
    );
  }
  if (file.size > limits.max_file_size) {
    throw new SupportGateError(
      'invalid_request',
      `файл больше ${Math.floor(limits.max_file_size / 1024 / 1024)} МБ`,
      0,
      'file_too_large',
    );
  }
}

/**
 * Данные окружения для диагностики. Подделка не страшна: на идентификацию
 * игрока они не влияют, сервер обрезает их по длине.
 */
export function collectClientInfo() {
  if (typeof navigator === 'undefined' || typeof screen === 'undefined') {
    return {};
  }
  return {
    browser: navigator.userAgent.slice(0, 255),
    screen: `${screen.width}x${screen.height}`,
    user_agent: navigator.userAgent.slice(0, 1024),
  };
}
