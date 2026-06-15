#nullable enable

using System.Collections.Generic;

namespace WaterSortGame.Model;

public static class LocalizedTextTable
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Entries =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["common.back"] = Text("返回", "Back"),
            ["common.close"] = Text("关闭", "Close"),
            ["common.confirm"] = Text("确认", "Confirm"),
            ["common.cancel"] = Text("取消", "Cancel"),
            ["common.seed_count"] = Text("种子 x{0}", "Seed x{0}"),
            ["common.potion_count"] = Text("药剂 x{0}", "Potion x{0}"),

            ["home.start_game"] = Text("调试药剂", "Start Game"),
            ["home.planting"] = Text("种植/铲除", "Plant / Shovel"),
            ["home.planting_page"] = Text("种植页面", "Planting"),
            ["home.warehouse"] = Text("仓库", "Warehouse"),
            ["home.settings"] = Text("设置", "Settings"),
            ["home.reward_stored"] = Text("新的种子和药剂已存入仓库", "New seeds and potions were stored in the warehouse"),
            ["home.select_planting_slot"] = Text("请选择种植位置", "Choose a planting position"),
            ["home.plant_success"] = Text("种植成功", "Planting successful"),
            ["home.shovel_success"] = Text("铲花成功", "Flowers removed"),
            ["home.select_available_slot"] = Text("请选择可操作花位种植或追加", "Choose an available position to plant or add a flower"),
            ["home.no_append_slot"] = Text("没有可追加的位置，请选择其他花", "No position can accept this flower; choose another flower"),
            ["home.no_plantable_flower"] = Text("没有可种植的花", "No flowers are available to plant"),
            ["home.no_action"] = Text("没有可操作内容", "No actions are available"),
            ["home.slot_actions"] = Text("花位 {0} 操作", "Position {0} Actions"),
            ["home.plantable_section"] = Text("可种植", "Available to Plant"),
            ["home.shovel_section"] = Text("铲花", "Remove Flowers"),
            ["home.shovel_all"] = Text("铲除该花位全部花", "Remove all flowers from this position"),
            ["home.shovel_flower"] = Text("铲除 {0} / {1}", "Remove {0} / {1}"),
            ["home.plant_choice"] = Text("{0} / {1}\n种子 x{2}  药剂 x{3}", "{0} / {1}\nSeed x{2}  Potion x{3}"),

            ["flower_select.title"] = Text("选择一种花", "Choose a Flower"),
            ["flower_select.hint"] = Text("选择 1 种花，完成药剂调试后回花园种植", "Choose a flower, finish the potion puzzle, then return to the garden"),
            ["flower_select.coming_soon"] = Text("待开放", "Coming Soon"),
            ["flower_select.full"] = Text("已种满", "Garden Full"),
            ["flower_select.coming_soon_tip"] = Text("该花将在后续版本开放", "This flower will be available in a future version"),
            ["flower_select.full_tip"] = Text("该花已种满，请选择其他花", "This flower is fully planted; choose another flower"),

            ["level_select.title"] = Text("选择关卡", "Select Level"),
            ["level_select.flower_title"] = Text("{0} 关卡", "{0} Levels"),
            ["level_select.level_title"] = Text("{0} 第 {1} 关", "{0} Level {1}"),
            ["level_select.level_number"] = Text("第{0}关", "Level {0}"),
            ["level_select.hint"] = Text("选择当前可玩关卡", "Choose the currently playable level"),
            ["level_select.completed"] = Text("已完成", "Completed"),
            ["level_select.playable"] = Text("可进入", "Play"),
            ["level_select.locked"] = Text("未解锁", "Locked"),
            ["level_select.completed_tip"] = Text("该关卡已完成", "This level is already completed"),
            ["level_select.locked_tip"] = Text("该关卡尚未解锁", "This level is not unlocked yet"),
            ["level_select.unavailable_tip"] = Text("该关卡暂不可进入", "This level is currently unavailable"),

            ["warehouse.title"] = Text("仓库", "Warehouse"),
            ["warehouse.seed"] = Text("种子", "Seed"),
            ["warehouse.potion"] = Text("药剂", "Potion"),

            ["settings.title"] = Text("设置", "Settings"),
            ["settings.music_volume"] = Text("音乐音量", "Music Volume"),
            ["settings.sfx_volume"] = Text("音效音量", "Sound Effects Volume"),
            ["settings.language"] = Text("语言", "Language"),
            ["settings.language_zh"] = Text("中文", "中文"),
            ["settings.language_en"] = Text("English", "English"),
            ["settings.reset_progress"] = Text("只重置进度", "Reset Progress Only"),
            ["settings.reset_all"] = Text("重置全部设置", "Reset All Settings"),
            ["settings.reset_progress_title"] = Text("确认重置进度", "Confirm Progress Reset"),
            ["settings.reset_progress_prompt"] = Text("将清空关卡进度、仓库库存和主页花位，但保留音乐、音效和语言设置。", "This clears level progress, warehouse inventory, and garden positions while keeping audio and language settings."),
            ["settings.reset_all_title"] = Text("确认重置全部设置", "Confirm Full Reset"),
            ["settings.reset_all_prompt"] = Text("将清空进度、仓库库存和主页花位，并恢复默认音乐、音效和语言。", "This clears progress, inventory, and garden positions and restores default audio and language settings."),
            ["settings.progress_reset"] = Text("进度已重置", "Progress reset"),
            ["settings.all_reset"] = Text("已重置全部设置", "All settings reset"),

            ["game.cannot_pour"] = Text("不能倒入", "Cannot pour here"),
            ["game.restart"] = Text("重开", "Restart"),
            ["game.victory"] = Text("通关成功", "Level Complete"),
            ["reward.title"] = Text("获得奖励", "Rewards"),
            ["reward.plant"] = Text("种植", "Plant"),
            ["reward.auto_continue"] = Text("3 秒后自动继续", "Continuing in 3 seconds"),

            ["tutorial.home_intro"] = Text("调试药剂可以获得种子和药剂。", "Complete potion puzzles to earn seeds and potions."),
            ["tutorial.reward_to_warehouse"] = Text("通关奖励已进入仓库。", "Your level rewards are now in the warehouse."),
            ["tutorial.warehouse_intro"] = Text("仓库可以查看种子和药剂数量。", "The warehouse shows how many seeds and potions you have."),
            ["tutorial.planting_intro"] = Text("先点数字圆圈选择位置，再选择可种花。", "Tap a numbered circle to choose a position, then select a flower to plant."),
            ["tutorial.shovel_intro"] = Text("这里可以铲除全部或单种花，铲花会返还库存。", "Remove all flowers or one type here. Removed flowers return to inventory."),
            ["tutorial.settings_intro"] = Text("这里可以调整音量、语言和重置进度。", "Adjust volume, language, and progress reset options here."),

            ["flower.pink_rose.name"] = Text("粉玫瑰", "Pink Rose"),
            ["flower.yellow_rose.name"] = Text("黄玫瑰", "Yellow Rose"),
            ["flower.lavender.name"] = Text("薰衣草", "Lavender"),
            ["flower.flower_04.name"] = Text("待定花 04", "Flower 04"),
            ["flower.flower_05.name"] = Text("待定花 05", "Flower 05"),
            ["flower.flower_06.name"] = Text("待定花 06", "Flower 06")
        };

    private static IReadOnlyDictionary<string, string> Text(string zh, string en)
    {
        return new Dictionary<string, string>
        {
            ["zh"] = zh,
            ["en"] = en
        };
    }
}
