using System;
using NUnit.Framework;

namespace Hooligapps.SupportGate.Tests
{
    public sealed class SupportGateBrowserTests
    {
        [Test]
        public void UrlPutsTheTokenIntoTheFragment()
        {
            var url = SupportGateBrowser.Url("https://support.example/", "eyJ.a+b/c=");

            // Токен только во фрагменте — после «#», в путь и query не попадает.
            Assert.AreEqual("https://support.example/form#token=eyJ.a%2Bb%2Fc%3D", url);
            Assert.AreEqual("/form", new Uri(url).AbsolutePath);
            Assert.IsEmpty(new Uri(url).Query);
        }

        [Test]
        public void UrlRequiresTokenAndAbsoluteEndpoint()
        {
            Assert.Throws<ArgumentException>(() => SupportGateBrowser.Url("https://support.example", ""));
            Assert.Throws<ArgumentException>(() => SupportGateBrowser.Url("support.example", "token"));
        }
    }
}
