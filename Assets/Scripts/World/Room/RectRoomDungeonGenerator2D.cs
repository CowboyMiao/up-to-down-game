using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 随机矩形房间 + 矩形走廊（2D 俯视角）。
/// 房间/走廊都定义在离散的“格子（cell）”坐标系中。
/// </summary>
[DefaultExecutionOrder(-50)]
public class RectRoomDungeonGenerator2D : MonoBehaviour
{
    [Header("生成：房间数量")]
    [Min(1)] public int minRoomCount = 5;
    [Min(1)] public int maxRoomCount = 10;

    [Tooltip("放置房间/走廊的最大尝试次数（越大越不容易生成失败，但会更慢）。")]
    [Min(1)] public int maxPlacementAttempts = 250;

    [Header("地图边界（单位：格子）")]
    [Min(1)] public int mapWidth = 60;
    [Min(1)] public int mapHeight = 40;

    [Tooltip("格子坐标 (0,0) 对应的世界坐标。通常设为地图左下角。")]
    public Vector2 worldOrigin = Vector2.zero;
    [Tooltip("每个格子的世界尺寸（通常为 1）。会同步到 Grid.cellSize（Tilemap 模式）。")]
    [Min(0.01f)] public float cellSize = 1f;

    [Header("自动运行")]
    [Tooltip("为 true 时：Start() 自动调用 Generate()。")]
    public bool generateOnStart = true;

    [Tooltip("为 true 时：Generate() 后自动调用 InstantiateRectObjects()（生成可视化/碰撞）。")]
    public bool instantiateOnStart = true;

    [Header("玩家出生点")]
    [Tooltip("为 true 时：Generate() 后把玩家放到初始房间中心。")]
    public bool placePlayerOnGenerate = true;

    [Tooltip("若赋值：在初始房间中心生成该预制体。")]
    public GameObject playerPrefab;

    [Tooltip("为 true 时：只要挂了 playerPrefab 就会实例化；若场景里已有同 Tag 的玩家会先销毁再生成（避免“有占位物体导致永远不实例化”）。")]
    public bool alwaysSpawnPlayerPrefab = true;

    [Tooltip("仅当未设置 Player Prefab 时生效：移动场景中已有玩家的 Transform。若已设置 Player Prefab，将优先实例化预制体（请留空此项以免误挡实例化）。")]
    public Transform playerTransform;

    [Tooltip("当未指定 playerTransform 时，用该 Tag 查找场景内玩家。")]
    public string playerTag = "Player";

    [Header("房间尺寸（单位：格子）")]
    [Min(1)] public int roomWidthMin = 6;
    [Min(1)] public int roomWidthMax = 14;
    [Min(1)] public int roomHeightMin = 6;
    [Min(1)] public int roomHeightMax = 14;

    [Tooltip("房间之间额外间距（单位：格子），用于避免贴太近或重叠。")]
    [Min(0)] public int roomPadding = 1;

    [Header("走廊参数")]
    [Min(1)] public int corridorWidth = 2;
    [Min(1)] public int corridorLengthMin = 4;
    [Min(1)] public int corridorLengthMax = 18;

    [Tooltip("每次尝试从某房间某边伸出连廊时，单条连廊内部的随机尝试次数（防止一次失败就放弃）。")]
    [Min(1)] public int connectAttemptsPerSide = 40;

    [Tooltip("为达到目标房间数时，从已有房间随机选边扩展的最大总尝试次数（防止死循环）。")]
    [Min(1)] public int maxExpansionAttempts = 2000;

    [Header("走廊拐弯方式（兼容旧逻辑）")]
    [Tooltip("用于旧的 L 型连廊逻辑：决定连接矩形时先水平后垂直，还是先垂直后水平。")]
    public bool horizontalThenVertical = true;

    [Header("实例化（可选）")]
    [Tooltip("生成内容的父节点：必须拖「场景 Hierarchy」里的物体。不要拖 Project 里的 Prefab 资源，否则会报错且无法生成。")]
    public Transform generatedRoot;

    [Tooltip("若指定：每个房间/走廊矩形会实例化该 Prefab（可带 BoxCollider2D/可视化）。")]
    public GameObject rectPrefab;

    [Tooltip("若未指定 rectPrefab：将创建仅带 BoxCollider2D 的 GameObject。")]
    public bool addBoxColliderIfNoPrefab = true;

    [Header("空气墙 / 碰撞（推荐开启）")]
    [Tooltip("在可走区域与空区域的边界生成墙体 BoxCollider2D（用于挡住玩家）。")]
    public bool buildBoundaryWalls = true;

    [Tooltip("空气墙厚度（世界单位）。")]
    [Min(0.01f)] public float wallThickness = 0.08f;
    [Tooltip("为 true 时：空气墙 collider 为 Trigger（通常挡人需要 false）。")]
    public bool wallsAreTriggers = false;

    [Tooltip("当实例化房间/走廊矩形 BoxCollider2D 时，将其设置为 Trigger，避免在房间内部卡住。")]
    public bool walkableRectCollidersAsTriggers = true;

    [Header("地面可视化（Prefab 模式：不使用 Tilemap）")]
    [Tooltip("为 true 时：用 Prefab 做地面可视化（仅视觉）。")]
    public bool useFloorPrefabs = true;

    [Tooltip("房间地面 Prefab（通常是 1x1 的 Sprite/Quad）。")]
    public GameObject floorRoomPrefab;

    [Tooltip("走廊地面 Prefab（可空；为空则使用 floorRoomPrefab）。")]
    public GameObject floorCorridorPrefab;

    [Tooltip("当使用地面可视化时，跳过房间/走廊的大矩形 collider（仍保留空气墙挡路）。")]
    public bool hideWalkableColliderWhenFloorPrefab = true;

    [Tooltip("地面渲染层级（Sorting Order），通常设成比角色/墙更低。")]
    public int floorSortingOrder = -10;

    [Header("地面可视化（Tilemap 模式）")]
    [Tooltip("为 true 时：把地面刷到 Tilemap（不再实例化地砖 GameObject）。")]
    public bool useTilemap = false;

    [Tooltip("场景中已有的 Grid（可空；为空会自动创建到 generatedRoot 下）。")]
    public Grid grid;

    [Tooltip("地面 Tilemap（可空；为空会自动创建到 Grid 下）。")]
    public Tilemap floorTilemap;

    [Tooltip("地面 TilemapRenderer（可空；为空会自动创建）。")]
    public TilemapRenderer floorTilemapRenderer;

    [Tooltip("参考地砖 Sprite：用于自动设置 cellSize/Grid.cellSize（不需要改 PPU）。不填则会尝试从 Room Tile 里取 Sprite（仅支持 Tile）。")]
    public Sprite referenceTileSprite;

    [Serializable]
    public class TileSet9
    {
        [Header("中心")]
        public TileBase center;

        [Header("边（上/下/左/右）")]
        public TileBase edgeTop;
        public TileBase edgeBottom;
        public TileBase edgeLeft;
        public TileBase edgeRight;

