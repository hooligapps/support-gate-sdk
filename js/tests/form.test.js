const assert = require('node:assert/strict');
const { test } = require('node:test');

const { setup, json, until } = require('./harness');

async function openForm(routes) {
  const context = setup(routes);
  const { SupportGate, dom } = context;
  SupportGate.init({ endpoint: 'https://support.example', token: 't' });
  const host = dom.window.document.createElement('div');
  dom.window.document.body.appendChild(host);

  await SupportGate.mount(host);
  return Object.assign(context, { host, $: (sel) => host.querySelector(sel) });
}

function setValue(control, value) {
  control.value = value;
  control.dispatchEvent(new globalThis.Event('change'));
}

test('форма рисует категории из ответа сервера', async () => {
  const { $ } = await openForm();

  const options = Array.from($('#sg-category').options).map((o) => o.value);
  assert.deepEqual(options, ['', 'gameplay', 'payment']);
});

test('подкатегории и доп. поля появляются после выбора категории', async () => {
  const { $, host } = await openForm();

  setValue($('#sg-category'), 'payment');

  assert.deepEqual(
    Array.from($('#sg-subcategory').options).map((o) => o.value),
    ['', 'refund'],
  );
  assert.ok(host.querySelector('[data-field="extra.payment_method"]'));

  setValue($('#sg-category'), 'gameplay');
  assert.equal(host.querySelector('[data-field="extra.payment_method"]'), null);
});

test('пустые обязательные поля не дают отправить', async () => {
  const { $, host, calls } = await openForm();

  $('.sg-footer .sg-primary').click();

  assert.equal(calls.filter((c) => c.key === 'POST /v1/tickets').length, 0);
  assert.ok(host.querySelectorAll('.sg-invalid').length >= 3);
});

test('подтверждённая почта показана, но не редактируется', async () => {
  const { $ } = await openForm({
    'GET /v1/form': () =>
      json(200, {
        ...require('./harness').FORM,
        prefill: { email: 'player@example.com', email_locked: true },
      }),
  });

  assert.equal($('#sg-email').value, 'player@example.com');
  assert.ok($('#sg-email').hasAttribute('readonly'));
});

test('заполненная форма отправляется одним запросом', async () => {
  const { $, calls } = await openForm();

  setValue($('#sg-category'), 'gameplay');
  $('#sg-subcategory').value = 'progress';
  $('#sg-email').value = 'player@example.com';
  $('#sg-subject').value = 'Прогресс пропал';
  $('#sg-description').value = 'После обновления сбросился уровень';
  $('.sg-footer .sg-primary').click();

  await until(() => calls.some((c) => c.key === 'POST /v1/tickets'), 'отправку обращения');

  const call = calls.filter((c) => c.key === 'POST /v1/tickets')[0];
  assert.deepEqual(call.body.category, 'gameplay');
  assert.deepEqual(call.body.subcategory, 'progress');
  assert.equal(call.body.email, 'player@example.com');
  assert.deepEqual(call.body.attachment_ids, []);
});

test('после успеха показан номер обращения', async () => {
  const { $, host, calls } = await openForm();

  setValue($('#sg-category'), 'gameplay');
  $('#sg-subcategory').value = 'progress';
  $('#sg-email').value = 'player@example.com';
  $('#sg-subject').value = 's';
  $('#sg-description').value = 'd';
  $('.sg-footer .sg-primary').click();

  await until(() => host.querySelector('.sg-state'), 'экран «отправлено»');
  assert.match(host.querySelector('.sg-state').textContent, /SUP-1/);
  assert.equal(calls.filter((c) => c.key === 'POST /v1/tickets').length, 1);
});

test('номер Jira подставляется, когда воркер доставит обращение', async () => {
  let polls = 0;
  const { $, host, calls } = await openForm({
    'POST /v1/tickets': () =>
      json(201, { ticket_id: 'tkt-1', issue_key: null, status: 'queued', created_at: '' }),
    'GET /v1/tickets/tkt-1': () => {
      polls += 1;
      return json(200, {
        ticket_id: 'tkt-1',
        issue_key: polls < 2 ? null : 'SUP-7',
        status: polls < 2 ? 'queued' : 'delivered',
        created_at: '',
      });
    },
  });
  // Опрос идёт раз в 2 с — в тесте паузы сжимаем.
  const realSetTimeout = globalThis.setTimeout;
  globalThis.setTimeout = (fn, ms, ...rest) => realSetTimeout(fn, ms >= 1000 ? 1 : ms, ...rest);
  try {
    setValue($('#sg-category'), 'gameplay');
    $('#sg-subcategory').value = 'progress';
    $('#sg-email').value = 'player@example.com';
    $('#sg-subject').value = 's';
    $('#sg-description').value = 'd';
    $('.sg-footer .sg-primary').click();

    await until(() => /SUP-7/.test(host.querySelector('.sg-state')?.textContent), 'номер Jira');
    await new Promise((resolve) => realSetTimeout(resolve, 20));
  } finally {
    globalThis.setTimeout = realSetTimeout;
  }
  assert.equal(calls.filter((c) => c.key === 'GET /v1/tickets/tkt-1').length, 2);
});

