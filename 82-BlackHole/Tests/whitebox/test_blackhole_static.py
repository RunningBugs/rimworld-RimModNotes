#!/usr/bin/env python3
"""82-BlackHole 白盒静态测试：锁定与原版 CompTransporter/LoadTransporters 对齐的关键约定。

对齐基准（原版行为，详见各测试 docstring）：
- 搬运双轨制：货单小人进 Lord（MakeLordsAsAppropriate），全图殖民者经 WorkGiver 帮忙
- 对话框 CheckForErrors：空选拒绝、选中的小人走不到黑洞拒绝
- 搜索侧排除 forbidden；卡死时 CompTick 一次性 CautionInput 警告（不擅自改货单）
- 最后一件送达时立即发 TaskCompletion 完成消息（SubtractFromToLoadList 时机）
- 取消装载：不中断在途 job（WasLoadingCanceled 自然终结），货留手中，殖民者置 UnloadEverything
- 还有人搬货时不发走入 job（JobGiver_EnterTransporter 组分支），走入用 Danger.Deadly
- DutyDef thinktree：搬货 → 卸背包（UnloadYourInventory）→ 走入
- StartCarryThing：failIfStackCountLessThanJobCount=false、canTakeFromInventory=true
- 装载清单绝不跨条目模糊合并（不同品质同 def 物品并条会导致计数永远减不完）
- 货物在发 job 时烘进 targetA/count（消除原版"判有活→起步再选货"的 null 预约竞态）
- allowOpportunisticPrefix 不得出现：原版因 targetA=Invalid 实际永不生效，
  本 mod 烘入 targetA 后它会半生效（只预约队列不拾取），移除即对齐原版实际行为

运行：python3 -m unittest discover -s Tests/whitebox -v
"""
from __future__ import annotations

import re
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "Source" / "BlackHole"


def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding="utf-8")


def read_src(name: str) -> str:
    return (SOURCE_DIR / name).read_text(encoding="utf-8")


def method_body(src: str, signature: str) -> str:
    """按签名定位方法并做花括号配对，返回方法体（含首尾花括号）。"""
    start = src.find(signature)
    if start == -1:
        raise AssertionError(f"找不到方法签名: {signature}")
    brace = src.find("{", start)
    if brace == -1:
        raise AssertionError(f"方法缺少方法体: {signature}")
    depth = 0
    for i in range(brace, len(src)):
        if src[i] == "{":
            depth += 1
        elif src[i] == "}":
            depth -= 1
            if depth == 0:
                return src[brace : i + 1]
    raise AssertionError(f"方法体花括号不配对: {signature}")


def keyed_keys(lang: str) -> set[str]:
    root = ET.parse(ROOT / "Languages" / lang / "Keyed" / "BlackHole.xml").getroot()
    return {node.tag for node in root}


def definjected_keys(lang: str, sub: str, filename: str) -> set[str]:
    root = ET.parse(ROOT / "Languages" / lang / "DefInjected" / sub / filename).getroot()
    return {node.tag for node in root}


class XmlWellFormedTests(unittest.TestCase):
    def test_mod_xml_files_parse(self) -> None:
        for top in ["Defs", "Languages", "About"]:
            for path in (ROOT / top).rglob("*.xml"):
                with self.subTest(path=path.relative_to(ROOT)):
                    ET.parse(path)


class AboutTests(unittest.TestCase):
    def test_package_id_matches_shader_loader_filter(self) -> None:
        """Graphic_BlackHole 按 PackageIdPlayerFacing 硬编码找 AssetBundle，必须与 About.xml 一致。"""
        about = ET.parse(ROOT / "About" / "About.xml").getroot()
        package_id = about.findtext("packageId")
        self.assertEqual(package_id, "RunningBugs.BlackHole")
        self.assertIn(f'mod.PackageIdPlayerFacing != "{package_id}"', read_src("Graphic_BlackHole.cs"))


class DefConsistencyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.jobs_xml = ET.parse(ROOT / "Defs" / "BlackHoleJobs.xml").getroot()
        cls.jobs_xml_text = read("Defs/BlackHoleJobs.xml")
        cls.thing_xml = ET.parse(ROOT / "Defs" / "ThingDefs_BlackHole.xml").getroot()
        cls.all_src = "\n".join(p.read_text(encoding="utf-8") for p in SOURCE_DIR.glob("*.cs"))

    def test_job_def_driver_classes_exist_in_source(self) -> None:
        drivers = set()
        for job_def in self.jobs_xml.findall("JobDef"):
            driver = job_def.findtext("driverClass")
            self.assertIsNotNone(driver)
            drivers.add(driver)
            self.assertIn(f"class {driver.split('.')[-1]}", self.all_src)
        self.assertEqual(len(drivers), 2, "应有 Haul/Enter 两个 JobDef")

    def test_haul_jobdef_flags_match_vanilla_effective_behavior(self) -> None:
        """suspendable/dropThingBeforeJob 对齐原版 HaulToTransporter；
        allowOpportunisticPrefix 不得出现（原版因 targetA=Invalid 实际不生效，见模块 docstring）。"""
        for job_def in self.jobs_xml.findall("JobDef"):
            if job_def.findtext("defName") != "BlackHoleHaul":
                continue
            self.assertEqual(job_def.findtext("suspendable"), "false")
            self.assertEqual(job_def.findtext("dropThingBeforeJob"), "false")
            self.assertIsNone(job_def.find("allowOpportunisticPrefix"))

    def test_duty_thinktree_matches_vanilla_load_and_enter(self) -> None:
        """原版 LoadAndEnterTransporters：搬货 → JobGiver_UnloadYourInventory（先卸背包）→ 走入。"""
        duty = self.jobs_xml.find("DutyDef")
        self.assertIsNotNone(duty)
        classes = [li.get("Class") for li in duty.findall(".//li")]
        ours = [c for c in classes if c and c.startswith("BlackHole.")]
        self.assertEqual(ours, ["BlackHole.JobGiver_DevourToBlackHole", "BlackHole.JobGiver_EnterBlackHole"])
        self.assertIn("JobGiver_UnloadYourInventory", classes)
        # 顺序：搬货 < 卸背包 < 走入
        text = self.jobs_xml_text
        self.assertLess(text.find("JobGiver_DevourToBlackHole"), text.find("JobGiver_UnloadYourInventory"))
        self.assertLess(text.find("JobGiver_UnloadYourInventory"), text.find("JobGiver_EnterBlackHole"))
        for cls in ours:
            self.assertIn(f"class {cls.split('.')[-1]}", self.all_src)

    def test_workgiver_def_matches_vanilla_load_transporters(self) -> None:
        """原版 LoadTransporters：workType=Hauling、priorityInType=110、requiredCapacities=Manipulation。"""
        wg = self.jobs_xml.find("WorkGiverDef")
        self.assertIsNotNone(wg)
        self.assertEqual(wg.findtext("defName"), "BlackHoleLoad")
        self.assertEqual(wg.findtext("giverClass"), "BlackHole.WorkGiver_BlackHole")
        self.assertEqual(wg.findtext("workType"), "Hauling")
        self.assertEqual(wg.findtext("priorityInType"), "110")
        self.assertEqual([li.text for li in wg.findall("requiredCapacities/li")], ["Manipulation"])
        self.assertIn("class WorkGiver_BlackHole", self.all_src)

    def test_defof_fields_match_xml_defnames(self) -> None:
        defof = read_src("BlackHoleDefOf.cs")
        job_duty_fields = set(re.findall(r"public static (?:JobDef|DutyDef) (\w+);", defof))
        xml_defnames = {node.findtext("defName") for node in self.jobs_xml if node.tag in ("JobDef", "DutyDef")}
        self.assertEqual(job_duty_fields, xml_defnames, "DefOf 的 JobDef/DutyDef 字段必须与 XML defName 一一对应")
        thing_fields = set(re.findall(r"public static ThingDef (\w+);", defof))
        self.assertEqual(thing_fields, {self.thing_xml.find("ThingDef").findtext("defName")})

    def test_thingdef_comp_and_graphic_exist_in_source(self) -> None:
        thing_def = self.thing_xml.find("ThingDef")
        comp = thing_def.find("comps/li").get("Class")
        self.assertEqual(comp, "BlackHole.CompProperties_BlackHole")
        self.assertIn("class CompProperties_BlackHole", self.all_src)
        graphic = thing_def.findtext("graphicData/graphicClass")
        self.assertEqual(graphic, "BlackHole.Graphic_BlackHole")
        self.assertIn("class Graphic_BlackHole", self.all_src)

    def test_thingdef_ticker_is_normal(self) -> None:
        """卡死警告依赖 CompTick 每 60 tick 触发，tickerType 必须是 Normal。"""
        self.assertEqual(self.thing_xml.findtext("ThingDef/tickerType"), "Normal")


