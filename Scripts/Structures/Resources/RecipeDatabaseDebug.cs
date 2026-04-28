#if DEBUG
using System;
using System.Collections.Generic;
using UI.Debug.DatabaseViewer;

namespace Structures.Resources
{
    public partial class RecipeDatabase : IDebugDataProvider
    {
        string IDataProvider.Name => "Recipes";
        string IDataProvider.Category => "Game";
        bool IDataProvider.NeedsRefresh => true;
        object IDebugDataProvider.SourceObject => this;
        string IDebugDataProvider.InstanceNamespace => "RecipeDatabase";
        bool IDebugDataProvider.IsSourceValid => IsLoaded;

        DebugDataNode IDataProvider.GetData()
        {
            if (!IsLoaded)
            {
                var unloadedNode = new DebugDataNode("Recipes");
                unloadedNode.AddProperty("Status", "Not loaded");
                unloadedNode.AddProperty("Load Progress", $"{LoadProgress:P0}");
                return unloadedNode;
            }

            var node = new DebugDataNode("Recipes").AddProperty(
                "Total Definitions",
                _recipes.Count
            );

            var categoriesNode = node.AddChild("By Category");
            var categories = new Dictionary<string, List<RecipeDefinition>>();

            foreach (var recipe in _recipes.Values)
            {
                var category = recipe.Category ?? "Uncategorized";
                if (!categories.ContainsKey(category))
                {
                    categories[category] = new List<RecipeDefinition>();
                }
                categories[category].Add(recipe);
            }

            foreach (var category in categories)
            {
                var categoryNode = categoriesNode.AddChild(category.Key)
                    .AddProperty("Count", category.Value.Count);

                foreach (var recipe in category.Value)
                {
                    var recipeNode = categoryNode.AddChild(recipe.RecipeId ?? "Unknown")
                        .AddProperty("Display Name", recipe.DisplayName ?? "")
                        .AddProperty("Description", recipe.Description ?? "")
                        .AddProperty("Work Required", recipe.WorkRequired);

                    var inputsNode = recipeNode.AddChild("Input Resources");
                    if (recipe.InputResources.Count == 0)
                    {
                        inputsNode.AddProperty("None", "No inputs required");
                    }
                    else
                    {
                        foreach (var input in recipe.InputResources)
                        {
                            inputsNode.AddProperty(input.Key, input.Value);
                        }
                    }

                    var outputsNode = recipeNode.AddChild("Output Resources");
                    if (recipe.OutputResources.Count == 0)
                    {
                        outputsNode.AddProperty("None", "No outputs produced");
                    }
                    else
                    {
                        foreach (var output in recipe.OutputResources)
                        {
                            outputsNode.AddProperty(output.Key, output.Value);
                        }
                    }
                }
            }

            return node;
        }

        void IDataProvider.Refresh() { }

        IEnumerable<string> IDataProvider.Search(string pattern)
        {
            if (!IsLoaded)
            {
                return Array.Empty<string>();
            }

            var results = new List<string>();
            foreach (var kvp in _recipes)
            {
                if (
                    kvp.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                    || (kvp.Value.DisplayName?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (kvp.Value.Category?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false)
                )
                {
                    results.Add($"By Category/{kvp.Value.Category ?? "Uncategorized"}/{kvp.Key}");
                }
            }
            return results;
        }
    }
}
#endif
