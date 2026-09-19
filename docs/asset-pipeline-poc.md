# 结构化剪发工位资产管线 POC 决策记录

## 现状

当前可玩版本是 Unity 6000.5.8f1 / C# / uGUI。`HairSalonDemo.unity` 只负责启动，房间、家具、顾客和理发师由 `SalonDemo.cs` 在运行时创建。剪发工位的镜台、镜子、灯泡、椅子、顾客座位点和理发师工作点依赖 `BuildHaircutZone` 中的绝对坐标；等待位、固定路线和其他工位又各自保存坐标。角色移动最终追逐这些 Transform，遮挡主要依靠 3D 几何和固定 World Space Canvas sorting order，缺少一个同时描述家具占地、碰撞、朝向、锚点与视觉资源的对象。

这种方式会让同一工位的视觉、交互和移动语义彼此脱节：替换图片或模型后必须重新查找并手调座位点和工作点；旋转靠墙方向时容易漏改某个偏移；没有统一 footprint/collision 就无法自动发现穿模或路径冲突；服务代码也无法在开始前证明角色已对齐。

## 本轮最小改造

保留现有 Unity Demo 和服务模型，只结构化两张现有剪发椅：

- 用一个 CutStation Manifest 定义视觉资产、默认尺寸/比例、占地、碰撞、合法方向、全部交互锚点、sorting/depth 与调试样式。
- 两张工位引用同一个资产定义；后墙向与右墙向通过同一套局部坐标和 90° 旋转规则产生，不复制两套坐标。
- CutStation 运行时组件创建视觉、碰撞体和命名锚点，并把锚点交给现有顾客/理发师移动与服务流程。
- 当前视觉继续使用已有程序化 Low-Poly 灰盒；视觉工厂同时保留 Sprite 与 Prefab/模型资源入口，替换 AssetId/资源路径不触碰顾客服务逻辑。
- 开发构建或 `?cutStationDebug=1` / `--cut-station-debug` 显示 footprint、collision、锚点、方向和深度；正式非开发构建不显示。
- 配置校验与 EditMode 测试覆盖路径、锚点、方向、占地、重复 ID、几何冲突和服务对齐；运行验收覆盖进入、排队、到站、对齐、服务和离场闭环。

现有 Unity 已具备组件、数据配置、运行时对象、角色移动、碰撞体和自动测试，本轮没有全面迁移引擎的理由，也不创建 Cocos 隔离实验。

## 回滚

本次接入集中在 CutStation 独立目录和 `SalonDemo.BuildHaircutZone` 的单一调用点。回滚时恢复原 `BuildHaircutZone` 程序化搭建代码，并删除 CutStation 目录、对应 Resources 配置及专项测试即可；原场景、服务模型、角色资源和其他工位不需要回退。
