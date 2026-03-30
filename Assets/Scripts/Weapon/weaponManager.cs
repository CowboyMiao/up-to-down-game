using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 武器管理器 - 负责实例化/切换/播放武器动画
/// </summary>
public class WeaponManager : MonoBehaviour
{
    [Header("武器挂载点")]
    [Tooltip("武器预制体实例化后会挂载到该节点下")]
    public Transform weaponPoint;

    [Header("武器配置")]
    [Tooltip("所有可用的武器数据列表，需与WeaponInventory一致")]
    public WeaponData[] allWeapons;

    [Header("当前状态")]
    [Tooltip("当前装备的武器类型")]
    public WeaponType currentWeaponType;

    // 当前实例化的武器对象
    public GameObject currentWeaponObj;
    // 武器数据映射表（优化查找性能）
    private Dictionary<WeaponType, WeaponData> weaponDataMap;

    private void Awake()
    {
        InitWeaponDataMap();
    }

    private void Start()
    {
        // 强制初始化小刀（确保默认有武器）
        EquipWeapon(WeaponType.Knife);
        Debug.Log("WeaponManager已初始化默认小刀");
        Debug.Log("PlayerWeaponController Start: 订阅是否成功？");
    }

    /// <summary>
    /// 初始化武器数据映射表
    /// </summary>
    private void InitWeaponDataMap()
    {
        weaponDataMap = new Dictionary<WeaponType, WeaponData>();
        if (allWeapons == null || allWeapons.Length == 0)
        {
            Debug.LogError("WeaponManager：Inspector中未配置allWeapons武器数据！");
            return;
        }

        foreach (var weapon in allWeapons)
        {
            if (weapon == null)
            {
                Debug.LogWarning("WeaponManager：allWeapons列表中存在空的武器数据项！");
                continue;
            }
            if (!weaponDataMap.ContainsKey(weapon.weaponType))
            {
                weaponDataMap.Add(weapon.weaponType, weapon);
            }
            else
            {
                Debug.LogWarning($"WeaponManager：重复添加同类型武器{weapon.weaponType}，已忽略");
            }
        }
    }

    /// <summary>
    /// 装备指定类型的武器
    /// </summary>
    public void EquipWeapon(WeaponType type)
    {
        // 已装备该武器且对象存在，直接返回
        if (currentWeaponType == type && currentWeaponObj != null) return;

        // 销毁当前武器
        DestroyCurrentWeapon();

        // 查找目标武器数据
        if (!weaponDataMap.TryGetValue(type, out WeaponData targetWeapon))
        {
            Debug.LogError($"WeaponManager：未找到{type}类型的武器数据！");
            currentWeaponType = WeaponType.None;
            return;
        }

        // 实例化武器（核心缺失逻辑补全）
        SpawnWeapon(targetWeapon);
        currentWeaponType = type;
        Debug.Log($"WeaponManager：成功装备{type}武器");
    }

    /// <summary>
    /// 销毁当前武器
    /// </summary>
    private void DestroyCurrentWeapon()
    {
        if (currentWeaponObj != null)
        {
            Destroy(currentWeaponObj);
            currentWeaponObj = null;
        }
    }

    /// <summary>
    /// 实例化武器（补全缺失的核心逻辑）
    /// </summary>
    private void SpawnWeapon(WeaponData weaponData)
    {
        if (weaponData == null || weaponData.weaponPrefab == null)
        {
            Debug.LogError($"WeaponManager：{weaponData?.weaponType}武器的预制体为空！");
            return;
        }

        // 实例化武器到挂载点
        currentWeaponObj = Instantiate(weaponData.weaponPrefab, weaponPoint);
        // 重置武器的本地变换（避免预制体自带偏移）
        currentWeaponObj.transform.localPosition = Vector3.zero;
        currentWeaponObj.transform.localRotation = Quaternion.identity;
        currentWeaponObj.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 同步武器翻转（改用 flipX，只翻转渲染，不旋转物体）
    /// </summary>
    /// <summary>
    /// 面向左时：武器随人物翻转 + 层级放到人物下方（背后）
    /// </summary>
    public void SyncWeaponFlip(bool isFacingLeft)
    {
        if (currentWeaponObj == null) return;

        SpriteRenderer weaponSr = currentWeaponObj.GetComponent<SpriteRenderer>();
        if (weaponSr == null) return;

        // 1. 统一翻转 Sprite（Idle 和 Attack 都生效）
        weaponSr.flipX = isFacingLeft;

        // 2. 层级：面向左时武器在人物背后
        weaponSr.sortingOrder = isFacingLeft ? 0 : 1;  // 0 就是背后，调成 -1 或 -2 更靠后也可以

        Debug.Log($"武器翻转同步：面向左 = {isFacingLeft}，flipX = {weaponSr.flipX}，Order = {weaponSr.sortingOrder}");
    }

    /// <summary>
    /// 播放武器攻击动画
    /// </summary>
    public void PlayWeaponAttackAnimation()
    {
        if (currentWeaponObj == null)
        {
            Debug.LogError("WeaponManager：当前无武器实例，无法播放攻击动画！");
            return;
        }

        Animator weaponAnim = currentWeaponObj.GetComponent<Animator>();
        if (weaponAnim == null)
        {
            Debug.LogError($"WeaponManager：{currentWeaponType}武器预制体无Animator组件！");
            return;
        }

        if (!weaponDataMap.TryGetValue(currentWeaponType, out WeaponData data))
        {
            Debug.LogError($"WeaponManager：未找到{currentWeaponType}武器的动画配置！");
            return;
        }

        if (string.IsNullOrEmpty(data.attackAnimTrigger))
        {
            Debug.LogError($"WeaponManager：{currentWeaponType}武器未配置attackAnimTrigger！");
            return;
        }

        // 重置 + 触发统一 Trigger
        weaponAnim.ResetTrigger(data.attackAnimTrigger);
        weaponAnim.SetTrigger(data.attackAnimTrigger);

        Debug.Log($"WeaponManager：触发统一攻击 Trigger：{data.attackAnimTrigger}（方向由 Right 参数决定）");
    }

    /// <summary>
    /// 启用/关闭攻击碰撞体（增加空校验，避免空引用）
    /// </summary>
    public void EnableAttackCollider(bool isEnable)
    {
        if (currentWeaponObj == null) return;

        Transform attackCheck = currentWeaponObj.transform.Find("AttackCheck");
        if (attackCheck == null)
        {
            Debug.LogWarning($"WeaponManager：{currentWeaponType}武器无AttackCheck子物体！");
            return;
        }

        Collider2D col = attackCheck.GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = isEnable;
            Debug.Log($"武器碰撞体{(isEnable ? "启用" : "关闭")}");
        }
        else
        {
            Debug.LogError($"WeaponManager：AttackCheck子物体无Collider2D组件！");
        }
    }

    /// <summary>
    /// 获取当前武器数据
    /// </summary>
    public WeaponData GetCurrentWeaponData()
    {
        weaponDataMap.TryGetValue(currentWeaponType, out var data);
        return data;
    }

    // 辅助方法：隐藏/显示武器
    public void HideCurrentWeapon() => currentWeaponObj?.SetActive(false);
    public void ShowCurrentWeapon() => currentWeaponObj?.SetActive(true);
    public void HideAllWeapons() => HideCurrentWeapon();
}