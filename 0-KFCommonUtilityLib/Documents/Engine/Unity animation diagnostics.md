# Unity 动画引擎经验：警告、图和绑定

更新：2026-11-09。用途：检索已证实结论，避免重复调查；不是所有动画警告的通用修复指南。

## 先分清调查轨道

1. Animator / AnimatorControllerPlayable 参数与层校验：原生日志策略不一致。
2. AnimationRigging 的 `Could not resolve ... Transform`：流绑定解析失败，不能归为自定义武器 PlayableGraph 的参数警告。
3. 图输出/世界退出生命周期：输出绑定和 target 解绑顺序，不能以静音代替修复。
4. Unbind/Rebind 必要性：见 [AnimationRigging handle lifecycle](AnimationRigging%20handle%20lifecycle.md)，按实际 Animator 根、骨骼树和 job 所有权判断。

## 样本身份和证据强度

当前 Player `UnityPlayer.dll`：FileVersion `2022.3.62.28864`（游戏显示 Unity 2022.3.62f2），SHA256 `a8dcc8f6f4cbee3da56552a4adbe56724228a0efcc2fcd737b516933f0418081`；对应本地 PDB GUID `a61ebfbe-39cd-4c79-b464-f38853db30b5`、age 1。已验证 PE CodeView 和 PDB MSF stream 1 匹配。

Editor 对照：2022.3.54.37157，SHA256 `0bb8a888982d8fc3941c29a6a7837cb9c114b9e13012c5117a51f37c2d1093a8`，PDB GUID `fd5a2dc0-911c-4353-b589-991af184853c`、age 1。不同版本/构建的静态比较不证明版本回归；尚未解释用户 Editor 不复现的现场条件。

原始符号、反汇编和版本数据在 [调查证据目录](../../../../_dsh-workspace/unity-warning-native/)。`.pdata` 的短展开序言不等于完整函数；曾遇到 27 字节的序言和 Ghidra size=1 退化函数体，必须查看真实控制流至 return。

## 参数、层警告：已证实

- Player 的 Animator.logWarnings getter/setter 读写 Animator+`0x48c`；标志确实存在，但不是全局日志闸门。
- `AnimatorControllerPlayable::ValidateParameterString` RVA `0xb58dd0` 直接进入 `DebugStringToFile`，实际函数至 `0xb590a5`；不查询该标志。
- 当前非开发 Player 的 `Animator::ValidateParameterString` RVA `0xb25490` 只格式化、释放错误文本；不能假定 Playable 也经过此实现。
- `ValidateLayerIndex` RVA `0xb58c30` 在失败时输出并返回无效，Animator/Playable 都可走它。普通 Animator.SetLayerWeight 会遍历多个已绑定 ControllerPlayable；主 Animator.layerCount=24 不能证明每个节点都支持层 3。
- Editor `Animator::WarningStringIfLoggingActive` RVA `0x1529070` 检查 Animator+`0x53c` 后才记录；Editor Playable 参数/层验证仍直接记录。故“Editor 编译掉 Playable 警告”不成立。

因此参数预筛选不能由 logWarnings=false 替代。不能 nop 校验器或固定其返回值；那会把无效输入伪装为有效。

## RigBuilder 缺失 Transform：已证实

`AnimationHandleBinder::ResolveAllStreamHandles` RVA `0xb5bcf0` 遍历步长 `0x78` 的绑定记录，先把解析索引置 -1，再调用 RVA `0xb54480` 的辅助解析函数。后者在当前流/Avatar 绑定表中搜索路径哈希；失败使用字符串地址 `0x181a3bec0`：

> Could not resolve '%s' because it is not a child Transform in the Animator hierarchy.

输出到 `DebugStringToFile` (`0x1099d00`) 后返回失败；独立的 invalid Avatar 分支仍在。此路径没有查询 Animator.logWarnings。`Animator::CreateBindings` RVA `0xb21b30` 重建绑定数据并递增 Animator+`0x610` 的代次（最小 2）。辅助解析函数准确 PDB 名未恢复，不臆造名称。

重要边界：这里搜索的是绑定表，而非直接遍历当时的 Transform.parent。文字本身不能证明现场对象已移出层级；旧 job、旧绑定代次、重建顺序、真实骨骼归属需同次观测。失败原样返回，不能吞校验。

