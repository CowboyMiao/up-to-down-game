using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponInputHandler : MonoBehaviour
{
    [SerializeField] private InputActionReference scrollWheel;
    [SerializeField] private InputActionReference switchPrimary;
    [SerializeField] private InputActionReference switchSecondary;

    [SerializeField, Range(0.01f, 0.5f)] private float scrollDeadZone = 0.05f;
    [SerializeField, Range(0.05f, 1f)] private float scrollAccumulateThreshold = 0.1f; // 降低阈值
    [SerializeField] private float inputCooldown = 0.15f;

    public event Action<WeaponInventory.WeaponSlot> OnWeaponSwitchRequest;
    public event Action OnScrollSwitchRequest;

    private float _scrollAccumulator;
    private float _lastInputTime;

    private void OnEnable()
    {
        if (scrollWheel?.action != null) scrollWheel.action.performed += OnScrollPerformed;
        if (switchPrimary?.action != null) switchPrimary.action.performed += _ => OnSwitchPrimary();
        if (switchSecondary?.action != null) switchSecondary.action.performed += _ => OnSwitchSecondary();

        // 强制启用 Action（重要！）
        scrollWheel?.action.Enable();
        switchPrimary?.action.Enable();
        switchSecondary?.action.Enable();

        Debug.Log("WeaponInputHandler: Actions 已启用");
    }

    private void OnDisable()
    {
        if (scrollWheel?.action != null) scrollWheel.action.performed -= OnScrollPerformed;
        if (switchPrimary?.action != null) switchPrimary.action.performed -= _ => OnSwitchPrimary();
        if (switchSecondary?.action != null) switchSecondary.action.performed -= _ => OnSwitchSecondary();

        scrollWheel?.action.Disable();
        switchPrimary?.action.Disable();
        switchSecondary?.action.Disable();
    }

    private void OnScrollPerformed(InputAction.CallbackContext ctx)
    {
        Debug.Log("滚轮事件触发！值: " + ctx.ReadValue<Vector2>().y);

        if (Time.time - _lastInputTime < inputCooldown) return;

        var scrollValue = ctx.ReadValue<Vector2>().y;
        if (Mathf.Abs(scrollValue) < scrollDeadZone) return;

        if (Mathf.Sign(scrollValue) == Mathf.Sign(_scrollAccumulator) || _scrollAccumulator == 0)
        {
            _scrollAccumulator += scrollValue;
        }
        else
        {
            _scrollAccumulator = scrollValue;
        }

        if (Mathf.Abs(_scrollAccumulator) >= scrollAccumulateThreshold)
        {
            Debug.Log("滚轮达到阈值，触发切换！");
            OnScrollSwitchRequest?.Invoke();
            _scrollAccumulator = 0;
            _lastInputTime = Time.time;
        }
    }

    private void OnSwitchPrimary()
    {
        Debug.Log("键盘1触发！请求切换主手");
        if (Time.time - _lastInputTime < inputCooldown) return;
        OnWeaponSwitchRequest?.Invoke(WeaponInventory.WeaponSlot.Primary);
        _lastInputTime = Time.time;
    }

    private void OnSwitchSecondary()
    {
        Debug.Log("键盘2触发！请求切换副手");
        if (Time.time - _lastInputTime < inputCooldown) return;
        OnWeaponSwitchRequest?.Invoke(WeaponInventory.WeaponSlot.Secondary);
        _lastInputTime = Time.time;
    }
}