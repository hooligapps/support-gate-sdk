// Форма обращения: справочник, поля категории, вложения и отправка.
// Рисует себя в переданный контейнер и ничего не знает про модальное окно —
// игра может вставить форму прямо в свою страницу.

import { randomKey } from '../api';
import { collectClientInfo, SupportGateClient } from '../client';
import { Logger } from '../logger';
import {
  Attachment,
  Category,
  ExtraField,
  FormConfig,
  SupportGateError,
  SupportResult,
  Ticket,
  TargetOptions,
  TicketDraft,
} from '../types';
import { clear, el, humanSize, icon, scrollTo } from './dom';
import { Strings, strings } from './i18n';

// Опрос номера Jira после отправки: 6 × 2 с, дальше остаётся tkt_….
const ISSUE_KEY_POLLS = 6;
const ISSUE_KEY_POLL_MS = 2000;

export interface SupportFormOptions extends TargetOptions {
  /** Скрывает уже заданные корректные поля, оставляя описание и вложения. */
  compact?: boolean;
  /** Начальные значения; extra относится только к указанной категории. */
  preset?: Partial<Pick<TicketDraft, 'category' | 'subcategory' | 'subject' | 'extra'>>;
  /** Вызывается после успешной отправки, до показа экрана «отправлено». */
  onSubmitted?: (ticket: Ticket) => void;
  /** Показать кнопку «Отмена» и вызвать это при нажатии (в модалке — закрытие). */
  onCancel?: () => void;
  /** Итог: `sent` / `error`; `closed` шлёт обёртка (модалка), форма сама — нет. */
  onResult?: (result: SupportResult) => void;
  /** Lets a surrounding window update its localized title after a target switch. */
  onConfig?: (config: FormConfig) => void;
}

interface FileRow {
  file: File;
  row: HTMLLIElement;
  attachment: Attachment | null;
  previewUrl: string | null;
}

export class SupportForm {
  private readonly host: HTMLElement;
  private client: SupportGateClient;
  private readonly baseClient: SupportGateClient;
  private readonly options: SupportFormOptions;

  private text: Strings = strings();
  private config: FormConfig | null = null;
  private files: FileRow[] = [];
  private submitting = false;
  /** Обращение ушло — окно закрывается уже не «без результата». */
  sent = false;

  // Один ключ на всю форму: повтор после сетевой ошибки не создаёт дубль.
  private idempotencyKey = '';

  private body!: HTMLElement;
  private footer!: HTMLElement;
  private banner: HTMLElement | null = null;
  private submitButton: HTMLButtonElement | null = null;
  private targetSelect: HTMLSelectElement | null = null;
  private controller = new AbortController();
  private onPaste: ((event: Event) => void) | null = null;
  private disposed = false;
  private targetLocked = false;
  private draft: Record<string, string> = {};
  private draftFiles: File[] = [];
  private rendered = false;
  private pendingDraft: TicketDraft | null = null;
  targetId?: string;

  constructor(host: HTMLElement, client: SupportGateClient, options: SupportFormOptions = {}) {
    this.host = host;
    this.client = client;
    this.baseClient = client;
    this.options = options;
    this.targetId = options.targetId;
    if (options.targets) {
      if (!options.targets.length || !client.canSelectTarget
        || options.targets.some((target) => !target.id)
        || new Set(options.targets.map((target) => target.id)).size !== options.targets.length) {
        throw new SupportGateError('invalid_request', 'targets require a token provider and a nonempty unique list');
      }
      if (this.targetId && !options.targets.some((target) => target.id === this.targetId)) {
        throw new SupportGateError('invalid_request', 'targetId is not in targets');
      }
    }
    if (this.targetId) this.client = client.forTarget(this.targetId);
  }

