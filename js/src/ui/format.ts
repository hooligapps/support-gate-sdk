// Подстановка в строки ICU MessageFormat — ровно то подмножество, которое
// используют каталоги locales/*.json: `{name}`, `{n, plural, one {…} other {…}}`
// c `=N` и `#`, `{x, select, a {…} other {…}}`. Вложенность разрешена.
// Полноценная библиотека для 35 строк — лишние килобайты в бандле игры.

export type FormatValues = Record<string, string | number>;

interface Argument {
  name: string;
  kind: 'value' | 'plural' | 'select';
  options: Record<string, Node[]>;
}

type Node = string | Argument;

const cache: Record<string, Node[]> = {};

export function format(message: string, values: FormatValues = {}, locale = 'en'): string {
  let nodes = cache[message];
  if (!nodes) {
    try {
      nodes = parse(message);
    } catch {
      // Кривая строка в каталоге не должна ломать форму: показываем как есть.
      nodes = [message];
    }
    cache[message] = nodes;
  }
  return render(nodes, values, locale, null);
}

function render(nodes: Node[], values: FormatValues, locale: string, hash: string | null): string {
  let out = '';
  for (const node of nodes) {
    if (typeof node === 'string') {
      out += hash === null ? node : node.split('#').join(hash);
      continue;
    }
    const value = values[node.name];
    if (node.kind === 'value') {
      out += value === undefined ? `{${node.name}}` : String(value);
      continue;
    }
    if (node.kind === 'plural') {
      const n = Number(value);
      const branch =
        node.options[`=${n}`] ?? node.options[pluralCategory(n, locale)] ?? node.options.other;
      out += branch ? render(branch, values, locale, String(value)) : '';
      continue;
    }
    const branch = node.options[String(value)] ?? node.options.other;
    out += branch ? render(branch, values, locale, hash) : '';
  }
  return out;
}

function pluralCategory(n: number, locale: string): string {
  if (!isFinite(n)) return 'other';
  try {
    // Регион не влияет на правила, а «es-la» браузер не знает — берём язык.
    return new Intl.PluralRules(locale.split('-')[0]).select(n);
  } catch {
    return n === 1 ? 'one' : 'other';
  }
}

function parse(message: string): Node[] {
  const state = { text: message, pos: 0 };
  const nodes = parseNodes(state, false);
  if (state.pos !== message.length) throw new Error('unbalanced braces');
  return nodes;
}

function parseNodes(state: { text: string; pos: number }, nested: boolean): Node[] {
  const nodes: Node[] = [];
  let literal = '';
  while (state.pos < state.text.length) {
    const char = state.text[state.pos];
    if (char === '}' && nested) break;
    if (char !== '{') {
      literal += char;
      state.pos += 1;
      continue;
    }
    if (literal) {
      nodes.push(literal);
      literal = '';
    }
    state.pos += 1;
    nodes.push(parseArgument(state));
  }
  if (literal) nodes.push(literal);
  return nodes;
}

function parseArgument(state: { text: string; pos: number }): Argument {
  const end = findArgumentEnd(state);
  const head = state.text.slice(state.pos, end);
  const parts = head.split(',').map((part) => part.trim());
  const name = parts[0];
  if (!name) throw new Error('empty argument');
  if (parts.length === 1) {
    state.pos = end + 1;
    return { name, kind: 'value', options: {} };
  }
  const kind = parts[1];
  if (kind !== 'plural' && kind !== 'select') throw new Error(`unsupported type ${kind}`);
  state.pos = end;
  const options: Record<string, Node[]> = {};
  while (true) {
    skipSpaces(state);
    if (state.text[state.pos] === '}') {
      state.pos += 1;
      break;
    }
    const keyStart = state.pos;
    while (state.pos < state.text.length && !/[\s{]/.test(state.text[state.pos])) state.pos += 1;
    const key = state.text.slice(keyStart, state.pos);
    skipSpaces(state);
    if (state.text[state.pos] !== '{' || !key) throw new Error('bad option');
    state.pos += 1;
    options[key] = parseNodes(state, true);
    if (state.text[state.pos] !== '}') throw new Error('unclosed option');
    state.pos += 1;
  }
  if (!options.other) throw new Error('missing other');
  return { name, kind, options };
}

/** Позиция конца заголовка аргумента: `}` у простого, второй `,` у plural/select. */
function findArgumentEnd(state: { text: string; pos: number }): number {
  let commas = 0;
  for (let i = state.pos; i < state.text.length; i += 1) {
    const char = state.text[i];
    if (char === '}') return i;
    if (char === '{') throw new Error('unexpected brace');
    if (char === ',') {
      commas += 1;
      if (commas === 2) return i + 1;
    }
  }
  throw new Error('unterminated argument');
}

function skipSpaces(state: { text: string; pos: number }): void {
  while (/\s/.test(state.text[state.pos] ?? '')) state.pos += 1;
}
