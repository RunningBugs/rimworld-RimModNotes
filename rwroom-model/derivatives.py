#!/usr/bin/env python3
"""观感值 G 对四个子统计的偏导数推导与数值验证

记号: G = 100 * g(fW, fB, fS, fC)

未达空间上限区 (raw <= 5*fS):
    g = 13/80 * Σf + 7/20 * min(f)
    ∂g/∂fi = 13/80 + (7/20)·[i 是最小因子]          ×100 → 16.25 / 51.25

空间上限区 (raw > 5*fS, 小房间):
    g = (1/4)(13/80·Σf + 7/20·min) + (15/4)·fS
    ∂g/∂fi = 13/320 + (7/80)·[i 是最小因子] + (15/4)·[i=S]   ×100 → 4.0625 / 12.8125 / 379.0625(387.8125)

统计量 → 因子 的链式法则:
    fW = F(W/1500)  → ∂fW/∂W = F'(W/1500)/1500
    fB = F(B/3)     → ∂fB/∂B = F'(B/3)/3
    fS = F(S/125)   → ∂fS/∂S = F'(S/125)/125
    fC: C>=0 → 0 ; C<0 → ∂fC/∂C = F'(1+C/2.5)/2.5
    F'(x) = 1 (|x|<1);  1/x (x>1);  -1/x (x<-1)   即 1/max(|x|,1)

几何耦合 (加 1 个站立格 n→n+1):
    ∂S/∂n = 1.4 ;  ∂W/∂n = 地板MV ;  B=Q/d(n) → ∂B/∂n = -Q·d'/d²
    d(n) = 20+n/2 (n<=40, d'=0.5) 或 n (n>40, d'=1)
"""
import math
from model import room_score, F, FLOORS, SCULPT, BED, DOOR_WOOD_MV, beauty_divisor

def Fp(x):
    return 1.0 / max(abs(x), 1.0)

def factor_partials(n, floor="wood", bed_stuff="wood", bed_quality="normal"):
    """返回 (∂G/∂fW, ∂G/∂fB, ∂G/∂fS, ∂G/∂fC, regime)"""
    r = room_score(n, floor, bed_stuff, bed_quality)
    fs = dict(W=r["fW"], B=r["fB"], S=r["fS"], C=r["fC"])
    argmin = min(fs, key=fs.get)
    if not r["capped"]:
        d = {i: 16.25 + (35.0 if i == argmin else 0.0) for i in "WBSC"}
        regime = "未封顶"
    else:
        d = {i: 4.0625 + (8.75 if i == argmin else 0.0) + (375.0 if i == "S" else 0.0) for i in "WBSC"}
        regime = "空间封顶"
    return d, argmin, regime, r

def stat_partials(n, floor="wood"):
    """∂G/∂W, ∂G/∂B, ∂G/∂S, ∂G/∂C — 单位统计量的边际分数"""
    d, argmin, regime, r = factor_partials(n, floor)
    dGdW = d["W"] * Fp(r["wealth"] / 1500.0) / 1500.0
    dGdB = d["B"] * Fp(r["beauty"] / 3.0) / 3.0
    dGdS = d["S"] * Fp(r["space"] / 125.0) / 125.0
    dGdC = 0.0 if r["clean"] >= 0 else d["C"] * Fp(1 + r["clean"] / 2.5) / 2.5
    return dict(W=dGdW, B=dGdB, S=dGdS, C=dGdC, argmin=argmin, regime=regime, r=r)

def per_tile(n, floor="wood"):
    """加 1 个站立格的净收益 ∂G/∂n (解析)"""
    p = stat_partials(n, floor)
    r = p["r"]
    Q = SCULPT["beauty"] + BED["normal"]["beauty"] + FLOORS[floor]["beauty"] * n
    d = beauty_divisor(n)
    dp = 0.5 if n <= 40 else 1.0
    dBdn = -Q * dp / d ** 2 + FLOORS[floor]["beauty"] / d   # 稀释 + 新格地板美观
    return 1.4 * p["S"] + FLOORS[floor]["mv"] * p["W"] + dBdn * p["B"]

def per_tile_fd(n, floor="wood"):
    """有限差分验证"""
    return room_score(n + 1, floor)["score"] - room_score(n, floor)["score"]

def beauty_item(n, floor="wood", beauty=800.0, wealth=298.0, label="传奇钢大雕塑"):
    """在房间内加一件美观物品的边际收益 (解析, 不占额外格: 美观和财富各加一份)"""
    p = stat_partials(n, floor)
    d = beauty_divisor(n)
    dG = p["B"] * (beauty / d) + p["W"] * wealth
    return dG

def beauty_item_fd(n, floor="wood", beauty=800.0, wealth=298.0):
    from model import room_score as rs
    base = rs(n, floor)["score"]
    new = rs(n, floor, extra_beauty=beauty, extra_wealth=wealth)["score"]
    return new - base

if __name__ == "__main__":
    print("=" * 110)
    print("一、单位统计量的边际分数 ∂G/∂(W,B,S) [木地板卧室: 1床+1传奇钢大雕塑]")
    print("=" * 110)
    print(f"{'n':>4} {'区间':<10}{'最小因子':<8}{'∂G/∂S':>10}{'∂G/∂B':>10}{'∂G/∂W':>10}{'∂G/∂C':>8}   解读")
    for n in [5, 7, 10, 13, 16, 17, 20, 25, 30, 40, 45, 60, 100, 150, 274]:
        p = stat_partials(n)
        print(f"{n:>4} {p['regime']:<10}{'f'+p['argmin']:<8}{p['S']:>10.4f}{p['B']:>10.4f}{p['W']:>10.5f}{p['C']:>8.3f}")

    print()
    print("=" * 110)
    print("二、三种杠杆的边际收益对比 (解析 vs 有限差分验证)")
    print("=" * 110)
    print(f"{'n':>4} {'+1格(解析)':>12}{'+1格(差分)':>12}{'+1传奇雕塑(解析)':>16}{'+1传奇雕塑(差分)':>16}{'+1000财富(解析)':>14}")
    for n in [5, 7, 10, 13, 16, 17, 20, 25, 30, 40, 60, 100, 150, 274]:
        pt, ptf = per_tile(n), per_tile_fd(n)
        bi, bif = beauty_item(n), beauty_item_fd(n)
        w = stat_partials(n)["W"] * 1000.0
        print(f"{n:>4} {pt:>12.3f}{ptf:>12.3f}{bi:>16.2f}{bif:>16.2f}{w:>14.2f}")

    print()
    print("=" * 110)
    print("三、每 1000 银投入的效率 (成本归一化)")
    print("=" * 110)
    # 假设: 加格成本≈15银(地板+摊薄墙体), 传奇钢大雕塑材料成本≈190银+工时(按500银估), 财富直接按面值
    print(f"{'n':>4} {'加格/1000银':>14}{'雕塑/1000银':>14}{'买财富/1000银':>14}")
    for n in [5, 10, 13, 20, 25, 40, 60, 100]:
        tile = per_tile(n) / 15.0 * 1000.0
        sc = beauty_item(n) / 500.0 * 1000.0
        w = stat_partials(n)["W"] * 1000.0
        print(f"{n:>4} {tile:>14.1f}{sc:>14.1f}{w:>14.1f}")
