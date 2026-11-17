using TMPro;
using UnityEngine;

namespace Lotba.Game
{
    public sealed partial class GameHud
    {
        private static readonly Color RankingGold = new Color32(255, 210, 102, 255);
        private static readonly Color RankingSlate = new Color32(35, 72, 98, 255);
        private readonly TMP_Text[,] rankingLetters = new TMP_Text[LocalHighScores.Capacity, LocalHighScores.NicknameLength];
        private readonly TMP_Text[] rankingScores = new TMP_Text[LocalHighScores.Capacity];
        private readonly UnityEngine.UI.Image[] rankingRows = new UnityEngine.UI.Image[LocalHighScores.Capacity];
        private readonly TMP_Text[] nicknameLetters = new TMP_Text[LocalHighScores.NicknameLength];
        private readonly UnityEngine.UI.Image[] nicknameSlots = new UnityEngine.UI.Image[LocalHighScores.NicknameLength];
        private LocalHighScores highScores;
        private RectTransform rankingOverlay, rankingCard, rankingList, rankingEditor, rankingActions;
        private UnityEngine.UI.Button rankingOffer, saveRankingButton, eraseNicknameButton;
        private TMP_Text rankingOfferLabel, rankingCloseLabel, rankingEntryLabel;
        private long? rankingCandidate;
        private string rankingNickname = string.Empty;
        private int highlightedRank = -1;

        public bool RankingOpen => rankingOverlay != null && rankingOverlay.gameObject.activeSelf;

        private void CreateRanking()
        {
            highScores = new LocalHighScores();
            rankingOffer = AddRankingButton(resultPanel.transform, new Vector2(.25f, .29f),
                new Vector2(.75f, .365f), "GUARDAR MARCA", 19f, RankingSlate,
                () => OpenRanking(true), out rankingOfferLabel);
            if (trophyIcon != null)
            {
                var icon = Background(Region("Trofeo", rankingOffer.transform, new Vector2(.055f, .16f),
                    new Vector2(.25f, .84f)), Color.white);
                icon.sprite = trophyIcon;
                icon.preserveAspect = true;
                PlaceRect(rankingOfferLabel.rectTransform, new Vector2(.25f, .08f), new Vector2(.97f, .92f));
            }
            rankingOffer.gameObject.SetActive(false);

            rankingOverlay = Region("Ranking local", readyPanel.transform.parent, Vector2.zero, Vector2.one);
            Background(rankingOverlay, new Color32(5, 16, 28, 200)).raycastTarget = true;
            rankingCard = Region("Panel de récords", rankingOverlay, Vector2.zero, Vector2.one);
            RoundedBackground(rankingCard, new Color32(12, 33, 50, 255));
            var title = MenuCaption("Título", rankingCard, new Vector2(.05f, .89f), new Vector2(.95f, .99f),
                "RÉCORDS LOCALES", 30f);
            title.color = RankingGold;
            rankingList = Region("Puestos", rankingCard, Vector2.zero, Vector2.one);
            for (int row = 0; row < LocalHighScores.Capacity; row++)
            {
                float bottom = 1f - (row + 1) * .2f;
                var line = Region($"Puesto {row + 1}", rankingList, new Vector2(0f, bottom + .013f),
                    new Vector2(1f, bottom + .19f));
                rankingRows[row] = Background(line, RankingSlate);
                MenuCaption("Puesto", line, new Vector2(.01f, 0f), new Vector2(.13f, 1f), $"{row + 1:00}", 20f)
                    .color = RankingGold;
                for (int i = 0; i < LocalHighScores.NicknameLength; i++)
                {
                    float x = .15f + i * .098f;
                    rankingLetters[row, i] = AddLetterSlot(line, x, .084f, .15f, .85f, out _);
                }
                rankingScores[row] = MenuCaption("Puntaje", line, new Vector2(.66f, 0f), new Vector2(.97f, 1f), "", 24f);
                rankingScores[row].alignment = TextAlignmentOptions.MidlineRight;
            }

            rankingEditor = Region("Ingresar apodo", rankingCard, new Vector2(.055f, .16f), new Vector2(.945f, .475f));
            rankingEntryLabel = MenuCaption("Apodo y puntaje", rankingEditor, new Vector2(0f, .84f),
                new Vector2(1f, 1f), "", 20f);
            for (int i = 0; i < LocalHighScores.NicknameLength; i++)
                nicknameLetters[i] = AddLetterSlot(rankingEditor, .19f + i * .126f, .115f, .63f, .83f, out nicknameSlots[i]);
            for (int i = 0; i < LocalHighScores.Alphabet.Length; i++)
            {
                char letter = LocalHighScores.Alphabet[i];
                float x = i % 9 / 9f, y = .6f - i / 9 * .195f;
                AddRankingButton(rankingEditor, new Vector2(x + .004f, y - .176f),
                    new Vector2(x + 1f / 9f - .004f, y), letter.ToString(), 22f, RankingSlate,
                    () => AddNicknameLetter(letter), out _);
            }

            rankingActions = Region("Acciones", rankingCard, Vector2.zero, Vector2.one);
            AddRankingButton(rankingActions, Vector2.zero, Vector2.one, "CERRAR", 19f, RankingSlate,
                CloseRanking, out rankingCloseLabel);
            eraseNicknameButton = AddRankingButton(rankingActions, new Vector2(.27f, 0f), new Vector2(.53f, 1f),
                "BORRAR", 17f, RankingSlate, EraseNicknameLetter, out _);
            saveRankingButton = AddRankingButton(rankingActions, new Vector2(.56f, 0f), Vector2.one,
                "GUARDAR", 20f, new Color32(32, 132, 83, 255), SaveRanking, out _);
            rankingOverlay.gameObject.SetActive(false);
        }

