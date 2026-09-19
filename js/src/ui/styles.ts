// Стили окна инжектятся один раз в head. Всё под префиксом .sg- и с
// собственными переменными: окно встраивается в чужую страницу и не должно ни
// наследовать её тему, ни ломать её.

const STYLE_ID = 'support-gate-styles';

const CSS = `
.sg-root, .sg-embed {
  --sg-bg: #ffffff;
  --sg-fg: #11181c;
  --sg-muted: #5c6b73;
  --sg-line: #d7dee3;
  --sg-accent: #1f6feb;
  --sg-danger: #c5221f;
  --sg-radius: 10px;
  font: 14px/1.45 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
  color: var(--sg-fg);
}
.sg-root *, .sg-embed * { box-sizing: border-box; }
.sg-embed { display: flex; flex-direction: column; }
.sg-root {
  position: fixed;
  inset: 0;
  z-index: 2147483000;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
}
.sg-backdrop {
  position: absolute;
  inset: 0;
  background: rgba(14, 20, 24, 0.55);
}
.sg-dialog {
  position: relative;
  display: flex;
  flex-direction: column;
  width: 100%;
  max-width: 900px;
  max-height: 100%;
  background: var(--sg-bg);
  border-radius: var(--sg-radius);
  box-shadow: 0 18px 48px rgba(0, 0, 0, 0.32);
  overflow: hidden;
}
.sg-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 16px 20px;
  border-bottom: 1px solid var(--sg-line);
}
.sg-header h2 { margin: 0; font-size: 16px; font-weight: 600; }
.sg-close {
  border: 0;
  background: transparent;
  font-size: 22px;
  line-height: 1;
  cursor: pointer;
  color: var(--sg-muted);
  padding: 4px 8px;
  border-radius: 6px;
}
.sg-close:hover { background: rgba(0, 0, 0, 0.06); }
/* Форма монтируется сюда: тело должно сжиматься, а подвал — оставаться виден. */
.sg-content { display: flex; flex-direction: column; min-height: 0; flex: 1; }
.sg-body { padding: 14px 20px; overflow-y: auto; flex: 1; }
.sg-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 14px 20px;
  border-top: 1px solid var(--sg-line);
}
.sg-field { margin-bottom: 10px; }
.sg-field > label {
  display: block;
  margin-bottom: 4px;
  font-weight: 500;
}
.sg-field input[type="text"],
.sg-field input[type="email"],
.sg-field input[type="date"],
.sg-field select,
.sg-field textarea {
  width: 100%;
  padding: 8px 10px;
  border: 1px solid var(--sg-line);
  border-radius: 8px;
  font: inherit;
  color: inherit;
  background: #fff;
}
.sg-field textarea { min-height: 116px; resize: vertical; }
.sg-field input:focus, .sg-field select:focus, .sg-field textarea:focus {
  outline: 2px solid var(--sg-accent);
  outline-offset: -1px;
  border-color: var(--sg-accent);
}
.sg-field.sg-invalid input,
.sg-field.sg-invalid select,
.sg-field.sg-invalid textarea { border-color: var(--sg-danger); }
.sg-hint { margin-top: 4px; color: var(--sg-muted); font-size: 12px; }
.sg-error { margin-top: 4px; color: var(--sg-danger); font-size: 12px; }
.sg-button {
  padding: 9px 16px;
  border-radius: 8px;
  border: 1px solid var(--sg-line);
  background: #fff;
  font: inherit;
  cursor: pointer;
}
.sg-button:hover:not(:disabled) { background: #f3f5f7; }
.sg-button.sg-primary {
  background: var(--sg-accent);
  border-color: var(--sg-accent);
  color: #fff;
}
.sg-button.sg-primary:hover:not(:disabled) { background: #1a5fd0; }
.sg-button:disabled { opacity: 0.6; cursor: default; }
.sg-columns { display: flex; gap: 20px; }
.sg-col { flex: 2 1 0; min-width: 0; }
.sg-col-text { flex: 3 1 0; display: flex; flex-direction: column; }
.sg-col-text .sg-field[data-field="description"] { display: flex; flex-direction: column; flex: 1; }
.sg-col-text .sg-field[data-field="description"] textarea { flex: 1; min-height: 140px; }
.sg-picker { display: none; }
.sg-drop {
  width: 112px;
  min-height: 112px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2px;
  border: 1.5px dashed var(--sg-line);
  border-radius: 8px;
  text-align: center;
  cursor: pointer;
  color: var(--sg-muted);
  font-size: 12px;
  transition: border-color 0.12s, background 0.12s;
}
.sg-drop:hover, .sg-drop:focus, .sg-drop.sg-drop-active {
  border-color: var(--sg-accent);
  background: rgba(31, 111, 235, 0.06);
  outline: none;
}
.sg-drop-icon { font-size: 26px; line-height: 1; color: var(--sg-accent); }
.sg-files {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}
.sg-file {
  position: relative;
  width: 112px;
  border: 1px solid var(--sg-line);
  border-radius: 8px;
  overflow: hidden;
  background: #fff;
}
.sg-thumb {
  height: 80px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #eef1f4;
  overflow: hidden;
}
.sg-thumb img, .sg-thumb video { width: 100%; height: 100%; object-fit: cover; display: block; }
.sg-thumb-ext { font-size: 12px; font-weight: 600; color: var(--sg-muted); }
.sg-file-meta { padding: 5px 6px; font-size: 11px; line-height: 1.3; }
.sg-file .sg-name { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.sg-file .sg-size { color: var(--sg-muted); }
.sg-file-pending .sg-thumb { opacity: 0.5; }
.sg-remove {
  position: absolute;
  top: 4px;
  inset-inline-end: 4px; /* RTL: в углу у конца строки */
  width: 22px;
  height: 22px;
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: rgba(14, 20, 24, 0.7);
  color: #fff;
  font-size: 16px;
  line-height: 22px;
  cursor: pointer;
}
.sg-remove:hover { background: var(--sg-danger); }
.sg-state { padding: 28px 20px; text-align: center; color: var(--sg-muted); }
.sg-state h3 { margin: 0 0 8px; color: var(--sg-fg); font-size: 16px; }
.sg-banner {
  margin-bottom: 14px;
  padding: 10px 12px;
  border-radius: 8px;
  background: #fdecea;
  color: var(--sg-danger);
}
@media (prefers-color-scheme: dark) {
  .sg-root, .sg-embed {
    --sg-bg: #16191c;
    --sg-fg: #e7edf2;
    --sg-muted: #9aa8b2;
    --sg-line: #2c3238;
  }
  .sg-field input, .sg-field select, .sg-field textarea { background: #1d2126; }
  .sg-button { background: #1d2126; color: inherit; }
  .sg-file { background: #1d2126; }
  .sg-thumb { background: #262c32; }
  .sg-button:hover:not(:disabled) { background: #262c32; }
  .sg-banner { background: #3a1d1c; color: #ff9d97; }
}
@media (max-width: 680px) {
  .sg-columns { flex-direction: column; gap: 0; }
  .sg-col-text .sg-field[data-field="description"] textarea { min-height: 120px; }
}
@media (max-width: 560px) {
  .sg-root { padding: 0; }
  .sg-dialog { max-width: none; height: 100%; border-radius: 0; }
}
`;

export function injectStyles(doc: Document = document): void {
  if (doc.getElementById(STYLE_ID)) return;

  const style = doc.createElement('style');
  style.id = STYLE_ID;
  style.textContent = CSS;
  doc.head.appendChild(style);
}
