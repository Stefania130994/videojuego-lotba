using System;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Scene UI bindings. Configuration exposes only implemented, available capabilities.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text gameTitle, developmentLabel, identityFooter, buildVersion;
        [SerializeField] private GameObject readyPanel, pausePanel, resultPanel, raceHud, controls, settingsPanel;
        [SerializeField] private UnityEngine.UI.Button start, retry, menu, pause, resume, brake, pauseMenu, exit;
        [SerializeField] private UnityEngine.UI.Button[] configurationButtons;
        [SerializeField] private UnityEngine.UI.Button closeSettings, soundButton, vibrationButton;
        [SerializeField] private TMPro.TMP_Text soundLabel, vibrationLabel;
        [SerializeField] private UnityEngine.UI.Button qualityButton;
        [SerializeField] private TMPro.TMP_Text qualityLabel;
        [SerializeField] private TMPro.TMP_Text score, gems, multiplier, resultTitle, resultStats;
        [SerializeField] private Sprite flameOutline, flameFill;
        [SerializeField] private SpeedGauge gauge;
        [SerializeField] private RectTransform wordBanner;
        [SerializeField] private TMPro.TMP_Text wordLabel;
        private Vector2 wordBannerVisiblePosition;
        private float wordBannerStarted, wordBannerUntil;
        private float multiplierPulseUntil;
        private float gemPulseUntil;
        private int previousGemBalance;
        private RectTransform gemPanel;
        private RectTransform speedOffer;
        private BoostSpeedLines boostSpeedLines;
        private RectTransform milestoneBar;
        private RectTransform milestoneFill;
        private RectTransform milestoneRider;
        private UnityEngine.UI.Image multiplierFill;
        private TMPro.TextMeshProUGUI milestoneLabel;
        private RectTransform boostTimeFill;
        private int previousMultiplier = 1;
        public bool SettingsOpen { get; private set; }
        private RunTelemetryPanel telemetryPanel;
        public bool TelemetryOpen => telemetryPanel != null && telemetryPanel.IsOpen;
        public void CloseTelemetry() => telemetryPanel?.Close();
        public event Action StartRequested, MenuRequested, PauseRequested, BrakeRequested, SoundRequested, SettingsRequested, VibrationRequested;
        public event Action QualityRequested;
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
            qualityButton.onClick.AddListener(() => QualityRequested?.Invoke());
        }

        private void CreateSpeedOffer()
        {
            RectTransform Region(string name, Transform parent, Vector2 min, Vector2 max)
            {
                var region = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
                region.SetParent(parent, false);
                region.anchorMin = min;
                region.anchorMax = max;
                region.offsetMin = region.offsetMax = Vector2.zero;
                return region;
            }

            TMPro.TextMeshProUGUI Label(string name, Transform parent, Vector2 min, Vector2 max, string text, float size)
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

            UnityEngine.UI.Image Background(RectTransform region, Color color)
            {
                var image = region.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.color = color;
                image.raycastTarget = false;
                return image;
            }

            void Card(string name, Vector2 min, Vector2 max, Color color, string description, bool accelerate)
            {
                RectTransform card = Region(name, speedOffer, min, max);
                var image = Background(card, color);
                image.raycastTarget = true;
                var button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => SpeedChosen?.Invoke(accelerate));
                Label("Título", card, new Vector2(.05f, .58f), new Vector2(.95f, .94f), name, 23f).fontStyle = TMPro.FontStyles.Bold;
                Label("Detalle", card, new Vector2(.07f, .08f), new Vector2(.93f, .57f), description, 17f);
            }

            void RoundedBackground(RectTransform region, Color color)
            {
                var image = Background(region, color);
                image.sprite = gemPanel.GetComponent<UnityEngine.UI.Image>().sprite;
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 3f;
            }

            milestoneBar = Region("Progreso de zona", raceHud.transform, new Vector2(.046f, .44f), new Vector2(.070f, .72f));
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
            milestoneLabel = Label("Indicador de impulso", raceHud.transform, new Vector2(.28f, .69f), new Vector2(.72f, .74f), "IMPULSO · GEMAS ×2", 17f);
            var boostTrack = Region("Tiempo de impulso", milestoneLabel.transform, new Vector2(.12f, 0f), new Vector2(.88f, .09f));
            Background(boostTrack, new Color32(17, 29, 43, 235));
            boostTimeFill = Region("Tiempo restante", boostTrack, Vector2.zero, Vector2.one);
            Background(boostTimeFill, new Color32(107, 221, 195, 255));
            milestoneLabel.gameObject.SetActive(false);

            Transform badge = multiplier.transform.parent;
            var flameRegion = Region("Fuego", badge, new Vector2(.05f, .06f), new Vector2(.49f, .94f));
            multiplierFill = Background(Region("Relleno", flameRegion, Vector2.zero, Vector2.one), new Color32(255, 159, 45, 255));
            multiplierFill.sprite = flameFill;
            multiplierFill.preserveAspect = true;
            multiplierFill.type = UnityEngine.UI.Image.Type.Filled;
            multiplierFill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
            multiplierFill.fillAmount = 0f;
            var outline = Background(Region("Contorno", flameRegion, Vector2.zero, Vector2.one), new Color32(255, 221, 164, 255));
            outline.sprite = flameOutline;
            outline.preserveAspect = true;

            speedOffer = Region("Tarjetas de velocidad", raceHud.transform, new Vector2(.04f, .35f), new Vector2(.96f, .66f));
            Background(speedOffer, new Color32(16, 27, 39, 245));
            Label("Encabezado", speedOffer, new Vector2(.04f, .73f), new Vector2(.96f, .98f), "Elegí tu ritmo", 27f);
            Card("Mantener ritmo", new Vector2(.04f, .08f), new Vector2(.48f, .71f),
                new Color32(47, 71, 95, 255), "Velocidad y gemas habituales", false);
            Card("Acelerar", new Vector2(.52f, .08f), new Vector2(.96f, .71f),
                new Color32(121, 61, 36, 255), $"{RunSession.BoostSeconds:0} s · Gemas ×2\nMenos tiempo para esquivar", true);
            milestoneBar.gameObject.SetActive(false);
            speedOffer.gameObject.SetActive(false);
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
            multiplierPulseUntil = 0; previousMultiplier = 1; SettingsOpen = false;
            gemPulseUntil = 0f;
            previousGemBalance = -1;
            telemetryPanel?.SetReport(null);
            wordBannerUntil = 0;
            if (wordBanner != null) wordBanner.gameObject.SetActive(false);
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
                if (collected[i]) text.Append("<color=#FFD14C>").Append(word[i]).Append("</color>");
                else text.Append(word[i]);
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
            qualityLabel.text = $"CALIDAD: {label}";
            qualityButton.interactable = canChange;
        }
        public bool IsRaceButtonAt(Vector2 screenPoint) =>
            IsButtonAt(pause, screenPoint) || IsButtonAt(brake, screenPoint);

        private static bool IsButtonAt(UnityEngine.UI.Button button, Vector2 screenPoint) =>
            button.isActiveAndEnabled && button.IsInteractable() &&
            RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, screenPoint);
        public void Render(RunSession session, GameSettings settings, bool muted, DeviceFeedbackAndInput devices, bool showBoostLines)
        {
            bool running = session.State == RunState.Running;
            bool active = running || session.State == RunState.Paused;
            if (session.State == RunState.Finished) SettingsOpen = false;
            readyPanel.SetActive(session.State == RunState.Ready && !SettingsOpen);
            pausePanel.SetActive(session.State == RunState.Paused && !SettingsOpen);
            settingsPanel.SetActive(SettingsOpen);
            telemetryPanel?.SetReport(session.State == RunState.Finished ? session.Telemetry : null);
            resultPanel.SetActive(session.State == RunState.Finished && !TelemetryOpen);
            raceHud.SetActive(active); controls.SetActive(active);
            if (boostSpeedLines != null) boostSpeedLines.Render(settings, session, showBoostLines);
            RenderWordBanner(running);
            pause.interactable = !SettingsOpen && !session.OfferPending;
            if (speedOffer != null)
            {
                speedOffer.gameObject.SetActive(running && session.OfferPending);
                milestoneBar.gameObject.SetActive(active && session.HasZoneProgress);
                float progress = Mathf.Lerp(.015f, .985f, session.OfferProgress);
                milestoneFill.anchorMax = new Vector2(.87f, progress);
                milestoneRider.anchorMin = milestoneRider.anchorMax = new Vector2(.5f, progress);
                milestoneLabel.gameObject.SetActive(running && session.BoostActive);
                boostTimeFill.anchorMax = new Vector2(session.BoostProgress, 1f);
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
            multiplier.text = $"×{session.Multiplier}";
            if (multiplierFill != null) multiplierFill.fillAmount = session.MultiplierProgress;
            if (session.Multiplier > previousMultiplier) multiplierPulseUntil = Time.unscaledTime + .3f;
            previousMultiplier = session.Multiplier;
            float pulse = running ? Mathf.Clamp01((multiplierPulseUntil - Time.unscaledTime) / .3f) : 0;
            multiplier.transform.parent.localScale = Vector3.one * (1 + Mathf.Sin(pulse * Mathf.PI) * .18f);
            gauge.SetSpeed(session.Speed, settings.InitialSpeed, settings.MaximumSpeed);
            soundLabel.text = muted ? "SONIDO: NO" : "SONIDO: SÍ";
            vibrationButton.interactable = devices.VibrationAvailable;
            vibrationLabel.text = !devices.VibrationAvailable ? "VIBRACIÓN: NO DISPONIBLE" : "VIBRACIÓN: " + devices.VibrationLevelLabel;
            if (session.State == RunState.Finished)
            {
                resultTitle.text = session.Ending == RunEnding.Brake ? "VOS MARCASTE\nEL FINAL" : "RECORRIDO\nCOMPLETADO";
                string question = session.VoluntaryBoosts > 0
                    ? "¿Qué buscabas al elegir acelerar?"
                    : session.DecisionsKept > 0 ? "¿Qué te hizo elegir mantener el ritmo?"
                    : session.Ending == RunEnding.Brake ? "¿Qué te hizo decidir frenar?" : "¿Qué te motivó a seguir?";
                resultStats.text = $"<size=42><color=#FFD14C>{session.Score}</color></size> puntos\n" +
                    $"{session.GemBalance} gemas · {session.Collisions} choques · {session.Distance:0} m\n" +
                    $"Elegiste acelerar {session.VoluntaryBoosts} · mantener {session.DecisionsKept}\n\n" +
                    $"{question}\n\nEn las apuestas, la expectativa de ganar puede influir en cuánto decidimos seguir.\n" +
                    "<b>HABLAR ES GANAR</b>";
            }
        }
    }
}
