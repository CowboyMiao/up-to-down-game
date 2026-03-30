using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWeaponController : MonoBehaviour
{
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private WeaponManager weaponManager;

    [SerializeField] private float switchDuration = 0.3f;
    public bool isSwitching;
    private float _switchTimer;

    private void Awake()
    {
        stateMachine ??= GetComponent<PlayerStateMachine>();
        weaponManager ??= GetComponent<WeaponManager>();
    }

    private void Start()
    {
        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.SwitchCurrentActionMap("player");
            Debug.Log("Input Map: " + playerInput.currentActionMap?.name);
        }

        var inputHandler = FindFirstObjectByType<WeaponInputHandler>();
        if (inputHandler != null)
        {
            inputHandler.OnWeaponSwitchRequest += HandleSwitchRequest;
            inputHandler.OnScrollSwitchRequest += HandleScrollSwitch;
        }

        if (WeaponInventory.Instance != null)
        {
            WeaponInventory.Instance.OnWeaponSwitched += HandleWeaponSwitched;
        }

        if (WeaponInventory.Instance != null)
        {
            var initialWeapon = WeaponInventory.Instance.GetActiveWeapon();
            if (initialWeapon != null)
            {
                weaponManager.EquipWeapon(initialWeapon.weaponType);
            }
        }
    }

    private void Update()
    {
        if (isSwitching)
        {
            _switchTimer -= Time.deltaTime;
            if (_switchTimer <= 0)
            {
                isSwitching = false;
                weaponManager.ShowCurrentWeapon();

                if (stateMachine != null && stateMachine.rb != null)
                {
                    stateMachine.rb.velocity = Vector2.zero;
                }
            }
        }
    }

    private void HandleSwitchRequest(WeaponInventory.WeaponSlot targetSlot)
    {
        if (isSwitching || WeaponInventory.Instance == null) return;

        isSwitching = true;
        _switchTimer = switchDuration;
        WeaponInventory.Instance.SwitchActiveSlot(targetSlot);
    }

    private void HandleScrollSwitch()
    {
        if (isSwitching || WeaponInventory.Instance == null) return;

        isSwitching = true;
        _switchTimer = switchDuration;
        WeaponInventory.Instance.SwitchToNextSlot();
    }

    private void HandleWeaponSwitched(WeaponData newWeapon, WeaponInventory.WeaponSlot activeSlot)
    {
        if (newWeapon == null)
        {
            isSwitching = false;
            return;
        }

        weaponManager.HideCurrentWeapon();
        weaponManager.EquipWeapon(newWeapon.weaponType);
    }

    private void OnDestroy()
    {
        var inputHandler = FindFirstObjectByType<WeaponInputHandler>();
        if (inputHandler != null)
        {
            inputHandler.OnWeaponSwitchRequest -= HandleSwitchRequest;
            inputHandler.OnScrollSwitchRequest -= HandleScrollSwitch;
        }
        if (WeaponInventory.Instance != null)
        {
            WeaponInventory.Instance.OnWeaponSwitched -= HandleWeaponSwitched;
        }
    }
}