  /** Загружает справочник и рисует форму. Ошибки показывает сама. */
  async mount(): Promise<void> {
    this.host.classList.toggle('sg-compact', !!this.options.compact);
    this.body = el('div', { class: 'sg-body' });
    this.footer = el('div', { class: 'sg-footer' });
    clear(this.host);
    if (this.options.targets) {
      const field = this.select('target', this.text.target, [
        { id: '', label: this.text.choose }, ...this.options.targets,
      ]);
      this.targetSelect = field.input as unknown as HTMLSelectElement;
      field.field.classList.add('sg-target');
      this.targetSelect.value = this.targetId ?? '';
      this.targetSelect.addEventListener('change', () => { void this.switchTarget(this.targetSelect!.value); });
      this.host.appendChild(field.field);
    }
    this.host.appendChild(this.body);
    this.host.appendChild(this.footer);

    if (this.options.targets && !this.targetId) {
      this.showState(this.text.choose);
    } else {
      this.showState(this.text.loading);
      await this.load();
    }
  }

  destroy(): void {
    this.disposed = true;
    this.releaseSession();
  }

  private releaseSession(): void {
    this.controller.abort();
    if (this.onPaste) document.removeEventListener('paste', this.onPaste);
    this.onPaste = null;
    for (const entry of this.files) if (entry.previewUrl) revokeUrl(entry.previewUrl);
    this.files = [];
  }

  private async switchTarget(targetId: string): Promise<void> {
    if (this.targetLocked || this.submitting || this.sent || this.disposed) {
      if (this.targetSelect) this.targetSelect.value = this.targetId ?? '';
      return;
    }
    if (this.rendered) {
      this.draft = {};
      this.body.querySelectorAll<HTMLInputElement>('input:not([type="file"]), select, textarea').forEach((input) => {
        if (!input.readOnly) this.draft[input.id] = input.value;
      });
      this.draftFiles = this.files.map((entry) => entry.file);
    }
    this.releaseSession();
    this.controller = new AbortController();
    this.targetId = targetId || undefined;
    this.pendingDraft = null;
    this.config = null;
    this.rendered = false;
    this.idempotencyKey = '';
    this.submitButton = null;
    this.showState(targetId ? this.text.loading : this.text.choose);
    if (!targetId) return;
    this.client = this.baseClient.forTarget(targetId);
    await this.load();
  }

  private async load(): Promise<void> {
    const client = this.client;
    const signal = this.controller.signal;
    try {
      const config = await client.form(signal);
      if (signal.aborted || this.disposed) return;
      this.config = config;
    } catch (error) {
      if (signal.aborted || this.disposed) return;
      Logger.log('SupportForm', 'справочник не загрузился: %s', [String(error)]);
      this.showLoadError();
      this.report('load', error);
      return;
    }
    this.text = strings(this.config.strings, this.config.locale);
    this.host.setAttribute('lang', this.config.locale);
    this.host.setAttribute('dir', this.config.direction || 'ltr');
    if (this.targetSelect) {
      const label = this.host.querySelector('label[for="sg-target"]');
      if (label) label.textContent = this.text.target;
      this.targetSelect.options[0].textContent = this.text.choose;
    }
    this.renderForm();
    this.options.onConfig?.(this.config);
  }

  private showState(title: string, description?: string, actions: HTMLElement[] = []): void {
    clear(this.body);
    clear(this.footer);
    const state = el('div', { class: 'sg-state' }, [el('h3', {}, [title])]);
    if (description) {
      state.appendChild(el('p', {}, [description]));
    }
    this.body.appendChild(state);
    for (const action of actions) {
      this.footer.appendChild(action);
    }
  }

  private showLoadError(): void {
    const retry = el('button', { type: 'button', class: 'sg-button sg-primary' }, [
      this.text.retry,
    ]);
    retry.addEventListener('click', () => {
      this.showState(this.text.loading);
      void this.load();
    });
    this.showState(this.text.loadFailed, undefined, [retry]);
  }

