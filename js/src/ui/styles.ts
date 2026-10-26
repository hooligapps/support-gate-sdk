// Стили окна инжектятся один раз в head. Всё под префиксом .sg- и с
// собственными переменными: окно встраивается в чужую страницу и не должно ни
// наследовать её тему, ни ломать её.

const STYLE_ID = 'support-gate-styles';

const CSS = `
.sg-root, .sg-embed {
  --sg-bg: #1f2436;
  --sg-raised: #232839;
  --sg-fg: #f0f2f8;
  --sg-muted: #b6bccd;
  --sg-line: rgba(255, 255, 255, 0.12);
  --sg-line-strong: rgba(255, 255, 255, 0.16);
  --sg-accent: #f42b5f;
  --sg-accent-hover: #ff4472;
  --sg-accent-soft: #ff6b8d;
  --sg-danger: #ff9d97;
  --sg-radius: 14px;
  box-sizing: border-box;
  color-scheme: dark;
  font: 14px/1.5 var(--sg-font-body, Barlow, system-ui, sans-serif);
  color: var(--sg-fg);
  text-align: start;
  -webkit-text-size-adjust: 100%;
  text-size-adjust: 100%;
}
.sg-root *, .sg-embed * { box-sizing: border-box; }
.sg-root [hidden], .sg-embed [hidden] { display: none !important; }
.sg-root ::selection, .sg-embed ::selection { background: var(--sg-accent); color: #fff; }
.sg-root :focus-visible, .sg-embed :focus-visible { outline: 2px solid var(--sg-accent-soft); outline-offset: 2px; }
.sg-embed {
  display: flex;
  flex-direction: column;
  min-width: 0;
  min-height: 0;
  max-height: 100%;
  border: 1px solid var(--sg-line);
  border-radius: var(--sg-radius);
  background: var(--sg-bg);
  overflow: hidden;
  container-type: inline-size;
}
.sg-root {
  position: fixed;
  inset: 0;
  z-index: 2147483000;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
}
.sg-backdrop { position: absolute; inset: 0; background: rgba(12, 14, 24, 0.72); }
.sg-dialog {
  position: relative;
  display: flex;
  flex-direction: column;
  width: 100%;
  max-width: 900px;
  max-height: min(780px, calc(100vh - 32px));
  max-height: min(780px, calc(100dvh - 32px));
  background: var(--sg-bg);
  border-radius: var(--sg-radius);
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.45);
  overflow: hidden;
  animation: sg-enter 0.18s ease-out;
}
.sg-header {
  display: flex;
  flex: none;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 16px 16px 16px 20px;
  border-bottom: 1px solid var(--sg-line);
}
.sg-header h2, .sg-state h3 {
  margin: 0;
  font-family: var(--sg-font-heading, Sora, var(--sg-font-body, system-ui, sans-serif));
  font-size: 21px;
  font-weight: 700;
  line-height: 1.3;
  letter-spacing: -0.01em;
  overflow-wrap: anywhere;
}
.sg-close, .sg-remove {
  flex: none;
  display: inline-grid;
  place-items: center;
  width: 34px;
  height: 34px;
  padding: 0;
  border: 1px solid var(--sg-line-strong);
  border-radius: 999px;
  background: rgba(20, 23, 35, 0.72);
  color: var(--sg-fg);
  cursor: pointer;
}
.sg-close:hover, .sg-remove:hover { border-color: rgba(255, 255, 255, 0.4); background: rgba(20, 23, 35, 0.9); }
.sg-close { width: 44px; min-width: 44px; max-width: 44px; height: 44px; margin: 0; margin-inline-start: auto; }
.sg-icon { display: block; width: 20px; height: 20px; pointer-events: none; }
/* Тело сжимается и прокручивается внутри окна; подвал остаётся доступным. */
.sg-content { display: flex; flex-direction: column; min-height: 0; flex: 1; }
.sg-body {
  min-height: 0;
  min-width: 0;
  padding: 16px 20px;
  overflow-y: auto;
  flex: 1 1 auto;
  scrollbar-color: #3c4460 transparent;
  scrollbar-width: thin;
}
.sg-body::-webkit-scrollbar { width: 8px; }
.sg-body::-webkit-scrollbar-thumb { background: #3c4460; border-radius: 8px; }
.sg-footer {
  display: flex;
  flex: none;
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 8px;
  padding: 14px 20px;
  border-top: 1px solid var(--sg-line);
}
.sg-field { min-width: 0; margin-bottom: 12px; }
.sg-target { padding: 14px 20px 0; margin-bottom: 0; flex-shrink: 0; }
.sg-field > label { display: block; margin-bottom: 6px; font-size: 13px; font-weight: 600; }
.sg-field input[type="text"], .sg-field input[type="email"], .sg-field input[type="date"],
.sg-field select, .sg-field textarea {
  width: 100%;
  min-width: 0;
  min-height: 44px;
  padding: 11px 14px;
  border: 1px solid var(--sg-line);
  border-radius: 12px;
  font: inherit;
  color: var(--sg-fg);
  background: rgba(255, 255, 255, 0.05);
  caret-color: var(--sg-accent-soft);
}
.sg-field select option { color: var(--sg-fg); background: var(--sg-raised); }
.sg-field input::placeholder, .sg-field textarea::placeholder { color: var(--sg-muted); opacity: 1; }
.sg-field textarea { min-height: 116px; resize: vertical; }
.sg-field input:focus, .sg-field select:focus, .sg-field textarea:focus { border-color: var(--sg-accent); }
.sg-field input[readonly] { color: var(--sg-muted); background: var(--sg-bg); }
.sg-field input:disabled, .sg-field select:disabled, .sg-field textarea:disabled { opacity: 0.6; cursor: not-allowed; }
.sg-field.sg-invalid input, .sg-field.sg-invalid select, .sg-field.sg-invalid textarea { border-color: var(--sg-danger); }
.sg-hint, .sg-error { margin-top: 5px; font-size: 12px; overflow-wrap: anywhere; }
.sg-hint { color: var(--sg-muted); }
.sg-error { color: var(--sg-danger); }
.sg-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 11px 22px;
  border: 1px solid var(--sg-line-strong);
  border-radius: 9px;
  background: transparent;
  color: var(--sg-fg);
  font: inherit;
  font-weight: 700;
  line-height: 1.3;
  cursor: pointer;
  transition: border-color 0.18s, background-color 0.18s;
}
.sg-button:hover:not(:disabled) { border-color: rgba(255, 255, 255, 0.4); background: rgba(255, 255, 255, 0.05); }
.sg-button:active:not(:disabled) { background: rgba(255, 255, 255, 0.1); }
.sg-button.sg-primary {
  background: var(--sg-accent);
  border-color: var(--sg-accent);
  color: #fff;
  box-shadow: 0 6px 22px rgba(244, 43, 95, 0.35);
}
.sg-button.sg-primary:hover:not(:disabled), .sg-button.sg-primary:active:not(:disabled) { background: var(--sg-accent-hover); border-color: var(--sg-accent-hover); }
.sg-button:disabled { opacity: 0.6; cursor: not-allowed; box-shadow: none; }
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
  gap: 6px;
  padding: 8px;
  border: 1px dashed var(--sg-line-strong);
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.03);
  text-align: center;
  cursor: pointer;
  color: var(--sg-muted);
  font-size: 12px;
  transition: border-color 0.18s, background-color 0.18s;
}
.sg-drop:hover, .sg-drop.sg-drop-active { border-color: var(--sg-accent); background: rgba(244, 43, 95, 0.08); }
.sg-drop > .sg-icon { width: 24px; height: 24px; color: var(--sg-accent-soft); }
.sg-files { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: 10px; }
.sg-file { position: relative; width: 112px; border: 1px solid var(--sg-line); border-radius: 12px; overflow: hidden; background: var(--sg-raised); }
.sg-thumb { height: 80px; display: flex; align-items: center; justify-content: center; background: #151928; overflow: hidden; }
.sg-thumb img, .sg-thumb video { width: 100%; height: 100%; object-fit: cover; display: block; }
.sg-thumb-ext { font-size: 12px; font-weight: 600; color: var(--sg-muted); }
.sg-file-meta { padding: 5px 6px; font-size: 11px; line-height: 1.3; }
.sg-file .sg-name { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.sg-file .sg-size { color: var(--sg-muted); }
.sg-file .sg-button { min-height: 28px; padding: 4px 6px; font-size: 12px; }
.sg-file-pending .sg-thumb { opacity: 0.5; }
.sg-remove { position: absolute; top: 4px; inset-inline-end: 4px; width: 26px; height: 26px; }
.sg-remove .sg-icon { width: 16px; height: 16px; }
.sg-remove:hover { background: #663142; }
.sg-state { padding: 28px 20px; text-align: center; color: var(--sg-muted); overflow-wrap: anywhere; }
.sg-state h3 { margin-bottom: 8px; font-size: 18px; color: var(--sg-fg); }
.sg-state p { margin: 0; }
.sg-ticket-number {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  width: auto;
  max-width: 100%;
  min-height: 44px;
  margin: 4px;
  padding: 8px 12px;
  border: 1px solid var(--sg-line-strong);
  border-radius: 9px;
  background: var(--sg-raised);
  color: var(--sg-fg);
  font: inherit;
  font-weight: 700;
  cursor: pointer;
}
.sg-ticket-number:hover { border-color: var(--sg-accent-soft); }
.sg-ticket-number span { min-width: 0; overflow-wrap: anywhere; user-select: text; -webkit-user-select: text; }
.sg-copy-status { display: block; min-height: 1.5em; margin-top: 4px; font-size: 12px; }
.sg-banner { margin-bottom: 14px; padding: 10px 12px; border: 1px solid rgba(255, 157, 151, 0.25); border-radius: 12px; background: rgba(255, 157, 151, 0.08); color: var(--sg-danger); overflow-wrap: anywhere; }
.sg-dialog-compact { max-width: 560px; }
.sg-compact .sg-columns { flex-direction: column; gap: 0; }
.sg-compact .sg-col { flex: none; }
.sg-preset { margin-bottom: 14px; overflow-wrap: anywhere; }
.sg-preset h3 { margin: 0 0 6px; font-size: 16px; font-weight: 700; line-height: 1.35; }
.sg-preset p { margin: 3px 0 0; color: var(--sg-muted); font-size: 12px; }
.sg-compact .sg-drop { width: auto; min-height: 44px; flex-direction: row; padding: 8px 12px; }
.sg-compact .sg-field[data-field="attachments"] > label { display: none; }
.sg-compact .sg-col-text .sg-field[data-field="description"] textarea { min-height: 120px; }
@keyframes sg-enter { from { opacity: 0; transform: translateY(8px) scale(0.98); } }
@media (prefers-reduced-motion: reduce) {
  .sg-dialog { animation: none; }
  .sg-root *, .sg-embed * { transition: none !important; }
}
@container (max-width: 680px) {
  .sg-embed .sg-columns { flex-direction: column; gap: 0; }
  .sg-embed .sg-col-text .sg-field[data-field="description"] textarea { min-height: 120px; }
}
@media (max-width: 680px) {
  .sg-columns { flex-direction: column; gap: 0; }
  .sg-col-text .sg-field[data-field="description"] textarea { min-height: 120px; }
}
@media (max-width: 560px) {
  .sg-root { padding: 8px; }
  .sg-dialog { max-height: calc(100vh - 16px); max-height: calc(100dvh - 16px); }
  .sg-header { padding: 12px; }
  .sg-header h2 { font-size: 18px; }
  .sg-body, .sg-footer { padding: 12px; }
  .sg-target { padding: 12px 12px 0; }
}

`;

export function injectStyles(doc: Document = document): void {
  if (doc.getElementById(STYLE_ID)) return;

  const style = doc.createElement('style');
  style.id = STYLE_ID;
  style.textContent = CSS;
  doc.head.appendChild(style);
}
