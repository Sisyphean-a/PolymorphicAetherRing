# 项目注意力

- 项目记忆与说明正文使用中文；代码标识、配置键和外部 API 名称保持原样。
- 开始任务时先从 `architecture/INDEX.md` 和 `requirements/CONTEXT.md` 选范围，只加载目标包及其映射的领域上下文；`history/` 不在默认工作集。
- 仓库只有一个实现包：`PolymorphicAetherRing/`。不要修改或检索生成目录 `bin/`、`obj/`。

## 验证

- 静态验证命令是 `dotnet build PolymorphicAetherRing.sln`。
- 构建会通过 Stardew ModBuildConfig 生成发布压缩包，并可能把模组复制到本机 Stardew Valley 的 `Mods` 目录；运行前应知晓这个副作用。
- 当前没有自动化测试项目。编译成功不能替代游戏内的菜单交互、触控和战斗烟测。
