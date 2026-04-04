using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间流程：玩家进房 -> 关门(空气墙) -> 刷怪 -> 清怪 -> 开门。
/// 设计目标：尽量少依赖其它系统；Enemy prefab 自己带 EnemyController/EnemyStateMachine/Entity 即可。
/// </summary>
public class RoomFlowController : MonoBehaviour
{
    [Header("刷怪配置")]
    [Tooltip("可刷出的敌人预制体列表（随机选）。Prefab 上应包含 EnemyController/EnemyStateMachine（至少其一）。")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();

    [Min(0)] public int enemiesPerRoomMin = 2;
    [Min(0)] public int enemiesPerRoomMax = 5;

    [Tooltip("刷怪点距离房间边缘的内缩（单位：格子），避免刷到墙上。")]
    [Min(0)] public int spawnInsetCells = 1;

    [Tooltip("刷怪点与玩家的最小距离（世界单位）。")]
    [Min(0f)] public float minSpawnDistanceFromPlayer = 2.5f;

    [Header("规则")]
    [Tooltip("为 true 时：初始房间（roomIndex=0）不刷怪，也不关门。")]
    public bool skipInitialRoom = true;

    [Header("门（空气墙）")]
    [Tooltip("进入房间后是否生成门空气墙阻挡走廊出口。")]
    public bool blockDoorsUntilCleared = true;

    [Tooltip("玩家进入房间后延迟多久再关门（秒）。避免玩家还没完全进房就被门挡在外面。")]
    [Min(0f)]
    public float doorCloseDelay = 0.5f;

    [Header("门可视化（可选）")]
    [Tooltip("门的可视化 Prefab（仅显示，不要带 Collider）。会作为门对象的子物体生成，开门时一起销毁。")]
    public GameObject doorVisualPrefab;

    [Tooltip("门可视化的 Sorting Order（若 Prefab 里有 SpriteRenderer，会被覆盖为该值）。")]
    public int doorVisualSortingOrder = 5;

    [Tooltip("门碰撞厚度（世界单位）。建议与生成器 wallThickness 一致。")]
    [Min(0.01f)] public float doorThickness = 0.08f;

    [Tooltip("门碰撞是否为 Trigger（挡路建议 false）。")]
    public bool doorsAreTriggers = false;

    [Header("引用")]
    [Tooltip("地图生成器（用于坐标换算/参数）。为空会在同物体查找。")]
    public RectRoomDungeonGenerator2D generator;

    private readonly Dictionary<int, RoomRuntime> _rooms = new Dictionary<int, RoomRuntime>();
    private int _currentRoomIndex = -1;

    private class RoomRuntime
    {
        public RoomArea2D area;
        public RectInt rect;
        public bool entered;
        public bool cleared;
        public readonly List<GameObject> aliveEnemies = new List<GameObject>();
        public readonly List<GameObject> doorObjects = new List<GameObject>();
    }

    private void Awake()
    {
        if (generator == null)
            generator = GetComponent<RectRoomDungeonGenerator2D>();
    }

    public void RegisterRoom(RoomArea2D area)
    {
        if (area == null) return;
        int idx = area.roomIndex;
        if (idx < 0) return;

        if (!_rooms.TryGetValue(idx, out var rr))
        {
            rr = new RoomRuntime();
            _rooms[idx] = rr;
        }

        rr.area = area;
        rr.rect = area.roomRect;

        area.OnPlayerEntered -= HandlePlayerEnteredRoom;
        area.OnPlayerEntered += HandlePlayerEnteredRoom;
    }

    private void HandlePlayerEnteredRoom(RoomArea2D area, Collider2D playerCol)
    {
        if (area == null) return;
        if (!_rooms.TryGetValue(area.roomIndex, out var rr)) return;
        if (rr.cleared) return;

        if (skipInitialRoom && area.roomIndex == 0)
        {
            rr.entered = true;
            rr.cleared = true;
            return;
        }

        _currentRoomIndex = area.roomIndex;

        if (!rr.entered)
        {
            rr.entered = true;

            if (blockDoorsUntilCleared)
                StartCoroutine(CloseDoorsAfterDelay(rr, doorCloseDelay));

            SpawnEnemiesForRoom(rr, playerCol != null ? playerCol.transform : null);
        }
    }

    private System.Collections.IEnumerator CloseDoorsAfterDelay(RoomRuntime rr, float delay)
    {
        if (rr == null) yield break;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // 延迟期间可能已经清房/或门已生成，无需重复
        if (rr.cleared) yield break;
        if (rr.doorObjects.Count > 0) yield break;

        BuildDoorsForRoom(rr);
    }

    private void SpawnEnemiesForRoom(RoomRuntime rr, Transform player)
    {
        if (generator == null) return;
        if (enemyPrefabs == null || enemyPrefabs.Count == 0) return;

        int minC = Mathf.Min(enemiesPerRoomMin, enemiesPerRoomMax);
        int maxC = Mathf.Max(enemiesPerRoomMin, enemiesPerRoomMax);
        int count = Random.Range(minC, maxC + 1);
        if (count <= 0) return;

        // 刷怪区域：房间内部内缩
        RectInt r = rr.rect;
        int inset = Mathf.Max(0, spawnInsetCells);
        r = new RectInt(r.xMin + inset, r.yMin + inset, Mathf.Max(1, r.width - inset * 2), Mathf.Max(1, r.height - inset * 2));

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = PickSpawnPositionInRect(r, player);
            var prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];
            if (prefab == null) continue;

            var go = Instantiate(prefab, pos, Quaternion.identity);
            rr.aliveEnemies.Add(go);

            TryInjectPlayerReference(go, player);
            HookEnemyDeath(go, rr);
        }
    }

    private Vector3 PickSpawnPositionInRect(RectInt rect, Transform player)
    {
        // 简单随机尝试若干次，找离玩家远一点的点
        const int tries = 24;
        Vector3 best = generator != null ? generatorCellCenter(rect.xMin, rect.yMin) : Vector3.zero;
        float bestDist = -1f;

        for (int t = 0; t < tries; t++)
        {
            int x = Random.Range(rect.xMin, rect.xMax);
            int y = Random.Range(rect.yMin, rect.yMax);
            Vector3 p = generatorCellCenter(x, y);

            if (player == null) return p;

            float d = Vector2.Distance(new Vector2(p.x, p.y), new Vector2(player.position.x, player.position.y));
            if (d >= minSpawnDistanceFromPlayer) return p;
            if (d > bestDist)
            {
                bestDist = d;
                best = p;
            }
        }

        return best;
    }

    private Vector3 generatorCellCenter(int cellX, int cellY)
    {
        float cx = generator.worldOrigin.x + (cellX + 0.5f) * generator.cellSize;
        float cy = generator.worldOrigin.y + (cellY + 0.5f) * generator.cellSize;
        return new Vector3(cx, cy, 0f);
    }

    private static void TryInjectPlayerReference(GameObject enemy, Transform player)
    {
        if (enemy == null || player == null) return;

        var controller = enemy.GetComponent<EnemyController>();
        if (controller != null) controller.player = player;

        var sm = enemy.GetComponent<EnemyStateMachine>();
        if (sm != null) sm.player = player;
    }

    private void HookEnemyDeath(GameObject enemy, RoomRuntime rr)
    {
        if (enemy == null || rr == null) return;

        // 统一用 Entity.OnDied；如果没有 Entity，就用 Destroy 回调兜底（但更建议 prefab 上有 Entity）。
        var entity = enemy.GetComponent<Entity>();
        if (entity != null)
        {
            entity.OnDied -= () => { };
            entity.OnDied += () => OnEnemyDied(enemy, rr);
        }
        else
        {
            var notifier = enemy.GetComponent<DestroyNotifier>();
            if (notifier == null) notifier = enemy.AddComponent<DestroyNotifier>();
            notifier.onDestroyed = () => OnEnemyDied(enemy, rr);
        }
    }

    private void OnEnemyDied(GameObject enemy, RoomRuntime rr)
    {
        if (rr == null) return;
        rr.aliveEnemies.Remove(enemy);

        if (rr.aliveEnemies.Count <= 0 && !rr.cleared)
        {
            rr.cleared = true;
            OpenDoors(rr);
        }
    }

    private void BuildDoorsForRoom(RoomRuntime rr)
    {
        if (generator == null) return;

        // 走廊入口：房间边界格子中，邻接格子是 walkable 但不在房间内（代表连接到走廊/其它房间）
        bool[,] walkable = generator.BuildWalkableGrid_Public();
        RectInt room = rr.rect;

        for (int x = room.xMin; x < room.xMax; x++)
        {
            for (int y = room.yMin; y < room.yMax; y++)
            {
                bool isBoundary = (x == room.xMin || x == room.xMax - 1 || y == room.yMin || y == room.yMax - 1);
                if (!isBoundary) continue;

                // right
                TryCreateDoorBetweenCells(rr, walkable, room, x, y, x + 1, y);
                // left
                TryCreateDoorBetweenCells(rr, walkable, room, x, y, x - 1, y);
                // up
                TryCreateDoorBetweenCells(rr, walkable, room, x, y, x, y + 1);
                // down
                TryCreateDoorBetweenCells(rr, walkable, room, x, y, x, y - 1);
            }
        }
    }

    private void TryCreateDoorBetweenCells(RoomRuntime rr, bool[,] walkable, RectInt room, int ax, int ay, int bx, int by)
    {
        // A 在房间边界且 walkable；B 必须 walkable 且不在房间内，才算入口
        if (!RectContains(room, ax, ay)) return;
        if (!IsWalkable(walkable, ax, ay)) return;
        if (!IsWalkable(walkable, bx, by)) return;
        if (RectContains(room, bx, by)) return;

        // 在 A 与 B 之间放一段门 collider
        Vector3 pos;
        Vector2 size;
        float cs = generator.cellSize;
        float t = doorThickness;

        // B 在右/左：竖线；B 在上/下：横线
        if (bx != ax)
        {
            float xBoundary = generator.worldOrigin.x + (Mathf.Max(ax, bx)) * cs; // 两格之间的竖边界世界 x
            float yCenter = generator.worldOrigin.y + (ay + 0.5f) * cs;
            pos = new Vector3(xBoundary, yCenter, 0f);
            size = new Vector2(t, cs);
        }
        else
        {
            float yBoundary = generator.worldOrigin.y + (Mathf.Max(ay, by)) * cs; // 两格之间的横边界世界 y
            float xCenter = generator.worldOrigin.x + (ax + 0.5f) * cs;
            pos = new Vector3(xCenter, yBoundary, 0f);
            size = new Vector2(cs, t);
        }

        // 去重：同一条门可能被多个边界格子扫描到
        string key = $"Door_{rr.area.roomIndex}_{ax}_{ay}_{bx}_{by}";
        foreach (var existing in rr.doorObjects)
        {
            if (existing != null && existing.name == key) return;
        }

        var go = new GameObject(key);
        go.transform.SetParent(rr.area.transform, false);
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = doorsAreTriggers;

        // 可视化：在门对象下生成一个纯显示的子物体
        if (doorVisualPrefab != null)
        {
            var vis = Instantiate(doorVisualPrefab, go.transform);
            vis.name = "DoorVisual";
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;

            // 尝试按门尺寸缩放（适配 1x1 的 sprite/prefab）
            // 如果你的门 prefab 自带正确尺寸，可以忽略该缩放（把本地 scale 设为 1 即可）。
            vis.transform.localScale = new Vector3(size.x, size.y, 1f);

            foreach (var sr in vis.GetComponentsInChildren<SpriteRenderer>(true))
                sr.sortingOrder = doorVisualSortingOrder;

            foreach (var c2d in vis.GetComponentsInChildren<Collider2D>(true))
            {
                if (Application.isPlaying)
                    Destroy(c2d);
                else
                    DestroyImmediate(c2d);
            }
        }

        rr.doorObjects.Add(go);
    }

    private static bool RectContains(RectInt rect, int x, int y) =>
        x >= rect.xMin && x < rect.xMax && y >= rect.yMin && y < rect.yMax;

    private static bool IsWalkable(bool[,] walkable, int x, int y)
    {
        if (walkable == null) return false;
        int w = walkable.GetLength(0);
        int h = walkable.GetLength(1);
        if (x < 0 || y < 0 || x >= w || y >= h) return false;
        return walkable[x, y];
    }

    private void OpenDoors(RoomRuntime rr)
    {
        for (int i = rr.doorObjects.Count - 1; i >= 0; i--)
        {
            var go = rr.doorObjects[i];
            if (go != null) Destroy(go);
        }
        rr.doorObjects.Clear();
    }

    private class DestroyNotifier : MonoBehaviour
    {
        public System.Action onDestroyed;
        private void OnDestroy() => onDestroyed?.Invoke();
    }
}

