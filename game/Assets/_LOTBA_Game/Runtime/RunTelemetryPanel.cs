using System;
using System.Text;
using TMPro;
using UnityEngine;

namespace Lotba.Game
{
    internal sealed class RunTelemetryPanel
    {
        private readonly UnityEngine.UI.Button openButton, buttonTemplate;
        private readonly UnityEngine.UI.Image backgroundTemplate;
        private readonly TMP_FontAsset font;
        private RectTransform panel, content;
        private UnityEngine.UI.ScrollRect scroll;
        private TMP_Text copyLabel;
        private RunTelemetry report, displayedReport;
        private static readonly Color Cyan = new Color32(130, 220, 247, 255);
        private static readonly Color Gold = new Color32(255, 216, 112, 255);

        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        public RunTelemetryPanel(GameObject results, TMP_Text resultStats, UnityEngine.UI.Button menu)
        {
            buttonTemplate = menu;
            backgroundTemplate = results.GetComponent<UnityEngine.UI.Image>();
            font = resultStats.font;
            openButton = AddButton("Ver telemetría", results.transform, new Vector2(.2f, .015f),
                new Vector2(.8f, .065f), Open, out TMP_Text openLabel);
            openButton.targetGraphic.color = Color.clear;
            openLabel.color = new Color32(160, 200, 226, 255);
            openButton.gameObject.SetActive(false);
        }

        public void SetReport(RunTelemetry value)
        {
            report = value;
            bool available = report?.summary != null;
            if (openButton.gameObject.activeSelf != available) openButton.gameObject.SetActive(available);
            if (!available) Close();
        }

        public void Close()
        {
            if (IsOpen) panel.gameObject.SetActive(false);
        }

        private void Open()
        {
            if (report?.summary == null) return;
            if (panel == null) BuildPanel();
            panel.gameObject.SetActive(true);
            copyLabel.text = "Copiar telemetría completa (JSON)";
            if (displayedReport != report)
            {
                for (int i = content.childCount - 1; i >= 0; i--)
                {
                    var row = content.GetChild(i).gameObject;
                    row.SetActive(false);
                    UnityEngine.Object.Destroy(row);
                }
                PopulateReport();
                displayedReport = report;
            }
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }

        private void BuildPanel()
        {
            panel = CreateRect("Telemetría", backgroundTemplate.transform.parent, new Vector2(.055f, .07f), new Vector2(.945f, .93f));
            var frame = AddBackground(panel, new Color32(114, 195, 219, 255));
            frame.raycastTarget = true;
            var shade = panel.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shade.effectColor = new Color32(0, 0, 0, 130);
            shade.effectDistance = new Vector2(0f, -5f);
            var interior = CreateRect("Interior", panel, Vector2.zero, Vector2.one);
            interior.offsetMin = Vector2.one * 2f;
            interior.offsetMax = Vector2.one * -2f;
            AddBackground(interior, new Color32(15, 39, 57, 247));
            TMP_Text title = AddText(panel, "Telemetría de la partida", 32f, true);
            SetRect(title.rectTransform, new Vector2(.05f, .89f), new Vector2(.95f, .98f));
            title.enableAutoSizing = true;
            title.fontSizeMin = 22f;
            title.fontSizeMax = 32f;
            title.alignment = TextAlignmentOptions.Center;

            RectTransform scrollRoot = CreateRect("Registro", panel, new Vector2(.06f, .145f), new Vector2(.94f, .88f));
            scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            RectTransform viewport = CreateRect("Viewport", scrollRoot, Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            content = CreateRect("Content", viewport, Vector2.up, Vector2.one);
            content.pivot = new Vector2(.5f, 1f);
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(1, 1, 4, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            var copyButton = AddButton("Copiar telemetría completa (JSON)", panel,
                new Vector2(.06f, .034f), new Vector2(.70f, .116f), Copy, out copyLabel);
            StyleFooterButton(copyButton, new Color32(13, 150, 133, 255));
            copyLabel.enableAutoSizing = true;
            copyLabel.fontSizeMin = 14f;
            copyLabel.fontSizeMax = 22f;
            var closeButton = AddButton("Cerrar", panel, new Vector2(.735f, .034f),
                new Vector2(.94f, .116f), Close, out TMP_Text closeLabel);
            StyleFooterButton(closeButton, new Color32(48, 91, 122, 255));
            closeLabel.fontSize = 21f;
        }

        private void PopulateReport()
        {
            RunTelemetrySummary result = report.summary;
            var summary = new StringBuilder()
                .AppendLine(Ending(result.ending))
                .AppendLine($"{Highlight(result.score)} pts · {Highlight(result.gems)} gemas · " +
                    $"{Highlight(result.collisions)} choques · {Highlight(result.distanceMetres.ToString("0.0"))} m")
                .AppendLine()
                .AppendLine("Velocidad y tiempo")
                .Append($"{Highlight(result.timeSeconds.ToString("0.0"))} s activos · " +
                    $"{Highlight(result.peakSpeedKmh.ToString("0.0"))} km/h máx.");
            if (report.schemaVersion >= 2)
                summary.Append($"\n\nAceleraciones voluntarias: {Highlight(result.voluntaryBoosts)} · Gemas extra: {Highlight(result.bonusGems)}");
            AddCard("RESUMEN DE JUEGO", summary.ToString());

            var events = new StringBuilder();
            if (report.schemaVersion >= 5)
                events.AppendLine($"Ofertas mostradas: {Highlight(result.decisionsShown)} · Ritmo mantenido: {Highlight(result.decisionsKept)}");
            if (report.schemaVersion >= 3)
                events.AppendLine($"Gemas recogidas: {Highlight(result.gems)} (perdidas: {Highlight(result.gemsLost)}) · Saldo: {Highlight(result.gemBalance)}");
            if (report.schemaVersion >= 9)
                events.AppendLine($"Mejoras de ritmo: {Highlight(result.speedUpgrades)} · Gemas gastadas: {Highlight(result.gemsSpent)} · " +
                    $"Aumento permanente: {Highlight($"+{result.purchasedSpeedKmh:0.#} km/h")}");
            if (report.schemaVersion >= 10)
                events.AppendLine($"Aceleraciones con el botón: {Highlight(result.manualBoosts)} · Choques con impulso: {Highlight(result.boostCollisions)}");
            else if (report.schemaVersion >= 3)
                events.AppendLine($"Choques con impulso: {Highlight(result.boostCollisions)}");
            if (report.schemaVersion >= 13)
                events.AppendLine($"Intentos de recuperación: {Highlight(result.recoveryAttempts)}");
            if (events.Length > 0) AddCard("EVENTOS Y MEJORAS", events.ToString().TrimEnd());

            string routeLength = report.schemaVersion < 7 ? $"Tiempo previsto: {report.durationSeconds:0} s" :
                report.routeDistanceMetres > 0f ? $"Distancia de ruta: {report.routeDistanceMetres:0} m" : "Ruta continua";
            AddCard("DATOS TÉCNICOS", $"Ruta: {report.route} · Semilla: {report.seed}\n" +
                $"Versión: {report.gameVersion} · Esquema: {report.schemaVersion} · {routeLength}\nID partida: {report.runId}", true);
        }

        private static string Highlight(object value) => $"<color=#FFD870><b>{value}</b></color>";

        private void AddCard(string title, string text, bool technical = false)
        {
            RectTransform card = CreateRect(title, content, Vector2.zero, Vector2.one);
            AddBackground(card, new Color32(8, 24, 37, 240));
            var layout = card.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 13);
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            AddText(card, title, 22f, true).color = Cyan;
            TMP_Text body = AddText(card, text, technical ? 16f : 19f);
            body.lineSpacing = 5f;
            body.richText = !technical;
            if (technical) body.color = new Color32(205, 222, 234, 255);
        }

        private UnityEngine.UI.Image AddBackground(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = backgroundTemplate.sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void StyleFooterButton(UnityEngine.UI.Button button, Color color)
        {
            button.targetGraphic.color = color;
            var shade = button.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shade.effectColor = new Color32(0, 0, 0, 125);
            shade.effectDistance = new Vector2(0f, -4f);
        }

        private void Copy()
        {
            if (report?.summary == null) return;
            GUIUtility.systemCopyBuffer = report.ToJson();
            copyLabel.text = "JSON copiado";
        }

        private UnityEngine.UI.Button AddButton(string title, Transform parent, Vector2 min, Vector2 max, Action onClick, out TMP_Text label)
        {
            var button = UnityEngine.Object.Instantiate(buttonTemplate, parent, false);
            button.name = title;
            SetRect((RectTransform)button.transform, min, max);
            button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.onClick.AddListener(() => onClick());
            label = button.GetComponentInChildren<TMP_Text>(true);
            SetRect(label.rectTransform, Vector2.zero, Vector2.one);
            Transform icon = button.transform.Find("Ícono de resultado");
            if (icon != null) icon.gameObject.SetActive(false);
            label.text = title;
            return button;
        }

        private TMP_Text AddText(Transform parent, string text, float size, bool bold = false)
        {
            RectTransform rect = CreateRect("Texto", parent, Vector2.zero, Vector2.one);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.richText = false;
            label.raycastTarget = false;
            label.color = bold ? Gold : Color.white;
            label.text = text;
            return label;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            SetRect(rect, min, max);
            return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static string Ending(string reason) => reason == "brake" ? "Frenado voluntario" :
            reason == "route_complete" ? "Recorrido completado" : "Fin por tiempo";

    }
}
