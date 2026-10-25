# AllowComboKeys 离线回归

本工程直接编译当前生产仲裁、快照构造器及补丁源码，引用本机游戏提供的 InControl、UnityEngine.CoreModule、Assembly-CSharp、LogLibrary 和 0Harmony 程序集；不构建或覆盖游戏 Mods 内的 DLL。

## 运行

在游戏根目录执行，需安装 .NET SDK、.NET 8 运行时与 .NET Framework 4.8 的构建/运行环境：

```bash
dotnet build ModsRepo/AllowComboKeys.Tests/AllowComboKeys.Tests.csproj -c Debug
dotnet ModsRepo/AllowComboKeys.Tests/bin/Debug/net8.0/ComboImplementationEvidence.dll
ModsRepo/AllowComboKeys.Tests/bin/Debug/net48/ComboImplementationEvidence.exe --harmony
```

可添加 `--report=绝对日志路径` 将输出写为逐行日志；失败返回非零退出码。`--wait` 供调试器在 READY 后等待标准输入。

## 两种宿主分别验证什么

- 默认 .NET 8：调用真实生产的输入 Prefix/Finalizer 和 GetValue 前缀；清空/恢复默认用例在执行原生方法前显式调用同一个生产入口前缀。不声称验证自动拦截。
- .NET Framework 4.8 的 `--harmony`：使用游戏自带 0Harmony，以 `CreateClassProcessor(typeof(Patches)).Patch()` 解析并安装整个生产补丁类。验证 ClearBindings/ResetBindings/GetValue 的实际拦截，不手工通知清空/恢复默认入口。仍由测试直接调用输入 Prefix/Finalizer/ResetInternal 回调，不执行完整原生 InputManager 更新循环。

按实际流程收敛：只对清空绑定及恢复默认的同引用复用做定点清理；其它正常替换/保存加载/owner 终点变化由下一输入 tick 的真实绑定图识别。不 patch BoundTo setter，不扫描所有 mutation 方法，不承诺任意 API 调用后内部仲裁立即变化。

本机额外的[预热审计宿主](<work/combo-input-audit/implementation/warm-host/Program.cs>)先调用原生 Clear/Reset/SaveLoad 再安装补丁；[预热结果](<work/combo-input-audit/implementation/regression-warmed.jsonl>)与标准冷启动回归分别保存。该宿主在审计 work 目录，不属于标准测试工程。

游戏这版 Harmony 在本机 CoreCLR 10 上抛“不支持 10.0.11”，在 CoreCLR 8 上安装补丁时遇到 ReflectionHelper 的 MethodAccessException；因此实际拦截用 Framework 宿主，不能将 CoreCLR 失败当作游戏 Mono 故障。

## 覆盖与边界

覆盖末键保持/松开/重复点按、三级父子及排列、等价与修饰键、禁用与监听资格矩阵、旧锁延续、下一输入 tick 识别解绑/换 owner、清空及恢复默认的同引用重挂、保存加载/同对象配置变化、空/隐藏绑定、provider 缺失、原生 ClearInputState 和 Reset 边界。

同帧晋升验证子动作 WasReleased 与父动作 WasPressed 同时为真且读取不消费；真实同 action 多 KeyBindingSource 的聚合也有用例，避免错误制造动作级释放。另有 float 注入模拟备用输入源，但不代表真实鼠标/手柄端到端验证。

使用实际 InControl UpdateWithValue/PrepareForUpdate 与边沿 getter；Commit 通过反射复制 nextState->thisState、清 pendingCommit，并保留原生 clearInputState 分支。该桥跳过 Unity 原生时钟、repeat/updateTick 等 Commit 行为。真实完整 PlayerAction.Update、监听录制器、设备输入、Mono 和其它 Mod 的游戏内 Harmony 链不在本工程验证范围。

监听在本 tick 前采样：如果开帧被阻断、但靠前动作提交后监听结束，后面的 A 与 AB 在这个完成帧可能同时沿原版路径按下；下一 tick A 才被压制并释放，AB 不补发 press。用例明确断言这一顺序差异，而不是宣称消除了它。

现有禁用行为不额外补边沿、不重武装、不自动业务取消；业务释放清理/组合按下顺序需在对应消费方及真实游戏上下文另行验证。
