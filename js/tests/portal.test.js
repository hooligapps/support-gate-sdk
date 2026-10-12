const assert = require('node:assert/strict');
const { test } = require('node:test');
const { setup, json, until, FORM } = require('./harness');

const targets = [{ id: 'portal', label: 'Portal' }, { id: 'game_a', label: 'Game A' },
  { id: 'game_b', label: 'Game B' }];
const targetOf = (call) => call.init.headers.Authorization.slice(7).split('/')[0];

async function portalForm(options = {}, routes = {}) {
  const hints = [];
  const context = setup({
    'GET /v1/form': (call) => json(200, { ...FORM, game: targetOf(call) }),
    ...routes,
  });
  const { SupportGate, dom } = context;
  SupportGate.init({ endpoint: 'https://support.example', token: (hint) => {
    hints.push(hint);
    return `${hint.targetId}/${hint.jti}/${hint.reason || ''}`;
  } });
  const host = dom.window.document.createElement('div');
  dom.window.document.body.appendChild(host);
  const form = await SupportGate.mount(host, options);
  return { ...context, form, hints, host, $: (sel) => host.querySelector(sel) };
}

function select(control, value) {
  control.value = value;
  control.dispatchEvent(new globalThis.Event('change'));
}

function fill($) {
  select($('#sg-category'), 'payment');
  $('#sg-subcategory').value = 'refund';
  $('[id="sg-extra.payment_method"]').value = 'apple';
  $('#sg-email').value = 'player@example.com';
  $('#sg-subject').value = 'Lost purchase';
  $('#sg-description').value = 'My purchase is missing';
}

test('fixed target has no selector and each window gets a new session', async () => {
  const { $, hints, form, SupportGate } = await portalForm({ targetId: 'game_a' });
  assert.equal($('#sg-target'), null);
  assert.equal(hints[0].targetId, 'game_a');
  form.destroy();
  const results = [];
  await SupportGate.open({ targetId: 'game_a', onResult: (r) => results.push(r) });
  const next = hints.at(-1);
  assert.notEqual(next.jti, hints[0].jti);
  SupportGate.close();
  assert.deepEqual(results, [{ status: 'closed', targetId: 'game_a' }]);
});

test('selector without a default waits for a target before fetching', async () => {
  const { $, calls, form } = await portalForm({ targets });
  assert.equal(calls.length, 0);
  select($('#sg-target'), 'portal');
  await until(() => $('#sg-subject'), 'selected form');
  assert.equal(targetOf(calls[0]), 'portal');
  form.destroy();
});

test('switch keeps draft, refreshes email and reuploads files under the new target', async () => {
  let upload = 0;
  const results = [];
  const { $, dom, calls, hints, form } = await portalForm({
    targets, targetId: 'game_a', onResult: (r) => results.push(r),
  }, {
    'GET /v1/form': (call) => json(200, {
      ...FORM, game: targetOf(call), prefill: targetOf(call) === 'game_b'
        ? { email: 'verified@example.com', email_locked: true } : FORM.prefill,
    }),
    'POST /v1/attachments/presign': () => json(200, {
      attachment_id: `att-${++upload}`, upload_url: 'https://storage.example/att-1',
      method: 'PUT', headers: { 'Content-Type': 'image/png' }, expires_in: 900,
    }),
  });
  fill($);
  const file = new dom.window.File(['png'], 'shot.png', { type: 'image/png' });
  const event = new dom.window.Event('drop', { bubbles: true });
  Object.defineProperty(event, 'dataTransfer', { value: { files: [file] } });
  $('.sg-drop').dispatchEvent(event);
  await until(() => $('.sg-file:not(.sg-file-pending)'), 'first upload');
  const firstJti = hints[0].jti;
  select($('#sg-target'), 'game_b');
  await until(() => $('.sg-file:not(.sg-file-pending)') && $('#sg-email')?.readOnly, 'new upload');
  assert.equal($('#sg-subject').value, 'Lost purchase');
  assert.equal($('#sg-description').value, 'My purchase is missing');
  assert.equal($('#sg-category').value, 'payment');
  assert.equal($('#sg-subcategory').value, 'refund');
  assert.equal($('[id="sg-extra.payment_method"]').value, 'apple');
  assert.equal($('#sg-email').value, 'verified@example.com');
  assert.notEqual(hints.at(-1).jti, firstJti);
  assert.deepEqual(calls.filter((c) => c.key === 'POST /v1/attachments/presign').map(targetOf),
    ['game_a', 'game_b']);
  $('.sg-footer .sg-primary').click();
  await until(() => results.length, 'sent');
  const sent = calls.find((c) => c.key === 'POST /v1/tickets');
  assert.equal(targetOf(sent), 'game_b');
  assert.deepEqual(sent.body.attachment_ids, ['att-2']);
  assert.equal(results[0].targetId, 'game_b');
  form.destroy();
});

