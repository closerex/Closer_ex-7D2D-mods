# 模块系统（Module system）原理 —— 复查用

**用途：** 供复查我对模块系统的理解，以及作为"补丁为什么这样写、方法为什么这样命名"的对照表。
**范围：** `0-KFCommonUtilityLib/Scripts/Utilities/Modular/` 与 `Scripts/Attributes/`、`Harmony/Init.cs`、`Harmony/ModularPatches.cs`。
**结论先行：** 模块系统的核心不是"怎么打 Harmony 补丁"，而是**把补丁的目标类型从"游戏里的共享类型"换成"运行时为某一件物品生成的派生类型"**。后面所有规则（静态性、类型解析、命名、注入访问器）都是这一条的推论。

---

## 1. 三个角色

| 角色 | 是什么 | 在哪 | 生命周期 |
| --- | --- | --- | --- |
| **模块** module | 带 `[TypeTarget(baseType)]` 的普通类，成员方法是补丁体 | 任意被加载的程序集 | 类型级，编译期固定 |
| **模块实例** | 模块类的实例，作为**字段**挂在生成的派生类型上 | `MakeContainerFor` `ModuleManipulator.cs:944` | 每个派生类型**每个实例**一份（ctor 里 newobj，`:605-622`、`:714-787`） |
| **数据实例** | `[TypeDataTarget(dataType)]` 声明的类，作为字段挂在**生成的 ItemActionData 子类**上 | `ItemActionModuleProcessor.InitModules` `:84-119` | 每个 action 数据实例一份；由重写的 `CreateModifierData` 构造 |

对外暴露：`IModuleContainerFor<T>.Instance`（模块实例字段的 getter，`MakeContainerFor` 生成的属性）。
`IModuleCollection.Modules` 提供 `object[]` 形式的全部模块实例（`ModuleManipulator.cs:960-971`）。

---

## 2. 装配流水线（XML → 运行时派生类型）

```
items.xml  里 <property name="ItemActionModules" value="A,B"/>
  → ModularPatches.cs:100        ModuleManagers.PatchType<ItemActionModuleProcessor>(originalType, typeof(ItemAction), null, "A,B", out result)
  → ModuleManagers.cs:175        模块数 0 → 直接用原类型
                                否则 typeName = ModuleUtils.CreateTypeName(baseType, moduleTypes)   // 由模块组合算 MD5
                                已有同名生成类型 → 直接复用（TryFindType / TryFindInCur，:248/:274）
                                否则 new ModuleManipulator(processor, targetType, baseType, parentType, out result, moduleTypes)
```

- 生成的是**派生类型**，覆写目标方法：`GetOrCreateOverride` 用 `DefineMethod(..., Virtual | HideBySig | Final) & ~NewSlot` + `DefineMethodOverride`（`ModuleManipulator.cs:557-574`）。
- 生成的程序集在 XML 加载期建立、加载结束时保存并加载：`ModuleManagers.InitNew`（`:129`）、`FinishAndLoad`（`:201`），产物落在 `<Mod>/AssemblyOutput/RuntimeAssembled<毫秒>.dll`。
- **同一组模块组合只生成一次类型**，名字由组合决定 —— 所以模块组合相同 = 同一个类型 = 同一份 IL。

---

## 3. 三类补丁成员：收集与目标解析

### 3.1 收集（`GatherPatchTargets<T>` `ModuleManipulator.cs:494`）

- 来源：模块类 + 它的 `[TypeTargetExtension]` 扩展类（`ModuleManagers.InitModuleExtensions` `:45`）。
- 过滤：带属性 `T`，且**静态性必须匹配**：`attr.IsStatic == m.IsStatic`（`:507-511`）。
  即 **Transpiler 必须 `static`；Prefix/Postfix 必须是实例方法**（`MethodTargetAttribute.cs:12-27`）。
- 间接写法：`[TargetMethods]` 标在静态方法上，由它返回带 `[HarmonyPatch]` 的目标方法（`GetHarmonyPatchesForTargetMethods` `:522`）。

### 3.2 Prefix / Postfix 的目标解析（`GetMethodOverrideTargets<T>` `:442-492`）

```
声明类型 d = 属性里的类型（可能为 null，因为 HarmonyPatch(nameof(X)) 不携带类型信息）
接受条件:  d == null  ||  d.IsAssignableFrom(targetType)          // :452
关键一步:  hmclone.declaringType = targetType                     // :457  ★
           → 不论属性写没写类型，方法名一律在"模块的 TypeTarget"上解析
根定义:    hmclone.GetBaseMethod()，要求 IsVirtual && !IsFinal     // :459-460 否则只记 Log.Warning 后跳过
就近优先:  同一个"方法标识"被多个补丁命中时，取继承链上离 targetType 最近的那个声明类型  // :466-478
```

