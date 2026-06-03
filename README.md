# 倒水小游戏 Demo

## 项目简介

这是一个使用 Godot 4.6.2 Mono + C# 开发的 2D 休闲益智倒水小游戏 Demo。

第一版重点是验证核心玩法，而不是美术、音效、商业化内容。

## 当前版本状态

第一版可玩 Demo 已完成。

已完成：
- 720×1280 竖屏布局
- 顶部 4 个颜色袋子
- 6 个水瓶，两排排列
- 每瓶最多 4 层水
- 顶层真实颜色显示
- 未解锁层黑色问号显示
- 点击选中水瓶并上移
- 经典倒水规则
- 问号层解锁
- 新解锁同色层不会在同一次操作继续倒出
- 无效倒水时目标瓶摇晃并显示“不能倒入”
- 满瓶同色自动收集
- 袋子计数显示
- 胜利弹窗
- 主按钮和弹窗按钮都可重开

## 技术栈

- Godot 4.6.2 Mono
- C#
- 目标布局：手机竖屏 720×1280
- 第一版运行平台：Windows Demo

## 运行方式

1. 用 Godot 4.6.2 Mono 打开项目
2. 打开或运行 GameScene.tscn
3. 点击水瓶进行操作

## 项目结构

- scripts/model：游戏数据模型
- scripts/core：核心流程与规则系统
- scripts/view：水瓶与袋子显示
- GameScene.tscn：主游戏场景

## 当前实现说明

当前第一版仍使用固定测试关卡。

下一步会把固定关卡从 GameManager 拆到 LevelGenerator。

为了稳定解决袋子计数显示问题，当前 GameManager 直接缓存并刷新 4 个真实 CountLabel。

BagSlotView 保留，但第一版计数刷新不再完全依赖 BagSlotView.Refresh。

后续 UI 重构时可以再把该逻辑收敛回 BagSlotView。
