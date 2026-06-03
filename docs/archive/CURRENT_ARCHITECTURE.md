# 当前架构说明

## Model

- WaterColor
- WaterLayer
- BottleData
- BagData
- GameState
- PourPlan
- PourResult

Model 只保存状态，不写倒水规则。

## Core

- GameManager：流程调度、点击处理、刷新视图、胜利、重开
- PourSystem：倒水判断、倒水执行、问号解锁
- BagSystem：满瓶同色判断、自动收集
- UIManager：提示、胜利弹窗、重开事件

## View

- BottleView：水瓶显示、点击事件、选中上移、无效摇晃、收集隐藏
- BagSlotView：袋子显示辅助。第一版计数由 GameManager 直接刷新 CountLabel。

## 当前实现取舍

第一版为了稳定，GameManager 直接缓存并刷新：

- BagSlot_0/CountLabel
- BagSlot_1/CountLabel
- BagSlot_2/CountLabel
- BagSlot_3/CountLabel

后续可以重构为：

GameManager -> BagSlotView.Refresh(BagData)

但现在不要急着改，因为当前版本已经真实窗口验证通过。
