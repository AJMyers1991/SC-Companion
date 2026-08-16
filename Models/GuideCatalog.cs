namespace SCCompanion.Models;

public static class GuideCatalog
{
    public const string BaseUrl =
        "https://frontiergaming.org/assets/SC-Companion/one-page-guides/";

    public static IReadOnlyList<GuideFolderItem> CreateFolders()
    {
        return
        [
            new GuideFolderItem(
                "One Page Guides",
                [
                    new GuideDefinition(
                        "Hathor",
                        ["Hathor-OPG-MrKraken.png"],
                        "Provided by MrKraken",
                        "https://robertsspaceindustries.com/en/citizens/MrKraken"),
                    new GuideDefinition(
                        "Rock Breaker",
                        [
                            "Rock-Breaker-OPG-MrKraken/Rock-Breaker-OPG-Map1.png",
                            "Rock-Breaker-OPG-MrKraken/Rock-Breaker-OPG-Map2.png",
                            "Rock-Breaker-OPG-MrKraken/Rock-Breaker-OPG-Map3.png",
                            "Rock-Breaker-OPG-MrKraken/Rock-Breaker-OPG-p1.png",
                            "Rock-Breaker-OPG-MrKraken/Rock-Breaker-OPG-p2.png"
                        ],
                        "Provided by MrKraken",
                        "https://robertsspaceindustries.com/en/citizens/MrKraken"),
                    new GuideDefinition(
                        "Stormbreaker",
                        [
                            "Stormbreaker-OPG-MrKraken/Farro-Datacenter-Map.png",
                            "Stormbreaker-OPG-MrKraken/Lazarus-Complex-Map.png",
                            "Stormbreaker-OPG-MrKraken/StormbreakerOPG.png"
                        ],
                        "Provided by MrKraken",
                        "https://robertsspaceindustries.com/en/citizens/MrKraken"),
                    new GuideDefinition(
                        "Tactical Strike Groups",
                        ["TSG-OPG-MrKraken.png"],
                        "Provided by MrKraken",
                        "https://robertsspaceindustries.com/en/citizens/MrKraken"),
                    new GuideDefinition(
                        "Vanduul Tech Smugglers",
                        ["Vanduul-Tech-Smugglers-OPG-MrKraken.png"],
                        "Provided by MrKraken",
                        "https://robertsspaceindustries.com/en/citizens/MrKraken")
                ]),
            new GuideFolderItem(
                "Maps",
                [
                    new GuideDefinition(
                        "Pyro",
                        [
                            "Maps/Checkmate-CZ.webp",
                            "Maps/Orbituary-CZ.webp",
                            "Maps/PYAM-EXHANG.webp",
                            "Maps/PYAM-SUPVISR.webp",
                            "Maps/Ruin-CZ.webp"
                        ],
                        "Provided by Kerast on Reddit",
                        "https://www.reddit.com/user/Kerast/")
                ])
        ];
    }
}
