using UnityEngine;

/// <summary>
/// 2D 俯视角：摄像机跟随目标。仅对 X/Y 做 SmoothDamp，避免 Vector3 整体阻尼时 Z 速度残留带来的细微抖动。
/// 若目标带 Rigidbody2D，请在玩家上开启 Interpolate = Interpolate，与 LateUpdate 镜头配合最顺。
/// </summary>
[DefaultExecutionOrder(100)]
public class CameraFollow2D : MonoBehaviour
{
    [Tooltip("跟随目标；为空时会在运行时按 targetTag 查找")]
    public Transform target;

    [Tooltip("当 target 为空时，用该 Tag 查找（默认 Player）")]
    public string targetTag = "Player";

    [Tooltip("相对目标的偏移（2D 相机通常 z 为负，例如 -10）")]
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    [Tooltip("平滑时间（秒）：越大越跟得慢、越稳；过小容易跟得太紧产生「抖动感」。建议 0.2~0.45")]
    [Min(0f)]
    public float smoothTime = 0.32f;

    [Tooltip("可选：限制相机在 XY 平面上最大速度（世界单位/秒）。0 = 不限制")]
    [Min(0f)]
    public float maxFollowSpeed = 0f;

    [Header("减少抖动（可选）")]
    [Tooltip("将镜头 XY 对齐到像素网格，减轻像素风游戏的亚像素闪烁；非像素风可关")]
    public bool snapPositionToPixelGrid;

    [Tooltip("与精灵 Pixels Per Unit 一致即可，例如 16、32")]
    [Min(1f)]
    public float pixelsPerUnit = 32f;

    [Tooltip("目标与镜头期望位置在 XY 上都很近时不再微调，减少微幅抖动（世界单位）")]
    [Min(0f)]
    public float deadZone = 0.02f;

    /// <summary>X/Y 各自的速度缓冲（勿手动改）</summary>
    private float _velX;
    private float _velY;

    private void Start()
    {
        TryAssignTarget();
        if (target != null && smoothTime <= 0f)
            SnapToTarget();
    }

    private void LateUpdate()
    {
        TryAssignTarget();

        if (target == null)
            return;

        float dt = Time.deltaTime;
        float desiredX = target.position.x + offset.x;
        float desiredY = target.position.y + offset.y;
        float desiredZ = offset.z;

        if (smoothTime <= 0f)
        {
            transform.position = new Vector3(desiredX, desiredY, desiredZ);
            _velX = 0f;
            _velY = 0f;
            return;
        }

        float cx = transform.position.x;
        float cy = transform.position.y;

        if (deadZone > 0f)
        {
            if (Mathf.Abs(desiredX - cx) < deadZone)
                desiredX = cx;
            if (Mathf.Abs(desiredY - cy) < deadZone)
                desiredY = cy;
        }

        float maxSpeed = maxFollowSpeed > 0f ? maxFollowSpeed : Mathf.Infinity;

        float nx = Mathf.SmoothDamp(cx, desiredX, ref _velX, smoothTime, maxSpeed, dt);
        float ny = Mathf.SmoothDamp(cy, desiredY, ref _velY, smoothTime, maxSpeed, dt);

        if (snapPositionToPixelGrid)
        {
            float ppu = pixelsPerUnit;
            nx = Mathf.Round(nx * ppu) / ppu;
            ny = Mathf.Round(ny * ppu) / ppu;
        }

        transform.position = new Vector3(nx, ny, desiredZ);
    }

    /// <summary>切换目标或读档时可调用，避免镜头从远处慢慢飘过来</summary>
    public void SnapToTarget()
    {
        if (target == null)
            return;

        float x = target.position.x + offset.x;
        float y = target.position.y + offset.y;

        if (snapPositionToPixelGrid)
        {
            float ppu = pixelsPerUnit;
            x = Mathf.Round(x * ppu) / ppu;
            y = Mathf.Round(y * ppu) / ppu;
        }

        transform.position = new Vector3(x, y, offset.z);
        _velX = 0f;
        _velY = 0f;
    }

    private void TryAssignTarget()
    {
        if (target != null || string.IsNullOrEmpty(targetTag))
            return;

        var go = GameObject.FindGameObjectWithTag(targetTag);
        if (go != null)
            target = go.transform;
    }
}
