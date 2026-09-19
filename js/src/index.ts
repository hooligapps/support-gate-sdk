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
