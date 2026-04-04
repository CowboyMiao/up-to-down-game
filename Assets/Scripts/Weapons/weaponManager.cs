using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 武器管理器：实例化 / 切换 / 播放武器动画 / 攻击判定碰撞体
/// </summary>
public class WeaponManager : MonoBehaviour
{
    [Header("武器挂载点")]
    [Tooltip("武器预制体实例化后会挂到该节点下")]
    public Transform weaponPoint;

    [Header("武器配置")]
    [Tooltip("所有可用武器数据，需与 WeaponInventory 一致")]
    public WeaponData[] allWeapons;

    [Header("当前状态")]
    [Tooltip("当前装备的武器类型")]
    public WeaponType currentWeaponType;

    public GameObject currentWeaponObj;
    private Dictionary<WeaponType, WeaponData> weaponDataMap;

    /// <summary>
    /// 每次开启攻击碰撞体时自增；同一挥击对每个敌人只结算一次，并配合 OnTriggerStay 解决已重叠时不触发 Enter 的问题。
    /// </summary>
    public int CurrentAttackSwingId { get; private set; }

    private void Awake()
    {
        InitWeaponDataMap();
    }

    private void Start()
    {
        EquipWeapon(WeaponType.Knife);
        Debug.Log("WeaponManager：已初始化默认小刀");
    }

    private void InitWeaponDataMap()
    {
        weaponDataMap = new Dictionary<WeaponType, WeaponData>();
        if (allWeapons == null || allWeapons.Length == 0)
        {
            Debug.LogError("WeaponManager：Inspector 中未配置 allWeapons！");
            return;
        }

        foreach (var weapon in allWeapons)
        {
            if (weapon == null)
            {
                Debug.LogWarning("WeaponManager：allWeapons 中存在空项");
                continue;
            }
            if (!weaponDataMap.ContainsKey(weapon.weaponType))
                weaponDataMap.Add(weapon.weaponType, weapon);
            else
                Debug.LogWarning($"WeaponManager：重复添加武器类型 {weapon.weaponType}，已忽略");
        }
    }

    public void EquipWeapon(WeaponType type)
    {
        if (currentWeaponType == type && currentWeaponObj != null) return;

        DestroyCurrentWeapon();

        if (!weaponDataMap.TryGetValue(type, out WeaponData targetWeapon))
        {
            Debug.LogError($"WeaponManager：未找到类型 {type} 的武器数据");
            currentWeaponType = WeaponType.None;
            return;
        }

        SpawnWeapon(targetWeapon);
        currentWeaponType = type;
        Debug.Log($"WeaponManager：已装备 {type}");
    }

    private void DestroyCurrentWeapon()
    {
        if (currentWeaponObj != null)
        {
            Destroy(currentWeaponObj);
            currentWeaponObj = null;
        }
    }

    private void SpawnWeapon(WeaponData weaponData)
    {
        if (weaponData == null || weaponData.weaponPrefab == null)
        {
            Debug.LogError($"WeaponManager：{weaponData?.weaponType} 武器预制体为空");
            return;
        }

        currentWeaponObj = Instantiate(weaponData.weaponPrefab, weaponPoint);
        currentWeaponObj.transform.localPosition = Vector3.zero;
        currentWeaponObj.transform.localRotation = Quaternion.identity;
        currentWeaponObj.transform.localScale = Vector3.one;

        EnsureAttackHitScanner(currentWeaponObj);
    }

    private static void EnsureAttackHitScanner(GameObject weaponRoot)
    {
        if (weaponRoot == null) return;
        Transform attackCheck = weaponRoot.transform.Find("AttackCheck");
        if (attackCheck == null) return;
        if (attackCheck.GetComponent<Collider2D>() == null) return;
        if (attackCheck.GetComponent<AttackColliderHitScanner2D>() == null)
            attackCheck.gameObject.AddComponent<AttackColliderHitScanner2D>();
    }

    public void SyncWeaponFlip(bool isFacingLeft)
    {
        if (currentWeaponObj == null) return;

        SpriteRenderer weaponSr = currentWeaponObj.GetComponent<SpriteRenderer>();
        if (weaponSr == null) return;

        weaponSr.flipX = isFacingLeft;
        weaponSr.sortingOrder = isFacingLeft ? 0 : 1;
    }

    public void PlayWeaponAttackAnimation()
    {
        if (currentWeaponObj == null)
        {
            Debug.LogError("WeaponManager：当前无武器实例");
            return;
        }

        Animator weaponAnim = currentWeaponObj.GetComponent<Animator>();
        if (weaponAnim == null)
        {
            Debug.LogError($"WeaponManager：{currentWeaponType} 预制体无 Animator");
            return;
        }

        if (!weaponDataMap.TryGetValue(currentWeaponType, out WeaponData data))
        {
            Debug.LogError($"WeaponManager：未找到 {currentWeaponType} 动画配置");
            return;
        }

        if (string.IsNullOrEmpty(data.attackAnimTrigger))
        {
            Debug.LogError($"WeaponManager：{currentWeaponType} 未配置 attackAnimTrigger");
            return;
        }

        weaponAnim.ResetTrigger(data.attackAnimTrigger);
        weaponAnim.SetTrigger(data.attackAnimTrigger);
    }

    public void EnableAttackCollider(bool isEnable)
    {
        if (currentWeaponObj == null) return;

        Transform attackCheck = currentWeaponObj.transform.Find("AttackCheck");
        if (attackCheck == null)
        {
            Debug.LogWarning($"WeaponManager：{currentWeaponType} 无 AttackCheck 子物体");
            return;
        }

        Collider2D col = attackCheck.GetComponent<Collider2D>();
        if (col != null)
        {
            if (isEnable)
                CurrentAttackSwingId++;

            col.enabled = isEnable;
        }
        else
            Debug.LogError("WeaponManager：AttackCheck 上无 Collider2D");
    }

    public WeaponData GetCurrentWeaponData()
    {
        weaponDataMap.TryGetValue(currentWeaponType, out var data);
        return data;
    }

    public void HideCurrentWeapon() => currentWeaponObj?.SetActive(false);
    public void ShowCurrentWeapon() => currentWeaponObj?.SetActive(true);
    public void HideAllWeapons() => HideCurrentWeapon();
}
