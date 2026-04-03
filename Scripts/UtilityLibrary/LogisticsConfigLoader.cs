using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary
{
    /// <summary>
    /// Represents an engine type category from the configuration.
    /// </summary>
    public class EngineTypeCategory
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a ship template category from the configuration.
    /// </summary>
    public class ShipTemplateCategory
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a specific engine definition from the configuration.
    /// </summary>
    public class EngineDefinition
    {
        public string Name { get; set; } = string.Empty;
        public float SpecificImpulse { get; set; }
        public float Thrust { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a specific ship definition from the configuration.
    /// </summary>
    public class ShipDefinition
    {
        public string Name { get; set; } = string.Empty;
        public float DryMass { get; set; }
        public float CargoCapacity { get; set; }
        public float FuelCapacity { get; set; }
        public string EngineCategory { get; set; } = string.Empty;
        public float ConstructionTime { get; set; }
        public Dictionary<string, int> RequiredResources { get; set; } = new();
    }

    /// <summary>
    /// Represents a station template category from the configuration.
    /// </summary>
    public class StationTemplateCategory
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a specific station definition from the configuration.
    /// </summary>
    public class StationDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string StationType { get; set; } = string.Empty;
        public float ConstructionTime { get; set; }
        public bool CanBuildShips { get; set; }
        public int MaxParallelShipBuilds { get; set; } = 1;
        public bool CanBuildBuildings { get; set; }
        public float BuildingWorkBudgetPerTick { get; set; } = 1.0f;
        public float BuildingScalingPenalty { get; set; } = 0.05f;
        public Dictionary<string, int> RequiredResources { get; set; } = new();
    }

    /// <summary>
    /// Loads and parses logistics-related YAML configurations:
    /// - EngineTypes.yaml: Engine categories for ships
    /// - ShipTemplates.yaml: Ship template categories
    /// - Individual category files: Chemical.yaml, Electric.yaml, Nuclear.yaml, Fusion.yaml
    ///   Cargo_Freighter.yaml, Fast_Courier.yaml, Heavy_Transport.yaml
    /// </summary>
    public static class LogisticsConfigLoader
    {
        private const string EngineTypesPath = "res://Configuration/engines/EngineTypes.yaml";
        private const string ShipTemplatesPath = "res://Configuration/ships/ShipTemplates.yaml";
        private const string StationTemplatesPath = "res://Configuration/stations/StationTemplates.yaml";
        private const string EnginesDirectory = "res://Configuration/engines/";
        private const string ShipsDirectory = "res://Configuration/ships/";
        private const string StationsDirectory = "res://Configuration/stations/";

        private static List<EngineTypeCategory>? _engineTypes;
        private static List<ShipTemplateCategory>? _shipTemplates;
        private static List<StationTemplateCategory>? _stationTemplates;
        private static Dictionary<string, List<EngineDefinition>>? _enginesByCategory;
        private static Dictionary<string, List<ShipDefinition>>? _shipsByCategory;
        private static Dictionary<string, List<StationDefinition>>? _stationsByCategory;

        /// <summary>
        /// Loads all engine type categories from EngineTypes.yaml.
        /// </summary>
        public static List<EngineTypeCategory> LoadEngineTypes()
        {
            if (_engineTypes != null)
            {
                return _engineTypes;
            }

            _engineTypes = new List<EngineTypeCategory>();

            if (!Godot.FileAccess.FileExists(EngineTypesPath))
            {
                GD.PrintErr($"Engine types file not found: {EngineTypesPath}");
                return _engineTypes;
            }

            try
            {
                using var file = Godot.FileAccess.Open(EngineTypesPath, Godot.FileAccess.ModeFlags.Read);
                string yamlContent = file.GetAsText();

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();

                var yamlData = deserializer.Deserialize<SysDict>(yamlContent);

                if (yamlData != null && yamlData.ContainsKey("categories"))
                {
                    var categoriesList = yamlData["categories"] as List<object>;
                    if (categoriesList != null)
                    {
                        foreach (var categoryObj in categoriesList)
                        {
                            if (categoryObj is Dictionary<object, object> categoryDict)
                            {
                                var category = ParseEngineTypeCategory(categoryDict);
                                _engineTypes.Add(category);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr(
                    $"Error loading engine types from {EngineTypesPath}: {e.Message}\n{e.StackTrace}"
                );
            }

            return _engineTypes;
        }

        /// <summary>
        /// Loads all ship template categories from ShipTemplates.yaml.
        /// </summary>
        public static List<ShipTemplateCategory> LoadShipTemplates()
        {
            if (_shipTemplates != null)
            {
                return _shipTemplates;
            }

            _shipTemplates = new List<ShipTemplateCategory>();

            if (!Godot.FileAccess.FileExists(ShipTemplatesPath))
            {
                GD.PrintErr($"Ship templates file not found: {ShipTemplatesPath}");
                return _shipTemplates;
            }

            try
            {
                using var file = Godot.FileAccess.Open(ShipTemplatesPath, Godot.FileAccess.ModeFlags.Read);
                string yamlContent = file.GetAsText();

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();

                var yamlData = deserializer.Deserialize<SysDict>(yamlContent);

                if (yamlData != null && yamlData.ContainsKey("categories"))
                {
                    var categoriesList = yamlData["categories"] as List<object>;
                    if (categoriesList != null)
                    {
                        foreach (var categoryObj in categoriesList)
                        {
                            if (categoryObj is Dictionary<object, object> categoryDict)
                            {
                                var category = ParseShipTemplateCategory(categoryDict);
                                _shipTemplates.Add(category);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr(
                    $"Error loading ship templates from {ShipTemplatesPath}: {e.Message}\n{e.StackTrace}"
                );
            }

            return _shipTemplates;
        }

        /// <summary>
        /// Gets the list of all engine category names.
        /// </summary>
        public static List<string> GetEngineCategories()
        {
            var engineTypes = LoadEngineTypes();
            return engineTypes.Select(e => e.Name).ToList();
        }

        /// <summary>
        /// Gets the list of all ship template category names.
        /// </summary>
        public static List<string> GetShipTemplateCategories()
        {
            var shipTemplates = LoadShipTemplates();
            return shipTemplates.Select(s => s.Name).ToList();
        }

        /// <summary>
        /// Loads all engine definitions from a specific category file.
        /// </summary>
        /// <param name="categoryName">The category name (e.g., "Chemical", "Electric", "Nuclear", "Fusion")</param>
        public static List<EngineDefinition> LoadEnginesByCategory(string categoryName)
        {
            if (_enginesByCategory == null)
            {
                _enginesByCategory = new Dictionary<string, List<EngineDefinition>>();
            }

            // Normalize the category name for lookup
            string normalizedName = categoryName.Replace(" ", "_");
            if (_enginesByCategory.ContainsKey(normalizedName))
            {
                return _enginesByCategory[normalizedName];
            }

            var engines = new List<EngineDefinition>();
            string filePath = EnginesDirectory + normalizedName + ".yaml";

            if (!Godot.FileAccess.FileExists(filePath))
            {
                GD.PrintErr($"Engine category file not found: {filePath}");
                _enginesByCategory[normalizedName] = engines;
                return engines;
            }

            try
            {
                using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
                string yamlContent = file.GetAsText();

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();

                var yamlData = deserializer.Deserialize<SysDict>(yamlContent);

                if (yamlData != null && yamlData.ContainsKey("engines"))
                {
                    var enginesList = yamlData["engines"] as List<object>;
                    if (enginesList != null)
                    {
                        foreach (var engineObj in enginesList)
                        {
                            if (engineObj is Dictionary<object, object> engineDict)
                            {
                                var engine = ParseEngineDefinition(engineDict);
                                engines.Add(engine);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr(
                    $"Error loading engines from {filePath}: {e.Message}\n{e.StackTrace}"
                );
            }

            _enginesByCategory[normalizedName] = engines;
            return engines;
        }

        /// <summary>
        /// Loads all ship definitions from a specific template file.
        /// </summary>
        /// <param name="categoryName">The template name (e.g., "Cargo_Freighter", "Fast_Courier", "Heavy_Transport")</param>
        public static List<ShipDefinition> LoadShipsByCategory(string categoryName)
        {
            if (_shipsByCategory == null)
            {
                _shipsByCategory = new Dictionary<string, List<ShipDefinition>>();
            }

            // Normalize the category name for lookup
            string normalizedName = categoryName.Replace(" ", "_");
            if (_shipsByCategory.ContainsKey(normalizedName))
            {
                return _shipsByCategory[normalizedName];
            }

            var ships = new List<ShipDefinition>();
            string filePath = ShipsDirectory + normalizedName + ".yaml";

            if (!Godot.FileAccess.FileExists(filePath))
            {
                GD.PrintErr($"Ship template file not found: {filePath}");
                _shipsByCategory[normalizedName] = ships;
                return ships;
            }

            try
            {
                using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
                string yamlContent = file.GetAsText();

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();

                var yamlData = deserializer.Deserialize<SysDict>(yamlContent);

                if (yamlData != null && yamlData.ContainsKey("ships"))
                {
                    var shipsList = yamlData["ships"] as List<object>;
                    if (shipsList != null)
                    {
                        foreach (var shipObj in shipsList)
                        {
                            if (shipObj is Dictionary<object, object> shipDict)
                            {
                                var ship = ParseShipDefinition(shipDict);
                                ships.Add(ship);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr(
                    $"Error loading ships from {filePath}: {e.Message}\n{e.StackTrace}"
                );
            }

            _shipsByCategory[normalizedName] = ships;
            return ships;
        }

        /// <summary>
        /// Loads all engines from all categories.
        /// </summary>
        public static List<EngineDefinition> LoadAllEngines()
        {
            var allEngines = new List<EngineDefinition>();
            var categories = GetEngineCategories();

            foreach (var category in categories)
            {
                var engines = LoadEnginesByCategory(category);
                allEngines.AddRange(engines);
            }

            return allEngines;
        }

        /// <summary>
        /// Loads all ships from all categories.
        /// </summary>
        public static List<ShipDefinition> LoadAllShips()
        {
            var allShips = new List<ShipDefinition>();
            var categories = GetShipTemplateCategories();

            foreach (var category in categories)
            {
                var ships = LoadShipsByCategory(category);
                allShips.AddRange(ships);
            }

            return allShips;
        }

        /// <summary>
        /// Loads all station template categories from StationTemplates.yaml.
        /// </summary>
        public static List<StationTemplateCategory> LoadStationTemplates()
        {
            if (_stationTemplates != null)
                return _stationTemplates;

            _stationTemplates = new List<StationTemplateCategory>();

            if (!Godot.FileAccess.FileExists(StationTemplatesPath))
            {
                GD.PrintErr($"Station templates file not found: {StationTemplatesPath}");
                return _stationTemplates;
            }

            try
            {
                using var file = Godot.FileAccess.Open(StationTemplatesPath, Godot.FileAccess.ModeFlags.Read);
                string yamlContent = file.GetAsText();

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();

                var yamlData = deserializer.Deserialize<SysDict>(yamlContent);

                if (yamlData != null && yamlData.ContainsKey("categories"))
                {
                    var categoriesList = yamlData["categories"] as List<object>;
                    if (categoriesList != null)
                    {
                        foreach (var categoryObj in categoriesList)
                        {
                            if (categoryObj is Dictionary<object, object> categoryDict)
                            {
                                var category = new StationTemplateCategory
                                {
                                    Name = ReadString(categoryDict, "name", ""),
                                    Description = ReadString(categoryDict, "description", ""),
                                };
                                _stationTemplates.Add(category);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"Error loading station templates from {StationTemplatesPath}: {e.Message}\n{e.StackTrace}");
            }

            return _stationTemplates;
        }

        /// <summary>
        /// Gets the list of all station template category names.
        /// </summary>
        public static List<string> GetStationTemplateCategories()
        {
            var stationTemplates = LoadStationTemplates();
            return stationTemplates.Select(s => s.Name).ToList();
        }

        /// <summary>
        /// Loads all station definitions from a specific category file.
        /// </summary>
        public static List<StationDefinition> LoadStationsByCategory(string categoryName)
        {
            if (_stationsByCategory == null)
                _stationsByCategory = new Dictionary<string, List<StationDefinition>>();

            string normalizedName = categoryName.Replace(" ", "_");
            if (_stationsByCategory.ContainsKey(normalizedName))
                return _stationsByCategory[normalizedName];

            var stations = new List<StationDefinition>();
            string filePath = StationsDirectory + normalizedName + ".yaml";

            if (!Godot.FileAccess.FileExists(filePath))
            {
                GD.PrintErr($"Station category file not found: {filePath}");
                _stationsByCategory[normalizedName] = stations;
                return stations;
            }

            try
            {
                using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
                string yamlContent = file.GetAsText();

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();

                var yamlData = deserializer.Deserialize<SysDict>(yamlContent);

                if (yamlData != null && yamlData.ContainsKey("stations"))
                {
                    var stationsList = yamlData["stations"] as List<object>;
                    if (stationsList != null)
                    {
                        foreach (var stationObj in stationsList)
                        {
                            if (stationObj is Dictionary<object, object> stationDict)
                            {
                                var station = ParseStationDefinition(stationDict);
                                stations.Add(station);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"Error loading stations from {filePath}: {e.Message}\n{e.StackTrace}");
            }

            _stationsByCategory[normalizedName] = stations;
            return stations;
        }

        /// <summary>
        /// Loads all stations from all categories.
        /// </summary>
        public static List<StationDefinition> LoadAllStations()
        {
            var allStations = new List<StationDefinition>();
            var categories = GetStationTemplateCategories();

            foreach (var category in categories)
            {
                var stations = LoadStationsByCategory(category);
                allStations.AddRange(stations);
            }

            return allStations;
        }

        /// <summary>
        /// Gets a station definition by name, searching all categories.
        /// </summary>
        public static StationDefinition? GetStationDefinition(string name)
        {
            var allStations = LoadAllStations();
            foreach (var station in allStations)
            {
                if (string.Equals(station.Name, name, StringComparison.OrdinalIgnoreCase))
                    return station;
            }
            return null;
        }

        /// <summary>
        /// Gets a ship definition by name, searching all categories.
        /// </summary>
        public static ShipDefinition? GetShipDefinition(string name)
        {
            var allShips = LoadAllShips();
            foreach (var ship in allShips)
            {
                if (string.Equals(ship.Name, name, StringComparison.OrdinalIgnoreCase))
                    return ship;
            }
            return null;
        }

        /// <summary>
        /// Gets an engine type category by name, or null if not found.
        /// </summary>
        public static EngineTypeCategory? GetEngineType(string name)
        {
            var engineTypes = LoadEngineTypes();
            foreach (var category in engineTypes)
            {
                if (string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return category;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a ship template category by name, or null if not found.
        /// </summary>
        public static ShipTemplateCategory? GetShipTemplate(string name)
        {
            var shipTemplates = LoadShipTemplates();
            foreach (var category in shipTemplates)
            {
                if (string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return category;
                }
            }
            return null;
        }

        /// <summary>
        /// Clears the cached data to force reload on next access.
        /// </summary>
        public static void ClearCache()
        {
            _engineTypes = null;
            _shipTemplates = null;
            _stationTemplates = null;
            _enginesByCategory = null;
            _shipsByCategory = null;
            _stationsByCategory = null;
        }

        private static EngineTypeCategory ParseEngineTypeCategory(Dictionary<object, object> dict)
        {
            return new EngineTypeCategory
            {
                Name = ReadString(dict, "name", ""),
                Description = ReadString(dict, "description", ""),
            };
        }

        private static ShipTemplateCategory ParseShipTemplateCategory(Dictionary<object, object> dict)
        {
            return new ShipTemplateCategory
            {
                Name = ReadString(dict, "name", ""),
                Description = ReadString(dict, "description", ""),
            };
        }

        private static EngineDefinition ParseEngineDefinition(Dictionary<object, object> dict)
        {
            return new EngineDefinition
            {
                Name = ReadString(dict, "name", ""),
                SpecificImpulse = ReadFloat(dict, "specific_impulse", 0f),
                Thrust = ReadFloat(dict, "thrust", 0f),
                Description = ReadString(dict, "description", ""),
            };
        }

        private static ShipDefinition ParseShipDefinition(Dictionary<object, object> dict)
        {
            var ship = new ShipDefinition
            {
                Name = ReadString(dict, "name", ""),
                DryMass = ReadFloat(dict, "dry_mass", 0f),
                CargoCapacity = ReadFloat(dict, "cargo_capacity", 0f),
                FuelCapacity = ReadFloat(dict, "fuel_capacity", 0f),
                EngineCategory = ReadString(dict, "engine_category", ""),
                ConstructionTime = ReadFloat(dict, "construction_time", 0f),
                RequiredResources = ReadResourceDict(dict, "required_resources"),
            };
            return ship;
        }

        private static StationDefinition ParseStationDefinition(Dictionary<object, object> dict)
        {
            return new StationDefinition
            {
                Name = ReadString(dict, "name", ""),
                StationType = ReadString(dict, "station_type", ""),
                ConstructionTime = ReadFloat(dict, "construction_time", 0f),
                CanBuildShips = ReadBool(dict, "can_build_ships", false),
                RequiredResources = ReadResourceDict(dict, "required_resources"),
            };
        }

        private static string ReadString(Dictionary<object, object> dict, string key, string fallback)
        {
            if (!dict.ContainsKey(key))
            {
                return fallback;
            }

            var value = dict[key];
            if (value is string s)
            {
                return s;
            }

            return value?.ToString() ?? fallback;
        }

        private static float ReadFloat(Dictionary<object, object> dict, string key, float fallback)
        {
            if (!dict.ContainsKey(key))
            {
                return fallback;
            }

            return NodeToFloat(dict[key], fallback);
        }

        private static bool ReadBool(Dictionary<object, object> dict, string key, bool fallback)
        {
            if (!dict.ContainsKey(key))
                return fallback;

            var value = dict[key];
            if (value is bool b)
                return b;
            if (value is string s)
                return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
            return fallback;
        }

        private static Dictionary<string, int> ReadResourceDict(Dictionary<object, object> dict, string key)
        {
            var result = new Dictionary<string, int>();
            if (!dict.ContainsKey(key))
                return result;

            if (dict[key] is Dictionary<object, object> resourceDict)
            {
                foreach (var kvp in resourceDict)
                {
                    string resourceName = kvp.Key?.ToString() ?? "";
                    int amount = (int)NodeToFloat(kvp.Value, 0f);
                    if (!string.IsNullOrEmpty(resourceName) && amount > 0)
                        result[resourceName] = amount;
                }
            }

            return result;
        }

        private static float NodeToFloat(object node, float fallback)
        {
            try
            {
                if (node is long l)
                    return (float)l;
                if (node is double d)
                    return (float)d;
                if (node is float f)
                    return f;
                if (node is int i)
                    return (float)i;

                var s = node?.ToString();
                if (float.TryParse(s, out var v))
                    return v;
            }
            catch (Exception e)
            {
                GD.PrintErr($"Error parsing node to float: {e.Message}");
            }

            return fallback;
        }
    }
}
