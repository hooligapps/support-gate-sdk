const assert = require('node:assert/strict');
const { test } = require('node:test');

const { setup, json, FORM } = require('./harness');

test('токен уходит в заголовке, ключ игры клиенту не нужен', async () => {
  const { SupportGate, calls } = setup();
  const client = SupportGate.init({ endpoint: 'https://support.example/', token: 'jwt-123' });

  await client.form();

  assert.equal(calls[0].key, 'GET /v1/form');
  assert.equal(calls[0].init.headers.Authorization, 'Bearer jwt-123');
});

test('токен можно отдавать функцией — бэкенд игры его обновляет', async () => {
  const { SupportGate, calls } = setup();
  let issued = 0;
  const client = SupportGate.init({
    endpoint: 'https://support.example',
    token: async () => `jwt-${(issued += 1)}`,
  });

  await client.form();
  await client.submit({
    category: 'gameplay',
    subcategory: 'progress',
    subject: 's',
    description: 'd',
  });

  assert.equal(calls[0].init.headers.Authorization, 'Bearer jwt-1');
  assert.equal(calls[1].init.headers.Authorization, 'Bearer jwt-2');
});

test('справочник берётся один раз на сессию', async () => {
  const { SupportGate, calls } = setup();
  const client = SupportGate.init({ endpoint: 'https://support.example', token: 't' });

  await Promise.all([client.form(), client.form()]);
  await client.form();

  assert.equal(calls.filter((c) => c.key === 'GET /v1/form').length, 1);
});

test('неудачная загрузка справочника не залипает в кэше', async () => {
  let fail = true;
  const { SupportGate } = setup({
    'GET /v1/form': () => (fail ? json(503, { error: 'unavailable' }) : json(200, FORM)),
  });
  const client = SupportGate.init({ endpoint: 'https://support.example', token: 't' });

  await assert.rejects(() => client.form());
  fail = false;
  const config = await client.form();

  assert.equal(config.game, 'demo');
});

test('отправка несёт ключ идемпотентности и данные окружения', async () => {
  const { SupportGate, calls } = setup();
  const client = SupportGate.init({ endpoint: 'https://support.example', token: 't' });

  const ticket = await client.submit(
    { category: 'gameplay', subcategory: 'progress', subject: 's', description: 'd' },
    'key-1',
  );

  const call = calls[calls.length - 1];
  assert.equal(ticket.issue_key, 'SUP-1');
  assert.equal(call.init.headers['Idempotency-Key'], 'key-1');
  assert.ok(call.body.client.screen);
});

test('ошибка сервиса разворачивается в код и поля', async () => {
  const { SupportGate } = setup({
    'POST /v1/tickets': () =>
      json(422, {
        detail: { error: 'validation_error', message: 'плохо', fields: { subject: 'пусто' } },
      }),
  });
  const client = SupportGate.init({ endpoint: 'https://support.example', token: 't' });

  await assert.rejects(
    () => client.submit({ category: 'g', subcategory: 's', subject: 's', description: 'd' }),
    (error) => {
      assert.equal(error.kind, 'http');
      assert.equal(error.status, 422);
      assert.equal(error.code, 'validation_error');
      assert.equal(error.fields.subject, 'пусто');
      assert.equal(error.transient, false);
      return true;
    },
  );
});

test('429 и 5xx помечаются как временные', async () => {
  const { SupportGate } = setup({
    'POST /v1/tickets': () => json(429, { error: 'rate_limited', message: 'позже' }),
  });
  const client = SupportGate.init({ endpoint: 'https://support.example', token: 't' });

  await assert.rejects(
    () => client.submit({ category: 'g', subcategory: 's', subject: 's', description: 'd' }),
    (error) => error.transient === true,
  );
});

test('файл сверх лимита не доходит до сети', async () => {
  const { SupportGate, calls } = setup();
  const client = SupportGate.init({ endpoint: 'https://support.example', token: 't' });
  const big = new File([new Uint8Array(1024)], 'big.png', { type: 'image/png' });
  Object.defineProperty(big, 'size', { value: 6 * 1024 * 1024 });

  await assert.rejects(
    () => client.upload(big),
    (error) => error.code === 'file_too_large' && error.kind === 'invalid_request',
  );
  assert.equal(calls.filter((c) => c.key === 'POST /v1/attachments/presign').length, 0);
});