  private renderForm(): void {
    const config = this.config as FormConfig;
    const t = this.text;
    const preset = this.options.preset ?? {};
    clear(this.body);
    clear(this.footer);
    this.files = [];
    this.idempotencyKey = '';

    const categorySelect = this.select('category', t.category, [
      { id: '', label: t.choose },
      ...config.categories.map((c) => ({ id: c.id, label: c.label })),
    ]);
    const subcategorySelect = this.select('subcategory', t.subcategory, [
      { id: '', label: t.choose },
    ]);
    const extraHost = el('div', { class: 'sg-extra' });

    categorySelect.input.addEventListener('change', () => {
      const category = this.categoryById(categorySelect.input.value);
      fillOptions(subcategorySelect.input, [
        { id: '', label: t.choose },
        ...(category?.subcategories ?? []),
      ]);
      clear(extraHost);
      for (const field of category?.extra_fields ?? []) {
        const holder = this.extraField(field);
        const value = category?.id === preset.category ? preset.extra?.[field.id] : undefined;
        if (value !== undefined) (holder.querySelector('input, select') as HTMLInputElement).value = value;
        extraHost.appendChild(holder);
      }
    });

    const email = this.emailField(config);
    const subject = this.input('subject', t.subject, 'text', { maxlength: '255' });
    const description = this.textarea('description', t.description, 5000);
    if (preset.category && this.categoryById(preset.category)) {
      categorySelect.input.value = preset.category;
      categorySelect.input.dispatchEvent(new Event('change'));
      subcategorySelect.input.value = preset.subcategory ?? '';
    }
    subject.input.value = preset.subject ?? '';

    // Две колонки: слева что и от кого, справа сам текст. На узком экране
    // колонки складываются в одну (см. стили .sg-columns).
    const left = el('div', { class: 'sg-col' }, [categorySelect.field, subcategorySelect.field, extraHost]);
    if (email) left.appendChild(email.field);
    const right = el('div', { class: 'sg-col sg-col-text' }, [
      subject.field,
      description.field,
      this.attachmentsField(),
    ]);
    this.body.appendChild(el('div', { class: 'sg-columns' }, [left, right]));

    if (this.options.onCancel) {
      const cancel = el('button', { type: 'button', class: 'sg-button' }, [t.cancel]);
      cancel.addEventListener('click', () => this.options.onCancel?.());
      this.footer.appendChild(cancel);
    }

    const submit = el('button', { type: 'button', class: 'sg-button sg-primary' }, [t.submit]);
    const fields = { category: categorySelect, subcategory: subcategorySelect, email, subject, description, extraHost };
    submit.addEventListener('click', () => { void this.submit(fields); });
    this.submitButton = submit;
    this.footer.appendChild(submit);
    for (const id of ['sg-category', 'sg-subcategory', 'sg-subject', 'sg-description', 'sg-email']) {
      const input = this.body.querySelector<HTMLInputElement>(`[id="${id}"]`);
      const value = this.draft[id];
      if (input && value !== undefined && !input.readOnly) {
        input.value = value;
        if (id === 'sg-category') input.dispatchEvent(new Event('change'));
      }
    }
    this.body.querySelectorAll<HTMLInputElement>('[data-field^="extra."] input, [data-field^="extra."] select').forEach((input) => {
      if (this.draft[input.id] !== undefined) input.value = this.draft[input.id];
    });
    this.applyCompact(fields);
    categorySelect.input.addEventListener('change', () => this.applyCompact(fields));
    this.rendered = true;
    this.addFiles(this.draftFiles, this.body.querySelector('.sg-files') as HTMLElement);
    this.draftFiles = [];
  }

  // --- поля -----------------------------------------------------------------

