// Транспорт: один способ сходить в сервис и один способ разобрать ошибку.
// Ни DOM, ни формы здесь нет — этот слой переиспользует и своя вёрстка игры.

import { Logger } from './logger';
import {
  FormConfig,
  SupportGateError,
  SupportGateOptions,
  Ticket,
  TicketDraft,
  TokenSource,
} from './types';

interface PresignResponse {
  attachment_id: string;
  upload_url: string;
  method: string;
  headers: Record<string, string>;
  expires_in: number;
}

const DEFAULT_TIMEOUT = 15000;

function normalizeEndpoint(endpoint: string): string {
  if (!endpoint) {
    throw new SupportGateError('invalid_request', 'endpoint обязателен');
  }
  return endpoint.replace(/\/+$/, '');
}

/** Ключ идемпотентности: повтор той же отправки не создаёт второе обращение. */
export function randomKey(): string {
  const crypto = typeof globalThis !== 'undefined' ? globalThis.crypto : undefined;
  if (crypto && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `sg-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

interface RefreshResponse {
  token: string;
  expires_in: number;
}

export class Api {
  private readonly endpoint: string;
  private readonly token: TokenSource;
  private readonly timeout: number;
  /** Токен, выданный шлюзом при обновлении: он новее того, что дала игра. */
  private renewed: string | null = null;

  constructor(options: SupportGateOptions) {
    this.endpoint = normalizeEndpoint(options.endpoint);
    this.token = options.token;
    this.timeout = options.timeout ?? DEFAULT_TIMEOUT;
  }

  async form(signal?: AbortSignal): Promise<FormConfig> {
    return this.call<FormConfig>('GET', '/v1/form', undefined, {}, signal);
  }

  async presign(
    filename: string,
    contentType: string,
    size: number,
    signal?: AbortSignal,
  ): Promise<PresignResponse> {
    return this.call<PresignResponse>(
      'POST',
      '/v1/attachments/presign',
      { filename, content_type: contentType, size },
      {},
      signal,
    );
  }

  async createTicket(
    draft: TicketDraft,
    idempotencyKey: string,
    signal?: AbortSignal,
  ): Promise<Ticket> {
    return this.call<Ticket>(
      'POST',
      '/v1/tickets',
      draft,
      { 'Idempotency-Key': idempotencyKey },
      signal,
    );
  }

  async ticket(ticketId: string, signal?: AbortSignal): Promise<Ticket> {
    return this.call<Ticket>(
      'GET',
      `/v1/tickets/${encodeURIComponent(ticketId)}`,
      undefined,
      {},
      signal,
    );
  }

  /**
   * Файл уходит прямо в хранилище по подписанной ссылке: сервис медиа не
   * проксирует, поэтому и ошибка здесь приходит не в нашем формате.
   */
  async upload(
    file: File | Blob,
    presigned: PresignResponse,
    signal?: AbortSignal,
  ): Promise<void> {
    Logger.log('Api.upload', 'PUT %s', [presigned.upload_url]);

    let response: Response;
    try {
      response = await fetch(presigned.upload_url, {
        method: presigned.method || 'PUT',
        headers: presigned.headers,
        body: file,
        signal,
      });
    } catch (error) {
      throw new SupportGateError('network', String(error));
    }

    if (!response.ok) {
      throw new SupportGateError(
        'http',
        `хранилище отклонило файл: HTTP ${response.status}`,
        response.status,
        'upload_failed',
      );
    }
  }

  private async current(): Promise<string> {
    if (this.renewed) return this.renewed;
    const token = typeof this.token === 'function' ? await this.token() : this.token;
    if (!token) {
      throw new SupportGateError('invalid_request', 'session token пустой');
    }
    return token;
  }

  /**
   * Сессия истекла (401): просим у игры свежий токен, а если она отдаёт тот же
   * или токен задан строкой — продлеваем через шлюз, пока не вышел его срок
   * (grace + предельное время жизни настраиваются на сервере). Ошибка
   * обновления не важна — наружу уйдёт исходный 401.
   */
  private async renew(stale: string): Promise<boolean> {
    if (typeof this.token === 'function') {
      try {
        const fresh = await this.token({ reason: 'expired' });
        if (fresh && fresh !== stale) {
          this.renewed = fresh;
          return true;
        }
      } catch (error) {
        Logger.log('Api.renew', 'игра не дала новый токен: %s', [String(error)]);
      }
    }
    try {
      const response = await this.request<RefreshResponse>('POST', '/v1/session/refresh', stale);
      this.renewed = response.token;
      return true;
    } catch (error) {
      Logger.log('Api.renew', 'шлюз не продлил сессию: %s', [String(error)]);
      return false;
    }
  }

  private async call<T>(
    method: string,
    path: string,
    body?: unknown,
    headers: Record<string, string> = {},
    signal?: AbortSignal,
  ): Promise<T> {
    const token = await this.current();
    try {
      return await this.request<T>(method, path, token, body, headers, signal);
    } catch (error) {
      if (!(error instanceof SupportGateError) || error.status !== 401 || !(await this.renew(token))) {
        throw error;
      }
    }
    // Повтор с теми же заголовками (и ключом идемпотентности) — дубля не будет.
    return this.request<T>(method, path, await this.current(), body, headers, signal);
  }

  private async request<T>(
    method: string,
    path: string,
    token: string,
    body?: unknown,
    headers: Record<string, string> = {},
    signal?: AbortSignal,
  ): Promise<T> {
    const url = `${this.endpoint}${path}`;
    const init: RequestInit = {
      method,
      headers: {
        Accept: 'application/json',
        Authorization: `Bearer ${token}`,
        ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
        ...headers,
      },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: signal ?? this.deadline(),
    };

    Logger.log('Api.call', '%s %s', [method, url]);

    let response: Response;
    try {
      response = await fetch(url, init);
    } catch (error) {
      throw new SupportGateError('network', String(error));
    }

    const text = await response.text();
    let payload: unknown = null;
    if (text) {
      try {
        payload = JSON.parse(text);
      } catch {
        if (response.ok) {
          throw new SupportGateError('parse', 'ответ не разобрался', response.status, null, {}, text);
        }
      }
    }

    if (!response.ok) {
      throw toError(response.status, payload, text);
    }

    Logger.log('Api.call', 'ответ %s %s', [method, path]);
    return payload as T;
  }

  private deadline(): AbortSignal | undefined {
    if (typeof AbortController === 'undefined') return undefined;

    const controller = new AbortController();
    setTimeout(() => controller.abort(), this.timeout);
    return controller.signal;
  }
}

/**
 * Ошибки сервиса приходят как {error, message, fields}, но FastAPI заворачивает
 * их в detail — разбираем оба вида, чтобы UI получал одинаковый объект.
 */
function toError(status: number, payload: unknown, raw: string): SupportGateError {
  const body = payload as { detail?: unknown; error?: string; message?: string } | null;
  const detail = (body && typeof body.detail === 'object' && body.detail !== null
    ? body.detail
    : body) as { error?: string; message?: string; fields?: Record<string, string> } | null;

  return new SupportGateError(
    'http',
    detail?.message || `HTTP ${status}`,
    status,
    detail?.error ?? null,
    detail?.fields ?? {},
    raw || null,
  );
}
