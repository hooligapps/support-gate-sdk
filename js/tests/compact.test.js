const assert = require('node:assert/strict');
const { test } = require('node:test');
const { setup, json, until, FORM } = require('./harness');

const preset = {
  category: 'payment', subcategory: 'refund', subject: 'Payment question',
  extra: { payment_method: 'apple' },
};

async function compactForm(options = {}, routes = {}) {
  const context = setup({
    'GET /v1/form': () => json(200, { ...FORM,
      prefill: { email: 'player@example.com', email_locked: true } }),
    ...routes,
  });
  context.SupportGate.init({ endpoint: 'https://support.example', token: 't' });
  const host = context.dom.window.document.createElement('div');
  context.dom.window.document.body.appendChild(host);
  await context.SupportGate.mount(host, { compact: true, preset, ...options });
  return { ...context, host, $: (selector) => host.querySelector(selector) };
}

test('compact form sends preset fields and still requires a description', async () => {
  const { $, calls } = await compactForm();
  for (const name of ['category', 'subcategory', 'subject', 'email', 'extra.payment_method']) {
    assert.equal($(`[data-field="${name}"]`).hidden, true, name);
  }
  assert.match($('.sg-preset').textContent, /Payment question/);
  assert.match($('.sg-preset').textContent, /Apple/);
  $('.sg-primary').click();
  assert.equal(calls.filter((call) => call.key === 'POST /v1/tickets').length, 0);
  assert.ok($('[data-field="description"] .sg-error'));
  $('#sg-description').value = 'The purchase did not arrive';
  $('.sg-primary').click();
  await until(() => $('.sg-state'), 'sent state');
  const body = calls.find((call) => call.key === 'POST /v1/tickets').body;
  assert.equal(body.category, preset.category);
  assert.equal(body.subcategory, preset.subcategory);
  assert.equal(body.subject, preset.subject);
  assert.deepEqual(body.extra, preset.extra);
  assert.equal(body.email, 'player@example.com');
});

test('missing required extra fields stay visible and block sending', async () => {
  const { $, calls } = await compactForm({ preset: { ...preset, extra: {} } });
  assert.equal($('[data-field="extra.payment_method"]').hidden, false);
  $('#sg-description').value = 'The purchase did not arrive';
  $('.sg-primary').click();
  assert.ok($('[data-field="extra.payment_method"] .sg-error'));
  assert.equal(calls.filter((call) => call.key === 'POST /v1/tickets').length, 0);
});

test('invalid presets and an unconfirmed email remain editable', async () => {
  const { $ } = await compactForm({ preset: {
    category: 'unknown', subcategory: 'unknown', subject: 'x'.repeat(256),
  } }, { 'GET /v1/form': () => json(200, FORM) });
  for (const name of ['category', 'subcategory', 'subject', 'email']) {
    assert.equal($(`[data-field="${name}"]`).hidden, false, name);
  }
  assert.equal($('#sg-category').value, '');
});

test('invalid extra values are not hidden', async () => {
  const { $ } = await compactForm({ preset: { ...preset,
    extra: { payment_method: 'unknown', transaction_id: 'too-long', purchase_date: 'not-a-date' },
  } }, { 'GET /v1/form': () => json(200, { ...FORM, categories: [{
    ...FORM.categories[1], extra_fields: [...FORM.categories[1].extra_fields,
      { id: 'transaction_id', label: 'Order', type: 'text', max_length: 4 },
      { id: 'purchase_date', label: 'Date', type: 'date' }],
  }] }) });
  for (const name of ['payment_method', 'transaction_id', 'purchase_date']) {
    assert.equal($(`[data-field="extra.${name}"]`).hidden, false, name);
  }
});

test('server rejection reveals the preset field and retry sends the correction', async () => {
  let attempts = 0;
  const { $, calls } = await compactForm({}, { 'POST /v1/tickets': () => ++attempts === 1
    ? json(422, { detail: { error: 'validation_failed', message: 'Invalid subject', fields: { subject: 'Fix subject' } } })
    : json(201, { ticket_id: 'tkt-1', issue_key: 'SUP-1', status: 'queued' }),
  });
  $('#sg-description').value = 'The purchase did not arrive';
  $('.sg-primary').click();
  await until(() => $('[data-field="subject"] .sg-error'), 'visible field error');
  assert.equal($('[data-field="subject"]').hidden, false);
  assert.equal($('[data-preset-field="subject"]'), null);
  $('#sg-subject').value = 'Corrected subject';
  $('.sg-primary').click();
  await until(() => $('.sg-state'), 'sent state');
  const requests = calls.filter((call) => call.key === 'POST /v1/tickets');
  assert.equal(requests[1].body.subject, 'Corrected subject');
});

test('presets without compact mode leave all fields visible', async () => {
  const { $ } = await compactForm({ compact: false });
  assert.equal($('#sg-subject').value, preset.subject);
  assert.equal($('#sg-category').value, preset.category);
  assert.equal($('[data-field="subject"]').hidden, false);
  assert.equal($('[data-field="extra.payment_method"]').hidden, false);
  assert.equal($('.sg-preset'), null);
});

test('switching to an incompatible target exposes its category controls', async () => {
  const context = setup({ 'GET /v1/form': (call) => {
    const game = call.init.headers.Authorization.slice(7).split('/')[0];
    return json(200, { ...FORM, game, categories: game === 'game_b' ? [FORM.categories[0]] : FORM.categories });
  } });
  context.SupportGate.init({ endpoint: 'https://support.example', token: (hint) => `${hint.targetId}/${hint.jti}` });
  const host = context.dom.window.document.createElement('div');
  context.dom.window.document.body.appendChild(host);
  await context.SupportGate.mount(host, { compact: true, preset, targetId: 'game_a',
    targets: [{ id: 'game_a', label: 'Game A' }, { id: 'game_b', label: 'Game B' }],
  });
  const target = host.querySelector('#sg-target');
  target.value = 'game_b';
  target.dispatchEvent(new globalThis.Event('change'));
  await until(() => host.querySelector('#sg-category')?.options.length === 2, 'new categories');
  assert.equal(host.querySelector('#sg-category').value, '');
  assert.equal(host.querySelector('[data-field="category"]').hidden, false);
  assert.equal(host.querySelector('[data-field="subcategory"]').hidden, false);
});
