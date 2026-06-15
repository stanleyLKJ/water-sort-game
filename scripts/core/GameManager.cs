#nullable enable

using Godot;
using System.Collections.Generic;
using WaterSortGame.Model;
using WaterSortGame.View;

namespace WaterSortGame.Core;

public sealed partial class GameManager : Node
{
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

    [Signal]
    public delegate void ExitRequestedEventHandler();

    [Export]
    public bool IsManagedByMainFlow { get; set; }

    [Export]
    public string SelectedFlowerId { get; set; } = string.Empty;

    [Export(PropertyHint.Range, "1,7,1")]
    public int SelectedLevelNumber { get; set; } = 1;

    private readonly GameState _state = new();
    private readonly List<BottleView> _bottleViews = new();
    private PourSystem _pourSystem = null!;
    private BagSystem _bagSystem = null!;
    private LevelGenerator _levelGenerator = null!;
    private UIManager _uiManager = null!;
    private CauldronView _cauldronView = null!;
    private int? _selectedBottleId;
    private bool _isResolving;
    private int _targetColorCount = 4;

    public override void _Ready()
    {
        GD.Print(
            $"CAULDRON_DIAG GameManager._Ready received " +
            $"flowerId={SelectedFlowerId} levelNumber={SelectedLevelNumber} managed={IsManagedByMainFlow}");
        _pourSystem = GetNode<PourSystem>("../PourSystem");
        _bagSystem = GetNode<BagSystem>("../BagSystem");
        _levelGenerator = GetNode<LevelGenerator>("../LevelGenerator");
        _uiManager = GetNode<UIManager>("../UIManager");
        _uiManager.RestartRequested += RestartGame;
        _uiManager.ExitRequested += RequestExit;
        _uiManager.SetExitAvailable(IsManagedByMainFlow);

        CreateLevelState();
        PrintLevelEntryDiagnostics();
        CacheBottleViews(_state.Bottles.Count);
        CacheCauldronView();
        _targetColorCount = GetRequiredColorCount();
        GD.Print(
            $"CAULDRON_DIAG GameManager.SetTargetColorCount initial " +
            $"RequiredColorCount={_state.RequiredColorCount} targetColorCount={_targetColorCount} " +
            $"CollectedColorOrderCount={_state.CollectedColorOrder.Count}");
        _cauldronView.SetTargetColorCount(_targetColorCount);
        _bagSystem.CollectCompletedBottles(_state);
        RefreshAllViews();
    }

    private void CreateLevelState()
    {
        if (IsManagedByMainFlow && !string.IsNullOrWhiteSpace(SelectedFlowerId))
        {
            _levelGenerator.GenerateSolvableLevel(_state, SelectedFlowerId, SelectedLevelNumber);
            return;
        }

        _levelGenerator.CreateInitialState(_state);
    }

    private void PrintLevelEntryDiagnostics()
    {
        string flowerId = string.IsNullOrWhiteSpace(SelectedFlowerId) ? "(debug_fixed)" : SelectedFlowerId;
        int seed = IsManagedByMainFlow && !string.IsNullOrWhiteSpace(SelectedFlowerId)
            ? _levelGenerator.LastGenerationStats.Seed
            : 0;
        GD.Print(
            $"GAME_SCENE_LEVEL_DIAG flower_id={flowerId} level_number={SelectedLevelNumber} " +
            $"bottle_count={_state.Bottles.Count} color_count={GetRequiredColorCount()} seed={seed} " +
            $"managed={IsManagedByMainFlow.ToString().ToLowerInvariant()}");
    }

    private void RequestExit()
    {
        if (!IsManagedByMainFlow)
        {
            GD.PushWarning("GameScene exit was requested without MainFlowController management.");
            return;
        }

        if (_isResolving)
        {
            EmitSignal(SignalName.PouringStateChanged, "Blocked", "Exit ignored while resolving.");
            return;
        }

        _selectedBottleId = null;
        RefreshSelectionViews();
        EmitSignal(SignalName.PouringStateChanged, "Idle", "Exit");
        EmitSignal(SignalName.ExitRequested);
    }

