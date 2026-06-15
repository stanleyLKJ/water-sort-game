#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using WaterSortGame.Core;
using WaterSortGame.Model;

namespace WaterSortGame.View;

public sealed partial class CauldronView : Node2D
{
    private const float BottleFlySeconds = 0.42f;
    private const float RewardPanelSeconds = 3f;
    private const string ItemTexturePathFormat = "res://assets/flowers/{0}/items/{0}_{1}.png";
    private static readonly Color EmptyProgressBubbleColor = new(0.58f, 0.58f, 0.58f, 0.34f);
    private const int DefaultProgressBubbleCount = 4;
    private const int MaxProgressBubbleCount = 6;

    private Label? _progressLabel;
    private Label? _rewardTitleLabel;
    private Label? _flowerIdLabel;
    private Label? _seedLabel;
    private Label? _potionLabel;
    private Label? _autoContinueLabel;
    private TextureRect? _seedTexture;
    private TextureRect? _potionTexture;
    private Button? _goPlantButton;
    private Control? _rewardPanel;
    private Node2D? _targetPoint;
    private readonly ColorRect?[] _progressBubbles = new ColorRect?[MaxProgressBubbleCount];
    private bool _isCached;
    private bool _goPlantButtonConnected;
    private bool _rewardCompleted;
    private TaskCompletionSource<bool>? _rewardCompletionSource;
    private LocalizationManager? _localizationManager;
    private int _targetColorCount = DefaultProgressBubbleCount;
    private int _runtimeCreatedProgressBubbleCount;

    public void SetLocalizationManager(LocalizationManager localizationManager)
    {
        _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
        if (_isCached)
        {
            ApplyLocalizedText();
        }
    }

    public override void _Ready()
    {
        CacheNodes();
        RefreshProgress(0, _targetColorCount);
        ApplyLocalizedText();
        HideRewards();
    }

    public void SetTargetColorCount(int targetColorCount)
    {
        CacheNodes();
        _targetColorCount = Mathf.Clamp(targetColorCount, 1, MaxProgressBubbleCount);
        LayoutProgressBubbles();
        PrintProgressBubbleDiagnostics($"SetTargetColorCount received={targetColorCount}");
        RefreshProgress(Array.Empty<WaterColor>());
    }

    public void RefreshProgress(int collectedCount, int totalCount)
    {
        CacheNodes();
        _targetColorCount = Mathf.Clamp(totalCount, 1, MaxProgressBubbleCount);
        LayoutProgressBubbles();
        SetProgressLabel(collectedCount, _targetColorCount);

        for (int i = 0; i < _progressBubbles.Length; i++)
        {
            ColorRect? bubble = _progressBubbles[i];
            if (bubble == null)
            {
                continue;
            }

            bubble.Color = EmptyProgressBubbleColor;
        }

        PrintProgressBubbleDiagnostics($"RefreshProgress count collected={collectedCount} total={totalCount}");
    }

    public void RefreshProgress(IReadOnlyDictionary<WaterColor, BagData> bags)
    {
        CacheNodes();

        int collectedCount = 0;
        foreach (BagData bag in bags.Values)
        {
            collectedCount += bag.CollectedCount;
        }

        SetProgressLabel(collectedCount, _targetColorCount);
        ClearProgressBubbles();
        PrintProgressBubbleDiagnostics($"RefreshProgress bags collected={collectedCount}");
    }

    public void RefreshProgress(IReadOnlyList<WaterColor> collectedColorOrder)
    {
        CacheNodes();

        int collectedCount = collectedColorOrder.Count;
        SetProgressLabel(collectedCount, _targetColorCount);

        for (int i = 0; i < _progressBubbles.Length; i++)
        {
            ColorRect? bubble = _progressBubbles[i];
            if (bubble == null)
            {
                continue;
            }

            bubble.Color = i < _targetColorCount && i < collectedColorOrder.Count
                ? GetProgressBubbleColor(collectedColorOrder[i])
                : EmptyProgressBubbleColor;
        }

        PrintProgressBubbleDiagnostics($"RefreshProgress order collected={collectedCount}");
    }

