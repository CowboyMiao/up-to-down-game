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

    [Tooltip("与玩家中心的最小保持距离（世界单位）。Kinematic 追逐若每帧贴到玩家中心会与玩家相向移动叠加成深度重叠；应略大于双方碰撞体半径之和。")]
    [Min(0.05f)]
    public float minSeparationFromPlayer = 0.55f;

    [Header("受击")]
    [Tooltip("受击后停顿（硬直）时间（秒）。大于 0 时覆盖 EnemyStateMachine 上的 hurtDuration。")]
    [Min(0f)]
    public float hitStunDuration = 0.2f;
}