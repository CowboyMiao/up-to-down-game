using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂在武器的 AttackCheck 上：在碰撞体启用期间每帧用 OverlapCollider 扫描重叠目标。
/// 解决「双 Trigger / Layer / 消息未传到 EnemyController」时 OnTrigger 不稳定的问题。
/// 使用 LateUpdate（而非 FixedUpdate）：攻击碰撞体在 PlayerStateMachine.Update 里开启，
/// FixedUpdate 先于整帧的 Update，会漏挥击首帧；用 LateUpdate 保证晚于玩家本帧的 Enable。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AttackColliderHitScanner2D : MonoBehaviour
{
    private static readonly List<Collider2D> s_Hits = new List<Collider2D>(16);

    private Collider2D _col;
    private ContactFilter2D _filter;

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        _filter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false
        };
    }

    private void LateUpdate()
    {
        if (_col == null || !_col.enabled)
            return;

        s_Hits.Clear();
        int n = _col.OverlapCollider(_filter, s_Hits);
        if (n <= 0)
            return;

        var wm = GetComponentInParent<WeaponManager>();
        if (wm == null)
            return;

        for (int i = 0; i < s_Hits.Count; i++)
        {
            var hit = s_Hits[i];
            if (hit == null || hit == _col)
                continue;

            var enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy != null)
                enemy.TryReceivePlayerWeaponHit(wm, _col);
        }
    }
}
