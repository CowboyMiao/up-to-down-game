using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家受击：与带 <see cref="EnemyController"/> 的碰撞体重叠时，按 <see cref="EnemyData.damage"/> 造成伤害。
/// 最终扣血量由 <see cref="PlayerStateMachine.TakeDamage"/> 内根据防御计算。
/// 需要：本物体上有 Collider2D（建议 isTrigger）+ Rigidbody2D（与敌人一侧的碰撞设置匹配）。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerHurtReceiver : MonoBehaviour
{
    [SerializeField] private PlayerStateMachine player;

    [Tooltip("同一敌人连续造成伤害的最短间隔（秒），防止在碰撞体内每帧多次受伤")]
    [Min(0.05f)]
    public float damageCooldownPerEnemy = 0.4f;

    [Tooltip("只响应带 Enemy 标签的碰撞体（可选，额外保险）")]
    public bool requireEnemyTag = false;

    private readonly Dictionary<int, float> _lastHitTimeByEnemy = new Dictionary<int, float>();

    private void Awake()
    {
        player ??= GetComponent<PlayerStateMachine>();
        if (player == null)
            player = GetComponentInParent<PlayerStateMachine>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 部分情况下 Enter 会漏（生成顺序），Stay 再试一次；冷却仍会限制频率
        TryApplyDamage(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider != null)
            TryApplyDamage(collision.collider);
    }

    private void TryApplyDamage(Collider2D other)
    {
        if (player == null)
            return;
        if (player.currentState == PlayerState.Dead)
            return;
        // 受伤硬直期间不再结算碰撞伤害，避免 Stay 每帧反复 TakeDamage 把 hurtTimer 刷满 → 永远卡在 Hurt 无法移动
        if (player.currentState == PlayerState.Hurt)
            return;

        if (requireEnemyTag && !other.CompareTag("Enemy"))
            return;

        var enemyController = other.GetComponentInParent<EnemyController>();
        if (enemyController == null)
            enemyController = other.GetComponent<EnemyController>();
        if (enemyController == null || enemyController.enemyData == null)
            return;

        var esm = enemyController.GetComponent<EnemyStateMachine>();
        if (esm != null && esm.currentState == EnemyState.Dead)
            return;

        int id = enemyController.gameObject.GetInstanceID();
        float now = Time.time;
        float cooldown = Mathf.Max(0.05f, damageCooldownPerEnemy);
        if (_lastHitTimeByEnemy.TryGetValue(id, out float last) && now - last < cooldown)
            return;

        int raw = Mathf.Max(0, enemyController.enemyData.damage);
        if (raw <= 0)
            return;

        _lastHitTimeByEnemy[id] = now;
        player.TakeDamage(raw);
    }
}
