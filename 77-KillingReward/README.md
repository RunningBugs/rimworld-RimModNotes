# KillingReward 嗜血恩赐

我方小人亲手击杀敌对派系单位可积累「血祭」进度。进度打满即可获得黑暗超凡智能的恩赐（三选一）：

- **禁忌知识**：立刻完成一项当前可研究的科技
- **技艺灌注**：一名小人的一项技能 +3 级
- **虚空馈赠**：任选一种物品，在指定格子领取一整格
- **黑暗赐福**：全体殖民地成员共享的群体增益（含动物与机械体）。类别：
  - 「生命回复（3 天）」——持续愈合伤口，并无中生有地长回缺失的肢体与器官
  - 「特质修改」——为一名小人添加/移除一项特质
  - 「基因修改」（需 Biotech DLC）——为一名小人添加/移除一个系谱或异种基因，带搜索的选择界面
  - 特质与基因每次修改都会提高下次的恩赐点数消耗（指数/线性可调，全体共享计数）

通过底部主按钮栏「嗜血恩赐」随时打开奖励窗口；每次升级会收到信件提醒。
初始要求 10 杀，之后每级 ×1.2（可在 Mod 设置中改为线性增长或调整参数）。

Kills by your own colonists against hostile factions fill a blood tithe. Each filled tithe earns a boon from the dark archotech: instantly complete a research, raise a pawn's skill by 3, receive a full stack of an item of your choice at a cell you pick, or bestow a Dark Blessing shared by the whole colony — animals and mechs included. Blessing categories: Life Renewal (3 days of regeneration: wounds steadily close and missing limbs and organs regrow), Trait Reshape (add/remove a trait for one pawn), and Gene Rewrite (add/remove a germline or xenogene for one pawn, with a searchable picker; requires the Biotech DLC). Each trait/gene modification raises the next one's boon cost (exponential or linear, shared counter, configurable in mod settings). Open the reward window anytime from the "Killing Reward" main button. Defaults: first tier 10 kills, ×1.2 per tier (linear mode and all parameters configurable in mod settings).

## 设计文档 / Docs

- [设计文档](docs/2026-07-31-KillingReward-design.md)
- [实现计划](docs/2026-07-31-KillingReward-implementation-plan.md)
- [黑暗赐福设计](docs/2026-08-23-DarkBlessing-design.md)

## 测试 / Tests

```bash
cd Tests/unit && dotnet test   # C# 单元测试
bash Tests/run_whitebox.sh     # 静态白盒检查
```