  private applyCompact(fields: SubmitFields): void {
    if (!this.options.compact) return;
    this.body.querySelector('.sg-preset')?.remove();
    const summary = el('div', { class: 'sg-preset' });
    const preset = this.options.preset ?? {};
    const hideKnown = (field: Field, expected?: string): boolean => {
      const value = field.input.value.trim();
      const max = Number(field.input.getAttribute('maxlength'));
      field.field.hidden = !!expected?.trim() && value === expected.trim() && (!max || value.length <= max);
      return field.field.hidden;
    };
    const addSummary = (field: Field, text: string, tag: 'h3' | 'p' = 'p') => {
      summary.appendChild(el(tag, { 'data-preset-field': field.id }, [text]));
    };
    const fixedCategory = hideKnown(fields.category, preset.category);
    hideKnown(fields.subcategory, fixedCategory ? preset.subcategory : undefined);
    if (hideKnown(fields.subject, preset.subject)) addSummary(fields.subject, fields.subject.input.value.trim(), 'h3');
    const category = this.categoryById(fields.category.input.value);
    for (const holder of Array.from(fields.extraHost.querySelectorAll<HTMLElement>('.sg-field'))) {
      const id = holder.getAttribute('data-field')!;
      const name = id.replace(/^extra\./, '');
      const input = holder.querySelector('input, select') as HTMLInputElement;
      const field = { field: holder, input, id };
      const expected = fixedCategory ? preset.extra?.[name] : undefined;
      if (hideKnown(field, expected)) {
        const definition = category?.extra_fields?.find((item) => item.id === name);
        const value = definition?.options?.find((item) => item.id === input.value)?.label ?? input.value;
        addSummary(field, `${definition?.label ?? name}: ${value}`);
      }
    }
    if (fields.email) {
      fields.email.field.hidden = fields.email.input.readOnly && /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(fields.email.input.value);
      if (fields.email.field.hidden) addSummary(fields.email, `${this.text.email}: ${fields.email.input.value}`);
    }
    if (summary.childElementCount) this.body.insertBefore(summary, this.body.firstChild);
  }

  private revealField(holder: HTMLElement, id: string): void {
    holder.hidden = false;
    this.body.querySelector(`[data-preset-field="${cssEscape(id)}"]`)?.remove();
  }

  private wrap(id: string, label: string, control: HTMLElement): Field {
    const field = el('div', { class: 'sg-field', 'data-field': id }, [
      el('label', { for: `sg-${id}` }, [label]),
      control,
    ]);
    return { field, input: control as HTMLInputElement, id };
  }

  private input(id: string, label: string, type: string, attrs: Record<string, string> = {}): Field {
    const input = el('input', { id: `sg-${id}`, type, ...attrs });
    return this.wrap(id, label, input);
  }

  private textarea(id: string, label: string, maxLength: number): Field {
    const area = el('textarea', { id: `sg-${id}`, maxlength: String(maxLength) });
    return this.wrap(id, label, area as unknown as HTMLElement);
  }

  private select(id: string, label: string, options: { id: string; label: string }[]): Field {
    const select = el('select', { id: `sg-${id}` });
    fillOptions(select, options);
    return this.wrap(id, label, select);
  }

  private emailField(config: FormConfig): Field | null {
    const field = this.input('email', this.text.email, 'email', { maxlength: '320' });
    if (config.prefill.email) {
      (field.input as HTMLInputElement).value = config.prefill.email;
    }
    if (config.prefill.email_locked) {
      // Адрес подтверждён бэкендом игры — показываем, но править не даём.
      field.input.setAttribute('readonly', 'readonly');
    }
    field.field.appendChild(el('div', { class: 'sg-hint' }, [this.text.emailHint]));
    return field;
  }

  private extraField(field: ExtraField): HTMLElement {
    const id = `extra.${field.id}`;
    if (field.type === 'select') {
      const control = this.select(id, field.label, [
        { id: '', label: this.text.choose },
        ...(field.options ?? []),
      ]);
      control.field.setAttribute('data-required', String(field.required));
      return control.field;
    }
    const attrs: Record<string, string> = {};
    if (field.max_length) attrs.maxlength = String(field.max_length);
    const control = this.input(id, field.label, field.type === 'date' ? 'date' : 'text', attrs);
    control.field.setAttribute('data-required', String(field.required));
    return control.field;
  }

