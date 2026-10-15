{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Method begins at RVA 0x2e50
		// Header size: 12
		// Code size: 738 (0x2e2)
		.maxstack 8
		.locals init (
			[0] class ArmorRacks.Things.ArmorRack,
			[1] class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Apparel>,
			[2] class ['Assembly-CSharp']Verse.Thing,
			[3] class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Apparel>,
			[4] class ['Assembly-CSharp']Verse.ThingWithComps,
			[5] class ['Assembly-CSharp']Verse.Thing,
			[6] class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Apparel>,
			[7] int32,
			[8] int32,
			[9] class ArmorRacks.ThingComps.ArmorRackUseCommandComp,
			[10] valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.Apparel>,
			[11] class ['Assembly-CSharp']RimWorld.Apparel,
			[12] bool,
			[13] valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.Apparel>,
			[14] class ['Assembly-CSharp']RimWorld.Apparel,
			[15] bool,
			[16] valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.Apparel>,
			[17] class ['Assembly-CSharp']RimWorld.Apparel,
			[18] bool,
			[19] int32,
			[20] int32,
			[21] bool,
			[22] bool,
			[23] bool
		)

		IL_0000: nop
		IL_0001: ldarg.0
		IL_0002: call instance class ['Assembly-CSharp']Verse.Thing ['Assembly-CSharp']Verse.AI.JobDriver::get_TargetThingA()
		IL_0007: isinst ArmorRacks.Things.ArmorRack
		IL_000c: stloc.0
		IL_000d: ldloc.0
		IL_000e: callvirt instance class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Apparel> ArmorRacks.Things.ArmorRack::GetStoredApparel()
		IL_0013: newobj instance void class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Apparel>::.ctor(class [mscorlib]System.Collections.Generic.IEnumerable`1<!0>)
		IL_0018: stloc.1
		IL_0019: ldloc.0
		IL_001a: callvirt instance class ['Assembly-CSharp']Verse.Thing ArmorRacks.Things.ArmorRack::GetStoredWeapon()
		IL_001f: stloc.2
		IL_0020: ldarg.0
		IL_0021: ldfld class ['Assembly-CSharp']Verse.Pawn ['Assembly-CSharp']Verse.AI.JobDriver::pawn
		IL_0026: ldfld class ['Assembly-CSharp']RimWorld.Pawn_ApparelTracker ['Assembly-CSharp']Verse.Pawn::apparel
		IL_002b: callvirt instance class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Apparel> ['Assembly-CSharp']RimWorld.Pawn_ApparelTracker::get_WornApparel()
		IL_0030: newobj instance void class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Apparel>::.ctor(class [mscorlib]System.Collections.Generic.IEnumerable`1<!0>)
		IL_0035: stloc.3
		IL_0036: ldarg.0
		IL_0037: ldfld class ['Assembly-CSharp']Verse.Pawn ['Assembly-CSharp']Verse.AI.JobDriver::pawn
		IL_003c: ldfld class ['Assembly-CSharp']Verse.Pawn_EquipmentTracker ['Assembly-CSharp']Verse.Pawn::equipment
		IL_0041: callvirt instance class ['Assembly-CSharp']Verse.ThingWithComps ['Assembly-CSharp']Verse.Pawn_EquipmentTracker::get_Primary()
		IL_0046: stloc.s 4
		IL_0048: ldloc.0
		IL_0049: ldfld class ArmorRacks.Things.ArmorRackInnerContainer ArmorRacks.Things.ArmorRack::InnerContainer
		IL_004e: callvirt instance void ['Assembly-CSharp']Verse.ThingOwner::Clear()
		IL_0053: nop
		IL_0054: nop
		IL_0055: ldloc.3
		IL_0056: callvirt instance valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<!0> class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Apparel>::GetEnumerator()
		IL_005b: stloc.s 10
		.try
		{
			IL_005d: br.s IL_009c
			// loop start (head: IL_009c)
				IL_005f: ldloca.s 10
				IL_0061: call instance !0 valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.Apparel>::get_Current()
				IL_0066: stloc.s 11
				IL_0068: nop
				IL_0069: ldloc.0
				IL_006a: ldloc.s 11
				IL_006c: callvirt instance bool ArmorRacks.Things.ArmorRack::Accepts(class ['Assembly-CSharp']Verse.Thing)
				IL_0071: stloc.s 12
				IL_0073: ldloc.s 12
				IL_0075: brfalse.s IL_009b

				IL_0077: nop
				IL_0078: ldarg.0
				IL_0079: ldfld class ['Assembly-CSharp']Verse.Pawn ['Assembly-CSharp']Verse.AI.JobDriver::pawn
				IL_007e: ldfld class ['Assembly-CSharp']RimWorld.Pawn_ApparelTracker ['Assembly-CSharp']Verse.Pawn::apparel
				IL_0083: ldloc.s 11
				IL_0085: callvirt instance void ['Assembly-CSharp']RimWorld.Pawn_ApparelTracker::Remove(class ['Assembly-CSharp']RimWorld.Apparel)
				IL_008a: nop
				IL_008b: ldloc.0
				IL_008c: ldfld class ArmorRacks.Things.ArmorRackInnerContainer ArmorRacks.Things.ArmorRack::InnerContainer
				IL_0091: ldloc.s 11
				IL_0093: ldc.i4.1
				IL_0094: callvirt instance bool ['Assembly-CSharp']Verse.ThingOwner::TryAdd(class ['Assembly-CSharp']Verse.Thing, bool)
				IL_0099: pop
				IL_009a: nop

				IL_009b: nop

				IL_009c: ldloca.s 10
				IL_009e: call instance bool valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.Apparel>::MoveNext()
				IL_00a3: brtrue.s IL_005f
			// end loop

			IL_00a5: leave.s IL_00b6
		} // end .try
		finally
		{
			IL_00a7: ldloca.s 10
			IL_00a9: constrained. valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.Apparel>
			IL_00af: callvirt instance void [mscorlib]System.IDisposable::Dispose()
			IL_00b4: nop
			IL_00b5: endfinally
		} // end handler

		IL_00b6: newobj instance void class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Apparel>::.ctor()
		IL_00bb: stloc.s 6
		IL_00bd: nop
		IL_00be: ldloc.1
		IL_00bf: callvirt instance valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<!0> class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Apparel>::GetEnumerator()
		IL_00c4: stloc.s 13
		.try
		{
			IL_00c6: br.s IL_0100
			// loop start (head: IL_0100)
				IL_00c8: ldloca.s 13
				IL_00ca: call instance !0 valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.Apparel>::get_Current()
				IL_00cf: stloc.s 14
				IL_00d1: nop
				IL_00d2: ldloc.0
				IL_00d3: ldloc.s 14
				IL_00d5: callvirt instance bool ArmorRacks.Things.ArmorRack::Accepts(class ['Assembly-CSharp']Verse.Thing)
				IL_00da: stloc.s 15
				IL_00dc: ldloc.s 15
				IL_00de: brfalse.s IL_00f3

				IL_00e0: nop
				IL_00e1: ldloc.0
				IL_00e2: ldfld class ArmorRacks.Things.ArmorRackInnerContainer ArmorRacks.Things.ArmorRack::InnerContainer
				IL_00e7: ldloc.s 14
				IL_00e9: ldc.i4.1
				IL_00ea: callvirt instance bool ['Assembly-CSharp']Verse.ThingOwner::TryAdd(class ['Assembly-CSharp']Verse.Thing, bool)
				IL_00ef: pop
				IL_00f0: nop
				IL_00f1: br.s IL_00ff

				IL_00f3: nop
				IL_00f4: ldloc.s 6
				IL_00f6: ldloc.s 14
				IL_00f8: callvirt instance void class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Apparel>::Add(!0)
				IL_00fd: nop
				IL_00fe: nop

				IL_00ff: nop

				IL_0100: ldloca.s 13
				IL_0102: call instance bool valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.Apparel>::MoveNext()
				IL_0107: brtrue.s IL_00c8
			// end loop

			IL_0109: leave.s IL_011a
		} // end .try
		finally
		{
			IL_010b: ldloca.s 13
			IL_010d: constrained. valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.Apparel>
			IL_0113: callvirt instance void [mscorlib]System.IDisposable::Dispose()
			IL_0118: nop
			IL_0119: endfinally
		} // end handler

		IL_011a: nop
		IL_011b: ldloc.s 6
		IL_011d: callvirt instance valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<!0> class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Apparel>::GetEnumerator()
		IL_0122: stloc.s 16
		.try
		{
			IL_0124: br.s IL_019a
			// loop start (head: IL_019a)
				IL_0126: ldloca.s 16
				IL_0128: call instance !0 valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.Apparel>::get_Current()
				IL_012d: stloc.s 17
				IL_012f: nop
				IL_0130: ldarg.0
				IL_0131: ldfld class ['Assembly-CSharp']Verse.Pawn ['Assembly-CSharp']Verse.AI.JobDriver::pawn
				IL_0136: ldloc.s 17
				IL_0138: ldfld class ['Assembly-CSharp']Verse.ThingDef ['Assembly-CSharp']Verse.Thing::def
				IL_013d: call bool ['Assembly-CSharp']RimWorld.ApparelUtility::HasPartsToWear(class ['Assembly-CSharp']Verse.Pawn, class ['Assembly-CSharp']Verse.ThingDef)
				IL_0142: brfalse.s IL_015d

				IL_0144: ldarg.0
				IL_0145: ldfld class ['Assembly-CSharp']Verse.Pawn ['Assembly-CSharp']Verse.AI.JobDriver::pawn
				IL_014a: ldfld class ['Assembly-CSharp']RimWorld.Pawn_ApparelTracker ['Assembly-CSharp']Verse.Pawn::apparel
				IL_014f: ldloc.s 17
				IL_0151: ldfld class ['Assembly-CSharp']Verse.ThingDef ['Assembly-CSharp']Verse.Thing::def
				IL_0156: callvirt instance bool ['Assembly-CSharp']RimWorld.Pawn_ApparelTracker::CanWearWithoutDroppingAnything(class ['Assembly-CSharp']Verse.ThingDef)
				IL_015b: br.s IL_015e

				IL_015d: ldc.i4.0

				IL_015e: stloc.s 18
				IL_0160: ldloc.s 18
				IL_0162: brfalse.s IL_017d

				IL_0164: nop
				IL_0165: ldarg.0
				IL_0166: ldfld class ['Assembly-CSharp']Verse.Pawn ['Assembly-CSharp']Verse.AI.JobDriver::pawn
				IL_016b: ldfld class ['Assembly-CSharp']RimWorld.Pawn_ApparelTracker ['Assembly-CSharp']Verse.Pawn::apparel
				IL_0170: ldloc.s 17
				IL_0172: ldc.i4.1
				IL_0173: ldc.i4.0
				IL_0174: callvirt instance void ['Assembly-CSharp']RimWorld.Pawn_ApparelTracker::Wear(class ['Assembly-CSharp']RimWorld.Apparel, bool, bool)
				IL_0179: nop
				IL_017a: nop
				IL_017b: br.s IL_0199

				IL_017d: nop
				IL_017e: ldloc.s 17
				IL_0180: ldloc.0
				IL_0181: callvirt instance valuetype ['Assembly-CSharp']Verse.IntVec3 ['Assembly-CSharp']Verse.Thing::get_Position()
				IL_0186: ldloc.0
				IL_0187: callvirt instance class ['Assembly-CSharp']Verse.Map ['Assembly-CSharp']Verse.Thing::get_Map()
				IL_018c: ldc.i4.1
				IL_018d: ldloca.s 5
				IL_018f: ldnull
				IL_0190: ldnull
				IL_0191: ldc.i4.1
				IL_0192: call bool ['Assembly-CSharp']Verse.GenDrop::TryDropSpawn(class ['Assembly-CSharp']Verse.Thing, valuetype ['Assembly-CSharp']Verse.IntVec3, class ['Assembly-CSharp']Verse.Map, valuetype ['Assembly-CSharp']Verse.ThingPlaceMode, class ['Assembly-CSharp']Verse.Thing&, class [mscorlib]System.Action`2<class ['Assembly-CSharp']Verse.Thing, int32>, class [mscorlib]System.Predicate`1<valuetype ['Assembly-CSharp']Verse.IntVec3>, bool)
				IL_0197: pop
				IL_0198: nop

				IL_0199: nop

				IL_019a: ldloca.s 16
				IL_019c: call instance bool valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.Apparel>::MoveNext()
				IL_01a1: brtrue.s IL_0126
			// end loop

			IL_01a3: leave.s IL_01b4
		} // end .try
		finally
		{
			IL_01a5: ldloca.s 16
			IL_01a7: constrained. valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.Apparel>
			IL_01ad: callvirt instance void [mscorlib]System.IDisposable::Dispose()
			IL_01b2: nop
			IL_01b3: endfinally
		} // end handler

		IL_01b4: ldloc.2
		IL_01b5: brfalse.s IL_01ba

		IL_01b7: ldc.i4.1
		IL_01b8: br.s IL_01bb

		IL_01ba: ldc.i4.0

		IL_01bb: stloc.s 7
		IL_01bd: ldloc.s 4
		IL_01bf: brfalse.s IL_01c5

		IL_01c1: ldc.i4.s 16
		IL_01c3: br.s IL_01c6

		IL_01c5: ldc.i4.0

		IL_01c6: stloc.s 8
		IL_01c8: ldloc.s 7
		IL_01ca: ldloc.s 8
		IL_01cc: or
		IL_01cd: stloc.s 20
		IL_01cf: ldloc.s 20
		IL_01d1: stloc.s 19
		IL_01d3: ldloc.s 19
		IL_01d5: ldc.i4.1
		IL_01d6: beq IL_0268

		IL_01db: br.s IL_01dd

		IL_01dd: ldloc.s 19
		IL_01df: ldc.i4.s 16
		IL_01e1: beq IL_0278

		IL_01e6: br.s IL_01e8

		IL_01e8: ldloc.s 19
		IL_01ea: ldc.i4.s 17
		IL_01ec: beq.s IL_01f3

		IL_01ee: br IL_02ad

		IL_01f3: nop
		IL_01f4: ldloc.0
		IL_01f5: ldloc.s 4
		IL_01f7: callvirt instance bool ArmorRacks.Things.ArmorRack::Accepts(class ['Assembly-CSharp']Verse.Thing)
		IL_01fc: stloc.s 21
		IL_01fe: ldloc.s 21
		IL_0200: brfalse.s IL_0256

		IL_0202: nop
		IL_0203: ldarg.0
		IL_0204: ldfld class ['Assembly-CSharp']Verse.Pawn ['Assembly-CSharp']Verse.AI.JobDriver::pawn
		IL_0209: ldfld class ['Assembly-CSharp']Verse.Pawn_EquipmentTracker ['Assembly-CSharp']Verse.Pawn::equipment
		IL_020e: ldloc.s 4
		IL_0210: callvirt instance void ['Assembly-CSharp']Verse.Pawn_EquipmentTracker::Remove(class ['Assembly-CSharp']Verse.ThingWithComps)
		IL_0215: nop
		IL_0216: ldloc.0
		IL_0217: ldfld class ArmorRacks.Things.ArmorRackInnerContainer ArmorRacks.Things.ArmorRack::InnerContainer
		IL_021c: ldloc.s 4
		IL_021e: ldc.i4.1
		IL_021f: callvirt instance bool ['Assembly-CSharp']Verse.ThingOwner::TryAdd(class ['Assembly-CSharp']Verse.Thing, bool)
		IL_0224: pop
		IL_0225: ldarg.0
		IL_0226: ldfld class ['Assembly-CSharp']Verse.Pawn ['Assembly-CSharp']Verse.AI.JobDriver::pawn
		IL_022b: ldfld class ['Assembly-CSharp']Verse.Pawn_EquipmentTracker ['Assembly-CSharp']Verse.Pawn::equipment
		IL_0230: ldloc.2
		IL_0231: castclass ['Assembly-CSharp']Verse.ThingWithComps
		IL_0236: callvirt instance void ['Assembly-CSharp']Verse.Pawn_EquipmentTracker::MakeRoomFor(class ['Assembly-CSharp']Verse.ThingWithComps)
		IL_023b: nop
		IL_023c: ldarg.0
		IL_023d: ldfld class ['Assembly-CSharp']Verse.Pawn ['Assembly-CSharp']Verse.AI.JobDriver::pawn
		IL_0242: ldfld class ['Assembly-CSharp']Verse.Pawn_EquipmentTracker ['Assembly-CSharp']Verse.Pawn::equipment
		IL_0247: ldloc.2
		IL_0248: castclass ['Assembly-CSharp']Verse.ThingWithComps
		IL_024d: callvirt instance void ['Assembly-CSharp']Verse.Pawn_EquipmentTracker::AddEquipment(class ['Assembly-CSharp']Verse.ThingWithComps)
		IL_0252: nop
		IL_0253: nop
		IL_0254: br.s IL_0266

		IL_0256: nop
		IL_0257: ldloc.0
		IL_0258: ldfld class ArmorRacks.Things.ArmorRackInnerContainer ArmorRacks.Things.ArmorRack::InnerContainer
		IL_025d: ldloc.2
		IL_025e: ldc.i4.1
		IL_025f: callvirt instance bool ['Assembly-CSharp']Verse.ThingOwner::TryAdd(class ['Assembly-CSharp']Verse.Thing, bool)
		IL_0264: pop
		IL_0265: nop

		IL_0266: br.s IL_02ad

		IL_0268: ldloc.0
		IL_0269: ldfld class ArmorRacks.Things.ArmorRackInnerContainer ArmorRacks.Things.ArmorRack::InnerContainer
		IL_026e: ldloc.2
		IL_026f: ldc.i4.1
		IL_0270: callvirt instance bool ['Assembly-CSharp']Verse.ThingOwner::TryAdd(class ['Assembly-CSharp']Verse.Thing, bool)
		IL_0275: pop
		IL_0276: br.s IL_02ad

		IL_0278: nop
		IL_0279: ldloc.0
		IL_027a: ldloc.s 4
		IL_027c: callvirt instance bool ArmorRacks.Things.ArmorRack::Accepts(class ['Assembly-CSharp']Verse.Thing)
		IL_0281: stloc.s 22
		IL_0283: ldloc.s 22
		IL_0285: brfalse.s IL_02ab

		IL_0287: nop
		IL_0288: ldarg.0
		IL_0289: ldfld class ['Assembly-CSharp']Verse.Pawn ['Assembly-CSharp']Verse.AI.JobDriver::pawn
		IL_028e: ldfld class ['Assembly-CSharp']Verse.Pawn_EquipmentTracker ['Assembly-CSharp']Verse.Pawn::equipment
		IL_0293: ldloc.s 4
		IL_0295: callvirt instance void ['Assembly-CSharp']Verse.Pawn_EquipmentTracker::Remove(class ['Assembly-CSharp']Verse.ThingWithComps)
		IL_029a: nop
		IL_029b: ldloc.0
		IL_029c: ldfld class ArmorRacks.Things.ArmorRackInnerContainer ArmorRacks.Things.ArmorRack::InnerContainer
		IL_02a1: ldloc.s 4
		IL_02a3: ldc.i4.1
		IL_02a4: callvirt instance bool ['Assembly-CSharp']Verse.ThingOwner::TryAdd(class ['Assembly-CSharp']Verse.Thing, bool)
		IL_02a9: pop
		IL_02aa: nop

		IL_02ab: br.s IL_02ad

		IL_02ad: ldarg.0
		IL_02ae: call instance class ['Assembly-CSharp']Verse.Thing ['Assembly-CSharp']Verse.AI.JobDriver::get_TargetThingA()
		IL_02b3: ldc.i4.0
		IL_02b4: ldc.i4.1
		IL_02b5: call void ['Assembly-CSharp']RimWorld.ForbidUtility::SetForbidden(class ['Assembly-CSharp']Verse.Thing, bool, bool)
		IL_02ba: nop
		IL_02bb: ldarg.0
		IL_02bc: ldfld class ['Assembly-CSharp']Verse.Pawn ['Assembly-CSharp']Verse.AI.JobDriver::pawn
		IL_02c1: callvirt instance !!0 ['Assembly-CSharp']Verse.ThingWithComps::GetComp<class ArmorRacks.ThingComps.ArmorRackUseCommandComp>()
		IL_02c6: stloc.s 9
		IL_02c8: ldloc.s 9
		IL_02ca: ldnull
		IL_02cb: cgt.un
		IL_02cd: stloc.s 23
		IL_02cf: ldloc.s 23
		IL_02d1: brfalse.s IL_02e1

		IL_02d3: nop
		IL_02d4: ldloc.s 9
		IL_02d6: ldsfld class ['Assembly-CSharp']Verse.JobDef ArmorRacks.DefOfs.ArmorRacksJobDefOf::ArmorRacks_JobWearRack
		IL_02db: stfld class ['Assembly-CSharp']Verse.JobDef ArmorRacks.ThingComps.ArmorRackUseCommandComp::CurArmorRackJobDef
		IL_02e0: nop

		IL_02e1: ret
	} // end of method JobDriverTransferToRack::'<MakeNewToils>b__1_0'