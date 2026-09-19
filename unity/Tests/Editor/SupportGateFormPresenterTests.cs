using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Hooligapps.SupportGate.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hooligapps.SupportGate.Tests
{
    public sealed class SupportGateTestRunner : MonoBehaviour
    {
    }

    public sealed class SupportGateFormPresenterTests
    {
        /// <summary>Вью-заглушка: хранит, что ей сказали показать, и отдаёт введённое.</summary>
        private sealed class View : ISupportGateFormView<GameObject>
        {
            public GameObject Root { get; set; }
            public SupportGateViewState State = SupportGateViewState.Hidden;
            public SupportGateFormConfig Config;
            public IReadOnlyList<SupportGateOption> Subcategories = new SupportGateOption[0];
            public IReadOnlyList<SupportGateExtraField> ExtraFields = new SupportGateExtraField[0];
            public IReadOnlyList<SupportGateAttachmentItem> Attachments = new SupportGateAttachmentItem[0];
            public IReadOnlyDictionary<string, string> Errors = new Dictionary<string, string>();
            public string Message;
            public string SentMessage;
            public SupportGateFormValues Values = new SupportGateFormValues();

            public event Action CloseRequested;
            public event Action RetryRequested;
            public event Action SubmitRequested;
            public event Action<string> CategoryChanged;
            public event Action AttachRequested;
            public event Action<int> AttachmentRemoved;

            public void SetState(SupportGateViewState state) { State = state; }
            public void BindConfig(SupportGateFormConfig config, SupportGateStrings text) { Config = config; }
            public void BindSubcategories(IReadOnlyList<SupportGateOption> options) { Subcategories = options; }
            public void BindExtraFields(IReadOnlyList<SupportGateExtraField> fields) { ExtraFields = fields; }
            public void BindAttachments(IReadOnlyList<SupportGateAttachmentItem> items) { Attachments = items; }
            public void ShowMessage(string message) { Message = message; }
            public void ShowFieldErrors(IReadOnlyDictionary<string, string> errors) { Errors = errors; }
            public void ShowSent(string message) { SentMessage = message; }
            public SupportGateFormValues ReadValues() { return Values; }

            public void RaiseSubmit() { SubmitRequested?.Invoke(); }
            public void RaiseCategory(string id) { CategoryChanged?.Invoke(id); }
            public void RaiseClose() { CloseRequested?.Invoke(); }
            public void RaiseAttachmentRemoved(int index) { AttachmentRemoved?.Invoke(index); }
            public void RaiseRetry() { RetryRequested?.Invoke(); }
            public void RaiseAttach() { AttachRequested?.Invoke(); }
        }

        private sealed class Host : ISupportGateModalHost<GameObject>
        {
            public GameObject Shown;
            public bool Allowed = true;

            public bool TryShow(GameObject content)
            {
                if (!Allowed || Shown != null)
                {
                    return false;
                }

                Shown = content;
                return true;
            }

            public void Close(GameObject content) { Shown = null; }
        }

        [UnityTest]
        public IEnumerator FormLoadsAndCategorySelectionFillsDependentFields()
        {
            yield return new EnterPlayMode();

            var go = new GameObject("support gate test runner");
            var runner = go.AddComponent<SupportGateTestRunner>();
            var view = new View { Root = go };
            var host = new Host();
            var transport = new FakeTransport(new SupportGateHttpResponse(200, Json.Form));
            var client = new SupportGateClient("https://support.example", SupportGateSession.FromToken("t"), transport);

            using (var presenter = new SupportGateFormPresenter<GameObject>(client, view, host, runner))
            {
                Assert.IsTrue(presenter.Open());
                yield return null;

                Assert.AreEqual(SupportGateViewState.Form, view.State);
                Assert.AreSame(go, host.Shown);

                view.RaiseCategory("payment");
                Assert.AreEqual(1, view.Subcategories.Count);
                Assert.AreEqual(1, view.ExtraFields.Count);

                view.RaiseCategory("gameplay");
                Assert.AreEqual(0, view.ExtraFields.Count);
            }

            UnityEngine.Object.DestroyImmediate(go);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RefusedHostKeepsTheWindowClosed()
        {
            yield return new EnterPlayMode();

            var go = new GameObject("support gate test runner");
            var runner = go.AddComponent<SupportGateTestRunner>();
            var view = new View { Root = go };
            var host = new Host { Allowed = false };
            var transport = new FakeTransport(new SupportGateHttpResponse(200, Json.Form));
            var client = new SupportGateClient("https://support.example", SupportGateSession.FromToken("t"), transport);

            using (var presenter = new SupportGateFormPresenter<GameObject>(client, view, host, runner))
            {
                var refused = 0;
                presenter.HostRefused += () => refused++;

                Assert.IsFalse(presenter.Open());
                yield return null;

                Assert.AreEqual(1, refused);
                Assert.IsFalse(presenter.IsVisible);
                Assert.AreEqual(0, transport.Requests.Count);
            }

            UnityEngine.Object.DestroyImmediate(go);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator EmptyRequiredFieldsBlockTheRequest()
        {
            yield return new EnterPlayMode();

            var go = new GameObject("support gate test runner");
            var runner = go.AddComponent<SupportGateTestRunner>();
            var view = new View { Root = go };
            var transport = new FakeTransport(new SupportGateHttpResponse(200, Json.Form));
            var client = new SupportGateClient("https://support.example", SupportGateSession.FromToken("t"), transport);

            using (var presenter = new SupportGateFormPresenter<GameObject>(client, view, new Host(), runner))
            {
                presenter.Open();
                yield return null;

                view.RaiseSubmit();
                yield return null;

                Assert.AreEqual(0, transport.CountOf("/v1/tickets"));
                Assert.IsTrue(view.Errors.ContainsKey("category"));
                Assert.IsTrue(view.Errors.ContainsKey("subject"));
                Assert.IsTrue(view.Errors.ContainsKey("email"));
            }

            UnityEngine.Object.DestroyImmediate(go);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FilledFormIsSentOnceAndShowsTheTicketNumber()
        {
            yield return new EnterPlayMode();

            var go = new GameObject("support gate test runner");
            var runner = go.AddComponent<SupportGateTestRunner>();
            var view = new View { Root = go };
            var transport = new FakeTransport(request =>
                request.Url.EndsWith("/v1/form")
                    ? new SupportGateHttpResponse(200, Json.Form)
                    : new SupportGateHttpResponse(200, Json.Ticket));
            var client = new SupportGateClient("https://support.example", SupportGateSession.FromToken("t"), transport);

            using (var presenter = new SupportGateFormPresenter<GameObject>(client, view, new Host(), runner))
            {
                SupportGateTicket ticket = null;
                presenter.Submitted += value => ticket = value;

                presenter.Open();
                yield return null;

                view.Values = Filled();
                view.RaiseSubmit();
                yield return null;

                Assert.AreEqual(1, transport.CountOf("/v1/tickets"));
                Assert.AreEqual(SupportGateViewState.Sent, view.State);
                Assert.AreEqual("SUP-1", ticket.IssueKey);
                StringAssert.Contains("SUP-1", view.SentMessage);
            }

            UnityEngine.Object.DestroyImmediate(go);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RetryAfterFailureKeepsTheIdempotencyKey()
        {
            yield return new EnterPlayMode();

            var go = new GameObject("support gate test runner");
            var runner = go.AddComponent<SupportGateTestRunner>();
            var view = new View { Root = go };
            var server = new FlakyServer();
            var transport = new FakeTransport(server.Handle);
            var client = new SupportGateClient("https://support.example", SupportGateSession.FromToken("t"), transport);

            using (var presenter = new SupportGateFormPresenter<GameObject>(client, view, new Host(), runner))
            {
                presenter.Open();
                yield return null;

                view.Values = Filled();
                view.RaiseSubmit();
                yield return null;

                Assert.AreEqual(SupportGateViewState.Form, view.State);

                view.RaiseSubmit();
                yield return null;

                Assert.AreEqual(2, server.Keys.Count);
                Assert.AreEqual(server.Keys[0], server.Keys[1]);
                Assert.AreEqual(SupportGateViewState.Sent, view.State);
            }

            UnityEngine.Object.DestroyImmediate(go);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ConfirmedEmailIsNotSentBack()
        {
            yield return new EnterPlayMode();

            var go = new GameObject("support gate test runner");
            var runner = go.AddComponent<SupportGateTestRunner>();
            var view = new View { Root = go };
            var transport = new FakeTransport(LockedEmailServer.Handle);
            var client = new SupportGateClient("https://support.example", SupportGateSession.FromToken("t"), transport);

            using (var presenter = new SupportGateFormPresenter<GameObject>(client, view, new Host(), runner))
            {
                presenter.Open();
                yield return null;

                var values = Filled();
                values.Email = null;
                view.Values = values;
                view.RaiseSubmit();
                yield return null;

                var body = Encoding.UTF8.GetString(transport.Last.Body);
                StringAssert.DoesNotContain("email", body);
                Assert.AreEqual(SupportGateViewState.Sent, view.State);
            }

            UnityEngine.Object.DestroyImmediate(go);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator UnfinishedUploadHoldsTheSubmission()
        {
            yield return new EnterPlayMode();

            var go = new GameObject("support gate test runner");
            var runner = go.AddComponent<SupportGateTestRunner>();
            var view = new View { Root = go };
            var transport = new FakeTransport(request =>
            {
                if (request.Url.EndsWith("/v1/form")) return new SupportGateHttpResponse(200, Json.Form);
                if (request.Url.EndsWith("/v1/attachments/presign")) return new SupportGateHttpResponse(200, Json.Presign);
                if (request.Url.StartsWith("https://storage.example")) return new SupportGateHttpResponse(200, string.Empty);
                return new SupportGateHttpResponse(200, Json.Ticket);
            }) { DelayFrames = 3 };
            var client = new SupportGateClient("https://support.example", SupportGateSession.FromToken("t"), transport);

            using (var presenter = new SupportGateFormPresenter<GameObject>(client, view, new Host(), runner))
            {
                presenter.Open();
                for (var i = 0; i < 5; i++) yield return null;

                presenter.AttachFile("shot.png", "image/png", new byte[] { 1, 2, 3 });
                view.Values = Filled();
                view.RaiseSubmit();

                Assert.AreEqual(0, transport.CountOf("/v1/tickets"));

                for (var i = 0; i < 12; i++) yield return null;

                Assert.AreEqual(1, presenter.Attachments.Count);
                Assert.AreEqual(SupportGateUploadState.Ready, presenter.Attachments[0].State);

                view.RaiseSubmit();
                for (var i = 0; i < 12; i++) yield return null;

                Assert.AreEqual(1, transport.CountOf("/v1/tickets"));
                Assert.AreEqual(SupportGateViewState.Sent, view.State);
            }

            UnityEngine.Object.DestroyImmediate(go);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CompletedReportsSentErrorsAndClosedWithoutSending()
        {
            yield return new EnterPlayMode();

            var go = new GameObject("support gate test runner");
            var runner = go.AddComponent<SupportGateTestRunner>();
            var view = new View { Root = go };
            var server = new FlakyServer();
            var transport = new FakeTransport(server.Handle);
            var client = new SupportGateClient("https://support.example", SupportGateSession.FromToken("t"), transport);
            var results = new ResultLog();

            using (var presenter = new SupportGateFormPresenter<GameObject>(client, view, new Host(), runner))
            {
                presenter.Completed += results.Add;

                presenter.Open();
                yield return null;
                view.RaiseClose();
                Assert.AreEqual("closed", results.Joined);

                results.Items.Clear();
                presenter.Open();
                yield return null;
                view.Values = Filled();
                view.RaiseSubmit();
                yield return null;
                Assert.AreEqual(1, results.Items.Count);
                Assert.AreEqual(SupportGateResultStatus.Error, results.Items[0].Status);
                Assert.AreEqual(SupportGateResultStage.Submit, results.Items[0].Stage);
                Assert.AreEqual(503, results.Items[0].Error.HttpStatus);

                view.RaiseSubmit();
                yield return null;
                Assert.AreEqual(2, results.Items.Count);
                Assert.AreEqual(SupportGateResultStatus.Sent, results.Items[1].Status);
                Assert.AreEqual("SUP-1", results.Items[1].Ticket.IssueKey);
                // Строки пришли с сервера на языке игрока.
                Assert.AreEqual("Номер обращения — SUP-1.", view.SentMessage);

                view.RaiseClose();
                Assert.AreEqual(2, results.Items.Count, "после отправки Closed не приходит");
            }

            UnityEngine.Object.DestroyImmediate(go);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FailedFormLoadIsReportedAsLoadError()
        {
            yield return new EnterPlayMode();

            var go = new GameObject("support gate test runner");
            var runner = go.AddComponent<SupportGateTestRunner>();
            var view = new View { Root = go };
            var transport = new FakeTransport(new SupportGateHttpResponse(0, null, "Cannot connect"));
            var client = new SupportGateClient("https://support.example", SupportGateSession.FromToken("t"), transport);
            var results = new ResultLog();

            using (var presenter = new SupportGateFormPresenter<GameObject>(client, view, new Host(), runner))
            {
                presenter.Completed += results.Add;
                presenter.Open();
                yield return null;

                Assert.AreEqual(SupportGateViewState.LoadFailed, view.State);
                Assert.AreEqual(1, results.Items.Count);
                Assert.AreEqual(SupportGateResultStage.Load, results.Items[0].Stage);
                Assert.AreEqual(SupportGateErrorKind.Network, results.Items[0].Error.Kind);

                view.RaiseClose();
                Assert.AreEqual("error load, closed", results.Joined);
            }

            UnityEngine.Object.DestroyImmediate(go);
            yield return new ExitPlayMode();
        }

        private sealed class ResultLog
        {
            public readonly List<SupportGateFormResult> Items = new List<SupportGateFormResult>();

            public void Add(SupportGateFormResult result)
            {
                Items.Add(result);
            }

            public string Joined
            {
                get
                {
                    var parts = new List<string>();
                    foreach (var item in Items)
                    {
                        parts.Add(item.Status == SupportGateResultStatus.Error
                            ? "error " + item.Stage.ToString().ToLowerInvariant()
                            : item.Status.ToString().ToLowerInvariant());
                    }

                    return string.Join(", ", parts);
                }
            }
        }

        /// <summary>
        /// Состояние сервера-заглушки живёт в объекте, а не в замыкании: локальные
        /// переменные, захваченные лямбдой до EnterPlayMode, не переживают
        /// перезагрузку домена.
        /// </summary>
        private sealed class FlakyServer
        {
            public readonly List<string> Keys = new List<string>();
            private int _failures = 1;

            public SupportGateHttpResponse Handle(SupportGateHttpRequest request)
            {
                if (request.Url.EndsWith("/v1/form"))
                {
                    return new SupportGateHttpResponse(200, Json.Form);
                }

                Keys.Add(request.Headers["Idempotency-Key"]);
                if (_failures-- > 0)
                {
                    return new SupportGateHttpResponse(503, @"{""error"": ""unavailable""}");
                }

                return new SupportGateHttpResponse(200, Json.Ticket);
            }
        }

        private static class LockedEmailServer
        {
            public static SupportGateHttpResponse Handle(SupportGateHttpRequest request)
            {
                if (!request.Url.EndsWith("/v1/form"))
                {
                    return new SupportGateHttpResponse(200, Json.Ticket);
                }

                var form = Json.Form.Replace(@"""email"": null, ""email_locked"": false",
                    @"""email"": ""player@example.com"", ""email_locked"": true");
                return new SupportGateHttpResponse(200, form);
            }
        }

        private static SupportGateFormValues Filled()
        {
            return new SupportGateFormValues
            {
                Category = "gameplay",
                Subcategory = "progress",
                Email = "player@example.com",
                Subject = "Прогресс пропал",
                Description = "После обновления сбросился уровень"
            };
        }
    }
}