test('повтор после ошибки идёт с тем же ключом идемпотентности', async () => {
  let failures = 1;
  const { $, calls } = await openForm({
    'POST /v1/tickets': () => {
      if (failures > 0) {
        failures -= 1;
        return json(503, { error: 'unavailable', message: 'сервис недоступен' });
      }
      return json(200, {
        ticket_id: 'tkt-1',
        issue_key: 'SUP-1',
        status: 'queued',
        created_at: '2026-01-01T00:00:00Z',
      });
    },
  });

  setValue($('#sg-category'), 'gameplay');
  $('#sg-subcategory').value = 'progress';
  $('#sg-email').value = 'player@example.com';
  $('#sg-subject').value = 's';
  $('#sg-description').value = 'd';

  $('.sg-footer .sg-primary').click();
  await until(() => calls.filter((c) => c.key === 'POST /v1/tickets').length === 1, 'первую попытку');
  $('.sg-footer .sg-primary').click();
  await until(() => calls.filter((c) => c.key === 'POST /v1/tickets').length === 2, 'вторую попытку');

  const [first, second] = calls.filter((c) => c.key === 'POST /v1/tickets');
  assert.equal(first.init.headers['Idempotency-Key'], second.init.headers['Idempotency-Key']);
});

test('ошибка сервиса показывается баннером и у поля', async () => {
  const { $, host } = await openForm({
    'POST /v1/tickets': () =>
      json(422, {
        detail: {
          error: 'validation_error',
          message: 'Тема слишком короткая',
          fields: { subject: 'минимум 5 символов' },
        },
      }),
  });

  setValue($('#sg-category'), 'gameplay');
  $('#sg-subcategory').value = 'progress';
  $('#sg-email').value = 'player@example.com';
  $('#sg-subject').value = 's';
  $('#sg-description').value = 'd';
  $('.sg-footer .sg-primary').click();

  await until(() => host.querySelector('.sg-banner'), 'баннер с ошибкой');
  assert.match(host.querySelector('.sg-banner').textContent, /Тема слишком короткая/);
  assert.match(host.querySelector('[data-field="subject"]').textContent, /минимум 5 символов/);
});

test('модальное окно закрывается по Esc и убирает себя из страницы', async () => {
  const { SupportGate, dom } = setup();
  SupportGate.init({ endpoint: 'https://support.example', token: 't' });

  await SupportGate.open();
  assert.ok(dom.window.document.querySelector('.sg-root'));

  dom.window.document.dispatchEvent(
    new dom.window.KeyboardEvent('keydown', { key: 'Escape' }),
  );

  assert.equal(dom.window.document.querySelector('.sg-root'), null);
});

test('onResult получает sent, error и closed', async () => {
  let failures = 1;
  const { SupportGate, dom } = setup({
    'POST /v1/tickets': () => {
      if (failures > 0) {
        failures -= 1;
        return json(503, { detail: { error: 'unavailable', message: 'сервис недоступен' } });
      }
      return json(200, {
        ticket_id: 'tkt-1',
        issue_key: 'SUP-1',
        status: 'queued',
        created_at: '2026-01-01T00:00:00Z',
      });
    },
  });
  SupportGate.init({ endpoint: 'https://support.example', token: 't' });
  const results = [];
  const doc = dom.window.document;
  const $ = (sel) => doc.querySelector(sel);

  // Закрыли, не отправив.
  await SupportGate.open({ onResult: (r) => results.push(r) });
  SupportGate.close();
  assert.deepEqual(results, [{ status: 'closed' }]);

  // Ошибка, потом успех; после успеха закрытие уже не «closed».
  results.length = 0;
  await SupportGate.open({ onResult: (r) => results.push(r) });
  setValue($('#sg-category'), 'gameplay');
  $('#sg-subcategory').value = 'progress';
  $('#sg-email').value = 'player@example.com';
  $('#sg-subject').value = 's';
  $('#sg-description').value = 'd';
  $('.sg-footer .sg-primary').click();
  await until(() => results.length === 1, 'ошибку');
  assert.equal(results[0].status, 'error');
  assert.equal(results[0].stage, 'submit');
  assert.equal(results[0].httpStatus, 503);
  assert.equal(results[0].code, 'unavailable');
  assert.match(results[0].message, /сервис недоступен/);

  $('.sg-footer .sg-primary').click();
  await until(() => results.length === 2, 'успех');
  assert.equal(results[1].status, 'sent');
  assert.equal(results[1].ticketId, 'tkt-1');
  assert.equal(results[1].issueKey, 'SUP-1');
  SupportGate.close();
  assert.equal(results.length, 2);
});

