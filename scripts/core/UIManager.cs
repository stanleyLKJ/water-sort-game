#nullable enable

using System;
using Godot;

namespace WaterSortGame.Core;

public sealed partial class UIManager : Node
{
    private Label _tipLabel = null!;
    private Button _restartButton = null!;
    private Button _exitButton = null!;
    private PopupPanel _victoryPopup = null!;
    private Button _popupRestartButton = null!;
    private Label _victoryLabel = null!;
    private int _tipVersion;
    private bool _isCached;
    private LocalizationManager? _localizationManager;

    public event Action? RestartRequested;
    public event Action? ExitRequested;

    public override void _Ready()
    {
        CacheNodes();
        ApplyLocalizedText();
    }

    public void SetLocalizationManager(LocalizationManager localizationManager)
    {
        _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
        if (_isCached)
        {
            ApplyLocalizedText();
        }
    }

    private void CacheNodes()
    {
        Node currentScene = GetNode<Node>("../..");
        if (_isCached)
        {
            return;
        }

        _tipLabel = currentScene.GetNode<Label>("CanvasLayer/TipLabel");
        _restartButton = currentScene.GetNode<Button>("CanvasLayer/RestartButton");
        _exitButton = currentScene.GetNode<Button>("CanvasLayer/ExitButton");
        _victoryPopup = currentScene.GetNode<PopupPanel>("CanvasLayer/VictoryPopup");
        _popupRestartButton = currentScene.GetNode<Button>("CanvasLayer/VictoryPopup/PopupRestartButton");
        _victoryLabel = currentScene.GetNode<Label>("CanvasLayer/VictoryPopup/VictoryLabel");

        _tipLabel.Visible = false;
        _victoryPopup.Visible = false;
        _restartButton.Pressed += OnRestartPressed;
        _exitButton.Pressed += OnExitPressed;
        _popupRestartButton.Pressed += OnRestartPressed;
        _isCached = true;
    }

    public async void ShowTip(string text)
    {
        CacheNodes();
        int version = ++_tipVersion;

        _tipLabel.Text = text;
        _tipLabel.Visible = true;

        SceneTreeTimer timer = GetTree().CreateTimer(1.0);
        await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);

        if (version == _tipVersion)
        {
            _tipLabel.Visible = false;
        }
    }

    public void ShowLocalizedTip(string key)
    {
        ShowTip(Tr(key));
    }

    public void ShowVictory()
    {
        CacheNodes();
        _victoryPopup.Visible = true;
    }

    public void HideVictory()
    {
        CacheNodes();
        _victoryPopup.Visible = false;
    }

    public void SetExitAvailable(bool isAvailable)
    {
        CacheNodes();
        _exitButton.Visible = isAvailable;
        _exitButton.Disabled = !isAvailable;
    }

    private void OnRestartPressed()
    {
        AudioManager.PlayGlobalClick();
        RestartRequested?.Invoke();
    }

    private void OnExitPressed()
    {
        AudioManager.PlayGlobalClick();
        ExitRequested?.Invoke();
    }

    private void ApplyLocalizedText()
    {
        _tipLabel.Text = Tr("game.cannot_pour");
        _restartButton.TooltipText = Tr("game.restart");
        _exitButton.TooltipText = Tr("common.back");
        _victoryLabel.Text = Tr("game.victory");
        _popupRestartButton.Text = Tr("game.restart");
    }

    private string Tr(string key)
    {
        return _localizationManager?.Tr(key) ?? LocalizationManager.GetText(key);
    }
}
