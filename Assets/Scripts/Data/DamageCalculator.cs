using UnityEngine;

/// <summary>
/// 伤害与暴击的纯计算（无状态），供玩家/敌人/武器调用。
/// </summary>
public static class DamageCalculator
{
    /// <summary>
    /// 受击：原始伤害经防御减免，至少为 1（可按项目改为允许 0）。
    /// </summary>
    public static int ApplyDefense(int rawDamage, float defense)
    {
        if (rawDamage <= 0) return 0;
        int mitigated = Mathf.RoundToInt(rawDamage - defense);
        return Mathf.Max(1, mitigated);
    }

    /// <summary>
    /// 是否触发暴击（0~1 暴击率）。
    /// </summary>
    public static bool RollCrit(float critChance01)
    {
        return Random.value < Mathf.Clamp01(critChance01);
    }

    /// <summary>
    /// 进攻：基础伤害 ×（暴击则乘倍率）。返回最终整数伤害。
    /// </summary>
    public static int ComputeOutgoingDamage(int baseDamage, CombatStatsData stats, out bool isCrit)
    {
        isCrit = false;
        if (baseDamage <= 0) return 0;
        if (stats == null)
            return baseDamage;

        isCrit = RollCrit(stats.critChance);
        float mult = isCrit ? stats.critDamageMultiplier : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * mult));
    }

    /// <summary>
    /// 仅根据是否暴击计算倍率后的伤害（不掷骰，由外部决定 isCrit）。
    /// </summary>
    public static int ApplyCritMultiplier(int baseDamage, bool isCrit, float critMultiplier)
    {
        if (baseDamage <= 0) return 0;
        float mult = isCrit ? Mathf.Max(1f, critMultiplier) : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * mult));
    }
}
