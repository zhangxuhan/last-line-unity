# 数值配置入口

在 Unity Project 窗口中选择：

`Assets/Resources/Task6/GameBalanceConfig.asset`

所有运行时系统都会读取这一个 `GameBalanceConfig` 资产。修改 Inspector 数值后直接进入 Play Mode 即可验证，不需要到脚本里逐项查找。

## 配置分区

- **Difficulty**：随生存时间增长的敌人生命、速度和生成间隔。
- **Waves**：首波预算、每波预算增长、同屏上限、波间休息和固定 Debug Seed。
- **Base Weapon**：初始伤害、射击间隔、弹速、弹道数、穿透和散射角。
- **Progression**：各等级所需经验与 5 级后的经验倍率。
- **Upgrade Rules**：射速/弹道/穿透上限、暴击倍率、连射节奏和闪电间隔。
- **Enemies**：每类敌人的生命、速度、体型、颜色、奖励、横向移动、盾牌格挡次数、巨型怪成长梯度和波次权重。
- **Rarities**：R/SR/SSR/UR 的抽取权重与各类升级幅度。
- **Upgrade Types**：每个技能词条的出现权重和最低稀有度。
- **Maximum Critical Chance**：暴击率硬上限。
- **Repeat Bonus Per Level / Maximum Repeat Bonus**：同名技能重复升级的额外成长。

## 调整数值时的注意点

- `Fire Interval Multiplier` 越小，射速提升越大。
- `Spawn Interval Multiplier Per Stage` 越小，后期生成越快。
- 敌人的 `Wave Weight Start/End` 是解锁波次区间内的线性权重；区间外保持起点或终点值。
- `Minimum Rarity` 会阻止技能以更低稀有度出现。
- 修改数组中的枚举键时，不要保留两个相同的 Enemy、Rarity 或 Upgrade Type 条目。
- 视觉布局、音量和纯表现参数仍保留在对应 Scene/Prefab 上，不属于战斗数值表。
