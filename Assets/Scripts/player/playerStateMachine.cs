using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerState
{
    Idle,
    Move,
    Attack,
    Hurt,
    Dead
}

public class PlayerStateMachine : MonoBehaviour
{
    [Header("???????")]
    public Rigidbody2D rb;
    public SpriteRenderer sr;
    public Animator anim;
    public WeaponManager weaponManager;

    [Header("????")]
    public float moveSpeed = 5f;
    public float globalAttackCooldown = 0.5f;

    [Header("?????????????")]
    [Tooltip("????????? maxHealth / ???? / ?????????????????????????? maxHealth ?????")]
    public CombatStatsData combatStats;

    [Header("????????????")]
    public int maxHealth = 3;

    [Header("????????????")]
    public float hurtDuration = 0.15f;

    public PlayerState currentState;
    private float attackTimer;
    private Vector2 moveDirection;
    private bool isDead;
    private bool isAttacking;
    private float lastAttackTime; // ????????????????????
    private bool _attackButtonActive;
    private float hurtTimer;

    private Entity _entity;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction attackAction;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        weaponManager = GetComponent<WeaponManager>();
        playerInput = GetComponent<PlayerInput>();

        if (rb != null)
        {
            rb.gravityScale = 0;
            rb.freezeRotation = true;
            // ??? FixedUpdate???? LateUpdate??????????/???????????
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        if (playerInput != null)
        {
            moveAction = playerInput.actions["Move"];
            attackAction = playerInput.actions["Attack"];
        }

        _entity = GetComponent<Entity>();
        if (_entity == null)
            _entity = gameObject.AddComponent<Entity>();
    }

    void Start()
    {
        currentState = PlayerState.Idle;
        attackTimer = 0;
        isDead = false;
        isAttacking = false;
        if (combatStats != null)
        {
            maxHealth = combatStats.maxHealth;
            _entity.ConfigureFromCombatStats(combatStats);
        }
        else
        {
            _entity.Configure(maxHealth, 0f);
        }
        _attackButtonActive = false;
    }

    void Update()
    {
        if (isDead) return;

        attackTimer = Mathf.Max(0, attackTimer - Time.deltaTime);

        UpdateMoveDirection();
        UpdateState();
        FlipSprite();
    }

    void FixedUpdate()
    {
        if (isDead || currentState == PlayerState.Attack || currentState == PlayerState.Hurt)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        if (rb != null)
        {
            rb.velocity = Vector2.Lerp(rb.velocity, moveDirection.normalized * moveSpeed, Time.fixedDeltaTime * 10f);
        }
    }

    void UpdateState()
    {
        UpdateAttackButtonActive();

        if (currentState == PlayerState.Hurt)
        {
            hurtTimer -= Time.deltaTime;
            if (hurtTimer <= 0f)
            {
                currentState = moveDirection.magnitude > 0.1f ? PlayerState.Move : PlayerState.Idle;
            }
            return;
        }

        if (currentState == PlayerState.Attack)
        {
            if (attackTimer <= 0)
            {
                currentState = moveDirection.magnitude > 0.1f ? PlayerState.Move : PlayerState.Idle;
            }
            return;
        }

        if (moveDirection.magnitude > 0.1f)
        {
            currentState = PlayerState.Move;
        }
        else
        {
            currentState = PlayerState.Idle;
        }

        // ???????????????????????????????????
        bool attackHeld = _attackButtonActive;

        if (attackTimer <= 0 && attackHeld && !isAttacking && Time.time - lastAttackTime > 0.05f)
        {
            lastAttackTime = Time.time;
            isAttacking = true;
            currentState = PlayerState.Attack;
            attackTimer = globalAttackCooldown;
            OnAttack();
            Debug.Log("??????????????????");
        }
    }

