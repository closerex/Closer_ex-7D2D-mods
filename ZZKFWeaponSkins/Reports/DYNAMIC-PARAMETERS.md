# 游戏驱动 Shader 参数：对应关系与未接线边界

## 当前状态

全部 482 个皮肤均保留基础材质替换，包含依赖动态参数的皮肤；631 条精确物品覆写已生成。本文只报告参数，不添加游戏脚本驱动。原调查中的 1052 是去重材质数，不是槽位总数。

时间滚动、脉冲、视线/屏幕输入和反射由 Shader/Unity 自行提供，不需要伪装成弹药或命中参数。静态旋转角也不应误接为时间。

## ammoCount

满弹匣=0，空弹匣=1。原 Timer_UpdateWeaponSkin 仅在 GetAmmoType(CurrentFireMode)==0 时更新。

公式/消费方式：1 - AmmoCount[AmmoType] / MagazineCapacity[AmmoType]

原版依据：KFWeapon.uc:1222-1247

对应属性：
- [KFStandardSpecularDynamic.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedDynamic/KFStandardSpecularDynamic.shader>)：_AmmoCount
- [Hellblade.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedHellblade/Hellblade.shader>)：_UE_AmmoCount
- [KFToxic.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedToxic/KFToxic.shader>)：_UE_AmmoCount
- [Spec_Glow.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedSpecGlow/Spec_Glow.shader>)：_UE_AmmoCount
- [KFChameleon.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedChameleon/KFChameleon.shader>)：_CT_AmmoCount

## shotsHit

原文不 clamp；不能擅自改成限制在 0..1。重装流程清零；具体射击/命中计数事件需接现有武器逻辑。

公式/消费方式：ShotsHit / MagazineCapacity[AmmoType]

原版依据：KFWeapon.uc:1234,1242,4196,7352

对应属性：
- [KFStandardSpecularDynamic.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedDynamic/KFStandardSpecularDynamic.shader>)：_ShotsHit
- [KFChameleon.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedChameleon/KFChameleon.shader>)：_CT_ShotsHit

## shotsFired

原脚本会写，当前已检查的换肤 Shader 没有对应属性，暂不接线。

公式/消费方式：(MagazineCapacity - AmmoCount) / MagazineCapacity

原版依据：KFWeapon.uc:1233,1241

对应属性：
- 无当前消费属性。

## bloodContrast

当前 7DTD 近战 Mod 原本已有装备、命中与清血事件写入器；本轮把所有迁移接口统一为 `_BloodContrast`，不新增 DLL 或游戏驱动。实际复测现有粉碎机 SkinId_7519：原生切换装备事件使活动 FP 材质从 0.2 写为物品血迹值 0，见 [游戏回执](<J:/KF2MaterialBridge/skin-issues/game-pulverizer-equipped.json>)。未单独制造命中事件，也未手工写材质。

原写入器按当前 FP/TP 视角选择索引；`VariableAsCVar` 模块把 `#bloodLevel` 元数据接到动作变量，并非单纯读取一个未同步的普通 CVar。源材质关闭的血迹门控保持关闭，不能把“统一参数名”等同于强制所有 TP 出血。以下公式是 KF2 原版语义，不是本轮新增的 7DTD 驱动。

AddBlood 的下限为 0.20，但清血动画通知可以写回 0。不是每次挥击都清零。1P 武器与 3P attachment 各自有累积/更新路径；保持实例材质原值，不统一覆盖成 0.2。

公式/消费方式：FMax(BloodParamValue + NewBlood, MinBloodParamValue)

原版依据：KFWeapon.uc:3338-3356; KFWeap_MeleeBase.uc:376-390; KFWeaponAttachment.uc:678-691