        [Header("角（左上/右上/左下/右下）")]
        public TileBase cornerTopLeft;
        public TileBase cornerTopRight;
        public TileBase cornerBottomLeft;
        public TileBase cornerBottomRight;
    }

    [Tooltip("房间九宫格地砖（角/边/中心）。center 不为空才启用。")]
    public TileSet9 roomTiles9;

    [Tooltip("走廊九宫格地砖（角/边/中心）。center 不为空才启用。")]
    public TileSet9 corridorTiles9;

    [Tooltip("备用：未设置房间九宫格时使用的单一地砖。")]
    public TileBase roomFloorTile;

    [Tooltip("备用：未设置走廊九宫格时使用的单一地砖（为空则使用 roomFloorTile）。")]
    public TileBase corridorFloorTile;

    [Tooltip("每次生成前是否清空地面 Tilemap。")]
    public bool clearFloorTilemapBeforePaint = true;

    [ContextMenu("Tilemap：按地砖自动匹配 cellSize")]
    public void AutoConfigureCellSizeFromTile()
    {
        Sprite s = referenceTileSprite;
        if (s == null)
        {
            // Best-effort: only UnityEngine.Tilemaps.Tile exposes sprite directly.
            var candidate = roomTiles9 != null ? roomTiles9.center : roomFloorTile;
            s = TryGetSpriteFromTileBase(candidate);
        }

        if (s == null)
        {
            Debug.LogWarning(
                $"{nameof(RectRoomDungeonGenerator2D)}：无法从 Tile 获取 Sprite。请在 Inspector 指定 referenceTileSprite（拖一张地砖 Sprite 进来），然后再点该按钮。",
                this);
            return;
        }

        Vector2 size = s.bounds.size; // world units, already reflects PPU
        float newCell = Mathf.Max(0.01f, size.x);
        if (Mathf.Abs(size.x - size.y) > 1e-3f)
        {
            Debug.LogWarning(
                $"{nameof(RectRoomDungeonGenerator2D)}：参考 Sprite 不是正方形（{size.x:F4} x {size.y:F4} world units）。将使用 X 作为 cellSize：{newCell:F4}。如需不同 X/Y，请手动设置 Grid.cellSize。",
                this);
        }

        cellSize = newCell;
        if (grid != null)
            grid.cellSize = new Vector3(cellSize, cellSize, 1f);

        Debug.Log($"{nameof(RectRoomDungeonGenerator2D)}：已自动设置 cellSize/Grid.cellSize = {cellSize:F4}（来自 Sprite：{s.name}，PPU={s.pixelsPerUnit}）。", this);
    }

    private static Sprite TryGetSpriteFromTileBase(TileBase tileBase)
    {
        if (tileBase == null) return null;
        if (tileBase is Tile t) return t.sprite;
        return null;
    }

    [Header("墙体可视化（Tilemap，可关闭；你现在用空气墙则不用）")]
    [Tooltip("为 true 时：在单独的 Tilemap 上绘制墙/边缘瓦片（仅视觉）。若只要空气墙，关闭即可。")]
    public bool paintWallTiles = false;

    [Header("房间流程（进房刷怪/清怪开门）")]
    [Tooltip("为 true 时：为每个房间创建触发器，并启用 RoomFlowController（进房刷怪、清怪开门）。")]
    public bool enableRoomFlow = false;

    [Tooltip("房间触发器高度（Z 轴不用管）。建议保持 1。")]
    [Min(0.1f)]
    public float roomTriggerZSize = 1f;

    [Tooltip("刷怪/关门逻辑由该组件负责。为空则会自动挂到本物体上。")]
    public RoomFlowController roomFlowController;

    [Tooltip("Optional existing Tilemap for walls/edges; if null, generator will create one under the Grid.")]
    public Tilemap wallTilemap;

    [Tooltip("Optional TilemapRenderer for walls/edges; if null, will be created with the Tilemap.")]
    public TilemapRenderer wallTilemapRenderer;

    [Tooltip("If true, clears the wall tilemap before painting.")]
    public bool clearWallTilemapBeforePaint = true;

    public enum WallTilePlacement
    {
        [Tooltip("Place edge tiles on the walkable boundary cell itself (overlay on top of floor).")]
        OnWalkableCell = 0,
        [Tooltip("Place edge tiles on the empty neighbor cell outside the walkable area.")]
        OnEmptyNeighborCell = 1
    }

    [Tooltip("Where wall tiles should be painted. Most 3D-wall sprites work best as overlay on the boundary floor cell.")]
    public WallTilePlacement wallTilePlacement = WallTilePlacement.OnWalkableCell;

    [Tooltip("Sorting order for wall visuals (should be above floor).")]
    public int wallSortingOrder = -5;

    [Tooltip("Wall tile for the top edge (when above is empty).")]
    public TileBase wallTopTile;

    [Tooltip("Wall tile for the bottom edge (when below is empty).")]
    public TileBase wallBottomTile;

    [Tooltip("Wall tile for the left edge (when left is empty).")]
    public TileBase wallLeftTile;

    [Tooltip("Wall tile for the right edge (when right is empty).")]
    public TileBase wallRightTile;

    [Header("Corner tiles (optional)")]
    [Tooltip("If true, paint corner tiles (outer + inner). Corner tiles override edge tiles.")]
    public bool paintCornerTiles = true;

    [Tooltip("Outer corner: top-left (when up and left are empty).")]
    public TileBase outerCornerTopLeftTile;
    [Tooltip("Outer corner: top-right (when up and right are empty).")]
    public TileBase outerCornerTopRightTile;
    [Tooltip("Outer corner: bottom-left (when down and left are empty).")]
    public TileBase outerCornerBottomLeftTile;
    [Tooltip("Outer corner: bottom-right (when down and right are empty).")]
    public TileBase outerCornerBottomRightTile;

    [Tooltip("Inner corner: top-left (when up and left are walkable but up-left diagonal is empty).")]
    public TileBase innerCornerTopLeftTile;
    [Tooltip("Inner corner: top-right (when up and right are walkable but up-right diagonal is empty).")]
    public TileBase innerCornerTopRightTile;
    [Tooltip("Inner corner: bottom-left (when down and left are walkable but down-left diagonal is empty).")]
    public TileBase innerCornerBottomLeftTile;
    [Tooltip("Inner corner: bottom-right (when down and right are walkable but down-right diagonal is empty).")]
    public TileBase innerCornerBottomRightTile;

    public readonly List<RectInt> GeneratedRooms = new List<RectInt>();
    public readonly List<RectInt> GeneratedCorridors = new List<RectInt>();

