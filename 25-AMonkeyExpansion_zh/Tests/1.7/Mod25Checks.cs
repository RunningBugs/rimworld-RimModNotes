using System;
using System.Linq;
using RimWorld;
using Verse;
namespace RW17.Validation;
public static class Mod25Checks
{
    public const string PackageId = "RunningBugs.AMonkeyExpansionZh";
    public static void Run(Map map)
    {
        if (LanguageDatabase.activeLanguage?.LegacyFolderName != "ChineseSimplified")
            throw new InvalidOperationException("Mod25 requires an isolated ChineseSimplified language case");
        if (DefDatabase<HediffDef>.GetNamed("Slipped").label != "摔倒！")
            throw new InvalidOperationException("Mod25 injection failed: Slipped.label");
        if (DefDatabase<HediffDef>.GetNamed("Slipped").description != "踩中香蕉皮摔倒了")
            throw new InvalidOperationException("Mod25 injection failed: Slipped.description");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealFine").label != "烹饪精致香蕉大餐")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealFine.label");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealFine").description != "精心烹饪一顿香蕉大餐，食谱含有香蕉和蛋白质")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealFine.description");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealFine").jobString != "正在烹饪精致香蕉大餐")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealFine.jobString");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealFineBulk").label != "烹饪精致香蕉大餐 x4")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealFineBulk.label");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealFineBulk").description != "大锅烹饪批量的精致香蕉大餐，食谱含有香蕉和蛋白质")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealFineBulk.description");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealFineBulk").jobString != "正在烹饪四份精致香蕉大餐")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealFineBulk.jobString");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealLavish").label != "烹饪奢华香蕉大餐")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealLavish.label");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealLavish").description != "由厨艺大师精心处理的香蕉和蛋白质，制作一份奢华的香蕉大餐，完美体现了各种味道的平衡")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealLavish.description");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealLavish").jobString != "正在烹饪奢华香蕉大餐")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealLavish.jobString");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealLavishBulk").label != "烹饪奢华香蕉大餐 x4")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealLavishBulk.label");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealLavishBulk").description != "厨艺大师高效迅速地批量制作奢华的香蕉大餐，依旧使用精心处理的香蕉和蛋白质，味道平衡一点也不逊色于单独制作的大餐")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealLavishBulk.description");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealLavishBulk").jobString != "正在烹饪奢华香蕉大餐 x4")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealLavishBulk.jobString");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealSimple").label != "制作简单香蕉食品")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealSimple.label");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealSimple").description != "烹饪一份简单的香蕉食品")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealSimple.description");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealSimple").jobString != "正在烹饪一份简单的香蕉食品")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealSimple.jobString");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealSimpleBulk").label != "烹饪简单香蕉食品 x4")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealSimpleBulk.label");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealSimpleBulk").description != "批量烹饪简单香蕉食品")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealSimpleBulk.description");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCCookBananaMealSimpleBulk").jobString != "正在烹饪简单香蕉食品 x4")
            throw new InvalidOperationException("Mod25 injection failed: ARCCCookBananaMealSimpleBulk.jobString");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCMakeBananaSmoothie").label != "制作香蕉奶昔 x4")
            throw new InvalidOperationException("Mod25 injection failed: ARCCMakeBananaSmoothie.label");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCMakeBananaSmoothie").description != "将一些香蕉打碎成美味的糊状，冷藏后倒入杯中。当然一定要加上附赠的小伞和吸管。没有小伞和吸管可完全不像热带风格。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCMakeBananaSmoothie.description");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCMakeBananaSmoothie").jobString != "正在制作香蕉奶昔 x4")
            throw new InvalidOperationException("Mod25 injection failed: ARCCMakeBananaSmoothie.jobString");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCMakeBananaSmoothieBulk").label != "制作香蕉奶昔 x20")
            throw new InvalidOperationException("Mod25 injection failed: ARCCMakeBananaSmoothieBulk.label");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCMakeBananaSmoothieBulk").description != "将巨量的香蕉打碎成美味的糊状，冷藏后倒入杯中。当然一定要加上附赠的小伞和吸管。没有小伞和吸管可完全不像热带风格。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCMakeBananaSmoothieBulk.description");
        if (DefDatabase<RecipeDef>.GetNamed("ARCCMakeBananaSmoothieBulk").jobString != "正在制作香蕉奶昔 x20")
            throw new InvalidOperationException("Mod25 injection failed: ARCCMakeBananaSmoothieBulk.jobString");
        if (DefDatabase<RecipeDef>.GetNamed("CureScariaBanacea").label != "使用香蕉神药治疗疤痕")
            throw new InvalidOperationException("Mod25 injection failed: CureScariaBanacea.label");
        if (DefDatabase<RecipeDef>.GetNamed("CureScariaBanacea").description != "使用香蕉神药的神效来治疗疤痕")
            throw new InvalidOperationException("Mod25 injection failed: CureScariaBanacea.description");
        if (DefDatabase<RecipeDef>.GetNamed("CureScariaBanacea").jobString != "正在使用香蕉神药治疗疤痕")
            throw new InvalidOperationException("Mod25 injection failed: CureScariaBanacea.jobString");
        if (DefDatabase<RecipeDef>.GetNamed("CureScariaBanacea").successfullyRemovedHediffMessage != "{0}成功地使用香蕉神药治好了{1}的疤痕")
            throw new InvalidOperationException("Mod25 injection failed: CureScariaBanacea.successfullyRemovedHediffMessage");
        if (DefDatabase<RecipeDef>.GetNamed("Make_ARCCAdonana").label != "制作阿蕉砖")
            throw new InvalidOperationException("Mod25 injection failed: Make_ARCCAdonana.label");
        if (DefDatabase<RecipeDef>.GetNamed("Make_ARCCAdonana").description != "将香蕉捣碎、压缩并干燥成一种粘土砖。")
            throw new InvalidOperationException("Mod25 injection failed: Make_ARCCAdonana.description");
        if (DefDatabase<RecipeDef>.GetNamed("Make_ARCCAdonana").jobString != "正在制作阿蕉砖。")
            throw new InvalidOperationException("Mod25 injection failed: Make_ARCCAdonana.jobString");
        if (DefDatabase<RecipeDef>.GetNamed("Make_ARCCAdonanaBulk").label != "批量制作阿蕉砖")
            throw new InvalidOperationException("Mod25 injection failed: Make_ARCCAdonanaBulk.label");
        if (DefDatabase<RecipeDef>.GetNamed("Make_ARCCAdonanaBulk").description != "将大量的香蕉捣碎、压缩并干燥成一种粘土砖。")
            throw new InvalidOperationException("Mod25 injection failed: Make_ARCCAdonanaBulk.description");
        if (DefDatabase<RecipeDef>.GetNamed("Make_ARCCAdonanaBulk").jobString != "正在批量制作阿蕉砖。")
            throw new InvalidOperationException("Mod25 injection failed: Make_ARCCAdonanaBulk.jobString");
        if (DefDatabase<RecipeDef>.GetNamed("Make_ARCCBananaLeather").label != "制作香蕉真皮")
            throw new InvalidOperationException("Mod25 injection failed: Make_ARCCBananaLeather.label");
        if (DefDatabase<RecipeDef>.GetNamed("Make_ARCCBananaLeather").description != "将香蕉皮干燥并鞣制成皮革状材料。")
            throw new InvalidOperationException("Mod25 injection failed: Make_ARCCBananaLeather.description");
        if (DefDatabase<RecipeDef>.GetNamed("Make_ARCCBananaLeather").jobString != "正在制作香蕉真皮。")
            throw new InvalidOperationException("Mod25 injection failed: Make_ARCCBananaLeather.jobString");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_AdvancedBananaPower").label != "高级蕉电")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_AdvancedBananaPower.label");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_AdvancedBananaPower").description != "研究改进钾的加工工艺，以发现更高效产生钾电的方法，以及一些将蕉泥再利用的方式。")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_AdvancedBananaPower.description");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_BanaceaResearch").label != "医疗蕉")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_BanaceaResearch.label");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_BanaceaResearch").description != "研究基础的抗疤痕技术，尝试使用香蕉和僵尸因子来重现")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_BanaceaResearch.description");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_BananaCooking").label != "“香”蕉")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_BananaCooking.label");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_BananaCooking").description != "研究基础食谱，尝试使用香蕉来替代一些食材")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_BananaCooking.description");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_BananaPower").label != "蕉电")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_BananaPower.label");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_BananaPower").description != "利用蕉和钾来产生大量的香蕉泥。蕉泥用途广泛，可以发电，也可以生产特殊的香蕉产品。")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_BananaPower.description");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_BananaSecurity").label != "钾防")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_BananaSecurity.label");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_BananaSecurity").description != "使用钾的力量防御基地。研究能够产生高速香蕉子弹的钾电炮塔来对抗胆敢反对你的敌人。")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_BananaSecurity.description");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_BananaSowing").label != "香蕉种植")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_BananaSowing.label");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_BananaSowing").description != "研究如何种植香蕉树，可以收获美味的香蕉。")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_BananaSowing.description");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_BasicBananaWeapons").label != "基础香蕉武器")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_BasicBananaWeapons.label");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_BasicBananaWeapons").description != "使用基础武器锻造的技术，来制作相对基础的香蕉武器。")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_BasicBananaWeapons.description");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_IntermediateBananaWeapons").label != "中级香蕉武器")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_IntermediateBananaWeapons.label");
        if (DefDatabase<ResearchProjectDef>.GetNamed("ARCC_IntermediateBananaWeapons").description != "使用已经研究好的钾泥来创造爆炸武器。")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_IntermediateBananaWeapons.description");
        if (DefDatabase<ResearchTabDef>.GetNamed("ARCCBananaMod").label != "万物归蕉")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaMod.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaTrap").label != "香蕉皮陷阱")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaTrap.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaTrap").description != "地上放的一个香蕉皮。尽管没什么伤害，它可以让敌人滑倒，会极大的减速敌人。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaTrap.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaMealFine").label != "精制香蕉大餐")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaMealFine.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaMealFine").description != "一个由多种食材组成的复杂食物，核心食材是香蕉。使用香蕉为核心制作美味的大餐可是相当难的。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaMealFine.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaMealLavish").label != "奢华香蕉大餐")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaMealLavish.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaMealLavish").description != "超人厨艺的完美体现。正常的烹饪手段完全无法指望能够像这顿大餐一样平衡各种味道，只有厨艺大师才能做到。这顿大餐证明了，回归自然不意味着失去了对美味的追求。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaMealLavish.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaMealSimple").label != "简单的香蕉食品")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaMealSimple.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaMealSimple").description != "一个完全由香蕉制作的简单食品。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaMealSimple.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaSmoothie").label != "香蕉奶昔")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaSmoothie.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaSmoothie").description != "将香蕉浆打碎、冷藏，倒入一个时髦的杯子里，杯子里还配有迷你雨伞和吸管。 它满足了娱乐的需要，但营养价值不高。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaSmoothie.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCC_Banacea").label != "香蕉神药")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_Banacea.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCC_Banacea").description != "由草药，香蕉和僵尸素混合而成。当正确使用的时候，可以治疗疤痕。")
            throw new InvalidOperationException("Mod25 injection failed: ARCC_Banacea.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCAdonana").label != "阿蕉砖")
            throw new InvalidOperationException("Mod25 injection failed: ARCCAdonana.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCAdonana").description != "将香蕉捣碎并压成砖状。 它有点软且易碎，但压制程序和香蕉的高水分含量使砖块不易燃。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCAdonana.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaLeather").label != "香蕉真皮")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaLeather.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaLeather").description != "一种由香蕉皮鞣制和干燥制成的一大块材料制成的皮革。尽管香蕉似乎不结实，但它却出奇地耐用。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaLeather.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananium").label != "香蕉合金")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananium.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananium").description != "高级太空科技材料。香蕉合金有着极高的强度和硬度，因为使用了钾的工艺进行了加强。（强度类似玻璃钢）")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananium.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCZombieChunks").label != "僵尸块")
            throw new InvalidOperationException("Mod25 injection failed: ARCCZombieChunks.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCZombieChunks").description != "来自不死者的各种肉块。也许有某种用法。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCZombieChunks.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCIronpalmFronds").label != "铁棕榈叶")
            throw new InvalidOperationException("Mod25 injection failed: ARCCIronpalmFronds.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCIronpalmFronds").description != "似乎非常易碎的叶子。独特的形状掩盖了其强度和用途。可以紧密的编织形成一种耐用的网状结构，这些叶子防火并能很好的保持温度。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCIronpalmFronds.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCPlant_TreeBanana").label != "香蕉树")
            throw new InvalidOperationException("Mod25 injection failed: ARCCPlant_TreeBanana.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCPlant_TreeBanana").description != "一种热带树林，可以产香蕉，经过基因工程的改造，能产生特殊的香蕉，并在不那么温暖的环境中生长。香蕉树被收获后，不会枯萎和死亡。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCPlant_TreeBanana.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCPlant_TreeIronPalm").label != "铁棕榈")
            throw new InvalidOperationException("Mod25 injection failed: ARCCPlant_TreeIronPalm.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCPlant_TreeIronPalm").description != "基因改造后的香蕉树。非常不幸地失去了产生美味香蕉的能力，现在只能产生坚硬并不好吃的种子荚。现在这种树专长于产生高耐用的叶子，因此也被称为“铁棕榈”。这些叶子可以被织成替代布的强质织物，在中世纪世界是一种常见的选择。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCPlant_TreeIronPalm.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCRawBananas").label != "香蕉")
            throw new InvalidOperationException("Mod25 injection failed: ARCCRawBananas.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCRawBananas").description != "非常美味的香蕉。直接吃也非常好吃。植物学上界定为莓果。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCRawBananas.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBullet_MiniTurret").label != "高速香蕉弹")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBullet_MiniTurret.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCGun_BananaBigBoiTurret").label != "大型钾炮")
            throw new InvalidOperationException("Mod25 injection failed: ARCCGun_BananaBigBoiTurret.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCGun_BananaBigBoiTurret").description != "一个简单的挂载于机枪塔的自动机炮")
            throw new InvalidOperationException("Mod25 injection failed: ARCCGun_BananaBigBoiTurret.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCGun_BananaBigBoiTurret").Verbs.Single(v => v.verbClass == typeof(Verb_Shoot)).label != "大型钾炮")
            throw new InvalidOperationException("Mod25 injection failed: ARCCGun_BananaBigBoiTurret.verbs.Verb_Shoot.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCGun_BananaMiniTurret").label != "小型钾枪")
            throw new InvalidOperationException("Mod25 injection failed: ARCCGun_BananaMiniTurret.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCGun_BananaMiniTurret").description != "一个简单的挂载于机枪塔的自动机枪")
            throw new InvalidOperationException("Mod25 injection failed: ARCCGun_BananaMiniTurret.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCGun_BananaMiniTurret").Verbs.Single(v => v.verbClass == typeof(Verb_Shoot)).label != "小型钾枪")
            throw new InvalidOperationException("Mod25 injection failed: ARCCGun_BananaMiniTurret.verbs.Verb_Shoot.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCTurret_LargePotassiumTurret").label != "大型自动钾炮塔")
            throw new InvalidOperationException("Mod25 injection failed: ARCCTurret_LargePotassiumTurret.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCTurret_LargePotassiumTurret").description != "一个可移动的自动炮塔。可以直接从钾电网络中产生子弹。会向任意敌人射击大量的高速香蕉子弹。小心友军误伤。*一定不要让这个炮塔爆炸*。当此炮塔受到一定的伤害时，流经此塔的超量钾会造成毁灭性的爆炸。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCTurret_LargePotassiumTurret.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCTurret_LargePotassiumTurret").comps.OfType<CompProperties_Refuelable>().Single().outOfFuelMessage != "无法射击：需要更多的钾")
            throw new InvalidOperationException("Mod25 injection failed: ARCCTurret_LargePotassiumTurret.comps.CompRefuelable.outOfFuelMessage");
        if (DefDatabase<ThingDef>.GetNamed("ARCCTurret_PotassiumTurret").label != "钾炮")
            throw new InvalidOperationException("Mod25 injection failed: ARCCTurret_PotassiumTurret.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCTurret_PotassiumTurret").description != "一个可移动的自动炮塔。直接从钾电系统中获取子弹。不分敌我的三连发高速香蕉子弹。损伤时可能会发生爆炸。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCTurret_PotassiumTurret.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCTurret_PotassiumTurret").comps.OfType<CompProperties_Refuelable>().Single().outOfFuelMessage != "无法射击：需要更多的钾")
            throw new InvalidOperationException("Mod25 injection failed: ARCCTurret_PotassiumTurret.comps.CompRefuelable.outOfFuelMessage");
        if (DefDatabase<ThingDef>.GetNamed("BART_PotassiumPipe").label != "钾管道")
            throw new InvalidOperationException("Mod25 injection failed: BART_PotassiumPipe.label");
        if (DefDatabase<ThingDef>.GetNamed("BART_PotassiumPipe").description != "地下重型钢铁管道，用于传输美味的香蕉泥。蕉泥可以来自粉碎机，产生器或者储钾罐。")
            throw new InvalidOperationException("Mod25 injection failed: BART_PotassiumPipe.description");
        if (DefDatabase<ThingDef>.GetNamed("BART_PotassiumShredder").label != "钾粉碎机")
            throw new InvalidOperationException("Mod25 injection failed: BART_PotassiumShredder.label");
        if (DefDatabase<ThingDef>.GetNamed("BART_PotassiumShredder").description != "一个用于粉碎物品的粉碎机，被设计用于粉碎香蕉产生富集钾的泥。如果连接到钾管道，会把这种美味液泥传输到使用它的设施，比如存储容器或者电力产生器。")
            throw new InvalidOperationException("Mod25 injection failed: BART_PotassiumShredder.description");
        if (DefDatabase<ThingDef>.GetNamed("BART_PotassiumTank").label != "储钾罐")
            throw new InvalidOperationException("Mod25 injection failed: BART_PotassiumTank.label");
        if (DefDatabase<ThingDef>.GetNamed("BART_PotassiumTank").description != "一个工业储藏罐，用于储存美味的香蕉泥。需要的时候，可以从其中提取香蕉泥或者把其内容物转移到其他的容器中。\n\n警告：钾泥非常容易反应。如果存储容器受损，钾泥会接触到空气并产生一个巨型爆炸！爆炸由容器中钾泥的含量相关。")
            throw new InvalidOperationException("Mod25 injection failed: BART_PotassiumTank.description");
        if (DefDatabase<ThingDef>.GetNamed("IndustrialPotassiumPoweredGenerator").label != "工业钾电产生器")
            throw new InvalidOperationException("Mod25 injection failed: IndustrialPotassiumPoweredGenerator.label");
        if (DefDatabase<ThingDef>.GetNamed("IndustrialPotassiumPoweredGenerator").description != "将热能转化为电能。这个电力产生器将水与富钾泥混合，产生微型爆炸从而产电。常理而言，因为产生高爆性的氢气，这总有些危险，但是高级香蕉科技防止产生器爆炸成巨型火球。这个型号的产生器比小号的效能微高。")
            throw new InvalidOperationException("Mod25 injection failed: IndustrialPotassiumPoweredGenerator.description");
        if (DefDatabase<ThingDef>.GetNamed("PotassiumPoweredGenerator").label != "钾电产生器")
            throw new InvalidOperationException("Mod25 injection failed: PotassiumPoweredGenerator.label");
        if (DefDatabase<ThingDef>.GetNamed("PotassiumPoweredGenerator").description != "将热能转化为电能。这个电力产生器将水与富钾泥混合，产生微型爆炸从而产电。常理而言，因为产生高爆性的氢气，这总有些危险，但是高级香蕉科技防止产生器爆炸成巨型火球。")
            throw new InvalidOperationException("Mod25 injection failed: PotassiumPoweredGenerator.description");
        if (DefDatabase<ThingDef>.GetNamed("PotassiumStandingLamp").label != "滤光钾灯")
            throw new InvalidOperationException("Mod25 injection failed: PotassiumStandingLamp.label");
        if (DefDatabase<ThingDef>.GetNamed("PotassiumStandingLamp").description != "这种灯利用香蕉泥的力量来产生大面积的光。非常适合照亮大片黑暗区域。灯光系统进行了一些额外的调整和改进，以发出不会伤害任何人眼睛的光线。")
            throw new InvalidOperationException("Mod25 injection failed: PotassiumStandingLamp.description");
        if (DefDatabase<ThingDef>.GetNamed("PotassiumStandingLampYellow").label != "钾灯")
            throw new InvalidOperationException("Mod25 injection failed: PotassiumStandingLampYellow.label");
        if (DefDatabase<ThingDef>.GetNamed("PotassiumStandingLampYellow").description != "这种灯利用香蕉泥的力量来产生大面积的光。非常适合照亮大片黑暗区域。胆小鬼不配使用这种灯，这盏灯产生的黄色光会导致永久性失明。这种灯照亮范围很大，但是是否值得不小心瞥见它时，穿透视网模的黄光产生的伤害？")
            throw new InvalidOperationException("Mod25 injection failed: PotassiumStandingLampYellow.description");
        if (DefDatabase<ThingDef>.GetNamed("BART_Potassium").label != "储钾桶")
            throw new InvalidOperationException("Mod25 injection failed: BART_Potassium.label");
        if (DefDatabase<ThingDef>.GetNamed("BART_Potassium").description != "盛满香蕉泥的容器。香蕉里富含的大量钾使得它有巨大的潜力产电和高质量的香蕉产品，但也给了它巨大的爆炸性。")
            throw new InvalidOperationException("Mod25 injection failed: BART_Potassium.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaBombArrow").label != "香蕉爆炸箭")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaBombArrow.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaBoomBow").label != "香蕉爆炸弓")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaBoomBow.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaBoomBow").description != "一个似乎有些原始的弓，由几根看起来很易碎的香蕉连接组成。任何正常的，理性的人都会说这是一个垃圾武器，完全比不上普通的木头弓。他们可能说的对。但是使用香蕉科技，即使更低级也是一种升级。尽管这种弓射速比大部分其他弓慢，它可以射出富钾爆炸箭。这些箭也有很高的概率会留下一个香蕉皮，让你的敌人不仅会被炸成碎片，也会给他们带来更多麻烦。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaBoomBow.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaBoomBow").tools[0].label != "弓臂")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaBoomBow.tools.limb.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBananaBoomBow").Verbs.Single(v => v.verbClass == typeof(Verb_Shoot)).label != "香蕉爆炸弓")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaBoomBow.verbs.Verb_Shoot.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCBullet_Banana").label != "高密度香蕉")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBullet_Banana.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCGun_Banana").label != "香蕉串")
            throw new InvalidOperationException("Mod25 injection failed: ARCCGun_Banana.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCGun_Banana").description != "一把用于向敌人投掷的香蕉。投掷者使用的特殊投掷技术，可以利用敌人的弱点，从而产生致命伤害。也可能是香蕉里填充的很重的阿蕉砖产生的伤害。")
            throw new InvalidOperationException("Mod25 injection failed: ARCCGun_Banana.description");
        if (DefDatabase<ThingDef>.GetNamed("ARCCGun_Banana").tools[0].label != "香蕉")
            throw new InvalidOperationException("Mod25 injection failed: ARCCGun_Banana.tools.banana.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCGun_Banana").tools[1].label != "香蕉茎")
            throw new InvalidOperationException("Mod25 injection failed: ARCCGun_Banana.tools.banana_stem.label");
        if (DefDatabase<ThingDef>.GetNamed("ARCCGun_Banana").Verbs.Single(v => v.verbClass == typeof(Verb_Shoot)).label != "香蕉串")
            throw new InvalidOperationException("Mod25 injection failed: ARCCGun_Banana.verbs.Verb_Shoot.label");
        if (DefDatabase<ThoughtDef>.GetNamed("ARCCBananaIngredient").stages[0].label != "美味的香蕉")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaIngredient.stages.banana_flavor.label");
        if (DefDatabase<ThoughtDef>.GetNamed("ARCCBananaIngredient").stages[0].description != "香蕉如此地好吃！")
            throw new InvalidOperationException("Mod25 injection failed: ARCCBananaIngredient.stages.banana_flavor.description");
        if (DefDatabase<ThoughtDef>.GetNamed("AteFineBananaMeal").stages[0].label != "吃精致香蕉大餐")
            throw new InvalidOperationException("Mod25 injection failed: AteFineBananaMeal.stages.ate_fine_banana_meal.label");
        if (DefDatabase<ThoughtDef>.GetNamed("AteFineBananaMeal").stages[0].description != "多么美味的大餐！没想到香蕉和蛋白质的组合竟如此的好吃！")
            throw new InvalidOperationException("Mod25 injection failed: AteFineBananaMeal.stages.ate_fine_banana_meal.description");
        if (DefDatabase<ThoughtDef>.GetNamed("AteLavishBananaMeal").stages[0].label != "吃奢华香蕉大餐")
            throw new InvalidOperationException("Mod25 injection failed: AteLavishBananaMeal.stages.ate_lavish_banana_meal.label");
        if (DefDatabase<ThoughtDef>.GetNamed("AteLavishBananaMeal").stages[0].description != "香蕉真的是太美味啦！就像很久之前我第一次吃到香蕉一样！对于任何文明人来说都是最高档次的享受！")
            throw new InvalidOperationException("Mod25 injection failed: AteLavishBananaMeal.stages.ate_lavish_banana_meal.description");
        if (DefDatabase<ThoughtDef>.GetNamed("AteSimpleBananaMeal").stages[0].label != "吃简单香蕉食品")
            throw new InvalidOperationException("Mod25 injection failed: AteSimpleBananaMeal.stages.ate_simple_banana_meal.label");
        if (DefDatabase<ThoughtDef>.GetNamed("AteSimpleBananaMeal").stages[0].description != "我确实挺喜欢吃香蕉的。")
            throw new InvalidOperationException("Mod25 injection failed: AteSimpleBananaMeal.stages.ate_simple_banana_meal.description");
        if ("RecruitSuccess".Translate().ToString() != "{0}说服了{1}，都是猩弟！（成功率{2}）。")
            throw new InvalidOperationException("Mod25 keyed translation failed: RecruitSuccess");
        if ("RecruitFail".Translate().ToString() != "{0}未能招募{1}（成功率{2}）。")
            throw new InvalidOperationException("Mod25 keyed translation failed: RecruitFail");
        if ("LetterLabelRelationsChange_Ally".Translate().ToString() != "猩弟：{0}")
            throw new InvalidOperationException("Mod25 keyed translation failed: LetterLabelRelationsChange_Ally");
        if ("LetterLabelRelationsChange_NeutralFromAlly".Translate().ToString() != "猩弟的疏远（盟友转中立）：{0}")
            throw new InvalidOperationException("Mod25 keyed translation failed: LetterLabelRelationsChange_NeutralFromAlly");
        if ("LetterPrisonBreak".Translate().ToString() != "囚犯们不知如何撬开了门锁，正在越狱！他们可能找寻武器、战斗或者直接逃跑。越狱囚犯:\n\n{0}\n\n当囚犯人数众多或身体健康时更容易试图越狱。")
            throw new InvalidOperationException("Mod25 keyed translation failed: LetterPrisonBreak");
        if ("LetterLabelDrugBinge".Translate().ToString() != "{0}滥用者——香蕉")
            throw new InvalidOperationException("Mod25 keyed translation failed: LetterLabelDrugBinge");
        if ("LetterDrugBinge".Translate().ToString() != "{2_label}无法控制自己的欲望，正在超剂量吃香蕉{1}。")
            throw new InvalidOperationException("Mod25 keyed translation failed: LetterDrugBinge");
        if ("LetterCatatonicMentalBreak".Translate().ToString() != "{0}的精神已彻底崩溃并陷入紧张性木僵状态。{PAWN_pronoun}会在数天后恢复。")
            throw new InvalidOperationException("Mod25 keyed translation failed: LetterCatatonicMentalBreak");
        if ("LetterRefugeeJoins".Translate().ToString() != "一个名为{PAWN_nameDef}的{PAWN_title}已经到达并认我们当猩弟！")
            throw new InvalidOperationException("Mod25 keyed translation failed: LetterRefugeeJoins");
        if ("LetterRescueeJoins".Translate().ToString() != "{PAWN_nameDef}感谢你救出了{PAWN_objective}。{PAWN_pronoun}觉得你们是好猩弟，{PAWN_pronoun}将加入你的殖民地。")
            throw new InvalidOperationException("Mod25 keyed translation failed: LetterRescueeJoins");
        if ("BART_ExtractPotassium".Translate().ToString() != "提取钾质")
            throw new InvalidOperationException("Mod25 keyed translation failed: BART_ExtractPotassium");
        if ("BART_ExtractPotassiumDesc".Translate().ToString() != "从容器中提取钾质。会有人过来做这个的。")
            throw new InvalidOperationException("Mod25 keyed translation failed: BART_ExtractPotassiumDesc");
        Log.Message("RW17 Mod25: all 132 injected and 12 keyed Chinese fields passed");
    }
}
