using Godot;
using System.Collections.Generic;
using WaterSortGame.Model;
using WaterSortGame.View;

namespace WaterSortGame.Core;

public sealed partial class GameManager : Node
{
    private const int BottleCount = 6;

    private readonly GameState _state = new();
    private readonly List<BottleView> _bottleViews = new();
    private readonly Dictionary<WaterColor, BagSlotView> _bagSlotViewsByColor = new();
    private PourSystem _pourSystem;
    private BagSystem _bagSystem;
    private UIManager _uiManager;
    private Label _redCountLabel;
    private Label _blueCountLabel;
    private Label _yellowCountLabel;
    private Label _greenCountLabel;
    private int? _selectedBottleId;

    public override void _Ready()
    {
        _pourSystem = GetNode<PourSystem>("../PourSystem");
        _bagSystem = GetNode<BagSystem>("../BagSystem");
        _uiManager = GetNode<UIManager>("../UIManager");
        _uiManager.RestartRequested += RestartGame;

        CacheBottleViews();
        CacheBagSlotViews();
        CacheBagCountLabels();
        CreateTestState();
        _bagSystem.CollectCompletedBottles(_state);
        RefreshAllViews();
    }

    private void CacheBottleViews()
    {
        _bottleViews.Clear();

        Node currentScene = GetTree().CurrentScene;
        for (int i = 0; i < BottleCount; i++)
        {
            BottleView view = currentScene.GetNode<BottleView>($"WorldRoot/BottleRoot/Bottle_{i}");
            view.Bind(i);
            view.Clicked += OnBottleClicked;
            _bottleViews.Add(view);
        }
    }

    private void CacheBagSlotViews()
    {
        _bagSlotViewsByColor.Clear();

        Node currentScene = GetTree().CurrentScene;

        _bagSlotViewsByColor[WaterColor.Red] =
            currentScene.GetNode<BagSlotView>("WorldRoot/BagRoot/BagSlot_0");
        _bagSlotViewsByColor[WaterColor.Blue] =
            currentScene.GetNode<BagSlotView>("WorldRoot/BagRoot/BagSlot_1");
        _bagSlotViewsByColor[WaterColor.Yellow] =
            currentScene.GetNode<BagSlotView>("WorldRoot/BagRoot/BagSlot_2");
        _bagSlotViewsByColor[WaterColor.Green] =
            currentScene.GetNode<BagSlotView>("WorldRoot/BagRoot/BagSlot_3");

        _bagSlotViewsByColor[WaterColor.Red].Bind(WaterColor.Red);
        _bagSlotViewsByColor[WaterColor.Blue].Bind(WaterColor.Blue);
        _bagSlotViewsByColor[WaterColor.Yellow].Bind(WaterColor.Yellow);
        _bagSlotViewsByColor[WaterColor.Green].Bind(WaterColor.Green);
    }

    private void CacheBagCountLabels()
    {
        Node currentScene = GetTree().CurrentScene;

        _redCountLabel =
            currentScene.GetNode<Label>("WorldRoot/BagRoot/BagSlot_0/CountLabel");
        _blueCountLabel =
            currentScene.GetNode<Label>("WorldRoot/BagRoot/BagSlot_1/CountLabel");
        _yellowCountLabel =
            currentScene.GetNode<Label>("WorldRoot/BagRoot/BagSlot_2/CountLabel");
        _greenCountLabel =
            currentScene.GetNode<Label>("WorldRoot/BagRoot/BagSlot_3/CountLabel");
    }

    private void CreateTestState()
    {
        _state.Bottles.Clear();
        _state.Bags.Clear();

        _state.Bags[WaterColor.Red] = new BagData(WaterColor.Red);
        _state.Bags[WaterColor.Blue] = new BagData(WaterColor.Blue);
        _state.Bags[WaterColor.Yellow] = new BagData(WaterColor.Yellow);
        _state.Bags[WaterColor.Green] = new BagData(WaterColor.Green);

        _state.Bottles.Add(CreateBottle(0,
            (WaterColor.Green, false),
            (WaterColor.Blue, false),
            (WaterColor.Yellow, false),
            (WaterColor.Red, true)));

        _state.Bottles.Add(CreateBottle(1,
            (WaterColor.Red, false),
            (WaterColor.Green, false),
            (WaterColor.Yellow, false),
            (WaterColor.Blue, true)));

        _state.Bottles.Add(CreateBottle(2,
            (WaterColor.Blue, false),
            (WaterColor.Red, false),
            (WaterColor.Green, false),
            (WaterColor.Yellow, true)));

        _state.Bottles.Add(CreateBottle(3,
            (WaterColor.Yellow, false),
            (WaterColor.Blue, false),
            (WaterColor.Red, false),
            (WaterColor.Green, true)));

        _state.Bottles.Add(new BottleData { Id = 4 });
        _state.Bottles.Add(new BottleData { Id = 5 });

        ValidateTestState();
    }

