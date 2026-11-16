"""Generate the bilingual XML definitions; run with the project's .venv interpreter."""
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
TEXT = {
    "Title": ("Ranged Firepower Stats — DPS by range", "远程火力数据 · DPS 逐格表"),
    "Open": ("DPS range table", "DPS 逐格表"),
    "OpenDescription": ("Compare sustained ranged damage using this pawn's current stats and weather.", "按当前小人属性与天气，比较普通、射击专家及专家＋乱开枪的逐格 DPS。"),
    "Current": ("Current", "当前"),
    "Specialist": ("Shooting specialist", "射击专家"),
    "SpecialistTriggerHappy": ("Specialist + trigger-happy", "专家＋乱开枪"),
    "Unavailable": ("N/A", "不适用"),
    "AtRange": ("{0} cells", "{0} 格"),
    "Cycle": ("{0} × {1} shots / ({2} aim + {3} burst + {4} cooldown + {5} scheduling ticks) × 60 = {6} DPS (attack check: {7} ticks)", "{0} × {1} 发 ÷（预热 {2}＋连发 {3}＋冷却 {4}＋调度 {5} tick）× 60 ＝ {6} DPS（射击检查每 {7} tick）"),
    "Interval": ("Nominal inter-shot interval: {0} ticks (verb value: {1}). Pawn stance/equipment tick ordering shortens the first gap by one tick; later gaps have a one-tick minimum.", "标称两发间隔：{0} tick（Verb 读取值 {1}）。小人的姿态与装备更新顺序使第一段间隔少 1 tick；其余间隔至少 1 tick。"),
    "IdealShooter": ("No pawn: ideal shooter accuracy, normal aim 100%, specialist aim from loaded role/trait defs. Trigger-happy accuracy penalties require choosing a real pawn.", "未指定小人：以理想射手精度比较，普通瞄准时间为 100%，专家情景按已加载职位/特性定义计算。选择真实小人后才会计入乱开枪的精度惩罚。"),
    "StandardConditions": ("Current equipment, health, genes, skills and stats; standard standing body-size-1 target, clear weather, no cover/smoke. Open the table for current weather.", "使用当前装备、健康、基因、技能等属性；信息卡口径为体型 1 的站立目标、晴天、无遮蔽/烟雾。当前天气请打开逐格表。"),
    "TableAssumptions": ("Standard standing body-size-1 target; current weather and shooter stats. No cover, smoke, armor or darkness offsets in this distance-only table. The selected-target row uses vanilla ShotReport for its actual position.", "逐格表：体型 1、站立的标准目标，计入当前天气和射手属性；不含掩体、烟雾、护甲和目标明暗。下方真实目标行按其位置调用原版射击报告。"),
    "Limitations": ("Primary pre-armor damage; no acquisition, rearming or extra damage. Includes 1.7 attack-check cadence (camera-dependent). Pawn first-gap overlap: −1 tick; later gaps ≥1 tick.", "护甲前主伤害，不含索敌、补弹和附加伤害。计入 1.7 射击检查调度（随镜头缩放/可见性变化）；小人首段连发扣 1 tick，其余间隔至少 1 tick。"),
    "TurretTiming": ("Turret timing uses mean warmup plus cooldown and burst gaps. Excludes the up-to-14-tick target scan wait and ammunition work; sustained potential, not measured wall-clock DPS.", "炮塔以平均预热＋冷却＋连发间隔计算持续潜力；不含最多 14 tick 的索敌扫描等待和补弹工作。不是实测墙钟 DPS。"),
    "Unsupported": ("Exact single-target expectations depend on explosion, scatter and projectile mechanics. Available primary burst damage is shown separately.", "爆炸、散布和弹丸机制决定具体的单目标期望伤害；可读取的主伤害理论值单独显示。"),
    "Fuel": ("Maximum primary damage per ammunition resource: {0}. Uses fuel multiplier and fuel consumed per shot/burst.", "每单位弹药资源的最大主伤害：{0}。计入资源补充倍率和每发/每梭消耗。"),
    "OpenHint": ("Use the DPS range table button at the bottom of this card for a comparison table.", "点击信息卡底部的“DPS 逐格表”打开并排对比。"),
    "StatRow": ("{0} cells: {1} ({2} hit)", "{0} 格：{1}（命中 {2}）"),
    "NoMap": ("no map — clear reference", "无地图，采用晴天"),
    "IdealOption": ("Ideal shooter (no pawn)", "理想射手（未指定小人）"),
    "Weather": ("Weather: {0}; accuracy ×{1}", "天气：{0}；命中倍率 ×{1}"),
    "Distance": ("Cells", "距离/格"),
    "Hit": ("Hit", "命中率"),
    "Shooter": ("Shooter: {0}", "射手：{0}"),
    "Weapon": ("Weapon: {0}", "武器：{0}"),
    "TargetRoof": ("Standard target is roofed", "标准目标在屋顶下"),
    "Refresh": ("Refresh", "刷新"),
    "Copy": ("Copy table", "复制表格"),
    "Copied": ("DPS table copied (tab-separated).", "DPS 表格已复制，可粘贴到电子表格。"),
    "ClearTarget": ("Clear selected target", "清除真实目标"),
    "SelectTarget": ("Select real target", "选择真实目标"),
    "TargetHint": ("Optional: choose a map pawn for a vanilla ShotReport estimate, including cover, smoke, body size, posture and darkness. Values below remain the standard distance comparison.", "可选：指定地图小人，按原版射击报告计算（含掩体、烟雾、体型、姿势、明暗）。逐格表仍采用标准目标。"),
    "Column": ("Hit chance                  DPS", "命中率                       DPS"),
    "NeedIdeology": ("Enable Ideology to compare shooting specialists.", "启用 Ideology 后可比较射击专家情景。"),
    "GraphOpen": ("View line chart", "查看折线图"),
    "GraphTitle": ("Ranged Firepower Stats — Range curves", "远程火力数据 · 折线图"),
    "GraphLinkName": ("accuracy / DPS line chart", "命中率 / DPS 折线图"),
    "ShooterGraphLinkName": ("shooting accuracy line chart", "射击精度折线图"),
    "GraphDps": ("Expected DPS", "期望 DPS"),
    "GraphHit": ("Hit chance", "命中率"),
    "GraphShooter": ("Shooter accuracy factor", "射手精度系数"),
    "GraphHover": ("Hover over the plot to inspect the values at each distance.", "将鼠标移到图上，可查看对应距离的各条曲线数值。"),
    "GraphHoverRow": ("{0}: {1} hit, {2} DPS", "{0}：命中 {1}，DPS {2}"),
    "GraphReference": ("Clear-weather reference, matching the info-card distance table. DPS is primary damage before armor.", "晴天参考条件，与信息卡内的逐格表一致。DPS 为护甲前主伤害。"),
    "GraphSnapshot": ("Same snapshot as the range table. Standing body-size-1 target, {0}; no cover, smoke, armor or darkness offsets. DPS is primary damage before armor.", "沿用逐格表快照：体型 1、站立目标，{0}；不含掩体、烟雾、护甲和明暗偏移。DPS 为护甲前主伤害。"),
    "GraphRoofed": ("roofed target", "目标有屋顶"),
    "GraphUnroofed": ("unroofed target", "目标无屋顶"),
    "GraphShooterNote": ("Shooter accuracy only, matching the native accuracy table. Weapon accuracy, target conditions and weather are excluded.", "仅绘制射手精度，与原版射击精度表一致；不含武器精度、目标条件和天气。"),
}

