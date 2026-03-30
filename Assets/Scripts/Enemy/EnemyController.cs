using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Data")]
    public EnemyData enemyData;
    public Transform player;
    
    [Header("State Machine")]
    [SerializeField] private EnemyStateMachine stateMachine;

    private void Awake()
    {
        if (stateMachine == null) stateMachine = GetComponent<EnemyStateMachine>();
        if (stateMachine == null) stateMachine = gameObject.AddComponent<EnemyStateMachine>();
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("AttackCheck"))
            return;

        int dmg = ResolveDamageFromPlayerAttack(other);
        TakeDamage(dmg);
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