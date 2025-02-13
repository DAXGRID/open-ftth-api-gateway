using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Tests.TestData
{
    public static class TestEquipmentConnectionFaceData
    {
        /*
        public static EquipmentConnectivityFace[] EquipmentConnectivityFaces()
        {
            List<EquipmentConnectivityFace> result = new();

            result.Add(new EquipmentConnectivityFace()
            {
                DirectionType = "Outgoing",
                DirectionName = "Mod F1200, F1210 og F1230",
                EquipmentId = Guid.Parse("6df22afa-21af-4dda-bdf7-51885c3ccddd"),
                EquipmentKind = "SpanEqupment",
                EquipmentName = "K423434 72 Fiber"
            });

            result.Add(new EquipmentConnectivityFace()
            {
                DirectionType = "Outgoing",
                DirectionName = "Mod F1400, F1410, F1411, F1412 og F1415",
                EquipmentId = Guid.Parse("62fe7274-33fd-4955-aa18-ef5f3c4ea6ee"),
                EquipmentKind = "SpanEqupment",
                EquipmentName = "K423444 72 Fiber"
            });

            result.Add(new EquipmentConnectivityFace()
            {
                DirectionType = "Outgoing",
                DirectionName = "Mod F1700, F1710, F1720, F1730 og F1750",
                EquipmentId = Guid.Parse("7e60a5f2-2b44-43ac-8437-04bd5100ce41"),
                EquipmentKind = "SpanEqupment",
                EquipmentName = "K423446 72 Fiber"
            });

            result.Add(new EquipmentConnectivityFace()
            {
                DirectionType = "Ingoing",
                DirectionName = "Splice Side",
                EquipmentId = Guid.Parse("a883644f-a00e-4fe1-a3a6-a9bd3f18cf7a"),
                EquipmentKind = "TerminalEqupment",
                EquipmentName = "Rack 1 - LISA Tray 1"
            });

            for (int i = 2; i <= 80; i++)
            {
                result.Add(new EquipmentConnectivityFace()
                {
                    DirectionType = "Ingoing",
                    DirectionName = "Splice Side",
                    EquipmentId = Guid.NewGuid(),
                    EquipmentKind = "TerminalEqupment",
                    EquipmentName = $"Rack 1 - LISA Tray {i}"
                });
            }


            for (int i = 1; i <= 80; i++)
            {
                result.Add(new EquipmentConnectivityFace()
                {
                    DirectionType = "Outgoing",
                    DirectionName = "Patch Side",
                    EquipmentId = Guid.NewGuid(),
                    EquipmentKind = "TerminalEqupment",
                    EquipmentName = $"Rack 1 - LISA Tray {i}"
                });
            }

            return result.ToArray();

        }

        public static EquipmentConnectivityFaceConnectionInfo[] TerminalEquipment_EquipmentConnectivityFaceConnectionInfo()
        {
            List<EquipmentConnectivityFaceConnectionInfo> result = new();

            for (int i = 1; i <= 12; i++)
            {
                result.Add(new EquipmentConnectivityFaceConnectionInfo()
                {
                    Id = Guid.NewGuid(),
                    Name = $"Rack 1 - LISA Tray 1 - Splice Pin {i}",
                    EndInfo = $"GALARH OLT 1-1-{i % 2} <- KINA WDM 1-2-{i}",
                    IsConnected = true
                });
            }

            for (int i = 13; i <= 24; i++)
            {
                result.Add(new EquipmentConnectivityFaceConnectionInfo()
                {
                    Id = Guid.NewGuid(),
                    Name = $"Rack 1 - LISA Tray 1 - Splice Pin {i}",
                    EndInfo = $"GALARH OLT 1-1-{i % 2} <- KINA WDM 1-2-{i}",
                    IsConnected = false
                });
            }


            return result.ToArray();
        }


        public static EquipmentConnectivityFaceConnectionInfo[] SpanEquipment_EquipmentConnectivityFaceConnectionInfo()
        {
            List<EquipmentConnectivityFaceConnectionInfo> result = new();

            for (int i = 1; i <= 12; i++)
            {
                result.Add(new EquipmentConnectivityFaceConnectionInfo()
                {
                    Id = Guid.NewGuid(),
                    Name = $"Fiber {i}",
                    EndInfo = $"F1200 GSS 1-1-{i} -> Splitter {i}",
                    IsConnected = true
                });
            }

            for (int i = 13; i <= 24; i++)
            {
                result.Add(new EquipmentConnectivityFaceConnectionInfo()
                {
                    Id = Guid.NewGuid(),
                    Name = $"Fiber {i}",
                    EndInfo = $"F1200 GSS 1-2-{i} -> K530030 (48) Fiber {i}",
                    IsConnected = false
                });
            }

            for (int i = 23; i <= 36; i++)
            {
                result.Add(new EquipmentConnectivityFaceConnectionInfo()
                {
                    Id = Guid.NewGuid(),
                    Name = $"Fiber {i}",
                    EndInfo = $"F1210 GSS 1-1-{i} -> Splitter {i}",
                    IsConnected = false
                });
            }

            for (int i = 37; i <= 48; i++)
            {
                result.Add(new EquipmentConnectivityFaceConnectionInfo()
                {
                    Id = Guid.NewGuid(),
                    Name = $"Fiber {i}",
                    EndInfo = $"F1210 GSS 1-2-{i} -> K430234 (48) Fiber {i}",
                    IsConnected = false
                });
            }

            for (int i = 49; i <= 60; i++)
            {
                result.Add(new EquipmentConnectivityFaceConnectionInfo()
                {
                    Id = Guid.NewGuid(),
                    Name = $"Fiber {i}",
                    EndInfo = $"F1230 GSS 1-1-{i} -> Splitter {i}",
                    IsConnected = true
                });
            }

            for (int i = 61; i <= 72; i++)
            {
                result.Add(new EquipmentConnectivityFaceConnectionInfo()
                {
                    Id = Guid.NewGuid(),
                    Name = $"Fiber {i}",
                    EndInfo = $"F1230 GSS 1-2-{i} -> K601155 (48) Fiber {i}",
                    IsConnected = false
                });
            }






            return result.ToArray();
        }
        */
    }
}
