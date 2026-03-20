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
        if (other.CompareTag("AttackCheck"))
            TakeDamage(1);
    }
}