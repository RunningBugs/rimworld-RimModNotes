#!/usr/bin/env python3
"""RimWorld 1.6 房间观感值(Impressiveness)数学模型

公式来源(游戏 1.6.4871 反编译 Assembly-CSharp + Core/Defs XML):

  RoomStatWorker_Impressiveness.GetScore:
    fW = F(Wealth / 1500)
    fB = F(Beauty / 3)
    fS = F(Space / 125)
    fC = F(1 + min(Cleanliness, 0) / 2.5)
    F(x) = x                if |x| < 1
         = 1 + ln(x)        if x >= 1
         = -1 - ln(-x)      if x <= -1
    raw  = 0.65 * mean(fW,fB,fS,fC) + 0.35 * min(fW,fB,fS,fC)
    cap  = fS * 5
    if raw > cap: raw = 0.25*raw + 0.75*cap
    Impressiveness = raw * 100

  Space (RoomStatWorker_Space): 站立格 1.4, 可通行但不可站立格(床/雕塑 PassThroughOnly) 0.5, 上限 350
  Beauty (RoomStatWorker_Beauty): 全房间格美观之和 / CellCountCurve(n)
         CellCountCurve: n<=40 -> 20+0.5n ; n>40 -> n   (小房间除数远小于 n, 美观被浓缩放大)
         多格物品(雕塑)美观只计一次; Fillage!=Full 时地形美观仍计入该格
  Wealth (RoomStatWorker_Wealth): 房间内建筑(床/雕塑/门) MarketValue + 每格地形 MarketValue
         墙不计入(墙不在房间 Region 内); MarketValue 不受品质影响(1.6 无 StatPart_Quality)
  Cleanliness: 普通地板 0 -> fC = 1

常数来源:
  SculptureLarge: Beauty 100 (Buildings_Art.xml), 1 格, PassThroughOnly, costStuffCount 100, WorkToMake 30000
  Beauty 品质倍率 (Stats_Basics_General.xml StatPart_Quality): legendary = 8  -> 传奇钢大雕塑美观 = 100*1*8 = 800
  MarketValue = 材料成本 + work*0.0036 (StatWorker_MarketValue.CalculatedBaseMarketValue)
    钢雕塑 = 100*1.9 + 30000*0.0036 = 298
  Bed (1x2, PassThroughOnly, Beauty 1): 木 45*1.2+800*0.0036 = 56.88; 钢 45*1.9+... = 88.38
  Door 木: 25*1.2 + 850*0.0036 = 33.06, 门格算房间格(站立 1.4)
  地板(每格 MV/美观/清洁): 木地板 3.906/0/0; 水泥 2.26/-1/0; 石板 4.88/0/0;
    银砖 72.88/4/0.2; 金砖 702.88/11/0.2   (Cleanliness>=0 时 fC 恒为 1)

思想阶段 (Thoughts_Memory_RoomStats.xml SleptInBedroom + Rooms/RoomStats.xml Impressiveness scoreStages):
  <20 awful -2 | 20~30 dull 无思想 | 30 mediocre +1 | 40 decent +2 | 50 slightly +3 |
  65 (somewhat) impressive +4 | 85 very +5 | 120 extremely +6 | 170 unbelievably +7 | 240 wondrously +8
"""
import math

# ---------- 基础公式 ----------

def F(x):
    """Impressiveness 的 GetFactor 分段函数"""
    if abs(x) < 1.0:
        return x
    return (1.0 + math.log(x)) if x > 0 else (-1.0 - math.log(-x))

def beauty_divisor(n):
    """CellCountCurve: (0,20),(40,40),(100000,100000)"""
    return 20.0 + 0.5 * n if n <= 40 else float(n)

STAGES = [  # (minScore, label, mood)  SleptInBedroom
    (240, "wondrously impressive 不可思议", 8),
    (170, "unbelievably impressive 难以置信", 7),
    (120, "extremely impressive 极其印象深刻", 6),
    (85,  "very impressive 非常印象深刻", 5),
    (65,  "impressive 印象深刻", 4),
    (50,  "slightly impressive 略显深刻", 3),
    (40,  "decent 体面", 2),
    (30,  "mediocre 一般", 1),
    (20,  "dull 乏味 (无思想)", 0),
    (-999, "awful 糟糕", -2),
]

def stage_of(score):
    for thr, label, mood in STAGES:
        if score >= thr:
            return label, mood
    return STAGES[-1][1], -2

# ---------- 材料/物品常数 ----------

SCULPT = dict(beauty=800.0, mv=298.0, cells=1)          # 传奇钢大雕塑
BED = dict(
    normal=dict(beauty=1.0, mv_wood=56.88, mv_steel=88.38),
    legendary=dict(beauty=8.0, mv_wood=56.88, mv_steel=88.38),
)
DOOR_WOOD_MV = 33.06
FLOORS = dict(  # 每格: (mv, beauty, cleanliness)
    wood=dict(mv=3.906, beauty=0.0, clean=0.0, label="木地板"),
    concrete=dict(mv=2.26, beauty=-1.0, clean=0.0, label="水泥地"),
    paved=dict(mv=4.88, beauty=0.0, clean=0.0, label="石板砖"),
    silver=dict(mv=72.88, beauty=4.0, clean=0.2, label="银砖"),
    gold=dict(mv=702.88, beauty=11.0, clean=0.2, label="金砖"),
)

