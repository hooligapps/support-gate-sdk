// Точка входа UMD-сборки: глобальный SupportGate.
//
// Минимум для игры:
//   SupportGate.init({endpoint: 'https://support.flushee.work', token: sessionToken});
//   SupportGate.open();
//
// Токен сессии выпускает бэкенд игры (JWT на 15 минут); ни ключ игры, ни
// учётки JSM в клиент не попадают.

import { SupportGateClient } from './client';
import { Logger } from './logger';
import { SupportGateOptions, SupportResult, Ticket } from './types';
import { SupportForm, SupportFormOptions } from './ui/form';
import { ModalOptions, SupportModal } from './ui/modal';
import { injectStyles } from './ui/styles';

export * from './types';
export { SupportGateClient } from './client';
export { SupportForm } from './ui/form';
export { SupportModal } from './ui/modal';
// Форматтер и сборка подписей открыты для тестов и своих окон поверх клиента.
export { format } from './ui/format';
export { strings } from './ui/i18n';
export type { Ticket, SupportResult, ModalOptions, SupportFormOptions };

let client: SupportGateClient | null = null;
let modal: SupportModal | null = null;

/** Создаёт клиента. Повторный вызов заменяет настройки — например, новый токен. */
export function init(options: SupportGateOptions): SupportGateClient {
  modal?.close();
  modal = null;
  client = new SupportGateClient(options);
  Logger.log('SupportGate', 'инициализирован: %s', [options.endpoint]);
  return client;
}

export function getInstance(): SupportGateClient {
  if (!client) {
    throw new Error('SupportGate.init(...) не вызван');
  }
  return client;
}

/** Открывает модальное окно с формой. */
export function open(options: ModalOptions = {}): Promise<void> {
  if (!modal || !modal.isOpen) {
    modal = new SupportModal(getInstance(), options);
  }
  return modal.open();
}

export function close(): void {
  modal?.close();
}

/** Встраивает форму в свой контейнер, без модального окна. */
export function mount(host: HTMLElement, options: SupportFormOptions = {}): Promise<SupportForm> {
  injectStyles();
  host.classList.add('sg-embed');
  const form = new SupportForm(host, getInstance(), options);
  return form.mount().then(() => form);
}

/**
 * Страница шлюза `/form`, которую игра открывает в системном браузере
 * (Unity, десктоп): токен приходит во фрагменте адреса — `/form#token=…` —
 * и до сервера не доходит (фрагмент не уходит в запрос и не попадает в логи).
 * Страница сразу стирает его из адресной строки, чтобы он не остался в
 * истории и закладках. Сервис — тот же origin, что и страница.
 *
 * Без токена монтировать нечего: показывается подсказка, возвращается null.
 * Сессию клиент продлевает сам через шлюз; когда и это не удалось (страницу
 * держали открытой дольше предельного срока), форма заменяется той же
 * подсказкой — новую сессию даст только игра.
 */
export function hosted(host: HTMLElement, options: SupportFormOptions = {}): Promise<SupportForm | null> {
  const token = tokenFromFragment();
  if (!token) {
    showReopenHint(host);
    return Promise.resolve(null);
  }

  init({ endpoint: window.location.origin, token });
  return mount(host, {
    ...options,
    onResult(result) {
      options.onResult?.(result);
      if (result.status === 'error' && result.httpStatus === 401) {
        showReopenHint(host);
      }
    },
  });
}

function showReopenHint(host: HTMLElement): void {
  injectStyles();
  host.classList.add('sg-embed');
  while (host.firstChild) host.removeChild(host.firstChild);
  host.appendChild(
    el('div', 'sg-state', [
      el('h3', '', ['Open this page from the game']),
      el('p', '', ['The support form needs a session from the game; the link has expired or is incomplete.']),
    ]),
  );
}

function tokenFromFragment(): string {
  const hash = window.location.hash.replace(/^#/, '');
  const token = new URLSearchParams(hash).get('token') ?? '';
  if (token) {
    window.history.replaceState(null, '', window.location.pathname + window.location.search);
  }
  return token;
}

function el(tag: string, className: string, children: (string | HTMLElement)[]): HTMLElement {
  const node = document.createElement(tag);
  if (className) node.className = className;
  for (const child of children) {
    node.appendChild(typeof child === 'string' ? document.createTextNode(child) : child);
  }
  return node;
}
