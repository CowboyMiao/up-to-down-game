using UnityEngine;

/// <summary>
/// 专门处理刀的动画事件（攻击判定/音效等）
/// </summary>
public class KnifeAnimEvents : MonoBehaviour
{
    // 引用玩家身上的武器管理器
    private WeaponManager weaponManager;

    void Awake()
    {
        // 自动查找父物体中的武器管理器
        weaponManager = GetComponentInParent<WeaponManager>();
    }

    /// <summary>
    /// 动画事件：开启/关闭攻击判定
    /// </summary>
    /// <param name="isEnable">true开启 false关闭</param>
    public void EnableAttack(bool isEnable)
    {
        weaponManager?.EnableAttackCollider(isEnable);
    }
}