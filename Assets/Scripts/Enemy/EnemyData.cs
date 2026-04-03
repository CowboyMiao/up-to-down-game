using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "GameData/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("????")]
    public float moveSpeed = 2f;
    public int maxHealth = 3;
    public int damage = 1;

    [Tooltip("????????? DamageCalculator.ApplyDefense ???")]
    [Min(0f)]
    public float defense = 0f;

    [Header("????")]
    public float chaseRange = 5f;

    [Header("受击")]
    [Tooltip("受击后停顿（硬直）时间（秒）。大于 0 时覆盖 EnemyStateMachine 上的 hurtDuration。")]
    [Min(0f)]
    public float hitStunDuration = 0.2f;
}