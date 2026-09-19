// Подписи самого окна. Переводы живут на шлюзе (locales/<locale>.json) и
// приходят вместе с /v1/form уже на языке игрока; в бандле только английский —
// на экран загрузки и на случай, когда форму получить не удалось.
// Категории и поля переводит сервер сам, здесь их нет.
//
// en.json — копия каталога шлюза, чтобы sdk/ собирался отдельно от сервера;
// совпадение проверяет tests/test_i18n.py.

import EN from './en.json';
import { format, FormatValues } from './format';

export type Catalog = Record<string, string>;

export interface Strings {
  title: string;
  category: string;
  subcategory: string;
  choose: string;
  email: string;
  emailHint: string;
  subject: string;
  description: string;
  attachments: string;
  attachmentsHint: (max: number, mb: number) => string;
  addFiles: string;
  dropHint: string;
  unsupportedType: string;
  tooManyFiles: (max: number) => string;
  remove: string;
  uploading: string;
  submit: string;
  submitting: string;
  cancel: string;
  close: string;
  required: string;
  invalidEmail: string;
  loading: string;
  loadFailed: string;
  retry: string;
  sentTitle: string;
  sentText: (id: string) => string;
  failedTitle: string;
  rateLimited: string;
  sessionExpired: string;
  offline: string;
}

const FALLBACK: Catalog = EN;

/**
 * Собирает подписи из каталога сервера. Ключа нет или он пустой — берётся
 * английский: неполный перевод не должен оставлять пустых кнопок.
 */
export function strings(catalog: Catalog = {}, locale = 'en'): Strings {
  const t = (key: string, values?: FormatValues): string =>
    format(catalog[`ui.${key}`] || FALLBACK[`ui.${key}`] || key, values, locale);

  return {
    title: t('title'),
    category: t('category'),
    subcategory: t('subcategory'),
    choose: t('choose'),
    email: t('email'),
    emailHint: t('email_hint'),
    subject: t('subject'),
    description: t('description'),
    attachments: t('attachments'),
    attachmentsHint: (max, mb) => t('attachments_hint', { max, mb }),
    addFiles: t('add_files'),
    dropHint: t('drop_hint'),
    unsupportedType: t('unsupported_type'),
    tooManyFiles: (max) => t('too_many_files', { max }),
    remove: t('remove'),
    uploading: t('uploading'),
    submit: t('submit'),
    submitting: t('submitting'),
    cancel: t('cancel'),
    close: t('close'),
    required: t('required'),
    invalidEmail: t('invalid_email'),
    loading: t('loading'),
    loadFailed: t('load_failed'),
    retry: t('retry'),
    sentTitle: t('sent_title'),
    sentText: (id) => t('sent_text', { id }),
    failedTitle: t('failed_title'),
    rateLimited: t('rate_limited'),
    sessionExpired: t('session_expired'),
    offline: t('offline'),
  };
}