test('last selection wins even if an old request completes later', async () => {
  let finish;
  const { $, form } = await portalForm({ targets, targetId: 'portal' }, {
    'GET /v1/form': (call) => targetOf(call) === 'game_a'
      ? new Promise((resolve) => { finish = resolve; })
      : json(200, { ...FORM, game: targetOf(call) }),
  });
  $('#sg-subject').value = 'Keep this';
  select($('#sg-target'), 'game_a');
  await until(() => finish, 'first request');
  select($('#sg-target'), 'game_b');
  await until(() => $('#sg-subject'), 'second request');
  finish(json(200, { ...FORM, game: 'game_a', categories: [] }));
  await new Promise((resolve) => setTimeout(resolve, 10));
  assert.equal($('#sg-target').value, 'game_b');
  assert.equal($('#sg-subject').value, 'Keep this');
  assert.equal($('#sg-category').options.length, 3);
  form.destroy();
});

for (const options of [{ targets, targetId: 'game_a' }, { targetId: 'game_a' }]) {
test(`uncertain ${options.targets ? 'selectable' : 'fixed'} submission retries the same target, key and payload`, async () => {
  let attempt = 0;
  const { $, host, calls, form } = await portalForm(options, {
    'POST /v1/tickets': () => ++attempt === 1
      ? json(503, { error: 'unavailable' })
      : json(201, { ticket_id: 'tkt-1', issue_key: 'SUP-1', status: 'queued', created_at: '' }),
  });
  fill($);
  $('.sg-footer .sg-primary').click();
  await until(() => $('.sg-banner'), 'submit failure');
  if ($('#sg-target')) assert.equal($('#sg-target').disabled, true);
  assert.ok(host.querySelector('.sg-columns').hasAttribute('inert'));
  assert.equal($('.sg-banner').closest('[inert]'), null);
  if ($('#sg-target')) {
    select($('#sg-target'), 'game_b');
    assert.equal($('#sg-target').value, 'game_a');
  }
  // Even a script changing fields cannot rewrite the uncertain request.
  $('#sg-description').value = 'Changed after failure';
  Object.defineProperty(globalThis.screen, 'width', { configurable: true, value: 2000 });
  $('.sg-footer .sg-primary').click();
  await until(() => form.sent, 'retry success');
  const [first, second] = calls.filter((c) => c.key === 'POST /v1/tickets');
  assert.equal(first.init.headers['Idempotency-Key'], second.init.headers['Idempotency-Key']);
  assert.deepEqual(first.body, second.body);
  assert.equal(targetOf(second), 'game_a');
  form.destroy();
});
}

test('failed reupload keeps the file, blocks sending and allows retry or switching', async () => {
  let upload = 0;
  let fail = true;
  const { $, dom, calls, form } = await portalForm({ targets, targetId: 'game_a' }, {
    'POST /v1/attachments/presign': () => json(200, {
      attachment_id: `att-${++upload}`, upload_url: 'https://storage.example/att-1',
      method: 'PUT', headers: {}, expires_in: 900,
    }),
    'PUT https://storage.example/att-1': () => upload > 1 && fail
      ? json(503, null) : json(200, null),
  });
  fill($);
  const file = new dom.window.File(['png'], 'clipboard.png', { type: 'image/png' });
  const event = new dom.window.Event('drop', { bubbles: true });
  Object.defineProperty(event, 'dataTransfer', { value: { files: [file] } });
  $('.sg-drop').dispatchEvent(event);
  await until(() => $('.sg-file:not(.sg-file-pending)'), 'first upload');
  select($('#sg-target'), 'game_b');
  await until(() => $('.sg-file .sg-button:not([hidden])'), 'failed upload');
  assert.equal($('.sg-name').textContent, 'clipboard.png');
  $('.sg-footer .sg-primary').click();
  assert.equal(calls.some((call) => call.key === 'POST /v1/tickets'), false);
  $('.sg-file .sg-button').click();
  await until(() => upload === 3 && $('.sg-file .sg-button:not([hidden])'), 'failed retry');
  select($('#sg-target'), 'game_a');
  await until(() => upload === 4 && $('.sg-file .sg-button:not([hidden])'), 'file survives switch back');
  fail = false;
  $('.sg-file .sg-button').click();
  await until(() => upload === 5 && $('.sg-file .sg-button[hidden]')
    && !$('.sg-file-pending'), 'successful retry');
  $('.sg-footer .sg-primary').click();
  await until(() => form.sent, 'sent with attachment');
  assert.deepEqual(calls.find((call) => call.key === 'POST /v1/tickets').body.attachment_ids, ['att-5']);
  form.destroy();
});