STATS = [
    ("WeaponMax", "maximum ranged DPS", "远程最大 DPS", "Weapon"),
    ("WeaponAverage", "average ranged DPS", "远程期望 DPS", "Weapon"),
    ("WeaponSpecialist", "specialist ranged DPS (ideal)", "射击专家 DPS（理想精度）", "Weapon"),
    ("WeaponTrigger", "specialist + trigger-happy DPS (ideal)", "专家＋乱开枪 DPS（理想精度）", "Weapon"),
    ("PawnCurrent", "ranged DPS", "远程 DPS", "PawnCombat"),
    ("PawnSpecialist", "shooting specialist DPS", "射击专家 DPS", "PawnCombat"),
    ("PawnTrigger", "specialist + trigger-happy DPS", "专家＋乱开枪 DPS", "PawnCombat"),
    ("TurretMax", "maximum ranged DPS", "远程最大 DPS", "Weapon"),
    ("TurretWeapon", "weapon ranged DPS", "武器远程 DPS", "Weapon"),
    ("TurretCurrent", "ranged DPS", "远程 DPS", "Weapon"),
    ("ResourceMax", "maximum damage per resource", "每资源最大伤害", "Weapon"),
    ("ResourceAverage", "damage per resource", "每资源期望伤害", "Weapon"),
]

def write(path, root):
    path.parent.mkdir(parents=True, exist_ok=True)
    ET.indent(root, space="  ")
    ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)

defs = ET.Element("Defs")
category_def = ET.SubElement(defs, "StatCategoryDef")
for key, value in {"defName": "RFS_RangedFirepower", "label": "Ranged Firepower Stats", "displayOrder": "49", "displayAllByDefault": "true"}.items():
    ET.SubElement(category_def, key).text = value
for index, (name, label, zh, category) in enumerate(STATS):
    stat = ET.SubElement(defs, "StatDef")
    for key, value in {
        "defName": "RFS_" + name, "workerClass": "RangedFirepowerStats.StatWorker_Dps", "label": label,
        "description": "Sustained primary damage before armor. Select this stat for the tick-based calculation and per-cell values. Open the DPS range table to compare the current pawn under current weather. Without a pawn, weapon entries assume ideal shooter accuracy.",
        "category": "RFS_RangedFirepower", "defaultBaseValue": "0", "minValue": "0", "toStringStyle": "FloatTwo",
        "forInformationOnly": "true", "neverDisabled": "true", "cacheable": "false",
        "displayPriorityInCategory": str(5100 - index),
    }.items(): ET.SubElement(stat, key).text = value
write(ROOT / "Defs/Stats.xml", defs)
for language, position in [("English", 0), ("ChineseSimplified", 1)]:
    keyed = ET.Element("LanguageData")
    for key, translations in TEXT.items(): ET.SubElement(keyed, "RFS_" + key).text = translations[position]
    write(ROOT / "Languages" / language / "Keyed/Inspector.xml", keyed)
    if position == 1:
        category_injected = ET.Element("LanguageData")
        ET.SubElement(category_injected, "RFS_RangedFirepower.label").text = "远程火力数据"
        write(ROOT / "Languages" / language / "DefInjected/StatCategoryDef/Categories.xml", category_injected)
        injected = ET.Element("LanguageData")
        for name, label, zh, category in STATS:
            ET.SubElement(injected, "RFS_" + name + ".label").text = zh
            ET.SubElement(injected, "RFS_" + name + ".description").text = "护甲前的持续主伤害。点击属性查看按 tick 计算的完整周期和逐格数值；信息卡底部可打开当前小人、当前天气的对比表。无指定小人的武器属性以理想精度作比较。"
        write(ROOT / "Languages" / language / "DefInjected/StatDef/Stats.xml", injected)