    private void CacheBottleViews(int bottleCount)
    {
        _bottleViews.Clear();

        Node currentScene = GetNode<Node>("../..");
        Node bottleRoot = currentScene.GetNode<Node>("WorldRoot/BottleRoot");
        BottleView template = bottleRoot.GetNode<BottleView>("Bottle_5");
        IReadOnlyList<Vector2> positions = BottleLayoutHelper.GetPositions(bottleCount);
        HashSet<BottleView> activeViews = new();
        for (int i = 0; i < bottleCount; i++)
        {
            BottleView? view = bottleRoot.GetNodeOrNull<BottleView>($"Bottle_{i}");
            if (view == null)
            {
                view = (BottleView)template.Duplicate();
                view.Name = $"Bottle_{i}";
                bottleRoot.AddChild(view);
            }

            view.Clicked -= OnBottleClicked;
            view.ApplyLayoutPosition(positions[i]);
            view.Bind(i);
            view.Clicked += OnBottleClicked;
            _bottleViews.Add(view);
            activeViews.Add(view);
        }

        foreach (Node child in bottleRoot.GetChildren())
        {
            if (child is BottleView view && !activeViews.Contains(view))
            {
                view.Clicked -= OnBottleClicked;
                view.DeactivateForLayout();
            }
        }
    }

    private void CacheCauldronView()
    {
        Node currentScene = GetNode<Node>("../..");
        _cauldronView = currentScene.GetNode<CauldronView>("WorldRoot/CauldronRoot/CauldronView");
    }

    private void RefreshAllBottleViews()
    {
        for (int i = 0; i < _bottleViews.Count; i++)
        {
            _bottleViews[i].Refresh(_state.Bottles[i]);
        }
    }

    private void RefreshAllViews()
    {
        RefreshAllBottleViews();
        _cauldronView.RefreshProgress(_state.CollectedColorOrder);
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

        AudioManager.PlayGlobalBlocked();
        _bottleViews[bottleId].PlayInvalidFeedback();
        _uiManager.ShowLocalizedTip("game.cannot_pour");
        _selectedBottleId = null;
        RefreshSelectionViews();
        EmitSignal(SignalName.PouringStateChanged, "Blocked", result.FailReason);
    }

    private void RefreshSelectionViews()
    {
        for (int i = 0; i < _bottleViews.Count; i++)
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
        return GetCollectedCount() >= GetRequiredColorCount();
    }

    private int GetCollectedCount()
    {
        return _state.CollectedColorOrder.Count;
    }

    private int GetRequiredColorCount()
    {
        return _state.RequiredColorCount > 0 ? _state.RequiredColorCount : _state.Bags.Count;
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
        _cauldronView.HideRewards();

        CreateLevelState();
        CacheBottleViews(_state.Bottles.Count);
        _targetColorCount = GetRequiredColorCount();
        GD.Print(
            $"CAULDRON_DIAG GameManager.SetTargetColorCount restart " +
            $"RequiredColorCount={_state.RequiredColorCount} targetColorCount={_targetColorCount} " +
            $"CollectedColorOrderCount={_state.CollectedColorOrder.Count}");
        _cauldronView.SetTargetColorCount(_targetColorCount);
        _bagSystem.CollectCompletedBottles(_state);
        RefreshAllViews();
    }

    private async System.Threading.Tasks.Task ResolveSuccessfulPourAsync(PourPlan plan, BottleData source, BottleData target)
    {
        _isResolving = true;
        _selectedBottleId = null;
        RefreshSelectionViews();
        AudioManager.PlayGlobalPour();

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

            int previousCollectedCount = _state.CollectedColorOrder.Count;
            List<int> collectedBottleIds = _bagSystem.CollectCompletedBottles(_state);
            for (int i = 0; i < collectedBottleIds.Count; i++)
            {
                int collectedBottleId = collectedBottleIds[i];
                BottleData collectedBottle = _state.Bottles[collectedBottleId];
                WaterColor collectedColor = collectedBottle.Layers.Count > 0
                    ? collectedBottle.Layers[0].Color
                    : WaterColor.Red;
                int visibleCollectedCount = Mathf.Min(_state.CollectedColorOrder.Count, previousCollectedCount + i + 1);
                IReadOnlyList<WaterColor> visibleCollectedOrder = _state.CollectedColorOrder.GetRange(0, visibleCollectedCount);
                await _cauldronView.PlayBottleCollectAsync(
                    _bottleViews[collectedBottleId],
                    collectedColor,
                    visibleCollectedOrder);
            }

            RefreshAllViews();

            if (IsWin())
            {
                _state.IsGameOver = true;
                AudioManager.PlayGlobalSuccess();
                await _cauldronView.ShowRewardsAsync(SelectedFlowerId);
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