test('closing and reopening legacy form does not cancel its shared catalog', async () => {
  let finish;
  let signal;
  const { SupportGate, dom, calls } = setup({
    'GET /v1/form': (call) => new Promise((resolve, reject) => {
      finish = resolve;
      signal = call.init.signal;
      signal.addEventListener('abort', () => reject(new Error('aborted')));
    }),
  });
  SupportGate.init({ endpoint: 'https://support.example', token: 'token' });
  const first = SupportGate.open();
  await until(() => finish, 'catalog request');
  SupportGate.close();
  const results = [];
  const second = SupportGate.open({ onResult: (result) => results.push(result) });
  assert.equal(signal.aborted, false);
  finish(json(200, FORM));
  await Promise.all([first, second]);
  assert.ok(dom.window.document.querySelector('#sg-subject'));
  assert.deepEqual(results, []);
  assert.equal(calls.length, 1);
  SupportGate.close();
});

test('explicit rejection allows switching and incompatible categories are cleared', async () => {
  const { $, form } = await portalForm({ targets, targetId: 'game_a' }, {
    'POST /v1/tickets': () => json(422, { error: 'validation_failed' }),
    'GET /v1/form': (call) => json(200, {
      ...FORM, game: targetOf(call), categories: targetOf(call) === 'game_b' ? [] : FORM.categories,
    }),
  });
  fill($);
  $('.sg-footer .sg-primary').click();
  await until(() => $('.sg-banner'), 'rejection');
  assert.equal($('#sg-target').disabled, false);
  select($('#sg-target'), 'game_b');
  await until(() => $('#sg-subject'), 'new form');
  assert.equal($('#sg-category').value, '');
  assert.equal($('#sg-subject').value, 'Lost purchase');
  form.destroy();
});

test('load failure keeps the draft and retry uses the selected target', async () => {
  let failures = 1;
  const { $, calls, form } = await portalForm({ targets, targetId: 'portal' }, {
    'GET /v1/form': (call) => targetOf(call) === 'game_a' && failures-- > 0
      ? json(503, { error: 'unavailable' }) : json(200, { ...FORM, game: targetOf(call) }),
  });
  $('#sg-subject').value = 'Keep this';
  select($('#sg-target'), 'game_a');
  await until(() => $('.sg-state .sg-button') || $('.sg-footer .sg-primary'), 'retry');
  $('.sg-footer .sg-primary').click();
  await until(() => $('#sg-subject'), 'retried form');
  assert.equal($('#sg-subject').value, 'Keep this');
  assert.equal(targetOf(calls.at(-1)), 'game_a');
  form.destroy();
});

test('refresh provider receives the same target and jti', async () => {
  const results = [];
  const { $, hints, form } = await portalForm({ targetId: 'game_a', onResult: (r) => results.push(r) }, {
    'POST /v1/tickets': (call) => call.init.headers.Authorization.endsWith('/expired')
      ? json(201, { ticket_id: 'tkt-1', issue_key: 'SUP-1', status: 'queued', created_at: '' })
      : json(401, { error: 'invalid_token' }),
  });
  fill($);
  $('.sg-footer .sg-primary').click();
  await until(() => results.length, 'refreshed submit');
  const hint = hints.find((hint) => hint.reason === 'expired');
  assert.equal(hint.targetId, 'game_a');
  assert.equal(hint.jti, hints[0].jti);
  assert.equal(results[0].status, 'sent');
  form.destroy();
});

test('mismatched token cannot mount or submit to another target', async () => {
  const results = [];
  const { $, calls, form } = await portalForm({ targetId: 'game_a', onResult: (r) => results.push(r) }, {
    'GET /v1/form': () => json(200, { ...FORM, game: 'game_b' }),
  });
  assert.equal($('#sg-subject'), null);
  assert.equal(results[0].code, 'target_mismatch');
  assert.equal(calls.some((c) => c.key === 'POST /v1/tickets'), false);
  form.destroy();
});
