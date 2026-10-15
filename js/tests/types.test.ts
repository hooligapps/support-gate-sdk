import { init, mount, open, TokenHint, TargetTokenHint } from '../src';

// Compile-only compatibility checks; node's test runner only loads *.test.js.
export function legacyIntegration() {
  init({ endpoint: '/', token: 'jwt' });
  init({ endpoint: '/', token: (hint) => hint?.reason === 'expired' ? 'fresh' : 'cached' });
  const provider = (hint?: TokenHint) => hint?.reason === 'expired' ? 'fresh' : 'cached';
  init({ endpoint: '/', token: provider });
  open({ onResult: (result) => result.status });
}

export function portalIntegration() {
  init({ endpoint: '/', token: ({ targetId, jti, reason } = {}) =>
    `${targetId}/${jti}/${reason}` });
  const provider = ({ targetId, jti }: TargetTokenHint) => `${targetId}/${jti}`;
  init({ endpoint: '/', token: provider });
  open({ targetId: 'game_a', targets: [{ id: 'game_a', label: 'Game A' }] });
}

export function paymentSupport(host: HTMLElement) {
  mount(host, { targetId: 'game_a', compact: true, preset: {
    category: 'payment', subcategory: 'refund', subject: 'Payment question',
    extra: { transaction_id: 'order_123' },
  } });
}
