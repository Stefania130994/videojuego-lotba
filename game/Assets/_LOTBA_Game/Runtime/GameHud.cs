using System;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Scene UI bindings. Configuration exposes only implemented, available capabilities.</summary>
    public sealed partial class GameHud : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text gameTitle, developmentLabel, identityFooter, buildVersion;
        [SerializeField] private GameObject readyPanel, pausePanel, resultPanel, raceHud, controls, settingsPanel;
        [SerializeField] private UnityEngine.UI.Button start, retry, menu, pause, resume, brake, pauseMenu, exit;
        [SerializeField] private UnityEngine.UI.Button[] configurationButtons;
        [SerializeField] private Sprite menuLogo, wideMenuButton, shortMenuButton, trophyIcon, settingsIcon;
        [SerializeField] private UnityEngine.UI.Button closeSettings, soundButton, vibrationButton;
        [SerializeField] private TMPro.TMP_Text soundLabel, vibrationLabel;
        [SerializeField] private UnityEngine.UI.Button qualityButton;
        [SerializeField] private TMPro.TMP_Text qualityLabel;
        private UnityEngine.UI.Button previousQuality;
        private UnityEngine.UI.Image soundSwitch;
        private RectTransform soundSwitchKnob;
        private TMPro.TMP_Text vibrationTestResult;
        [SerializeField] private TMPro.TMP_Text score, gems, resultTitle, resultStats;
        [SerializeField] private Sprite resultFlagIcon, resultWarningIcon, resultRecoverIcon, resultRetryIcon, resultHomeIcon;
        private TMPro.TMP_Text resultPoints;
        private readonly TMPro.TMP_Text[] resultRows = new TMPro.TMP_Text[5];
        private UnityEngine.UI.Image endingBackground;
        private CanvasGroup endingMessage, endingResults;
        private TMPro.TMP_Text endingHeading, endingReflection;
        private int endingHeadingCharacters, endingReflectionCharacters;
        private float endingStarted;
        private static readonly Color ResultsBackdrop = new Color32(12, 33, 50, 255);

        [SerializeField] private SpeedGauge gauge;
        [SerializeField] private RectTransform wordBanner;
        [SerializeField] private TMPro.TMP_Text wordLabel;
        private Vector2 wordBannerVisiblePosition;
        private float wordBannerStarted, wordBannerUntil;
        private float gemPulseUntil;
        private int previousGemBalance;
        private RectTransform gemPanel;
        private RectTransform speedOffer;
        [SerializeField] private Sprite keepPaceIcon, increasePaceIcon, offerGemIcon;
        [SerializeField] private Sprite unavailablePaceIcon, unavailableGemIcon;
        private UnityEngine.UI.Image paceCardIcon, paceCardGemIcon;
        private UnityEngine.UI.Graphic[] paceCardGraphics;
        private Color[] paceCardColors;
        private UnityEngine.UI.Button increaseSpeedButton;
        private TMPro.TMP_Text increaseSpeedDetail, increaseSpeedPrice;
        private RectTransform increaseSpeedPriceBadge;
        private BoostSpeedLines boostSpeedLines;
        private RectTransform milestoneBar;
        private RectTransform milestoneFill;
        private RectTransform milestoneRider;
        private UnityEngine.UI.Button turboButton;
        private UnityEngine.UI.Image turboFill;
        [SerializeField] private Sprite turboBoltSprite;
        private UnityEngine.UI.Image turboBolt;
        private static readonly Color TurboGold = new Color32(203, 159, 14, 255);
        private Material controlLabelMaterial;
        public bool SettingsOpen { get; private set; }
        private RunTelemetryPanel telemetryPanel;
        public bool TelemetryOpen => telemetryPanel != null && telemetryPanel.IsOpen;
        public void CloseTelemetry() => telemetryPanel?.Close();
        public event Action StartRequested, MenuRequested, PauseRequested, BrakeRequested, SoundRequested, SettingsRequested, VibrationRequested;
        public event Action<int> QualityRequested;
        public event Action VibrationTestRequested;
        public event Action TurboRequested;
        public event Action UiClicked;
        public event Action<bool> SpeedChosen;
        private void Awake()
        {
            ApplyIdentity();
            gemPanel = (RectTransform)gems.transform.parent;
            gems.enableAutoSizing = false;
            gems.overflowMode = TMPro.TextOverflowModes.Overflow;
            ResizeGemCounter();
            wordBannerVisiblePosition = wordBanner.anchoredPosition;
            wordBanner.gameObject.SetActive(false);
            var linesObject = new GameObject("Boost speed lines", typeof(RectTransform), typeof(CanvasRenderer), typeof(BoostSpeedLines));
            var linesRect = (RectTransform)linesObject.transform;
            linesRect.SetParent(raceHud.transform, false);
            linesRect.SetAsFirstSibling();
            linesRect.anchorMin = Vector2.zero;
            linesRect.anchorMax = Vector2.one;
            linesRect.offsetMin = linesRect.offsetMax = Vector2.zero;
            boostSpeedLines = linesObject.GetComponent<BoostSpeedLines>();
            boostSpeedLines.raycastTarget = false;
            linesObject.SetActive(false);
            CreateSpeedOffer();
            StyleMainMenu();
            StyleSettings();
            CreateEndingScreen();
            CreateRanking();
            telemetryPanel = new RunTelemetryPanel(resultPanel, resultStats, menu);
            exit.gameObject.SetActive(Application.platform != RuntimePlatform.WebGLPlayer);
            exit.onClick.AddListener(ExitGame);
            start.onClick.AddListener(() => StartRequested?.Invoke()); retry.onClick.AddListener(() => StartRequested?.Invoke());
            pauseMenu.onClick.AddListener(() => MenuRequested?.Invoke());
            menu.onClick.AddListener(() => MenuRequested?.Invoke());
            pause.onClick.AddListener(() => PauseRequested?.Invoke()); resume.onClick.AddListener(() => PauseRequested?.Invoke());
            brake.onClick.AddListener(() => BrakeRequested?.Invoke());
            foreach (var button in configurationButtons) button.onClick.AddListener(() => SettingsRequested?.Invoke());
            closeSettings.onClick.AddListener(() => SettingsOpen = false);
            soundButton.onClick.AddListener(() => SoundRequested?.Invoke());
            vibrationButton.onClick.AddListener(() => VibrationRequested?.Invoke());
            qualityButton.onClick.AddListener(() => QualityRequested?.Invoke(1));
            foreach (var button in GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (button != brake && button != turboButton)
                    button.onClick.AddListener(() => UiClicked?.Invoke());
        }

        private void StyleMainMenu()
        {
            foreach (Transform child in readyPanel.transform)
                if (child.GetComponent<UnityEngine.UI.Button>() == null) child.gameObject.SetActive(false);

            var logo = Background(Region("Logo Frená a Tiempo", readyPanel.transform,
                new Vector2(.07f, .69f), new Vector2(.93f, .96f)), Color.white);
            logo.sprite = menuLogo;
            logo.preserveAspect = true;

            StyleMenuButton(start, wideMenuButton, new Color32(33, 200, 88, 255),
                new Vector2(.16f, .253f), new Vector2(.84f, .353f), "JUGAR", 40f).fontSharedMaterial = controlLabelMaterial;

            UnityEngine.UI.Button options = Array.Find(configurationButtons,
                button => button.transform.IsChildOf(readyPanel.transform));
            var slate = new Color32(45, 55, 71, 255);
            StyleMenuButton(options, shortMenuButton, slate,
                new Vector2(.515f, .033f), new Vector2(.735f, .129f), "OPCIONES", 17f, settingsIcon);
            var ranking = Region("Récords", readyPanel.transform, Vector2.zero, Vector2.one);
            var rankingImage = ranking.gameObject.AddComponent<UnityEngine.UI.Image>();
            var rankingButton = ranking.gameObject.AddComponent<UnityEngine.UI.Button>();
            rankingButton.targetGraphic = rankingImage;
            StyleMenuButton(rankingButton, shortMenuButton, slate,
                new Vector2(.265f, .033f), new Vector2(.485f, .129f), "RÉCORDS", 17f, trophyIcon);
            rankingButton.onClick.AddListener(() => OpenRanking(false));
            StyleMenuButton(exit, wideMenuButton, new Color32(197, 40, 49, 255),
                new Vector2(.16f, .141f), new Vector2(.84f, .241f), "SALIR", 40f).fontSharedMaterial = controlLabelMaterial;
        }

        private TMPro.TMP_Text StyleMenuButton(UnityEngine.UI.Button button, Sprite sprite, Color color,
            Vector2 min, Vector2 max, string text, float fontSize, Sprite icon = null)
        {
            PlaceRect((RectTransform)button.transform, min, max);
            var image = (UnityEngine.UI.Image)button.targetGraphic;
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = false;
            image.color = color;
            var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label == null)
                label = MenuCaption("Etiqueta", button.transform, Vector2.zero, Vector2.one, text, fontSize);
            PlaceRect(label.rectTransform, new Vector2(.05f, icon == null ? .08f : .13f),
                new Vector2(.95f, icon == null ? .92f : .38f));
            label.text = text;
            label.color = Color.white;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.fontStyle = TMPro.FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = fontSize;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            if (icon != null)
            {
                var artwork = Background(Region("Ícono", button.transform,
                    new Vector2(.24f, .39f), new Vector2(.76f, .87f)), Color.white);
                artwork.sprite = icon;
                artwork.preserveAspect = true;
            }
            return label;
        }

        private void StyleSettings()
        {
            var root = (RectTransform)settingsPanel.transform;
            foreach (Transform child in root)
                if (child.GetComponent<UnityEngine.UI.Button>() == null) child.gameObject.SetActive(false);
            PlaceRect(root, Vector2.zero, Vector2.one);
            var scrim = settingsPanel.GetComponent<UnityEngine.UI.Image>();
            scrim.sprite = null;
            scrim.type = UnityEngine.UI.Image.Type.Simple;
            scrim.color = new Color32(5, 17, 28, 155);
            var frame = Region("Marco", root, new Vector2(.055f, .145f), new Vector2(.945f, .855f));
            RoundedBackground(frame, new Color32(98, 153, 188, 255));
            var panel = Region("Panel", frame, Vector2.zero, Vector2.one);
            panel.offsetMin = Vector2.one * 2f;
            panel.offsetMax = Vector2.one * -2f;
            RoundedBackground(panel, new Color32(12, 33, 50, 248));
            MenuCaption("Título", panel, new Vector2(.06f, .86f), new Vector2(.94f, .98f), "CONFIGURACIÓN", 37f);

            RectTransform Row(string title, float bottom)
            {
                var row = Region(title, panel, new Vector2(.065f, bottom), new Vector2(.935f, bottom + .132f));
                RoundedBackground(row, new Color32(24, 55, 78, 255));
                var label = MenuCaption("Etiqueta", row, new Vector2(.07f, .08f), new Vector2(.56f, .92f), title, 24f);
                label.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
                return row;
            }

            void Attach(UnityEngine.UI.Button button, RectTransform parent)
            {
                button.transform.SetParent(parent, false);
                PlaceRect((RectTransform)button.transform, Vector2.zero, Vector2.one);
                ((UnityEngine.UI.Image)button.targetGraphic).color = Color.clear;
            }

            var sound = Row("SONIDO", .71f);
            Attach(soundButton, sound);
            soundLabel.gameObject.SetActive(false);
            var toggle = Region("Interruptor", soundButton.transform, new Vector2(.72f, .26f), new Vector2(.96f, .74f));
            RoundedBackground(toggle, Color.white);
            soundSwitch = toggle.GetComponent<UnityEngine.UI.Image>();
            soundSwitchKnob = Region("Indicador", toggle, new Vector2(.50f, .10f), new Vector2(.94f, .90f));
            RoundedBackground(soundSwitchKnob, new Color32(242, 249, 255, 255));

            var vibration = Row("VIBRACIÓN", .555f);
            Attach(vibrationButton, vibration);
            PlaceRect(vibrationLabel.rectTransform, new Vector2(.65f, .14f), new Vector2(.96f, .86f));
            vibrationLabel.enableAutoSizing = true;
            vibrationLabel.fontSizeMin = 10f;
            vibrationLabel.fontSizeMax = 17f;
            vibrationLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

            var quality = Row("CALIDAD", .40f);
            var badge = Region("Nivel de calidad", quality, new Vector2(.67f, .22f), new Vector2(.89f, .78f));
            RoundedBackground(badge, new Color32(46, 97, 128, 255));
            qualityLabel.transform.SetParent(badge, false);
            PlaceRect(qualityLabel.rectTransform, Vector2.zero, Vector2.one);
            qualityLabel.enableAutoSizing = true;
            qualityLabel.fontSizeMin = 10f;
            qualityLabel.fontSizeMax = 20f;
            qualityLabel.color = new Color32(255, 221, 124, 255);
            Attach(qualityButton, quality);
            PlaceRect((RectTransform)qualityButton.transform, new Vector2(.89f, 0f), Vector2.one);
            MenuCaption("Siguiente", qualityButton.transform, Vector2.zero, Vector2.one, "›", 32f);
            var previous = Region("Anterior", quality, new Vector2(.57f, 0f), new Vector2(.67f, 1f));
            previousQuality = previous.gameObject.AddComponent<UnityEngine.UI.Button>();
            var previousHitbox = Background(previous, Color.clear);
            previousHitbox.raycastTarget = true;
            previousQuality.targetGraphic = previousHitbox;
            previousQuality.interactable = qualityButton.interactable;
            previousQuality.onClick.AddListener(() => QualityRequested?.Invoke(-1));
            MenuCaption("Anterior", previous, Vector2.zero, Vector2.one, "‹", 32f);

            var notice = Label("Aviso", panel, new Vector2(.10f, .16f), new Vector2(.90f, .29f),
                "Los cambios de sonido y vibración se aplican a esta partida.", 19f);
            var debugToggle = DebugButton(panel, .31f, .39f, "MOSTRAR PRUEBA · SOLO PARA DEBUG");
            var debugLabel = debugToggle.GetComponentInChildren<TMPro.TMP_Text>();
            var debugSection = Region("Prueba de vibración", panel, new Vector2(.065f, .16f), new Vector2(.935f, .305f));
            var testButton = DebugButton(debugSection, .48f, 1f, "PROBAR VIBRACIÓN");
            vibrationTestResult = Label("Resultado de la prueba", debugSection, Vector2.zero, new Vector2(1f, .46f),
                "Tocá el botón para solicitar un pulso.", 17f);
            vibrationTestResult.richText = false;
            debugSection.gameObject.SetActive(false);
            debugToggle.onClick.AddListener(() =>
            {
                bool show = !debugSection.gameObject.activeSelf;
                debugSection.gameObject.SetActive(show);
                notice.gameObject.SetActive(!show);
                debugLabel.text = show ? "OCULTAR PRUEBA · SOLO PARA DEBUG" : "MOSTRAR PRUEBA · SOLO PARA DEBUG";
            });
            testButton.onClick.AddListener(() => VibrationTestRequested?.Invoke());
            closeSettings.transform.SetParent(panel, false);
            StyleMenuButton(closeSettings, wideMenuButton, new Color32(67, 111, 148, 255),
                new Vector2(.065f, .045f), new Vector2(.935f, .155f), "VOLVER", 27f);

            UnityEngine.UI.Button DebugButton(Transform parent, float bottom, float top, string caption)
            {
                var rect = Region(caption, parent, new Vector2(.065f, bottom), new Vector2(.935f, top));
                RoundedBackground(rect, new Color32(46, 97, 128, 255));
                var image = rect.GetComponent<UnityEngine.UI.Image>();
                image.raycastTarget = true;
                var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image;
                MenuCaption("Etiqueta", rect, new Vector2(.04f, 0f), new Vector2(.96f, 1f), caption, 16f);
                return button;
            }
        }

        public void SetVibrationTestResult(string result) => vibrationTestResult.text = result;

        private void CreateEndingScreen()
        {
            var screen = Region("Cierre de partida", resultPanel.transform.parent, Vector2.zero, Vector2.one);
            endingBackground = Background(screen, Color.clear);
            endingBackground.raycastTarget = true;

            var message = Region("Mensaje LOTBA", screen, new Vector2(.09f, .30f), new Vector2(.91f, .70f));
            endingMessage = message.gameObject.AddComponent<CanvasGroup>();
            endingMessage.blocksRaycasts = false;
            endingHeading = MenuCaption("Lema", message, new Vector2(0f, .52f), Vector2.one, string.Empty, 39f);
            endingHeading.color = new Color32(169, 219, 246, 255);
            endingReflection = Label("Reflexión", message, new Vector2(.03f, .1f), new Vector2(.97f, .54f),
                string.Empty, 24f);
            endingHeading.useMaxVisibleDescender = false;
            endingReflection.useMaxVisibleDescender = false;

            resultPanel.transform.SetParent(screen, false);
            endingResults = resultPanel.AddComponent<CanvasGroup>();
            screen.gameObject.SetActive(false);
        }

        private void RenderEnding(RunEnding ending, bool opening, GameSettings settings)
        {
            if (opening)
            {
                endingStarted = Time.unscaledTime;
                endingBackground.gameObject.SetActive(true);
                if (ending != RunEnding.RouteComplete)
                {
                    endingHeading.text = settings.EndingHeading;
                    endingReflection.text = settings.EndingReflection;
                    endingHeading.ForceMeshUpdate();
                    endingReflection.ForceMeshUpdate();
                    endingHeadingCharacters = endingHeading.textInfo.characterCount;
                    endingReflectionCharacters = endingReflection.textInfo.characterCount;
                }
            }
            if (ending == RunEnding.RouteComplete)
            {
                endingBackground.color = Color.clear;
                endingMessage.alpha = 0f;
                endingResults.alpha = 1f;
                endingResults.interactable = endingResults.blocksRaycasts = true;
                resultPanel.SetActive(!TelemetryOpen && !RankingOpen);
                return;
            }
            float age = Time.unscaledTime - endingStarted;
            float fadeStart = ending == RunEnding.Brake ? RunnerWorld.BrakeSlideSeconds : 0f;
            float fadeDuration = ending == RunEnding.Brake ? settings.EndingBrakeFadeSeconds : 0f;
            float blackoutAt = fadeStart + fadeDuration;
            float blackout = fadeDuration > 0f
                ? Mathf.SmoothStep(0f, 1f, (age - fadeStart) / fadeDuration)
                : age >= blackoutAt ? 1f : 0f;
            float messageAge = age - blackoutAt;
            float charactersPerSecond = settings.EndingCharactersPerSecond;
            float readingHold = settings.EndingReadingHoldSeconds;
            int typed = Mathf.FloorToInt(Mathf.Max(0f, messageAge) * charactersPerSecond);
            endingHeading.maxVisibleCharacters = Mathf.Min(endingHeadingCharacters, typed);
            endingReflection.maxVisibleCharacters = Mathf.Clamp(typed - endingHeadingCharacters, 0, endingReflectionCharacters);
            float resultsAge = messageAge - (endingHeadingCharacters + endingReflectionCharacters) / charactersPerSecond - readingHold;
            float resultsFade = settings.EndingResultsFadeSeconds;
            float reveal = resultsFade > 0f
                ? Mathf.SmoothStep(0f, 1f, resultsAge / resultsFade)
                : resultsAge >= 0f ? 1f : 0f;
            endingMessage.alpha = 1f - reveal;
            Color background = Color.Lerp(Color.black, ResultsBackdrop, reveal);
            background.a = blackout;
            endingBackground.color = background;
            endingResults.alpha = reveal;
            endingResults.interactable = endingResults.blocksRaycasts = reveal >= 1f;
            resultPanel.SetActive(resultsAge >= 0f && !TelemetryOpen && !RankingOpen);
        }

        private TMPro.TextMeshProUGUI MenuCaption(string name, Transform parent, Vector2 min, Vector2 max,
            string text, float size)
        {
            var label = Label(name, parent, min, max, text, size);
            label.fontStyle = TMPro.FontStyles.Bold;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            return label;
        }

        private static void PlaceRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static RectTransform Region(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var region = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            region.SetParent(parent, false);
            PlaceRect(region, min, max);
            return region;
        }

        private TMPro.TextMeshProUGUI Label(string name, Transform parent, Vector2 min, Vector2 max, string text, float size)
        {
            var label = Region(name, parent, min, max).gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            label.font = score.font;
            label.text = text;
            label.fontSize = size;
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = size;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static UnityEngine.UI.Image Background(RectTransform region, Color color)
        {
            var image = region.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void RoundedBackground(RectTransform region, Color color)
        {
            var image = Background(region, color);
            image.sprite = gemPanel.GetComponent<UnityEngine.UI.Image>().sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 3f;
        }

        private void CreateSpeedOffer()
        {
            void Card(string name, Vector2 min, Vector2 max, Color color, Color accent, bool accelerate)
            {
                RectTransform card = Region(name, speedOffer, min, max);
                RoundedBackground(card, accent);
                var image = card.GetComponent<UnityEngine.UI.Image>();
                image.raycastTarget = true;
                var button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image;
                var colors = button.colors;
                colors.disabledColor = new Color(.63f, .63f, .63f, 1f);
                button.colors = colors;
                button.onClick.AddListener(() => SpeedChosen?.Invoke(accelerate));
                var face = Region("Fondo", card, Vector2.zero, Vector2.one);
                face.offsetMin = Vector2.one * 2f;
                face.offsetMax = Vector2.one * -2f;
                RoundedBackground(face, color);
                var icon = Background(Region("Ícono", face, new Vector2(.25f, .74f), new Vector2(.75f, .96f)), Color.white);
                icon.sprite = accelerate ? increasePaceIcon : keepPaceIcon;
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
                var title = Label("Título", face, new Vector2(.04f, .46f), new Vector2(.96f, .73f), name, 24f);
                title.fontStyle = TMPro.FontStyles.Bold;
                var footer = Region("Pie", face, new Vector2(0f, 0f), new Vector2(1f, .16f));
                RoundedBackground(footer, Color.Lerp(color, new Color32(9, 16, 23, 255), .5f));
                var choose = Label("Elegir", footer, Vector2.zero, Vector2.one, "ELEGIR", 16f);
                choose.color = accent;
                choose.fontStyle = TMPro.FontStyles.Bold;
                var badge = Region("Costo", face, new Vector2(.5f, .31f), new Vector2(.5f, .31f));
                badge.sizeDelta = new Vector2(76f, 33f);
                RoundedBackground(badge, Color.Lerp(color, Color.white, .16f));
                if (accelerate)
                {
                    increaseSpeedButton = button;
                    increaseSpeedPriceBadge = badge;
                    var gem = Region("Gema", badge, new Vector2(0f, .5f), new Vector2(0f, .5f));
                    gem.sizeDelta = new Vector2(19f, 19f);
                    gem.anchoredPosition = new Vector2(18f, 0f);
                    var gemImage = Background(gem, Color.white);
                    paceCardIcon = icon;
                    paceCardGemIcon = gemImage;
                    gemImage.sprite = offerGemIcon;
                    gemImage.preserveAspect = true;
                    increaseSpeedPrice = Label("Precio", badge, Vector2.zero, Vector2.one, "", 24f);
                    increaseSpeedPrice.rectTransform.offsetMin = new Vector2(30f, 0f);
                    increaseSpeedPrice.rectTransform.offsetMax = new Vector2(-8f, 0f);
                    increaseSpeedPrice.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                    increaseSpeedPrice.fontStyle = TMPro.FontStyles.Bold;
                    increaseSpeedDetail = Label("Detalle", face, new Vector2(.04f, .16f), new Vector2(.96f, .245f), "", 13f);
                    paceCardGraphics = card.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                    paceCardColors = Array.ConvertAll(paceCardGraphics, graphic => graphic.color);
                    button.transition = UnityEngine.UI.Selectable.Transition.None;
                }
                else Label("Gratis", badge, Vector2.zero, Vector2.one, "GRATIS", 16f).fontStyle = TMPro.FontStyles.Bold;
            }

            var brakeRect = (RectTransform)brake.transform;
            var turboRect = Region("Acelerar manualmente", brakeRect.parent, Vector2.zero, Vector2.zero);
            void PlaceControl(RectTransform button, float side)
            {
                button.anchorMin = button.anchorMax = new Vector2(side, 0f);
                button.pivot = Vector2.one * .5f;
                button.anchoredPosition = new Vector2(side == 0f ? 78f : -78f, 82f);
                button.sizeDelta = new Vector2(120f, 120f);
            }
            PlaceControl(brakeRect, 0f);
            PlaceControl(turboRect, 1f);
            var brakeImage = (UnityEngine.UI.Image)brake.targetGraphic;
            brakeImage.type = UnityEngine.UI.Image.Type.Simple;
            brakeImage.color = Color.black;
            UnityEngine.UI.Image ButtonFace(string name, RectTransform parent, Color color)
            {
                var face = Background(Region(name, parent, Vector2.zero, Vector2.one), color);
                face.rectTransform.offsetMin = new Vector2(3f, 3f);
                face.rectTransform.offsetMax = new Vector2(-3f, -3f);
                face.sprite = brakeImage.sprite;
                return face;
            }
            var brakeFace = ButtonFace("Fondo rojo", brakeRect, new Color32(181, 34, 43, 255));
            brakeFace.transform.SetAsFirstSibling();
            brake.targetGraphic = brakeFace;
            var brakeLabel = brake.GetComponentInChildren<TMPro.TMP_Text>(true);
            controlLabelMaterial = new Material(score.font.material);
            controlLabelMaterial.SetColor(TMPro.ShaderUtilities.ID_OutlineColor, Color.black);
            controlLabelMaterial.SetFloat(TMPro.ShaderUtilities.ID_OutlineWidth, .26f);
            controlLabelMaterial.EnableKeyword("OUTLINE_ON");
            if (brakeLabel != null)
            {
                RectTransform labelRect = brakeLabel.rectTransform;
                labelRect.anchorMin = new Vector2(.08f, .08f);
                labelRect.anchorMax = new Vector2(.92f, .92f);
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                StyleControlLabel(brakeLabel);
            }
            var turboBackground = Background(turboRect, Color.black);
            turboBackground.sprite = brakeImage.sprite;
            turboBackground.raycastTarget = true;
            ButtonFace("Fondo dorado", turboRect, new Color32(91, 61, 23, 255));
            turboButton = turboRect.gameObject.AddComponent<UnityEngine.UI.Button>();
            turboButton.targetGraphic = turboBackground;
            turboButton.transition = UnityEngine.UI.Selectable.Transition.None;
            turboButton.onClick.AddListener(() => TurboRequested?.Invoke());
            turboFill = ButtonFace("Carga", turboRect, TurboGold);
            turboFill.type = UnityEngine.UI.Image.Type.Filled;
            turboFill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
            turboFill.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
            var turboLabel = Label("Etiqueta", turboRect, new Vector2(.08f, .08f), new Vector2(.92f, .92f), "ACELERAR", 18f);
            StyleControlLabel(turboLabel);
            var bolt = Region("Rayo de turbo listo", turboRect, new Vector2(.5f, .87f), new Vector2(.5f, .87f));
            bolt.sizeDelta = new Vector2(42f, 54f);
            turboBolt = Background(bolt, Color.white);
            turboBolt.sprite = turboBoltSprite;
            turboBolt.preserveAspect = true;
            turboBolt.enabled = false;

            void StyleControlLabel(TMPro.TMP_Text label)
            {
                label.font = score.font;
                label.enableAutoSizing = false;
                label.fontSize = 18f;
                label.fontStyle = TMPro.FontStyles.Bold;
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                label.overflowMode = TMPro.TextOverflowModes.Overflow;
                label.margin = Vector4.zero;
                label.color = Color.white;
                label.fontSharedMaterial = controlLabelMaterial;
            }

            milestoneBar = Region("Progreso hasta la decisión", raceHud.transform, new Vector2(.046f, .44f), new Vector2(.070f, .72f));
            var track = Region("Carril de progreso", milestoneBar, Vector2.zero, new Vector2(1f, .82f));
            RoundedBackground(track, new Color32(17, 29, 43, 235));
            milestoneFill = Region("Progreso", track, new Vector2(.13f, .015f), new Vector2(.87f, .985f));
            RoundedBackground(milestoneFill, new Color32(107, 221, 195, 255));

            var flag = Region("Bandera de llegada", milestoneBar, new Vector2(.5f, .98f), new Vector2(.5f, .98f));
            flag.sizeDelta = new Vector2(24f, 24f);
            Background(Region("Mástil", flag, new Vector2(.12f, .05f), new Vector2(.21f, .94f)), Color.white);
            for (int column = 0; column < 3; column++)
                for (int row = 0; row < 2; row++)
                {
                    var min = new Vector2(.21f + column * .24f, .44f + row * .25f);
                    Background(Region("Cuadro", flag, min, min + new Vector2(.24f, .25f)),
                        (column + row) % 2 == 0 ? Color.white : new Color32(17, 29, 43, 255));
                }

            milestoneRider = Region("Ciclista", track, new Vector2(.5f, 0f), new Vector2(.5f, 0f));
            milestoneRider.sizeDelta = new Vector2(21f, 21f);
            RoundedBackground(milestoneRider, new Color32(17, 29, 43, 255));
            RoundedBackground(Region("Cara", milestoneRider, new Vector2(.23f, .15f), new Vector2(.77f, .72f)),
                new Color32(239, 183, 135, 255));
            RoundedBackground(Region("Casco", milestoneRider, new Vector2(.13f, .55f), new Vector2(.87f, .89f)),
                new Color32(98, 207, 223, 255));
            for (int i = 0; i < 2; i++)
            {
                float x = .33f + .23f * i;
                Background(Region("Ojo", milestoneRider, new Vector2(x, .39f), new Vector2(x + .09f, .48f)),
                    new Color32(17, 29, 43, 255));
            }


            speedOffer = Region("Tarjetas de velocidad", raceHud.transform, new Vector2(.025f, .33f), new Vector2(.975f, .68f));
            RoundedBackground(speedOffer, new Color32(10, 25, 36, 233));
            Label("Encabezado", speedOffer, new Vector2(.04f, .81f), new Vector2(.96f, .99f), "Elegí tu ritmo", 27f);
            Card("Mantener ritmo", new Vector2(.04f, .055f), new Vector2(.48f, .80f),
                new Color32(26, 53, 74, 255), new Color32(138, 183, 216, 255), false);
            Card("Aumentar\nvelocidad", new Vector2(.52f, .055f), new Vector2(.96f, .80f),
                new Color32(108, 48, 22, 255), new Color32(255, 178, 115, 255), true);
            milestoneBar.gameObject.SetActive(false);
            speedOffer.gameObject.SetActive(false);

            var results = (RectTransform)resultPanel.transform;
            results.anchorMin = new Vector2(.055f, .09f);
            results.anchorMax = new Vector2(.945f, .91f);
            results.offsetMin = results.offsetMax = Vector2.zero;
            resultPanel.GetComponent<UnityEngine.UI.Image>().color = new Color32(10, 29, 43, 242);
            var resultBorder = Region("Marco de resultados", results, Vector2.zero, Vector2.one);
            resultBorder.offsetMin = Vector2.one * -2f;
            resultBorder.offsetMax = Vector2.one * 2f;
            resultBorder.SetAsFirstSibling();
            RoundedBackground(resultBorder, new Color32(89, 143, 174, 255));
            var resultFace = Region("Interior", resultBorder, Vector2.zero, Vector2.one);
            resultFace.offsetMin = Vector2.one * 2f;
            resultFace.offsetMax = Vector2.one * -2f;
            RoundedBackground(resultFace, new Color32(10, 29, 43, 255));

            void Position(RectTransform rect, Vector2 min, Vector2 max)
            {
                rect.anchorMin = min;
                rect.anchorMax = max;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }

            Position(resultTitle.rectTransform, new Vector2(.06f, .83f), new Vector2(.94f, .98f));
            resultStats.gameObject.SetActive(false);
            resultPoints = Label("Puntos", results, new Vector2(.06f, .725f), new Vector2(.94f, .83f), "", 41f);
            resultPoints.color = new Color32(255, 209, 76, 255);
            resultPoints.fontStyle = TMPro.FontStyles.Bold;
            AddResultRow(0, resultFlagIcon, new Color32(132, 204, 244, 255));
            AddResultRow(1, offerGemIcon, Color.white);
            AddResultRow(2, resultWarningIcon, new Color32(255, 147, 120, 255));
            AddResultRow(3, resultRecoverIcon, new Color32(111, 223, 176, 255));
            AddResultRow(4, turboBoltSprite, Color.white);
            StyleResultButton(retry, .16f, .24f, resultRetryIcon, new Color32(36, 142, 88, 255));
            StyleResultButton(menu, .075f, .15f, resultHomeIcon, new Color32(29, 67, 94, 255));

            void AddResultRow(int index, Sprite sprite, Color tint)
            {
                float top = .72f - index * .06f;
                var icon = Background(Region("Ícono de resultado", results,
                    new Vector2(.075f, top - .052f), new Vector2(.15f, top)), tint);
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.enabled = sprite != null;
                var label = Label("Dato", results, new Vector2(.18f, top - .052f), new Vector2(.94f, top), "", 21f);
                label.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
                resultRows[index] = label;
            }

            void StyleResultButton(UnityEngine.UI.Button button, float bottom, float top, Sprite sprite, Color tint)
            {
                var rect = (RectTransform)button.transform;
                Position(rect, new Vector2(.07f, bottom), new Vector2(.93f, top));
                if (button.targetGraphic is UnityEngine.UI.Image image) image.color = tint;
                var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
                Position(label.rectTransform, new Vector2(.17f, 0f), new Vector2(.94f, 1f));
                var icon = Background(Region("Ícono de resultado", rect,
                    new Vector2(.045f, .18f), new Vector2(.145f, .82f)), Color.white);
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.enabled = sprite != null;
            }
        }
        private static void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public void ApplyIdentity()
        {
            if (gameTitle != null)
            {
                string title = GameIdentity.Title.ToUpperInvariant();
                int split = title.IndexOf(' ');
                gameTitle.text = split > 0
                    ? title.Substring(0, split) + "\n" + title.Substring(split + 1)
                    : title;
                gameTitle.enableAutoSizing = true;
                gameTitle.fontSizeMin = 32;
                gameTitle.fontSizeMax = 82;
            }
            if (developmentLabel != null)
            {
                developmentLabel.text = GameIdentity.DevelopmentLabel;
                developmentLabel.enableAutoSizing = true;
                developmentLabel.fontSizeMin = 12;
                developmentLabel.fontSizeMax = 17;
            }
            if (identityFooter != null) identityFooter.text = GameIdentity.FooterLabel;
            if (buildVersion != null) buildVersion.text = $"v{Application.version}";
        }
        public void ResetView()
        {
            SettingsOpen = false;
            ClearRankingOffer();
            endingBackground.gameObject.SetActive(false);
            gemPulseUntil = 0f;
            previousGemBalance = -1;
            telemetryPanel?.SetReport(null);
            wordBannerUntil = 0;
            if (wordBanner != null) wordBanner.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (controlLabelMaterial != null) Destroy(controlLabelMaterial);
        }

        private void ResizeGemCounter()
        {
            const float iconAndPadding = 52f;
            gems.fontSize = 24f;
            float textWidth = gems.GetPreferredValues(gems.text, Mathf.Infinity, Mathf.Infinity).x;
            float width = Mathf.Min(250f, textWidth * 1.18f + iconAndPadding);
            gems.fontSize *= Mathf.Min(1f, (width - iconAndPadding) / Mathf.Max(1f, textWidth * 1.18f));
            gemPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }


        public void ShowWordProgress(string word, bool[] collected, bool complete)
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < word.Length; i++)
            {
                if (word[i] == ' ')
                {
                    text.Append("   ");
                    continue;
                }
                if (i > 0 && word[i - 1] != ' ') text.Append(' ');
                if (collected[i]) text.Append("<color=#FFD14C>").Append(word[i]).Append("</color>");
                else text.Append('_');
            }
            wordLabel.text = text.ToString();
            if (!wordBanner.gameObject.activeSelf || Time.unscaledTime >= wordBannerUntil)
                wordBannerStarted = Time.unscaledTime;
            wordBannerUntil = Time.unscaledTime + (complete ? 3f : 1.6f);
        }

        private void RenderWordBanner(bool running)
        {
            if (wordBanner == null) return;
            float now = Time.unscaledTime;
            bool visible = running && now < wordBannerUntil;
            wordBanner.gameObject.SetActive(visible);
            if (!visible) return;
            float slide = Mathf.Min((now - wordBannerStarted) / .22f, (wordBannerUntil - now) / .22f);
            float hiddenY = wordBanner.rect.height * wordBanner.pivot.y + 9f;
            wordBanner.anchoredPosition = new Vector2(wordBannerVisiblePosition.x,
                Mathf.SmoothStep(hiddenY, wordBannerVisiblePosition.y, slide));
        }
        public void OpenSettings() => SettingsOpen = true;
        public void SetQuality(string label, bool canChange)
        {
            qualityLabel.text = label;
            qualityButton.interactable = canChange;
            if (previousQuality != null) previousQuality.interactable = canChange;
        }
        public bool IsRaceButtonAt(Vector2 screenPoint) =>
            IsButtonAt(pause, screenPoint) || IsButtonAt(brake, screenPoint) || IsButtonAt(turboButton, screenPoint);

        private static bool IsButtonAt(UnityEngine.UI.Button button, Vector2 screenPoint) =>
            button.isActiveAndEnabled && button.IsInteractable() &&
            RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, screenPoint);
        public void Render(RunSession session, GameSettings settings, bool muted, DeviceFeedbackAndInput devices, bool showBoostLines)
        {
            bool running = session.State == RunState.Running;
            bool active = running || session.State == RunState.Paused;
            bool finished = session.State == RunState.Finished;
            bool openingEnding = finished && !endingBackground.gameObject.activeSelf;
            if (finished) SettingsOpen = false;
            readyPanel.SetActive(session.State == RunState.Ready && !SettingsOpen && !RankingOpen);
            pausePanel.SetActive(session.State == RunState.Paused && !SettingsOpen);
            settingsPanel.SetActive(SettingsOpen);
            telemetryPanel?.SetReport(finished ? session.Telemetry : null);
            if (finished) RenderEnding(session.Ending, openingEnding, settings);
            else resultPanel.SetActive(false);
            raceHud.SetActive(active); controls.SetActive(active);
            turboButton.interactable = running && !SettingsOpen && session.CanActivateTurbo;
            turboFill.fillAmount = session.BoostActive ? session.BoostProgress : session.TurboChargeProgress;
            turboBolt.enabled = turboButton.interactable && turboBoltSprite != null;
            float beat = Mathf.Repeat(Time.unscaledTime, 1.1f);
            float primary = Mathf.Max(0f, 1f - Mathf.Abs(beat - .12f) / .12f);
            float secondary = Mathf.Max(0f, 1f - Mathf.Abs(beat - .34f) / .10f);
            float pulse = turboButton.interactable ? .14f * primary * primary + .08f * secondary * secondary : 0f;
            turboButton.transform.localScale = Vector3.one * (1f - pulse);

            if (boostSpeedLines != null) boostSpeedLines.Render(settings, session, showBoostLines);
            RenderWordBanner(running);
            pause.interactable = !SettingsOpen && !session.OfferPending;
            if (speedOffer != null)
            {
                speedOffer.gameObject.SetActive(running && session.OfferPending);
                if (speedOffer.gameObject.activeSelf)
                {
                    bool upgrade = session.UpgradeCost > 0;
                    bool availableToBuy = !upgrade || session.CanBuySpeed;
                    if (increaseSpeedButton.interactable != availableToBuy)
                    {
                        increaseSpeedButton.interactable = availableToBuy;
                        for (int i = 0; i < paceCardGraphics.Length; i++)
                        {
                            Color color = paceCardColors[i];
                            float gray = color.grayscale;
                            paceCardGraphics[i].color = availableToBuy ? color : new Color(gray, gray, gray, color.a);
                        }
                        paceCardIcon.sprite = availableToBuy ? increasePaceIcon : unavailablePaceIcon;
                        paceCardGemIcon.sprite = availableToBuy ? offerGemIcon : unavailableGemIcon;
                    }
                    increaseSpeedPriceBadge.gameObject.SetActive(upgrade);
                    if (upgrade)
                    {
                        string price = session.UpgradeCost.ToString();
                        increaseSpeedPrice.text = price;
                        float available = ((RectTransform)increaseSpeedPriceBadge.parent).rect.width * .88f;
                        float preferred = increaseSpeedPrice.GetPreferredValues(price).x;
                        increaseSpeedPriceBadge.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                            Mathf.Min(Mathf.Max(64f, preferred + 42f), Mathf.Max(48f, available)));
                    }
                    increaseSpeedDetail.text = upgrade
                        ? session.GemBalance < session.UpgradeCost ? $"Te faltan {session.UpgradeCost - session.GemBalance}" : ""
                        : $"Gratis · {RunSession.BoostSeconds:0} s\nGemas ×2";
                }
                milestoneBar.gameObject.SetActive(active && session.HasDecisionProgress);
                float progress = Mathf.Lerp(.015f, .985f, session.OfferProgress);
                milestoneFill.anchorMax = new Vector2(.87f, progress);
                milestoneRider.anchorMin = milestoneRider.anchorMax = new Vector2(.5f, progress);
            }
            if (session.GemBalance != previousGemBalance)
            {
                if (previousGemBalance >= 0 && session.GemBalance > previousGemBalance)
                    gemPulseUntil = Time.unscaledTime + .24f;
                previousGemBalance = session.GemBalance;
                gems.text = $"{session.GemBalance} ×";
                ResizeGemCounter();
            }
            float gemPulse = running ? Mathf.Clamp01((gemPulseUntil - Time.unscaledTime) / .24f) : 0f;
            gems.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(gemPulse * Mathf.PI) * .18f);
            score.text = session.Score.ToString();
            gauge.SetSpeed(session.Speed, settings.InitialSpeed, session.PurchasedSpeed, session.Elapsed,
                running && !session.OfferPending);
            if (SettingsOpen)
            {
                soundSwitch.color = muted ? new Color32(74, 94, 111, 255) : new Color32(30, 202, 113, 255);
                PlaceRect(soundSwitchKnob, new Vector2(muted ? .06f : .50f, .10f),
                    new Vector2(muted ? .50f : .94f, .90f));
                vibrationButton.interactable = devices.VibrationAvailable;
                vibrationLabel.text = devices.VibrationAvailable ? devices.VibrationLevelLabel : "NO DISPONIBLE";
                vibrationLabel.color = devices.VibrationAvailable ? Color.white : new Color32(255, 156, 144, 255);
            }
            if (openingEnding)
            {
                SetRankingOffer(session.Score);
                resultTitle.text = session.Ending switch
                {
                    RunEnding.Brake => "VOS MARCASTE\nEL FINAL",
                    RunEnding.Overload => "FIN DE LA\nPARTIDA",
                    _ => "RECORRIDO\nCOMPLETADO"
                };
                resultPoints.text = $"{session.Score:N0} PUNTOS";
                resultRows[0].text = $"Recorriste <color=#84CCF4><b>{session.Distance / 1000f:0.0#} km</b></color>";
                resultRows[1].text = $"Recolectaste <color=#84CCF4><b>{session.Gems} gemas</b></color>";
                resultRows[2].text = $"Chocaste <color=#FF9378><b>{Times(session.Collisions)}</b></color>";
                resultRows[3].text = $"Trataste de recuperarte <color=#6FDFAF><b>{Times(session.RecoveryAttempts)}</b></color>";
                resultRows[4].text = $"Aceleraste <color=#FFD18A><b>{Times(session.VoluntaryBoosts + session.SpeedUpgrades)}</b></color>";
            }
        }

        private static string Times(int count) => count == 1 ? "1 vez" : $"{count} veces";
    }
}