    private void RefreshAllBottleViews()
    {
        for (int i = 0; i < BottleCount; i++)
        {
            _bottleViews[i].Refresh(_state.Bottles[i]);
        }
    }

    private void RefreshAllBagSlotViews()
    {
        _redCountLabel.Text = _state.Bags[WaterColor.Red].CollectedCount.ToString();
        _blueCountLabel.Text = _state.Bags[WaterColor.Blue].CollectedCount.ToString();
        _yellowCountLabel.Text = _state.Bags[WaterColor.Yellow].CollectedCount.ToString();
        _greenCountLabel.Text = _state.Bags[WaterColor.Green].CollectedCount.ToString();
    }

    private void RefreshAllViews()
    {
        RefreshAllBottleViews();
        RefreshAllBagSlotViews();
        RefreshSelectionViews();
    }

    private void OnBottleClicked(int bottleId)
    {
        if (_state.IsGameOver)
        {
            return;
        }

        BottleData bottle = _state.Bottles[bottleId];

        if (_selectedBottleId == null)
        {
            _selectedBottleId = CanSelectAsSource(bottle) ? bottleId : null;
            RefreshSelectionViews();
            return;
        }

        if (_selectedBottleId == bottleId)
        {
            _selectedBottleId = null;
            RefreshSelectionViews();
            return;
        }

        BottleData source = _state.Bottles[_selectedBottleId.Value];
        BottleData target = _state.Bottles[bottleId];
        PourResult result = _pourSystem.TryCreatePourPlan(source, target);

        if (result.Success)
        {
            _pourSystem.ExecutePour(result.Plan!, _state);
            _bagSystem.CollectCompletedBottles(_state);
            _selectedBottleId = null;
            RefreshAllViews();

            if (IsWin())
            {
                _state.IsGameOver = true;
                _uiManager.ShowVictory();
            }

            return;
        }

        _bottleViews[bottleId].PlayInvalidFeedback();
        _uiManager.ShowTip("不能倒入");
        _selectedBottleId = null;
        RefreshSelectionViews();
    }

    private void RefreshSelectionViews()
    {
        for (int i = 0; i < BottleCount; i++)
        {
            _bottleViews[i].SetSelected(_selectedBottleId == i);
        }
    }

    private static bool CanSelectAsSource(BottleData bottle)
    {
        return !bottle.IsEmpty && !bottle.IsCollected;
    }

    private void ValidateTestState()
    {
        Dictionary<WaterColor, int> layerCounts = new()
        {
            [WaterColor.Red] = 0,
            [WaterColor.Blue] = 0,
            [WaterColor.Yellow] = 0,
            [WaterColor.Green] = 0
        };

        foreach (BottleData bottle in _state.Bottles)
        {
            foreach (WaterLayer layer in bottle.Layers)
            {
                layerCounts[layer.Color]++;
            }

            if (!bottle.IsEmpty && !bottle.Layers[^1].IsRevealed)
            {
                GD.PushWarning($"Bottle {bottle.Id} top layer should be revealed in the initial test state.");
            }
        }

        foreach (KeyValuePair<WaterColor, int> pair in layerCounts)
        {
            if (pair.Value != 4)
            {
                GD.PushWarning($"Initial test state has {pair.Value} {pair.Key} layers; expected 4.");
            }
        }
    }

    private bool IsWin()
    {
        int totalCollected = 0;

        foreach (BagData bag in _state.Bags.Values)
        {
            totalCollected += bag.CollectedCount;
        }

        return totalCollected >= 4;
    }

    private void RestartGame()
    {
        _selectedBottleId = null;
        _state.IsGameOver = false;
        _uiManager.HideVictory();

        CreateTestState();
        _bagSystem.CollectCompletedBottles(_state);
        RefreshAllViews();
    }

    private static BottleData CreateBottle(int id, params (WaterColor Color, bool IsRevealed)[] layers)
    {
        BottleData bottle = new() { Id = id };
        foreach ((WaterColor color, bool isRevealed) in layers)
        {
            bottle.Layers.Add(new WaterLayer(color, isRevealed));
        }

        return bottle;
    }
}
