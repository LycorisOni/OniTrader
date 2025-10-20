using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using System.Reflection;
using Path = System.IO.Path;
using System.Text.Encodings;
using System.IO;
using System.Security.Cryptography;
using SPTarkov.Server.Core.Utils.Cloners;

namespace _13AddTraderWithAssortJson;

// This record holds the various properties for your mod
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

        // Future Production Method
        //EditHideout();

        // lets write a nice log message to the server console so players know our mod has made changes
        logger.Success("Removed the Flea Hooray!");

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
        TimeUtil timeUtil,
        TraderHelper
            traderHelper // This is a custom class we add for this mod, we made it injectable so it can be accessed like other classes here
    )
        : IOnLoad
    {
        private readonly TraderConfig _traderConfig = configServer.GetConfig<TraderConfig>();
        private readonly RagfairConfig _ragfairConfig = configServer.GetConfig<RagfairConfig>();


        public Task OnLoad()
        {
            // A path to the mods files we use below
            var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

            // A relative path to the trader icon to show
            var traderImagePath = Path.Combine(pathToMod, "data/oni.jpg");

            // The base json containing trader settings we will add to the server
            var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(pathToMod, "data/base.json");

            // Create a helper class and use it to register our traders image/icon + set its stock refresh time
            imageRouter.AddRoute(traderBase.Avatar.Replace(".jpg", ""), traderImagePath);
            traderHelper.SetTraderUpdateTime(_traderConfig, traderBase, timeUtil.GetHoursAsSeconds(1),
                timeUtil.GetHoursAsSeconds(2));

            _ragfairConfig.Traders.TryAdd(traderBase.Id, true);

            traderHelper.AddTraderWithEmptyAssortToDb(traderBase);

            // Add localisation text for our trader to the database so it shows to people playing in different languages
            traderHelper.AddTraderToLocales(traderBase, "Oni", "This is my shop.");

            // Get the assort data from JSON
            var assort = modHelper.GetJsonDataFromFile<TraderAssort>(pathToMod, "data/assort.json");
            // Save the data we loaded above into the trader we've made
            traderHelper.OverwriteTraderAssort(traderBase.Id, assort);

            logger.Success("Loaded my Trader Oni!");
            // Send back a success to the server to say our trader is good to go
            return Task.CompletedTask;
        }
    }
}