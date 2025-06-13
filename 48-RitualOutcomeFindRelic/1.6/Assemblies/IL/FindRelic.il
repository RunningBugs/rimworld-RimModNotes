// IL code: FindRelic
.class private auto ansi '<Module>'
{
} // end of class <Module>

.class private auto ansi sealed beforefieldinit Microsoft.CodeAnalysis.EmbeddedAttribute
	extends [mscorlib]System.Attribute
{
	.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
		01 00 00 00
	)
	.custom instance void Microsoft.CodeAnalysis.EmbeddedAttribute::.ctor() = (
		01 00 00 00
	)
	// Methods
	.method public hidebysig specialname rtspecialname 
		instance void .ctor () cil managed 
	{
		// Method begins at RVA 0x2048
		// Header size: 1
		// Code size: 8 (0x8)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void [mscorlib]System.Attribute::.ctor()
		IL_0006: nop
		IL_0007: ret
	} // end of method EmbeddedAttribute::.ctor

} // end of class Microsoft.CodeAnalysis.EmbeddedAttribute

.class private auto ansi sealed beforefieldinit System.Runtime.CompilerServices.RefSafetyRulesAttribute
	extends [mscorlib]System.Attribute
{
	.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
		01 00 00 00
	)
	.custom instance void Microsoft.CodeAnalysis.EmbeddedAttribute::.ctor() = (
		01 00 00 00
	)
	.custom instance void [mscorlib]System.AttributeUsageAttribute::.ctor(valuetype [mscorlib]System.AttributeTargets) = (
		01 00 02 00 00 00 02 00 54 02 0d 41 6c 6c 6f 77
		4d 75 6c 74 69 70 6c 65 00 54 02 09 49 6e 68 65
		72 69 74 65 64 00
	)
	// Fields
	.field public initonly int32 Version

	// Methods
	.method public hidebysig specialname rtspecialname 
		instance void .ctor (
			int32 ''
		) cil managed 
	{
		// Method begins at RVA 0x2051
		// Header size: 1
		// Code size: 15 (0xf)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void [mscorlib]System.Attribute::.ctor()
		IL_0006: nop
		IL_0007: ldarg.0
		IL_0008: ldarg.1
		IL_0009: stfld int32 System.Runtime.CompilerServices.RefSafetyRulesAttribute::Version
		IL_000e: ret
	} // end of method RefSafetyRulesAttribute::.ctor

} // end of class System.Runtime.CompilerServices.RefSafetyRulesAttribute

