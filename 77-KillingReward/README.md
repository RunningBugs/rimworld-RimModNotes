# KillingReward 嗜血恩赐

我方小人亲手击杀敌对派系单位可积累「血祭」进度。进度打满即可获得黑暗超凡智能的恩赐（三选一）：

- **禁忌知识**：立刻完成一项当前可研究的科技
- **技艺灌注**：一名小人的一项技能 +3 级
- **虚空馈赠**：任选一种物品，在指定格子领取一整格

通过底部主按钮栏「嗜血恩赐」随时打开奖励窗口；每次升级会收到信件提醒。
初始要求 10 杀，之后每级 ×1.2（可在 Mod 设置中改为线性增长或调整参数）。

Kills by your own colonists against hostile factions fill a blood tithe. Each filled tithe earns a boon from the dark archotech: instantly complete a research, raise a pawn's skill by 3, or receive a full stack of an item of your choice at a cell you pick. Open the reward window anytime from the "Killing Reward" main button. Defaults: first tier 10 kills, ×1.2 per tier (linear mode and all parameters configurable in mod settings).

## 设计文档 / Docs

- [设计文档](docs/2026-07-31-KillingReward-design.md)
- [实现计划](docs/2026-07-31-KillingReward-implementation-plan.md)

## 测试 / Tests

```bash
cd Tests/unit && dotnet test   # C# 单元测试
bash Tests/run_whitebox.sh     # 静态白盒检查
```
