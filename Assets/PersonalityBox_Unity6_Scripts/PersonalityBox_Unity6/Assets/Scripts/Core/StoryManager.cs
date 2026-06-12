using System.Collections;
using UnityEngine;
using TMPro;
using PersonalityBox.Core;
using PersonalityBox.Characters;

namespace PersonalityBox.Core
{
    /// <summary>
    /// In Story mode, reveals opponent backstory after each round.
    /// Final round reveals the thematic conclusion: "누가 더 인간다운가?"
    /// </summary>
    public class StoryManager : MonoBehaviour
    {
        [Header("Story UI")]
        public GameObject storyPanel;
        public TMP_Text   storyText;
        public float      displayDuration = 5f;

        [Header("Ending")]
        [TextArea(3,8)]
        public string endingTextHumanWins  = "기계의 몸 속에서도\n인간의 의지는 꺾이지 않았다.\n\n누가 더 인간다운가?\n— 그것은 몸이 아닌 마음에 달려 있다.";
        [TextArea(3,8)]
        public string endingTextMachineWins = "인간의 마음을 지닌 기계는\n마침내 링 위에 섰다.\n\n누가 더 인간다운가?\n— 감정이 있다면, 이미 인간이다.";

        MatchManager _match;

        void Start()
        {
            _match = MatchManager.Instance;
            if (_match == null || _match.mode != GameMode.Story) { enabled = false; return; }

            _match.OnRoundEnd += HandleRoundEnd;
            _match.OnMatchEnd += HandleMatchEnd;
        }

        void HandleRoundEnd(Fighter winner, int s1, int s2)
        {
            string text = $"[ ROUND {_match.CurrentRound} RESULT ]\n\n" +
                          $"{winner.data.fighterName}\n\n" +
                          winner.data.backstory;
            StartCoroutine(ShowStory(text));
        }

        void HandleMatchEnd(Fighter winner)
        {
            bool humanWins = winner.data.fighterType == FighterType.HumanMachine;
            string text = humanWins ? endingTextHumanWins : endingTextMachineWins;
            StartCoroutine(ShowStory(text, isPermanent: true));
        }

        IEnumerator ShowStory(string text, bool isPermanent = false)
        {
            storyPanel.SetActive(true);
            storyText.text = "";

            // Typewriter effect
            foreach (char c in text)
            {
                storyText.text += c;
                yield return new WaitForSeconds(0.03f);
            }

            if (!isPermanent)
            {
                yield return new WaitForSeconds(displayDuration);
                storyPanel.SetActive(false);
            }
        }
    }
}
