using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将 Slider 血条与 <see cref="Entity"/> 的生命值同步，并抵消父物体翻转（如玩家/敌人 scale.x 为负）导致的血条镜像。
/// 血条物体建议作为角色的子物体；若使用屏幕空间 Canvas 单独跟随，可关闭「抵消父级翻转」。
/// 可挂在带 Slider 的物体上，或挂在父物体上（会自动在子级查找 Slider）。
/// </summary>
public class EntityHealthBar : MonoBehaviour
{
    [Header("数据")]
    [Tooltip("为空则从父级查找 Entity")]
    [SerializeField] private Entity entity;

    [Header("翻转")]
    [Tooltip("为 true 时，每帧根据父级 lossyScale 修正本物体 localScale，使血条不被水平镜像")]
    [SerializeField] private bool cancelParentScaleFlip = true;

    [Tooltip("仅修正 X 轴（2D 角色翻转常用）；若血条仍异常可改为三轴都按 lossyScale 抵消")]
    [SerializeField] private bool onlyCompensateScaleX = true;

    [Tooltip("父级 lossyScale 若过小（如 CanvasScaler 0.01）或过大，不做翻转抵消，避免 localScale 被除爆导致血条极长")]
    [SerializeField] private float minParentScaleAbs = 0.25f;

    [SerializeField] private float maxParentScaleAbs = 4f;

    [Tooltip("补偿后 localScale 单轴绝对值上限，防止异常层级仍把条拉爆")]
    [SerializeField] private float maxLocalScaleAbs = 8f;

    private Slider _slider;
    private Vector3 _initialLocalScale;

    private void Awake()
    {
        _slider = GetComponent<Slider>() ?? GetComponentInChildren<Slider>(true);
        _initialLocalScale = transform.localScale;

        if (entity == null)
            entity = GetComponentInParent<Entity>();
    }

    private void OnEnable()
    {
        if (entity != null)
        {
            entity.OnHealthChanged += HandleHealthChanged;
            entity.OnDied += HandleDied;
            RefreshImmediate();
        }
    }

    private void OnDisable()
    {
        if (entity != null)
        {
            entity.OnHealthChanged -= HandleHealthChanged;
            entity.OnDied -= HandleDied;
        }
    }

    private void LateUpdate()
    {
        if (cancelParentScaleFlip && transform.parent != null)
            CompensateParentFlip();
    }

    /// <summary>
    /// 抵消父级在 X（或三轴）上的缩放，避免子物体血条跟着翻面。
    /// 注意：若父级是 Canvas / 带 CanvasScaler 的节点，lossyScale.x 可能为 0.01 量级，
    /// 直接 1/ps.x 会让 localScale 变成上百倍 → 血条被拉得极长，因此只在「接近角色翻转」的尺度范围内才补偿。
    /// </summary>
    private void CompensateParentFlip()
    {
        Transform p = transform.parent;
        Vector3 ps = p.lossyScale;

        if (onlyCompensateScaleX)
        {
            float absPx = Mathf.Abs(ps.x);
            // 过小：多为 UI/Canvas 缩放；过大：异常层级。都不做除法补偿。
            if (absPx < minParentScaleAbs || absPx > maxParentScaleAbs)
                return;

            float denom = Mathf.Sign(ps.x) * Mathf.Max(absPx, minParentScaleAbs);
            Vector3 ls = transform.localScale;
            ls.x = Mathf.Clamp(_initialLocalScale.x / denom, -maxLocalScaleAbs, maxLocalScaleAbs);
            transform.localScale = ls;
        }
        else
        {
            if (!IsSafeScale(ps.x) || !IsSafeScale(ps.y) || !IsSafeScale(ps.z))
                return;
            float dx = Mathf.Sign(ps.x) * Mathf.Max(Mathf.Abs(ps.x), minParentScaleAbs);
            float dy = Mathf.Sign(ps.y) * Mathf.Max(Mathf.Abs(ps.y), minParentScaleAbs);
            float dz = Mathf.Sign(ps.z) * Mathf.Max(Mathf.Abs(ps.z), minParentScaleAbs);
            transform.localScale = new Vector3(
                Mathf.Clamp(_initialLocalScale.x / dx, -maxLocalScaleAbs, maxLocalScaleAbs),
                Mathf.Clamp(_initialLocalScale.y / dy, -maxLocalScaleAbs, maxLocalScaleAbs),
                Mathf.Clamp(_initialLocalScale.z / dz, -maxLocalScaleAbs, maxLocalScaleAbs));
        }
    }

    private bool IsSafeScale(float v)
    {
        float a = Mathf.Abs(v);
        return a >= minParentScaleAbs && a <= maxParentScaleAbs;
    }

    private void HandleHealthChanged(int current, int max)
    {
        ApplySlider(current, max);
    }

    private void HandleDied()
    {
        ApplySlider(0, entity != null ? entity.MaxHealth : 1);
    }

    private void RefreshImmediate()
    {
        if (entity == null) return;
        if (_slider == null)
            _slider = GetComponent<Slider>() ?? GetComponentInChildren<Slider>(true);
        if (_slider == null) return;
        ApplySlider(entity.CurrentHealth, entity.MaxHealth);
    }

    private void ApplySlider(int current, int max)
    {
        if (_slider == null) return;
        max = Mathf.Max(1, max);
        current = Mathf.Clamp(current, 0, max);
        _slider.normalizedValue = (float)current / max;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (entity == null)
            entity = GetComponentInParent<Entity>();
    }
#endif
}