test('onResult сообщает, что справочник не загрузился', async () => {
  const { SupportGate } = setup({ 'GET /v1/form': () => json(500, { detail: { error: 'internal_error', message: 'boom' } }) });
  SupportGate.init({ endpoint: 'https://support.example', token: 't' });
  const results = [];
  await SupportGate.open({ onResult: (r) => results.push(r) });
  await until(() => results.length === 1, 'ошибку загрузки');
  assert.equal(results[0].status, 'error');
  assert.equal(results[0].stage, 'load');
  assert.equal(results[0].httpStatus, 500);
});

// jsdom не умеет DataTransfer: событие собираем руками, форма читает только .files/.items.
function fileEvent(dom, type, files) {
  const event = new dom.window.Event(type, { bubbles: true, cancelable: true });
  const payload = { files, items: [] };
  Object.defineProperty(event, type === 'paste' ? 'clipboardData' : 'dataTransfer', { value: payload });
  return event;
}

test('файл, брошенный в зону, загружается и показан плиткой', async () => {
  const { $, dom, calls } = await openForm();
  const file = new dom.window.File(['png'], 'shot.png', { type: 'image/png' });

  $('.sg-drop').dispatchEvent(fileEvent(dom, 'drop', [file]));

  await until(() => calls.some((c) => c.key === 'PUT https://storage.example/att-1'), 'загрузку файла');
  await until(() => $('.sg-file:not(.sg-file-pending)'), 'плитку после загрузки');
  assert.equal($('.sg-file .sg-name').textContent, 'shot.png');
  assert.equal($('.sg-files').querySelectorAll('.sg-file').length, 1);
});

test('скриншот из буфера попадает во вложения, чужие типы — нет', async () => {
  const { $, host, dom, calls } = await openForm();
  const shot = new dom.window.File(['png'], 'image.png', { type: 'image/png' });
  const doc = new dom.window.File(['pdf'], 'x.pdf', { type: 'application/pdf' });

  $('#sg-description').dispatchEvent(fileEvent(dom, 'paste', [shot, doc]));

  await until(() => $('.sg-file'), 'плитку вставленного файла');
  assert.match($('.sg-file .sg-name').textContent, /^screenshot-.*\.png$/);
  assert.equal($('.sg-files').querySelectorAll('.sg-file').length, 1);
  assert.match(host.querySelector('.sg-banner').textContent, /x\.pdf/);
  await until(() => calls.some((c) => c.key === 'POST /v1/attachments/presign'), 'presign');
});

test('удалённая плитка не уходит с обращением', async () => {
  const { $, dom, calls } = await openForm();
  const file = new dom.window.File(['png'], 'shot.png', { type: 'image/png' });
  $('.sg-drop').dispatchEvent(fileEvent(dom, 'drop', [file]));
  await until(() => $('.sg-file:not(.sg-file-pending)'), 'плитку после загрузки');

  $('.sg-remove').click();
  assert.equal($('.sg-files').querySelectorAll('.sg-file').length, 0);

  setValue($('#sg-category'), 'gameplay');
  $('#sg-subcategory').value = 'progress';
  $('#sg-email').value = 'player@example.com';
  $('#sg-subject').value = 's';
  $('#sg-description').value = 'd';
  $('.sg-footer .sg-primary').click();
  await until(() => calls.some((c) => c.key === 'POST /v1/tickets'), 'отправку');
  assert.deepEqual(calls.filter((c) => c.key === 'POST /v1/tickets')[0].body.attachment_ids, []);
});