class TranslationTests(unittest.TestCase):
    LANGS = ["ChineseSimplified", "English"]
    DEFINJECTED = [("ThingDef", "BlackHole.xml"), ("JobDef", "BlackHoleJobs.xml"), ("WorkGiverDef", "BlackHoleJobs.xml")]

    def test_keyed_key_parity_between_languages(self) -> None:
        key_sets = {lang: keyed_keys(lang) for lang in self.LANGS}
        base = key_sets[self.LANGS[0]]
        for lang in self.LANGS[1:]:
            self.assertEqual(key_sets[lang], base, f"{lang} 的 Keyed 翻译 key 与 {self.LANGS[0]} 不一致")

    def test_definjected_key_parity_between_languages(self) -> None:
        for sub, filename in self.DEFINJECTED:
            key_sets = {lang: definjected_keys(lang, sub, filename) for lang in self.LANGS}
            base = key_sets[self.LANGS[0]]
            for lang in self.LANGS[1:]:
                self.assertEqual(key_sets[lang], base, f"{lang}/{sub}/{filename} 翻译 key 不一致")

    def test_all_translate_keys_used_in_source_are_translated(self) -> None:
        used: set[str] = set()
        for path in SOURCE_DIR.glob("*.cs"):
            used |= set(re.findall(r'"(BlackHole\.[A-Za-z0-9]+)"\s*\.Translate\s*\(', path.read_text(encoding="utf-8")))
        self.assertGreater(len(used), 0, "源码中应至少用到一些 BlackHole.* 翻译 key")
        for lang in self.LANGS:
            translated = keyed_keys(lang)
            for key in sorted(used):
                self.assertIn(key, translated, f"{lang} 缺少源码用到的翻译 key: {key}")


class CompBlackHoleSourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.src = read_src("CompBlackHole.cs")

    def test_add_to_load_list_never_merges_across_transferables(self) -> None:
        """不同品质的同 def 物品是不同 transferable；Desperate 按 def 模糊匹配会把它们并条，
        数量累加但 things 不加 → 计数永远减不完 → 装载死锁。只允许直接入列（等价原版新建条目路径）。"""
        body = method_body(self.src, "public void AddToTheToLoadList")
        self.assertNotIn("TransferableUtility.TransferableMatchingDesperate", body)
        self.assertNotIn("AdjustBy", body)
        self.assertIn("leftToLoad.Add(transferable)", body)
        self.assertIn("transferable.AdjustTo(count)", body)

    def test_comp_tick_warns_once_when_stalled_vanilla_shape(self) -> None:
        """原版 CompTransporter.CompTick：卡死时 CautionInput 警告一次，不擅自修改货单。"""
        body = method_body(self.src, "public override void CompTick()")
        self.assertIn("notifiedCantLoadMore", body)
        self.assertIn("AnyPawnCanLoadAnythingNow", body)
        self.assertIn("MessageTypeDefOf.CautionInput", body)
        self.assertNotIn("PruneLeftToLoad", self.src, "不得擅自剔除/下调玩家货单（原版只警告）")

    def test_any_pawn_can_load_checks_all_vanilla_channels(self) -> None:
        """原版 AnyPawnCanLoadAnythingNow 三个渠道：在途 job / 货单小人能走入 / 有殖民者能搬。"""
        body = method_body(self.src, "private bool AnyPawnCanLoadAnythingNow")
        self.assertIn("BlackHoleDefOf.BlackHoleHaul", body)
        self.assertIn("BlackHoleDefOf.BlackHoleEnter", body)
        self.assertIn("LeftToLoadContains(pawn)", body)
        self.assertIn("Danger.Deadly", body)
        self.assertIn("IsColonist", body)
        self.assertIn("BlackHoleJobUtility.HasJobOnHole", body)

    def test_devour_completes_loading_immediately_with_task_completion(self) -> None:
        """原版 MessageFinishedLoadingTransporters：最后一件送达时立即发，TaskCompletion。"""
        body = method_body(self.src, "public void Devour(Thing thing)")
        self.assertIn("!AnythingLeftToLoad", body)
        self.assertIn("EndLoading()", body)
        self.assertIn("MessageTypeDefOf.TaskCompletion", body)

    def test_start_loading_uses_only_cargo_pawns(self) -> None:
        """原版 MakeLordsAsAppropriate：只有货单里的自由小人/机械体进 Lord，
        不抓全图闲人（全图帮忙走 WorkGiver 体系）。"""
        body = method_body(self.src, "public void StartLoading")
        self.assertIn("x.IsColonist || x.IsColonyMechPlayerControlled", body)
        self.assertIn("!x.Downed", body)
        self.assertIn("IsSelfShutdown", body)
        self.assertNotIn("AllPawnsSpawned", body)
        self.assertNotIn("FallbackWorkerCount", self.src)

    def test_start_loading_resets_stall_warning(self) -> None:
        body = method_body(self.src, "public void StartLoading")
        self.assertIn("loadingInProgress = true;", body)
        self.assertIn("notifiedCantLoadMore = false;", body)

    def test_cancel_load_vanilla_shape(self) -> None:
        """原版 CancelLoad：不主动中断在途 job（WasLoadingCanceled 自然终结、货留手中），
        殖民者置 UnloadEverything；不得就地丢货。"""
        body = method_body(self.src, "public void CancelLoad(Map map)")
        self.assertIn("UnloadEverything = true", body)
        self.assertIn("EndLoading(map)", body)
        self.assertNotIn("EndCurrentJob", body)
        self.assertNotIn("TryDropCarriedThing", body)

    def test_destroy_cancels_via_previous_map(self) -> None:
        """PostDestroy 时 parent.Map 已为 null，必须用 previousMap 清理 Lord/置标志位。"""
        body = method_body(self.src, "public override void PostDestroy")
        self.assertIn("CancelLoad(previousMap)", body)

    def test_gizmo_hidden_without_map(self) -> None:
        body = method_body(self.src, "public override IEnumerable<Gizmo> CompGetGizmosExtra()")
        self.assertIn("parent.Map == null", body)

    def test_command_textures_lazy_main_thread_only(self) -> None:
        """静态贴图若在后台线程初始化会被 ContentFinder 永久毒化成 null（历史事故）。"""
        self.assertGreaterEqual(self.src.count("UnityData.IsInMainThread"), 2)

    def test_stall_warning_flag_scribed(self) -> None:
        body = method_body(self.src, "public override void PostExposeData()")
        self.assertIn("notifiedCantLoadMore", body)


class JobUtilitySourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.src = read_src("BlackHoleJobUtility.cs")

    def test_job_bakes_cargo_target_and_count(self) -> None:
        """货物在发 job 时烘入（一次搜索），消除原版"判有活→起步再选货"的 null 预约竞态。"""
        self.assertIn("JobMaker.MakeJob(BlackHoleDefOf.BlackHoleHaul, thingCount.Thing, hole.parent)", self.src)
        self.assertIn("job.count = thingCount.Count", self.src)

    def test_forbidden_items_excluded_like_vanilla(self) -> None:
        """原版 validator 排除 forbidden；搬不到的货物由 CompTick 卡死警告兜底，不擅自搬运。"""
        body = method_body(self.src, "public static ThingCount FindThingToLoad")
        self.assertIn("!x.IsForbidden(p)", body)
        self.assertIn("job.ignoreForbidden = true", self.src)

    def test_carry_conditions_match_vanilla_exactly(self) -> None:
        """原版扛人条件：囚犯/动物 || 倒地 || 低电量自关机，且 !UnloadEverything。
        不可达小人在对话框 CheckForErrors 阶段已被拒绝，这里不得另开扛人条款。"""
        body = method_body(self.src, "public static ThingCount FindThingToLoad")
        self.assertIn("pawn.Downed", body)
        self.assertIn("IsLowEnergySelfShutdown", body)
        self.assertIn("UnloadEverything", body)
        self.assertIn("pawn.IsFreeColonist", body)
        self.assertIn("pawn.IsColonyMech", body)
        self.assertNotIn("pawn.CanReach(hole.parent", body)

    def test_has_job_delegates_to_can_haul_precheck(self) -> None:
        body = method_body(self.src, "public static bool HasJobOnHole")
        self.assertIn("CanHaulToHole(pawn, hole)", body)
        self.assertIn("FindThingToLoad(pawn, hole)", body)


class JobDriverSourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.src = read_src("JobDriver_HaulToBlackHole.cs")

    def test_haul_driver_reserves_baked_target(self) -> None:
        body = method_body(self.src, "public override bool TryMakePreToilReservations")
        self.assertIn("pawn.Reserve(job.targetA, job", body)

    def test_initial_count_taken_from_job_count(self) -> None:
        body = method_body(self.src, "public override void Notify_Starting()")
        self.assertIn("initialCount = job.count;", body)

    def test_player_forced_drops_other_carried_thing(self) -> None:
        """原版 Notify_Starting：右键强制搬运时先丢掉手中其他货物。"""
        body = method_body(self.src, "public override void Notify_Starting()")
        self.assertIn("job.playerForced", body)
        self.assertIn("TryDropCarriedThing", body)

    def test_start_carry_uses_vanilla_container_flags(self) -> None:
        """原版 JobDriver_HaulToContainer：堆数不足不失败、可从小人背包取货。"""
        self.assertIn("failIfStackCountLessThanJobCount: false", self.src)
        self.assertIn("canTakeFromInventory: true", self.src)

    def test_goto_fails_on_self_and_parents_despawned(self) -> None:
        """原版容器搬运用 FailOnSelfAndParentsDespawnedOrNull（目标进了容器也算消失）。"""
        self.assertIn("FailOnSelfAndParentsDespawnedOrNull(TargetIndex.A)", self.src)

    def test_devour_drops_cargo_when_hole_destroyed(self) -> None:
        """黑洞在抵达瞬间被毁：货物就地放下，不能摘出容器后凭空泄漏。"""
        self.assertIn("TryDropCarriedThing", self.src)

    def test_enter_driver_rechecks_remaining_haul_work(self) -> None:
        """到位时若还有搬运活则让位（不湮灭自己）——原版 EnterTransporter 同款让位检查。"""
        self.assertIn("BlackHoleJobUtility.HasJobOnHole(pawn, hole)", self.src)


class JobGiverSourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.src = read_src("JobGiver_DevourToBlackHole.cs")

    def test_haul_jobgiver_single_search_via_jobonhole(self) -> None:
        body = method_body(self.src, "protected override Job TryGiveJob(Pawn pawn)")
        self.assertIn("BlackHoleJobUtility.CanHaulToHole(pawn, hole)", body)
        self.assertIn("BlackHoleJobUtility.JobOnHole(pawn, hole)", body)

    def test_enter_jobgiver_waits_while_anyone_is_hauling(self) -> None:
        """原版 JobGiver_EnterTransporter 组分支：还有人往本组搬货时不发走入 job（踱步根治）。"""
        body = self.src[self.src.find("class JobGiver_EnterBlackHole"):]
        self.assertIn("BlackHoleDefOf.BlackHoleHaul", body)
        self.assertIn("driver.HoleBuilding == building", body)

    def test_enter_jobgiver_uses_deadly_danger_like_vanilla(self) -> None:
        body = self.src[self.src.find("class JobGiver_EnterBlackHole"):]
        self.assertIn("pawn.CanReach(building, PathEndMode.Touch, Danger.Deadly)", body)


class WorkGiverSourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.src = read_src("WorkGiver_BlackHole.cs")

    def test_workgiver_scans_blackhole_def(self) -> None:
        self.assertIn("WorkGiver_Scanner", self.src)
        self.assertIn("ThingRequest.ForDef(BlackHoleDefOf.BlackHole)", self.src)

    def test_workgiver_delegates_to_job_utility(self) -> None:
        """与原版 WorkGiver_LoadTransporters 一致：Has/JobOnThing 直接转 JobUtility。"""
        self.assertIn("BlackHoleJobUtility.HasJobOnHole(pawn, hole)", self.src)
        self.assertIn("BlackHoleJobUtility.JobOnHole(pawn,", self.src)
        self.assertIn("Danger.Deadly", self.src)


class DialogSourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.src = read_src("Dialog_DevourBlackHole.cs")

    def test_no_dead_static_fields(self) -> None:
        self.assertNotIn("TmpLeftCountToTransfer", self.src)

    def test_check_for_errors_rejects_before_starting(self) -> None:
        """原版 CheckForErrors：空选、选中但走不到黑洞的小人，都在接受前拒绝（RejectInput）。"""
        body = method_body(self.src, "private bool TryAccept()")
        self.assertIn("BlackHole.CantDevourEmpty", body)
        self.assertIn("BlackHole.PawnCantReachBlackHole", body)
        self.assertIn("reachability.CanReach", body)
        self.assertLess(body.find("BlackHole.CantDevourEmpty"), body.find("hole.StartLoading("))
        self.assertLess(body.find("BlackHole.PawnCantReachBlackHole"), body.find("hole.StartLoading("))

    def test_load_list_assigned_before_loading_starts(self) -> None:
        """原版 TryAccept：InitiateLoading/AssignTransferables 先于 MakeLordsAsAppropriate。"""
        body = method_body(self.src, "private bool TryAccept()")
        self.assertLess(body.find("AssignTransferablesToHole()"), body.find("hole.StartLoading("))

    def test_no_workers_concept_removed(self) -> None:
        """无人能搬不是拒绝理由：原版接受后由 WorkGiver/卡死警告兜底。"""
        self.assertNotIn("NoWorkers", self.src)


if __name__ == "__main__":
    unittest.main()