.class public auto ansi beforefieldinit FindRelic.FR_GameComp
	extends ['Assembly-CSharp']Verse.GameComponent
{
	// Methods
	.method public hidebysig specialname rtspecialname 
		instance void .ctor (
			class ['Assembly-CSharp']Verse.Game game
		) cil managed 
	{
		// Method begins at RVA 0x2061
		// Header size: 1
		// Code size: 9 (0x9)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void ['Assembly-CSharp']Verse.GameComponent::.ctor()
		IL_0006: nop
		IL_0007: nop
		IL_0008: ret
	} // end of method FR_GameComp::.ctor

	.method public hidebysig static 
		string RefreshEffectDesc (
			string effectDesc
		) cil managed 
	{
		// Method begins at RVA 0x206c
		// Header size: 12
		// Code size: 213 (0xd5)
		.maxstack 5
		.locals init (
			[0] bool,
			[1] int32,
			[2] bool,
			[3] int32,
			[4] bool,
			[5] char,
			[6] bool,
			[7] bool,
			[8] bool,
			[9] bool,
			[10] string
		)

		IL_0000: nop
		IL_0001: ldarg.0
		IL_0002: ldstr "{0}"
		IL_0007: callvirt instance bool [mscorlib]System.String::Contains(string)
		IL_000c: ldc.i4.0
		IL_000d: ceq
		IL_000f: stloc.0
		IL_0010: ldloc.0
		IL_0011: brfalse IL_00af

		IL_0016: nop
		IL_0017: ldarg.0
		IL_0018: ldc.i4.s 37
		IL_001a: callvirt instance int32 [mscorlib]System.String::IndexOf(char)
		IL_001f: stloc.1
		IL_0020: ldloc.1
		IL_0021: ldc.i4.0
		IL_0022: cgt
		IL_0024: stloc.2
		IL_0025: ldloc.2
		IL_0026: brfalse IL_00ae

		IL_002b: nop
		IL_002c: ldloc.1
		IL_002d: stloc.3
		IL_002e: ldc.i4.0
		IL_002f: stloc.s 4
		IL_0031: br.s IL_007a
		// loop start (head: IL_007a)
			IL_0033: nop
			IL_0034: ldarg.0
			IL_0035: ldloc.3
			IL_0036: ldc.i4.1
			IL_0037: sub
			IL_0038: callvirt instance char [mscorlib]System.String::get_Chars(int32)
			IL_003d: stloc.s 5
			IL_003f: ldloc.s 5
			IL_0041: ldc.i4.s 46
			IL_0043: bne.un.s IL_004f

			IL_0045: ldloc.s 4
			IL_0047: brtrue.s IL_004f

			IL_0049: ldloc.3
			IL_004a: ldloc.1
			IL_004b: clt
			IL_004d: br.s IL_0050

			IL_004f: ldc.i4.0

			IL_0050: stloc.s 6
			IL_0052: ldloc.s 6
			IL_0054: brfalse.s IL_0061

			IL_0056: nop
			IL_0057: ldc.i4.1
			IL_0058: stloc.s 4
			IL_005a: ldloc.3
			IL_005b: ldc.i4.1
			IL_005c: sub
			IL_005d: stloc.3
			IL_005e: nop
			IL_005f: br.s IL_0079

			IL_0061: ldloc.s 5
			IL_0063: call bool [mscorlib]System.Char::IsDigit(char)
			IL_0068: stloc.s 7
			IL_006a: ldloc.s 7
			IL_006c: brfalse.s IL_0076

			IL_006e: nop
			IL_006f: ldloc.3
			IL_0070: ldc.i4.1
			IL_0071: sub
			IL_0072: stloc.3
			IL_0073: nop
			IL_0074: br.s IL_0079

			IL_0076: nop
			IL_0077: br.s IL_0084

			IL_0079: nop

			IL_007a: ldloc.3
			IL_007b: ldc.i4.0
			IL_007c: cgt
			IL_007e: stloc.s 8
			IL_0080: ldloc.s 8
			IL_0082: brtrue.s IL_0033
		// end loop

		IL_0084: ldloc.3
		IL_0085: ldloc.1
		IL_0086: clt
		IL_0088: stloc.s 9
		IL_008a: ldloc.s 9
		IL_008c: brfalse.s IL_00ad

		IL_008e: nop
		IL_008f: ldarg.0
		IL_0090: ldc.i4.0
		IL_0091: ldloc.3
		IL_0092: callvirt instance string [mscorlib]System.String::Substring(int32, int32)
		IL_0097: ldstr "{0}"
		IL_009c: ldarg.0
		IL_009d: ldloc.1
		IL_009e: ldc.i4.1
		IL_009f: add
		IL_00a0: callvirt instance string [mscorlib]System.String::Substring(int32)
		IL_00a5: call string [mscorlib]System.String::Concat(string, string, string)
		IL_00aa: starg.s effectDesc
		IL_00ac: nop

		IL_00ad: nop

		IL_00ae: nop

		IL_00af: ldarg.0
		IL_00b0: ldsfld float32 FindRelic.Settings::FindRelicChance
		IL_00b5: ldstr "0"
		IL_00ba: call string ['Assembly-CSharp']Verse.GenText::ToStringPercent(float32, string)
		IL_00bf: call valuetype ['Assembly-CSharp']Verse.NamedArgument ['Assembly-CSharp']Verse.NamedArgument::op_Implicit(string)
		IL_00c4: call valuetype ['Assembly-CSharp']Verse.TaggedString ['Assembly-CSharp']Verse.TranslatorFormattedStringExtensions::Translate(string, valuetype ['Assembly-CSharp']Verse.NamedArgument)
		IL_00c9: call string ['Assembly-CSharp']Verse.TaggedString::op_Implicit(valuetype ['Assembly-CSharp']Verse.TaggedString)
		IL_00ce: stloc.s 10
		IL_00d0: br.s IL_00d2

		IL_00d2: ldloc.s 10
		IL_00d4: ret
	} // end of method FR_GameComp::RefreshEffectDesc

	.method public hidebysig virtual 
		instance void FinalizeInit () cil managed 
	{
		// Method begins at RVA 0x214d
		// Header size: 1
		// Code size: 27 (0x1b)
		.maxstack 8

		IL_0000: nop
		IL_0001: ldsfld class ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectDef FindRelic.MyDefOf::FindRelic
		IL_0006: ldsfld class ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectDef FindRelic.MyDefOf::FindRelic
		IL_000b: ldfld string ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectDef::effectDesc
		IL_0010: call string FindRelic.FR_GameComp::RefreshEffectDesc(string)
		IL_0015: stfld string ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectDef::effectDesc
		IL_001a: ret
	} // end of method FR_GameComp::FinalizeInit

} // end of class FindRelic.FR_GameComp

.class public auto ansi beforefieldinit FindRelic.Settings
	extends ['Assembly-CSharp']Verse.ModSettings
{
	.custom instance void ['Assembly-CSharp']Verse.StaticConstructorOnStartup::.ctor() = (
		01 00 00 00
	)
	// Fields
	.field public static float32 FindRelicChance

	// Methods
	.method public hidebysig specialname rtspecialname 
		instance void .ctor () cil managed 
	{
		// Method begins at RVA 0x2169
		// Header size: 1
		// Code size: 9 (0x9)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void ['Assembly-CSharp']Verse.ModSettings::.ctor()
		IL_0006: nop
		IL_0007: nop
		IL_0008: ret
	} // end of method Settings::.ctor

	.method public hidebysig virtual 
		instance void ExposeData () cil managed 
	{
		// Method begins at RVA 0x2173
		// Header size: 1
		// Code size: 24 (0x18)
		.maxstack 8

		IL_0000: nop
		IL_0001: ldsflda float32 FindRelic.Settings::FindRelicChance
		IL_0006: ldstr "FindRelicChance"
		IL_000b: ldc.r4 1
		IL_0010: ldc.i4.0
		IL_0011: call void ['Assembly-CSharp']Verse.Scribe_Values::Look<float32>(!!0&, string, !!0, bool)
		IL_0016: nop
		IL_0017: ret
	} // end of method Settings::ExposeData

	.method private hidebysig specialname rtspecialname static 
		void .cctor () cil managed 
	{
		// Method begins at RVA 0x218c
		// Header size: 1
		// Code size: 11 (0xb)
		.maxstack 8

		IL_0000: ldc.r4 1
		IL_0005: stsfld float32 FindRelic.Settings::FindRelicChance
		IL_000a: ret
	} // end of method Settings::.cctor

} // end of class FindRelic.Settings

