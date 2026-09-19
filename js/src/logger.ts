import { DEBUG } from './environment/environment';

export class Logger {
  static log(meta: string, message: string, args: unknown[] = []) {
    if (!DEBUG) return;

    const metaStyle = 'color: #1f6feb; font-weight: bold';
    const infoStyle = 'color: inherit; font-weight: 300';
    console.log(`%c[%s]%c: ${message}`, metaStyle, meta, infoStyle, ...args);
  }
}