⇒ 这解释了 `[HarmonyPatch(nameof(ItemAction.ExecuteAction))]` 为什么能用在 `[TypeTarget(typeof(ItemActionDynamic))]` 的模块上：**`ItemAction` 只是取名字用的，解析发生在 `ItemActionDynamic` 上**。

### 3.3 Transpiler 的目标解析（`:263-300`）

```
必须写类型：hp.info.declaringType == null → 直接跳过（:268 的 null 检查）
然后沿继承链遍历：
    nextType = targetType
    while (hm.declaringType.IsAssignableFrom(nextType)) {
        hm_next.declaringType = nextType;                      // :291
        找到该层的原始方法 → 为该层加一个 Harmony transpiler  // :304 / :317
        nextType = nextType.BaseType;
    }
```

⇒ 属性里的类型是**层级遍历的边界**（"从 TypeTarget 到哪个基类为止，每一层都要打"）。
⇒ 同一个模块的 transpiler 可能被施加在**多个层级**上，不是只施加一次。

### 3.4 命名约定（本次被纠正的地方）

格式：**`[Kind_]<HarmonyPatch 里显式写出的类型，若有>_<方法名>`**

| 情形 | 属性写法 | 名字 | 例子 |
| --- | --- | --- | --- |
| Prefix/Postfix 沿用 TypeTarget | `[HarmonyPatch(nameof(X.M))]`（无类型） | 无类型段 | `Prefix_ExecuteAction`、`Postfix_CanExecute`、`Postfix_ReadFrom` |
| Transpiler（**必须**写类型） | `[HarmonyPatch(typeof(T), nameof(M))]` | 带属性里的类型 `T` | `Transpiler_ItemActionDynamic_hitTarget`、`Transpiler_MinEventActionAddBuff_Execute` |

要点：括号里的类名**只反映"属性里写没写那个类型"**，与"方法真正声明在哪个最底层基类"无关 ——
`Transpiler_MinEventActionAddBuff_Execute` 里的 `MinEventActionAddBuff` 是属性写的类型，而 `Execute` 的声明类是 `MinEventActionBase`（`CVarReplacer.cs:94`）。
反过来，没有写类型的补丁方法**就该省略类名**（`CVarReplacer.cs:132` 的 `Postfix_CanExecute`）。

---

## 4. 补丁体怎么落地（`ApplyMethodPatches` `:628-712`）

生成的 override 方法体顺序：

```
loc0 = true                                  // "runOriginal"
<前缀 1..n>                                   // 返回值 bool 的会 &= loc0；其它返回值丢弃
if (!loc0) goto POSTFIX
call (TranspilerBuilder ?? OriginalMethod)(this, 原参数…)     // ★ 有 transpiler 时调用被改写后的方法
POSTFIX:
<后缀 1..n>
return loc1 (原返回值)
```

- **Postfix 先注册、Prefix 后注册**（`:400-422`，注释写明是为了让前缀能跳到正确指令），模块之间 Prefix 逆序（`:413`）。
- 每个方法最多 1 个 transpiler（`TranspilerBuilder`），多个模块都改同一方法时，**transpiler 会串成一条 Harmony 链**（`:304/:317` 的 `AddTranspilers`），`ModuleManipulator.cs:259` 注释："multiple transpilers on the same method is allowed"。

### 4.1 补丁方法的参数匹配（`MatchPatchArguments` `:803`、`MatchPatchSpecialParameters` `:876`）

| 参数名 | 含义 |
| --- | --- |
| （实例方法的 this） | **模块实例**（`:811-812` 先压入模块字段）；静态扩展方法则跳过第一个参数（`:814`） |
| `__instance` | 被覆写方法的所有者（`ldarg.0`） |
| `__result` | 原方法返回值；**仅当原方法有返回值**，否则抛错（`:891-905`） |
| `__runOriginal` | 局部 0；**只读**，`ref` 会抛（`:907-911`） |
| `__state` | 每模块一个局部：前缀必须 `out`、后缀必须非 `out`，且必须成对，未清空在 `:427-430` 抛 |
| `___字段名` | 目标类型上的（私有）字段，`ref`/值均可（`:816-832`） |
| `__0`/`__1`… 或**按名字** | 原方法第 n 个参数（`:836-871`） |
| `__customData` | 该模块的 `[TypeDataTarget]` 数据实例：`ItemActionModuleProcessor.MatchSpecialArgs` `:159-186` 把 ItemActionData 参数 `castclass` 成生成的数据类型后取字段 |

