using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarController : MonoBehaviour
{
    // 1. 在 Inspector 中拖入你的 Fill 物件 (Image Type 必須設為 Filled)
    [Header("UI References")]
    public Image foreground;

    // 2. 配置屬性
    [Header("Health Settings")]
    [Tooltip("角色的總生命值")]
    public float maxHealth = 100f;

    private float currentHealth; // 當前生命值

    // 3. 緩衝條設置 (可選)
    [Header("Visual Settings")]
    [Tooltip("受傷後血條恢復到真實血量的速度 (0 為立即更新)")]
    public float healthUpdateSpeed = 5f;

    // 編輯器專用變數，用於追蹤上一次的 fillAmount
    private float targetFillAmount;

    void Start()
    {
        // 初始化
        currentHealth = maxHealth;

        // 確保 UI 初始化為滿血
        if (foreground != null)
        {
            foreground.type = Image.Type.Filled; // 強制設為 Filled (如果你忘記在 Inspector 設置)
            foreground.fillAmount = 1f;
        }
    }

    void Update()
    {
        // 模擬受傷 (按下空格減少 20 血)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TakeDamage(20f);
        }

        // 平滑更新 UI (可選效果)
        UpdateHealthBarVisual();
    }

    /// <summary>
    /// 受傷函數 (外部調用)
    /// </summary>
    /// <param name="damage">傷害數值</param>
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth); // 限制範圍

        // 計算目標填充比例
        targetFillAmount = currentHealth / maxHealth;

        // 觸發 UI 更新
        UpdateHealthBarVisual();
    }

    /// <summary>
    /// 回血函數 (外部調用)
    /// </summary>
    /// <param name="healAmount">回復數值</param>
    public void Heal(float healAmount)
    {
        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth); // 限制範圍

        // 計算目標填充比例
        targetFillAmount = currentHealth / maxHealth;

        // 觸發 UI 更新
        UpdateHealthBarVisual();
    }

    /// <summary>
    /// 更新血條視覺效果 (支持平滑過渡)
    /// </summary>
    private void UpdateHealthBarVisual()
    {
        if (foreground == null) return;

        // 立即更新模式
        if (healthUpdateSpeed <= 0)
        {
            foreground.fillAmount = targetFillAmount;
        }
        else
        {
            // 平滑過渡模式 (血條會慢慢減少，看起來更柔和)
            foreground.fillAmount = Mathf.Lerp(foreground.fillAmount, targetFillAmount, Time.deltaTime * healthUpdateSpeed);
        }
    }
}