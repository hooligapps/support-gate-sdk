using System.Reflection;
using Hooligapps.SupportGate.UI.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace Hooligapps.SupportGate.Demo
{
    /// <summary>
    /// Собирает uGUI-форму кодом, потому что префабов пакета ещё нет: их делают в
    /// редакторе. Как только префабы появятся, фабрика заменяется на
    /// Instantiate(prefab) — поля вью здесь заполняются те же самые.
    ///
    /// Ссылки кладутся рефлексией: поля вью приватные и заполняются инспектором.
    /// В игре так делать не нужно. Контролы — из DefaultControls uGUI, поэтому
    /// у Dropdown есть шаблон списка, а у InputField — плейсхолдер.
    /// </summary>
    public static class SupportGateDemoUGuiFactory
    {
        private const int FontSize = 24;

        private static readonly Color PanelColor = new Color(0.97f, 0.97f, 0.98f, 1f);
        private static readonly Color TextColor = new Color(0.07f, 0.09f, 0.11f, 1f);
        private static readonly Color MutedColor = new Color(0.36f, 0.42f, 0.45f, 1f);
        private static readonly Color ErrorColor = new Color(0.77f, 0.13f, 0.12f, 1f);
        private static readonly Color PrimaryColor = new Color(0.24f, 0.47f, 0.85f, 1f);
        private static readonly Color QuietColor = new Color(0.85f, 0.87f, 0.9f, 1f);

        private static readonly DefaultControls.Resources Resources = new DefaultControls.Resources();

        public static SupportGateFormView CreateFormView(Transform parent)
        {
            var root = CreatePanel("SupportGateFormView", parent);
            var view = root.AddComponent<SupportGateFormView>();

            var title = CreateLabel(root.transform, "Contact support", 30, TextColor, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;

            // Баннер — сам Text: вью включает и выключает его объект целиком.
            var banner = CreateLabel(root.transform, string.Empty, FontSize, ErrorColor, TextAnchor.MiddleLeft);
            banner.gameObject.AddComponent<LayoutElement>().minHeight = 48f;
            banner.gameObject.SetActive(false);

            // Экран состояния: загрузка, ошибка загрузки, «отправлено».
            var stateRoot = CreateColumn(root.transform, "State", 24);
            var state = CreateLabel(stateRoot.transform, "Loading…", FontSize + 2, MutedColor, TextAnchor.MiddleCenter);
            state.gameObject.AddComponent<LayoutElement>().minHeight = 120f;
            var retry = CreateButton(stateRoot.transform, "Try again", QuietColor, TextColor);

            // Сама форма прокручивается: категорий и доп. полей может быть много.
            var formRoot = CreateScroll(root.transform, out var content);

            var categoryLabel = CreateLabel(content, "Category", FontSize, TextColor, TextAnchor.MiddleLeft);
            var category = CreateDropdown(content);
            var categoryError = CreateError(content);

            var subcategoryLabel = CreateLabel(content, "Topic", FontSize, TextColor, TextAnchor.MiddleLeft);
            var subcategory = CreateDropdown(content);
            var subcategoryError = CreateError(content);

            var extraRoot = CreateColumn(content, "Extra", 8);
            var textRowTemplate = CreateTextRow(extraRoot.transform);
            var dropdownRowTemplate = CreateDropdownRow(extraRoot.transform);

            var emailLabel = CreateLabel(content, "Email", FontSize, TextColor, TextAnchor.MiddleLeft);
            var email = CreateInput(content, false);
            var emailError = CreateError(content);

            var subjectLabel = CreateLabel(content, "Subject", FontSize, TextColor, TextAnchor.MiddleLeft);
            var subject = CreateInput(content, false);
            var subjectError = CreateError(content);

            var descriptionLabel = CreateLabel(content, "Describe the problem", FontSize, TextColor, TextAnchor.MiddleLeft);
            var description = CreateInput(content, true);
            var descriptionError = CreateError(content);

            var attachmentsLabel = CreateLabel(content, "Attachments", FontSize, TextColor, TextAnchor.MiddleLeft);
            var attachmentsRoot = CreateColumn(content, "Attachments", 4);
            var attachmentRowTemplate = CreateAttachmentRow(attachmentsRoot.transform);
            var attach = CreateButton(content, "Add files", QuietColor, TextColor);
            var attachmentsHint = CreateLabel(content, string.Empty, FontSize - 6, MutedColor, TextAnchor.MiddleLeft);

            var footer = CreateRow(root.transform, "Footer");
            var cancel = CreateButton(footer.transform, "Cancel", QuietColor, TextColor);
            var submit = CreateButton(footer.transform, "Send", PrimaryColor, Color.white);

            SetField(view, "_category", category);
            SetField(view, "_subcategory", subcategory);
            SetField(view, "_email", email);
            SetField(view, "_subject", subject);
            SetField(view, "_description", description);

            SetField(view, "_title", title);
            SetField(view, "_banner", banner);
            SetField(view, "_state", state);
            SetField(view, "_categoryLabel", categoryLabel);
            SetField(view, "_subcategoryLabel", subcategoryLabel);
            SetField(view, "_emailLabel", emailLabel);
            SetField(view, "_subjectLabel", subjectLabel);
            SetField(view, "_descriptionLabel", descriptionLabel);
            SetField(view, "_attachmentsLabel", attachmentsLabel);
            SetField(view, "_attachmentsHint", attachmentsHint);

            SetField(view, "_categoryError", categoryError);
            SetField(view, "_subcategoryError", subcategoryError);
            SetField(view, "_emailError", emailError);
            SetField(view, "_subjectError", subjectError);
            SetField(view, "_descriptionError", descriptionError);

            SetField(view, "_submit", submit);
            SetField(view, "_cancel", cancel);
            SetField(view, "_attach", attach);
            SetField(view, "_retry", retry);
            SetField(view, "_submitText", submit.GetComponentInChildren<Text>());
            SetField(view, "_cancelText", cancel.GetComponentInChildren<Text>());
            SetField(view, "_attachText", attach.GetComponentInChildren<Text>());
            SetField(view, "_retryText", retry.GetComponentInChildren<Text>());

            SetField(view, "_formRoot", formRoot);
            SetField(view, "_stateRoot", stateRoot);
            SetField(view, "_extraRoot", extraRoot.transform);
            SetField(view, "_attachmentsRoot", attachmentsRoot.transform);
            SetField(view, "_textRowTemplate", textRowTemplate);
            SetField(view, "_dropdownRowTemplate", dropdownRowTemplate);
            SetField(view, "_attachmentRowTemplate", attachmentRowTemplate);

            return view;
        }

        public static GameObject CreateBlocker(Transform parent)
        {
            var go = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;

            rect.SetParent(parent, false);
            Stretch(rect);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            go.SetActive(false);

            return go;
        }

        public static Button CreateEntryButton(Transform parent)
        {
            var button = CreateButton(parent, "Support", PrimaryColor, Color.white);
            var rect = (RectTransform)button.transform;

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(220f, 88f);

            return button;
        }

        private static SupportGateTextRow CreateTextRow(Transform parent)
        {
            var row = CreateColumn(parent, "TextRow", 4);
            var label = CreateLabel(row.transform, "Field", FontSize, TextColor, TextAnchor.MiddleLeft);
            var input = CreateInput(row.transform, false);
            var error = CreateError(row.transform);

            var component = row.AddComponent<SupportGateTextRow>();
            SetField(component, "_label", label);
            SetField(component, "_input", input);
            SetField(component, "_error", error);

            return component;
        }

        private static SupportGateDropdownRow CreateDropdownRow(Transform parent)
        {
            var row = CreateColumn(parent, "DropdownRow", 4);
            var label = CreateLabel(row.transform, "Field", FontSize, TextColor, TextAnchor.MiddleLeft);
            var dropdown = CreateDropdown(row.transform);
            var error = CreateError(row.transform);

            var component = row.AddComponent<SupportGateDropdownRow>();
            SetField(component, "_label", label);
            SetField(component, "_dropdown", dropdown);
            SetField(component, "_error", error);

            return component;
        }

        private static SupportGateAttachmentRow CreateAttachmentRow(Transform parent)
        {
            var row = CreateRow(parent, "AttachmentRow");
            var name = CreateLabel(row.transform, "file.png", FontSize - 2, TextColor, TextAnchor.MiddleLeft);
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var status = CreateLabel(row.transform, string.Empty, FontSize - 4, MutedColor, TextAnchor.MiddleRight);
            status.gameObject.AddComponent<LayoutElement>().preferredWidth = 160f;
            var remove = CreateButton(row.transform, "Remove", QuietColor, TextColor);
            remove.GetComponent<LayoutElement>().preferredWidth = 150f;
            remove.GetComponent<LayoutElement>().flexibleWidth = 0f;

            var component = row.AddComponent<SupportGateAttachmentRow>();
            SetField(component, "_name", name);
            SetField(component, "_status", status);
            SetField(component, "_remove", remove);

            return component;
        }

        private static GameObject CreatePanel(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RectMask2D),
                typeof(VerticalLayoutGroup));

            // Гасим сразу: Awake вью подписывается на кнопки, а поля ему ещё не
            // проставлены. На префабе этой проблемы нет — там ссылки уже внутри.
            go.SetActive(false);

            var rect = (RectTransform)go.transform;

            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(640f, 860f);

            go.GetComponent<Image>().color = PanelColor;

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 12f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            return go;
        }

        private static GameObject CreateColumn(Transform parent, string name, int spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));

            go.transform.SetParent(parent, false);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            return go;
        }

        private static GameObject CreateRow(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));

            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().minHeight = 72f;

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childForceExpandWidth = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            return go;
        }

        private static GameObject CreateScroll(Transform parent, out Transform content)
        {
            var scrollGo = new GameObject("Form", typeof(RectTransform), typeof(ScrollRect), typeof(Image),
                typeof(RectMask2D), typeof(LayoutElement));

            scrollGo.transform.SetParent(parent, false);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.03f);

            var sizing = scrollGo.GetComponent<LayoutElement>();
            sizing.minHeight = 0f;
            sizing.preferredHeight = 0f;
            sizing.flexibleHeight = 1f;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            var contentRect = (RectTransform)contentGo.transform;

            contentRect.SetParent(scrollGo.transform, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = (RectTransform)scrollGo.transform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            content = contentRect;
            return scrollGo;
        }

        private static Text CreateLabel(Transform parent, string value, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));

            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = DefaultFont();
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        private static Text CreateError(Transform parent)
        {
            var error = CreateLabel(parent, string.Empty, FontSize - 6, ErrorColor, TextAnchor.MiddleLeft);
            error.gameObject.SetActive(false);
            return error;
        }

        private static Dropdown CreateDropdown(Transform parent)
        {
            var go = DefaultControls.CreateDropdown(Resources);
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().minHeight = 60f;

            var dropdown = go.GetComponent<Dropdown>();
            Style(dropdown.captionText, FontSize, TextColor);
            Style(dropdown.itemText, FontSize, TextColor);

            // Шаблон списка рассчитан на 14-й кегль: раздвигаем под наш.
            var item = (RectTransform)dropdown.itemText.transform.parent;
            item.sizeDelta = new Vector2(item.sizeDelta.x, 56f);
            dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 300f);

            var caption = (RectTransform)dropdown.captionText.transform;
            caption.offsetMin = new Vector2(16f, caption.offsetMin.y);

            return dropdown;
        }

        private static InputField CreateInput(Transform parent, bool multiline)
        {
            var go = DefaultControls.CreateInputField(Resources);
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().minHeight = multiline ? 180f : 60f;

            var input = go.GetComponent<InputField>();
            Style(input.textComponent, FontSize, TextColor);
            Style((Text)input.placeholder, FontSize, MutedColor);
            ((Text)input.placeholder).text = string.Empty;

            if (multiline)
            {
                input.lineType = InputField.LineType.MultiLineNewline;
                input.textComponent.alignment = TextAnchor.UpperLeft;
            }

            var textRect = (RectTransform)input.textComponent.transform;
            textRect.offsetMin = new Vector2(16f, textRect.offsetMin.y);

            return input;
        }

        private static Button CreateButton(Transform parent, string label, Color color, Color textColor)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement));

            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            go.GetComponent<LayoutElement>().minHeight = 64f;

            var text = CreateLabel(go.transform, label, FontSize, textColor, TextAnchor.MiddleCenter);
            Stretch((RectTransform)text.transform);

            return go.GetComponent<Button>();
        }

        private static void Style(Text text, int size, Color color)
        {
            if (text == null)
            {
                return;
            }

            text.font = DefaultFont();
            text.fontSize = size;
            text.color = color;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Font DefaultFont()
        {
            // Имя встроенного шрифта поменялось в Unity 2022.
            var font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return font != null ? font : UnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
            {
                Debug.LogError("Support Gate demo: поле " + name + " не найдено в " + target.GetType().Name);
                return;
            }

            field.SetValue(target, value);
        }
    }
}
