using UnityEngine;

/// <summary>
/// 武器类型枚举
/// </summary>
public enum WeaponType
{
    None,
    Knife,
    Guai
}

/// <summary>
/// 武器数据（可在Project面板创建配置文件）
/// </summary>
[CreateAssetMenu(fileName = "WeaponData", menuName = "GameData/WeaponData", order = 1)]
public class WeaponData : ScriptableObject
{
    [Header("基础配置")]
    public WeaponType weaponType;
    public GameObject weaponPrefab;
    [Tooltip("武器Animator的攻击Trigger名称（必须与动画控制器一致）")]
    public string attackAnimTrigger = "Attack"; // 默认值，避免空字符串

    [Header("战斗参数")]
    public int attackDamage = 1;
    public float attackCooldown = 0.5f;
    [Tooltip("攻击判定持续时间（碰撞体开启时长）")]
    public float attackDuration = 0.2f; // 新增：攻击判定时长，默认0.2秒
}