# ---------- 房间计算 ----------

def room_score(n_cells, floor="wood", bed_stuff="wood", bed_quality="normal",
               furniture_cells=3, extra_beauty=0.0, extra_wealth=0.0):
    """n_cells: 房间总格数(含门格)。家具: 床2格+雕塑1格, 均 PassThroughOnly(每格空间 0.5)"""
    f = FLOORS[floor]
    bq = BED[bed_quality]
    bed_mv = bq["mv_steel"] if bed_stuff == "steel" else bq["mv_wood"]

    standable = n_cells - furniture_cells            # 门格+空格: 1.4
    space = 1.4 * standable + 0.5 * furniture_cells  # 床/雕塑格: 0.5
    space = min(space, 350.0)

    beauty_sum = (SCULPT["beauty"] + bq["beauty"] + extra_beauty
                  + f["beauty"] * n_cells)
    beauty = beauty_sum / beauty_divisor(n_cells)

    wealth = (SCULPT["mv"] + bed_mv + DOOR_WOOD_MV + extra_wealth
              + f["mv"] * n_cells)

    clean = f["clean"]  # 物品清洁度皆为 0, 地板即房间平均

    fW, fB, fS = F(wealth / 1500.0), F(beauty / 3.0), F(space / 125.0)
    fC = F(1.0 + min(clean, 0.0) / 2.5)
    raw = 0.65 * (fW + fB + fS + fC) / 4.0 + 0.35 * min(fW, fB, fS, fC)
    cap = fS * 5.0
    capped = raw > cap
    if capped:
        raw = 0.25 * raw + 0.75 * cap
    score = raw * 100.0
    label, mood = stage_of(score)
    return dict(n=n_cells, space=space, beauty=beauty, wealth=wealth, clean=clean,
                fW=fW, fB=fB, fS=fS, fC=fC, score=score, stage=label, mood=mood,
                capped=capped)

def scan(floor="wood", bed_stuff="wood", bed_quality="normal", n_range=range(5, 61)):
    rows = [room_score(n, floor, bed_stuff, bed_quality) for n in n_range]
    return rows

def min_n_for_mood(mood_target, **kw):
    for n in range(5, 2000):
        r = room_score(n, **kw)
        if r["mood"] >= mood_target:
            return n, r
    return None, None

if __name__ == "__main__":
    print("=" * 100)
    print("配置: 1 床(木) + 1 传奇钢大雕塑 + 木门, 地板=木地板, n=房间总格数(含门)")
    print("=" * 100)
    print(f"{'n':>4} {'Space':>7} {'Beauty':>8} {'Wealth':>8} {'fS':>6} {'fB':>6} {'fW':>6} "
          f"{'score':>7} {'cap?':>4}  {'stage':<28} {'mood':>4}")
    for r in scan():
        print(f"{r['n']:>4} {r['space']:>7.1f} {r['beauty']:>8.2f} {r['wealth']:>8.0f} "
              f"{r['fS']:>6.3f} {r['fB']:>6.3f} {r['fW']:>6.3f} {r['score']:>7.1f} "
              f"{'Y' if r['capped'] else '':>4}  {r['stage']:<28} {r['mood']:>+4d}")

    print()
    print("=" * 100)
    print("各心情档位所需最小格数 (不同地板/床配置对比)")
    print("=" * 100)
    configs = [
        dict(floor="wood", bed_stuff="wood", bed_quality="normal", name="木地板+木床"),
        dict(floor="paved", bed_stuff="wood", bed_quality="normal", name="石板砖+木床"),
        dict(floor="silver", bed_stuff="wood", bed_quality="normal", name="银砖+木床"),
        dict(floor="gold", bed_stuff="wood", bed_quality="normal", name="金砖+木床"),
        dict(floor="wood", bed_stuff="wood", bed_quality="legendary", name="木地板+传奇木床"),
    ]
    header = f"{'配置':<18}" + "".join(f"{'+'+str(m)+'心情':>10}" for m in [1, 2, 3, 4, 5, 6, 7, 8])
    print(header)
    for cfg in configs:
        name = cfg.pop("name")
        line = f"{name:<18}"
        for m in [1, 2, 3, 4, 5, 6, 7, 8]:
            n, r = min_n_for_mood(m, **cfg)
            line += f"{('n='+str(n)) if n else '不可达':>10}"
        print(line)

    # 找每种地板下单格效率峰值和分数峰值
    print()
    print("=" * 100)
    print("分数随格数变化的峰值位置 (木地板)")
    print("=" * 100)
    best = None
    for r in scan(floor="wood", n_range=range(5, 400)):
        if best is None or r["score"] > best["score"]:
            best = r
    print(f"木地板峰值: n={best['n']}, score={best['score']:.1f}, stage={best['stage']}, mood={best['mood']:+d}")
    for fl in ["paved", "silver", "gold"]:
        best = None
        for r in scan(floor=fl, n_range=range(5, 600)):
            if best is None or r["score"] > best["score"]:
                best = r
        print(f"{FLOORS[fl]['label']}峰值: n={best['n']}, score={best['score']:.1f}, stage={best['stage']}, mood={best['mood']:+d}")
