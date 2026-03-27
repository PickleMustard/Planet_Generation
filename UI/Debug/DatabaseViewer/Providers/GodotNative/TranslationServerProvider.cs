#if DEBUG
using System.Collections.Generic;
using Godot;

namespace UI.Debug.DatabaseViewer;

/// <summary>
/// Data provider for localization: locales and translation keys.
/// </summary>
[DebugData("Translation Server", Category = "Localization")]
public class TranslationServerProvider : IDataProvider
{
    private DebugDataNode? _cachedData;
    private bool _needsRefresh = true;

    public string Name => "Translation Server";
    public string Category => "Localization";
    public bool NeedsRefresh => _needsRefresh;

    public DebugDataNode GetData()
    {
        return _cachedData ??= BuildTranslationData();
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

    private DebugDataNode BuildTranslationData()
    {
        var root = new DebugDataNode("Translation Server");

        root.AddProperty("Locale", TranslationServer.GetLocale());

        var allLocales = TranslationServer.GetAllLanguages();
        root.AddProperty("Available Languages", allLocales.Length);

        var localesNode = root.AddChild("Available Locales");
        foreach (var locale in allLocales)
        {
            var localeStr = locale.ToString();
            var localeNode = localesNode.AddChild(localeStr);
            var langName = TranslationServer.GetLanguageName(locale);
            localeNode.AddProperty("Name", langName);
        }

        var translationsNode = root.AddChild("Loaded Translations");
        var translations = CollectTranslations();
        translationsNode.AddProperty("Count", translations.Count);

        foreach (var kvp in translations)
        {
            var transNode = translationsNode.AddChild(kvp.Key);
            transNode.AddProperty("Locale", kvp.Value);
        }

        var pseudolocNode = root.AddChild("Pseudolocalization");
        pseudolocNode.AddProperty("Enabled", TranslationServer.IsPseudolocalizationEnabled());

        return root;
    }

    private Dictionary<string, string> CollectTranslations()
    {
        var translations = new Dictionary<string, string>();

        var sceneTree = Engine.GetMainLoop() as SceneTree;
        if (sceneTree?.Root == null) return translations;

        void CollectFromNode(Node node)
        {
            var propertyList = node.GetPropertyList();
            foreach (var property in propertyList)
            {
                if (property.TryGetValue("name", out var nameVar) &&
                    property.TryGetValue("type", out var typeVar))
                {
                    var type = (Variant.Type)(int)typeVar;
                    if (type == Variant.Type.Object)
                    {
                        var propName = nameVar.AsStringName();
                        var value = node.Get(propName);
                        if (value.Obj is Translation translation)
                        {
                            var key = !string.IsNullOrEmpty(translation.ResourcePath)
                                ? translation.ResourcePath
                                : $"[inline:{node.Name}]";
                            translations[key] = translation.Locale;
                        }
                    }
                }
            }

            foreach (var child in node.GetChildren())
            {
                CollectFromNode(child);
            }
        }

        for (int i = 0; i < sceneTree.Root.GetChildCount(); i++)
        {
            CollectFromNode(sceneTree.Root.GetChild(i));
        }

        return translations;
    }
}
#endif
