using System;
using UnityEngine;

/// <summary>
/// 房间触发器：玩家进入该区域时触发事件。
/// 由地图生成器在每个房间矩形上自动创建并配置。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class RoomArea2D : MonoBehaviour
{
    [NonSerialized] public int roomIndex = -1;
    [NonSerialized] public RectInt roomRect;

    public event Action<RoomArea2D, Collider2D> OnPlayerEntered;

    private BoxCollider2D _col;

    private void Awake()
    {
        _col = GetComponent<BoxCollider2D>();
        if (_col != null) _col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;
        OnPlayerEntered?.Invoke(this, other);
    }
}