    public async Task PlayBottleCollectAsync(
        BottleView bottleView,
        WaterColor color,
        IReadOnlyList<WaterColor> collectedColorOrder)
    {
        CacheNodes();

        Vector2 originalGlobalPosition = bottleView.GlobalPosition;
        Vector2 originalScale = bottleView.Scale;
        float originalRotation = bottleView.Rotation;
        Color originalModulate = bottleView.Modulate;
        Vector2 targetPosition = _targetPoint?.GlobalPosition ?? GlobalPosition;

        Tween tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(bottleView, "global_position", targetPosition, BottleFlySeconds);
        tween.Parallel().TweenProperty(bottleView, "scale", originalScale * 0.42f, BottleFlySeconds);
        tween.Parallel().TweenProperty(bottleView, "rotation", originalRotation + Mathf.DegToRad(18f), BottleFlySeconds);
        tween.Parallel().TweenProperty(bottleView, "modulate", new Color(GetColor(color), 0.2f), BottleFlySeconds);
        await ToSignal(tween, Tween.SignalName.Finished);

        bottleView.Visible = false;
        bottleView.GlobalPosition = originalGlobalPosition;
        bottleView.Scale = originalScale;
        bottleView.Rotation = originalRotation;
        bottleView.Modulate = originalModulate;

        RefreshProgress(collectedColorOrder);
        await PlayCauldronPulseAsync(color);
    }

    private void ClearProgressBubbles()
    {
        for (int i = 0; i < _progressBubbles.Length; i++)
        {
            ColorRect? bubble = _progressBubbles[i];
            if (bubble != null)
            {
                bubble.Color = EmptyProgressBubbleColor;
            }
        }
    }

    public async Task ShowRewardsAsync(string? flowerId)
    {
        CacheNodes();
        if (_rewardPanel == null)
        {
            return;
        }

        _rewardCompleted = false;
        _rewardCompletionSource = new TaskCompletionSource<bool>();

        string targetFlowerId = string.IsNullOrWhiteSpace(flowerId) ? "pink_rose" : flowerId.Trim();
        string targetName = Tr($"flower.{targetFlowerId}.name");
        if (_rewardTitleLabel != null)
        {
            _rewardTitleLabel.Text = Tr("reward.title");
        }

        if (_flowerIdLabel != null)
        {
            _flowerIdLabel.Text = string.IsNullOrWhiteSpace(targetName)
                ? targetFlowerId
                : $"{targetName} ({targetFlowerId})";
        }

        if (_seedLabel != null)
        {
            _seedLabel.Text = TrFormat("common.seed_count", 1);
        }

        if (_potionLabel != null)
        {
            _potionLabel.Text = TrFormat("common.potion_count", 1);
        }

        LoadRewardItemTextures(targetFlowerId);

        if (_goPlantButton != null)
        {
            _goPlantButton.Text = Tr("reward.plant");
            _goPlantButton.Disabled = false;
        }

        _rewardPanel.Modulate = new Color(1f, 1f, 1f, 0f);
        _rewardPanel.Visible = true;

        Tween showTween = CreateTween();
        showTween.TweenProperty(_rewardPanel, "modulate", Colors.White, 0.18);
        await ToSignal(showTween, Tween.SignalName.Finished);

        _ = AutoCompleteRewardAfterDelayAsync();
        await _rewardCompletionSource.Task;
        HideRewards();
    }

    public void LoadRewardItemTextures(string flowerId)
    {
        CacheNodes();
        TrySetRewardTexture(_seedTexture, flowerId, "seed");
        TrySetRewardTexture(_potionTexture, flowerId, "potion");
    }

    public void HideRewards()
    {
        CacheNodes();
        if (_rewardPanel != null)
        {
            _rewardPanel.Visible = false;
            _rewardPanel.Modulate = Colors.White;
        }

        if (_seedTexture != null)
        {
            _seedTexture.Texture = null;
            _seedTexture.Visible = false;
        }

        if (_potionTexture != null)
        {
            _potionTexture.Texture = null;
            _potionTexture.Visible = false;
        }

        if (_goPlantButton != null)
        {
            _goPlantButton.Disabled = false;
        }
    }

    private async Task AutoCompleteRewardAfterDelayAsync()
    {
        SceneTreeTimer timer = GetTree().CreateTimer(RewardPanelSeconds);
        await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        CompleteRewardOnce();
    }

    private void OnGoPlantButtonPressed()
    {
        AudioManager.PlayGlobalClick();
        CompleteRewardOnce();
    }

