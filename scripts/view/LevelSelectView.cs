#nullable enable

using System;
using Godot;

namespace WaterSortGame.View;

public sealed partial class LevelSelectView : Control
{
    public event Action? LevelOneRequested;
    public event Action? BackRequested;

    public override void _Ready()
    {
        GetNode<Button>("Panel/LevelOneButton").Pressed += OnLevelOnePressed;
        GetNode<Button>("Panel/BackButton").Pressed += OnBackPressed;
    }

    private void OnLevelOnePressed()
    {
        LevelOneRequested?.Invoke();
    }

    private void OnBackPressed()
    {
        BackRequested?.Invoke();
    }
}
