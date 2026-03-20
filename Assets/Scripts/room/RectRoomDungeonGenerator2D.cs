using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Random rect rooms + rect corridors (top-down 2D).
/// Rooms and corridors are axis-aligned rectangles in a discrete cell grid.
/// </summary>
public class RectRoomDungeonGenerator2D : MonoBehaviour
{
    [Header("Generation")]
    [Min(1)] public int minRoomCount = 5;
    [Min(1)] public int maxRoomCount = 10;

    [Min(1)] public int maxPlacementAttempts = 250;

    [Header("Map Bounds (cells)")]
    [Min(1)] public int mapWidth = 60;
    [Min(1)] public int mapHeight = 40;

    [Tooltip("World origin for cell (0,0). Usually set to the bottom-left of the map.")]
    public Vector2 worldOrigin = Vector2.zero;
    [Min(0.01f)] public float cellSize = 1f;

    [Header("Auto-Run")]
    [Tooltip("If true, Generate() will be called in Start().")]
    public bool generateOnStart = true;

    [Tooltip("If true, after Generate() it will instantiate room/corridor objects (colliders/visuals).")]
    public bool instantiateOnStart = true;

    [Header("Player Placement")]
    [Tooltip("If true, place the player at the initial room center during Generate().")]
    public bool placePlayerOnGenerate = true;

    [Tooltip("Optional: assign player transform explicitly. If null, generator will find GameObject with tag 'Player'.")]
    public Transform playerTransform;

    [Tooltip("Player tag used when playerTransform is not assigned.")]
    public string playerTag = "Player";

    [Header("Room Size (cells)")]
    [Min(1)] public int roomWidthMin = 6;
    [Min(1)] public int roomWidthMax = 14;
    [Min(1)] public int roomHeightMin = 6;
    [Min(1)] public int roomHeightMax = 14;

    [Tooltip("Extra padding around rooms to avoid overlaps (in cells).")]
    [Min(0)] public int roomPadding = 1;

    [Header("Corridors")]
    [Min(1)] public int corridorWidth = 2;
    [Min(1)] public int corridorLengthMin = 4;
    [Min(1)] public int corridorLengthMax = 18;

    [Tooltip("For each side midpoint of the initial room, chance to create a corridor to a new room.")]
    [Range(0f, 1f)] public float sideConnectionChance = 0.7f;

    [Min(1)] public int connectAttemptsPerSide = 40;

    [Header("Instantiate (optional)")]
    [Tooltip("Parent transform for generated objects.")]
    public Transform generatedRoot;

    [Tooltip("If provided, instantiate this prefab for each room/corridor rect (it should have BoxCollider2D or visuals).")]
    public GameObject rectPrefab;

    [Tooltip("If no prefab is provided, generator will create a GameObject with BoxCollider2D only.")]
    public bool addBoxColliderIfNoPrefab = true;

    [Header("Walls & Collision")]
    [Tooltip("Build wall colliders on the boundary between walkable cells and empty cells.")]
    public bool buildBoundaryWalls = true;

    [Min(0.01f)] public float wallThickness = 0.08f;
    [Tooltip("If true, wall colliders will be triggers (for blocking you typically want false).")]
    public bool wallsAreTriggers = false;

    [Tooltip("If we instantiate BoxCollider2D for rooms/corridors, set it as trigger so it won't block internal movement.")]
    public bool walkableRectCollidersAsTriggers = true;

    public readonly List<RectInt> GeneratedRooms = new List<RectInt>();
    public readonly List<RectInt> GeneratedCorridors = new List<RectInt>();

    [ContextMenu("Generate Dungeon")]
    public void Generate()
    {
        ClearGenerated();
        GeneratedRooms.Clear();
        GeneratedCorridors.Clear();

        // 1) Initial room (random rect, non-overlapping)
        var initialRoom = GenerateSingleRoom();
        GeneratedRooms.Add(initialRoom);

        // 2) Place player at initial room center
        if (placePlayerOnGenerate)
        {
            var target = playerTransform;
            if (target == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag(playerTag);
                if (playerObj != null) target = playerObj.transform;
            }

            if (target != null)
            {
                Vector2 c = CellToWorldCenter(initialRoom);
                target.position = new Vector3(c.x, c.y, target.position.z);
            }
        }

        // 3) From initial room, check 4 side midpoints and randomly connect to new rooms
        foreach (Side dir in Enum.GetValues(typeof(Side)))
        {
            if (UnityEngine.Random.value > sideConnectionChance) continue;
            TryConnectOneSide(dir, initialRoom);
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

    private void TryConnectOneSide(Side side, RectInt sourceRoom)
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
            return;
        }
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

    private void ClearGenerated()
    {
        var root = generatedRoot != null ? generatedRoot : transform;

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

        var root = generatedRoot != null ? generatedRoot : transform;

        // Avoid duplicate colliders for corridor overlaps: simple key by rect.
        var created = new HashSet<RectKey>();

        foreach (var r in GeneratedRooms)
        {
            if (created.Add(new RectKey(r))) CreateRectObject(root, "RectDungeon_Room", r, rectPrefab);
        }
        foreach (var r in GeneratedCorridors)
        {
            if (created.Add(new RectKey(r))) CreateRectObject(root, "RectDungeon_Corridor", r, rectPrefab);
        }

        if (buildBoundaryWalls)
        {
            BuildBoundaryWalls(root);
        }
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