.class public auto ansi beforefieldinit FindRelic.SettingsUI
	extends ['Assembly-CSharp']Verse.Mod
{
	// Methods
	.method public hidebysig specialname rtspecialname 
		instance void .ctor (
			class ['Assembly-CSharp']Verse.ModContentPack content
		) cil managed 
	{
		// Method begins at RVA 0x2198
		// Header size: 1
		// Code size: 22 (0x16)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldarg.1
		IL_0002: call instance void ['Assembly-CSharp']Verse.Mod::.ctor(class ['Assembly-CSharp']Verse.ModContentPack)
		IL_0007: nop
		IL_0008: nop
		IL_0009: ldarg.0
		IL_000a: ldarg.0
		IL_000b: call instance !!0 ['Assembly-CSharp']Verse.Mod::GetSettings<class FindRelic.Settings>()
		IL_0010: stfld class ['Assembly-CSharp']Verse.ModSettings ['Assembly-CSharp']Verse.Mod::modSettings
		IL_0015: ret
	} // end of method SettingsUI::.ctor

	.method public hidebysig virtual 
		instance void DoSettingsWindowContents (
			valuetype [UnityEngine.CoreModule]UnityEngine.Rect inRect
		) cil managed 
	{
		// Method begins at RVA 0x21b0
		// Header size: 12
		// Code size: 117 (0x75)
		.maxstack 4
		.locals init (
			[0] class ['Assembly-CSharp']Verse.Listing_Standard
		)

		IL_0000: nop
		IL_0001: newobj instance void ['Assembly-CSharp']Verse.Listing_Standard::.ctor()
		IL_0006: stloc.0
		IL_0007: ldloc.0
		IL_0008: ldarg.1
		IL_0009: callvirt instance void ['Assembly-CSharp']Verse.Listing::Begin(valuetype [UnityEngine.CoreModule]UnityEngine.Rect)
		IL_000e: nop
		IL_000f: ldloc.0
		IL_0010: ldstr "FindRelicChance"
		IL_0015: ldsfld float32 FindRelic.Settings::FindRelicChance
		IL_001a: ldstr "0"
		IL_001f: call string ['Assembly-CSharp']Verse.GenText::ToStringPercent(float32, string)
		IL_0024: call valuetype ['Assembly-CSharp']Verse.NamedArgument ['Assembly-CSharp']Verse.NamedArgument::op_Implicit(string)
		IL_0029: call valuetype ['Assembly-CSharp']Verse.TaggedString ['Assembly-CSharp']Verse.TranslatorFormattedStringExtensions::Translate(string, valuetype ['Assembly-CSharp']Verse.NamedArgument)
		IL_002e: ldc.r4 -1
		IL_0033: ldnull
		IL_0034: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Rect ['Assembly-CSharp']Verse.Listing_Standard::Label(valuetype ['Assembly-CSharp']Verse.TaggedString, float32, string)
		IL_0039: pop
		IL_003a: ldloc.0
		IL_003b: ldsfld float32 FindRelic.Settings::FindRelicChance
		IL_0040: ldc.r4 0.0
		IL_0045: ldc.r4 1
		IL_004a: callvirt instance float32 ['Assembly-CSharp']Verse.Listing_Standard::Slider(float32, float32, float32)
		IL_004f: stsfld float32 FindRelic.Settings::FindRelicChance
		IL_0054: ldloc.0
		IL_0055: callvirt instance void ['Assembly-CSharp']Verse.Listing::End()
		IL_005a: nop
		IL_005b: ldsfld class ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectDef FindRelic.MyDefOf::FindRelic
		IL_0060: ldsfld class ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectDef FindRelic.MyDefOf::FindRelic
		IL_0065: ldfld string ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectDef::effectDesc
		IL_006a: call string FindRelic.FR_GameComp::RefreshEffectDesc(string)
		IL_006f: stfld string ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectDef::effectDesc
		IL_0074: ret
	} // end of method SettingsUI::DoSettingsWindowContents

	.method public hidebysig virtual 
		instance string SettingsCategory () cil managed 
	{
		// Method begins at RVA 0x2234
		// Header size: 12
		// Code size: 21 (0x15)
		.maxstack 1
		.locals init (
			[0] string
		)

		IL_0000: nop
		IL_0001: ldstr "FindRelic"
		IL_0006: call valuetype ['Assembly-CSharp']Verse.TaggedString ['Assembly-CSharp']Verse.Translator::Translate(string)
		IL_000b: call string ['Assembly-CSharp']Verse.TaggedString::op_Implicit(valuetype ['Assembly-CSharp']Verse.TaggedString)
		IL_0010: stloc.0
		IL_0011: br.s IL_0013

		IL_0013: ldloc.0
		IL_0014: ret
	} // end of method SettingsUI::SettingsCategory

} // end of class FindRelic.SettingsUI

.class public auto ansi beforefieldinit FindRelic.MyDefOf
	extends [mscorlib]System.Object
{
	.custom instance void ['Assembly-CSharp']RimWorld.DefOf::.ctor() = (
		01 00 00 00
	)
	// Fields
	.field public static class ['Assembly-CSharp']RimWorld.QuestScriptDef RelicHunt
	.field public static class ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectDef FindRelic

	// Methods
	.method public hidebysig specialname rtspecialname 
		instance void .ctor () cil managed 
	{
		// Method begins at RVA 0x2255
		// Header size: 1
		// Code size: 8 (0x8)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void [mscorlib]System.Object::.ctor()
		IL_0006: nop
		IL_0007: ret
	} // end of method MyDefOf::.ctor

} // end of class FindRelic.MyDefOf

