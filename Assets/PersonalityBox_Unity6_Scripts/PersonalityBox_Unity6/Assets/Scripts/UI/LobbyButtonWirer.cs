using UnityEngine;
using UnityEngine.UI;
using PersonalityBox.UI;

namespace PersonalityBox.UI
{
    /// <summary>
    /// 로비 씬 로드 시 MainMenuUI에 버튼 이벤트를 자동 연결.
    /// GameSetup이 씬을 자동 생성할 때 Canvas에 추가됨.
    /// </summary>
    public class LobbyButtonWirer : MonoBehaviour
    {
        void Start()
        {
            var menu = FindFirstObjectByType<MainMenuUI>();
            if (menu == null) { Debug.LogWarning("[LobbyButtonWirer] MainMenuUI를 찾지 못했습니다."); return; }

            // 로비 버튼
            Wire("StartButton",    menu.OnClickStart);
            Wire("SettingsButton", menu.OnClickSettings);
            Wire("CommandsButton", menu.OnClickCommands);
            Wire("QuitButton",     menu.OnClickQuit);

            // 뒤로 버튼 (패널마다)
            Wire("BackButton_Mode",     menu.OnClickBack);
            Wire("BackButton_Settings", menu.OnClickBack);
            Wire("BackButton_Commands", menu.OnClickBack);
            Wire("BackButton_Char",     menu.OnClickBack);

            // 싸우기 버튼
            Wire("StartFightButton", menu.StartFight);

            // 모드 선택 (Story=0, VsAI=1, TwoPlayer=2)
            WireInt("Mode0_Button", menu.SelectMode, 0);
            WireInt("Mode1_Button", menu.SelectMode, 1);
            WireInt("Mode2_Button", menu.SelectMode, 2);

            // 파이터 선택
            WireInt("P1Fighter0_Button", menu.P1ChooseFighter, 0);
            WireInt("P1Fighter1_Button", menu.P1ChooseFighter, 1);
            WireInt("P2Fighter0_Button", menu.P2ChooseFighter, 0);
            WireInt("P2Fighter1_Button", menu.P2ChooseFighter, 1);
        }

        // GetComponentsInChildren(true) = 비활성 패널 안의 버튼도 탐색
        void Wire(string btnName, UnityEngine.Events.UnityAction action)
        {
            foreach (var btn in GetComponentsInChildren<Button>(true))
            {
                if (btn.name == btnName)
                {
                    btn.onClick.AddListener(action);
                    return;
                }
            }
        }

        void WireInt(string btnName, System.Action<int> action, int arg)
        {
            foreach (var btn in GetComponentsInChildren<Button>(true))
            {
                if (btn.name == btnName)
                {
                    btn.onClick.AddListener(() => action(arg));
                    return;
                }
            }
        }
    }
}
