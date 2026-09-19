using System;
using System.Collections;
using System.Text;
using NUnit.Framework;

namespace Hooligapps.SupportGate.Tests
{
    public sealed class SupportGateClientTests
    {
        private static SupportGateClient Client(ISupportGateTransport transport, string token = "jwt-1")
        {
            return new SupportGateClient("https://support.example/", SupportGateSession.FromToken(token), transport);
        }

        [Test]
        public void TokenTravelsInHeaderAndEndpointIsNormalized()
        {
            var transport = new FakeTransport(new SupportGateHttpResponse(200, Json.Form));
            SupportGateFormConfig config = null;

            Pump.RunToEnd(Client(transport).LoadForm(result => config = result.Value));

            Assert.AreEqual("https://support.example/v1/form", transport.Last.Url);
            Assert.AreEqual("Bearer jwt-1", transport.Last.Headers["Authorization"]);
            Assert.AreEqual("demo", config.Game);
        }

        [Test]
        public void ServerStringsAndDirectionAreParsed()
        {
            var transport = new FakeTransport(new SupportGateHttpResponse(200, Json.Form));
            SupportGateFormConfig config = null;

            Pump.RunToEnd(Client(transport).LoadForm(result => config = result.Value));

            Assert.AreEqual("ru", config.Locale);
            Assert.AreEqual("ltr", config.Direction);
            Assert.IsFalse(config.IsRightToLeft);
            Assert.AreEqual("Отправить", config.Strings["ui.submit"]);

            var rtl = new FakeTransport(new SupportGateHttpResponse(200,
                Json.Form.Replace(@"""direction"": ""ltr""", @"""direction"": ""rtl""")));
            Pump.RunToEnd(Client(rtl).LoadForm(result => config = result.Value));
            Assert.IsTrue(config.IsRightToLeft);

            var legacy = new FakeTransport(new SupportGateHttpResponse(200, @"{""game"": ""demo"", ""locale"": ""en""}"));
            Pump.RunToEnd(Client(legacy).LoadForm(result => config = result.Value));
            Assert.AreEqual("ltr", config.Direction);
            Assert.AreEqual(0, config.Strings.Count);
        }

