#nullable enable

using Godot;

namespace WaterSortGame.View;

public sealed partial class TutorialBubbleView : Control
{
    private const double AutoHideSeconds = 5d;

    private PanelContainer _bubblePanel = null!;
    private Label _messageLabel = null!;
    private Timer _autoHideTimer = null!;

    public string CurrentTutorialKey { get; private set; } = string.Empty;
    public string CurrentText => _messageLabel?.Text ?? string.Empty;
    public bool IsShowing => _bubblePanel?.Visible == true;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        BuildBubble();
        DismissTutorial();
    }

    public void ShowTutorial(string tutorialKey, string text)
    {
        CurrentTutorialKey = tutorialKey;
        _messageLabel.Text = text;
        _bubblePanel.Visible = true;
        _autoHideTimer.Stop();
        _autoHideTimer.Start(AutoHideSeconds);
    }

    public void DismissTutorial()
    {
        CurrentTutorialKey = string.Empty;
        if (_messageLabel != null)
        {
            _messageLabel.Text = string.Empty;
        }

        if (_bubblePanel != null)
        {
            _bubblePanel.Visible = false;
        }

        _autoHideTimer?.Stop();
    }

    private void BuildBubble()
    {
        _bubblePanel = new PanelContainer
        {
            Name = "BubblePanel",
            MouseFilter = MouseFilterEnum.Stop
        };
        _bubblePanel.SetAnchorsPreset(LayoutPreset.TopWide);
        _bubblePanel.OffsetLeft = 42f;
        _bubblePanel.OffsetTop = 42f;
        _bubblePanel.OffsetRight = -42f;
        _bubblePanel.OffsetBottom = 218f;
        _bubblePanel.GuiInput += OnBubbleGuiInput;

        StyleBoxFlat panelStyle = new()
        {
            BgColor = new Color(0.12f, 0.09f, 0.06f, 0.94f),
            BorderColor = new Color(0.95f, 0.77f, 0.37f, 0.96f),
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 20,
            CornerRadiusTopRight = 20,
            CornerRadiusBottomLeft = 20,
            CornerRadiusBottomRight = 20,
            ContentMarginLeft = 22f,
            ContentMarginTop = 16f,
            ContentMarginRight = 22f,
            ContentMarginBottom = 14f
        };
        _bubblePanel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_bubblePanel);

        VBoxContainer content = new()
        {
            Name = "Content",
            MouseFilter = MouseFilterEnum.Pass
        };
        content.AddThemeConstantOverride("separation", 8);
        _bubblePanel.AddChild(content);

        _messageLabel = new Label
        {
            Name = "MessageLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _messageLabel.AddThemeFontSizeOverride("font_size", 25);
        _messageLabel.AddThemeColorOverride("font_color", new Color(1f, 0.97f, 0.86f));
        content.AddChild(_messageLabel);

        Button closeButton = new()
        {
            Name = "CloseButton",
            Text = "×",
            CustomMinimumSize = new Vector2(96f, 38f),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            FocusMode = FocusModeEnum.None
        };
        closeButton.AddThemeFontSizeOverride("font_size", 22);
        closeButton.Pressed += DismissTutorial;
        content.AddChild(closeButton);

        _autoHideTimer = new Timer
        {
            Name = "AutoHideTimer",
            OneShot = true,
            Autostart = false
        };
        _autoHideTimer.Timeout += DismissTutorial;
        AddChild(_autoHideTimer);
    }

    private void OnBubbleGuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton mouseButton
            && mouseButton.ButtonIndex == MouseButton.Left
            && mouseButton.Pressed)
        {
            DismissTutorial();
        }
    }
}
