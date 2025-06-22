"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.mod = void 0;
const ConfigTypes_1 = require("C:/snapshot/project/obj/models/enums/ConfigTypes");
const Traders_1 = require("C:/snapshot/project/obj/models/enums/Traders");
// Settings for my trader
const baseJson = require("../db/base.json");
const assortJson = require("../db/assort.json");
const traderHelpers_1 = require("./traderHelpers");
class Oni {
    mod;
    traderImgPath;
    logger;
    traderHelper;
    constructor() {
        this.mod = "Oni"; // Set name to folder
        this.traderImgPath = "res/oni.jpg"; // Set to image path
    }
    preSptLoad(container) {
        this.logger = container.resolve("WinstonLogger");
        this.logger.debug(`[${this.mod}] preSpt Loading... `);
        // Spt code fetch to load
        const preSptModLoader = container.resolve("PreSptModLoader");
        const imageRouter = container.resolve("ImageRouter");
        const configServer = container.resolve("ConfigServer");
        const traderConfig = configServer.getConfig(ConfigTypes_1.ConfigTypes.TRADER);
        const ragfairConfig = configServer.getConfig(ConfigTypes_1.ConfigTypes.RAGFAIR);
        this.traderHelper = new traderHelpers_1.TraderHelper();
        imageRouter.addRoute(baseJson.avatar.replace(".jpg", ""), `${preSptModLoader.getModPath(this.mod)}${this.traderImgPath}`);
        this.traderHelper.setTraderUpdateTime(traderConfig, baseJson, 3600, 4000);
        Traders_1.Traders[baseJson._id] = baseJson._id;
        ragfairConfig.traders[baseJson._id] = true;
        this.logger.debug(`[${this.mod}] preSpt Loaded`);
    }
    postDBLoad(container) {
        this.logger.debug(`[${this.mod}] postDb Loading... `);
        const databaseServer = container.resolve("DatabaseServer");
        const jsonUtil = container.resolve("JsonUtil");
        const tables = databaseServer.getTables();
        tables.globals.config.RagFair.minUserLevel = 99;
        this.traderHelper.addTraderToDb(baseJson, tables, jsonUtil, assortJson);
        this.traderHelper.addTraderToLocales(baseJson, tables, baseJson.name, "Oni", baseJson.nickname, baseJson.location, "The Lycoris Fields");
        this.logger.debug(`[${this.mod}] postDb Loaded`);
    }
}
exports.mod = new Oni();
//# sourceMappingURL=mod.js.map