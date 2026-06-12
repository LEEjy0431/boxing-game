using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using PersonalityBox.Core;

namespace PersonalityBox.UI
{
    /// <summary>
    /// Handles main menu, character select, and mode select.
    /// Selected data is passed via GameSettings singleton.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject mainMenuPanel;
        public GameObject modeSelectPanel;
        public GameObject charSelectPanel;

        [Header("Mode buttons – assign in Inspector")]
        // (linked via UnityEvent in Inspector to the public methods below)

        [Header("Character Select")]
        public FighterData[] availableFighters;   // assign HumanMachine & MachineHuman SOs

        [Header("Labels")]
        public TMP_Text p1SelectLabel;
        public TMP_Text p2SelectLabel;

        int _p1Choice = 0;
        int _p2Choice = 1;

        public void ShowModeSelect()
        {
            mainMenuPanel.SetActive(false);
            modeSelectPanel.SetActive(true);
        }

        public void SelectMode(int modeIndex)
        {
            GameSettings.Instance.mode = (GameMode)modeIndex;
            modeSelectPanel.SetActive(false);
            charSelectPanel.SetActive(true);
        }

        public void P1ChooseFighter(int index)
        {
            _p1Choice = index;
            p1SelectLabel.text = availableFighters[index].fighterName;
        }

        public void P2ChooseFighter(int index)
        {
            _p2Choice = index;
            p2SelectLabel.text = availableFighters[index].fighterName;
        }

        public void StartFight()
        {
            GameSettings.Instance.fighter1Data = availableFighters[_p1Choice];
            GameSettings.Instance.fighter2Data = availableFighters[_p2Choice];
            SceneManager.LoadScene("FightScene");
        }

        public void QuitGame() => Application.Quit();
    }
}
