// Мелкие помощники вместо шаблонизатора: разметка окна небольшая, а любая
// вставка через innerHTML на чужих данных — это XSS в игре.

export function el<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  attrs: Record<string, string> = {},
  children: (Node | string)[] = [],
): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  for (const name of Object.keys(attrs)) {
    node.setAttribute(name, attrs[name]);
  }
  for (const child of children) {
    node.appendChild(typeof child === 'string' ? document.createTextNode(child) : child);
  }
  return node;
}

export function clear(node: Element): void {
  while (node.firstChild) {
    node.removeChild(node.firstChild);
  }
}

export function icon(name: 'close' | 'upload' | 'copy'): SVGSVGElement {
  const ns = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(ns, 'svg');
  svg.setAttribute('class', 'sg-icon');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('fill', 'none');
  svg.setAttribute('stroke', 'currentColor');
  svg.setAttribute('stroke-width', '2');
  svg.setAttribute('stroke-linecap', 'round');
  svg.setAttribute('stroke-linejoin', 'round');
  svg.setAttribute('aria-hidden', 'true');
  svg.setAttribute('focusable', 'false');
  const path = document.createElementNS(ns, 'path');
  path.setAttribute('d', name === 'close'
    ? 'M18 6 6 18M6 6l12 12'
    : name === 'copy'
      ? 'M9 9h12v12H9zM15 5V3H3v12h2'
      : 'M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M17 8l-5-5-5 5M12 3v12');
  svg.appendChild(path);
  return svg;
}

/** В старых webview и в jsdom метода нет, а форма из-за него падать не должна. */
export function scrollTo(node: Element): void {
  if (typeof (node as HTMLElement).scrollIntoView === 'function') {
    (node as HTMLElement).scrollIntoView({ block: 'nearest' });
  }
}

export function humanSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}
