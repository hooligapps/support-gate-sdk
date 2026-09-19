const assert = require('node:assert/strict');
const { test } = require('node:test');

const { setup } = require('./harness');

function api() {
  return setup().SupportGate;
}

test('простые подстановки и отсутствующие значения', () => {
  const { format } = api();
  assert.equal(format('Hello {name}!', { name: 'Max' }), 'Hello Max!');
  assert.equal(format('Hello {name}!', {}), 'Hello {name}!');
  assert.equal(format('No braces', {}), 'No braces');
});

test('plural по правилам языка, =N и # внутри ветки', () => {
  const { format } = api();
  const en = 'Up to {max, plural, =0 {no files} one {# file} other {# files}}, {mb} MB each.';
  assert.equal(format(en, { max: 1, mb: 25 }), 'Up to 1 file, 25 MB each.');
  assert.equal(format(en, { max: 5, mb: 25 }), 'Up to 5 files, 25 MB each.');
  assert.equal(format(en, { max: 0, mb: 25 }), 'Up to no files, 25 MB each.');

  const ru = '{n, plural, one {# файл} few {# файла} many {# файлов} other {# файла}}';
  assert.equal(format(ru, { n: 1 }, 'ru'), '1 файл');
  assert.equal(format(ru, { n: 3 }, 'ru'), '3 файла');
  assert.equal(format(ru, { n: 5 }, 'ru'), '5 файлов');
  assert.equal(format(ru, { n: 21 }, 'ru-RU'), '21 файл');
  // Регион, которого нет в Intl (es-la), не ломает выбор формы.
  assert.equal(format('{n, plural, one {uno} other {varios}}', { n: 1 }, 'es-la'), 'uno');
});

test('select и вложенность', () => {
  const { format } = api();
  const msg = '{kind, select, image {{n, plural, one {# image} other {# images}}} other {files}}';
  assert.equal(format(msg, { kind: 'image', n: 2 }), '2 images');
  assert.equal(format(msg, { kind: 'video', n: 2 }), 'files');
});

test('кривая строка показывается как есть', () => {
  const { format } = api();
  assert.equal(format('Broken {max, plural, one {# file}', { max: 1 }), 'Broken {max, plural, one {# file}');
  assert.equal(format('Unbalanced }', {}), 'Unbalanced }');
});

test('подписи: каталог сервера поверх английского', () => {
  const { strings } = api();
  const ru = strings({ 'ui.submit': 'Отправить', 'ui.too_many_files': 'Не больше {max, plural, one {# файла} other {# файлов}}' }, 'ru');
  assert.equal(ru.submit, 'Отправить');
  assert.equal(ru.cancel, 'Cancel');
  assert.equal(ru.tooManyFiles(1), 'Не больше 1 файла');
  assert.equal(ru.tooManyFiles(5), 'Не больше 5 файлов');
  assert.equal(strings().attachmentsHint(5, 25), 'Up to 5 files, 25 MB each.');
});