    private void UpdateAttackButtonActive()
    {
        // InputSystem ?????? started/canceled ?????????????????????????
        if (attackAction != null) return;

        if (Input.GetMouseButtonDown(0))
            _attackButtonActive = true;
        if (Input.GetMouseButtonUp(0))
            _attackButtonActive = false;
    }

    private void OnEnable()
    {
        if (attackAction == null) return;
        attackAction.started += HandleAttackStarted;
        attackAction.canceled += HandleAttackCanceled;
    }

    private void OnDisable()
    {
        if (attackAction == null) return;
        attackAction.started -= HandleAttackStarted;
        attackAction.canceled -= HandleAttackCanceled;
    }

    private void HandleAttackStarted(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        _attackButtonActive = true;
    }

    private void HandleAttackCanceled(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        _attackButtonActive = false;
    }

    void OnAttack()
    {
        if (weaponManager == null) return;

        weaponManager.PlayWeaponAttackAnimation();
        weaponManager.EnableAttackCollider(true);
        Invoke(nameof(DisableAttackCollider), 0.2f);
    }

    void DisableAttackCollider()
    {
        weaponManager?.EnableAttackCollider(false);
        isAttacking = false;
    }

    void UpdateMoveDirection()
    {
        moveDirection = moveAction?.ReadValue<Vector2>() ?? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    void FlipSprite()
    {
        if (sr == null) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        bool facingRight = mousePos.x >= transform.position.x;

        sr.flipX = !facingRight;

        if (weaponManager?.currentWeaponObj != null)
        {
            var weaponSr = weaponManager.currentWeaponObj.GetComponent<SpriteRenderer>();
            if (weaponSr != null)
            {
                weaponSr.flipX = !facingRight;
                weaponSr.sortingOrder = facingRight ? 1 : 0;
            }

            var weaponAnim = weaponManager.currentWeaponObj.GetComponent<Animator>();
            if (weaponAnim != null)
            {
                weaponAnim.SetBool("Right", facingRight);
            }
        }
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        rb.velocity = Vector2.zero;
        rb.simulated = false;
        currentState = PlayerState.Dead;
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        if (_entity != null && _entity.IsDead) return;
        if (damage <= 0) return;
        // ?????????????? Stay ????/????????
        if (currentState == PlayerState.Hurt)
            return;

        int final = _entity.ApplyDamage(damage);
        if (final <= 0) return;

        Debug.Log($"{nameof(PlayerStateMachine)}: Took {final} damage (raw {damage}), remaining {_entity.CurrentHealth}");

        if (_entity.IsDead)
        {
            Die();
            return;
        }

        // Enter Hurt: stop movement/attack for a short duration.
        currentState = PlayerState.Hurt;
        hurtTimer = hurtDuration;
        attackTimer = 0f;
        isAttacking = false;

        if (weaponManager != null) weaponManager.EnableAttackCollider(false);
        if (sr != null) sr.color = Color.red;

        StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        yield return new WaitForSeconds(hurtDuration);
        if (isDead) yield break;
        if (sr != null) sr.color = Color.white;
    }

    public void ResetAttackTimer()
    {
        attackTimer = 0;
    }

    /// <summary>
    /// ????????????????????? CombatStatsData ?????????????????????????????????
    /// </summary>
    public int GetOutgoingDamageFromWeapon(int weaponBaseDamage, out bool isCrit)
    {
        return DamageCalculator.ComputeOutgoingDamage(weaponBaseDamage, combatStats, out isCrit);
    }

    /// <summary>??????? Entity?</summary>
    public int CurrentHealth => _entity != null ? _entity.CurrentHealth : 0;

    /// <summary>????</summary>
    public int MaxHealth => _entity != null ? _entity.MaxHealth : maxHealth;

    /// <summary>??</summary>
    public float Defense => _entity != null ? _entity.Defense : 0f;

    /// <summary>?????????/????</summary>
    public Entity Entity => _entity;

    /// <summary>?????? 0~1</summary>
    public float CritChance => combatStats != null ? combatStats.critChance : 0f;
}