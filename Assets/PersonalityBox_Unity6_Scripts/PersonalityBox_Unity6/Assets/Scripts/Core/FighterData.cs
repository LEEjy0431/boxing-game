using UnityEngine;

namespace PersonalityBox.Core
{
    public enum FighterType  { HumanMachine, MachineHuman }
    public enum PunchType    { Jab, Hook, Uppercut }
    public enum PunchHeight  { High, Mid, Low }
    public enum FighterState { Idle, Moving, Attacking, Blocking, Dodging, Awakened, KnockedDown, KO }

    [CreateAssetMenu(fileName = "FighterData", menuName = "PersonalityBox/Fighter Data")]
    public class FighterData : ScriptableObject
    {
        [Header("Identity")]
        public string fighterName;
        public FighterType fighterType;
        [TextArea(3, 6)]
        public string backstory;

        [Header("Base Stats")]
        public float maxHealth      = 100f;
        public float maxStamina     = 100f;
        public float staminaRegen   = 8f;   // per second

        [Header("Punch Stats")]
        public float jabDamage      = 7f;
        public float hookDamage     = 14f;
        public float upperDamage    = 18f;
        public float jabStamCost    = 6f;
        public float hookStamCost   = 12f;
        public float upperStamCost  = 16f;

        [Header("Defence Stats")]
        public float blockDamageReduction = 0.65f;  // 65 % reduction
        public float dodgeCooldown        = 0.8f;

        [Header("Awakening")]
        public float awakenThreshold      = 0.30f;  // triggers at 30 % HP
        public float awakenDamageBonus    = 1.40f;  // ×1.4 damage
        public bool  awakenRestoresStam   = true;

        [Header("Special Move")]
        public float specialDamage        = 35f;
        public float specialStamCost      = 40f;
        public string specialMoveName;
    }
}
