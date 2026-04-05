using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家受击：检测附近带 <see cref="EnemyController"/> 的敌人并造成伤害（Overlap 检测，不依赖与敌人的 Trigger 重叠回调）。
/// 若项目里存在名为 Player / Enemy 的 Layer，会在运行时对该层对调用 <see cref="Physics2D.IgnoreLayerCollision"/>，
/// 使玩家实体碰撞体与敌人不产生物理求解（重叠时不再被「粘住」）；伤害仍由本脚本的 Overlap 独立结算。
/// 地图墙请放在与 Player 在 Layer Collision Matrix 中仍勾选的层（如 Wall 或 Default）。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerHurtReceiver : MonoBehaviour
{
    [SerializeField] private PlayerStateMachine player;

    [Min(0.05f)]
    [Tooltip("当 Rigidbody 所在物体上没有任何 CircleCollider2D 时，新建实体圆所用的半径")]
    public float autoSolidColliderRadius = 0.35f;

    [Tooltip("同一敌人连续造成伤害的最短间隔（秒），防止在碰撞体内每帧多次受伤")]
    [Min(0.05f)]
    public float damageCooldownPerEnemy = 0.4f;

    [Tooltip("只响应带 Enemy 标签的碰撞体（可选，额外保险）")]
    public bool requireEnemyTag = false;

    [Tooltip("近战受击检测半径；≤0 时自动用身上 CircleCollider2D 的世界半径")]
    public float contactDamageRadius = -1f;

    private readonly Dictionary<int, float> _lastHitTimeByEnemy = new Dictionary<int, float>();
    private readonly Collider2D[] _overlapBuffer = new Collider2D[24];

    private void Awake()
    {
        player ??= GetComponent<PlayerStateMachine>();
        if (player == null)
            player = GetComponentInParent<PlayerStateMachine>();
        EnsureSolidColliderForWallCollision();
        TryIgnoreLayerCollisionPlayerVsEnemy();
    }

    /// <summary>
    /// 全局：Player 层与 Enemy 层不参与物理碰撞/求解，避免 Dynamic 与 Trigger/Kinematic 重叠时把玩家速度「解没」。
    /// OverlapCircle 检测伤害不受 Layer 矩阵影响。
    /// </summary>
    private static void TryIgnoreLayerCollisionPlayerVsEnemy()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (playerLayer < 0 || enemyLayer < 0 || playerLayer == enemyLayer)
            return;
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);
    }

    private void FixedUpdate()
    {
        if (player == null)
            return;
        if (player.currentState == PlayerState.Dead)
            return;
        if (player.currentState == PlayerState.Hurt)
            return;

        if (!TryGetHurtCircle(out Vector2 center, out float radius) || radius <= 0f)
            return;

        int count = Physics2D.OverlapCircleNonAlloc(center, radius, _overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuffer[i];
            if (col == null)
                continue;
            TryApplyDamage(col);
        }
    }

    private bool TryGetHurtCircle(out Vector2 center, out float radius)
    {
        if (contactDamageRadius > 0f)
        {
            center = transform.position;
            radius = contactDamageRadius;
            return true;
        }

        var circle = GetComponent<CircleCollider2D>();
        if (circle == null)
            circle = GetComponentInParent<Rigidbody2D>()?.GetComponent<CircleCollider2D>();
        if (circle == null)
        {
            foreach (var c in transform.root.GetComponentsInChildren<CircleCollider2D>(true))
            {
                if (c != null && !c.isTrigger)
                {
                    circle = c;
                    break;
                }
            }
        }

        if (circle == null)
        {
            center = default;
            radius = 0f;
            return false;
        }

        center = (Vector2)circle.transform.TransformPoint(circle.offset);
        float s = Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y));
        if (s < 1e-4f)
            s = 1f;
        radius = circle.radius * s;
        return true;
    }

    private void EnsureSolidColliderForWallCollision()
    {
        if (HasAnySolidCollider())
            return;

        var rb = GetComponentInParent<Rigidbody2D>();
        var host = rb != null ? rb.gameObject : transform.root.gameObject;

        var circleOnReceiver = GetComponent<CircleCollider2D>();
        if (circleOnReceiver != null)
        {
            circleOnReceiver.isTrigger = false;
            return;
        }

        foreach (var circle in host.GetComponentsInChildren<CircleCollider2D>(true))
        {
            if (circle.isTrigger)
            {
                circle.isTrigger = false;
                return;
            }
        }

        var added = host.AddComponent<CircleCollider2D>();
        added.isTrigger = false;
        added.radius = Mathf.Max(0.05f, autoSolidColliderRadius);
    }

    private bool HasAnySolidCollider()
    {
        foreach (var c in transform.root.GetComponentsInChildren<Collider2D>(true))
        {
            if (c != null && !c.isTrigger)
                return true;
        }
        return false;
    }

    private void TryApplyDamage(Collider2D other)
    {
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
        player.TakeDamage(raw, enemyController.transform.position);
    }
}
