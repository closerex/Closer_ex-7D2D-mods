# AnimationRigging 句柄生命周期与解绑裁剪

## 范围与证据等级

本记录针对当前游戏 Unity 2022.3.62f2，精确读取游戏 Managed 中的 Unity.Animation.Rigging.dll 与 UnityEngine.AnimationModule.dll。不是跨版本 Unity 保证。此前引擎警告与图生命周期结论见 [Unity animation diagnostics](Unity%20animation%20diagnostics.md)。

本次工作顺序：先记录未修改基线的真实骨骼树、约束引用与原生攻击/装填采样，再分别判断 Stream 清理、Scene 清理和 Animator.Rebind；最后记录实际保留的生产修改与重新启动验证。运行实验不等同于源码构建验收。

## 包的清理闭环

- `RigBuilder.Build()` 首先 `Clear()`，取当前 Animator 和 `layers.ToArray()`，重新创建 jobs/playables，最后 Play。`Build(PlayableGraph)` 也先 Clear，但使用调用方图，不能把该图所有权当作 RigBuilder 自有图。
- `Clear()` 销毁自己的有效 graph，逐个 runtime layer `Reset()`，再重置 SyncSceneToStream 层。`RigLayer.Reset()` 调用 `RigUtils.DestroyAnimationJobs`，清空 constraints/jobs 和 initialized 标记。
- `RigLayer.Initialize(animator)` 重新收集约束并调用 CreateJob/CreateAnimationJobs。它不是只把旧 job 接到新输出；但该闭环不等于原生 Animator 全局绑定记录已清空。
- Build/Clear 的包实现没有显式调用 Animator.UnbindAllStreamHandles、UnbindAllSceneHandles 或 Animator.Rebind。因此不能用“Unity 包内部必然调用这三个 API”来论证重复。
- inactive layer 也参与 Initialize/CreateJob；只检查 layer.active 或 activeInHierarchy 不足以证明没有注册句柄。状态有效、Build=true 也不足以证明约束确实求值。

## Stream 与 Scene 不可混为一谈

`ReadOnlyTransformHandle.Bind(animator, transform)` 在 Bind 当时调用 IsChildOf：子级走 BindStreamTransform，非子级走 BindSceneTransform，并将选择存入 m_InStream。后续访问不会重新按层级选择句柄类别。

`ReadWriteTransformHandle.Bind` 只允许 Animator 子级；非子级会抛 `Transform '<name>' is not a child of the Animator hierarchy, and cannot be written to.`。因此外部只读源是合法场景，外部写目标不是。

`TransformStreamHandle.CheckIsValidAndResolve` 在 handle/stream binding version 不一致或 skeletonIndex 未解析时调用 native ResolveInternal。它支持版本变化后的重解析，不证明未刷新 binding generation 的层级改变能自动修复。

`SyncSceneToStream` 收集规则是另一条机制：只把标记字段中属于 Animator 子级的 Transform 加入同步集合。这不禁止 ReadOnlyTransformHandle 指向 Animator 外部，也不消除 Scene 句柄。

## 当前项目调用域

- [AnimationTargetsAbs.cs](../../KFAttached/RigAdaptors/AnimationTargetsAbs.cs) 的 BuildRig 原先每次执行 Stream Unbind → Scene Unbind → Animator.Rebind → RigBuilder.Build。FPV/TPV 注入与移除 layer 均调用它。
- [AnimationRiggingPatches.cs](../../Harmony/AnimationRiggingPatches.cs) 原先在 SDCSUtils.setupEquipmentCommon 前全 Unbind 两类句柄，之后 Rebind。原版自身已通过 RigBuilder.enabled=false/true 触发 Clear/Build；仍需按装备约束和原生更新域单独验证，而不是从武器切换推定全局冗余。
- [ItemAnimatorUpdate.cs](../../KFAttached/Misc/ItemAnimatorUpdate.cs) 对普通 FPV 武器有独立 Animator/手动 PlayableGraph，并调用 weaponRB.Build(graph)/SyncLayers。玩家 RigBuilder 正常不能证明该武器图正常。
- RigConverter.Rebind 是引用适配器重定位，不是 Animator.Rebind。本次不能因名字相同而移除。
- 保持 AnimationGraphBuilder.SetCurrentTarget 原顺序：旧 target 禁用/Remove → RemoveAttachTrans → DestroyWeapon/DestroyGraph → 新 target 启用/Setup → MoveAttachTrans。不要恢复此前失败的提前整图销毁实验。

## 未改基线的实机骨骼树

隔离存档 __mcp_work_luaprobe_20261002 / Navezgane，EnemySpawnMode=false。本轮未修改生产源码、原始程序集或日志策略。证据目录 `_dsh-workspace/rig-stream-prune/baseline`（从仓库根的父目录定位）。

实际 FPV BoneCrusher：玩家 Animator 路径是 `Players/Player_171/Camera/UFPSRoot/baseRigFP`；武器 Animator 在兄弟分支 `.../UFPSRoot/ItemFpv/ItemFpvRoot`。玩家约束的 LeftArmRoot/Target 等只读源位于武器分支，IsChildOf(playerAnimator)=false，实际需要 Scene 读句柄；写入的玩家臂骨仍在 baseRigFP 子树。不能以“所有骨骼都在玩家对象下”推断 Scene 不存在。

同轮 Eviscerator 的外部源位于 `.../Wep_1stP_SawBlade_Rig/Root/LeftArm_1stP/Target`。武器自身 WeaponRig 的多位置/旋转约束则位于自己的 Animator 子树。TPV 是另一个 `.../Graphics/Model/player_FemaleRagdoll` Animator 域。

完整快照包括 Animator/Transform/父级 instance ID、路径、Avatar ID/valid、RigBuilder/layers、约束 weight、data 内 Transform 的 inside 标记。包含 inactive 项以覆盖 Build 的初始化范围；分析“实际求值”还要加 Animator/组件/layer 的启用条件，不能把快照条数全部当作活动约束数。

基线覆盖 BoneCrusher、Eviscerator、Medic、AUG、Scrap、原版手枪与空手的 FPV/TPV，共 14 个完成状态、229 个姿态样本。初版观察器直接反射 ref-return data 遇到 NotSupportedException，改读 RigConstraint 基类 m_Data。另一个初版错误是按约束 GameObject.activeInHierarchy 过滤：inactive 约束仍可能被包初始化、用于有效 layer，导致后续复用时漏计。最终观察器不再使用此过滤，不把早期样本当作完整约束对照。原生装填只有真正进入 busy 的情况才算完成。

观察域还必须与目标一致：ItemInventoryData.model 是模型子节点，独立 ItemAnimatorUpdate/WeaponRig 在其祖先 ItemFpvRoot 上。对模型子节点搜索得到零个约束，不能推断武器没有约束；应从实际活动 ItemAnimatorUpdate 的 Animator 根采集。AUG 也没有 BoneCrusher/Evis 的十二个额外 FPV TwoBoneIK，不能给所有武器套同一个约束数量下限。

## 分项实验和最终裁剪

1. 共享 BuildRig 先只略过 Scene Unbind，再同时略过 Scene Unbind/Rebind；装备补丁仍保持原样。两阶段均完成原生切换驱动，但早期姿态统计存在上述 inactive 过滤错误，不能仅据驱动 PASS 判安全。
2. 全略过阶段仅调用 rb.Build()。在第一次 BoneCrusher→Evis 切换后立即出现 `Could not resolve 'Rig_UID_0/LeftShoulderReposition'` 洪流：[原始日志 2242 行](../../../../_dsh-workspace/rig-stream-prune/baseline/game.log#L2242)。这是 all-off 阶段内发生的失败，不是首次发生在恢复旧方法之后。随后进程已污染，恢复旧方法所得 motion-original 不能算独立干净对照。保留这条反例，不删除 Stream 清理，不静音。
3. 最终 [AnimationTargetsAbs.BuildRig](../../KFAttached/RigAdaptors/AnimationTargetsAbs.cs#L576) 保留 `animator.UnbindAllStreamHandles(); rb.Build();`，删除 Scene Unbind 和 Animator.Rebind。[装备前缀](../../Harmony/AnimationRiggingPatches.cs#L18) 同样只保留 Stream Unbind，删除为后置 Rebind 服务的 __state 和整个配对 postfix。原版 enabled=false/true 的 Clear/Build 闭环不变。
4. Scene 只读源确实存在；删除的是当前重建闭环中的全局 Scene 清理，不是删除 Scene 绑定本身。新 job 重新 Bind 当前引用；保留 Scene 的理由不能仅是“存在 Scene 句柄”。这也不是所有项目都可删除 Scene 清理的通用定理，未作长期原生绑定存储增长基准。

RigConverter.Rebind、图切换顺序、旧 target 清理时机、weapon manual graph 均未改变。仅同步实际修改的 AnimationTargetsAbs Unity 镜像。

## 构建与最终实机证据

普通 Build 会触发双向 Unity 同步和版本更新，本次使用限定 Compile/Copy 链。首轮增量构建虽然退出成功却复用了旧 DLL：CAS 编辑保留了 mtime，已明确判为陈旧构建。随后只删除本项目 obj/Debug/KFCommonUtilityLib.dll 强制编译，验证新 obj/output DLL 与 PDB 一致、项目和 ModInfo 未变化。

最终 Core SHA256 `add67e17ff3764829ae1cc3a8da54aaec1f742aee7cf45107ad435d6a48b97a1`；运行时再次解析实际 BuildRig IL，确认只有 Stream Unbind/Build，装备 postfix 不存在。不是旧 DLL 上临时 hook 的验收。[构建日志](../../../../_dsh-workspace/kf-debug-rig-stream-prune-forced-20261109.log)。

[最终进程日志](../../../../_dsh-workspace/rig-stream-prune/final/game.log) 和 [分析器](../../../../_dsh-workspace/rig-stream-prune/analyze-final.py) 保留完整结果：骨骼样本含 tip/source ID、位置、权重和 inactive 标记；实际染料安装/卸除产生新 model ID；头盔装卸命中原版装备回调；FPV/TPV 快切；Scrap 实际弹量 4→3、6→5，装填 busy=true 后分别 3→6、5→6；三次原生载具进入、mounted 刷新、Detach 回到 Current。独立武器 manual graph 的有效性和时间前进单独观测，不以玩家 graph 代替。

最终分析输出见 [analysis.json](../../../../_dsh-workspace/rig-stream-prune/final/analysis.json)：六组共 144 个玩家域样本；另从活动 ItemAnimatorUpdate 根记录 24 个武器域样本、十个 MultiPosition 约束及源/写入骨骼 ID，观察原生攻击时两侧位置持续变化。它是运行响应证据，不是全资产姿态数学等价或性能收益声明。

过程失败也保留：隔离角色先死亡，死亡后的装备等待超时不计验收；官方重生后恢复掉落背包的原版偏好快照，再经空手切换重跑。另一次在暂停状态启动驱动超时，以及一次从错误模型子节点采集武器约束的观察器失败，均不是已通过结果或生产修复。分析器明确列出被拒绝的驱动/观察器尝试，检测裸 Unity Exception，不只搜索 ERR/EXC。最终样本和成功段独立判断，不声称整份日志从未出错。另有一次原版 Discord analytics HTTP 503、一次退出时 E_XBL_NOT_INITIALIZED，均独立列入分析结果，不扩散忽略范围。

清理：两个被动跟踪 hook 删除后 active/residual 均为零；临时染料移除、头盔恢复为空；仅自建自行车 259 已移除，原有 249/250 保留；隔离角色掉落背包 256 经原生 Bag/Inventory API 恢复并移除；随后原生 shutdown，日志记录 OnApplicationQuit。无引擎文件修改、日志屏蔽、普通 Build、版本提升、提交或推送。

## 必须保留的边界

- 旧 job 销毁不自动证明原生绑定记录生命周期；global Unbind 的影响范围也不是一个 RigLayer。
- 层级父级、Avatar 和实际绑定代次都会变化；同 Animator instance 不代表同拓扑。
- 先分别验证必要性，再删除。不得用过滤 missing-transform 日志掩盖约束失效。
- 影响评估：只做这些调用覆盖的切换、视角、实际重建、约束与攻击/装填、装备和载具回归；不跑无关模块矩阵。不声称多人/所有资产已覆盖。