.class public auto ansi beforefieldinit FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic
	extends ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectWorker
{
	// Nested Types
	.class nested private auto ansi sealed serializable beforefieldinit '<>c'
		extends [mscorlib]System.Object
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Fields
		.field public static initonly class FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c' '<>9'
		.field public static class [mscorlib]System.Func`2<class ['Assembly-CSharp']RimWorld.Precept_Relic, bool> '<>9__1_1'
		.field public static class [mscorlib]System.Func`2<class ['Assembly-CSharp']RimWorld.Quest, bool> '<>9__1_0'
		.field public static class [mscorlib]System.Func`2<class ['Assembly-CSharp']RimWorld.Quest, bool> '<>9__3_0'

		// Methods
		.method private hidebysig specialname rtspecialname static 
			void .cctor () cil managed 
		{
			// Method begins at RVA 0x2569
			// Header size: 1
			// Code size: 11 (0xb)
			.maxstack 8

			IL_0000: newobj instance void FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::.ctor()
			IL_0005: stsfld class FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c' FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::'<>9'
			IL_000a: ret
		} // end of method '<>c'::.cctor

		.method public hidebysig specialname rtspecialname 
			instance void .ctor () cil managed 
		{
			// Method begins at RVA 0x2575
			// Header size: 1
			// Code size: 8 (0x8)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance void [mscorlib]System.Object::.ctor()
			IL_0006: nop
			IL_0007: ret
		} // end of method '<>c'::.ctor

		.method assembly hidebysig 
			instance bool '<GenerateAndSendQuest>b__1_1' (
				class ['Assembly-CSharp']RimWorld.Precept_Relic p
			) cil managed 
		{
			// Method begins at RVA 0x257e
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.1
			IL_0001: callvirt instance bool ['Assembly-CSharp']RimWorld.Precept_Relic::get_CanGenerateRelic()
			IL_0006: ret
		} // end of method '<>c'::'<GenerateAndSendQuest>b__1_1'

		.method assembly hidebysig 
			instance bool '<GenerateAndSendQuest>b__1_0' (
				class ['Assembly-CSharp']RimWorld.Quest q
			) cil managed 
		{
			// Method begins at RVA 0x2586
			// Header size: 1
			// Code size: 44 (0x2c)
			.maxstack 8

			IL_0000: ldarg.1
			IL_0001: ldfld class ['Assembly-CSharp']RimWorld.QuestScriptDef ['Assembly-CSharp']RimWorld.Quest::root
			IL_0006: ldfld string ['Assembly-CSharp']Verse.Def::defName
			IL_000b: ldsfld class ['Assembly-CSharp']RimWorld.QuestScriptDef FindRelic.MyDefOf::RelicHunt
			IL_0010: ldfld string ['Assembly-CSharp']Verse.Def::defName
			IL_0015: call bool [mscorlib]System.String::op_Equality(string, string)
			IL_001a: brfalse.s IL_002a

			IL_001c: ldarg.1
			IL_001d: callvirt instance valuetype ['Assembly-CSharp']RimWorld.QuestState ['Assembly-CSharp']RimWorld.Quest::get_State()
			IL_0022: ldc.i4.1
			IL_0023: cgt
			IL_0025: ldc.i4.0
			IL_0026: ceq
			IL_0028: br.s IL_002b

			IL_002a: ldc.i4.0

			IL_002b: ret
		} // end of method '<>c'::'<GenerateAndSendQuest>b__1_0'

		.method assembly hidebysig 
			instance bool '<GenerateSubquest>b__3_0' (
				class ['Assembly-CSharp']RimWorld.Quest q
			) cil managed 
		{
			// Method begins at RVA 0x25b3
			// Header size: 1
			// Code size: 13 (0xd)
			.maxstack 8

			IL_0000: ldarg.1
			IL_0001: callvirt instance valuetype ['Assembly-CSharp']RimWorld.QuestState ['Assembly-CSharp']RimWorld.Quest::get_State()
			IL_0006: ldc.i4.1
			IL_0007: cgt
			IL_0009: ldc.i4.0
			IL_000a: ceq
			IL_000c: ret
		} // end of method '<>c'::'<GenerateSubquest>b__3_0'

	} // end of class <>c


	// Methods
	.method public hidebysig static 
		void DebugAction () cil managed 
	{
		.custom instance void ['Assembly-CSharp']LudeonTK.DebugActionAttribute::.ctor(string, string, bool, bool, bool, bool, bool, int32, bool) = (
			01 00 06 51 75 65 73 74 73 1d 47 65 6e 65 72 61
			74 65 41 6e 64 53 65 6e 64 46 69 6e 64 52 65 6c
			69 63 51 75 65 73 74 00 00 00 00 00 00 00 00 00
			00 02 00 53 55 67 4c 75 64 65 6f 6e 54 4b 2e 44
			65 62 75 67 41 63 74 69 6f 6e 54 79 70 65 2c 20
			41 73 73 65 6d 62 6c 79 2d 43 53 68 61 72 70 2c
			20 56 65 72 73 69 6f 6e 3d 31 2e 36 2e 39 32 39
			33 2e 31 32 31 36 34 2c 20 43 75 6c 74 75 72 65
			3d 6e 65 75 74 72 61 6c 2c 20 50 75 62 6c 69 63
			4b 65 79 54 6f 6b 65 6e 3d 6e 75 6c 6c 0a 61 63
			74 69 6f 6e 54 79 70 65 00 00 00 00 53 55 69 4c
			75 64 65 6f 6e 54 4b 2e 41 6c 6c 6f 77 65 64 47
			61 6d 65 53 74 61 74 65 73 2c 20 41 73 73 65 6d
			62 6c 79 2d 43 53 68 61 72 70 2c 20 56 65 72 73
			69 6f 6e 3d 31 2e 36 2e 39 32 39 33 2e 31 32 31
			36 34 2c 20 43 75 6c 74 75 72 65 3d 6e 65 75 74
			72 61 6c 2c 20 50 75 62 6c 69 63 4b 65 79 54 6f
			6b 65 6e 3d 6e 75 6c 6c 11 61 6c 6c 6f 77 65 64
			47 61 6d 65 53 74 61 74 65 73 0a 00 00 00
		)
		// Method begins at RVA 0x225e
		// Header size: 1
		// Code size: 8 (0x8)
		.maxstack 8

		IL_0000: nop
		IL_0001: call void FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic::GenerateAndSendQuest()
		IL_0006: nop
		IL_0007: ret
	} // end of method RitualAttachableOutcomeEffectWorker_FindRelic::DebugAction

	.method public hidebysig static 
		void GenerateAndSendQuest () cil managed 
	{
		// Method begins at RVA 0x2268
		// Header size: 12
		// Code size: 353 (0x161)
		.maxstack 3
		.locals init (
			[0] class ['Assembly-CSharp']RimWorld.QuestScriptDef,
			[1] class ['Assembly-CSharp']RimWorld.Ideo,
			[2] class ['Assembly-CSharp']RimWorld.Quest,
			[3] bool,
			[4] bool,
			[5] bool,
			[6] class ['Assembly-CSharp']RimWorld.QuestGen.Slate,
			[7] bool,
			[8] class [mscorlib]System.Exception,
			[9] valuetype ['0Harmony']System.Runtime.CompilerServices.DefaultInterpolatedStringHandler
		)

		IL_0000: nop
		IL_0001: ldstr "Relic hunt rescue"
		IL_0006: call bool ['Assembly-CSharp']Verse.ModLister::CheckIdeology(string)
		IL_000b: ldc.i4.0
		IL_000c: ceq
		IL_000e: stloc.3
		IL_000f: ldloc.3
		IL_0010: brfalse.s IL_0018

		IL_0012: nop
		IL_0013: br IL_0160

		IL_0018: ldsfld class ['Assembly-CSharp']RimWorld.QuestScriptDef FindRelic.MyDefOf::RelicHunt
		IL_001d: stloc.0
		IL_001e: call class ['Assembly-CSharp']RimWorld.Faction ['Assembly-CSharp']RimWorld.Faction::get_OfPlayer()
		IL_0023: ldfld class ['Assembly-CSharp']RimWorld.FactionIdeosTracker ['Assembly-CSharp']RimWorld.Faction::ideos
		IL_0028: callvirt instance class ['Assembly-CSharp']RimWorld.Ideo ['Assembly-CSharp']RimWorld.FactionIdeosTracker::get_PrimaryIdeo()
		IL_002d: stloc.1
		IL_002e: ldloc.1
		IL_002f: callvirt instance class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0> ['Assembly-CSharp']RimWorld.Ideo::GetAllPreceptsOfType<class ['Assembly-CSharp']RimWorld.Precept_Relic>()
		IL_0034: ldsfld class [mscorlib]System.Func`2<class ['Assembly-CSharp']RimWorld.Precept_Relic, bool> FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::'<>9__1_1'
		IL_0039: dup
		IL_003a: brtrue.s IL_0053

		IL_003c: pop
		IL_003d: ldsfld class FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c' FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::'<>9'
		IL_0042: ldftn instance bool FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::'<GenerateAndSendQuest>b__1_1'(class ['Assembly-CSharp']RimWorld.Precept_Relic)
		IL_0048: newobj instance void class [mscorlib]System.Func`2<class ['Assembly-CSharp']RimWorld.Precept_Relic, bool>::.ctor(object, native int)
		IL_004d: dup
		IL_004e: stsfld class [mscorlib]System.Func`2<class ['Assembly-CSharp']RimWorld.Precept_Relic, bool> FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::'<>9__1_1'

		IL_0053: call class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0> [System.Core]System.Linq.Enumerable::Where<class ['Assembly-CSharp']RimWorld.Precept_Relic>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>, class [mscorlib]System.Func`2<!!0, bool>)
		IL_0058: call int32 [System.Core]System.Linq.Enumerable::Count<class ['Assembly-CSharp']RimWorld.Precept_Relic>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
		IL_005d: ldc.i4.0
		IL_005e: ceq
		IL_0060: stloc.s 4
		IL_0062: ldloc.s 4
		IL_0064: brfalse.s IL_0092

		IL_0066: nop
		IL_0067: ldstr "NoRelicsToGenerate"
		IL_006c: ldloc.1
		IL_006d: ldfld string ['Assembly-CSharp']RimWorld.Ideo::name
		IL_0072: call valuetype ['Assembly-CSharp']Verse.NamedArgument ['Assembly-CSharp']Verse.NamedArgument::op_Implicit(string)
		IL_0077: call valuetype ['Assembly-CSharp']Verse.TaggedString ['Assembly-CSharp']Verse.TranslatorFormattedStringExtensions::Translate(string, valuetype ['Assembly-CSharp']Verse.NamedArgument)
		IL_007c: call string ['Assembly-CSharp']Verse.TaggedString::op_Implicit(valuetype ['Assembly-CSharp']Verse.TaggedString)
		IL_0081: ldsfld class ['Assembly-CSharp']Verse.MessageTypeDef ['Assembly-CSharp']RimWorld.MessageTypeDefOf::RejectInput
		IL_0086: ldc.i4.0
		IL_0087: call void ['Assembly-CSharp']Verse.Messages::Message(string, class ['Assembly-CSharp']Verse.MessageTypeDef, bool)
		IL_008c: nop
		IL_008d: br IL_0160

		IL_0092: call class ['Assembly-CSharp']RimWorld.QuestManager ['Assembly-CSharp']Verse.Find::get_QuestManager()
		IL_0097: callvirt instance class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Quest> ['Assembly-CSharp']RimWorld.QuestManager::get_QuestsListForReading()
		IL_009c: ldsfld class [mscorlib]System.Func`2<class ['Assembly-CSharp']RimWorld.Quest, bool> FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::'<>9__1_0'
		IL_00a1: dup
		IL_00a2: brtrue.s IL_00bb

		IL_00a4: pop
		IL_00a5: ldsfld class FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c' FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::'<>9'
		IL_00aa: ldftn instance bool FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::'<GenerateAndSendQuest>b__1_0'(class ['Assembly-CSharp']RimWorld.Quest)
		IL_00b0: newobj instance void class [mscorlib]System.Func`2<class ['Assembly-CSharp']RimWorld.Quest, bool>::.ctor(object, native int)
		IL_00b5: dup
		IL_00b6: stsfld class [mscorlib]System.Func`2<class ['Assembly-CSharp']RimWorld.Quest, bool> FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::'<>9__1_0'

		IL_00bb: call class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0> [System.Core]System.Linq.Enumerable::Where<class ['Assembly-CSharp']RimWorld.Quest>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>, class [mscorlib]System.Func`2<!!0, bool>)
		IL_00c0: call !!0 [System.Core]System.Linq.Enumerable::FirstOrDefault<class ['Assembly-CSharp']RimWorld.Quest>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
		IL_00c5: stloc.2
		IL_00c6: ldloc.2
		IL_00c7: ldnull
		IL_00c8: ceq
		IL_00ca: stloc.s 5
		IL_00cc: ldloc.s 5
		IL_00ce: brfalse IL_0159

		IL_00d3: nop
		IL_00d4: newobj instance void ['Assembly-CSharp']RimWorld.QuestGen.Slate::.ctor()
		IL_00d9: stloc.s 6
		IL_00db: ldloc.0
		IL_00dc: ldloc.s 6
		IL_00de: call class ['Assembly-CSharp']RimWorld.Planet.World ['Assembly-CSharp']Verse.Find::get_World()
		IL_00e3: callvirt instance bool ['Assembly-CSharp']RimWorld.QuestScriptDef::CanRun(class ['Assembly-CSharp']RimWorld.QuestGen.Slate, class ['Assembly-CSharp']RimWorld.IIncidentTarget)
		IL_00e8: ldc.i4.0
		IL_00e9: ceq
		IL_00eb: stloc.s 7
		IL_00ed: ldloc.s 7
		IL_00ef: brfalse.s IL_010f

		IL_00f1: nop
		IL_00f2: ldstr "Cannot generate quest "
		IL_00f7: ldloc.0
		IL_00f8: ldfld string ['Assembly-CSharp']Verse.Def::defName
		IL_00fd: ldstr " with the current parameters."
		IL_0102: call string [mscorlib]System.String::Concat(string, string, string)
		IL_0107: call void ['Assembly-CSharp']Verse.Log::Error(string)
		IL_010c: nop
		IL_010d: br.s IL_0160

		IL_010f: nop
		.try
		{
			IL_0110: nop
			IL_0111: ldloc.0
			IL_0112: ldloc.s 6
			IL_0114: call class ['Assembly-CSharp']RimWorld.Quest ['Assembly-CSharp']RimWorld.QuestUtility::GenerateQuestAndMakeAvailable(class ['Assembly-CSharp']RimWorld.QuestScriptDef, class ['Assembly-CSharp']RimWorld.QuestGen.Slate)
			IL_0119: stloc.2
			IL_011a: nop
			IL_011b: leave.s IL_0150
		} // end .try
		catch [mscorlib]System.Exception
		{
			IL_011d: stloc.s 8
			IL_011f: nop
			IL_0120: ldloca.s 9
			IL_0122: ldc.i4.s 71
			IL_0124: ldc.i4.1
			IL_0125: call instance void ['0Harmony']System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::.ctor(int32, int32)
			IL_012a: ldloca.s 9
			IL_012c: ldstr "Error generating quest possibly due to no available relic to generate: "
			IL_0131: call instance void ['0Harmony']System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
			IL_0136: nop
			IL_0137: ldloca.s 9
			IL_0139: ldloc.s 8
			IL_013b: call instance void ['0Harmony']System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted<class [mscorlib]System.Exception>(!!0)
			IL_0140: nop
			IL_0141: ldloca.s 9
			IL_0143: call instance string ['0Harmony']System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::ToStringAndClear()
			IL_0148: call void ['Assembly-CSharp']Verse.Log::Error(string)
			IL_014d: nop
			IL_014e: leave.s IL_0160
		} // end handler

		IL_0150: ldloc.2
		IL_0151: ldnull
		IL_0152: call void ['Assembly-CSharp']RimWorld.QuestUtility::SendLetterQuestAvailable(class ['Assembly-CSharp']RimWorld.Quest, string)
		IL_0157: nop
		IL_0158: nop

		IL_0159: ldloc.2
		IL_015a: call void FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic::GenerateSubquestForExistingRelicHunt(class ['Assembly-CSharp']RimWorld.Quest)
		IL_015f: nop

		IL_0160: ret
	} // end of method RitualAttachableOutcomeEffectWorker_FindRelic::GenerateAndSendQuest

	.method private hidebysig static 
		void GenerateSubquestForExistingRelicHunt (
			class ['Assembly-CSharp']RimWorld.Quest relicHuntQuest
		) cil managed 
	{
		// Method begins at RVA 0x23e8
		// Header size: 12
		// Code size: 138 (0x8a)
		.maxstack 2
		.locals init (
			[0] class ['Assembly-CSharp']RimWorld.QuestPart_SubquestGenerator_RelicHunt,
			[1] class ['Assembly-CSharp']RimWorld.Precept_Relic,
			[2] bool,
			[3] valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.QuestPart>,
			[4] class ['Assembly-CSharp']RimWorld.QuestPart,
			[5] class ['Assembly-CSharp']RimWorld.QuestPart_SubquestGenerator_RelicHunt,
			[6] bool,
			[7] bool
		)

		IL_0000: nop
		IL_0001: ldarg.0
		IL_0002: callvirt instance class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.QuestPart> ['Assembly-CSharp']RimWorld.Quest::get_PartsListForReading()
		IL_0007: call class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0> [System.Core]System.Linq.Enumerable::OfType<class ['Assembly-CSharp']RimWorld.QuestPart_SubquestGenerator_RelicHunt>(class [mscorlib]System.Collections.IEnumerable)
		IL_000c: call !!0 [System.Core]System.Linq.Enumerable::FirstOrDefault<class ['Assembly-CSharp']RimWorld.QuestPart_SubquestGenerator_RelicHunt>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
		IL_0011: stloc.0
		IL_0012: ldloc.0
		IL_0013: ldnull
		IL_0014: ceq
		IL_0016: stloc.2
		IL_0017: ldloc.2
		IL_0018: brfalse.s IL_001d

		IL_001a: nop
		IL_001b: br.s IL_0089

		IL_001d: ldnull
		IL_001e: stloc.1
		IL_001f: nop
		IL_0020: ldarg.0
		IL_0021: callvirt instance class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.QuestPart> ['Assembly-CSharp']RimWorld.Quest::get_PartsListForReading()
		IL_0026: callvirt instance valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<!0> class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.QuestPart>::GetEnumerator()
		IL_002b: stloc.3
		.try
		{
			IL_002c: br.s IL_0058
			// loop start (head: IL_0058)
				IL_002e: ldloca.s 3
				IL_0030: call instance !0 valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.QuestPart>::get_Current()
				IL_0035: stloc.s 4
				IL_0037: nop
				IL_0038: ldloc.s 4
				IL_003a: isinst ['Assembly-CSharp']RimWorld.QuestPart_SubquestGenerator_RelicHunt
				IL_003f: stloc.s 5
				IL_0041: ldloc.s 5
				IL_0043: ldnull
				IL_0044: cgt.un
				IL_0046: stloc.s 6
				IL_0048: ldloc.s 6
				IL_004a: brfalse.s IL_0057

				IL_004c: nop
				IL_004d: ldloc.s 5
				IL_004f: ldfld class ['Assembly-CSharp']RimWorld.Precept_Relic ['Assembly-CSharp']RimWorld.QuestPart_SubquestGenerator_RelicHunt::relic
				IL_0054: stloc.1
				IL_0055: br.s IL_0061

				IL_0057: nop

				IL_0058: ldloca.s 3
				IL_005a: call instance bool valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.QuestPart>::MoveNext()
				IL_005f: brtrue.s IL_002e
			// end loop

			IL_0061: leave.s IL_0072
		} // end .try
		finally
		{
			IL_0063: ldloca.s 3
			IL_0065: constrained. valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']RimWorld.QuestPart>
			IL_006b: callvirt instance void [mscorlib]System.IDisposable::Dispose()
			IL_0070: nop
			IL_0071: endfinally
		} // end handler

		IL_0072: ldloc.1
		IL_0073: ldnull
		IL_0074: ceq
		IL_0076: stloc.s 7
		IL_0078: ldloc.s 7
		IL_007a: brfalse.s IL_007f

		IL_007c: nop
		IL_007d: br.s IL_0089

		IL_007f: ldloc.0
		IL_0080: ldarg.0
		IL_0081: call void FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic::GenerateSubquest(class ['Assembly-CSharp']RimWorld.QuestPart_SubquestGenerator_RelicHunt, class ['Assembly-CSharp']RimWorld.Quest)
		IL_0086: nop
		IL_0087: br.s IL_0089

		IL_0089: ret
	} // end of method RitualAttachableOutcomeEffectWorker_FindRelic::GenerateSubquestForExistingRelicHunt

	.method private hidebysig static 
		void GenerateSubquest (
			class ['Assembly-CSharp']RimWorld.QuestPart_SubquestGenerator_RelicHunt subquestGenerator,
			class ['Assembly-CSharp']RimWorld.Quest parent
		) cil managed 
	{
		// Method begins at RVA 0x2490
		// Header size: 12
		// Code size: 125 (0x7d)
		.maxstack 3
		.locals init (
			[0] class [mscorlib]System.Reflection.MethodInfo,
			[1] bool,
			[2] valuetype [mscorlib]System.Nullable`1<valuetype ['Assembly-CSharp']RimWorld.QuestState>
		)

		IL_0000: nop
		IL_0001: ldarg.1
		IL_0002: ldloca.s 2
		IL_0004: initobj valuetype [mscorlib]System.Nullable`1<valuetype ['Assembly-CSharp']RimWorld.QuestState>
		IL_000a: ldloc.2
		IL_000b: call class [mscorlib]System.Collections.Generic.IEnumerable`1<class ['Assembly-CSharp']RimWorld.Quest> ['Assembly-CSharp']RimWorld.QuestUtility::GetSubquests(class ['Assembly-CSharp']RimWorld.Quest, valuetype [mscorlib]System.Nullable`1<valuetype ['Assembly-CSharp']RimWorld.QuestState>)
		IL_0010: ldsfld class [mscorlib]System.Func`2<class ['Assembly-CSharp']RimWorld.Quest, bool> FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::'<>9__3_0'
		IL_0015: dup
		IL_0016: brtrue.s IL_002f

		IL_0018: pop
		IL_0019: ldsfld class FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c' FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::'<>9'
		IL_001e: ldftn instance bool FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::'<GenerateSubquest>b__3_0'(class ['Assembly-CSharp']RimWorld.Quest)
		IL_0024: newobj instance void class [mscorlib]System.Func`2<class ['Assembly-CSharp']RimWorld.Quest, bool>::.ctor(object, native int)
		IL_0029: dup
		IL_002a: stsfld class [mscorlib]System.Func`2<class ['Assembly-CSharp']RimWorld.Quest, bool> FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic/'<>c'::'<>9__3_0'

		IL_002f: call int32 [System.Core]System.Linq.Enumerable::Count<class ['Assembly-CSharp']RimWorld.Quest>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>, class [mscorlib]System.Func`2<!!0, bool>)
		IL_0034: ldc.i4.2
		IL_0035: clt
		IL_0037: ldc.i4.0
		IL_0038: ceq
		IL_003a: stloc.1
		IL_003b: ldloc.1
		IL_003c: brfalse.s IL_005c

		IL_003e: nop
		IL_003f: ldstr "TooManyOngoingOrNotAcceptedSubquests"
		IL_0044: call valuetype ['Assembly-CSharp']Verse.TaggedString ['Assembly-CSharp']Verse.Translator::Translate(string)
		IL_0049: call string ['Assembly-CSharp']Verse.TaggedString::op_Implicit(valuetype ['Assembly-CSharp']Verse.TaggedString)
		IL_004e: ldsfld class ['Assembly-CSharp']Verse.MessageTypeDef ['Assembly-CSharp']RimWorld.MessageTypeDefOf::RejectInput
		IL_0053: ldc.i4.0
		IL_0054: call void ['Assembly-CSharp']Verse.Messages::Message(string, class ['Assembly-CSharp']Verse.MessageTypeDef, bool)
		IL_0059: nop
		IL_005a: br.s IL_007c

		IL_005c: ldtoken ['Assembly-CSharp']RimWorld.QuestPart_SubquestGenerator
		IL_0061: call class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
		IL_0066: ldstr "GenerateSubquest"
		IL_006b: ldc.i4.s 36
		IL_006d: call instance class [mscorlib]System.Reflection.MethodInfo [mscorlib]System.Type::GetMethod(string, valuetype [mscorlib]System.Reflection.BindingFlags)
		IL_0072: stloc.0
		IL_0073: ldloc.0
		IL_0074: ldarg.0
		IL_0075: ldnull
		IL_0076: callvirt instance object [mscorlib]System.Reflection.MethodBase::Invoke(object, object[])
		IL_007b: pop

		IL_007c: ret
	} // end of method RitualAttachableOutcomeEffectWorker_FindRelic::GenerateSubquest

	.method public hidebysig virtual 
		instance void Apply (
			class [mscorlib]System.Collections.Generic.Dictionary`2<class ['Assembly-CSharp']Verse.Pawn, int32> totalPresence,
			class ['Assembly-CSharp']RimWorld.LordJob_Ritual jobRitual,
			class ['Assembly-CSharp']RimWorld.RitualOutcomePossibility outcome,
			[out] string& extraOutcomeDesc,
			class ['Assembly-CSharp']Verse.LookTargets& letterLookTargets
		) cil managed 
	{
		// Method begins at RVA 0x251c
		// Header size: 12
		// Code size: 46 (0x2e)
		.maxstack 2
		.locals init (
			[0] bool
		)

		IL_0000: nop
		IL_0001: ldsfld float32 FindRelic.Settings::FindRelicChance
		IL_0006: call bool ['Assembly-CSharp']Verse.Rand::Chance(float32)
		IL_000b: stloc.0
		IL_000c: ldloc.0
		IL_000d: brfalse.s IL_0027

		IL_000f: nop
		IL_0010: call void FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic::GenerateAndSendQuest()
		IL_0015: nop
		IL_0016: ldarg.s extraOutcomeDesc
		IL_0018: ldarg.0
		IL_0019: ldfld class ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectDef ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectWorker::def
		IL_001e: ldfld string ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectDef::letterInfoText
		IL_0023: stind.ref
		IL_0024: nop
		IL_0025: br.s IL_002d

		IL_0027: nop
		IL_0028: ldarg.s extraOutcomeDesc
		IL_002a: ldnull
		IL_002b: stind.ref
		IL_002c: nop

		IL_002d: ret
	} // end of method RitualAttachableOutcomeEffectWorker_FindRelic::Apply

	.method public hidebysig specialname rtspecialname 
		instance void .ctor () cil managed 
	{
		// Method begins at RVA 0x2556
		// Header size: 1
		// Code size: 8 (0x8)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void ['Assembly-CSharp']RimWorld.RitualAttachableOutcomeEffectWorker::.ctor()
		IL_0006: nop
		IL_0007: ret
	} // end of method RitualAttachableOutcomeEffectWorker_FindRelic::.ctor

} // end of class FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic

.class private auto ansi sealed beforefieldinit System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute
	extends [mscorlib]System.Attribute
{
	.custom instance void [mscorlib]System.AttributeUsageAttribute::.ctor(valuetype [mscorlib]System.AttributeTargets) = (
		01 00 01 00 00 00 01 00 54 02 0d 41 6c 6c 6f 77
		4d 75 6c 74 69 70 6c 65 01
	)
	// Methods
	.method assembly hidebysig specialname rtspecialname 
		instance void .ctor (
			string assemblyName
		) cil managed 
	{
		// Method begins at RVA 0x255f
		// Header size: 1
		// Code size: 9 (0x9)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void [mscorlib]System.Attribute::.ctor()
		IL_0006: nop
		IL_0007: nop
		IL_0008: ret
	} // end of method IgnoresAccessChecksToAttribute::.ctor

} // end of class System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute

