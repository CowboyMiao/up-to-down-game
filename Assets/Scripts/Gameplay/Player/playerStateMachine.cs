using System.Collections;
using UnityEngine;
using UnityEngine.Events;
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

    [Header("战斗规则")]
    [Tooltip("为 true 时攻击中仍可移动（俯视角肉鸽常用，避免被怪贴脸时无法走位）。若需要站桩输出可关掉。")]
    public bool allowMoveDuringAttack = true;

    [Tooltip("进入受击硬直时沿「远离伤害来源」方向的击退初速度（0 则关闭击退）")]
    public float hurtKnockbackSpeed = 5.5f;

    [Header("与敌人重叠")]
    [Tooltip("与敌人中心小于此距离时，去掉速度中「指向敌人」的分量，避免与怪对向移动时顶进同一位置卡住。应与 EnemyData.minSeparationFromPlayer 一致。")]
    [Min(0.05f)]
    public float playerEnemyMinSeparation = 0.55f;

    [Header("死亡")]
    [Tooltip("生命归零时触发（与第三次受击「卡死」实为同一时刻：Die 会关闭刚体模拟并停用输入）。可在此绑定打开死亡 UI、播放动画或重新加载场景。")]
    public UnityEvent onPlayerDied;

    public PlayerState currentState;
    private float attackTimer;
    private Vector2 moveDirection;
    private bool isDead;
    private bool isAttacking;
    private float lastAttackTime; // ????????????????????
    private bool _attackButtonActive;
    private float hurtTimer;
    private Vector2 _hurtKnockbackVelocity;
    private readonly Collider2D[] _enemyOverlapBuffer = new Collider2D[12];

    private Entity _entity;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction attackAction;
    private Camera _facingCamera;

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
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
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
    }

    private void LateUpdate()
    {
        if (isDead)
            return;
        FlipSprite();
    }

    void FixedUpdate()
    {
        if (isDead)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        if (currentState == PlayerState.Hurt)
        {
            if (rb != null)
            {
                if (_hurtKnockbackVelocity.sqrMagnitude > 0.01f)
                {
                    rb.velocity = _hurtKnockbackVelocity;
                    _hurtKnockbackVelocity =
                        Vector2.Lerp(_hurtKnockbackVelocity, Vector2.zero, Time.fixedDeltaTime * 14f);
                }
                else
                {
                    rb.velocity = Vector2.zero;
                }
            }
            return;
        }

        // 攻击中默认锁移动；子类可重写 CanMoveDuringAttack 做例外（如蓄力移动斩）
        if (currentState == PlayerState.Attack && !CanMoveDuringAttack())
        {
            if (rb != null)
            {
                if (_hurtKnockbackVelocity.sqrMagnitude > 0.01f)
                {
                    rb.velocity = _hurtKnockbackVelocity;
                    _hurtKnockbackVelocity =
                        Vector2.Lerp(_hurtKnockbackVelocity, Vector2.zero, Time.fixedDeltaTime * 14f);
                }
                else
                {
                    rb.velocity = Vector2.zero;
                }
            }
            return;
        }

        if (rb != null)
        {
            if (moveDirection.sqrMagnitude > 0.01f)
                rb.WakeUp();

            Vector2 targetVel = moveDirection.normalized * moveSpeed;
            if (_hurtKnockbackVelocity.sqrMagnitude > 0.01f)
            {
                targetVel += _hurtKnockbackVelocity;
                _hurtKnockbackVelocity =
                    Vector2.Lerp(_hurtKnockbackVelocity, Vector2.zero, Time.fixedDeltaTime * 14f);
            }
            rb.velocity = Vector2.Lerp(rb.velocity, targetVel, Time.fixedDeltaTime * 10f);
            ApplyEnemyProximityVelocityClamp();
        }
    }

    /// <summary>
    /// 只对「最近」且距离小于 minSep 的敌人去掉指向它的速度分量。多敌人时若逐个去分量会把合速度减成 0，导致卡住。
    /// </summary>
    private void ApplyEnemyProximityVelocityClamp()
    {
        if (rb == null)
            return;

        float probe = Mathf.Max(0.5f, playerEnemyMinSeparation * 2f);
        int mask = LayerMask.GetMask("Enemy");
        int count = mask != 0
            ? Physics2D.OverlapCircleNonAlloc(rb.position, probe, _enemyOverlapBuffer, mask)
            : Physics2D.OverlapCircleNonAlloc(rb.position, probe, _enemyOverlapBuffer);

        float minSep = Mathf.Max(0.05f, playerEnemyMinSeparation);

        float bestD = float.MaxValue;
        Vector2 bestTowardEnemy = default;
        var found = false;

        for (int i = 0; i < count; i++)
        {
            var col = _enemyOverlapBuffer[i];
            if (col == null)
                continue;
            var ec = col.GetComponentInParent<EnemyController>();
            if (ec == null)
                continue;

            Vector2 toEnemy = (Vector2)ec.transform.position - rb.position;
            float d = toEnemy.magnitude;
            if (d < 1e-4f || d >= minSep)
                continue;

            if (d < bestD)
            {
                bestD = d;
                bestTowardEnemy = toEnemy / d;
                found = true;
            }
        }

        if (!found)
            return;

        Vector2 v = rb.velocity;
        float inward = Vector2.Dot(v, bestTowardEnemy);
        if (inward > 0f)
            rb.velocity = v - bestTowardEnemy * inward;
    }

    /// <summary>攻击中是否允许移动（默认 false）。子类可重写以实现特殊武器/ Buff。</summary>
    protected virtual bool CanMoveDuringAttack() => allowMoveDuringAttack;

    void UpdateState()
    {
        UpdateAttackButtonActive();

        if (currentState == PlayerState.Dead)
            return;

        bool attackHeld = _attackButtonActive;

        // 必须在 Hurt 分支之前处理：否则整个 hurtDuration 内无法起手，贴怪连续受击会「无法攻击」
        if (attackTimer <= 0f && attackHeld && !isAttacking && Time.time - lastAttackTime > 0.05f)
        {
            lastAttackTime = Time.time;
            isAttacking = true;
            currentState = PlayerState.Attack;
            attackTimer = globalAttackCooldown;
            hurtTimer = 0f;
            OnAttack();
            return;
        }

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
            if (attackTimer <= 0f)
            {
                isAttacking = false;
                currentState = moveDirection.magnitude > 0.1f ? PlayerState.Move : PlayerState.Idle;
            }
            return;
        }

        if (moveDirection.magnitude > 0.1f)
            currentState = PlayerState.Move;
        else
            currentState = PlayerState.Idle;
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
        CancelInvoke(nameof(DisableAttackCollider));
        float hitDuration = 0.92f;
        var wd = weaponManager.GetCurrentWeaponData();
        if (wd != null && wd.attackDuration > 0f)
            hitDuration = wd.attackDuration;
        Invoke(nameof(DisableAttackCollider), hitDuration);
    }

    void DisableAttackCollider()
    {
        weaponManager?.EnableAttackCollider(false);
    }

    void UpdateMoveDirection()
    {
        moveDirection = moveAction?.ReadValue<Vector2>() ?? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    void FlipSprite()
    {
        if (sr == null) return;

        var cam = GetFacingCamera();
        if (cam == null)
            return;

        Vector3 mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
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

    private Camera GetFacingCamera()
    {
        if (_facingCamera != null && _facingCamera.isActiveAndEnabled)
            return _facingCamera;
        _facingCamera = Camera.main;
        if (_facingCamera == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("MainCamera");
            if (tagged != null)
                _facingCamera = tagged.GetComponent<Camera>();
        }
        if (_facingCamera == null)
            _facingCamera = Object.FindObjectOfType<Camera>();
        return _facingCamera;
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        Debug.Log("[PlayerStateMachine] 玩家已死亡（生命归零）。表现为无法移动、无输入：这是 Die() 的设计，不是物理卡死。默认生命为 3 时第三次受伤即死亡。可在 Entity / CombatStatsData 提高 maxHealth，或在 onPlayerDied 中接 UI/复活。");
        LockInputCompletely();
        CancelInvoke(nameof(DisableAttackCollider));
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.simulated = false;
        }
        currentState = PlayerState.Dead;
        weaponManager?.EnableAttackCollider(false);
        isAttacking = false;
        onPlayerDied?.Invoke();
    }

    private void LockInputCompletely()
    {
        _attackButtonActive = false;
        moveDirection = Vector2.zero;
        if (playerInput != null)
            playerInput.DeactivateInput();
    }

    public void TakeDamage(int damage, Vector2? damageSourceWorld = null)
    {
        if (isDead) return;
        if (_entity != null && _entity.IsDead) return;
        if (damage <= 0) return;
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

        if (rb != null)
            rb.WakeUp();

        Vector2 knockDir = Vector2.zero;
        if (damageSourceWorld.HasValue && hurtKnockbackSpeed > 0f)
        {
            Vector2 delta = (Vector2)transform.position - damageSourceWorld.Value;
            if (delta.sqrMagnitude > 1e-6f)
                knockDir = delta.normalized;
        }

        // 受击不打断攻击：攻击中只扣血与短暂闪红，不进 Hurt、不关判定、不清 attackTimer
        if (currentState == PlayerState.Attack)
        {
            if (knockDir.sqrMagnitude > 0.01f)
                _hurtKnockbackVelocity = knockDir * (hurtKnockbackSpeed * 0.45f);
            StartCoroutine(BriefHitFlashDuringAttackRoutine());
            return;
        }

        currentState = PlayerState.Hurt;
        hurtTimer = hurtDuration;
        attackTimer = 0f;
        isAttacking = false;

        _hurtKnockbackVelocity = knockDir * hurtKnockbackSpeed;

        if (weaponManager != null) weaponManager.EnableAttackCollider(false);
        if (sr != null) sr.color = Color.red;

        StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator BriefHitFlashDuringAttackRoutine()
    {
        if (sr == null) yield break;
        Color prev = sr.color;
        sr.color = Color.red;
        yield return new WaitForSeconds(0.08f);
        if (!isDead && currentState == PlayerState.Attack && sr != null)
            sr.color = prev;
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