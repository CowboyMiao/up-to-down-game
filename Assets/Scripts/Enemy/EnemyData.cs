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
}