    [ContextMenu("Generate Dungeon")]
    public void Generate()
    {
        ClearGenerated();
        GeneratedRooms.Clear();
        GeneratedCorridors.Clear();

        // 1) Initial room（随机矩形，不重叠）
        var initialRoom = GenerateSingleRoom();
        GeneratedRooms.Add(initialRoom);

        // 2) 目标房间数：在 [minRoomCount, maxRoomCount] 内随机（与生成逻辑一致）
        int minC = Mathf.Min(minRoomCount, maxRoomCount);
        int maxC = Mathf.Max(minRoomCount, maxRoomCount);
        int targetRooms = UnityEngine.Random.Range(minC, maxC + 1);
        targetRooms = Mathf.Max(1, targetRooms);

        // 3) 从已有房间随机选一边扩展，直到达到 targetRooms 或尝试耗尽
        int budget = maxExpansionAttempts;
        while (GeneratedRooms.Count < targetRooms && budget-- > 0)
        {
            int idx = UnityEngine.Random.Range(0, GeneratedRooms.Count);
            var src = GeneratedRooms[idx];
            var side = (Side)UnityEngine.Random.Range(0, 4);
            TryConnectOneSide(side, src);
        }

        if (GeneratedRooms.Count < minC)
        {
            Debug.LogWarning(
                $"{nameof(RectRoomDungeonGenerator2D)}: 仅生成 {GeneratedRooms.Count} 间房间，低于最少 {minC}（地图空间不足或参数过严，可调大地图或 corridor 长度范围后重试）。");
        }

        // 4) 玩家在初始房间中央（生成预制体或移动已有对象）
        PlacePlayerAtInitialRoom(initialRoom);
    }

    /// <summary>
    /// 将玩家放在第一个房间（initialRoom）的世界坐标中心。
    /// </summary>
    private void PlacePlayerAtInitialRoom(RectInt initialRoom)
    {
        if (!placePlayerOnGenerate)
        {
            Debug.Log($"{nameof(RectRoomDungeonGenerator2D)}：已跳过放置玩家（{nameof(placePlayerOnGenerate)} = false）。");
            return;
        }

        Vector2 c = CellToWorldCenter(initialRoom);

        // 优先：只要挂了 Player Prefab 就实例化（避免 Inspector 里误拖了 playerTransform 导致永远不 Instantiate）
        if (playerPrefab != null)
        {
            float z = playerPrefab.transform.position.z;
            var pos = new Vector3(c.x, c.y, z);

            if (alwaysSpawnPlayerPrefab)
            {
                var old = GameObject.FindGameObjectWithTag(playerTag);
                if (old != null)
                {
                    if (Application.isPlaying)
                        Destroy(old);
                    else
                        DestroyImmediate(old);
                }

                var spawned = Instantiate(playerPrefab, pos, Quaternion.identity);
                ApplyPlayerTag(spawned);
                Debug.Log($"{nameof(RectRoomDungeonGenerator2D)}：已在初始房间中心实例化玩家 -> {pos}", spawned);
                return;
            }

            var existing = GameObject.FindGameObjectWithTag(playerTag);
            if (existing != null)
            {
                existing.transform.position = new Vector3(c.x, c.y, existing.transform.position.z);
                Debug.Log($"{nameof(RectRoomDungeonGenerator2D)}：已移动场景中已有玩家到初始房间中心 -> {c}");
                return;
            }

            var go = Instantiate(playerPrefab, pos, Quaternion.identity);
            ApplyPlayerTag(go);
            Debug.Log($"{nameof(RectRoomDungeonGenerator2D)}：已实例化玩家（场景中原本无同 Tag 对象）-> {pos}", go);
            return;
        }

        // 未挂 Prefab：只移动场景中已有对象
        if (playerTransform != null)
        {
            if (!playerTransform.gameObject.scene.IsValid())
            {
                Debug.LogWarning($"{nameof(RectRoomDungeonGenerator2D)}：{nameof(playerTransform)} 指向非场景物体（可能是工程里的 Prefab 资源），已忽略。请拖 Hierarchy 里的玩家，或设置 Player Prefab。");
            }
            else
            {
                playerTransform.position = new Vector3(c.x, c.y, playerTransform.position.z);
                Debug.Log($"{nameof(RectRoomDungeonGenerator2D)}：已移动指定 Transform 到初始房间中心。");
                return;
            }
        }

        var byTag = GameObject.FindGameObjectWithTag(playerTag);
        if (byTag != null)
        {
            byTag.transform.position = new Vector3(c.x, c.y, byTag.transform.position.z);
            Debug.Log($"{nameof(RectRoomDungeonGenerator2D)}：已按 Tag 移动已有对象到初始房间中心。");
        }
        else
        {
            Debug.LogWarning(
                $"{nameof(RectRoomDungeonGenerator2D)}：未设置 {nameof(playerPrefab)}，且场景中没有 Tag 为 \"{playerTag}\" 的对象，无法生成或移动玩家。请在生成器上指定 Player Prefab，或在场景里放带该 Tag 的玩家。");
        }
    }

