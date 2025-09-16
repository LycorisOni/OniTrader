import { DependencyContainer } from "tsyringe";

// SPT types
import { IPreSptLoadMod } from "@spt/models/external/IPreSptLoadMod";
import { IPostDBLoadMod } from "@spt/models/external/IPostDBLoadMod";
import { ILogger } from "@spt/models/spt/utils/ILogger";
import { PreSptModLoader } from "@spt/loaders/PreSptModLoader";
import { DatabaseServer } from "@spt/servers/DatabaseServer";
import { ImageRouter } from "@spt/routers/ImageRouter";
import { ConfigServer } from "@spt/servers/ConfigServer";
import { ConfigTypes } from "@spt/models/enums/ConfigTypes";
import { ITraderConfig } from "@spt/models/spt/config/ITraderConfig";
import { IRagfairConfig } from "@spt/models/spt/config/IRagfairConfig";
import { JsonUtil } from "@spt/utils/JsonUtil";
import { Traders } from "@spt/models/enums/Traders";
import { createHash } from 'crypto';
import { readFileSync } from 'fs';
// Settings for my trader
import baseJson = require("../db/base.json");
import assortJson = require("../db/assort.json");

import { TraderHelper } from "./traderHelpers";


class Oni implements IPreSptLoadMod, IPostDBLoadMod
{
    private mod: string;
    private traderImgPath: string;
    private logger: ILogger;
    private traderHelper: TraderHelper;

    constructor() {
        this.mod = "Oni"; // Set name to folder
        this.traderImgPath = "res/oni.jpg"; // Set to image path
    }

    public preSptLoad(container: DependencyContainer): void
    {
        this.logger = container.resolve<ILogger>("WinstonLogger");
        this.logger.debug(`[${this.mod}] preSpt Loading... `);

        // Spt code fetch to load
        const preSptModLoader: PreSptModLoader = container.resolve<PreSptModLoader>("PreSptModLoader");
        const imageRouter: ImageRouter = container.resolve<ImageRouter>("ImageRouter");
        const configServer = container.resolve<ConfigServer>("ConfigServer");
        const traderConfig: ITraderConfig = configServer.getConfig<ITraderConfig>(ConfigTypes.TRADER);
        const ragfairConfig = configServer.getConfig<IRagfairConfig>(ConfigTypes.RAGFAIR);

        this.traderHelper = new TraderHelper();
        imageRouter.addRoute(baseJson.avatar.replace(".jpg", ""), `${preSptModLoader.getModPath(this.mod)}${this.traderImgPath}`);
        this.traderHelper.setTraderUpdateTime(traderConfig, baseJson, 3600, 4000);

        Traders[baseJson._id] = baseJson._id;

        ragfairConfig.traders[baseJson._id] = true;

        this.logger.debug(`[${this.mod}] preSpt Loaded`);
    }

    public postDBLoad(container: DependencyContainer): void
    {
        this.logger.debug(`[${this.mod}] postDb Loading... `);

        const databaseServer: DatabaseServer = container.resolve<DatabaseServer>("DatabaseServer");
        const jsonUtil: JsonUtil = container.resolve<JsonUtil>("JsonUtil");
        const tables = databaseServer.getTables();
       
        tables.globals.config.RagFair.minUserLevel = 99;

        this.traderHelper.addTraderToDb(baseJson, tables, jsonUtil, assortJson);
        this.traderHelper.addTraderToLocales(baseJson, tables, baseJson.name, "Oni", baseJson.nickname, baseJson.location, "The Lycoris Fields");
        
        this.logger.debug(`[${this.mod}] postDb Loaded`);
    }
}
const filePath = 'user/mods/Oni/db/base.json';
const md5Hash = createHash('md5').update(readFileSync(filePath)).digest('hex');
if (md5Hash !== "daadd4f2740ad2d3b1f7d4add8629c42") {
    throw new Error("You touched my .json go undo what you did to load my trader.");}
export const mod = new Oni();