        private TMP_Text AddLetterSlot(Transform parent, float x, float width, float bottom, float top,
            out UnityEngine.UI.Image slot)
        {
            var box = Region("Casillero", parent, new Vector2(x, bottom), new Vector2(x + width, top));
            slot = Background(box, new Color32(64, 103, 132, 255));
            var interior = Region("Interior", box, Vector2.zero, Vector2.one);
            interior.offsetMin = Vector2.one;
            interior.offsetMax = -Vector2.one;
            Background(interior, new Color32(17, 42, 60, 255));
            var letter = MenuCaption("Letra", interior, Vector2.zero, Vector2.one, "", 24f);
            letter.enableAutoSizing = false;
            return letter;
        }

        private UnityEngine.UI.Button AddRankingButton(Transform parent, Vector2 min, Vector2 max,
            string caption, float size, Color tint, UnityEngine.Events.UnityAction action, out TMP_Text label)
        {
            var rect = Region(caption, parent, min, max);
            RoundedBackground(rect, tint);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            label = MenuCaption("Etiqueta", rect, new Vector2(.04f, .06f), new Vector2(.96f, .94f), caption, size);
            return button;
        }

        private void SetRankingOffer(long score)
        {
            int rank = highScores.RankFor(score);
            rankingCandidate = rank >= 0 ? score : (long?)null;
            rankingOfferLabel.text = rank == 0 ? "NUEVO RÉCORD" : "GUARDAR MARCA";
            rankingOfferLabel.color = rank == 0 ? RankingGold : Color.white;
            rankingOffer.gameObject.SetActive(rankingCandidate.HasValue);
        }

        private void ClearRankingOffer()
        {
            CloseRanking();
            rankingCandidate = null;
            rankingOffer.gameObject.SetActive(false);
        }

        private void OpenRanking(bool fromResults)
        {
            if (fromResults && !rankingCandidate.HasValue) return;
            rankingNickname = string.Empty;
            highlightedRank = -1;
            rankingOverlay.SetAsLastSibling();
            rankingOverlay.gameObject.SetActive(true);
            ShowRanking(fromResults);
        }

        public void CloseRanking() => rankingOverlay.gameObject.SetActive(false);

        private void ShowRanking(bool editing)
        {
            PlaceRect(rankingCard, new Vector2(.05f, editing ? .07f : .20f),
                new Vector2(.95f, editing ? .93f : .80f));
            PlaceRect(rankingList, new Vector2(.055f, editing ? .48f : .20f), new Vector2(.945f, .88f));
            PlaceRect(rankingActions, new Vector2(.055f, .045f), new Vector2(.945f, editing ? .13f : .16f));
            PlaceRect((RectTransform)rankingCloseLabel.transform.parent,
                editing ? Vector2.zero : new Vector2(.20f, 0f),
                editing ? new Vector2(.24f, 1f) : new Vector2(.80f, 1f));
            rankingCloseLabel.text = editing ? "VOLVER" : "CERRAR";
            rankingEditor.gameObject.SetActive(editing);
            eraseNicknameButton.gameObject.SetActive(editing);
            saveRankingButton.gameObject.SetActive(editing);
            for (int row = 0; row < LocalHighScores.Capacity; row++)
            {
                var entry = row < highScores.Entries.Count ? highScores.Entries[row] : null;
                rankingRows[row].color = row == highlightedRank ? new Color32(33, 99, 81, 255) : RankingSlate;
                rankingScores[row].text = entry != null ? entry.score.ToString("N0") : "—";
                for (int i = 0; i < LocalHighScores.NicknameLength; i++)
                    rankingLetters[row, i].text = entry != null && i < entry.nickname.Length
                        ? entry.nickname[i].ToString() : string.Empty;
            }
            if (editing)
            {
                rankingEntryLabel.text = $"APODO · {rankingCandidate.GetValueOrDefault():N0} PUNTOS";
                UpdateNickname();
            }
        }

        private void AddNicknameLetter(char letter)
        {
            if (rankingNickname.Length >= LocalHighScores.NicknameLength) return;
            rankingNickname += letter;
            UpdateNickname();
        }

        private void EraseNicknameLetter()
        {
            if (rankingNickname.Length == 0) return;
            rankingNickname = rankingNickname.Substring(0, rankingNickname.Length - 1);
            UpdateNickname();
        }

        private void UpdateNickname()
        {
            for (int i = 0; i < nicknameLetters.Length; i++)
            {
                nicknameLetters[i].text = i < rankingNickname.Length ? rankingNickname[i].ToString() : string.Empty;
                nicknameSlots[i].color = i == rankingNickname.Length ? RankingGold : new Color32(64, 103, 132, 255);
            }
            eraseNicknameButton.interactable = saveRankingButton.interactable = rankingNickname.Length > 0;
        }

        private void SaveRanking()
        {
            if (!rankingCandidate.HasValue) return;
            if (!highScores.TrySave(rankingNickname, rankingCandidate.Value, out highlightedRank))
            {
                rankingEntryLabel.text = "NO SE PUDO GUARDAR";
                return;
            }
            rankingCandidate = null;
            rankingOffer.gameObject.SetActive(false);
            ShowRanking(false);
        }
    }
}
