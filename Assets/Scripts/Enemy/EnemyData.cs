using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "GameData/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("»ù´¡ÊôĞÔ")]
    public float moveSpeed = 2f;      // ÒÆ¶¯ËÙ¶È
    public int maxHealth = 3;         // ×î´óÑªÁ¿
    public int damage = 1;            // ¹¥»÷ÉËº¦
    [Header("¼ì²â·¶Î§")]
    public float chaseRange = 5f;     // ×·»÷·¶Î§
}