    private void CompleteRewardOnce()
    {
        if (_rewardCompleted)
        {
            return;
        }

        _rewardCompleted = true;
        if (_goPlantButton != null)
        {
            _goPlantButton.Disabled = true;
        }

        _rewardCompletionSource?.TrySetResult(true);
    }

    private void SetProgressLabel(int collectedCount, int totalCount)
    {
        int safeTotal = Mathf.Max(1, totalCount);
        int safeCollected = Mathf.Clamp(collectedCount, 0, safeTotal);
        if (_progressLabel != null)
        {
            _progressLabel.Text = $"{safeCollected}/{safeTotal}";
        }
    }

    private async Task PlayCauldronPulseAsync(WaterColor color)
    {
        Node2D? sprite = GetNodeOrNull<Node2D>("CauldronSprite");
        if (sprite == null)
        {
            return;
        }

        Vector2 originalScale = sprite.Scale;
        Tween tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "scale", originalScale * 1.08f, 0.08);
        tween.TweenProperty(sprite, "scale", originalScale, 0.14);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private void CacheNodes()
    {
        if (_isCached)
        {
            return;
        }

        _progressLabel = GetNodeOrNull<Label>("CauldronProgressRoot/ProgressLabel");
        _targetPoint = GetNodeOrNull<Node2D>("CollectTargetPoint");
        _rewardPanel = GetNodeOrNull<Control>("RewardPanel");
        _rewardTitleLabel = GetNodeOrNull<Label>("RewardPanel/RewardTitleLabel");
        _flowerIdLabel = GetNodeOrNull<Label>("RewardPanel/FlowerIdLabel");
        _seedLabel = GetNodeOrNull<Label>("RewardPanel/SeedLabel");
        _potionLabel = GetNodeOrNull<Label>("RewardPanel/PotionLabel");
        _autoContinueLabel = GetNodeOrNull<Label>("RewardPanel/RewardContinueLabel");
        _seedTexture = GetNodeOrNull<TextureRect>("RewardPanel/SeedTexture");
        _potionTexture = GetNodeOrNull<TextureRect>("RewardPanel/PotionTexture");
        _goPlantButton = GetNodeOrNull<Button>("RewardPanel/GoPlantButton");
        if (_goPlantButton != null && !_goPlantButtonConnected)
        {
            _goPlantButton.Pressed += OnGoPlantButtonPressed;
            _goPlantButtonConnected = true;
        }

        for (int i = 0; i < _progressBubbles.Length; i++)
        {
            _progressBubbles[i] = GetNodeOrNull<ColorRect>($"CauldronProgressRoot/Bubble_{i}");
            if (_progressBubbles[i] == null)
            {
                ColorRect bubble = new()
                {
                    Name = $"Bubble_{i}",
                    Color = EmptyProgressBubbleColor,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                GetNode<Node>("CauldronProgressRoot").AddChild(bubble);
                _progressBubbles[i] = bubble;
                _runtimeCreatedProgressBubbleCount++;
            }
        }

        _isCached = true;
        LayoutProgressBubbles();
    }

    private void LayoutProgressBubbles()
    {
        const float bubbleSize = 22f;
        const float gap = 14f;
        const float centerX = 11f;
        float totalWidth = (_targetColorCount * bubbleSize) + ((_targetColorCount - 1) * gap);
        float startX = centerX - (totalWidth * 0.5f);

        for (int i = 0; i < _progressBubbles.Length; i++)
        {
            ColorRect? bubble = _progressBubbles[i];
            if (bubble == null)
            {
                continue;
            }

            bubble.Visible = i < _targetColorCount;
            float left = startX + (i * (bubbleSize + gap));
            bubble.OffsetLeft = left;
            bubble.OffsetTop = 12f;
            bubble.OffsetRight = left + bubbleSize;
            bubble.OffsetBottom = 34f;
        }
    }

    private void PrintProgressBubbleDiagnostics(string stage)
    {
        GD.Print(
            $"CAULDRON_DIAG CauldronView.{stage} targetColorCount={_targetColorCount} " +
            $"cachedBubbleCount={CountCachedProgressBubbles()} runtimeCreatedBubbleCount={_runtimeCreatedProgressBubbleCount}");

        for (int i = 0; i < _progressBubbles.Length; i++)
        {
            ColorRect? bubble = _progressBubbles[i];
            if (bubble == null)
            {
                GD.Print($"CAULDRON_DIAG CauldronView.Bubble_{i} null");
                continue;
            }

            GD.Print(
                $"CAULDRON_DIAG CauldronView.Bubble_{i} " +
                $"Visible={bubble.Visible} Position={bubble.Position} GlobalPosition={bubble.GlobalPosition} " +
                $"Size={bubble.Size} ColorA={bubble.Color.A} ModulateA={bubble.Modulate.A} ZIndex={bubble.ZIndex}");
        }
    }

    private int CountCachedProgressBubbles()
    {
        int count = 0;
        foreach (ColorRect? bubble in _progressBubbles)
        {
            if (bubble != null)
            {
                count++;
            }
        }

        return count;
    }

    private void ApplyLocalizedText()
    {
        if (_rewardTitleLabel != null)
        {
            _rewardTitleLabel.Text = Tr("reward.title");
        }

        if (_seedLabel != null)
        {
            _seedLabel.Text = TrFormat("common.seed_count", 1);
        }

        if (_potionLabel != null)
        {
            _potionLabel.Text = TrFormat("common.potion_count", 1);
        }

        if (_goPlantButton != null)
        {
            _goPlantButton.Text = Tr("reward.plant");
        }

        if (_autoContinueLabel != null)
        {
            _autoContinueLabel.Text = Tr("reward.auto_continue");
        }
    }

    private string Tr(string key)
    {
        return _localizationManager?.Tr(key) ?? LocalizationManager.GetText(key);
    }

    private string TrFormat(string key, params object[] args)
    {
        return _localizationManager?.TrFormat(key, args)
            ?? string.Format(System.Globalization.CultureInfo.InvariantCulture, LocalizationManager.GetText(key), args);
    }

    private static Color GetColor(WaterColor color)
    {
        return color switch
        {
            WaterColor.Red => new Color(0.9f, 0.24f, 0.32f),
            WaterColor.Blue => new Color(0.2f, 0.52f, 0.95f),
            WaterColor.Yellow => new Color(1.0f, 0.76f, 0.22f),
            WaterColor.Green => new Color(0.38f, 0.75f, 0.32f),
            WaterColor.Purple => new Color(0.62f, 0.36f, 0.82f),
            WaterColor.Orange => new Color(0.95f, 0.48f, 0.16f),
            _ => Colors.White
        };
    }

    private static Color GetProgressBubbleColor(WaterColor color)
    {
        return color switch
        {
            WaterColor.Red => new Color(0.94f, 0.48f, 0.52f, 0.95f),
            WaterColor.Blue => new Color(0.48f, 0.68f, 0.95f, 0.95f),
            WaterColor.Yellow => new Color(0.96f, 0.78f, 0.34f, 0.95f),
            WaterColor.Green => new Color(0.52f, 0.78f, 0.48f, 0.95f),
            WaterColor.Purple => new Color(0.72f, 0.56f, 0.9f, 0.95f),
            WaterColor.Orange => new Color(0.96f, 0.65f, 0.38f, 0.95f),
            _ => Colors.White
        };
    }

    private static void TrySetRewardTexture(TextureRect? textureRect, string flowerId, string itemKind)
    {
        if (textureRect == null)
        {
            return;
        }

        string texturePath = BuildRewardItemPath(flowerId, itemKind);
        Texture2D? texture = LoadItemTexture(texturePath);
        if (texture == null)
        {
            GD.PushWarning($"Cauldron reward {itemKind} texture missing for {flowerId}: {texturePath}");
            textureRect.Texture = null;
            textureRect.Visible = false;
            return;
        }

        textureRect.Texture = texture;
        textureRect.Visible = true;
    }

    public static string BuildRewardItemPath(string flowerId, string itemKind)
    {
        return string.Format(ItemTexturePathFormat, flowerId, itemKind);
    }

    private static Texture2D? LoadItemTexture(string texturePath)
    {
        if (ResourceLoader.Exists(texturePath))
        {
            return GD.Load<Texture2D>(texturePath);
        }

        if (!FileAccess.FileExists(texturePath))
        {
            return null;
        }

        Image image = Image.LoadFromFile(ProjectSettings.GlobalizePath(texturePath));
        if (image == null || image.IsEmpty())
        {
            return null;
        }

        ImageTexture texture = ImageTexture.CreateFromImage(image);
        texture.ResourcePath = texturePath;
        return texture;
    }

}