test('запрещённый тип файла отклоняется на клиенте', async () => {
  const { SupportGate } = setup();
  const client = SupportGate.init({ endpoint: 'https://support.example', token: 't' });
  const exe = new File(['x'], 'run.exe', { type: 'application/x-msdownload' });

  await assert.rejects(() => client.upload(exe), (error) => error.code === 'unsupported_type');
});

test('файл уходит прямо в хранилище, а сервису — только идентификатор', async () => {
  const { SupportGate, calls } = setup();
  const client = SupportGate.init({ endpoint: 'https://support.example', token: 't' });
  const png = new File(['x'], 'shot.png', { type: 'image/png' });

  const attachment = await client.upload(png);

  const put = calls.filter((c) => c.key === 'PUT https://storage.example/att-1');
  assert.equal(put.length, 1);
  assert.equal(put[0].init.headers['Content-Type'], 'image/png');
  assert.equal(attachment.attachment_id, 'att-1');
});

const DRAFT = { category: 'gameplay', subcategory: 'progress', subject: 's', description: 'd' };

function expiredUnless(token) {
  return (call) =>
    call.init.headers.Authorization === `Bearer ${token}`
      ? json(200, { ticket_id: 'tkt-1', issue_key: null, status: 'queued', created_at: '2026-01-01T00:00:00Z' })
      : json(401, { error: 'unauthorized', message: 'expired' });
}

test('на 401 игра спрашивается о новом токене, запрос повторяется с тем же ключом', async () => {
  const { SupportGate, calls } = setup({ 'POST /v1/tickets': expiredUnless('fresh') });
  const hints = [];
  const client = SupportGate.init({
    endpoint: 'https://support.example',
    token: (hint) => {
      hints.push(hint);
      return hint ? 'fresh' : 'stale';
    },
  });

  const ticket = await client.submit(DRAFT, 'key-1');

  assert.equal(ticket.ticket_id, 'tkt-1');
  assert.deepEqual(hints, [undefined, { reason: 'expired' }]);
  const posts = calls.filter((c) => c.key === 'POST /v1/tickets');
  assert.equal(posts.length, 2);
  assert.equal(posts[0].init.headers.Authorization, 'Bearer stale');
  assert.equal(posts[1].init.headers.Authorization, 'Bearer fresh');
  assert.equal(posts[1].init.headers['Idempotency-Key'], posts[0].init.headers['Idempotency-Key']);
});

test('токен строкой продлевается через шлюз и дальше используется новый', async () => {
  const { SupportGate, calls } = setup({
    'POST /v1/tickets': expiredUnless('renewed'),
    'POST /v1/session/refresh': (call) =>
      call.init.headers.Authorization === 'Bearer stale'
        ? json(200, { token: 'renewed', expires_in: 900 })
        : json(401, { error: 'unauthorized' }),
  });
  const client = SupportGate.init({ endpoint: 'https://support.example', token: 'stale' });

  await client.submit(DRAFT, 'key-1');
  await client.form();

  assert.deepEqual(
    calls.map((c) => [c.key, c.init.headers.Authorization]),
    [
      ['POST /v1/tickets', 'Bearer stale'],
      ['POST /v1/session/refresh', 'Bearer stale'],
      ['POST /v1/tickets', 'Bearer renewed'],
      ['GET /v1/form', 'Bearer renewed'],
    ],
  );
});

test('если ни игра, ни шлюз не дали токен — наружу уходит исходный 401, без второго повтора', async () => {
  const { SupportGate, calls } = setup({
    'POST /v1/tickets': () => json(401, { error: 'unauthorized', message: 'expired' }),
    'POST /v1/session/refresh': () => json(401, { error: 'unauthorized', message: 'too old' }),
  });
  const client = SupportGate.init({ endpoint: 'https://support.example', token: () => 'same' });

  await assert.rejects(() => client.submit(DRAFT, 'key-1'), (error) => error.status === 401 && error.message === 'expired');
  assert.deepEqual(
    calls.map((c) => c.key),
    ['POST /v1/tickets', 'POST /v1/session/refresh'],
  );
});