  private attachmentsField(): HTMLElement {
    const limits = (this.config as FormConfig).attachments;
    const t = this.text;

    const picker = el('input', {
      id: 'sg-attachments',
      type: 'file',
      multiple: 'multiple',
      accept: limits.allowed_types.join(','),
      class: 'sg-picker',
    }) as HTMLInputElement;
    const list = el('ul', { class: 'sg-files' });
    // Зона загрузки — такая же плитка, как превью файлов, и стоит в одном
    // ряду с ними: ряд не растёт по высоте, пока файлов не больше пяти.
    const drop = el('li', { class: 'sg-drop', role: 'button', tabindex: '0' }, [
      icon('upload'),
      el('span', { class: 'sg-drop-text' }, [t.addFiles]),
    ]);
    list.appendChild(drop);

    const field = el('div', { class: 'sg-field', 'data-field': 'attachments' }, [
      el('label', { for: 'sg-attachments' }, [t.attachments]),
      picker,
      list,
      el('div', { class: 'sg-hint' }, [
        `${t.dropHint}. ${t.attachmentsHint(limits.max_files, Math.floor(limits.max_file_size / 1024 / 1024))}`,
      ]),
    ]);

    const accept = (files: File[]) => this.addFiles(files, list);

    picker.addEventListener('change', () => {
      accept(Array.prototype.slice.call(picker.files ?? []) as File[]);
      picker.value = '';
    });
    drop.addEventListener('click', () => picker.click());
    drop.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        picker.click();
      }
    });
    drop.addEventListener('dragover', (event) => {
      event.preventDefault();
      drop.classList.add('sg-drop-active');
    });
    drop.addEventListener('dragleave', () => drop.classList.remove('sg-drop-active'));
    drop.addEventListener('drop', (event) => {
      event.preventDefault();
      drop.classList.remove('sg-drop-active');
      accept(Array.prototype.slice.call(event.dataTransfer?.files ?? []) as File[]);
    });

    // Скриншот из буфера: Cmd/Ctrl+V где угодно в окне. Обычный текст в
    // поля вставляется как раньше — перехватываем только когда есть файлы.
    const onPaste = (event: Event) => {
      if (!this.host.isConnected) {
        document.removeEventListener('paste', onPaste);
        return;
      }
      if (!this.host.contains(event.target as Node) && event.target !== document.body) return;
      const files = clipboardFiles(event as ClipboardEvent);
      if (files.length === 0) return;
      event.preventDefault();
      accept(files);
    };
    document.addEventListener('paste', onPaste);
    this.onPaste = onPaste;

    return field;
  }

  private addFiles(files: File[], list: HTMLElement): void {
    if (this.targetLocked || this.sent || this.disposed) return;
    const limits = (this.config as FormConfig).attachments;
    for (const file of files) {
      if (this.files.length >= limits.max_files) {
        this.showBanner(this.text.tooManyFiles(limits.max_files));
        return;
      }
      if (limits.allowed_types.length > 0 && limits.allowed_types.indexOf(file.type) < 0) {
        this.showBanner(`${file.name}: ${this.text.unsupportedType}`);
        continue;
      }
      this.addFile(file, list);
    }
  }

  private addFile(file: File, list: HTMLElement): void {
    const status = el('span', { class: 'sg-size' }, [this.text.uploading]);
    const retry = el('button', { type: 'button', class: 'sg-button', hidden: 'hidden' }, [this.text.retry]);
    const remove = el('button', { type: 'button', class: 'sg-remove', 'aria-label': this.text.remove, title: this.text.remove }, [icon('close')]);
    const previewUrl = objectUrl(file);
    const preview = el('div', { class: 'sg-thumb' });
    if (previewUrl && file.type.indexOf('image/') === 0) {
      const img = el('img', { src: previewUrl, alt: '' });
      // Битый или неподдерживаемый браузером файл — вместо пустой картинки расширение.
      img.addEventListener('error', () => {
        clear(preview);
        preview.appendChild(el('span', { class: 'sg-thumb-ext' }, [extension(file.name)]));
      });
      preview.appendChild(img);
    } else if (previewUrl && file.type.indexOf('video/') === 0) {
      preview.appendChild(el('video', { src: previewUrl, muted: 'muted', playsinline: 'playsinline' }));
    } else {
      preview.appendChild(el('span', { class: 'sg-thumb-ext' }, [extension(file.name)]));
    }
    const row = el('li', { class: 'sg-file sg-file-pending' }, [
      preview,
      el('div', { class: 'sg-file-meta' }, [
        el('span', { class: 'sg-name', title: file.name }, [file.name]),
        status,
        retry,
      ]),
      remove,
    ]) as HTMLLIElement;
    const entry: FileRow = { file, row, attachment: null, previewUrl };

    const drop = () => {
      this.files = this.files.filter((item) => item !== entry);
      row.remove();
      if (entry.previewUrl) revokeUrl(entry.previewUrl);
    };
    remove.addEventListener('click', drop);

    this.files.push(entry);
    // Плитка «добавить» всегда последняя.
    list.insertBefore(row, list.querySelector('.sg-drop'));

    const signal = this.controller.signal;
    const upload = () => {
      if (signal.aborted || this.targetLocked || !this.files.includes(entry)) return;
      retry.hidden = true;
      row.classList.add('sg-file-pending');
      status.textContent = this.text.uploading;
      this.client.upload(file, signal).then(
        (attachment) => {
          if (signal.aborted || !this.files.includes(entry)) return;
          entry.attachment = attachment;
          row.classList.remove('sg-file-pending');
          status.textContent = humanSize(file.size);
        },
        (error: unknown) => {
          if (signal.aborted || !this.files.includes(entry)) return;
          row.classList.remove('sg-file-pending');
          status.textContent = this.describe(error);
          retry.hidden = false;
          this.showBanner(`${file.name}: ${this.describe(error)}`);
        },
      );
    };
    retry.addEventListener('click', upload);
    upload();
  }

  // --- отправка -------------------------------------------------------------

  private async submit(fields: SubmitFields): Promise<void> {
    if (this.submitting) return;

    const draft: TicketDraft | null = this.pendingDraft ?? this.collect(fields);
    if (!draft) return;

    if (!this.pendingDraft && this.files.some((item) => !item.attachment)) {
      this.showBanner(this.text.uploading);
      return;
    }
    if (!this.pendingDraft) {
      draft.attachment_ids = this.files.map((item) => (item.attachment as Attachment).attachment_id);
      draft.client = collectClientInfo();
    }

    if (!this.idempotencyKey) {
      this.idempotencyKey = randomKey();
    }

    this.setSubmitting(true);
    this.targetLocked = true;
    this.pendingDraft = draft;
    this.body.querySelector('.sg-columns')?.setAttribute('inert', '');
    if (this.targetSelect) this.targetSelect.disabled = true;
    try {
      const ticket = await this.client.submit(draft, this.idempotencyKey, this.controller.signal);
      this.sent = true;
      this.body.querySelector('.sg-columns')?.removeAttribute('inert');
      this.options.onSubmitted?.(ticket);
      this.options.onResult?.({
        ...(this.targetId ? { targetId: this.targetId } : {}),
        status: 'sent',
        ticket,
        ticketId: ticket.ticket_id,
        issueKey: ticket.issue_key,
      });
      if (!this.disposed) this.showSent(ticket);
    } catch (error) {
      if (this.disposed) return;
      // An explicit 4xx rejects the request. Network/5xx/parse may have saved it.
      if (error instanceof SupportGateError && error.kind === 'http'
        && error.status >= 400 && error.status < 500) {
        this.targetLocked = false;
        this.pendingDraft = null;
        this.body.querySelector('.sg-columns')?.removeAttribute('inert');
        if (this.targetSelect) this.targetSelect.disabled = false;
      }
      this.setSubmitting(false);
      this.showBanner(this.describe(error));
      this.markFields(error);
      this.report('submit', error);
    }
  }

  private collect(fields: SubmitFields): Draft | null {
    const t = this.text;
    this.clearErrors();

    let invalid: HTMLElement | null = null;
    const fail = (field: Field, message: string) => {
      this.revealField(field.field, field.id);
      field.field.classList.add('sg-invalid');
      field.field.appendChild(el('div', { class: 'sg-error' }, [message]));
      invalid = invalid ?? field.field;
    };

    const category = fields.category.input.value;
    if (!category) fail(fields.category, t.required);
    const subcategory = fields.subcategory.input.value;
    if (!subcategory) fail(fields.subcategory, t.required);

    const subject = fields.subject.input.value.trim();
    if (!subject) fail(fields.subject, t.required);
    const description = (fields.description.input as unknown as HTMLTextAreaElement).value.trim();
    if (!description) fail(fields.description, t.required);

    let email: string | undefined;
    if (fields.email) {
      email = fields.email.input.value.trim();
      if (!email) {
        fail(fields.email, t.required);
      } else if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) {
        fail(fields.email, t.invalidEmail);
      }
    }

    const extra: Record<string, string> = {};
    const extraFields = fields.extraHost.querySelectorAll('.sg-field');
    for (let i = 0; i < extraFields.length; i += 1) {
      const holder = extraFields[i] as HTMLElement;
      const name = (holder.getAttribute('data-field') ?? '').replace(/^extra\./, '');
      const control = holder.querySelector('input, select') as HTMLInputElement | null;
      const value = control ? control.value.trim() : '';
      if (value) {
        extra[name] = value;
      } else if (holder.getAttribute('data-required') === 'true') {
        fail({ field: holder, input: control as HTMLInputElement, id: name }, t.required);
      }
    }

    if (invalid) {
      scrollTo(invalid as HTMLElement);
      return null;
    }
    return { category, subcategory, subject, description, email, extra, attachment_ids: [] };
  }

  private markFields(error: unknown): void {
    if (!(error instanceof SupportGateError)) return;

    for (const name of Object.keys(error.fields)) {
      const holder = this.body.querySelector<HTMLElement>(`[data-field="${cssEscape(name)}"]`);
      if (!holder) continue;
      this.revealField(holder, name);
      holder.classList.add('sg-invalid');
      holder.appendChild(el('div', { class: 'sg-error' }, [error.fields[name]]));
    }
  }

  private clearErrors(): void {
    this.banner?.remove();
    this.banner = null;
    const errors = this.body.querySelectorAll('.sg-error');
    for (let i = 0; i < errors.length; i += 1) errors[i].remove();
    const invalid = this.body.querySelectorAll('.sg-invalid');
    for (let i = 0; i < invalid.length; i += 1) invalid[i].classList.remove('sg-invalid');
  }

  private setSubmitting(active: boolean): void {
    this.submitting = active;
    if (!this.submitButton) return;
    this.submitButton.disabled = active;
    this.submitButton.textContent = active ? this.text.submitting
      : this.pendingDraft ? this.text.retry : this.text.submit;
  }

  private showBanner(message: string): void {
    this.banner?.remove();
    this.banner = el('div', { class: 'sg-banner', role: 'alert' }, [message]);
    this.body.insertBefore(this.banner, this.body.firstChild);
    scrollTo(this.banner);
  }

  private showSent(ticket: Ticket): void {
    const close = el('button', { type: 'button', class: 'sg-button sg-primary' }, [
      this.text.close,
    ]);
    close.addEventListener('click', () => this.options.onCancel?.());
    this.showState(
      this.text.sentTitle,
      this.text.sentText(ticket.issue_key || ticket.ticket_id),
      this.options.onCancel ? [close] : [],
    );
    if (!ticket.issue_key) {
      void this.awaitIssueKey(ticket.ticket_id);
    }
  }

  /**
   * Номер Jira появляется, когда воркер доставит обращение — обычно через
   * секунды. Пока экран «отправлено» открыт, подменяем tkt_… на SUP-…; не
   * дождались — остаётся внутренний номер, по нему поддержка тоже найдёт.
   */
  private async awaitIssueKey(ticketId: string): Promise<void> {
    const description = this.body.querySelector('.sg-state p');
    for (let attempt = 0; attempt < ISSUE_KEY_POLLS; attempt += 1) {
      await new Promise((resolve) => setTimeout(resolve, ISSUE_KEY_POLL_MS));
      if (this.disposed || !description || !description.isConnected) return;
      let ticket: Ticket;
      try {
        ticket = await this.client.ticket(ticketId, this.controller.signal);
      } catch {
        return;
      }
      if (ticket.issue_key) {
        if (description.isConnected) {
          description.textContent = this.text.sentText(ticket.issue_key);
        }
        return;
      }
    }
  }

  /** Игре — сырые данные ошибки, без перевода: ей их логировать, не показывать. */
  private report(stage: 'load' | 'submit', error: unknown): void {
    if (!this.options.onResult) return;
    const known = error instanceof SupportGateError ? error : null;
    this.options.onResult({
      ...(this.targetId ? { targetId: this.targetId } : {}),
      status: 'error',
      stage,
      message: known?.message || String(error),
      kind: known?.kind ?? null,
      httpStatus: known?.status ?? 0,
      code: known?.code ?? null,
    });
  }

  /** Человеческий текст ошибки: коды сервиса переводим, остальное — как есть. */
  private describe(error: unknown): string {
    if (!(error instanceof SupportGateError)) return String(error);
    if (error.kind === 'network') return this.text.offline;
    if (error.status === 429) return this.text.rateLimited;
    if (error.status === 401) return this.text.sessionExpired;
    return error.message || this.text.failedTitle;
  }

  private categoryById(id: string): Category | undefined {
    return (this.config as FormConfig).categories.filter((c) => c.id === id)[0];
  }
}

