using System.Collections;
using UnityEngine;

public enum EnemyState
{
    Idle,
    Chase,
    Hurt,
    Dead
}

/// <summary>
/// Top-down 2D enemy state machine: idle/chase/hurt/dead.
/// </summary>
public class EnemyStateMachine : MonoBehaviour
{
    [Header("Data")]
    public EnemyData enemyData;
    public Transform player;

    [Header("Tuning")]
    [Tooltip("受击硬直时长（秒）。若 EnemyData.hitStunDuration > 0 则以数据为准。")]
    public float hurtDuration = 0.15f;
    public float dieDelay = 1f;

    [Header("Debug")]
    public EnemyState currentState = EnemyState.Idle;

    private Rigidbody2D _rb;
    private SpriteRenderer _sr;
    private Entity _entity;
    private float _hurtTimer;
    private Coroutine _hitFlashRoutine;

    private float EffectiveHurtDuration =>
        enemyData != null && enemyData.hitStunDuration > 0f ? enemyData.hitStunDuration : hurtDuration;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();

        // Fallback: if EnemyController has the references, pull them here.
        if (enemyData == null || player == null)
        {
            var controller = GetComponent<EnemyController>();
            if (controller != null)
            {
                if (enemyData == null) enemyData = controller.enemyData;
                if (player == null) player = controller.player;
            }
        }

        _entity = GetComponent<Entity>();
        if (_entity == null)
            _entity = gameObject.AddComponent<Entity>();

        // Dynamic + velocity 追逐会与玩家的 Dynamic 实体碰撞在多次受击/击退后产生不稳定挤压（像「突破」后卡死）。
        // Kinematic + MovePosition：不参与动态刚体间求解，仍可与 Trigger 重叠做伤害判定；地图墙若需挡怪请用非 Trigger 碰撞体。
        if (_rb != null)
        {
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.gravityScale = 0f;
            _rb.velocity = Vector2.zero;
        }
    }

    private void Start()
    {
        if (enemyData != null)
            _entity.ConfigureFromEnemyData(enemyData);
        else
            _entity.Configure(1, 0f);

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
    }

    private void Update()
    {
        if (currentState == EnemyState.Dead) return;
        if (enemyData == null || player == null)
        {
            if (_rb != null) _rb.velocity = Vector2.zero;
            currentState = EnemyState.Idle;
            return;
        }

        switch (currentState)
        {
            case EnemyState.Idle:
                TickIdle();
                break;
            case EnemyState.Chase:
                TickChase();
                break;
            case EnemyState.Hurt:
                TickHurt();
                break;
        }
    }

    private void FixedUpdate()
    {
        if (currentState == EnemyState.Dead) return;
        if (_rb == null || enemyData == null || player == null) return;

        if (currentState == EnemyState.Chase)
            MoveChaseTowardPlayer();
    }

    /// <summary>
    /// 追逐时保持与玩家的最小距离：双方相向移动时否则会每帧「顶」进同一位置，体感像突破一层后卡住。
    /// </summary>
    private void MoveChaseTowardPlayer()
    {
        Vector2 playerPos = player.position;
        Vector2 delta = playerPos - _rb.position;
        float dist = delta.magnitude;
        if (dist > enemyData.chaseRange)
            return;
        if (dist < 1e-6f)
            return;

        // 旧数据里若未序列化该字段会为 0，回退到默认环距
        float minSep = enemyData.minSeparationFromPlayer > 0.01f
            ? Mathf.Max(0.05f, enemyData.minSeparationFromPlayer)
            : 0.55f;
        Vector2 dir = delta / dist;

        // 已比最小距离更近：先把自身摆回环上，避免与玩家中心重叠
        if (dist < minSep)
        {
            _rb.MovePosition(playerPos - dir * minSep);
            return;
        }

        float step = enemyData.moveSpeed * Time.fixedDeltaTime;
        float nextDist = dist - step;
        if (nextDist < minSep)
            step = Mathf.Max(0f, dist - minSep);

        if (step > 1e-6f)
            _rb.MovePosition(_rb.position + dir * step);
    }

    private void TickIdle()
    {
        if (_rb != null) _rb.velocity = Vector2.zero;

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance <= enemyData.chaseRange)
        {
            SetState(EnemyState.Chase);
        }
    }

    private void TickChase()
    {
        float distance = Vector2.Distance(transform.position, player.position);
        if (distance > enemyData.chaseRange)
        {
            SetState(EnemyState.Idle);
            return;
        }

        Vector2 moveDir = (player.position - transform.position).normalized;
        if (_sr != null) _sr.flipX = moveDir.x < 0;
    }

    private void TickHurt()
    {
        if (_rb != null) _rb.velocity = Vector2.zero;

        _hurtTimer -= Time.deltaTime;
        if (_hurtTimer <= 0f)
        {
            float distance = Vector2.Distance(transform.position, player.position);
            SetState(distance <= enemyData.chaseRange ? EnemyState.Chase : EnemyState.Idle);
        }
    }

    public void TakeDamage(int damage)
    {
        if (currentState == EnemyState.Dead) return;
        if (_entity != null && _entity.IsDead) return;
        if (damage <= 0) return;

        int final = _entity.ApplyDamage(damage);
        if (final <= 0) return;

        Debug.Log($"{nameof(EnemyStateMachine)}: Took {final} damage (raw {damage}), remaining {_entity.CurrentHealth}");

        if (_entity.IsDead)
            SetState(EnemyState.Dead);
        else
            SetState(EnemyState.Hurt);
    }

    /// <summary>共用实体（生命、防御等）</summary>
    public Entity Entity => _entity;

    private void SetState(EnemyState newState)
    {
        // If already hurt, re-apply hurt timer so multiple hits extend the hurt period.
        if (currentState == newState && newState == EnemyState.Hurt)
        {
            _hurtTimer = EffectiveHurtDuration;
            if (_sr != null) _sr.color = Color.red;
            return;
        }

        if (currentState == newState) return;

        // Exit
        if (currentState == EnemyState.Hurt && _sr != null)
        {
            _sr.color = Color.white;
        }

        currentState = newState;

        // Enter
        switch (currentState)
        {
            case EnemyState.Idle:
                if (_rb != null) _rb.velocity = Vector2.zero;
                break;

            case EnemyState.Chase:
                break;

            case EnemyState.Hurt:
                if (_rb != null) _rb.velocity = Vector2.zero;
                _hurtTimer = EffectiveHurtDuration;
                if (_sr != null) _sr.color = Color.red;

                if (_hitFlashRoutine != null) StopCoroutine(_hitFlashRoutine);
                _hitFlashRoutine = StartCoroutine(HitFlashRoutine());
                break;

            case EnemyState.Dead:
                if (_rb != null) _rb.velocity = Vector2.zero;
                if (_hitFlashRoutine != null) StopCoroutine(_hitFlashRoutine);
                Destroy(gameObject, dieDelay);
                break;
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        // Extra brief feedback; actual hurt duration is controlled by _hurtTimer.
        yield return new WaitForSeconds(0.05f);
        if (currentState == EnemyState.Hurt && _sr != null)
            _sr.color = Color.red;
    }
}

