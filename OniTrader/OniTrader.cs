using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Services;
using System.Reflection;
using System.Security.Cryptography;
using Path = System.IO.Path;

namespace LycorisOni.Trader;

// My package.json heheheh
public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.lycorisoni.onitrader";
    public override string Name { get; init; } = "OniTrader";
    public override string Author { get; init; } = "LycorisOni";
    public override List<string>? Contributors { get; init; } = null;
    public override SemanticVersioning.Version Version { get; init; } = new("0.8.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; } = null;
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = null;
    public override string? Url { get; init; } = null;
    public override bool? IsBundleMod { get; init; } = true;
    public override string? License { get; init; } = "MIT";
}

public record FileCheckerData
{
    public required string RelativePath { get; set; }
    public required string ExpectedHash { get; set; }
    public required string Name { get; set; }
}
    
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class EditDatabaseValues(
    ISptLogger<EditDatabaseValues> logger,
    DatabaseService databaseService)
    : IOnLoad
{
    public Task OnLoad()
    {
        // Allows my flea adjustment
        EditGlobals();
        // Cutesy first message
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("🌸 Removed the Flea Hooray! 🌸");
        Console.ResetColor();

        // Finished up
        return Task.CompletedTask;
    }

    private void EditGlobals()
    {
        // Let's me set up my flea level
        var globals = databaseService.GetGlobals();

        // Is grabbing flea level
        var ragfairSettings = globals.Configuration.RagFair;

        // Sets my flea level.
        ragfairSettings.MinUserLevel = 93;
    }

    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 3)]
    public class OniTrader(
        
        ISptLogger<EditDatabaseValues> logger,
        ModHelper modHelper,
        ImageRouter imageRouter,
        ConfigServer configServer,
        DatabaseService databaseService,
        TimeUtil timeUtil,
        TraderHelper traderHelper 
    )
        : IOnLoad
    {
        private readonly TraderConfig _traderConfig = configServer.GetConfig<TraderConfig>();
        private readonly RagfairConfig _ragfairConfig = configServer.GetConfig<RagfairConfig>();


        public Task OnLoad()
        {
            // Yoinks my paths to make shit functional
            var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

            // Critical check.
            if (!VerifyFileHashes(pathToMod))
            {
                logger.Error("You failed my hashfile check. Pray for forgiveness.");
                return Task.FromException(new Exception());
            }

            // Load custom production recipes
            LoadCustomProduction(pathToMod);

            // A relative path to the trader icon to show
            var traderImagePath = Path.Combine(pathToMod, "data/oni.jpg");

            // My Base.json loader.
            var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(pathToMod, "data/base.json");

            // Wizard magic. but it just sets my trader image and my restock timer.
            imageRouter.AddRoute(traderBase.Avatar.Replace(".jpg", ""), traderImagePath);
            traderHelper.SetTraderUpdateTime(_traderConfig, traderBase, timeUtil.GetHoursAsSeconds(1),
                timeUtil.GetHoursAsSeconds(2));

            _ragfairConfig.Traders.TryAdd(traderBase.Id, true);

            traderHelper.AddTraderWithEmptyAssortToDb(traderBase);

            // Localization fail back.
            traderHelper.AddTraderToLocales(traderBase, "Oni", "This is my shop.");

            // Get the assort data from JSON
            var assort = modHelper.GetJsonDataFromFile<TraderAssort>(pathToMod, "data/assort.json");
            // Cheeky save.
            traderHelper.OverwriteTraderAssort(traderBase.Id, assort);

            // Other cutesy message. Don't yell at me.
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("🌸 Loaded OniTrader Successfully! 🌸");
            Console.ResetColor();
            
            // Good to go once this clears.
            return Task.CompletedTask;
        }

        private void LoadCustomProduction(string modPath)
        {
            try
            {
                var productionPath = Path.Combine(modPath, "data/production.json");
                
                // Checks for my file.
                if (!File.Exists(productionPath))
                {
                    logger.Info("No custom production.json found, skipping production loading.");
                    return;
                }

                logger.Info("Found production.json, attempting to load...");

                // Yoinks the production.json to allow production.json of my own to load.
                var hideout = databaseService.GetHideout();
               
                // Loads my production.json to load.
                var customProductions = modHelper.GetJsonDataFromFile<List<HideoutProduction>>(modPath, "data/production.json");
                
                if (customProductions.Count > 0)
                {
                    // Adds up all the productions.
                    foreach (var production in customProductions)
                    {
                        hideout.Production.Recipes.Add(production);
                        logger.Info($"Added production: {production.Id}");
                    }
                    // If recipes are found sends this.
                    logger.Success($"Successfully loaded {customProductions.Count} custom production recipe(s)!");
                }
                // If no recipes is found sends this.
                else
                {
                    logger.Warning("production.json exists but contains no recipes.");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error loading custom production.json: {ex.Message}");
                logger.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        private bool VerifyFileHashes(string modPath)
        {
            var filesToCheck = new List<FileCheckerData>();

            var assortFileChecksum = new FileCheckerData
            {
                RelativePath = "data/assort.json",
                ExpectedHash = "1e9233702f30f9c9682a3f6de617e2f9",
                Name = "assort.json"
            };
            filesToCheck.Add(assortFileChecksum);

            var baseFileChecksum = new FileCheckerData
            {
                RelativePath = "data/base.json",
                ExpectedHash = "18edb20ddb62bfcb99bef44b0d445341",
                Name = "base.json"
            };
            filesToCheck.Add(baseFileChecksum);
            
            var productionFileChecksum = new FileCheckerData
            {
                RelativePath = "data/production.json",
                ExpectedHash = "adeed155bae0b0b4ef9ddd25f55081e7",
                Name = "production.json"
            };
            filesToCheck.Add(productionFileChecksum);

            var failedFiles = new List<string>();

            foreach (var file in filesToCheck)
            {
                var fullPath = Path.Combine(modPath, file.RelativePath);
                
                if (!File.Exists(fullPath))
                {
                    logger.Error($"File not found: {file.RelativePath}");
                    failedFiles.Add(file.Name);
                    continue;
                }

                try
                {
                    var actualHash = ComputeMD5Hash(fullPath);
                    
                    if (!actualHash.Equals(file.ExpectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        failedFiles.Add(file.Name);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"Error checking hash for {file.RelativePath}: {ex.Message}");
                    failedFiles.Add(file.Name);
                }
            }
            // Main Logger for filechecksum. 
            if (failedFiles.Count > 0)
            {
                var fileList = failedFiles.Count == 2 ? "my .jsons" : $"my {failedFiles[0]}";
                logger.Error($"OniTrader! You messed with {fileList} undo what you did to load my Trader!");
                return false;
            }

            return true;
        }

        private static string ComputeMD5Hash(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 4)]
public class Oni(
    WTTServerCommonLib.WTTServerCommonLib wttCommon
) : IOnLoad
{
    public async Task OnLoad()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        
        // Use WTT-CommonLib services
        await wttCommon.CustomQuestService.CreateCustomQuests(assembly);
        await wttCommon.CustomQuestZoneService.CreateCustomQuestZones(assembly, 
            Path.Join("ZoneData", "QuestZones"));
        await Task.CompletedTask;
    }
}
