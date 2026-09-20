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
    /// Раскладка и палитра повторяют веб-версию (sdk/js/src/ui/styles.ts) в
    /// масштабе ×1.6 под канвас 1080×1920: шапка с крестиком, две колонки 2:3,
    /// плитки вложений с превью, подвал с кнопками. Скругления — процедурный
    /// 9-slice спрайт, картинок в демо нет.
    ///
    /// Ссылки кладутся рефлексией: поля вью приватные и заполняются инспектором.
    /// В игре так делать не нужно.
    /// </summary>
    public static class SupportGateDemoUGuiFactory
    {
        private const int FontSize = 22;
        private const int SmallFontSize = 19;
        private const float ControlHeight = 56f;
        private const float TileSize = 180f;
        private const float Radius = 12f;

        private static readonly Color Background = Rgb(255, 255, 255);
        private static readonly Color Foreground = Rgb(17, 24, 28);
        private static readonly Color Muted = Rgb(92, 107, 115);
        private static readonly Color Line = Rgb(215, 222, 227);
        private static readonly Color Accent = Rgb(31, 111, 235);
        private static readonly Color Danger = Rgb(197, 34, 31);
        private static readonly Color DangerBackground = Rgb(253, 236, 234);
        private static readonly Color ThumbBackground = Rgb(238, 241, 244);
        private static readonly Color HoverBackground = Rgb(243, 245, 247);

        private static readonly DefaultControls.Resources Resources = new DefaultControls.Resources();
        private static Sprite _rounded;

        public static SupportGateFormView CreateFormView(Transform parent)
        {
            var root = CreatePanel("SupportGateFormView", parent);
            var view = root.AddComponent<SupportGateFormView>();

            // Шапка: заголовок слева, крестик справа, снизу линия.
            var header = CreateRow(root.transform, "Header", 24, 32);
            var title = CreateLabel(header.transform, "Contact support", 26, Foreground, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var close = CreateFlatButton(header.transform, "×", 36, Muted, 64f);
            CreateLine(root.transform);

            // Баннер ошибки над телом и над экраном состояния: сообщение о сбое
            // загрузки видно рядом с «Повторить». Вью включает подложку (_bannerRoot).
            var bannerHolder = CreateColumn(root.transform, "BannerHolder", 0);
            bannerHolder.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(32, 32, 22, 0);
            var banner = CreateLabel(bannerHolder.transform, string.Empty, FontSize, Danger, TextAnchor.MiddleLeft);
            Wrap(banner, "Banner", DangerBackground, 16, 20);
            bannerHolder.SetActive(false);

            // Экран состояния: загрузка, ошибка загрузки, «отправлено».
            var stateRoot = CreateColumn(root.transform, "State", 16);
            stateRoot.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(32, 32, 44, 44);
            stateRoot.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            stateRoot.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            var state = CreateLabel(stateRoot.transform, "Loading…", FontSize, Muted, TextAnchor.MiddleCenter);
            state.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var retry = CreateButton(stateRoot.transform, "Try again", false);
            retry.GetComponent<LayoutElement>().flexibleWidth = 0f;
            retry.GetComponent<LayoutElement>().preferredWidth = 260f;

            // Тело прокручивается: категорий и доп. полей может быть много.
            var formRoot = CreateScroll(root.transform, out var content);

            var columns = CreateColumns(content);
            var left = CreateColumn(columns.transform, "Left", 0);
            left.GetComponent<LayoutElement>().flexibleWidth = 2f;
            var right = CreateColumn(columns.transform, "Right", 0);
            right.GetComponent<LayoutElement>().flexibleWidth = 3f;

            var categoryLabel = CreateFieldLabel(left.transform, "Category");
            var category = CreateDropdown(left.transform);
            var categoryError = CreateError(left.transform);

            var subcategoryLabel = CreateFieldLabel(left.transform, "Topic");
            var subcategory = CreateDropdown(left.transform);
            var subcategoryError = CreateError(left.transform);

            var extraRoot = CreateColumn(left.transform, "Extra", 0);
            var textRowTemplate = CreateTextRow(extraRoot.transform);
            var dropdownRowTemplate = CreateDropdownRow(extraRoot.transform);

            var emailLabel = CreateFieldLabel(right.transform, "Email");
            var email = CreateInput(right.transform, false);
            var emailError = CreateError(right.transform);

            var subjectLabel = CreateFieldLabel(right.transform, "Subject");
            var subject = CreateInput(right.transform, false);
            var subjectError = CreateError(right.transform);

            var descriptionLabel = CreateFieldLabel(right.transform, "Describe the problem");
            var description = CreateInput(right.transform, true);
            var descriptionError = CreateError(right.transform);

            var attachmentsLabel = CreateFieldLabel(right.transform, "Attachments");
            var attachmentsRoot = CreateGrid(right.transform);
            var attachmentRowTemplate = CreateAttachmentTile(attachmentsRoot.transform);
            var attach = CreateDropTile(attachmentsRoot.transform);
            var attachmentsHint = CreateLabel(right.transform, string.Empty, SmallFontSize, Muted, TextAnchor.MiddleLeft);

            // Подвал: линия сверху, кнопки прижаты вправо.
            CreateLine(root.transform);
            var footer = CreateRow(root.transform, "Footer", 22, 32);
            footer.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            footer.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var cancel = CreateButton(footer.transform, "Cancel", false);
            var submit = CreateButton(footer.transform, "Send", true);

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
            SetField(view, "_attachText", attach.transform.Find("Inner/Caption").GetComponent<Text>());
            SetField(view, "_bannerRoot", bannerHolder);
            SetField(view, "_retryText", retry.GetComponentInChildren<Text>());

            SetField(view, "_formRoot", formRoot);
            SetField(view, "_stateRoot", stateRoot);
            SetField(view, "_extraRoot", extraRoot.transform);
            SetField(view, "_attachmentsRoot", attachmentsRoot.transform);
            SetField(view, "_textRowTemplate", textRowTemplate);
            SetField(view, "_dropdownRowTemplate", dropdownRowTemplate);
            SetField(view, "_attachmentRowTemplate", attachmentRowTemplate);

            // Крестик в шапке — то же, что «Отмена»: у вью для него отдельного поля нет.
            close.onClick.AddListener(() => cancel.onClick.Invoke());

            return view;
        }

        public static GameObject CreateBlocker(Transform parent)
        {
            var go = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;

            rect.SetParent(parent, false);
            Stretch(rect);
            go.GetComponent<Image>().color = new Color(14f / 255f, 20f / 255f, 24f / 255f, 0.55f);
            go.SetActive(false);

            return go;
        }

        public static Button CreateEntryButton(Transform parent)
        {
            var button = CreateButton(parent, "Support", true);
            var rect = (RectTransform)button.transform;

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(220f, 80f);

            return button;
        }

        // --- Ряды доп. полей и плитки вложений -------------------------------

        private static SupportGateTextRow CreateTextRow(Transform parent)
        {
            var row = CreateColumn(parent, "TextRow", 0);
            var label = CreateFieldLabel(row.transform, "Field");
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
            var row = CreateColumn(parent, "DropdownRow", 0);
            var label = CreateFieldLabel(row.transform, "Field");
            var dropdown = CreateDropdown(row.transform);
            var error = CreateError(row.transform);

            var component = row.AddComponent<SupportGateDropdownRow>();
            SetField(component, "_label", label);
            SetField(component, "_dropdown", dropdown);
            SetField(component, "_error", error);

            return component;
        }

        /// <summary>Плитка файла: превью или расширение сверху, имя и размер снизу, крестик в углу.</summary>
        private static SupportGateAttachmentRow CreateAttachmentTile(Transform parent)
        {
            var tile = CreateRounded("AttachmentTile", parent, Line);
            var inner = CreateRounded("Inner", tile.transform, Background);
            Inset((RectTransform)inner.transform, 1f);

            var layout = inner.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            var thumb = new GameObject("Thumb", typeof(RectTransform), typeof(Image), typeof(LayoutElement),
                typeof(RectMask2D), typeof(CanvasGroup));
            thumb.transform.SetParent(inner.transform, false);
            thumb.GetComponent<Image>().color = ThumbBackground;
            thumb.GetComponent<LayoutElement>().preferredHeight = 128f;

            var preview = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            preview.transform.SetParent(thumb.transform, false);
            Stretch((RectTransform)preview.transform);
            preview.GetComponent<RawImage>().raycastTarget = false;

            var extension = CreateLabel(thumb.transform, "PNG", SmallFontSize, Muted, TextAnchor.MiddleCenter);
            extension.fontStyle = FontStyle.Bold;
            Stretch((RectTransform)extension.transform);

            var meta = CreateColumn(inner.transform, "Meta", 0);
            meta.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(10, 10, 8, 8);
            var name = CreateLabel(meta.transform, "file.png", 17, Foreground, TextAnchor.MiddleLeft);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            var status = CreateLabel(meta.transform, string.Empty, 17, Muted, TextAnchor.MiddleLeft);

            var remove = CreateFlatButton(tile.transform, "×", 26, Color.white, 36f);
            var removeRect = (RectTransform)remove.transform;
            removeRect.anchorMin = new Vector2(1f, 1f);
            removeRect.anchorMax = new Vector2(1f, 1f);
            removeRect.pivot = new Vector2(1f, 1f);
            removeRect.anchoredPosition = new Vector2(-6f, -6f);
            removeRect.sizeDelta = new Vector2(36f, 36f);
            remove.GetComponent<Image>().color = new Color(14f / 255f, 20f / 255f, 24f / 255f, 0.7f);
            remove.GetComponent<Image>().sprite = Rounded();
            remove.GetComponent<Image>().type = Image.Type.Sliced;

            var component = tile.AddComponent<SupportGateAttachmentRow>();
            SetField(component, "_name", name);
            SetField(component, "_status", status);
            SetField(component, "_remove", remove);
            SetField(component, "_preview", preview.GetComponent<RawImage>());
            SetField(component, "_extension", extension);
            SetField(component, "_pending", thumb.GetComponent<CanvasGroup>());

            return component;
        }

        /// <summary>Плитка «+ Добавить файлы»: пунктирной рамки в uGUI нет — тонкая линия.</summary>
        private static Button CreateDropTile(Transform parent)
        {
            var tile = CreateRounded("Add files", parent, Line);
            var button = tile.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = Accent;
            colors.pressedColor = Accent;
            button.colors = colors;

            var inner = CreateRounded("Inner", tile.transform, Background);
            Inset((RectTransform)inner.transform, 2f);
            inner.GetComponent<Image>().raycastTarget = false;

            var layout = inner.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.padding = new RectOffset(8, 8, 8, 8);

            var icon = CreateLabel(inner.transform, "+", 42, Accent, TextAnchor.MiddleCenter);
            icon.raycastTarget = false;
            var caption = CreateLabel(inner.transform, "Add files", SmallFontSize, Muted, TextAnchor.MiddleCenter);
            caption.name = "Caption";
            caption.raycastTarget = false;

            return button;
        }

        // --- Контейнеры -----------------------------------------------------

        private static GameObject CreatePanel(string name, Transform parent)
        {
            var go = CreateRounded(name, parent, Background);
            go.AddComponent<RectMask2D>();

            // Гасим сразу: Awake вью подписывается на кнопки, а поля ему ещё не
            // проставлены. На префабе этой проблемы нет — там ссылки уже внутри.
            go.SetActive(false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1000f, 1500f);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            return go;
        }

        private static GameObject CreateRounded(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = Rounded();
            image.type = Image.Type.Sliced;
            image.color = color;

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

        private static GameObject CreateRow(Transform parent, string name, int paddingY, int paddingX)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(paddingX, paddingX, paddingY, paddingY);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            return go;
        }

        /// <summary>Две колонки 2:3; уже 1000 px складываются в одну.</summary>
        private static GameObject CreateColumns(Transform parent)
        {
            var go = new GameObject("Columns", typeof(RectTransform), typeof(SupportGateDemoColumns));
            go.transform.SetParent(parent, false);

            var columns = go.GetComponent<SupportGateDemoColumns>();
            columns.spacing = 32f;
            columns.childForceExpandHeight = false;
            columns.childControlHeight = true;
            columns.childControlWidth = true;

            return go;
        }

        private static GameObject CreateGrid(Transform parent)
        {
            var go = new GameObject("Files", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var grid = go.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(TileSize, TileSize);
            grid.spacing = new Vector2(16f, 16f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.Flexible;

            // Сетка не считает свою высоту по рядам без подсказки о ширине —
            // компонент ниже пересчитывает preferredHeight по числу плиток.
            go.AddComponent<SupportGateDemoGridHeight>();

            return go;
        }

        private static GameObject CreateScroll(Transform parent, out Transform content)
        {
            var scrollGo = new GameObject("Body", typeof(RectTransform), typeof(ScrollRect), typeof(Image),
                typeof(RectMask2D), typeof(LayoutElement));
            scrollGo.transform.SetParent(parent, false);
            scrollGo.GetComponent<Image>().color = Background;

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
            layout.spacing = 0f;
            layout.padding = new RectOffset(32, 32, 22, 22);
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

        private static void CreateLine(Transform parent)
        {
            var go = new GameObject("Line", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Line;
            go.GetComponent<LayoutElement>().preferredHeight = 1f;
        }

        /// <summary>Оборачивает текст в скруглённую подложку с отступами (баннер ошибки).</summary>
        private static GameObject Wrap(Text text, string name, Color color, int paddingY, int paddingX)
        {
            var box = CreateRounded(name, text.transform.parent, color);
            box.transform.SetSiblingIndex(text.transform.GetSiblingIndex());

            var layout = box.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(paddingX, paddingX, paddingY, paddingY);
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            text.transform.SetParent(box.transform, false);
            box.AddComponent<LayoutElement>();

            return box;
        }

        // --- Контролы -------------------------------------------------------

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

        private static Text CreateFieldLabel(Transform parent, string value)
        {
            var label = CreateLabel(parent, value, FontSize, Foreground, TextAnchor.MiddleLeft);
            label.fontStyle = FontStyle.Bold;
            label.gameObject.AddComponent<LayoutElement>().minHeight = 34f;
            return label;
        }

        private static Text CreateError(Transform parent)
        {
            var error = CreateLabel(parent, string.Empty, SmallFontSize, Danger, TextAnchor.MiddleLeft);
            error.gameObject.SetActive(false);
            AddSpacing(error.gameObject, 16f);
            return error;
        }

        /// <summary>Скруглённая рамка цвета линии с белым контролом внутри; снизу отступ поля.</summary>
        private static GameObject CreateFrame(Transform parent, float height)
        {
            var frame = CreateRounded("Frame", parent, Line);
            var sizing = frame.AddComponent<LayoutElement>();
            sizing.minHeight = height;
            sizing.preferredHeight = height;

            var layout = frame.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(1, 1, 1, 1);
            layout.childForceExpandHeight = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            return frame;
        }

        private static Dropdown CreateDropdown(Transform parent)
        {
            var frame = CreateFrame(parent, ControlHeight);
            var go = DefaultControls.CreateDropdown(Resources);
            go.transform.SetParent(frame.transform, false);
            StyleControl(go.GetComponent<Image>());

            var dropdown = go.GetComponent<Dropdown>();
            Style(dropdown.captionText, FontSize, Foreground);
            Style(dropdown.itemText, FontSize, Foreground);

            // Шаблон списка рассчитан на 14-й кегль: раздвигаем под наш.
            var item = (RectTransform)dropdown.itemText.transform.parent;
            item.sizeDelta = new Vector2(item.sizeDelta.x, 56f);
            dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 300f);
            dropdown.template.GetComponent<Image>().color = Background;

            var caption = (RectTransform)dropdown.captionText.transform;
            caption.offsetMin = new Vector2(16f, caption.offsetMin.y);

            var arrow = go.transform.Find("Arrow");
            if (arrow != null)
            {
                arrow.GetComponent<Image>().color = Muted;
            }

            return dropdown;
        }

        private static InputField CreateInput(Transform parent, bool multiline)
        {
            var frame = CreateFrame(parent, multiline ? 220f : ControlHeight);
            var go = DefaultControls.CreateInputField(Resources);
            go.transform.SetParent(frame.transform, false);
            StyleControl(go.GetComponent<Image>());

            var input = go.GetComponent<InputField>();
            Style(input.textComponent, FontSize, Foreground);
            Style((Text)input.placeholder, FontSize, Muted);
            ((Text)input.placeholder).text = string.Empty;

            if (multiline)
            {
                input.lineType = InputField.LineType.MultiLineNewline;
                input.textComponent.alignment = TextAnchor.UpperLeft;
            }

            foreach (var rect in new[] { (RectTransform)input.textComponent.transform, (RectTransform)input.placeholder.transform })
            {
                rect.offsetMin = new Vector2(16f, multiline ? 12f : rect.offsetMin.y);
                rect.offsetMax = new Vector2(-16f, multiline ? -12f : rect.offsetMax.y);
            }

            return input;
        }

        private static Button CreateButton(Transform parent, string label, bool primary)
        {
            var go = CreateRounded(label, parent, primary ? Accent : Line);
            var button = go.AddComponent<Button>();
            var sizing = go.AddComponent<LayoutElement>();
            sizing.minHeight = 64f;
            sizing.preferredHeight = 64f;
            sizing.preferredWidth = 220f;

            var colors = button.colors;
            colors.highlightedColor = primary ? Rgb(26, 95, 208) : HoverBackground;
            colors.pressedColor = primary ? Rgb(26, 95, 208) : Line;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.6f);
            button.colors = colors;

            if (!primary)
            {
                // Рамка цвета линии с белой серединой: фон «обычной» кнопки в вебе белый.
                var inner = CreateRounded("Inner", go.transform, Background);
                Inset((RectTransform)inner.transform, 1f);
                inner.GetComponent<Image>().raycastTarget = false;
            }

            var text = CreateLabel(go.transform, label, FontSize, primary ? Color.white : Foreground, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            Stretch((RectTransform)text.transform);

            return button;
        }

        /// <summary>Кнопка без подложки: крестик в шапке и на плитке.</summary>
        private static Button CreateFlatButton(Transform parent, string label, int size, Color color, float side)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Color.clear;

            var sizing = go.GetComponent<LayoutElement>();
            sizing.preferredWidth = side;
            sizing.preferredHeight = side;
            sizing.flexibleWidth = 0f;

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.clear;
            colors.highlightedColor = new Color(0f, 0f, 0f, 0.06f);
            colors.pressedColor = new Color(0f, 0f, 0f, 0.1f);
            button.colors = colors;

            var text = CreateLabel(go.transform, label, size, color, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            Stretch((RectTransform)text.transform);

            return button;
        }

        // --- Утилиты --------------------------------------------------------

        private static void StyleControl(Image image)
        {
            image.sprite = Rounded();
            image.type = Image.Type.Sliced;
            image.color = Background;
        }

        /// <summary>Отступ под полем — как margin-bottom у .sg-field в вебе.</summary>
        private static void AddSpacing(GameObject after, float bottom)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(after.transform.parent, false);
            spacer.transform.SetSiblingIndex(after.transform.GetSiblingIndex() + 1);
            spacer.GetComponent<LayoutElement>().preferredHeight = bottom;
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

        private static void Inset(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Font DefaultFont()
        {
            // Имя встроенного шрифта поменялось в Unity 2022.
            var font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return font != null ? font : UnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        /// <summary>
        /// 9-slice спрайт со скруглёнными углами: белый квадрат 64×64 с радиусом
        /// 12 px, углы прозрачные. Цвет задаёт Image.
        /// </summary>
        private static Sprite Rounded()
        {
            if (_rounded != null)
            {
                return _rounded;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SupportGateRounded" };
            var pixels = new Color32[size * size];
            var radius = Radius;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(0f, Mathf.Max(radius - 0.5f - x, x + 0.5f - (size - radius)));
                    var dy = Mathf.Max(0f, Mathf.Max(radius - 0.5f - y, y + 0.5f - (size - radius)));
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            texture.hideFlags = HideFlags.DontSave;

            var border = radius + 2f;
            _rounded = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 1f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            _rounded.name = "SupportGateRounded";
            _rounded.hideFlags = HideFlags.DontSave;

            return _rounded;
        }

        private static Color Rgb(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
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

    /// <summary>
    /// Лэйаут-группа, которая раскладывает колонки в ряд, а на узкой ширине —
    /// в столбик: замена @media (max-width) из веба. Horizontal- и
    /// VerticalLayoutGroup устроены так же, только ось у них зашита.
    /// </summary>
    public sealed class SupportGateDemoColumns : HorizontalOrVerticalLayoutGroup
    {
        public float NarrowWidth = 1000f;

        private bool Narrow
        {
            get
            {
                var width = rectTransform.rect.width;
                return width > 0f && width < NarrowWidth;
            }
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            CalcAlongAxis(0, Narrow);
        }

        public override void CalculateLayoutInputVertical()
        {
            CalcAlongAxis(1, Narrow);
        }

        public override void SetLayoutHorizontal()
        {
            SetChildrenAlongAxis(0, Narrow);
        }

        public override void SetLayoutVertical()
        {
            SetChildrenAlongAxis(1, Narrow);
        }
    }

    /// <summary>
    /// GridLayoutGroup с гибким числом колонок не сообщает высоту по числу рядов
    /// внутри вертикального лэйаута — считаем сами по ширине и числу плиток.
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup), typeof(LayoutElement))]
    public sealed class SupportGateDemoGridHeight : MonoBehaviour
    {
        private void LateUpdate()
        {
            var grid = GetComponent<GridLayoutGroup>();
            var width = ((RectTransform)transform).rect.width;
            if (width <= 0f)
            {
                return;
            }

            var active = 0;
            for (var i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).gameObject.activeSelf) active++;
            }

            var perRow = Mathf.Max(1, Mathf.FloorToInt((width + grid.spacing.x) / (grid.cellSize.x + grid.spacing.x)));
            var rows = Mathf.CeilToInt(active / (float)perRow);
            var height = rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y + 16f;

            var element = GetComponent<LayoutElement>();
            if (!Mathf.Approximately(element.preferredHeight, height))
            {
                element.preferredHeight = height;
            }
        }
    }
}
