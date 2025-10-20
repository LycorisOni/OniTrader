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
using SPTarkov.Server.Core.Services;
using System.Reflection;
using System.Security.Cryptography;
using Path = System.IO.Path;

namespace LycorisOni.Trader;

// My package.json lol
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
    public override bool? IsBundleMod { get; init; } = false;
    public override string? License { get; init; } = "MIT";
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
        // Inform server we have finished
        return Task.CompletedTask;
    }

    private void EditGlobals()
    {
        // Let's edit settings in the GLOBALS file (database/globals.json)
        var globals = databaseService.GetGlobals();

        // Now lets try editing the ragfair unlock level, lets get the ragfair settings first
        var ragfairSettings = globals.Configuration.RagFair;

        // Lets set the level you need to be to access flea to be 1
        ragfairSettings.MinUserLevel = 93;
    }

    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
    public class AddTraderWithAssortJson(
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
            // Grabs the path to my mod to get my class functional
            var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

            // Special file check sum to insure no dirty .json tampering has happened.
            if (!VerifyFileHashes(pathToMod))
            {
                logger.Error("You failed my hashcheck pray for forgiveness.");
                return Task.FromException(new Exception());
            }

            // Method to get my CustomProduction in.
            ProductionLoader(pathToMod);

            // A relative path to the trader icon to show
            var traderImagePath = Path.Combine(pathToMod, "data/oni.jpg");

            // Yeets my base.json data in
            var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(pathToMod, "data/base.json");

            // Dark sorcery to set up a helper class and get it setting up my image for Oni along with the stock refresh timer
            imageRouter.AddRoute(traderBase.Avatar.Replace(".jpg", ""), traderImagePath);
            traderHelper.SetTraderUpdateTime(_traderConfig, traderBase, timeUtil.GetHoursAsSeconds(1),
                timeUtil.GetHoursAsSeconds(2));

            _ragfairConfig.Traders.TryAdd(traderBase.Id, true);

            traderHelper.AddTraderWithEmptyAssortToDb(traderBase);

            // Add localisation text for my trader to the database so it shows to people playing in different languages
            traderHelper.AddTraderToLocales(traderBase, "Oni", "This is my shop.");

            // Yeets my assort data in
            var assort = modHelper.GetJsonDataFromFile<TraderAssort>(pathToMod, "data/assort.json");
            // Saves the data of my Trader
            traderHelper.OverwriteTraderAssort(traderBase.Id, assort);

            logger.Success("🌸 Loaded OniTrader Successfully! 🌸");
            // Yeets that log to show mod is gucci
            return Task.CompletedTask;
        }

        private void ProductionLoader(string modPath)
        {
            try
            {
                // Yoinks the hideout data so I can yeet my production.json in
                var hideout = databaseService.GetHideout();
                //If the install is somehow corrupted sends a message
                if (hideout.Production.Recipes == null)
                {
                    logger.Error("No hideout productions were found. Failed to load new ones.");
                    return;
                }

                // Loads in my Production.json's Productions like a good little command.
                var customProductions = modHelper.GetJsonDataFromFile<List<HideoutProduction>>(modPath, "data/production.json");

                if (customProductions != null && customProductions.Count > 0)
                {
                    // Adds in my productions in order. Should..
                    foreach (var production in customProductions)
                    {
                        hideout.Production.Recipes.Add(production);
                    }

                    logger.Success($"OniTrader productions loaded! {customProductions.Count}");
                }
            }
            catch (Exception)
            {
                logger.Warning($"Found no productions to load.. Did you remove my file???");
            }
        }

        private bool VerifyFileHashes(string modPath)
        {
            var filesToCheck = new[]
            {
                new { RelativePath = "data/assort.json", ExpectedHash = "b7ad2b4bf069fe9041fa6424d2aaa432", Name = "assort.json" },
                new { RelativePath = "data/base.json", ExpectedHash = "520f5c5170ee5eb6489ec1d5f75c8371", Name = "base.json" },
                //new { RelativePath = "data/production.json", ExpectedHash = "", Name = "production.json" }
            };

            var failedFiles = new List<string>();

            foreach (var file in filesToCheck)
            {
                var fullPath = Path.Combine(modPath, file.RelativePath);
                
                if (!File.Exists(fullPath))
                {
                    logger.Error($"File failed to be loaded? Get the file back in there!: {file.RelativePath}");
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
                    logger.Error($"Failed to find the correct hash>:( {file.RelativePath}: {ex.Message}");
                    failedFiles.Add(file.Name);
                }
            }

            if (failedFiles.Count > 0)
            {
                var fileList = failedFiles.Count == 2 ? "both my .jsons" : $"my {failedFiles[0]}";
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
