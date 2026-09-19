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
