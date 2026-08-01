using System.Collections.Generic;
using System.Reflection.Emit;
using System.Xml.Linq;
using CodeWriter.ExpressionParser;
using HarmonyLib;
using JetBrains.Annotations;
using KFCommonUtilityLib.Attributes;
using UniLinq;

namespace KFCommonUtilityLib
{
    public abstract class CVarReplacer : ICustomXElementParser
    {
        protected readonly List<EntityAlive> targets = new();
        public abstract bool ParseModuleAttribute(MinEventActionBase parent, XAttribute attr);
        public virtual bool ParseModuleElements(MinEventActionBase parent, XElement elem)
        {
            return false;
        }
        
        [HarmonyPatch(nameof(Transpiler_EntityBuffs_GetCustomVar))]
        [TargetMethods]
        [UsedImplicitly]
        private static IEnumerable<HarmonyPatch> TargetMethods_GetCVarFromEntityBuffs()
        {
            //MinEventActionAddBuff is handled differently
            yield return new(typeof(MinEventActionAddBuff), nameof(MinEventActionAddBuff.Execute));
            yield return new(typeof(MinEventActionAddHealth), nameof(MinEventActionAddHealth.Execute));
            yield return new(typeof(MinEventActionAwardChallenge), nameof(MinEventActionAwardChallenge.Execute));
            yield return new(typeof(MinEventActionAwardQuestStat), nameof(MinEventActionAwardQuestStat.Execute));
            yield return new(typeof(MinEventActionCVarLogValue), nameof(MinEventActionCVarLogValue.Execute));
            yield return new(typeof(MinEventActionGiveExp), nameof(MinEventActionGiveExp.Execute));
            yield return new(typeof(MinEventActionModifyCVar), nameof(MinEventActionModifyCVar.Execute));
            yield return new(typeof(MinEventActionModifyCVarWithSelfRef), nameof(MinEventActionModifyCVarWithLocalCache.Execute));
            yield return new(typeof(MinEventActionModifyStat), nameof(MinEventActionModifyStat.Execute));
            yield return new(typeof(MinEventActionModifyStats), nameof(MinEventActionModifyStats.execute));
            yield return new(typeof(MinEventActionRagdoll), nameof(MinEventActionRagdoll.Execute));
            yield return new(typeof(MinEventActionShakeCamera), nameof(MinEventActionShakeCamera.Execute));
            yield return new(typeof(MinEventActionItemCountRandomBase), "GetCount");
            yield return new(typeof(MinEventActionSetNavObject), nameof(MinEventActionSetNavObject.Execute));
            yield return new(typeof(MinEventActionSetPartActive), nameof(MinEventActionSetPartActive.Execute));
            yield return new(typeof(MinEventActionSetSkinMaterialProperty), nameof(MinEventActionSetSkinMaterialProperty.Execute));
        }

        [MethodTargetTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_EntityBuffs_GetCustomVar(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            
            var mtd_get_cvar_buff = AccessTools.Method(typeof(EntityBuffs), nameof(EntityBuffs.GetCustomVar));
            var mtd_get_cvar_entity = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.GetCVar));
            var prop_get_item = AccessTools.IndexerGetter(typeof(List<EntityAlive>), new[] { typeof(int) });
            var fld_targets =
                AccessTools.Field(typeof(MinEventActionTargetedBase), nameof(MinEventActionTargetedBase.targets));
            
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_get_cvar_buff) || codes[i].Calls(mtd_get_cvar_entity))
                {
                    // which target should we check for? if not found, use param.self
                    CodeInstruction ins_load_target_index = null;
                    for (int j = i - 1; j >= 2; j--)
                    {
                        if (codes[j].Calls(prop_get_item) && codes[j - 2].LoadsField(fld_targets))
                        {
                            ins_load_target_index = new(codes[j - 1].opcode, codes[j - 1].operand);
                            break;
                        }
                    }

                    codes.InsertRange(i + 1, new[]
                    {
                        new(codes[i - 1].opcode, codes[i - 1].operand),
                        new(OpCodes.Ldarg_1),
                        new(OpCodes.Ldarg_0),
                        CodeInstruction.Call(typeof(ModuleExtensions), nameof(ModuleExtensions.LoadModule), null, new[] { typeof(CVarReplacer) }),
                        ins_load_target_index ?? new(OpCodes.Ldc_I4_M1),
                        CodeInstruction.Call(typeof(CVarReplacer), nameof(GetCVarOverrideWrapper))
                    });
                    
                    // if cvar name comes from field. should always be loading from MEA itself
                    if (codes[i - 1].opcode == OpCodes.Ldfld)
                    {
                        codes.Insert(i + 1, new(OpCodes.Ldarg_0));
                        i++;
                    }
                    i += 6;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(MinEventActionAddBuff), nameof(MinEventActionBase.Execute)), MethodTargetTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_MinEventActionAddBuff_Execute(
            IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var mtd_initial_max = AccessTools.PropertyGetter(typeof(BuffClass), nameof(BuffClass.InitialDurationMax));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_initial_max))
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(MinEventActionAddBuff), nameof(MinEventActionAddBuff.refCvarName)),
                        new(OpCodes.Ldarg_1),
                        new(OpCodes.Ldarg_0),
                        CodeInstruction.Call(typeof(ModuleExtensions), nameof(ModuleExtensions.LoadModule), null, new[] { typeof(CVarReplacer) }),
                        new(OpCodes.Ldloc_2),
                        CodeInstruction.Call(typeof(CVarReplacer), nameof(GetCVarOverrideWrapper))
                    });
                    break;
                }
            }

            return codes;
        }

        public static float GetCVarOverrideWrapper(float _originalValue, string _cvarName, MinEventParams _params, CVarReplacer _module, int _targetIndex)
        {
            if (_targetIndex < 0 || _targetIndex >= _module.targets.Count)
                return _originalValue;
            return _module.GetCVarOverride(_originalValue, _cvarName, _params, _targetIndex);
        }

        protected abstract float GetCVarOverride(float  _originalValue, string _cvarName, MinEventParams _params, int _targetIndex);

        [HarmonyPatch(nameof(MinEventActionBase.CanExecute)), MethodTargetPostfix]
        public void Postfix_CanExecute(MinEventParams _params)
        {
            targets.Clear();
            targets.Add(_params.Self);
        }

        [HarmonyPatch(typeof(MinEventActionTargetedBase), nameof(MinEventActionBase.CanExecute)), MethodTargetPostfix]
        public void Postfix_MinEventActionTargetedBase_CanExecute(MinEventActionTargetedBase __instance)
        {
            targets.Clear();
            targets.AddRange(__instance.targets);
        }

        protected virtual VariableInfo ParseCustomVariables(string varStr) => null;
    }
}