历史 [run-11 日志](../../../../_dsh-workspace/weapon-acceptance-33/run-11/game.log) 有 402 条同类警告；run-17/18/19 分别 740/370/370。`destroy animation rig took` 是项目 RemoveFpv 的日志，不是 DestroyFpv，也不是对象已经释放的证明。

后续裁剪实验获得新阳性反例：共享 BuildRig 同时略过 Stream/Scene Unbind 和 Rebind、仅调用 RigBuilder.Build 时，在 BoneCrusher→Evis 的首次切换出现同类洪流，见 [baseline/game.log:2242](../../../../_dsh-workspace/rig-stream-prune/baseline/game.log#L2242)。故不能用包的 Clear/DestroyJobs 推断全局原生 stream 绑定记录已经清理；也不能把该阶段末尾的驱动 PASS 当安全结论。最终保留两处 Stream Unbind，仅移除两处 Scene Unbind 和两处 Animator.Rebind，重新编译并以新进程验证；具体骨骼域、失败时间线和验收边界记录在 [句柄生命周期文档](AnimationRigging%20handle%20lifecycle.md)。

## 已验证的图生命周期与失败实验

当前 AnimationGraphBuilder.DestroyGraph 在 CleanupBehaviours 后、graph.Destroy 前对自有 AnimationPlayableOutput.SetTarget(null)。独立实机 final-14 的两把测试武器及退出路径通过；不等于所有武器/多人/全部重建验收。

曾把整个 DestroyGraph 提前到旧 target.Remove 之前，final-13 仍出现层索引警告，已撤销。保持原 SetCurrentTarget 主顺序：旧 target 非 current → SetEnabled(false) 内部 Remove/BuildRig → 移除附属节点 → 销毁旧武器图 → 新 target 启用和挂接。不要把失败实验重新当修复。

SaveAndCleanupWorld Prefix 会对所有 builder（含 inactive）执行完整 SetCurrentTarget(null)，并非仅解绑输出。退出要单独验证。CleanupBehaviours 会触发生命周期回调，必须发生在旧 playables 仍有效时。

## 已完成的实机范围与未证明事项

未修改生产代码的 rig-rebuild-1/2：跨普通 RigTargets/PlayGraph/原版武器、FPV/TPV、空手、模型刷新；染料安装/卸除；装备头盔安装/还原；每 .08s 请求快速切换、每轮最终状态；三次原生上/下自行车及 mounted 视角刷新。原生日志保持，警告洪流未在该轮复现，不能称历史根因已修复。

详见 [可复现调查记录](../../../../_dsh-workspace/unity-warning-native/INVESTIGATION.md) 和 [rig-rebuild-2 日志](../../../../_dsh-workspace/weapon-acceptance-33/rig-rebuild-2/game.log)。单独保留原版退出 E_XBL_NOT_INITIALIZED，不把整份日志称为零错误。测试空槽必须用 Hand.UsingBareHand；Equipment.PreferredItemSlot 是历史偏好，不是装甲类别槽，应读 ItemClassArmor.EquipSlot。

## 不修改原始文件的静音候选：仅备忘，未实施

独立 native 插件可运行时 detour `void DebugStringToFile(const DebugStringToFileData&)`，按精确调用点/Warning 过滤，其他同步转发。必须在直接原生入口捕获返回地址，匹配模块哈希、ABI、结构和指令；未知构建保留日志。它仍修改进程可执行内存，有崩溃及其他 Hook 冲突风险。

共享层校验器仅靠日志返回地址分不出 Animator/Playable；需要已证明来源，或排除此点。不能在全局日志热路径调用托管或保存借用数据。重定向单个日志 call 也有同类风险。ILogHandler / logMessageReceived 尚未证明能取消这条原生记录。静音不解决无效绑定、解析成本或卡顿。

## 后续调查的最小入口

先读本文和句柄生命周期文档；仅在版本/调用链变化或出现新的现场反例时重跑对应原生定位。保存确切模块身份、原始失败日志、真实 Animator/武器根和 job 代次。一般编译/托管 mock 不能证明 Unity/Mono 原生绑定正确；RigBuilder.Build=true 也不能证明约束实际生效。
