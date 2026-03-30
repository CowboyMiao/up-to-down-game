using System;
using UnityEngine;

/// <summary>
/// 玩家与敌人共用的实体数据：生命、防御、受伤、死亡事件。
/// 挂在角色根物体上；由 <see cref="PlayerStateMachine"/> / <see cref="EnemyStateMachine"/> 或外部调用 <see cref="ApplyDamage"/>。
/// </summary>
[DisallowMultipleComponent]
public class Entity : MonoBehaviour
{
    [Header("默认值（若在 Start 前被 Configure 覆盖则以配置为准）")]
    [Min(1)] public int maxHealth = 3;

    [Min(0f)]
    public float defense = 0f;

    private int _currentHealth;
    private bool _isDead;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => _currentHealth;
    public float Defense => defense;
    public bool IsDead => _isDead;

    /// <summary>最终伤害, 原始伤害</summary>
    public event Action<int, int> OnDamaged;

    public event Action OnDied;

    /// <summary>当前生命, 最大生命</summary>
    public event Action<int, int> OnHealthChanged;

    private void Awake()
    {
        if (_currentHealth <= 0)
            _currentHealth = Mathf.Max(1, maxHealth);
    }

    /// <summary>用数值初始化（肉鸽里换关、读档可再次调用）</summary>
    public void Configure(int maxHp, float def)
    {
        maxHealth = Mathf.Max(1, maxHp);
        defense = Mathf.Max(0f, def);
        _currentHealth = maxHealth;
        _isDead = false;
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    public void ConfigureFromCombatStats(CombatStatsData data)
    {
        if (data == null) return;
        Configure(data.maxHealth, data.defense);
    }

    public void ConfigureFromEnemyData(EnemyData data)
    {
        if (data == null) return;
        Configure(data.maxHealth, data.defense);
    }

    /// <summary>
    /// 受到原始伤害，内部经防御减免后扣血。
    /// </summary>
    /// <returns>实际结算的最终伤害（0 表示未扣血）</returns>
    public int ApplyDamage(int rawDamage)
    {
        if (_isDead || rawDamage <= 0) return 0;

        int final = DamageCalculator.ApplyDefense(rawDamage, defense);
        _currentHealth -= final;

        OnDamaged?.Invoke(final, rawDamage);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);

        if (_currentHealth <= 0)
        {
            _currentHealth = 0;
            _isDead = true;
            OnDied?.Invoke();
        }

        return final;
    }

    public void Heal(int amount)
    {
        if (_isDead || amount <= 0) return;
        _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    /// <summary>外部强制进入死亡状态（不触发 ApplyDamage）</summary>
    public void ForceKillWithoutEvent()
    {
        _currentHealth = 0;
        _isDead = true;
    }
}
