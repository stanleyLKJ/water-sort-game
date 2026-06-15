#nullable enable

using System.Threading.Tasks;
using Godot;

namespace WaterSortGame.View;

public sealed class TemporaryTipHandle
{
    private ulong _version;

    public void Show(Label label, string message, double seconds = 3.0)
    {
        _version++;
        ulong version = _version;

        if (string.IsNullOrWhiteSpace(message))
        {
            label.Text = string.Empty;
            label.Visible = false;
            return;
        }

        label.Text = message;
        label.Visible = true;
        _ = HideAfterDelayAsync(label, version, seconds);
    }

    private async Task HideAfterDelayAsync(Label label, ulong version, double seconds)
    {
        SceneTree? tree = label.GetTree();
        if (tree == null)
        {
            return;
        }

        SceneTreeTimer timer = tree.CreateTimer(seconds);
        await label.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);

        if (version != _version || !GodotObject.IsInstanceValid(label))
        {
            return;
        }

        label.Text = string.Empty;
        label.Visible = false;
    }
}
