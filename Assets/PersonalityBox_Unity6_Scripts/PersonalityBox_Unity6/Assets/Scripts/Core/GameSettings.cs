using UnityEngine;
using PersonalityBox.Core;

namespace PersonalityBox.Core
{
    /// <summary>
    /// Persistent singleton carrying mode + fighter selection across scenes.
    /// </summary>
    public class GameSettings : MonoBehaviour
    {
        public static GameSettings Instance { get; private set; }

        public GameMode    mode;
        public FighterData fighter1Data;
        public FighterData fighter2Data;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
