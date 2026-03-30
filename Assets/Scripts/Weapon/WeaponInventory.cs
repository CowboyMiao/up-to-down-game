using System;
using UnityEngine;

/// <summary>
/// 双武器槽库存管理（单例），负责武器数据存储与变更通知
/// </summary>
public class WeaponInventory : MonoBehaviour
{
    // 单例实例（全局唯一）
    public static WeaponInventory Instance { get; private set; }

    // 武器槽定义（主手/副手）
    public enum WeaponSlot { Primary, Secondary }

    [Header("初始武器配置")]
    [Tooltip("主手初始武器")]
    public WeaponData primaryInitWeapon;
    [Tooltip("副手初始武器")]
    public WeaponData secondaryInitWeapon;

    // 武器槽数据（私有，仅通过方法访问）
    private WeaponData[] weaponSlots = new WeaponData[2];
    // 当前激活的武器槽
    private WeaponSlot _activeSlot = WeaponSlot.Primary;

    // 武器切换完成事件（参数：新武器数据、激活槽位）
    public event Action<WeaponData, WeaponSlot> OnWeaponSwitched;
    // 武器槽变更事件（参数：槽位、新武器数据）
    public event Action<WeaponSlot, WeaponData> OnWeaponSlotUpdated;

    private void Awake()
    {
        // 单例初始化（防重复）
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 初始化武器槽（使用Inspector配置的初始武器）
        InitWeaponSlots(primaryInitWeapon, secondaryInitWeapon);
    }

    /// <summary>
    /// 获取当前激活的武器数据
    /// </summary>
    public WeaponData GetActiveWeapon()
    {
        return weaponSlots[(int)_activeSlot];
    }

    /// <summary>
    /// 获取指定槽位的武器数据
    /// </summary>
    public WeaponData GetWeaponInSlot(WeaponSlot slot)
    {
        return weaponSlots[(int)slot];
    }

    /// <summary>
    /// 切换激活的武器槽
    /// </summary>
    public void SwitchActiveSlot(WeaponSlot targetSlot)
    {
        // 避免切换空武器槽/重复切换
        if (_activeSlot == targetSlot || weaponSlots[(int)targetSlot] == null)
        {
            Debug.LogWarning($"WeaponInventory：无法切换到{targetSlot}槽（空槽/重复切换）");
            return;
        }

        _activeSlot = targetSlot;
        OnWeaponSwitched?.Invoke(GetActiveWeapon(), targetSlot);
        Debug.Log($"WeaponInventory：切换到{targetSlot}槽，武器：{GetActiveWeapon().weaponType}");
    }

    /// <summary>
    /// 切换到下一个武器槽（滚轮切换用）
    /// </summary>
    public void SwitchToNextSlot()
    {
        var nextSlot = _activeSlot == WeaponSlot.Primary ? WeaponSlot.Secondary : WeaponSlot.Primary;
        SwitchActiveSlot(nextSlot);
    }

    /// <summary>
    /// 【预留接口】拾取武器并替换指定槽位（地牢掉落用）
    /// </summary>
    /// <param name="newWeapon">新拾取的武器数据</param>
    /// <param name="targetSlot">要替换的槽位（null则替换当前激活槽）</param>
    public void PickupAndReplaceWeapon(WeaponData newWeapon, WeaponSlot? targetSlot = null)
    {
        if (newWeapon == null)
        {
            Debug.LogWarning("WeaponInventory：拾取的武器数据为空！");
            return;
        }

        var slotToReplace = targetSlot ?? _activeSlot;
        weaponSlots[(int)slotToReplace] = newWeapon;
        OnWeaponSlotUpdated?.Invoke(slotToReplace, newWeapon);
        Debug.Log($"WeaponInventory：{slotToReplace}槽替换为武器：{newWeapon.weaponType}");

        // 如果替换的是当前激活槽，触发切换事件
        if (slotToReplace == _activeSlot)
        {
            OnWeaponSwitched?.Invoke(newWeapon, slotToReplace);
        }
    }

    /// <summary>
    /// 初始化武器槽（游戏开始时配置初始武器）
    /// </summary>
    public void InitWeaponSlots(WeaponData primary, WeaponData secondary)
    {
        weaponSlots[(int)WeaponSlot.Primary] = primary;
        weaponSlots[(int)WeaponSlot.Secondary] = secondary;

        // 确保初始激活槽有武器
        if (primary != null)
        {
            OnWeaponSwitched?.Invoke(primary, WeaponSlot.Primary);
        }
        else if (secondary != null)
        {
            _activeSlot = WeaponSlot.Secondary;
            OnWeaponSwitched?.Invoke(secondary, WeaponSlot.Secondary);
        }
        else
        {
            Debug.LogWarning("WeaponInventory：初始武器槽全为空！");
        }
    }
}