interface Field {
  field: HTMLElement;
  input: HTMLInputElement;
  id: string;
}

interface SubmitFields {
  category: Field;
  subcategory: Field;
  email: Field | null;
  subject: Field;
  description: Field;
  extraHost: HTMLElement;
}

interface Draft {
  category: string;
  subcategory: string;
  subject: string;
  description: string;
  email?: string;
  extra: Record<string, string>;
  attachment_ids: string[];
}

function fillOptions(select: HTMLElement, options: { id: string; label: string }[]): void {
  clear(select);
  for (const option of options) {
    select.appendChild(el('option', { value: option.id }, [option.label]));
  }
}

/** Файлы из буфера обмена: скриншот приходит как image/png без имени. */
function clipboardFiles(event: ClipboardEvent): File[] {
  const data = event.clipboardData;
  if (!data) return [];
  const files = Array.prototype.slice.call(data.files ?? []) as File[];
  if (files.length > 0) return files.map(nameClipboardFile);
  const items = Array.prototype.slice.call(data.items ?? []) as DataTransferItem[];
  const result: File[] = [];
  for (const item of items) {
    if (item.kind !== 'file') continue;
    const file = item.getAsFile();
    if (file) result.push(nameClipboardFile(file));
  }
  return result;
}

function nameClipboardFile(file: File): File {
  if (file.name && file.name !== 'image.png') return file;
  const ext = (file.type.split('/')[1] || 'png').replace(/[^a-z0-9]/gi, '');
  const name = `screenshot-${new Date().toISOString().replace(/[:.]/g, '-')}.${ext}`;
  try {
    return new File([file], name, { type: file.type });
  } catch {
    return file;
  }
}

function objectUrl(file: File): string | null {
  if (typeof URL === 'undefined' || typeof URL.createObjectURL !== 'function') return null;
  try {
    return URL.createObjectURL(file);
  } catch {
    return null;
  }
}

function revokeUrl(url: string): void {
  if (typeof URL.revokeObjectURL === 'function') URL.revokeObjectURL(url);
}

function extension(name: string): string {
  const match = /\.([a-z0-9]{1,5})$/i.exec(name);
  return match ? match[1].toUpperCase() : 'FILE';
}

/** Имена полей приходят с сервера — в селектор их без экранирования нельзя. */
function cssEscape(value: string): string {
  return value.replace(/["\\]/g, '\\$&');
}