    private void ApplyPlayerTag(GameObject spawned)
    {
        if (spawned == null || string.IsNullOrEmpty(playerTag))
            return;
        try
        {
            spawned.tag = playerTag;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"{nameof(RectRoomDungeonGenerator2D)}：Tag \"{playerTag}\" 未在 Project Settings 中定义，无法设置。请 Edit -> Project Settings -> Tags and Layers 添加。");
        }
    }

    private enum Side { Left, Right, Up, Down }

    private RectInt GenerateSingleRoom()
    {
        int attempts = 0;
        while (attempts < maxPlacementAttempts)
        {
            attempts++;

            int w = UnityEngine.Random.Range(Mathf.Min(roomWidthMin, roomWidthMax), Mathf.Max(roomWidthMin, roomWidthMax) + 1);
            int h = UnityEngine.Random.Range(Mathf.Min(roomHeightMin, roomHeightMax), Mathf.Max(roomHeightMin, roomHeightMax) + 1);

            int xMin = UnityEngine.Random.Range(0, mapWidth - w + 1);
            int yMin = UnityEngine.Random.Range(0, mapHeight - h + 1);

            var rect = new RectInt(xMin, yMin, w, h);
            if (!IsRectInsideMap(rect)) continue;

            // No existing rooms/corridors at this point; still keep padding-safe logic by using roomPadding check against "virtual empty" isn't needed.
            return rect;
        }

        // Fallback: centered room
        int fw = Mathf.Clamp(roomWidthMin, 1, mapWidth);
        int fh = Mathf.Clamp(roomHeightMin, 1, mapHeight);
        int fx = (mapWidth - fw) / 2;
        int fy = (mapHeight - fh) / 2;
        return new RectInt(fx, fy, fw, fh);
    }

    /// <returns>是否成功新增一间房+一段连廊</returns>
    private bool TryConnectOneSide(Side side, RectInt sourceRoom)
    {
        // Corridor starts at the source room boundary, on the side midpoint line, and ends at a destination room boundary.
        for (int attempt = 0; attempt < connectAttemptsPerSide; attempt++)
        {
            int corridorLen = UnityEngine.Random.Range(Mathf.Min(corridorLengthMin, corridorLengthMax), Mathf.Max(corridorLengthMin, corridorLengthMax) + 1);
            corridorLen = Mathf.Max(1, corridorLen);

            int midCellX = sourceRoom.xMin + sourceRoom.width / 2;
            int midCellY = sourceRoom.yMin + sourceRoom.height / 2;

            // Pick destination room size
            int w = UnityEngine.Random.Range(Mathf.Min(roomWidthMin, roomWidthMax), Mathf.Max(roomWidthMin, roomWidthMax) + 1);
            int h = UnityEngine.Random.Range(Mathf.Min(roomHeightMin, roomHeightMax), Mathf.Max(roomHeightMin, roomHeightMax) + 1);

            RectInt corridorRect;
            RectInt destRoom;

            int halfCorr = corridorWidth / 2;

            switch (side)
            {
                case Side.Right:
                {
                    int x0 = sourceRoom.xMax;
                    int x1 = x0 + corridorLen; // corridor end (touch dest's left edge)
                    int y0 = midCellY - halfCorr;
                    corridorRect = new RectInt(x0, y0, corridorLen, corridorWidth);

                    int destXMin = x1;
                    int destYMin = midCellY - h / 2;
                    destRoom = new RectInt(destXMin, destYMin, w, h);
                    break;
                }
                case Side.Left:
                {
                    int x1 = sourceRoom.xMin;
                    int x0 = x1 - corridorLen; // corridor start
                    int y0 = midCellY - halfCorr;
                    corridorRect = new RectInt(x0, y0, corridorLen, corridorWidth);

                    int destXMax = x0;
                    int destXMin = destXMax - w;
                    int destYMin = midCellY - h / 2;
                    destRoom = new RectInt(destXMin, destYMin, w, h);
                    break;
                }
                case Side.Up:
                {
                    int y0 = sourceRoom.yMax;
                    int y1 = y0 + corridorLen;
                    int x0 = midCellX - halfCorr;
                    corridorRect = new RectInt(x0, y0, corridorWidth, corridorLen);

                    int destYMin = y1;
                    int destXMin = midCellX - w / 2;
                    destRoom = new RectInt(destXMin, destYMin, w, h);
                    break;
                }
                default: // Down
                {
                    int y1 = sourceRoom.yMin;
                    int y0 = y1 - corridorLen;
                    int x0 = midCellX - halfCorr;
                    corridorRect = new RectInt(x0, y0, corridorWidth, corridorLen);

                    int destYMax = y0;
                    int destYMin = destYMax - h;
                    int destXMin = midCellX - w / 2;
                    destRoom = new RectInt(destXMin, destYMin, w, h);
                    break;
                }
            }

            if (!IsRectInsideMap(corridorRect) || !IsRectInsideMap(destRoom))
                continue;

            // Must NOT overlap rooms/corridors (touching edges is allowed).
            if (AnyRectOverlapsExisting(corridorRect) || AnyRectOverlapsExisting(destRoom))
                continue;

            GeneratedCorridors.Add(corridorRect);
            GeneratedRooms.Add(destRoom);
            return true;
        }

        return false;
    }

    private bool IsRectInsideMap(RectInt rect)
    {
        return rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= mapWidth && rect.yMax <= mapHeight;
    }

    private bool AnyRectOverlapsExisting(RectInt candidate)
    {
        foreach (var r in GeneratedRooms)
        {
            if (candidate.Overlaps(r)) return true;
        }
        foreach (var c in GeneratedCorridors)
        {
            if (candidate.Overlaps(c)) return true;
        }
        return false;
    }

    private void Start()
    {
        if (!generateOnStart) return;
        Generate();
        if (instantiateOnStart) InstantiateRectObjects();
    }

    private List<RectInt> GenerateRooms(int roomCount)
    {
        var result = new List<RectInt>(roomCount);

        int attempts = 0;
        while (result.Count < roomCount && attempts < maxPlacementAttempts)
        {
            attempts++;

            int w = UnityEngine.Random.Range(
                Mathf.Min(roomWidthMin, roomWidthMax),
                Mathf.Max(roomWidthMin, roomWidthMax) + 1
            );
            int h = UnityEngine.Random.Range(
                Mathf.Min(roomHeightMin, roomHeightMax),
                Mathf.Max(roomHeightMin, roomHeightMax) + 1
            );

            if (w > mapWidth || h > mapHeight) continue;

            int xMin = UnityEngine.Random.Range(0, mapWidth - w + 1);
            int yMin = UnityEngine.Random.Range(0, mapHeight - h + 1);

            var rect = new RectInt(xMin, yMin, w, h);
            if (!CanPlace(rect, result))
                continue;

            result.Add(rect);
        }

        return result;
    }

    private bool CanPlace(RectInt rect, List<RectInt> existing)
    {
        var padded = Expand(rect, roomPadding);
        foreach (var r in existing)
        {
            if (padded.Overlaps(r))
                return false;
        }
        return true;
    }

    private static RectInt Expand(RectInt rect, int pad)
    {
        if (pad <= 0) return rect;
        return new RectInt(rect.xMin - pad, rect.yMin - pad, rect.width + pad * 2, rect.height + pad * 2);
    }

    private struct Edge
    {
        public int a;
        public int b;
        public float weight;
    }

    private List<Edge> BuildEdges(List<RectInt> rooms)
    {
        var edges = new List<Edge>();
        for (int i = 0; i < rooms.Count; i++)
        {
            for (int j = i + 1; j < rooms.Count; j++)
            {
                Vector2 c1 = Center(rooms[i]);
                Vector2 c2 = Center(rooms[j]);
                float w = (c1 - c2).sqrMagnitude;
                edges.Add(new Edge { a = i, b = j, weight = w });
            }
        }
        return edges;
    }

    private void CreateCorridorBetween(RectInt a, RectInt b)
    {
        // Corridor should connect room edge -> other room edge (not room center -> room center).
        // We pick a primary axis by relative position between centers, then build an L-shaped path
        // that starts on an edge of room A and ends on an edge of room B.

        Vector2 ca = Center(a);
        Vector2 cb = Center(b);

        float dx = cb.x - ca.x;
        float dy = cb.y - ca.y;

        int halfW = corridorWidth / 2;
        int w = corridorWidth;

        bool preferHorizontal = Mathf.Abs(dx) >= Mathf.Abs(dy);

        if (preferHorizontal)
        {
            // Connect horizontally: A right/left edge -> B left/right edge
            bool bIsRight = dx >= 0f;

            int xStart = bIsRight ? (a.xMax - 1) : a.xMin; // inside A at the chosen side
            int xEnd = bIsRight ? b.xMin : (b.xMax - 1); // inside B at the chosen side

            // Choose y positions near the other room so the corridor enters/leaves close to edges.
            int yA = ClampIntToRange(Mathf.RoundToInt(cb.y), a.yMin, a.yMax - 1);
            int yB = ClampIntToRange(Mathf.RoundToInt(ca.y), b.yMin, b.yMax - 1);

            if (horizontalThenVertical)
            {
                // horizontal then vertical (corner at xEnd)
                AddRectIfInsideMap(RectFromSegment(
                    xMin: Mathf.Min(xStart, xEnd),
                    xMax: Mathf.Max(xStart, xEnd) + 1,
                    yMin: yA - halfW,
                    yMax: yA - halfW + w
                ));

                AddRectIfInsideMap(RectFromSegment(
                    xMin: xEnd - halfW,
                    xMax: xEnd - halfW + w,
                    yMin: Mathf.Min(yA, yB),
                    yMax: Mathf.Max(yA, yB) + 1
                ));
            }
            else
            {
                // vertical then horizontal (corner at yB)
                AddRectIfInsideMap(RectFromSegment(
                    xMin: xStart - halfW,
                    xMax: xStart - halfW + w,
                    yMin: Mathf.Min(yA, yB),
                    yMax: Mathf.Max(yA, yB) + 1
                ));

                AddRectIfInsideMap(RectFromSegment(
                    xMin: Mathf.Min(xStart, xEnd),
                    xMax: Mathf.Max(xStart, xEnd) + 1,
                    yMin: yB - halfW,
                    yMax: yB - halfW + w
                ));
            }
        }
        else
        {
            // Connect vertically: A top/bottom edge -> B bottom/top edge
            bool bIsAbove = dy >= 0f;

            int yStart = bIsAbove ? (a.yMax - 1) : a.yMin; // inside A at chosen side
            int yEnd = bIsAbove ? b.yMin : (b.yMax - 1);   // inside B at chosen side

            int xA = ClampIntToRange(Mathf.RoundToInt(cb.x), a.xMin, a.xMax - 1);
            int xB = ClampIntToRange(Mathf.RoundToInt(ca.x), b.xMin, b.xMax - 1);

            if (horizontalThenVertical)
            {
                // We'll still respect the "horizontal then vertical" flag by swapping segments order appropriately:
                // Here vertical connection is the primary segment.
                // vertical then horizontal then? For simplicity: build vertical at xA first, then horizontal at yEnd (corner at yEnd).
                AddRectIfInsideMap(RectFromSegment(
                    xMin: xA - halfW,
                    xMax: xA - halfW + w,
                    yMin: Mathf.Min(yStart, yEnd),
                    yMax: Mathf.Max(yStart, yEnd) + 1
                ));

                AddRectIfInsideMap(RectFromSegment(
                    xMin: Mathf.Min(xA, xB),
                    xMax: Mathf.Max(xA, xB) + 1,
                    yMin: yEnd - halfW,
                    yMax: yEnd - halfW + w
                ));
            }
            else
            {
                // horizontal then vertical (corner at xA) variant
                AddRectIfInsideMap(RectFromSegment(
                    xMin: Mathf.Min(xA, xB),
                    xMax: Mathf.Max(xA, xB) + 1,
                    yMin: yStart - halfW,
                    yMax: yStart - halfW + w
                ));

                AddRectIfInsideMap(RectFromSegment(
                    xMin: xB - halfW,
                    xMax: xB - halfW + w,
                    yMin: Mathf.Min(yStart, yEnd),
                    yMax: Mathf.Max(yStart, yEnd) + 1
                ));
            }
        }
    }

    private static int ClampIntToRange(int value, int minInclusive, int maxInclusive)
    {
        if (minInclusive > maxInclusive) return minInclusive;
        return Mathf.Clamp(value, minInclusive, maxInclusive);
    }

    private RectInt RectFromSegment(int xMin, int xMax, int yMin, int yMax)
    {
        // Convert to RectInt(width/height) where xMax/yMax are exclusive.
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private void AddRectIfInsideMap(RectInt rect)
    {
        if (rect.width <= 0 || rect.height <= 0) return;
        // Clamp to map bounds.
        RectInt map = new RectInt(0, 0, mapWidth, mapHeight);
        rect = Intersect(rect, map);
        if (rect.width <= 0 || rect.height <= 0) return;

        GeneratedCorridors.Add(rect);
    }

    private static RectInt Intersect(RectInt a, RectInt b)
    {
        int xMin = Mathf.Max(a.xMin, b.xMin);
        int yMin = Mathf.Max(a.yMin, b.yMin);
        int xMax = Mathf.Min(a.xMax, b.xMax);
        int yMax = Mathf.Min(a.yMax, b.yMax);
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private static Vector2 Center(RectInt rect)
    {
        return new Vector2(rect.xMin + rect.width / 2f, rect.yMin + rect.height / 2f);
    }

    private static Vector2Int CenterCell(RectInt rect)
    {
        return new Vector2Int(rect.xMin + rect.width / 2, rect.yMin + rect.height / 2);
    }

    /// <summary>
    /// 父节点必须是已加载场景中的 Transform。若把 Project 里的 Prefab 资源拖到 Generated Root，
    /// Instantiate/SetParent 会触发 “Setting the parent of a transform which resides in a Prefab Asset…” 并刷屏。
    /// </summary>
    private Transform GetInstantiationRoot()
    {
        if (generatedRoot == null)
            return transform;

        if (!generatedRoot.gameObject.scene.IsValid())
        {
            Debug.LogWarning(
                $"{nameof(RectRoomDungeonGenerator2D)}：{nameof(generatedRoot)} 指向了 Prefab 资源或非场景物体，无法作为父节点。已自动使用本生成器物体。请在 Hierarchy 中建空物体并拖到此处，或留空。",
                this);
            return transform;
        }

        return generatedRoot;
    }

    private void ClearGenerated()
    {
        var root = GetInstantiationRoot();

        // Only clear objects created previously by this generator.
        // Tag them with a prefix in the name to avoid destroying unrelated children.
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (child.name.StartsWith("RectDungeon_"))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.DestroyObjectImmediate(child.gameObject);
                else
#endif
                    Destroy(child.gameObject);
            }
        }
    }

    private void LateUpdate()
    {
        // Instantiate once after Generate() is called.
        // (LateUpdate is used to keep it simple and avoid editor-time differences.)
    }

    private void OnDrawGizmosSelected()
    {
        DrawRectsGizmos(GeneratedRooms, Color.green * 0.7f);
        DrawRectsGizmos(GeneratedCorridors, Color.cyan * 0.6f);
    }

    private void DrawRectsGizmos(List<RectInt> rects, Color color)
    {
        foreach (var rect in rects)
        {
            Vector2 centerWorld = CellToWorldCenter(rect);
            Vector2 sizeWorld = new Vector2(rect.width * cellSize, rect.height * cellSize);

            Gizmos.color = color;
            Gizmos.DrawWireCube(new Vector3(centerWorld.x, centerWorld.y, 0f), new Vector3(sizeWorld.x, sizeWorld.y, 0.01f));
        }
    }

    private Vector2 CellToWorldCenter(RectInt rect)
    {
        float cx = (rect.xMin + rect.width / 2f) * cellSize + worldOrigin.x;
        float cy = (rect.yMin + rect.height / 2f) * cellSize + worldOrigin.y;
        return new Vector2(cx, cy);
    }

    private Vector2 CellToWorldSize(RectInt rect)
    {
        return new Vector2(rect.width * cellSize, rect.height * cellSize);
    }

    /// <summary>
    /// Call this after you have rects (rooms/corridors) and want physical objects.
    /// It is NOT automatically instantiated to avoid unexpected scene changes.
    /// </summary>
    [ContextMenu("Instantiate Rect Objects (Rooms+Corridors)")]
    public void InstantiateRectObjects()
    {
        if (GeneratedRooms.Count == 0 && GeneratedCorridors.Count == 0)
        {
            Generate();
        }

        var root = GetInstantiationRoot();

        bool tilemapOk = useTilemap && roomFloorTile != null;
        bool floorPrefabOk = !useTilemap && useFloorPrefabs && floorRoomPrefab != null;

        if (tilemapOk)
        {
            PaintFloorTilemap(root);
        }
        else if (useTilemap && roomFloorTile == null)
        {
            Debug.LogWarning($"{nameof(RectRoomDungeonGenerator2D)}：已启用 Tilemap 但未设置 Room Floor Tile（roomFloorTile），将跳过地板绘制。", this);
        }
        else if (floorPrefabOk)
        {
            GameObject corridorFloorPrefab = floorCorridorPrefab != null ? floorCorridorPrefab : floorRoomPrefab;
            foreach (var r in GeneratedRooms)
                CreateFloorVisual(root, "Room", r, floorRoomPrefab);
            foreach (var r in GeneratedCorridors)
                CreateFloorVisual(root, "Corridor", r, corridorFloorPrefab);
        }

        bool hasAnyFloorVisual = tilemapOk || floorPrefabOk;
        bool skipWalkableCollider = hideWalkableColliderWhenFloorPrefab && hasAnyFloorVisual;

        // Avoid duplicate colliders for corridor overlaps: simple key by rect.
        var created = new HashSet<RectKey>();

        if (!skipWalkableCollider)
        {
            foreach (var r in GeneratedRooms)
            {
                if (created.Add(new RectKey(r))) CreateRectObject(root, "RectDungeon_Room", r, rectPrefab);
            }
            foreach (var r in GeneratedCorridors)
            {
                if (created.Add(new RectKey(r))) CreateRectObject(root, "RectDungeon_Corridor", r, rectPrefab);
            }
        }

        if (buildBoundaryWalls)
        {
            BuildBoundaryWalls(root);
        }

        if (enableRoomFlow)
        {
            SetupRoomFlow(root);
        }
    }

    /// <summary>
    /// 为每个房间创建触发器，并注册到 RoomFlowController。
    /// </summary>
    private void SetupRoomFlow(Transform root)
    {
        if (roomFlowController == null)
            roomFlowController = GetComponent<RoomFlowController>();
        if (roomFlowController == null)
            roomFlowController = gameObject.AddComponent<RoomFlowController>();

        roomFlowController.generator = this;

        for (int i = 0; i < GeneratedRooms.Count; i++)
        {
            RectInt r = GeneratedRooms[i];
            Vector2 centerWorld = CellToWorldCenter(r);
            Vector2 sizeWorld = CellToWorldSize(r);

            var go = new GameObject($"RectDungeon_RoomArea_{i}_{r.xMin}_{r.yMin}_{r.width}_{r.height}");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(centerWorld.x, centerWorld.y, 0f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = sizeWorld;

            var area = go.AddComponent<RoomArea2D>();
            area.roomIndex = i;
            area.roomRect = r;

            roomFlowController.RegisterRoom(area);
        }
    }

    /// <summary>
    /// Instantiates floor prefabs per cell (no stretching).
    /// Colliders on tile prefabs are removed so only boundary walls block movement.
    /// </summary>
    private void CreateFloorVisual(Transform root, string kind, RectInt rect, GameObject prefab)
    {
        if (prefab == null || rect.width <= 0 || rect.height <= 0) return;

        var group = new GameObject($"RectDungeon_Floor_{kind}_{rect.xMin}_{rect.yMin}_{rect.width}_{rect.height}");
        group.transform.SetParent(root, false);

        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                Vector2 cellCenter = CellToWorldCenter(x, y);
                var tile = Instantiate(prefab, new Vector3(cellCenter.x, cellCenter.y, 0f), Quaternion.identity, group.transform);
                tile.name = $"Tile_{x}_{y}";

                foreach (var sr in tile.GetComponentsInChildren<SpriteRenderer>(true))
                    sr.sortingOrder = floorSortingOrder;

                foreach (var col in tile.GetComponentsInChildren<Collider2D>(true))
                {
                    if (Application.isPlaying)
                        Destroy(col);
                    else
                        DestroyImmediate(col);
                }
            }
        }
    }

    private void PaintFloorTilemap(Transform root)
    {
        EnsureFloorTilemapExists(root);

        if (floorTilemap == null)
            return;

        if (clearFloorTilemapBeforePaint)
            floorTilemap.ClearAllTiles();

        // Corridor tile fallback.
        TileBase corridorTile = corridorFloorTile != null ? corridorFloorTile : roomFloorTile;

        // 全局可走网格：用于判断某个边是否真的是“外边界”。
        // 这样房间与走廊连接处不会错误地画出一条完整边（看起来像断层）。
        bool[,] walkable = BuildWalkableGrid();

        // Paint corridors first, then rooms (room interior should look consistent; corridors only show where not covered by rooms).
        foreach (var c in GeneratedCorridors)
        {
            if (corridorTiles9 != null && corridorTiles9.center != null)
                PaintRectTiles9ByWalkable(floorTilemap, c, corridorTiles9, walkable);
            else
                PaintRectTiles(floorTilemap, c, corridorTile);
        }

        foreach (var r in GeneratedRooms)
        {
            if (roomTiles9 != null && roomTiles9.center != null)
                PaintRectTiles9ByWalkable(floorTilemap, r, roomTiles9, walkable);
            else
                PaintRectTiles(floorTilemap, r, roomFloorTile);
        }

        floorTilemap.CompressBounds();

        if (paintWallTiles)
            PaintWallTilemap(root);
    }

    private void EnsureFloorTilemapExists(Transform root)
    {
        // Keep everything under Generated Root so ClearGenerated() can clean it.
        if (grid == null || !grid.gameObject.scene.IsValid())
        {
            var go = new GameObject("RectDungeon_Grid");
            go.transform.SetParent(root, false);
            grid = go.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
        }

        // Match generator cellSize/worldOrigin to the grid.
        grid.cellSize = new Vector3(cellSize, cellSize, 1f);
        grid.transform.position = new Vector3(worldOrigin.x, worldOrigin.y, 0f);

        if (floorTilemap == null || !floorTilemap.gameObject.scene.IsValid())
        {
            var go = new GameObject("RectDungeon_FloorTilemap");
            go.transform.SetParent(grid.transform, false);
            floorTilemap = go.AddComponent<Tilemap>();
            floorTilemapRenderer = go.AddComponent<TilemapRenderer>();
        }

        if (floorTilemapRenderer == null)
            floorTilemapRenderer = floorTilemap.GetComponent<TilemapRenderer>();

        if (floorTilemapRenderer != null)
        {
            floorTilemapRenderer.sortingOrder = floorSortingOrder;
            floorTilemapRenderer.sortOrder = TilemapRenderer.SortOrder.BottomLeft;
        }
    }

    private void EnsureWallTilemapExists(Transform root)
    {
        // Wall tilemap shares the same Grid (so it aligns with floor).
        EnsureFloorTilemapExists(root);

        if (grid == null)
            return;

        if (wallTilemap == null || !wallTilemap.gameObject.scene.IsValid())
        {
            var go = new GameObject("RectDungeon_WallTilemap");
            go.transform.SetParent(grid.transform, false);
            wallTilemap = go.AddComponent<Tilemap>();
            wallTilemapRenderer = go.AddComponent<TilemapRenderer>();
        }

        if (wallTilemapRenderer == null)
            wallTilemapRenderer = wallTilemap.GetComponent<TilemapRenderer>();

        if (wallTilemapRenderer != null)
        {
            wallTilemapRenderer.sortingOrder = wallSortingOrder;
            wallTilemapRenderer.sortOrder = TilemapRenderer.SortOrder.BottomLeft;
        }
    }

    private void PaintWallTilemap(Transform root)
    {
        // Need at least one wall tile to do meaningful work.
        if (wallTopTile == null && wallBottomTile == null && wallLeftTile == null && wallRightTile == null
            && outerCornerTopLeftTile == null && outerCornerTopRightTile == null && outerCornerBottomLeftTile == null && outerCornerBottomRightTile == null
            && innerCornerTopLeftTile == null && innerCornerTopRightTile == null && innerCornerBottomLeftTile == null && innerCornerBottomRightTile == null)
            return;

        EnsureWallTilemapExists(root);
        if (wallTilemap == null)
            return;

        if (clearWallTilemapBeforePaint)
            wallTilemap.ClearAllTiles();

        bool[,] walkable = BuildWalkableGrid();

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (!walkable[x, y]) continue;

                // Neighbor checks: if outside map or not walkable => boundary edge.
                bool wUp = IsWalkable(walkable, x, y + 1);
                bool wDown = IsWalkable(walkable, x, y - 1);
                bool wLeft = IsWalkable(walkable, x - 1, y);
                bool wRight = IsWalkable(walkable, x + 1, y);

                bool emptyUp = !wUp;
                bool emptyDown = !wDown;
                bool emptyLeft = !wLeft;
                bool emptyRight = !wRight;

                if (emptyUp && wallTopTile != null)
                    SetWallTile(x, y, 0, 1, wallTopTile);
                if (emptyDown && wallBottomTile != null)
                    SetWallTile(x, y, 0, -1, wallBottomTile);
                if (emptyLeft && wallLeftTile != null)
                    SetWallTile(x, y, -1, 0, wallLeftTile);
                if (emptyRight && wallRightTile != null)
                    SetWallTile(x, y, 1, 0, wallRightTile);

                if (!paintCornerTiles)
                    continue;

                // Corner tiles override edge tiles (set after edges).
                // Outer corners: two adjacent sides are empty.
                if (emptyUp && emptyLeft && outerCornerTopLeftTile != null)
                    SetCornerTile(x, y, -1, 1, outerCornerTopLeftTile);
                if (emptyUp && emptyRight && outerCornerTopRightTile != null)
                    SetCornerTile(x, y, 1, 1, outerCornerTopRightTile);
                if (emptyDown && emptyLeft && outerCornerBottomLeftTile != null)
                    SetCornerTile(x, y, -1, -1, outerCornerBottomLeftTile);
                if (emptyDown && emptyRight && outerCornerBottomRightTile != null)
                    SetCornerTile(x, y, 1, -1, outerCornerBottomRightTile);

                // Inner corners: both orthogonal neighbors are walkable, but the diagonal is empty.
                bool wUpLeft = IsWalkable(walkable, x - 1, y + 1);
                bool wUpRight = IsWalkable(walkable, x + 1, y + 1);
                bool wDownLeft = IsWalkable(walkable, x - 1, y - 1);
                bool wDownRight = IsWalkable(walkable, x + 1, y - 1);

                if (wUp && wLeft && !wUpLeft && innerCornerTopLeftTile != null)
                    SetCornerTile(x, y, -1, 1, innerCornerTopLeftTile);
                if (wUp && wRight && !wUpRight && innerCornerTopRightTile != null)
                    SetCornerTile(x, y, 1, 1, innerCornerTopRightTile);
                if (wDown && wLeft && !wDownLeft && innerCornerBottomLeftTile != null)
                    SetCornerTile(x, y, -1, -1, innerCornerBottomLeftTile);
                if (wDown && wRight && !wDownRight && innerCornerBottomRightTile != null)
                    SetCornerTile(x, y, 1, -1, innerCornerBottomRightTile);
            }
        }

        wallTilemap.CompressBounds();
    }

    private void SetWallTile(int walkableX, int walkableY, int dx, int dy, TileBase tile)
    {
        int tx = wallTilePlacement == WallTilePlacement.OnWalkableCell ? walkableX : walkableX + dx;
        int ty = wallTilePlacement == WallTilePlacement.OnWalkableCell ? walkableY : walkableY + dy;
        wallTilemap.SetTile(new Vector3Int(tx, ty, 0), tile);
    }

    private void SetCornerTile(int walkableX, int walkableY, int cornerDx, int cornerDy, TileBase tile)
    {
        // When painting on walkable cells, corners are drawn as an overlay on the boundary floor cell.
        // When painting on empty neighbor cells, corners are drawn on the diagonal neighbor cell.
        int tx = wallTilePlacement == WallTilePlacement.OnWalkableCell ? walkableX : walkableX + cornerDx;
        int ty = wallTilePlacement == WallTilePlacement.OnWalkableCell ? walkableY : walkableY + cornerDy;
        wallTilemap.SetTile(new Vector3Int(tx, ty, 0), tile);
    }

    private static bool IsWalkable(bool[,] walkable, int x, int y)
    {
        if (walkable == null) return false;
        int w = walkable.GetLength(0);
        int h = walkable.GetLength(1);
        if (x < 0 || y < 0 || x >= w || y >= h) return false;
        return walkable[x, y];
    }

    private static void PaintRectTiles(Tilemap tilemap, RectInt rect, TileBase tile)
    {
        if (tilemap == null || tile == null) return;
        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
    }

    private static void PaintRectTiles9ByWalkable(Tilemap tilemap, RectInt rect, TileSet9 tiles, bool[,] walkable)
    {
        if (tilemap == null || tiles == null || tiles.center == null) return;

        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                bool emptyLeft = !IsWalkable(walkable, x - 1, y);
                bool emptyRight = !IsWalkable(walkable, x + 1, y);
                bool emptyBottom = !IsWalkable(walkable, x, y - 1);
                bool emptyTop = !IsWalkable(walkable, x, y + 1);

                TileBase t = tiles.center;

                // 角优先于边：仅当邻接方向真的是“外部空区域”才画角/边。
                if (emptyTop && emptyLeft && tiles.cornerTopLeft != null) t = tiles.cornerTopLeft;
                else if (emptyTop && emptyRight && tiles.cornerTopRight != null) t = tiles.cornerTopRight;
                else if (emptyBottom && emptyLeft && tiles.cornerBottomLeft != null) t = tiles.cornerBottomLeft;
                else if (emptyBottom && emptyRight && tiles.cornerBottomRight != null) t = tiles.cornerBottomRight;
                else if (emptyTop && tiles.edgeTop != null) t = tiles.edgeTop;
                else if (emptyBottom && tiles.edgeBottom != null) t = tiles.edgeBottom;
                else if (emptyLeft && tiles.edgeLeft != null) t = tiles.edgeLeft;
                else if (emptyRight && tiles.edgeRight != null) t = tiles.edgeRight;

                tilemap.SetTile(new Vector3Int(x, y, 0), t);
            }
        }
    }

    private Vector2 CellToWorldCenter(int cellX, int cellY)
    {
        float cx = worldOrigin.x + (cellX + 0.5f) * cellSize;
        float cy = worldOrigin.y + (cellY + 0.5f) * cellSize;
        return new Vector2(cx, cy);
    }

    private struct RectKey : IEquatable<RectKey>
    {
        public int xMin;
        public int yMin;
        public int w;
        public int h;

        public RectKey(RectInt r)
        {
            xMin = r.xMin;
            yMin = r.yMin;
            w = r.width;
            h = r.height;
        }

        public bool Equals(RectKey other) => xMin == other.xMin && yMin == other.yMin && w == other.w && h == other.h;
        public override bool Equals(object obj) => obj is RectKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(xMin, yMin, w, h);
    }

    private void CreateRectObject(Transform root, string prefix, RectInt rect, GameObject prefab)
    {
        if (rect.width <= 0 || rect.height <= 0) return;

        Vector2 centerWorld = CellToWorldCenter(rect);
        Vector2 sizeWorld = CellToWorldSize(rect);

        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, new Vector3(centerWorld.x, centerWorld.y, 0f), Quaternion.identity, root);
            go.name = $"{prefix}_{rect.xMin}_{rect.yMin}_{rect.width}_{rect.height}";

            // If prefab has a RectTransform or a SpriteRenderer, scaling rules are project-specific.
            // Here we only try to scale BoxCollider2D automatically (common for debug/physics prefabs).
            var col = go.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.size = sizeWorld;
                col.isTrigger = walkableRectCollidersAsTriggers;
            }
        }
        else
        {
            go = new GameObject($"{prefix}_{rect.xMin}_{rect.yMin}_{rect.width}_{rect.height}");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(centerWorld.x, centerWorld.y, 0f);

            if (addBoxColliderIfNoPrefab)
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.size = sizeWorld;
                col.isTrigger = walkableRectCollidersAsTriggers;
            }
        }
    }

    private void BuildBoundaryWalls(Transform root)
    {
        bool[,] walkable = BuildWalkableGrid();

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (!walkable[x, y]) continue;

                // Right wall
                if (x + 1 >= mapWidth || !walkable[x + 1, y])
                    CreateWallSegment(root, WallDir.Right, x, y);

                // Left wall
                if (x - 1 < 0 || !walkable[x - 1, y])
                    CreateWallSegment(root, WallDir.Left, x, y);

                // Top wall
                if (y + 1 >= mapHeight || !walkable[x, y + 1])
                    CreateWallSegment(root, WallDir.Top, x, y);

                // Bottom wall
                if (y - 1 < 0 || !walkable[x, y - 1])
                    CreateWallSegment(root, WallDir.Bottom, x, y);
            }
        }
    }

    private enum WallDir { Left, Right, Top, Bottom }

    private void CreateWallSegment(Transform root, WallDir dir, int cellX, int cellY)
    {
        // World boundary positions for the cell.
        float leftX = worldOrigin.x + cellX * cellSize;
        float rightX = worldOrigin.x + (cellX + 1) * cellSize;
        float bottomY = worldOrigin.y + cellY * cellSize;
        float topY = worldOrigin.y + (cellY + 1) * cellSize;

        Vector3 pos;
        Vector2 size;

        switch (dir)
        {
            case WallDir.Left:
                pos = new Vector3(leftX, worldOrigin.y + (cellY + 0.5f) * cellSize, 0f);
                size = new Vector2(wallThickness, cellSize);
                break;
            case WallDir.Right:
                pos = new Vector3(rightX, worldOrigin.y + (cellY + 0.5f) * cellSize, 0f);
                size = new Vector2(wallThickness, cellSize);
                break;
            case WallDir.Top:
                pos = new Vector3(worldOrigin.x + (cellX + 0.5f) * cellSize, topY, 0f);
                size = new Vector2(cellSize, wallThickness);
                break;
            default: // Bottom
                pos = new Vector3(worldOrigin.x + (cellX + 0.5f) * cellSize, bottomY, 0f);
                size = new Vector2(cellSize, wallThickness);
                break;
        }

        var wall = new GameObject($"RectDungeon_Wall_{dir}_{cellX}_{cellY}");
        wall.transform.SetParent(root, false);
        wall.transform.position = pos;

        var col = wall.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = wallsAreTriggers;
    }

    private bool[,] BuildWalkableGrid()
    {
        bool[,] walkable = new bool[mapWidth, mapHeight];

        foreach (var r in GeneratedRooms)
            MarkRectOnGrid(walkable, r);
        foreach (var r in GeneratedCorridors)
            MarkRectOnGrid(walkable, r);

        return walkable;
    }

    /// <summary>
    /// 供外部系统（如房间流程/门空气墙）查询可走网格。
    /// </summary>
    public bool[,] BuildWalkableGrid_Public() => BuildWalkableGrid();

    private void MarkRectOnGrid(bool[,] grid, RectInt rect)
    {
        if (rect.width <= 0 || rect.height <= 0) return;

        int x0 = Mathf.Clamp(rect.xMin, 0, mapWidth - 1);
        int y0 = Mathf.Clamp(rect.yMin, 0, mapHeight - 1);
        int x1 = Mathf.Clamp(rect.xMax - 1, 0, mapWidth - 1);
        int y1 = Mathf.Clamp(rect.yMax - 1, 0, mapHeight - 1);

        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                grid[x, y] = true;
            }
        }
    }

    private class UnionFind
    {
        private readonly int[] parent;
        private readonly int[] rank;

        public UnionFind(int n)
        {
            parent = new int[n];
            rank = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
        }

        public int Find(int x)
        {
            if (parent[x] == x) return x;
            parent[x] = Find(parent[x]);
            return parent[x];
        }

        public void Union(int a, int b)
        {
            int ra = Find(a);
            int rb = Find(b);
            if (ra == rb) return;

            if (rank[ra] < rank[rb])
                parent[ra] = rb;
            else if (rank[ra] > rank[rb])
                parent[rb] = ra;
            else
            {
                parent[rb] = ra;
                rank[ra]++;
            }
        }
    }
}

