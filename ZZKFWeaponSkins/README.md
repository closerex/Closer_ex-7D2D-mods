# KFWeaponSkins：当前 KF 武器皮肤配置

## 内容与安装

本配置面向当前已确认的 30 个基础物品及 2 个同网格派生物品，共 25 类武器、482 个皮肤、631 条精确物品覆写。没有为不同武器共用一个安装 tag，也没有用通配符覆盖所有物品。动态皮肤同样包含基础材质替换，额外游戏参数暂不接线。

使用原版染色 cosmetic 槽：modifier_tags=dye,kfWeaponSkin。沿用现有 KFMaterialReplacer 与 SkinRendererList，不添加新换肤框架。名称为 KFWeaponSkins，目录前缀 ZZ 用于本安装根内晚于既有 ZCLS/Medic 定义；它不是跨所有 Mod 根的绝对排序保证。

资源通过 [Resources 符号链接](<E:/SteamLibrary/steamapps/common/7 Days To Die/ModsRepo/ZZKFWeaponSkins/Resources/>) 指向 [已构建皮肤资源](<E:/SteamLibrary/steamapps/common/7 Days To Die/ModsRequested/KFWeaponSkins/Resources/>)。链接与包文件均被 Git 忽略，不把大资源加入仓库。已将此 Mod 挂载到实际 Mods，并使用配套更新的既有武器包；测试部署计划在 [deployment-plan.json](<J:/KF2MaterialBridge/skin-xml/game-acceptance/deployment-plan.json>)，已按用户明确要求直接更新 29 个配套资源包、启用 Mod，未创建资源备份；见 [部署回执](<J:/KF2MaterialBridge/skin-xml/game-acceptance/deployment-result.json>)。

## 条件 include 与服务器 XML

[items.xml](<E:/SteamLibrary/steamapps/common/7 Days To Die/ModsRepo/ZZKFWeaponSkins/Config/items.xml>) 和 [item_modifiers.xml](<E:/SteamLibrary/steamapps/common/7 Days To Die/ModsRepo/ZZKFWeaponSkins/Config/item_modifiers.xml>) 各含 25 个根级 conditional → if cond → include。每类仅按 path_exists('@modfolder(KFWeaponSkins):Resources/对应目录') 选择其子 XML；没有把条件放入 append 的数据层，也未标记为 client evaluator。

当前原版代码路径先执行 include/patch 和 Host 条件，再 CachePatchedXml；服务器下发非空压缩 XML 时，客户端解压后只执行剩余 Client 条件与加载，不重跑这些 include/path_exists。items 与 item_modifiers 都注册为 sendToClients。若走 LoadClientFile/空数据本地加载分支，仍会读取本地配置，不能宣称所有网络分支都不检查本地。这里已完成静态原版代码核对，尚未进行双端联机实测。

服务器判断文件夹存在不代表客户端自动获得 Bundle。客户端仍需要匹配的 Mod 注册信息、本地资源与依赖包。本配置依赖现有 KF 公共库中的 path_exists 条件扩展与材质替换补丁，不附带新 DLL。

## 原版皮肤图标与血迹更新

482 张 KF2 原版 PNG 已接入各皮肤 CustomIcon；游戏图集中逐个检查缺失 0，并查看了 [背包截图](<J:/KF2MaterialBridge/skin-issues/game-skin-icons.jpg>)。PNG 按原仓库策略不进入 Git，部署时需保留 UIAtlases/ItemIconAtlas 目录。

迁移血迹参数已统一为 `_BloodContrast`，保留材质原值与门控；成品及现有粉碎机的原生装备写入已验证。既有近战写入器继续使用，未新增 DLL。详见 [本轮报告](<J:/KF2MaterialBridge/skin-issues/FINAL-REPORT.md>)。

## 原文名称、描述与 CSV

[Localization.csv](<E:/SteamLibrary/steamapps/common/7 Days To Die/ModsRepo/ZZKFWeaponSkins/Config/Localization.csv>) 包含全部 482 个皮肤的英文/简中名称及各自 DescriptionKey。名称来自 KF2 原始 ItemDefinitions 本地化，按明确武器 token、完整套装/变种和品相映射，不按近似名称或多数票猜测。

476 条描述来自对应武器/品相的原版皮肤说明；另外 6 条没有对应皮肤品相说明，使用同武器 KFGameContent 原始 ItemDescription，未套用别的品相。逐条原文件、原行与原文在 [官方文本映射](<J:/KF2MaterialBridge/skin-xml/official-localization-map.json>)。

CSV 使用标准 writer，所有字段加引号、内部双引号成对转义、换行写为游戏支持的反斜杠 n。除了 Python 回读，还实际执行当前安装的 NGUI.dll 中 ByteReader.ReadCSV；965 行（含表头）、5790 个单元格逐项与原文匹配，见 [原生 CSV reader 回执](<J:/KF2MaterialBridge/skin-xml/native-csv-reader-verification.json>)。该测试在独立 .NET 进程运行，不冒充已进入游戏。

## 验证与已知边界

[静态闭环检查](<J:/KF2MaterialBridge/skin-xml/mod-validation.json>) 验证 50 个条件 include、482 个 modifier、631 条精确覆写、专用安装 tag 和所有翻译键。[成品回读](<J:/KF2MaterialBridge/skin-xml/item-export-readback.json>) 验证最新 189 包依赖、46 个新补全 Replacer、637 个 XML URI 及 C4 手持槽位。原有 648 个数据对象/37 个布局受到保护；新增 C4 手持组件及 46 个补全数据已单独验证。

需要安装的基础数据/脚本前提：正常 CloneModel 重建路径中 class replacer → item override → tint。null 槽保留 CURRENT 材质；没有实现同一旧模型实例连续切换皮肤时清除前一个皮肤残留的自定义框架。原 22 个资源缺失问题、M14 特定 scope 缺失没有被 null 隐藏。

[动态参数对应说明](<E:/SteamLibrary/steamapps/common/7 Days To Die/ModsRepo/ZZKFWeaponSkins/Reports/DYNAMIC-PARAMETERS.md>) 记录 AmmoCount、ShotsHit、血迹与未证实驱动项。弹药/命中计数等未新增驱动；血迹沿用既有近战事件，本轮已统一 `_BloodContrast` 并验证原生装备事件写入。SpecGlow/Laser 的 bounds 绑定器也未部署宿主 DLL。法线近似与 D3D11 以外未验收边界沿用 [材质迁移说明](<J:/KF2MaterialBridge/full-migration/MIGRATION-REPORT.md>)。

隔离单人档已加载全部 482 个皮肤定义，9mm、G18 与 C4 起爆器的原生 CosmeticMods → CloneModel → SkinRendererList 路径已实测。C4 仅给起爆器提供安装 tag/覆写，手中的原始 C4 网格随同一 Replacer 换肤；不改变可堆叠投掷物上限。完整进程重启后复核通过：964 个名称/描述键自动加载，三个样本的 CosmeticMods 与材质自动恢复，C4 投掷物仍无独立换肤 tag、MaxCount 仍为 25。已正常保存退出，正式存档未改动。旧成品回读的 637 URI 包含已停用的 6 个投掷物入口，资源保留但新 XML 不再引用。

[实际游戏验收报告与抽样截图](<J:/KF2MaterialBridge/skin-xml/game-acceptance/ACCEPTANCE.md>)。退出阶段另记录 Xbox Live 与 Rig 等清理异常；不是全程零错误，详见报告。单人三样本通过，不等于全部皮肤逐项或双端联机实测。
