# Ghoul Attack Spin 食尸鬼攻击旋转特效

RimWorld 1.6 小型视觉 Mod：食尸鬼触发普通近战攻击动画时，当前渲染的 pawn 会在短暂攻击动画窗口内旋转 360 度。

- 纯视觉效果：不修改伤害、命中、移动、AI、工作等逻辑。
- 只作用于 `Pawn.IsGhoul` 的 pawn。
- 旋转由 `Pawn_DrawTracker.Notify_MeleeAttackOn` 触发，和原版 melee jitter 动画窗口一致，结束后自动恢复。
