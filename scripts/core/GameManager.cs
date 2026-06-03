using Godot;
using System.Collections.Generic;
using WaterSortGame.Model;
using WaterSortGame.View;

namespace WaterSortGame.Core;

public sealed partial class GameManager : Node
{
    private const int BottleCount = 6;
    private const float Epsilon = 0.001f;

    [Signal]
    public delegate void TransferCommittedEventHandler(
        int sourceBottleId,
        int targetBottleId,
        int moved,
        int color,
        Vector2 streamStartGlobal,
        Vector2 streamEndGlobal,
        bool isGroundPour);

    [Signal]
    public delegate void PouringStateChangedEventHandler(string state, string reason);

    [Signal]
    public delegate void LevelCompletedEventHandler();

    [Export]
    public bool IsManagedByMainFlow { get; set; }

    private readonly GameState _state = new();
    private readonly List<BottleView> _bottleViews = new();
    private readonly Dictionary<WaterColor, BagSlotView> _bagSlotViewsByColor = new();
    private PourSystem _pourSystem;
    private BagSystem _bagSystem;
    private LevelGenerator _levelGenerator;
    private UIManager _uiManager;
    private int? _selectedBottleId;
    private bool _isResolving;

    public override void _Ready()
    {
        _pourSystem = GetNode<PourSystem>("../PourSystem");
        _bagSystem = GetNode<BagSystem>("../BagSystem");
        _levelGenerator = GetNode<LevelGenerator>("../LevelGenerator");
        _uiManager = GetNode<UIManager>("../UIManager");
        _uiManager.RestartRequested += RestartGame;

        CacheBottleViews();
        CacheBagSlotViews();
        _levelGenerator.CreateInitialState(_state);
        _bagSystem.CollectCompletedBottles(_state);
        RefreshAllViews();
    }

    private void CacheBottleViews()
    {
        _bottleViews.Clear();

        Node currentScene = GetNode<Node>("../..");
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

        Node currentScene = GetNode<Node>("../..");

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

    private void RefreshAllBottleViews()
    {
        for (int i = 0; i < BottleCount; i++)
        {
            _bottleViews[i].Refresh(_state.Bottles[i]);
        }
    }

    private void RefreshAllBagSlotViews()
    {
        foreach (KeyValuePair<WaterColor, BagSlotView> pair in _bagSlotViewsByColor)
        {
            pair.Value.Refresh(_state.Bags[pair.Key]);
        }
    }

    private void RefreshAllViews()
    {
        RefreshAllBottleViews();
        RefreshAllBagSlotViews();
        RefreshSelectionViews();
    }

    private void OnBottleClicked(int bottleId)
    {
        if (_state.IsGameOver || _isResolving)
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
            EmitSignal(SignalName.PouringStateChanged, "Idle", "Cancelled");
            return;
        }

        BottleData source = _state.Bottles[_selectedBottleId.Value];
        BottleData target = _state.Bottles[bottleId];
        PourResult result = _pourSystem.TryCreatePourPlan(source, target);

        if (result.Success)
        {
            _ = ResolveSuccessfulPourAsync(result.Plan!, source, target);
            return;
        }

        _bottleViews[bottleId].PlayInvalidFeedback();
        _uiManager.ShowTip("不能倒入");
        _selectedBottleId = null;
        RefreshSelectionViews();
        EmitSignal(SignalName.PouringStateChanged, "Blocked", result.FailReason);
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
        if (_isResolving)
        {
            EmitSignal(SignalName.PouringStateChanged, "Blocked", "Restart ignored while resolving.");
            return;
        }

        _selectedBottleId = null;
        _state.IsGameOver = false;
        EmitSignal(SignalName.PouringStateChanged, "Idle", "Restart");
        _uiManager.HideVictory();

        _levelGenerator.CreateInitialState(_state);
        _bagSystem.CollectCompletedBottles(_state);
        RefreshAllViews();
    }

    private async System.Threading.Tasks.Task ResolveSuccessfulPourAsync(PourPlan plan, BottleData source, BottleData target)
    {
        _isResolving = true;
        _selectedBottleId = null;
        RefreshSelectionViews();

        BottleView sourceView = _bottleViews[source.Id];
        BottleView targetView = _bottleViews[target.Id];

        try
        {
            EmitSignal(SignalName.PouringStateChanged, "PouringToTarget", string.Empty);
            await sourceView.PlayPourAnimationTo(
                targetView,
                plan.Color,
                plan.Amount,
                (streamStart, streamEnd) =>
                {
                    _pourSystem.ExecutePour(plan, _state);
                    if (plan.Amount > Epsilon)
                    {
                        EmitSignal(
                            SignalName.TransferCommitted,
                            plan.SourceBottleId,
                            plan.TargetBottleId,
                            plan.Amount,
                            (int)plan.Color,
                            streamStart,
                            streamEnd,
                            false);
                    }
                },
                () => EmitSignal(SignalName.PouringStateChanged, "StreamComplete", string.Empty));

            _bagSystem.CollectCompletedBottles(_state);
            RefreshAllViews();

            if (IsWin())
            {
                _state.IsGameOver = true;
                EmitSignal(SignalName.LevelCompleted);

                if (IsManagedByMainFlow)
                {
                    _uiManager.HideVictory();
                }
                else
                {
                    _uiManager.ShowVictory();
                }
            }
        }
        finally
        {
            _isResolving = false;
            EmitSignal(SignalName.PouringStateChanged, "Idle", string.Empty);
        }
    }
}