对应属性：
- [KFStandardSpecularDynamic.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedDynamic/KFStandardSpecularDynamic.shader>)：_BloodContrast
- [KFStandardSpecularWorkshop.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedWorkshop/KFStandardSpecularWorkshop.shader>)：_BloodContrast
- [KFStandardSpecularPreciousSkin.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedPreciousSkin/KFStandardSpecularPreciousSkin.shader>)：_BloodContrast
- [KFStandardSpecular.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModified/KFStandardSpecular.shader>)：_BloodContrast
- [KFChameleon.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedChameleon/KFChameleon.shader>)：_BloodContrast
- [KFChameleon3P.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedChameleon/KFChameleon3P.shader>)：_BloodContrast
- [KFToxic.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedToxic/KFToxic.shader>)：_BloodContrast
- [Hellblade.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedHellblade/Hellblade.shader>)：_BloodContrast
- [Spec_Glow.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedSpecGlow/Spec_Glow.shader>)：_BloodContrast
- [Spec_Glow_3P.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedSpecGlow/Spec_Glow_3P.shader>)：_BloodContrast
- [Spec.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedSpec/Spec.shader>)：_BloodContrast
- [Spec_3P.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedSpec/Spec_3P.shader>)：_BloodContrast
- [KFNeon.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedNeon/KFNeon.shader>)：_BloodContrast
- [KFNeon_3P.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedNeon/KFNeon_3P.shader>)：_BloodContrast
- [KFWorkshop_3P.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedWorkshop3P/KFWorkshop_3P.shader>)：_BloodContrast
- [KFItemPrecious.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedItemPrecious/KFItemPrecious.shader>)：_BloodContrast
- [KFPreciousSkin_3P.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedPreciousSkin3P/KFPreciousSkin_3P.shader>)：_BloodContrast
- [KFWeaponBasic.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedWeaponBasic/KFWeaponBasic.shader>)：_BloodContrast

## upgradeLvl

找到的原脚本写的是掉落物 Upgrade，不足以证明武器皮肤 UpgradeLvl 的驱动。不能直接等同 7DTD 物品品质。

公式/消费方式：Shader uses ceil(saturate(_UpgradeLvl - upgradeMask))

原版依据：KFDroppedPickup.uc:218-227

对应属性：
- [KFStandardSpecularDynamic.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedDynamic/KFStandardSpecularDynamic.shader>)：_UpgradeLvl

## heatUp

确认 Shader 有颜色参数；未找到可靠原脚本写入证据，保留材质值，不猜测温度/连续开火映射。

公式/消费方式：HDR color parameter

原版依据：No confirmed script writer

对应属性：
- [KFStandardSpecularDynamic.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedDynamic/KFStandardSpecularDynamic.shader>)：_HeatUpColor
- [KFNeon.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedNeon/KFNeon.shader>)：_UE_Vector_HeatupColor
- [KFNeon_3P.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedNeon/KFNeon_3P.shader>)：_UE_Vector_HeatupColor
- [Spec_Glow.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedSpecGlow/Spec_Glow.shader>)：_UE_Vector_HeatupColor
- [Spec_Glow_3P.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedSpecGlow/Spec_Glow_3P.shader>)：_UE_Vector_HeatupColor
- [KFToxic.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedToxic/KFToxic.shader>)：_UE_Vector_HeatupColor
- [Hellblade.shader](<J:/Unity Projects/AnimationPlayground/Assets/Shaders/StandardShaderModifiedHellblade/Hellblade.shader>)：_UE_Vector_HeatupColor

## 原文与实现边界

- [原版武器更新和血迹逻辑](<E:/SteamLibrary/steamapps/common/killingfloor2/Development/_Src/KFGame/Classes/KFWeapon.uc#L1222-L1247>)。
- [清血动画通知](<E:/SteamLibrary/steamapps/common/killingfloor2/Development/_Src/KFGame/Classes/KFWeap_MeleeBase.uc#L376-L390>)，不是每次挥击的无条件重置。
- [第三人称血迹累积](<E:/SteamLibrary/steamapps/common/killingfloor2/Development/_Src/KFGame/Classes/KFWeaponAttachment.uc#L678-L691>)。
- [掉落物 Upgrade 写入](<E:/SteamLibrary/steamapps/common/killingfloor2/Development/_Src/KFGame/Classes/KFDroppedPickup.uc#L218-L227>)不证明 UpgradeLvl。
- 属性存在不代表每个材质静态开关分支都启用该效果；未来接线要按具体 Shader/实例分支选择，不改写共享资产。
- SpecGlow 1P/3P 与 LaserBeam 的 ObjectWorldPosition 还需要宿主安装 [KF2UECoordinateBindings](<J:/Unity Projects/AnimationPlayground/Assets/KFAttached/Render/KF2UECoordinateBindings.cs>)。此资源/XML Mod 不含这项宿主 DLL 部署。
- 此处是源代码与资源对应表，不是已完成游戏事件、联机或存档验证。
