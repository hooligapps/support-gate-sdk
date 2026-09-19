using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hooligapps.SupportGate.UI;
using Hooligapps.SupportGate.UI.UGUI;
using Hooligapps.SupportGate.UI.UIToolkit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;

namespace Hooligapps.SupportGate.Demo.Tests
{
    /// <summary>
    /// Сквозной прогон стенда на настоящих вью обоих рендереров: тесты пакета
    /// работают с вью-заглушкой, здесь проверяется, что uGUI и UI Toolkit
    /// действительно показывают строки сервера, собирают значения и доходят до
    /// экрана «отправлено».
    /// </summary>
    public sealed class SupportGateDemoFlowTests
    {
        private GameObject _documentGo;
        private GameObject _appGo;
        private SupportGateDemoApp _app;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _documentGo = new GameObject("UIDocument", typeof(UIDocument));
            var document = _documentGo.GetComponent<UIDocument>();
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1080, 1920);
            document.panelSettings = settings;

            _appGo = new GameObject("SupportGateDemoApp");
            _appGo.SetActive(false);
            _app = _appGo.AddComponent<SupportGateDemoApp>();
            SetField(_app, "_document", document);
            _appGo.SetActive(true);

            yield return null;

            _app.Transport.DelaySeconds = 0f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_appGo);
            Object.Destroy(_documentGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UGuiFormShowsServerStringsAndSubmits()
        {
            _app.UsesUGui = true;
            _app.Transport.Form = SupportGateDemoFormScenario.Russian;

            var results = new List<SupportGateFormResult>();
            _app.UGuiPresenter.Completed += results.Add;

            Assert.IsTrue(_app.UGuiPresenter.Open());
            yield return Settle();

            var view = Object.FindFirstObjectByType<SupportGateFormView>();
            Assert.IsNotNull(view);
            Assert.IsTrue(view.gameObject.activeInHierarchy);

            var labels = new List<string>();
            foreach (var text in view.GetComponentsInChildren<Text>())
            {
                labels.Add(text.text);
            }

            CollectionAssert.Contains(labels, "Обращение в поддержку");
            CollectionAssert.Contains(labels, "Отправить");
            CollectionAssert.Contains(labels, "До 3 файлов, по 5 МБ.");

            var dropdowns = Active(view.GetComponentsInChildren<Dropdown>());
            var inputs = Active(view.GetComponentsInChildren<InputField>());
            Assert.AreEqual(2, dropdowns.Count, "категория и тема");
            Assert.AreEqual(3, inputs.Count, "почта, тема, описание");

            Assert.AreEqual("Выберите…", dropdowns[0].options[0].text);
            Assert.AreEqual("Платежи", dropdowns[0].options[2].text);

            // Категория «Платежи» → появляются подкатегории и доп. поля.
            dropdowns[0].value = 2;
            yield return null;
            Assert.AreEqual(3, dropdowns[1].options.Count);
            Assert.AreEqual(3, Active(view.GetComponentsInChildren<Dropdown>()).Count, "плюс способ оплаты");
            Assert.AreEqual(5, Active(view.GetComponentsInChildren<InputField>()).Count, "плюс дата и ID транзакции");

            dropdowns[1].value = 1;
            inputs[0].text = "player@example.com";
            inputs[1].text = "Покупка не пришла";
            inputs[2].text = "Оплатил набор, кристаллы не начислились";
            Active(view.GetComponentsInChildren<Dropdown>())[2].value = 1;

            // Вложение: presign → PUT в «хранилище» → строка со статусом Ready.
            _app.UGuiPresenter.AttachFile("shot.png", "image/png", new byte[] { 1, 2, 3 });
            yield return Settle();
            Assert.AreEqual(1, _app.UGuiPresenter.Attachments.Count);
            Assert.AreEqual(SupportGateUploadState.Ready, _app.UGuiPresenter.Attachments[0].State);

            Click(view, "Отправить");
            yield return Settle();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(SupportGateResultStatus.Sent, results[0].Status);
            Assert.AreEqual("SUP-101", results[0].Ticket.IssueKey);
            Assert.IsFalse(_app.UGuiPresenter.IsSending);

            var sent = false;
            foreach (var text in view.GetComponentsInChildren<Text>())
            {
                sent |= text.text == "Номер обращения — SUP-101. Ответ придёт на почту.";
            }

            Assert.IsTrue(sent, "экран «отправлено» со строкой сервера");

            _app.UGuiPresenter.Close();
            Assert.AreEqual(1, results.Count, "после отправки Closed не приходит");
        }

        [UnityTest]
        public IEnumerator UGuiValidationErrorsFromServerLandOnFields()
        {
            _app.UsesUGui = true;
            _app.Transport.Form = SupportGateDemoFormScenario.English;
            _app.Transport.Submit = SupportGateDemoSubmitScenario.ValidationError;

            var results = new List<SupportGateFormResult>();
            _app.UGuiPresenter.Completed += results.Add;

            _app.UGuiPresenter.Open();
            yield return Settle();

            var view = Object.FindFirstObjectByType<SupportGateFormView>();
            var dropdowns = Active(view.GetComponentsInChildren<Dropdown>());
            var inputs = Active(view.GetComponentsInChildren<InputField>());

            // Пустая форма: сервер не вызывается, ошибки локальные.
            Submit(view);
            yield return null;
            Assert.AreEqual(0, results.Count);
            Assert.IsTrue(HasText(view, "Required field"));

            dropdowns[0].value = 1;
            yield return null;
            dropdowns[1].value = 1;
            inputs[0].text = "player@example.com";
            inputs[1].text = "Hi";
            inputs[2].text = "Something broke";

            Submit(view);
            yield return Settle();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(SupportGateResultStatus.Error, results[0].Status);
            Assert.AreEqual(SupportGateResultStage.Submit, results[0].Stage);
            Assert.AreEqual("validation_error", results[0].Error.Code);
            Assert.IsTrue(HasText(view, "Too short"), "ошибка поля от сервера показана у поля");
            Assert.IsTrue(HasText(view, "Check the highlighted fields"), "сообщение сервера в баннере");

            _app.UGuiPresenter.Close();
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(SupportGateResultStatus.Closed, results[1].Status);
        }

        [UnityTest]
        public IEnumerator UGuiFailedLoadOffersRetry()
        {
            _app.UsesUGui = true;
            _app.Transport.Form = SupportGateDemoFormScenario.NetworkError;

            var results = new List<SupportGateFormResult>();
            _app.UGuiPresenter.Completed += results.Add;

            _app.UGuiPresenter.Open();
            yield return Settle();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(SupportGateResultStage.Load, results[0].Stage);

            var view = Object.FindFirstObjectByType<SupportGateFormView>();
            Assert.IsTrue(HasText(view, "Could not load the form."));
            Assert.IsTrue(HasText(view, "No connection to the server. Check the network and try again."));

            _app.Transport.Form = SupportGateDemoFormScenario.English;
            Click(view, "Try again");
            yield return Settle();

            Assert.AreEqual(1, results.Count, "успешная повторная загрузка — не результат");
            Assert.AreEqual(2, Active(view.GetComponentsInChildren<Dropdown>()).Count, "форма показана");
        }

        [UnityTest]
        public IEnumerator ToolkitFormMirrorsForArabicAndSubmits()
        {
            _app.UsesUGui = false;
            Assume.That(_app.ToolkitPresenter, Is.Not.Null, "UIDocument в тесте есть");
            _app.Transport.Form = SupportGateDemoFormScenario.Arabic;

            var results = new List<SupportGateFormResult>();
            _app.ToolkitPresenter.Completed += results.Add;

            Assert.IsTrue(_app.ToolkitPresenter.Open());
            yield return Settle();

            var document = _documentGo.GetComponent<UIDocument>();
            var form = document.rootVisualElement.Q<SupportGateFormElement>();
            Assert.IsNotNull(form);
            Assert.IsTrue(form.ClassListContains("sg-rtl"), "направление письма с сервера");
            Assert.AreEqual("الاتصال بالدعم", form.Q<Label>(className: "sg-title").text);

            var dropdowns = form.Query<DropdownField>().ToList();
            var fields = form.Query<TextField>().ToList();
            Assert.AreEqual(2, dropdowns.Count);
            Assert.AreEqual(3, fields.Count);
            Assert.AreEqual("اختر…", dropdowns[0].choices[0]);

            dropdowns[0].index = 1;
            yield return null;
            Assert.AreEqual(3, dropdowns[1].choices.Count);
            dropdowns[1].index = 1;
            fields[0].value = "player@example.com";
            fields[1].value = "موضوع";
            fields[2].value = "وصف المشكلة";

            var buttons = form.Query<UnityEngine.UIElements.Button>().ToList();
            UnityEngine.UIElements.Button submit = null;
            foreach (var button in buttons)
            {
                if (button.text == "إرسال") submit = button;
            }

            Assert.IsNotNull(submit, "кнопка отправки подписана строкой сервера");
            using (var evt = new NavigationSubmitEvent { target = submit })
            {
                submit.SendEvent(evt);
            }

            yield return Settle();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(SupportGateResultStatus.Sent, results[0].Status);
            StringAssert.Contains("SUP-101", form.Q<Label>(className: "sg-state").text);

            _app.ToolkitPresenter.Close();
            Assert.IsNull(document.rootVisualElement.Q(name: "sg-demo-overlay"), "оверлей снят");
            Assert.AreEqual(1, results.Count);
        }

        [UnityTest]
        public IEnumerator RefusingHostReportsNothing()
        {
            _app.UsesUGui = true;
            var host = (SupportGateDemoCanvasHost)GetField(_app, "_uguiHost");
            host.CanShow = false;

            var results = new List<SupportGateFormResult>();
            var refused = 0;
            _app.UGuiPresenter.Completed += results.Add;
            _app.UGuiPresenter.HostRefused += () => refused++;

            Assert.IsFalse(_app.UGuiPresenter.Open());
            yield return null;

            Assert.AreEqual(1, refused);
            Assert.AreEqual(0, results.Count);
        }

        private static IEnumerator Settle()
        {
            for (var i = 0; i < 6; i++)
            {
                yield return null;
            }
        }

        private static void Submit(SupportGateFormView view)
        {
            Click(view, "Send");
        }

        private static void Click(SupportGateFormView view, string label)
        {
            foreach (var button in view.GetComponentsInChildren<Button>())
            {
                var text = button.GetComponentInChildren<Text>();
                if (text != null && text.text == label && button.gameObject.activeInHierarchy)
                {
                    button.onClick.Invoke();
                    return;
                }
            }

            Assert.Fail("кнопка «" + label + "» не найдена");
        }

        private static bool HasText(SupportGateFormView view, string value)
        {
            foreach (var text in view.GetComponentsInChildren<Text>())
            {
                if (text.text == value) return true;
            }

            return false;
        }

        private static List<T> Active<T>(T[] components) where T : Component
        {
            var active = new List<T>();
            foreach (var component in components)
            {
                if (component.gameObject.activeInHierarchy) active.Add(component);
            }

            return active;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static object GetField(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }
    }
}
