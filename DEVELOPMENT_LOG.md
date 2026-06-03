# 开发记录

## 第一版 Demo 完成记录

第一版 Demo 已完成。

## 已完成阶段

- [x] 搭建项目与竖屏场景
- [x] 建立核心数据模型
- [x] 实现水瓶显示
- [x] 实现点击选中流程
- [x] 实现倒水规则
- [x] 实现问号层解锁
- [x] 实现无效操作反馈
- [x] 实现自动收集袋子
- [x] 修复袋子计数显示问题
- [x] 实现胜利与重开
- [x] 真实窗口验证第一版主流程

## 关键问题记录

### 1. BottleData 职责边界

问题：

一开始 BottleData 容易混入 CanPourTo / PourTo 等倒水逻辑。

解决：

BottleData 保持纯数据，倒水判断和执行统一放在 PourSystem。

### 2. 问号层连锁倒出

问题：

如果按真实颜色直接计算顶部连续同色，会把未揭示的问号层也倒出去。

解决：

PourSystem 只计算 IsRevealed == true 的顶部连续同色层。

倒水后只解锁源瓶新的顶部层，不在同一次操作继续倒出。

### 3. 袋子计数不刷新

问题：

水瓶已经被自动收集隐藏，但袋子数字仍显示 0。

原因：

BagSlot 刷新目标不稳定，运行时真实显示 Label 与刷新入口不够明确。

解决：

统一袋子数字节点名为 CountLabel。

GameManager 显式缓存并刷新 4 个真实 CountLabel。

保留 BagSlotView，但不把它作为第一版唯一计数刷新入口。

### 4. BottleView 初始化顺序

问题：

GameManager 早于 BottleView._Ready() 调用 SetSelected(false) 时，可能导致瓶子位置异常。

解决：

BottleView 增加初始化保护，确保 base position 正确后再进行选中位置计算。

## 当前稳定提交

6dc1e90 Add first playable demo

## 验证情况

- dotnet build 通过，0 警告，0 错误
- Godot 真实窗口验证通过
- 胜利后点击瓶子无反应
- 主按钮和弹窗按钮都能重开
