// Страница /form: игра открывает её в системном браузере с токеном во фрагменте.

const assert = require('node:assert/strict');
const { test } = require('node:test');

const { setup, until, json } = require('./harness');

test('hosted берёт токен из фрагмента, стирает его из адреса и ходит на свой origin', async () => {
  const { SupportGate, calls, dom } = setup({}, 'https://support.example/form#token=eyJ.session.token');
  const host = dom.window.document.createElement('div');
  dom.window.document.body.appendChild(host);

  const form = await SupportGate.hosted(host);

  assert.ok(form, 'форма смонтирована');
  assert.equal(dom.window.location.href, 'https://support.example/form');
  await until(() => calls.length > 0, 'запрос формы');
  assert.equal(calls[0].url, 'https://support.example/v1/form');
  assert.equal(calls[0].init.headers.Authorization, 'Bearer eyJ.session.token');
  assert.ok(host.querySelector('select'), 'форма на экране');
  assert.equal(host.querySelectorAll('.sg-footer button').length, 1, 'только «Отправить», без «Отмены»');
});

test('hosted без токена объясняет, что страницу открывают из игры', async () => {
  const { SupportGate, calls, dom } = setup({}, 'https://support.example/form');
  const host = dom.window.document.createElement('div');
  dom.window.document.body.appendChild(host);

  const form = await SupportGate.hosted(host);

  assert.equal(form, null);
  assert.equal(calls.length, 0, 'в сеть не ходит');
  assert.match(host.textContent, /Open this page from the game/);
});

test('hosted: если сессию не продлить, форма сменяется подсказкой открыть страницу заново', async () => {
  const { SupportGate, calls, dom } = setup(
    {
      'POST /v1/tickets': () => json(401, { error: 'unauthorized', message: 'expired' }),
      'POST /v1/session/refresh': () => json(401, { error: 'unauthorized', message: 'too old' }),
    },
    'https://support.example/form#token=old',
  );
  const host = dom.window.document.createElement('div');
  dom.window.document.body.appendChild(host);
  const results = [];

  const form = await SupportGate.hosted(host, { onResult: (r) => results.push(r) });
  await until(() => host.querySelector('#sg-category'), 'форма');
  const $ = (sel) => host.querySelector(sel);
  $('#sg-category').value = 'gameplay';
  $('#sg-category').dispatchEvent(new dom.window.Event('change'));
  $('#sg-subcategory').value = 'progress';
  $('#sg-email').value = 'player@example.com';
  $('#sg-subject').value = 'Lost progress';
  $('#sg-description').value = 'Details of the problem';
  $('.sg-footer .sg-primary').click();
  await until(() => results.length > 0, 'результат');

  assert.ok(form);
  assert.equal(results[0].httpStatus, 401);
  assert.ok(calls.some((c) => c.key === 'POST /v1/session/refresh'), 'пробовали продлить');
  assert.equal(host.querySelector('.sg-primary'), null, 'отправлять больше нечем');
  assert.match(host.textContent, /Open this page from the game/);
});
