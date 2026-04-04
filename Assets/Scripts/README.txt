Scripts 目录结构（已整理，Prefab/场景引用脚本 GUID 不变，无需重挂）

Core/
  Data/          数值与公式：CombatStatsData、DamageCalculator
  Entity/        生命与受伤：Entity

Gameplay/
  Player/        玩家：状态机、受击、相机跟随、武器控制器
  Enemy/         敌人：EnemyController、状态机、EnemyData

Weapons/         武器与攻击判定：WeaponManager、WeaponData、AttackColliderHitScanner2D 等

World/
  Room/          地图生成、房间流程、Tilemap 相关

UI/              EntityHealthBar 等

说明：Unity 内若某文件夹图标异常，点一次 Assets -> Refresh 或重启编辑器即可。
