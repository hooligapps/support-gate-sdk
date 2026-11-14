const assert = require('node:assert/strict');
const { test } = require('node:test');
const { setup, until } = require('./harness');
const cases = require('./email-cases.json');

for (const entry of cases) {
  test(`email validation: ${JSON.stringify(entry.input)}`, async () => {
    const { SupportGate, dom, calls } = setup();
    SupportGate.init({ endpoint: 'https://support.example', token: 't' });
    const host = document.createElement('div');
    document.body.appendChild(host);
    const form = await SupportGate.mount(host);
    const $ = (selector) => host.querySelector(selector);
    try {
      $('#sg-category').value = 'gameplay';
      $('#sg-category').dispatchEvent(new Event('change'));
      $('#sg-subcategory').value = 'progress';
      $('#sg-subject').value = 'Question';
      $('#sg-description').value = 'Details';
      const email = $('#sg-email');
      email.value = entry.input;
      email.dispatchEvent(new Event('blur'));
      if (entry.expected === null) {
        assert.ok($('[data-field="email"] .sg-error'));
        assert.equal(email.getAttribute('aria-invalid'), 'true');
        $('.sg-primary').click();
        assert.equal(calls.filter((call) => call.key === 'POST /v1/tickets').length, 0);
        email.value = 'corrected@example.com';
        email.dispatchEvent(new Event('blur'));
        assert.equal($('[data-field="email"] .sg-error'), null);
        assert.equal(email.getAttribute('aria-invalid'), 'false');
        assert.equal($('[data-field="email"]').classList.contains('sg-invalid'), false);
      } else {
        assert.equal(email.value, entry.expected);
        assert.equal($('[data-field="email"] .sg-error'), null);
        $('.sg-primary').click();
        await until(() => calls.some((call) => call.key === 'POST /v1/tickets'), 'ticket request');
        assert.equal(calls.find((call) => call.key === 'POST /v1/tickets').body.email, entry.expected);
      }
    } finally {
      form.destroy();
      dom.window.close();
    }
  });
}
