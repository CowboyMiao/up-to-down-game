using UnityEngine;

/// <summary>
/// 通用战斗属性数据（玩家/敌人等可在 Inspector 里引用同一份或各自一份）。
/// 在 Project 右键 Create -> GameData -> Combat Stats
/// </summary>
[CreateAssetMenu(fileName = "CombatStats", menuName = "GameData/Combat Stats", order = 0)]
public class CombatStatsData : ScriptableObject
{
    [Header("生命")]
    [Min(1)] public int maxHealth = 3;

    [Header("防御")]
    [Tooltip("每次受击时，先减去该数值（再至少扣 1 点血）。可与 DamageCalculator.ApplyDefense 配合使用。")]
    [Min(0f)]
    public float defense = 0f;

    [Header("暴击（进攻方）")]
    [Tooltip("0~1，例如 0.15 表示 15% 暴击率")]
    [Range(0f, 1f)]
    public float critChance = 0.05f;

    [Tooltip("暴击时伤害倍率（例如 1.5 = 150%）")]
    [Min(1f)]
    public float critDamageMultiplier = 1.5f;
}
