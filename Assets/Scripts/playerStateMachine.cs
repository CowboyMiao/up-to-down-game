using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerState
{
    Idle,
    Move,
    Attack,
    Dead
}

public class PlayerStateMachine : MonoBehaviour
{
    [Header("核心组件")]
    public Rigidbody2D rb;
    public SpriteRenderer sr;
    public Animator anim;
    public WeaponManager weaponManager;

    [Header("参数")]
    public float moveSpeed = 5f;
    public float globalAttackCooldown = 0.5f;

    public PlayerState currentState;
    private float attackTimer;
    private Vector2 moveDirection;
    private bool isDead;
    private bool isAttacking;
    private float lastAttackTime; // 新增：防止松开时重复触发

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
        }

        if (playerInput != null)
        {
            moveAction = playerInput.actions["Move"];
            attackAction = playerInput.actions["Attack"];
        }
    }

    void Start()
    {
        currentState = PlayerState.Idle;
        attackTimer = 0;
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
        if (isDead || currentState == PlayerState.Attack)
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

        // 严格只在按下瞬间触发一次（彻底封死松开触发）
        if (attackAction != null)
        {
            if (attackTimer <= 0 && attackAction.WasPressedThisFrame() && !isAttacking && Time.time - lastAttackTime > 0.05f)
            {
                lastAttackTime = Time.time;
                isAttacking = true;
                currentState = PlayerState.Attack;
                attackTimer = globalAttackCooldown;
                OnAttack();
                Debug.Log("攻击触发 - 只一次（已防松开）");
            }
        }
        else
        {
            if (attackTimer <= 0 && Input.GetMouseButtonDown(0) && !isAttacking && Time.time - lastAttackTime > 0.05f)
            {
                lastAttackTime = Time.time;
                isAttacking = true;
                currentState = PlayerState.Attack;
                attackTimer = globalAttackCooldown;
                OnAttack();
                Debug.Log("攻击触发 - 只一次（旧输入）");
            }
        }
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

    public void ResetAttackTimer()
    {
        attackTimer = 0;
    }
}