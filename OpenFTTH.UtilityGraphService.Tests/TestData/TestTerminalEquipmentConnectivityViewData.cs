using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Tests.TestData
{
    public static class TestTerminalEquipmentConnectivityViewData
    {
        /*
        public static TerminalEquipmentAZConnectivityViewModel LISAODFRack()
        {
            // Create rack with 80 LISA trays places in two holder, whith a splitter panel inbetween
            return new TerminalEquipmentAZConnectivityViewModel(
                terminalEquipments: new TerminalEquipmentAZConnectivityViewEquipmentInfo[]
                {
                    new TerminalEquipmentAZConnectivityViewEquipmentInfo(
                        id: Guid.NewGuid(),
                        category: "Subrack",
                        name: "Coupler hylde",
                        specName: "LGX Bæreramme",
                        terminalStructures: new TerminalEquipmentAZConnectivityViewTerminalStructureInfo[]
                        {
                            CreateCouplerType1("1", "WDM Coupler CommScope Type 1"),
                        }
                    )
                    {
                        ParentNodeStructureId = Guid.Parse("842a9bac-d9d8-4065-b182-6451ab3296b8")
                    }
                    ,
                    new TerminalEquipmentAZConnectivityViewEquipmentInfo(
                        id: Guid.NewGuid(),
                        category: "Subrack",
                        name: "Holder 1",
                        specName: "LISA Kassetteholder",
                        terminalStructures: new TerminalEquipmentAZConnectivityViewTerminalStructureInfo[]
                        {
                            CreateLISATray("1", "LISATray 24 x LC/APC"),
                            CreateLISATray("2", "LISATray 24 x LC/APC"),
                            CreateLISATray("3", "LISATray 24 x LC/APC"),
                            CreateLISATray("4", "LISATray 24 x LC/APC"),
                            CreateLISATray("5", "LISATray 24 x LC/APC"),
                            CreateLISATray("6", "LISATray 24 x LC/APC"),
                            CreateLISATray("7", "LISATray 24 x LC/APC"),
                            CreateLISATray("8", "LISATray 24 x LC/APC"),
                            CreateLISATray("9", "LISATray 24 x LC/APC"),
                            CreateLISATray("10", "LISATray 24 x LC/APC"),
                            CreateLISATray("11", "LISATray 24 x LC/APC"),
                            CreateLISATray("12", "LISATray 24 x LC/APC"),
                            CreateLISATray("13", "LISATray 24 x LC/APC"),
                            CreateLISATray("14", "LISATray 24 x LC/APC"),
                            CreateLISATray("15", "LISATray 24 x LC/APC"),
                            CreateLISATray("16", "LISATray 24 x LC/APC"),
                            CreateLISATray("17", "LISATray 24 x LC/APC"),
                            CreateLISATray("18", "LISATray 24 x LC/APC"),
                            CreateLISATray("19", "LISATray 24 x LC/APC"),
                            CreateLISATray("20", "LISATray 24 x LC/APC"),
                            CreateLISATray("21", "LISATray 24 x LC/APC"),
                            CreateLISATray("22", "LISATray 24 x LC/APC"),
                            CreateLISATray("23", "LISATray 24 x LC/APC"),
                            CreateLISATray("24", "LISATray 24 x LC/APC"),
                            CreateLISATray("25", "LISATray 24 x LC/APC"),
                            CreateLISATray("26", "LISATray 24 x LC/APC"),
                            CreateLISATray("27", "LISATray 24 x LC/APC"),
                            CreateLISATray("28", "LISATray 24 x LC/APC"),
                            CreateLISATray("29", "LISATray 24 x LC/APC"),
                            CreateLISATray("30", "LISATray 24 x LC/APC"),
                            CreateLISATray("31", "LISATray 24 x LC/APC"),
                            CreateLISATray("32", "LISATray 24 x LC/APC"),
                            CreateLISATray("33", "LISATray 24 x LC/APC"),
                            CreateLISATray("34", "LISATray 24 x LC/APC"),
                            CreateLISATray("35", "LISATray 24 x LC/APC"),
                            CreateLISATray("36", "LISATray 24 x LC/APC"),
                            CreateLISATray("37", "LISATray 24 x LC/APC"),
                            CreateLISATray("38", "LISATray 24 x LC/APC"),
                            CreateLISATray("39", "LISATray 24 x LC/APC"),
                            CreateLISATray("40", "LISATray 24 x LC/APC"),
                        }
                    )
                    {
                        ParentNodeStructureId = Guid.Parse("842a9bac-d9d8-4065-b182-6451ab3296b8"),
                        Info = "Forsyningsfibre"
                    }
                    ,
                    new TerminalEquipmentAZConnectivityViewEquipmentInfo(
                        id: Guid.NewGuid(),
                        category: "Subrack",
                        name: "Splitter holder",
                        specName: "LISA PLC Holder",
                        terminalStructures: new TerminalEquipmentAZConnectivityViewTerminalStructureInfo[]
                        {
                            CreateLISASplitter("1", "1:32 LC/APC LISA"),
                            CreateLISASplitter("2", "1:32 LC/APC LISA"),
                        }
                    )
                    {
                        ParentNodeStructureId = Guid.Parse("842a9bac-d9d8-4065-b182-6451ab3296b8"),
                        Info = "Kundesplittere"
                    }
                    ,
                    new TerminalEquipmentAZConnectivityViewEquipmentInfo(
                        id: Guid.NewGuid(),
                        category: "Subrack",
                        name: "Tray holder 2",
                        specName: "LISA Kassetteholder",
                        terminalStructures: new TerminalEquipmentAZConnectivityViewTerminalStructureInfo[]
                        {
                            CreateLISATray("41", "LISATray 24 x LC/APC"),
                            CreateLISATray("42", "LISATray 24 x LC/APC"),
                            CreateLISATray("43", "LISATray 24 x LC/APC"),
                            CreateLISATray("44", "LISATray 24 x LC/APC"),
                            CreateLISATray("45", "LISATray 24 x LC/APC"),
                            CreateLISATray("46", "LISATray 24 x LC/APC"),
                            CreateLISATray("47", "LISATray 24 x LC/APC"),
                            CreateLISATray("48", "LISATray 24 x LC/APC"),
                            CreateLISATray("49", "LISATray 24 x LC/APC"),
                            CreateLISATray("50", "LISATray 24 x LC/APC"),
                            CreateLISATray("51", "LISATray 24 x LC/APC"),
                            CreateLISATray("52", "LISATray 24 x LC/APC"),
                            CreateLISATray("53", "LISATray 24 x LC/APC"),
                            CreateLISATray("54", "LISATray 24 x LC/APC"),
                            CreateLISATray("55", "LISATray 24 x LC/APC"),
                            CreateLISATray("56", "LISATray 24 x LC/APC"),
                            CreateLISATray("57", "LISATray 24 x LC/APC"),
                            CreateLISATray("58", "LISATray 24 x LC/APC"),
                            CreateLISATray("59", "LISATray 24 x LC/APC"),
                            CreateLISATray("60", "LISATray 24 x LC/APC"),
                            CreateLISATray("61", "LISATray 24 x LC/APC"),
                            CreateLISATray("62", "LISATray 24 x LC/APC"),
                            CreateLISATray("63", "LISATray 24 x LC/APC"),
                            CreateLISATray("64", "LISATray 24 x LC/APC"),
                            CreateLISATray("65", "LISATray 24 x LC/APC"),
                            CreateLISATray("66", "LISATray 24 x LC/APC"),
                            CreateLISATray("67", "LISATray 24 x LC/APC"),
                            CreateLISATray("68", "LISATray 24 x LC/APC"),
                            CreateLISATray("69", "LISATray 24 x LC/APC"),
                            CreateLISATray("70", "LISATray 24 x LC/APC"),
                            CreateLISATray("71", "LISATray 24 x LC/APC"),
                            CreateLISATray("72", "LISATray 24 x LC/APC"),
                            CreateLISATray("73", "LISATray 24 x LC/APC"),
                            CreateLISATray("74", "LISATray 24 x LC/APC"),
                            CreateLISATray("75", "LISATray 24 x LC/APC"),
                            CreateLISATray("76", "LISATray 24 x LC/APC"),
                            CreateLISATray("77", "LISATray 24 x LC/APC"),
                            CreateLISATray("78", "LISATray 24 x LC/APC"),
                            CreateLISATray("79", "LISATray 24 x LC/APC"),
                            CreateLISATray("80", "LISATray 24 x LC/APC"),
                        }
                    )
                    {
                        ParentNodeStructureId = Guid.Parse("842a9bac-d9d8-4065-b182-6451ab3296b8"),
                        Info = "Forsyningsfibre"
                    }

                }
            )
            {
                ParentNodeStructures = new TerminalEquipmentAZConnectivityViewNodeStructureInfo[]
                {
                    new TerminalEquipmentAZConnectivityViewNodeStructureInfo(Guid.Parse("842a9bac-d9d8-4065-b182-6451ab3296b8"), "Rack", "Rack 1", "LISA ODF Rack")
                }
            };
        }

        private static TerminalEquipmentAZConnectivityViewTerminalStructureInfo CreateLISATray(string name, string specName)
        {

            return
                new TerminalEquipmentAZConnectivityViewTerminalStructureInfo(
                    id: Guid.NewGuid(),
                    category: "PatchSplice",
                    name: name,
                    specName: specName,
                    lines: new TerminalEquipmentAZConnectivityViewLineInfo[]
                    {
                        // connected to a splitter
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") { 
                            A = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("1")) { ConnectedTo = "Splitter 1-1", End = "OLT 1-1-1"},
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("1")) { ConnectedTo = "K23433 (72) Fiber 1", End = "FS1332 Inst 234433 Vesterbakken 1, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            A = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("2")) { ConnectedTo = "Splitter 1-2", End = "OLT 1-1-1"},
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("2")) { ConnectedTo = "K23433 (72) Fiber 2", End = "FS1332 Inst 234434 Vesterbakken 2, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            A = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("3")) { ConnectedTo = "Splitter 2-10", End = "OLT 1-1-2"},
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("3")) { ConnectedTo = "K23433 (72) Fiber 3", End = "FS1712 Inst 224434 Rugbakken 10, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            A = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("4")) { ConnectedTo = "Splitter 2-11", End = "OLT 1-1-2"},
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("4")) { ConnectedTo = "K23433 (72) Fiber 4", End = "FS1712 Inst 143444 Rugbakken 15, Grejs"},
                        },

                        // not connected to a splitter
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("5")) { ConnectedTo = "K23433 (72) Fiber 5", End = "FS1512 Inst 143445 Rugbakken 16, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("6")) { ConnectedTo = "K23433 (72) Fiber 6", End = "FS1512 Inst 143446 Rugbakken 17, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("7")) { ConnectedTo = "K23433 (72) Fiber 7", End = "FS1512 Inst 143447 Rugbakken 18 ST TV, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("8")) { ConnectedTo = "K23433 (72) Fiber 8", End = "FS1512 Inst 143448 Rugbakken 18 ST TH, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("9")) { ConnectedTo = "K23433 (72) Fiber 9", End = "FS1512 Inst 143449 Rugbakken 18 1 TV, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("10")) { ConnectedTo = "K23433 (72) Fiber 10", End = "FS1512 Inst 143449 Rugbakken 18 1 TV, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("11")) { ConnectedTo = "K23433 (72) Fiber 11", End = "FS1512 Inst 143449 Rugbakken 18 1 TH, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("12")) { ConnectedTo = "K23433 (72) Fiber 12", End = "FS1512 Inst 143450 Rugbakken 18 2 TV, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("13")) { ConnectedTo = "K23433 (72) Fiber 13", End = "FS1512 Inst 143451 Rugbakken 18 2 TH, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("14")) { ConnectedTo = "K23433 (72) Fiber 14", End = "FS1512 Inst 143452 Rugbakken 18 3 TV, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("15")) { ConnectedTo = "K23433 (72) Fiber 15", End = "FS1512 Inst 143453 Rugbakken 18 3 TH, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("16")) { ConnectedTo = "K23433 (72) Fiber 16", End = "FS1512 Inst 143454 Rugbakken 18 4 TV, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("17")) { ConnectedTo = "K23433 (72) Fiber 17", End = "FS1512 Inst 143455 Rugbakken 18 4 TH, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("18")) { ConnectedTo = "K23433 (72) Fiber 18", End = "FS1512 Inst 143456 Rugbakken 18 5 TV, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("19")) { ConnectedTo = "K23433 (72) Fiber 19", End = "FS1512 Inst 143457 Rugbakken 18 5 TH, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("20")) { ConnectedTo = "K23433 (72) Fiber 20", End = "FS1512 Inst 143458 Rugbakken 18 6 TV, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("21")) { ConnectedTo = "K23433 (72) Fiber 21", End = "FS1512 Inst 143459 Rugbakken 18 6 TH, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("22")) { ConnectedTo = "K23433 (72) Fiber 22", End = "FS1512 Inst 143460 Rugbakken 18 7 TV, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("23")) { ConnectedTo = "K23433 (72) Fiber 23", End = "FS1512 Inst 143461 Rugbakken 18 7 TH, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Splice") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("24")) { ConnectedTo = "K23433 (72) Fiber 24", End = "FS1512 Inst 143466 Rugbakken 22, Grejs"},
                        },
                    }
                );
                {
                }

        }

        private static TerminalEquipmentAZConnectivityViewTerminalStructureInfo CreateLISASplitter(string name, string specName)
        {

            return
                new TerminalEquipmentAZConnectivityViewTerminalStructureInfo(
                    id: Guid.NewGuid(),
                    category: "Splitter",
                    name: name,
                    specName: specName,
                    lines: new TerminalEquipmentAZConnectivityViewLineInfo[]
                    {
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailSplice_PigtailPatch") {
                            A = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("IN 1")) { ConnectedTo = "Splitter 1-1-1", End = "OLT 1-1-1"},
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 1")) { ConnectedTo = "PatchSplice 1-1-1", End = "FS1332 Inst 234433 Vesterbakken 1, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 2")) { ConnectedTo = "PatchSplice 1-1-2", End = "FS1332 Inst 234434 Vesterbakken 2, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 3")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 4")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 5")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 6")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 7")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 8")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 9")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 10")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 11")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 12")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 13")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 14")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 15")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 16")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 17")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 18")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 19")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 20")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 21")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 22")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 23")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 24")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 25")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 26")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 27")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 28")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 29")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 30")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 31")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 32")) {},
                        },
                    }
                );
            {
            }
        }


        private static TerminalEquipmentAZConnectivityViewTerminalStructureInfo CreateCouplerType1(string name, string specName)
        {
            return
                new TerminalEquipmentAZConnectivityViewTerminalStructureInfo(
                    id: Guid.NewGuid(),
                    category: "Coupler",
                    name: name,
                    specName: specName,
                    lines: new TerminalEquipmentAZConnectivityViewLineInfo[]
                    {
                        new TerminalEquipmentAZConnectivityViewLineInfo("Patch_Patch") {
                            A = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("IN 1")) { ConnectedTo = "Splitter 1-1-1", End = "OLT 1-1-1"},
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 1")) { ConnectedTo = "PatchSplice 1-1-1", End = "FS1332 Inst 234433 Vesterbakken 1, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 2")) { ConnectedTo = "PatchSplice 1-1-2", End = "FS1332 Inst 234434 Vesterbakken 2, Grejs"},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 3")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 4")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 5")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 6")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 7")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 8")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 9")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 10")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 11")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 12")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 13")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 14")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 15")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 16")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 17")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 18")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 19")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 20")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 21")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 22")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 23")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 24")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 25")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 26")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 27")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 28")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 29")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 30")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 31")) {},
                        },
                        new TerminalEquipmentAZConnectivityViewLineInfo("PigtailPatch") {
                            Z = new TerminalEquipmentAZConnectivityViewEndInfo(CreateTerminal("OUT 32")) {},
                        },
                    }
                );
            {
            }
        }

        private static TerminalEquipmentAZConnectivityViewTerminalInfo CreateTerminal(string name)
        {
            return new TerminalEquipmentAZConnectivityViewTerminalInfo(Guid.NewGuid(), name);
        }
        */

    }
}
