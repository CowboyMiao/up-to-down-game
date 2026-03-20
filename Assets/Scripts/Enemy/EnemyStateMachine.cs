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
    public float hurtDuration = 0.1f;
    public float dieDelay = 1f;

    [Header("Debug")]
    public EnemyState currentState = EnemyState.Idle;

    private Rigidbody2D _rb;
    private SpriteRenderer _sr;
    private int _currentHealth;
    private float _hurtTimer;
    private Coroutine _hitFlashRoutine;

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
    }

    private void Start()
    {
        _currentHealth = enemyData != null ? enemyData.maxHealth : 0;

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
        if (_rb != null) _rb.velocity = moveDir * enemyData.moveSpeed;
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
        if (damage <= 0) return;

        _currentHealth -= damage;
        Debug.Log($"{nameof(EnemyStateMachine)}: Took damage, remaining {_currentHealth}");

        if (_currentHealth <= 0)
        {
            SetState(EnemyState.Dead);
        }
        else
        {
            SetState(EnemyState.Hurt);
        }
    }

    private void SetState(EnemyState newState)
    {
        // If already hurt, re-apply hurt timer so multiple hits extend the hurt period.
        if (currentState == newState && newState == EnemyState.Hurt)
        {
            _hurtTimer = hurtDuration;
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
                _hurtTimer = hurtDuration;
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

