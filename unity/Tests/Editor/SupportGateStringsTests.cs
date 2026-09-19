using System.Collections.Generic;
using Hooligapps.SupportGate.UI;
using NUnit.Framework;

namespace Hooligapps.SupportGate.Tests
{
    public sealed class SupportGateStringsTests
    {
        private static string Format(string message, string locale, params object[] pairs)
        {
            var values = new Dictionary<string, object>();
            for (var i = 0; i < pairs.Length; i += 2)
            {
                values[(string)pairs[i]] = pairs[i + 1];
            }

            return SupportGateMessageFormat.Format(message, values, locale);
        }

        [Test]
        public void PlainPlaceholdersAreSubstituted()
        {
            Assert.AreEqual("Your request number is SUP-7. Bye.",
                Format("Your request number is {id}. Bye.", "en", "id", "SUP-7"));
            Assert.AreEqual("{id}", Format("{id}", "en"));
            Assert.AreEqual("no braces", Format("no braces", "en"));
        }

        [Test]
        public void PluralPicksCategoryByLocale()
        {
            const string en = "Up to {max, plural, one {# file} other {# files}}, {mb} MB each.";
            Assert.AreEqual("Up to 1 file, 5 MB each.", Format(en, "en", "max", 1, "mb", 5));
            Assert.AreEqual("Up to 3 files, 5 MB each.", Format(en, "en", "max", 3, "mb", 5));

            const string ru = "До {max, plural, one {# файла} few {# файлов} many {# файлов} other {# файла}}";
            Assert.AreEqual("До 1 файла", Format(ru, "ru", "max", 1));
            Assert.AreEqual("До 3 файлов", Format(ru, "ru", "max", 3));
            Assert.AreEqual("До 5 файлов", Format(ru, "ru", "max", 5));
            Assert.AreEqual("До 21 файла", Format(ru, "ru", "max", 21));

            const string ar = "{n, plural, zero {z} one {o} two {t} few {f} many {m} other {x}}";
            Assert.AreEqual("z", Format(ar, "ar", "n", 0));
            Assert.AreEqual("t", Format(ar, "ar", "n", 2));
            Assert.AreEqual("f", Format(ar, "ar", "n", 3));
            Assert.AreEqual("m", Format(ar, "ar", "n", 11));
            Assert.AreEqual("x", Format(ar, "ar", "n", 100));

            Assert.AreEqual("o", Format("{n, plural, one {o} other {x}}", "fr", "n", 0));
            Assert.AreEqual("x", Format("{n, plural, one {o} other {x}}", "ja", "n", 1));
            Assert.AreEqual("exact", Format("{n, plural, =0 {exact} one {o} other {x}}", "en", "n", 0));
        }

        [Test]
        public void SelectAndNestingWork()
        {
            const string message = "{g, select, f {She has {n, plural, one {# cat} other {# cats}}} other {They have #}}";
            Assert.AreEqual("She has 2 cats", Format(message, "en", "g", "f", "n", 2));
            Assert.AreEqual("They have #", Format(message, "en", "g", "m", "n", 2));
        }

        [Test]
        public void BrokenMessageIsShownAsIs()
        {
            Assert.AreEqual("{max, plural, one {# file}", Format("{max, plural, one {# file}", "en", "max", 1));
            Assert.AreEqual("{x, plural, one {a}}", Format("{x, plural, one {a}}", "en", "x", 1));
        }

        [Test]
        public void CatalogFillsStringsAndFallsBackToEnglish()
        {
            var text = SupportGateStrings.FromCatalog(new Dictionary<string, string>
            {
                { "ui.title", "Обращение" },
                { "ui.cancel", "" },
                { "ui.too_many_files", "Не больше {max, plural, one {# файла} few {# файлов} many {# файлов} other {# файла}}" }
            }, "ru");

            Assert.AreEqual("Обращение", text.Title);
            Assert.AreEqual("Cancel", text.Cancel);
            Assert.AreEqual("Send", text.Submit);
            Assert.AreEqual("Не больше 2 файлов", text.TooManyFiles(2));
            Assert.AreEqual("Your request number is SUP-1. We will reply by email.", text.SentText("SUP-1"));

            var english = SupportGateStrings.FromCatalog(null, null);
            Assert.AreEqual("Up to 3 files, 5 MB each.", english.AttachmentsHint(3, 5 * 1024 * 1024));
            Assert.AreEqual("Up to 1 file, 5 MB each.", english.AttachmentsHint(1, 5 * 1024 * 1024));
        }
    }
}
