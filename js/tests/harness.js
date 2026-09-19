// Общая обвязка тестов: DOM из jsdom и подставной сервис вместо сети.
// Тесты гоняют собранный бандл — ровно то, что подключает игра.

const { JSDOM } = require('jsdom');

const BUNDLE = require.resolve('../dist/support_gate.js');

const FORM = {
  game: 'demo',
  locale: 'en',
  direction: 'ltr',
  strings: {},
  prefill: { email: null, email_locked: false },
  categories: [
    {
      id: 'gameplay',
      label: 'Gameplay',
      subcategories: [{ id: 'progress', label: 'Progress' }],
    },
    {
      id: 'payment',
      label: 'Payment',
      subcategories: [{ id: 'refund', label: 'Refund' }],
      extra_fields: [
        {
          id: 'payment_method',
          type: 'select',
          label: 'Payment Method',
          required: true,
          options: [{ id: 'apple', label: 'Apple' }],
        },
      ],
    },
  ],
  attachments: { max_files: 3, max_file_size: 5 * 1024 * 1024, allowed_types: ['image/png'] },
};

/**
 * Поднимает окружение браузера и подменённый fetch.
 * Возвращает SupportGate из бандла и журнал запросов.
 */
function setup(routes = {}) {
  const dom = new JSDOM('<!doctype html><html><head></head><body></body></html>', {
    url: 'https://game.example',
  });
  const calls = [];

  const handlers = Object.assign(
    {
      'GET /v1/form': () => json(200, FORM),
      'POST /v1/attachments/presign': () =>
        json(200, {
          attachment_id: 'att-1',
          upload_url: 'https://storage.example/att-1',
          method: 'PUT',
          headers: { 'Content-Type': 'image/png' },
          expires_in: 600,
        }),
      'PUT https://storage.example/att-1': () => json(200, null),
      'POST /v1/tickets': () =>
        json(200, {
          ticket_id: 'tkt-1',
          issue_key: 'SUP-1',
          status: 'queued',
          created_at: '2026-01-01T00:00:00Z',
        }),
    },
    routes,
  );

  const fetchImpl = async (url, init = {}) => {
    const method = init.method || 'GET';
    const path = url.indexOf('https://storage.example') === 0 ? url : url.replace(/^[^/]*\/\/[^/]*/, '');
    const key = `${method} ${path}`;
    // Тело файла — не JSON: разбираем только наши запросы к сервису.
    const body = typeof init.body === 'string' ? JSON.parse(init.body) : null;
    calls.push({ key, url, init, body });

    const handler = handlers[key];
    if (!handler) throw new Error(`нет обработчика для ${key}`);
    return handler(calls[calls.length - 1]);
  };

  for (const name of ['window', 'document', 'navigator', 'screen', 'Event', 'File', 'Blob']) {
    globalThis[name] = name === 'window' ? dom.window : dom.window[name];
  }
  globalThis.fetch = fetchImpl;

  delete require.cache[BUNDLE];
  const SupportGate = require(BUNDLE);
  return { SupportGate, calls, dom, form: FORM };
}

function json(status, payload) {
  const text = payload === null ? '' : JSON.stringify(payload);
  return {
    ok: status >= 200 && status < 300,
    status,
    text: async () => text,
  };
}

/** Ждёт, пока предикат станет истинным: загрузки и отправка асинхронные. */
async function until(predicate, message, attempts = 200) {
  for (let i = 0; i < attempts; i += 1) {
    if (predicate()) return;
    await new Promise((resolve) => setTimeout(resolve, 5));
  }
  throw new Error(`не дождались: ${message}`);
}

module.exports = { setup, json, until, FORM };