模块 ctor 的参数匹配同理（`MatchConstructorArguments` `:714-787`），支持 `___字段`、`__instance`、按名字。

---

## 5. 两个注入访问器 —— 为什么必须在调用点取

```csharp
ModuleExtensions.LoadModule<T>(this object obj)            // :91  (obj as IModuleContainerFor<T>)?.Instance
ItemActionModuleProcessor.LoadDataModule<T>(ItemActionData) // :58  (actionData as IModuleContainerFor<T>)?.Instance
```

两者都是**泛型**的，编译进 transpiler 的 IL 里只是一个 `call LoadX<某模块/某数据>`。真正把它们变成"这一件物品的实例"的是
`processor.ApplyPatchModifiers` 给补丁链追加的 `TranspilerHelper.LoadDataModuleHelper`（`ItemActionModuleProcessor.cs:23-52`）：

- 扫每条 `call LoadDataModule<T>`（`T` 已闭合），用 `T` 在 `arr_fldbd_data` 里定位字段；
- 把调用点**改写成生成数据类型的 `get_Instance`**，并在前面插 `castclass 生成数据类型`；
- 找不到对应字段 → `Log.Error("Accessing invalid ItemActionData module!")`。

⇒ **只有在调用点原地写这两个访问器，模块系统才能重写它**。写进普通 helper 里就永远不会被改写（null 或类型不匹配），
这就是"数据和模块实例必须在注入点取、helper 只负责使用交到手上的东西"的原因（`ActionModuleDynamicGraze.cs:115-116`）。

---

## 6. 为什么用 MethodTarget，而不是独立的 `[HarmonyPatch]` 类

| | 独立 `[HarmonyPatch]` 类 | 模块的 MethodTarget 成员 |
| --- | --- | --- |
| 收集者 | `Init.cs:86` 的 `HarmonyInstance.PatchAllUncategorized` | `ModuleManipulator` 在生成派生类型时 |
| 改写的对象 | **共享的游戏方法**（全游戏同一份 IL） | 只写进"携带该模块的物品"的**派生类型**（`ApplyMethodPatches` 只在该类型上生成 override） |
| 作用域 | 每一件物品 | 只有这件物品 |

⇒ 判据是**作用域**，不是方便程度。

---

## 7. 失败模式（写/查补丁时的检查清单）

| 症状 | 原因 | 行为 |
| --- | --- | --- |
| 方法完全没生效，无日志 | 静态性不匹配（Transpiler 写成了实例方法，或 Prefix 写成 static） | `:507-511` 静默过滤 |
| `Method not found or invalid` | 目标方法不存在 / 不是 virtual / 是 final | `Log.Warning` 后**静默跳过**（`:460-463`） |
| `__state variable count does not match` | 前缀声明了 `__state` 但没有对应后缀 | 抛异常（`:427-430`） |
| `Parameter "x" not found!` | 参数名既不是特殊名，也不匹配原方法参数名 | 抛异常（`:851`） |
| `Accessing invalid ItemActionData module!` | transpiler 里 `LoadDataModule<T>` 的 `T` 不是该模块声明的数据类 | `Log.Error`，调用点保持原样 |
| 生成类型复用导致"改代码没生效" | 类型名由模块组合决定，组合不变就复用旧生成类型 | 需要重加载/换组合（`CreateTypeName`） |

---

## 8. 对本次两处改动的对照结论

- `ActionModuleDynamicGraze` 的 `[TypeTarget(typeof(ItemActionDynamic))]` + `[TypeDataTarget(typeof(WindowDamageData))]`：
  这两个属性决定了"补丁施加在 ItemActionDynamic 的派生类型上"和"每个 action 数据带一份 WindowDamageData"。
- 两个 transpiler 属性里都**显式写了类型**（`typeof(ItemActionDynamic)` / `typeof(ItemActionDynamicMelee)`）——因为 transpiler 的解析必须靠这个类型做层级遍历（`:268`），
  所以它们的名字是 `Transpiler_ItemActionDynamic_hitTarget`、`Transpiler_ItemActionDynamicMelee_Raycast`：Kind 前缀 + 属性里显式写的类型 + 方法名。
- 同模块其余 6 个 Prefix/Postfix 的属性里都没写类型（沿用 TypeTarget），所以名字**不带类型段**，无需修改。