        [Test]
        public void EndpointWithoutSchemeIsRejected()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new SupportGateClient("support.example", SupportGateSession.FromToken("t")));
        }

        [Test]
        public void FormIsFetchedOncePerSession()
        {
            var transport = new FakeTransport(new SupportGateHttpResponse(200, Json.Form));
            var client = Client(transport);

            Pump.RunToEnd(client.LoadForm(null));
            Pump.RunToEnd(client.LoadForm(null));

            Assert.AreEqual(1, transport.CountOf("/v1/form"));
        }

        [Test]
        public void CategoriesAndExtraFieldsAreParsed()
        {
            var transport = new FakeTransport(new SupportGateHttpResponse(200, Json.Form));
            SupportGateFormConfig config = null;

            Pump.RunToEnd(Client(transport).LoadForm(result => config = result.Value));

            var payment = config.FindCategory("payment");
            Assert.AreEqual(1, payment.ExtraFields.Count);
            Assert.AreEqual(SupportGateFieldType.Select, payment.ExtraFields[0].Type);
            Assert.IsTrue(payment.ExtraFields[0].Required);
            Assert.AreEqual("apple", payment.ExtraFields[0].Options[0].Id);
            Assert.AreEqual(5242880, config.Attachments.MaxFileSize);
        }

        [Test]
        public void ServiceErrorIsUnwrappedIntoCodeAndFields()
        {
            var transport = new FakeTransport(new SupportGateHttpResponse(422,
                @"{""detail"": {""error"": ""validation_error"", ""message"": ""плохо"",
                   ""fields"": {""subject"": ""пусто""}}}"));
            SupportGateError error = null;

            Pump.RunToEnd(Client(transport).SubmitTicket(Draft(), "key-1", result => error = result.Error));

            Assert.AreEqual("validation_error", error.Code);
            Assert.AreEqual("плохо", error.Message);
            Assert.AreEqual("пусто", error.Fields["subject"]);
            Assert.IsFalse(error.IsTransient);
        }

        [Test]
        public void RateLimitAndServerErrorsAreTransient()
        {
            var transport = new FakeTransport(new SupportGateHttpResponse(429, @"{""error"": ""rate_limited""}"));
            SupportGateError error = null;

            Pump.RunToEnd(Client(transport).SubmitTicket(Draft(), "key-1", result => error = result.Error));

            Assert.IsTrue(error.IsTransient);
        }

        [Test]
        public void IdempotencyKeyTravelsWithTheTicket()
        {
            var transport = new FakeTransport(new SupportGateHttpResponse(200, Json.Ticket));
            SupportGateTicket ticket = null;

            Pump.RunToEnd(Client(transport).SubmitTicket(Draft(), "key-1", result => ticket = result.Value));

            Assert.AreEqual("key-1", transport.Last.Headers["Idempotency-Key"]);
            Assert.AreEqual("SUP-1", ticket.IssueKey);
            Assert.AreEqual("SUP-1", ticket.DisplayId);
        }

        [Test]
        public void ClientInfoIsAddedButCarriesNoSecrets()
        {
            var transport = new FakeTransport(new SupportGateHttpResponse(200, Json.Ticket));

            Pump.RunToEnd(Client(transport).SubmitTicket(Draft(), "key-1", null));

            var body = Encoding.UTF8.GetString(transport.Last.Body);
            StringAssert.Contains("\"client\"", body);
            StringAssert.DoesNotContain("jwt-1", body);
        }

        [Test]
        public void FileGoesStraightToStorageAndServiceGetsOnlyTheId()
        {
            var transport = new FakeTransport(request =>
                request.Url.EndsWith("/v1/attachments/presign")
                    ? new SupportGateHttpResponse(200, Json.Presign)
                    : new SupportGateHttpResponse(200, string.Empty));
            var client = Client(transport);
            SupportGateAttachment attachment = null;

            Pump.RunToEnd(client.LoadForm(null));
            Pump.RunToEnd(client.UploadAttachment("shot.png", "image/png", new byte[] { 1, 2, 3 },
                result => attachment = result.Value));

            Assert.AreEqual("att-1", attachment.AttachmentId);
            Assert.AreEqual("https://storage.example/att-1", transport.Last.Url);
            Assert.AreEqual("PUT", transport.Last.Method);
            Assert.AreEqual("image/png", transport.Last.ContentType);
            Assert.IsFalse(transport.Last.Headers.ContainsKey("Authorization"));
        }

        [Test]
        public void OversizedFileNeverReachesTheNetwork()
        {
            var transport = new FakeTransport(new SupportGateHttpResponse(200, Json.Form));
            var client = Client(transport);
            SupportGateError error = null;

            Pump.RunToEnd(client.LoadForm(null));
            Pump.RunToEnd(client.UploadAttachment("big.png", "image/png", new byte[6 * 1024 * 1024],
                result => error = result.Error));

            Assert.AreEqual("file_too_large", error.Code);
            Assert.AreEqual(0, transport.CountOf("/v1/attachments/presign"));
        }

        [Test]
        public void UnsupportedTypeIsRejectedLocally()
        {
            var transport = new FakeTransport(new SupportGateHttpResponse(200, Json.Form));
            var client = Client(transport);
            SupportGateError error = null;

            Pump.RunToEnd(client.LoadForm(null));
            Pump.RunToEnd(client.UploadAttachment("save.dat", "application/octet-stream", new byte[] { 1 },
                result => error = result.Error));

            Assert.AreEqual("unsupported_type", error.Code);
            Assert.AreEqual(SupportGateErrorKind.InvalidRequest, error.Kind);
        }

        [Test]
        public void ProviderIsAskedForAFreshTokenOnEveryCall()
        {
            var issued = 0;
            var transport = new FakeTransport(new SupportGateHttpResponse(200, Json.Ticket));
            var session = SupportGateSession.FromProvider(onToken => IssueToken(onToken, "jwt-" + ++issued));
            var client = new SupportGateClient("https://support.example", session, transport);

            Pump.RunToEnd(client.SubmitTicket(Draft(), "key-1", null));
            Pump.RunToEnd(client.SubmitTicket(Draft(), "key-2", null));

            Assert.AreEqual(2, issued);
            Assert.AreEqual("Bearer jwt-2", transport.Last.Headers["Authorization"]);
        }

        /// <summary>Провайдер токена игры: здесь он отвечает сразу, без похода на сервер.</summary>
        private static IEnumerator IssueToken(Action<string> onToken, string token)
        {
            onToken(token);
            yield break;
        }

        private static SupportGateTicketDraft Draft()
        {
            return new SupportGateTicketDraft
            {
                Category = "gameplay",
                Subcategory = "progress",
                Subject = "Прогресс пропал",
                Description = "После обновления сбросился уровень"
            };
        }
    }
}
