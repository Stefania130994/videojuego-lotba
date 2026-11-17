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
        [SerializeField] private UnityEngine.UI.Button closeSettings, soundButton, vibrationButton;
        [SerializeField] private TMPro.TMP_Text soundLabel, vibrationLabel;
        [SerializeField] private UnityEngine.UI.Button qualityButton;
        [SerializeField] private TMPro.TMP_Text qualityLabel;
        [SerializeField] private UnityEngine.UI.Button previousQuality, vibrationDebugToggle, vibrationTestButton;
        [SerializeField] private UnityEngine.UI.Image soundSwitch;
        [SerializeField] private RectTransform soundSwitchKnob, vibrationDebugSection;
        private Vector2 soundOnMin, soundOnMax;
        [SerializeField] private TMPro.TMP_Text vibrationTestResult, settingsNotice, vibrationDebugLabel;
        [SerializeField] private TMPro.TMP_Text score, gems, resultTitle;
        [SerializeField] private TMPro.TMP_Text resultPoints;
        [SerializeField] private TMPro.TMP_Text[] resultRows = new TMPro.TMP_Text[5];
        [SerializeField] private UnityEngine.UI.Image endingBackground;
        [SerializeField] private CanvasGroup endingMessage, endingResults;
        [SerializeField] private TMPro.TMP_Text endingReflection;
        [SerializeField] private UnityEngine.UI.Button endingContinueButton;
        [SerializeField] private CanvasGroup endingContinueGroup;
        private int endingReflectionCharacters;
        private float endingStarted;
        private float endingContinuedAt = float.PositiveInfinity;
        [SerializeField] private Color resultsBackdrop = new Color32(12, 33, 50, 255);

        [SerializeField] private SpeedGauge gauge;
        [SerializeField] private RectTransform wordBanner;
        [SerializeField] private TMPro.TMP_Text wordLabel;
        private Vector2 wordBannerVisiblePosition;
        private float wordBannerStarted, wordBannerUntil;
        private float gemPulseUntil;
        private int previousGemBalance;
        private RectTransform gemPanel;
        [SerializeField] private RectTransform speedOffer;
        private Sprite availablePaceIcon, availableGemIcon;
        [SerializeField] private Sprite unavailablePaceIcon, unavailableGemIcon;
        [SerializeField] private UnityEngine.UI.Image paceCardIcon, paceCardGemIcon;
        private UnityEngine.UI.Graphic[] paceCardGraphics;
        private Color[] paceCardColors;
        [SerializeField] private UnityEngine.UI.Button increaseSpeedButton, keepSpeedButton;
        [SerializeField] private TMPro.TMP_Text increaseSpeedDetail, increaseSpeedPrice;
        [SerializeField] private RectTransform increaseSpeedPriceBadge;
        [SerializeField] private BoostSpeedLines boostSpeedLines;
        [SerializeField] private RectTransform milestoneBar, milestoneFill, milestoneRider;
        private Vector2 milestoneFillEnd, milestoneRiderMin, milestoneRiderMax;
        [SerializeField] private UnityEngine.UI.Button turboButton;
        [SerializeField] private UnityEngine.UI.Image turboFill;
        [SerializeField] private UnityEngine.UI.Image turboBolt;
        private Vector3 turboRestScale, gemsRestScale;
        public bool SettingsOpen { get; private set; }
        [SerializeField] private RunTelemetryPanel telemetryPanel;
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
            ResizeGemCounter();
            wordBannerVisiblePosition = wordBanner.anchoredPosition;
            wordBanner.gameObject.SetActive(false);
            soundOnMin = soundSwitchKnob.anchorMin;
            soundOnMax = soundSwitchKnob.anchorMax;
            milestoneFillEnd = milestoneFill.anchorMax;
            milestoneRiderMin = milestoneRider.anchorMin;
            milestoneRiderMax = milestoneRider.anchorMax;
            turboRestScale = turboButton.transform.localScale;
            gemsRestScale = gems.rectTransform.localScale;
            paceCardGraphics = increaseSpeedButton.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            paceCardColors = Array.ConvertAll(paceCardGraphics, graphic => graphic.color);
            availablePaceIcon = paceCardIcon.sprite;
            availableGemIcon = paceCardGemIcon.sprite;
            BindRanking();
            telemetryPanel.Bind();
            previousQuality.onClick.AddListener(() => QualityRequested?.Invoke(-1));
            vibrationDebugToggle.onClick.AddListener(ToggleVibrationDebug);
            vibrationTestButton.onClick.AddListener(() => VibrationTestRequested?.Invoke());
            keepSpeedButton.onClick.AddListener(() => SpeedChosen?.Invoke(false));
            increaseSpeedButton.onClick.AddListener(() => SpeedChosen?.Invoke(true));
            turboButton.onClick.AddListener(() => TurboRequested?.Invoke());
            exit.gameObject.SetActive(Application.platform != RuntimePlatform.WebGLPlayer);
            exit.onClick.AddListener(ExitGame);
            start.onClick.AddListener(() => StartRequested?.Invoke()); retry.onClick.AddListener(() => StartRequested?.Invoke());
            pauseMenu.onClick.AddListener(() => MenuRequested?.Invoke());
            menu.onClick.AddListener(() => MenuRequested?.Invoke());
            pause.onClick.AddListener(() => PauseRequested?.Invoke()); resume.onClick.AddListener(() => PauseRequested?.Invoke());
            brake.onClick.AddListener(() => BrakeRequested?.Invoke());
            endingContinueButton.onClick.AddListener(ContinueEnding);
            foreach (var button in configurationButtons) button.onClick.AddListener(() => SettingsRequested?.Invoke());
            closeSettings.onClick.AddListener(() => SettingsOpen = false);
            soundButton.onClick.AddListener(() => SoundRequested?.Invoke());
            vibrationButton.onClick.AddListener(() => VibrationRequested?.Invoke());
            qualityButton.onClick.AddListener(() => QualityRequested?.Invoke(1));
            foreach (var button in GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (button != brake && button != turboButton)
                    button.onClick.AddListener(() => UiClicked?.Invoke());
        }

        public void SetVibrationTestResult(string result) => vibrationTestResult.text = result;

        private void ToggleVibrationDebug()
        {
            bool show = !vibrationDebugSection.gameObject.activeSelf;
            vibrationDebugSection.gameObject.SetActive(show);
            settingsNotice.gameObject.SetActive(!show);
            vibrationDebugLabel.text = show ? "OCULTAR PRUEBA · SOLO PARA DEBUG" : "MOSTRAR PRUEBA · SOLO PARA DEBUG";
        }

        private void RenderEnding(RunEnding ending, bool opening, GameSettings settings)
        {
            if (opening)
            {
                endingStarted = Time.unscaledTime;
                endingContinuedAt = float.PositiveInfinity;
                endingBackground.gameObject.SetActive(true);
                if (ending != RunEnding.RouteComplete)
                {
                    endingReflection.maxVisibleCharacters = 0;
                    endingReflection.ForceMeshUpdate();
                    endingReflectionCharacters = endingReflection.textInfo.characterCount;
                }
            }
            if (ending == RunEnding.RouteComplete)
            {
                endingBackground.color = Color.clear;
                endingMessage.alpha = 0f;
                endingMessage.blocksRaycasts = false;
                endingContinueButton.interactable = false;
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
            float artworkAge = age - blackoutAt;
            float messageAge = artworkAge - settings.EndingArtworkFadeSeconds;
            float charactersPerSecond = settings.EndingCharactersPerSecond;
            int typed = Mathf.FloorToInt(Mathf.Max(0f, messageAge) * charactersPerSecond);
            endingReflection.maxVisibleCharacters = Mathf.Min(typed, endingReflectionCharacters);
            float buttonAge = messageAge - endingReflectionCharacters / charactersPerSecond - settings.EndingReadingHoldSeconds;
            float resultsAge = Time.unscaledTime - endingContinuedAt;
            float reveal = FadeIn(resultsAge, settings.EndingResultsFadeSeconds);
            float buttonReveal = FadeIn(buttonAge, settings.EndingContinueFadeSeconds);
            bool canContinue = float.IsPositiveInfinity(endingContinuedAt) && buttonReveal >= 1f;
            endingMessage.alpha = FadeIn(artworkAge, settings.EndingArtworkFadeSeconds) * (1f - reveal);
            endingMessage.blocksRaycasts = endingContinueButton.interactable = canContinue;
            endingContinueGroup.alpha = buttonReveal;
            Color background = Color.Lerp(settings.Brand.Black, resultsBackdrop, reveal);
            background.a = blackout;
            endingBackground.color = background;
            endingResults.alpha = reveal;
            endingResults.interactable = endingResults.blocksRaycasts = reveal >= 1f;
            resultPanel.SetActive(resultsAge >= 0f && !TelemetryOpen && !RankingOpen);
        }

        private void ContinueEnding()
        {
            if (endingContinueButton.interactable && float.IsPositiveInfinity(endingContinuedAt))
                endingContinuedAt = Time.unscaledTime;
        }

        private static float FadeIn(float age, float duration) =>
            age < 0f ? 0f : duration > 0f ? Mathf.SmoothStep(0f, 1f, age / duration) : 1f;

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
            turboBolt.enabled = turboButton.interactable && turboBolt.sprite != null;
            float beat = Mathf.Repeat(Time.unscaledTime, 1.1f);
            float primary = Mathf.Max(0f, 1f - Mathf.Abs(beat - .12f) / .12f);
            float secondary = Mathf.Max(0f, 1f - Mathf.Abs(beat - .34f) / .10f);
            float pulse = turboButton.interactable ? .14f * primary * primary + .08f * secondary * secondary : 0f;
            turboButton.transform.localScale = turboRestScale * (1f - pulse);

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
                        paceCardIcon.sprite = availableToBuy ? availablePaceIcon : unavailablePaceIcon;
                        paceCardGemIcon.sprite = availableToBuy ? availableGemIcon : unavailableGemIcon;
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
                float progress = Mathf.Lerp(milestoneFill.anchorMin.y, milestoneFillEnd.y, session.OfferProgress);
                milestoneFill.anchorMax = new Vector2(milestoneFillEnd.x, progress);
                milestoneRider.anchorMin = new Vector2(milestoneRiderMin.x, milestoneRiderMin.y + progress);
                milestoneRider.anchorMax = new Vector2(milestoneRiderMax.x, milestoneRiderMax.y + progress);
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
            gems.rectTransform.localScale = gemsRestScale * (1f + Mathf.Sin(gemPulse * Mathf.PI) * .18f);
            score.text = session.Score.ToString();
            gauge.SetSpeed(session.Speed, settings.InitialSpeed, session.PurchasedSpeed, session.Elapsed,
                running && !session.OfferPending);
            if (SettingsOpen)
            {
                soundSwitch.color = muted ? new Color32(74, 94, 111, 255) : new Color32(30, 202, 113, 255);
                soundSwitchKnob.anchorMin = muted ? new Vector2(1f - soundOnMax.x, soundOnMin.y) : soundOnMin;
                soundSwitchKnob.anchorMax = muted ? new Vector2(1f - soundOnMin.x, soundOnMax.y) : soundOnMax;
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
