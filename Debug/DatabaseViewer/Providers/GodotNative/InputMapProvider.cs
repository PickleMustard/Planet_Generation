#if DEBUG
using System.Collections.Generic;
using Godot;

namespace Debug.DatabaseViewer;

/// <summary>
/// Data provider for input mappings: actions and their input events.
/// </summary>
[DebugData("Input Map", Category = "Input")]
public class InputMapProvider : IDataProvider
{
    private DebugDataNode? _cachedData;
    private bool _needsRefresh = true;

    public string Name => "Input Map";
    public string Category => "Input";
    public bool NeedsRefresh => _needsRefresh;

    public DebugDataNode GetData()
    {
        return _cachedData ??= BuildInputData();
    }

    public void Refresh()
    {
        _cachedData = null;
        _needsRefresh = false;
    }

    public IEnumerable<string> Search(string pattern)
    {
        var data = GetData();
        var results = new List<string>();
        SearchRecursive(data, "", pattern.ToLower(), results);
        return results;
    }

    private void SearchRecursive(DebugDataNode node, string path, string pattern, List<string> results)
    {
        var currentPath = string.IsNullOrEmpty(path) ? node.Name : $"{path}/{node.Name}";

        if (node.Name.ToLower().Contains(pattern) ||
            (node.HasValue && node.Value?.ToString()?.ToLower()?.Contains(pattern) == true))
        {
            results.Add(currentPath);
        }

        foreach (var prop in node.Properties.Values)
        {
            var propPath = $"{currentPath}.{prop.Name}";
            if (prop.Name.ToLower().Contains(pattern) ||
                (prop.HasValue && prop.Value?.ToString()?.ToLower()?.Contains(pattern) == true))
            {
                results.Add(propPath);
            }
        }

        foreach (var child in node.Children)
        {
            SearchRecursive(child, currentPath, pattern, results);
        }
    }

    private DebugDataNode BuildInputData()
    {
        var root = new DebugDataNode("Input Map");

        var actions = InputMap.GetActions();
        root.AddProperty("Action Count", actions.Count);

        var actionsNode = root.AddChild("Actions");

        foreach (var actionName in actions)
        {
            var actionStr = actionName.ToString();
            var actionNode = actionsNode.AddChild(actionStr);

            var events = InputMap.ActionGetEvents(actionName);
            actionNode.AddProperty("Event Count", events.Count);

            if (events.Count > 0)
            {
                var eventsNode = actionNode.AddChild("Events");

                for (int i = 0; i < events.Count; i++)
                {
                    var evt = events[i];
                    var eventNode = eventsNode.AddChild($"Event {i}");

                    if (evt is InputEventKey keyEvent)
                    {
                        eventNode.AddProperty("Type", "Key");
                        eventNode.AddProperty("Key", keyEvent.Keycode.ToString());
                        eventNode.AddProperty("Physical Key", keyEvent.PhysicalKeycode.ToString());
                        eventNode.AddProperty("Shift", keyEvent.ShiftPressed);
                        eventNode.AddProperty("Ctrl", keyEvent.CtrlPressed);
                        eventNode.AddProperty("Alt", keyEvent.AltPressed);
                        eventNode.AddProperty("Meta", keyEvent.MetaPressed);
                    }
                    else if (evt is InputEventMouseButton mouseButton)
                    {
                        eventNode.AddProperty("Type", "Mouse Button");
                        eventNode.AddProperty("Button", mouseButton.ButtonIndex.ToString());
                        eventNode.AddProperty("Double Click", mouseButton.DoubleClick);
                        eventNode.AddProperty("Shift", mouseButton.ShiftPressed);
                        eventNode.AddProperty("Ctrl", mouseButton.CtrlPressed);
                    }
                    else if (evt is InputEventJoypadButton joyButton)
                    {
                        eventNode.AddProperty("Type", "Joypad Button");
                        eventNode.AddProperty("Button", joyButton.ButtonIndex.ToString());
                        eventNode.AddProperty("Device", joyButton.Device);
                    }
                    else if (evt is InputEventJoypadMotion joyMotion)
                    {
                        eventNode.AddProperty("Type", "Joypad Motion");
                        eventNode.AddProperty("Axis", joyMotion.Axis.ToString());
                        eventNode.AddProperty("Device", joyMotion.Device);
                    }
                    else
                    {
                        eventNode.AddProperty("Type", evt?.GetType().Name ?? "Unknown");
                    }
                }
            }
        }

        return root;
    }
}
#endif
