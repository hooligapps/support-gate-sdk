// Модальная обёртка над формой: подложка, Esc, возврат фокуса. Всё живёт в
// одном узле на body, чтобы вёрстка игры не влияла на позиционирование.

import { SupportGateClient } from '../client';
import { SupportResult, Ticket } from '../types';
import { el } from './dom';
import { SupportForm } from './form';
import { strings } from './i18n';
import { injectStyles } from './styles';

export interface ModalOptions {
  onSubmitted?: (ticket: Ticket) => void;
  onClose?: () => void;
  /** Итог окна: `sent`, `error` (на каждую попытку) или `closed` — закрыли, не отправив. */
  onResult?: (result: SupportResult) => void;
}

export class SupportModal {
  private readonly client: SupportGateClient;
  private readonly options: ModalOptions;

  private root: HTMLElement | null = null;
  private form: SupportForm | null = null;
  private previousFocus: Element | null = null;
  private readonly onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape') this.close();
  };

  constructor(client: SupportGateClient, options: ModalOptions = {}) {
    this.client = client;
    this.options = options;
  }

  get isOpen(): boolean {
    return this.root !== null;
  }

  async open(): Promise<void> {
    if (this.root) return;

    injectStyles();
    this.previousFocus = document.activeElement;

    const backdrop = el('div', { class: 'sg-backdrop' });
    backdrop.addEventListener('click', () => this.close());

    const close = el('button', { type: 'button', class: 'sg-close', 'aria-label': 'Close' }, ['×']);
    close.addEventListener('click', () => this.close());

    const title = el('h2', { id: 'sg-title' }, [strings().title]);
    const dialog = el(
      'div',
      { class: 'sg-dialog', role: 'dialog', 'aria-modal': 'true', 'aria-labelledby': 'sg-title' },
      [el('div', { class: 'sg-header' }, [title, close])],
    );

    const content = el('div', { class: 'sg-content' });
    dialog.appendChild(content);

    const root = el('div', { class: 'sg-root' }, [backdrop, dialog]);
    document.body.appendChild(root);
    document.addEventListener('keydown', this.onKeyDown);
    this.root = root;

    const form = new SupportForm(content, this.client, {
      onSubmitted: this.options.onSubmitted,
      onCancel: () => this.close(),
      onResult: this.options.onResult,
    });
    this.form = form;
    await form.mount();

    // Заголовок и направление письма — по ответу сервера: язык выбирает он.
    const config = await this.client.form().catch(() => null);
    if (config && this.root) {
      const text = strings(config.strings, config.locale);
      title.textContent = text.title;
      close.setAttribute('aria-label', text.close);
      dialog.setAttribute('lang', config.locale);
      dialog.setAttribute('dir', config.direction || 'ltr');
    }

    const first = dialog.querySelector('select, input, textarea, button') as HTMLElement | null;
    first?.focus();
  }

  close(): void {
    if (!this.root) return;

    document.removeEventListener('keydown', this.onKeyDown);
    this.root.remove();
    this.root = null;
    (this.previousFocus as HTMLElement | null)?.focus?.();
    if (!this.form?.sent) this.options.onResult?.({ status: 'closed' });
    this.form = null;
    this.options.onClose?.();
  }
}
