# 可参考的开源项目

这些项目仅作为架构和交互研究材料，不直接复制代码、美术、名称或关卡。

## Unity / 时间管理与工位流程

- [VRChef](https://github.com/dyanikoglu/VRChef) — Unity 厨房模拟，包含配方步骤、设备交互和实时进度提示。采用 Apache-2.0，可重点参考“服务步骤数据”和“工位交互”拆分。
- [Open-GDR/awesome-unity-games](https://github.com/Open-GDR/awesome-unity-games) — 开源 Unity 游戏索引，其中列有 [Undercooked](https://github.com/TeamUndercooked/Undercooked) 等烹饪类项目，可用于比较场景组织、订单队列和多人协作的实现方式。
- [PopLifeSimulator](https://github.com/alankalles/poplifesimulator) — 时间推进、顾客生成、队列和结算数据模型的参考，适合借鉴“营业阶段 → 结算”的事件结构。

## 理论 / 队列模型

- [Barbershop Simulator 示例](https://gist.github.com/tejaskumar31/2f030b5ea6ed79217ade) — 非 Unity 的 Java 示例，展示到达时间、服务时间和等待队列的最小数据模型；仅参考概念，不建议直接使用实现。

## 对本 Demo 的采用结论

本 Demo 保持轻量，不引入上述项目的完整系统：

1. 从 VRChef 借鉴“需求步骤 + 工位进度”的数据分离。
2. 从 Undercooked 类项目借鉴“多个订单同时存在、有限工位造成取舍”的压力结构。
3. 从 PopLifeSimulator 借鉴“固定时长营业 → 统计结算”的流程。
4. 不加入员工、库存、装修、广告或复杂经济系统。

使用前应逐个核对仓库当前 LICENSE、第三方依赖和素材授权；参考仓库的许可证不自动覆盖其素材或依赖。
