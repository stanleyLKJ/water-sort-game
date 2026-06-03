#nullable enable

using System;
using Godot;

namespace WaterSortGame.Core;

public sealed partial class UIManager : Node
{
    private Label _tipLabel = null!;
    private Button _restartButton = null!;
    private PopupPanel _victoryPopup = null!;
    private Button _popupRestartButton = null!;
    private int _tipVersion;

    public event Action? RestartRequested;

    public override void _Ready()
    {
        Node currentScene = GetNode<Node>("../..");
        _tipLabel = currentScene.GetNode<Label>("CanvasLayer/TipLabel");
        _restartButton = currentScene.GetNode<Button>("CanvasLayer/RestartButton");
        _victoryPopup = currentScene.GetNode<PopupPanel>("CanvasLayer/VictoryPopup");
        _popupRestartButton = currentScene.GetNode<Button>("CanvasLayer/VictoryPopup/PopupRestartButton");

        _tipLabel.Visible = false;
        _victoryPopup.Visible = false;
        _restartButton.Pressed += OnRestartPressed;
        _popupRestartButton.Pressed += OnRestartPressed;
    }

    public async void ShowTip(string text)
    {
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

    public void ShowVictory()
    {
        _victoryPopup.Visible = true;
    }

    public void HideVictory()
    {
        _victoryPopup.Visible = false;
    }

    private void OnRestartPressed()
    {
        RestartRequested?.Invoke();
    }
}
