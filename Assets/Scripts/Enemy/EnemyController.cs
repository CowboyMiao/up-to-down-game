using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Data")]
    public EnemyData enemyData;
    public Transform player;
    
    [Header("State Machine")]
    [SerializeField] private EnemyStateMachine stateMachine;

    [Header("碰撞（俯视角）")]
    [Tooltip("为 true 时：将身上所有 Collider2D 设为 Trigger，避免与玩家 Dynamic Rigidbody2D 互相挤压、卡进体内。依赖地图空气墙挡路；若敌人需要实体推挤请关。")]
    public bool bodyCollidersAsTriggers = true;

    /// <summary>本敌人上一次已结算的玩家挥击 ID，同一挥击只受击一次。</summary>
    private int _lastProcessedPlayerSwingId = -1;

    private void Awake()
    {
        if (stateMachine == null) stateMachine = GetComponent<EnemyStateMachine>();
        if (stateMachine == null) stateMachine = gameObject.AddComponent<EnemyStateMachine>();

        if (bodyCollidersAsTriggers)
        {
            foreach (var c in GetComponentsInChildren<Collider2D>(true))
            {
                if (c == null) continue;
                c.isTrigger = true;
            }
        }
    }

    private void Start()
    {
        // Inject data into state machine.
        if (stateMachine != null)
        {
            stateMachine.enemyData = enemyData;
            stateMachine.player = player;
        }
    }

    public void TakeDamage(int damage)
    {
        stateMachine?.TakeDamage(damage);
    }

    private void OnTriggerEnter2D(Collider2D other) => TryTakeDamageFromPlayerAttack(other);

    private void OnTriggerStay2D(Collider2D other) => TryTakeDamageFromPlayerAttack(other);

    /// <summary>由 AttackColliderHitScanner2D 调用；与 Trigger 消息共用同一套挥击 ID 与伤害。</summary>
    public void TryReceivePlayerWeaponHit(WeaponManager wm, Collider2D attackCheckCollider)
    {
        if (wm == null || attackCheckCollider == null)
            return;
        if (!attackCheckCollider.CompareTag("AttackCheck"))
            return;

        int swingId = wm.CurrentAttackSwingId;
        if (swingId <= 0)
            return;

        if (_lastProcessedPlayerSwingId == swingId)
            return;

        _lastProcessedPlayerSwingId = swingId;

        int dmg = ResolveDamageFromPlayerAttack(attackCheckCollider);
        TakeDamage(dmg);
    }

    private void TryTakeDamageFromPlayerAttack(Collider2D other)
    {
        if (!other.CompareTag("AttackCheck"))
            return;

        var wm = other.GetComponentInParent<WeaponManager>();
        if (wm == null)
            return;

        TryReceivePlayerWeaponHit(wm, other);
    }

    /// <summary>
    /// 从玩家武器数据 + 玩家 CombatStats（暴击）计算伤害；找不到则退回为 1。
    /// </summary>
    private static int ResolveDamageFromPlayerAttack(Collider2D attackCheck)
    {
        var weaponManager = attackCheck.GetComponentInParent<WeaponManager>();
        if (weaponManager == null)
            return 1;

        var weaponData = weaponManager.GetCurrentWeaponData();
        if (weaponData == null)
            return 1;

        int baseDamage = weaponData.attackDamage;
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo == null)
            return DamageCalculator.ComputeOutgoingDamage(baseDamage, null, out _);

        var psm = playerGo.GetComponent<PlayerStateMachine>();
        if (psm == null)
            return DamageCalculator.ComputeOutgoingDamage(baseDamage, null, out _);

        return psm.GetOutgoingDamageFromWeapon(baseDamage, out _);
    }
}