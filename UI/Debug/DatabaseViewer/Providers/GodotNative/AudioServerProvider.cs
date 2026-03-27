#if DEBUG
using System.Collections.Generic;
using Godot;

namespace UI.Debug.DatabaseViewer;

/// <summary>
/// Data provider for audio system: buses, volumes, and active streams.
/// </summary>
[DebugData("Audio Server", Category = "Audio")]
public class AudioServerProvider : IDataProvider
{
    private DebugDataNode? _cachedData;
    private bool _needsRefresh = true;

    public string Name => "Audio Server";
    public string Category => "Audio";
    public bool NeedsRefresh => _needsRefresh;

    public DebugDataNode GetData()
    {
        return _cachedData ??= BuildAudioData();
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

    private DebugDataNode BuildAudioData()
    {
        var root = new DebugDataNode("Audio Server");

        root.AddProperty("Bus Count", AudioServer.BusCount);
        root.AddProperty("Output Device", AudioServer.GetOutputDevice());

        var busesNode = root.AddChild("Buses");

        for (int i = 0; i < AudioServer.BusCount; i++)
        {
            var busName = AudioServer.GetBusName(i);
            var busNode = busesNode.AddChild(busName);
            busNode.AddProperty("Index", i);
            busNode.AddProperty("Volume (dB)", $"{AudioServer.GetBusVolumeDb(i):F2} dB");
            busNode.AddProperty("Mute", AudioServer.IsBusMute(i));
            busNode.AddProperty("Solo", AudioServer.IsBusSolo(i));
            busNode.AddProperty("Bypass", AudioServer.IsBusBypassingEffects(i));

            var sendName = AudioServer.GetBusSend(i);
            if (!string.IsNullOrEmpty(sendName))
            {
                busNode.AddProperty("Send", sendName);
            }

            int effectCount = AudioServer.GetBusEffectCount(i);
            if (effectCount > 0)
            {
                var effectsNode = busNode.AddChild("Effects");
                for (int j = 0; j < effectCount; j++)
                {
                    var effect = AudioServer.GetBusEffect(i, j);
                    var effectNode = effectsNode.AddChild($"Effect {j}");
                    effectNode.AddProperty("Type", effect?.GetType().Name ?? "Unknown");
                    effectNode.AddProperty("Enabled", AudioServer.IsBusEffectEnabled(i, j));
                }
            }
        }

        var streamsNode = root.AddChild("Active Streams").SetCollapsed();
        CollectActiveStreams(streamsNode);

        return root;
    }

    private void CollectActiveStreams(DebugDataNode streamsNode)
    {
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        if (sceneTree?.Root == null) return;

        var audioPlayers = new List<AudioStreamPlayer>();
        var audioPlayers2D = new List<AudioStreamPlayer2D>();
        var audioPlayers3D = new List<AudioStreamPlayer3D>();

        void FindAudioPlayers(Node node)
        {
            if (node is AudioStreamPlayer player && player.Playing)
                audioPlayers.Add(player);
            if (node is AudioStreamPlayer2D player2D && player2D.Playing)
                audioPlayers2D.Add(player2D);
            if (node is AudioStreamPlayer3D player3D && player3D.Playing)
                audioPlayers3D.Add(player3D);

            foreach (var child in node.GetChildren())
            {
                FindAudioPlayers(child);
            }
        }

        for (int i = 0; i < sceneTree.Root.GetChildCount(); i++)
        {
            FindAudioPlayers(sceneTree.Root.GetChild(i));
        }

        foreach (var player in audioPlayers)
        {
            var node = streamsNode.AddChild(player.Name);
            node.AddProperty("Type", "AudioStreamPlayer");
            node.AddProperty("Stream", player.Stream?.ResourcePath ?? player.Stream?.ToString() ?? "None");
            node.AddProperty("Volume (dB)", $"{player.VolumeDb:F2} dB");
            node.AddProperty("Pitch Scale", player.PitchScale);
            node.AddProperty("Bus", player.Bus);
            node.AddProperty("Playback Position", $"{player.GetPlaybackPosition():F2}s");
        }

        foreach (var player in audioPlayers2D)
        {
            var node = streamsNode.AddChild(player.Name);
            node.AddProperty("Type", "AudioStreamPlayer2D");
            node.AddProperty("Stream", player.Stream?.ResourcePath ?? player.Stream?.ToString() ?? "None");
            node.AddProperty("Volume (dB)", $"{player.VolumeDb:F2} dB");
            node.AddProperty("Position", player.Position);
            node.AddProperty("Bus", player.Bus);
        }

        foreach (var player in audioPlayers3D)
        {
            var node = streamsNode.AddChild(player.Name);
            node.AddProperty("Type", "AudioStreamPlayer3D");
            node.AddProperty("Stream", player.Stream?.ResourcePath ?? player.Stream?.ToString() ?? "None");
            node.AddProperty("Volume (dB)", $"{player.VolumeDb:F2} dB");
            node.AddProperty("Position", player.Position);
            node.AddProperty("Bus", player.Bus);
        }

        streamsNode.AddProperty("Total Active", audioPlayers.Count + audioPlayers2D.Count + audioPlayers3D.Count);
    }
}
#endif
