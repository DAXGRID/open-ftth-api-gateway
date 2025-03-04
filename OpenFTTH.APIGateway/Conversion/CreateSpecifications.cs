using OpenFTTH.Results;
using OpenFTTH.CQRS;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using System;
using System.Linq;
using System.Threading;

namespace OpenFTTH.APIGateway.Conversion
{
    public class CreateSpecifications
    {
        private static Guid _specSeederId = Guid.Parse("2897abbb-f504-4957-ac6e-fe47f5294239");

        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;

        public CreateSpecifications(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        public static Guid Manu_GMPlast = Guid.Parse("47e87d16-a1f0-488a-8c3e-cb3a4f3e8926");
        public static Guid Manu_Emtelle = Guid.Parse("fd457db0-ad32-444c-9946-a9e5e8a14d17");
        public static Guid Manu_Fiberpowertech = Guid.Parse("e845dc91-f3b9-407b-a622-2c300d43aaad");
        public static Guid Manu_Cubis = Guid.Parse("6b02e4aa-19f1-46a5-85e8-c1faab236ef0");

        // Span equipments
        public static Guid CustomerConduit_Ø7_Orange = Guid.Parse("671529f8-65f7-4670-9b02-8bae53747f1c");
        public static Guid CustomerConduit_Ø12_Orange = Guid.Parse("ddd86873-9d6c-4741-a406-084c628314db");

        // Uknown types
        public static Guid Well_Unknown = Guid.Parse("7388baca-4710-4756-a3df-caaf1fc246b8");
        public static Guid ConduitClosure_Unknown = Guid.Parse("c288e797-a65c-4cf6-b63d-5eda4b4a8a8c");

        // Span Structures
        public static Guid Ø7_Blue = Guid.Parse("94d5bc20-31da-4191-b4dc-9a701bba83d6");
        public static Guid Ø7_Yellow = Guid.Parse("6416641c-1c31-4e6f-9bc4-41a435915a69");
        public static Guid Ø7_White = Guid.Parse("6c861c63-0134-45ac-9e1e-c982b543e74e");
        public static Guid Ø7_Green = Guid.Parse("55af46cb-5c53-4046-a4d6-0069ad60bbf8");
        public static Guid Ø7_Black = Guid.Parse("b96a1ecb-4665-4929-a016-82ee48cae57e");
        public static Guid Ø7_Red = Guid.Parse("ef1c6787-eb76-4941-96e0-4ca2fac5b57b");
        public static Guid Ø7_Orange = Guid.Parse("def493b9-71a1-43c5-9f09-02e8229bc785");
        public static Guid Ø7_Pink = Guid.Parse("1bb0ee9b-3f41-44f0-8f5b-ccae8cb63a23");
        public static Guid Ø7_Silver = Guid.Parse("16c77f06-3439-4843-8ff9-76bb3a7c8b20");
        public static Guid Ø7_Brown = Guid.Parse("4e9373e8-c6bd-4a85-856d-6f2ef57f6833");
        public static Guid Ø7_Turquoise = Guid.Parse("72d88e01-178a-4656-9fe5-8819544c94c9");
        public static Guid Ø7_Violet = Guid.Parse("3672dd91-886a-444e-93a2-afc0d44d6541");

        public static Guid Ø7_3_5_Orange = Guid.Parse("00b2786c-e6b9-4f88-b4ea-584337a4d38a");


        public static Guid Ø10_Blue = Guid.Parse("980a2a21-cf40-4b70-91ae-69af79be9e80");
        public static Guid Ø10_Yellow = Guid.Parse("779a8d88-1c52-4fca-b2d5-0aabfa652393");
        public static Guid Ø10_White = Guid.Parse("ec75555f-33ea-432f-9235-d1f111cebd68");
        public static Guid Ø10_Green = Guid.Parse("c1734de6-1ca2-4b2f-af74-86c6ca45f6e5");
        public static Guid Ø10_Black = Guid.Parse("30f3f962-274c-43fc-951b-f3e2213f9ba7");
        public static Guid Ø10_Red = Guid.Parse("2ef21422-fc35-4c87-8051-b235568c1def");
        public static Guid Ø10_Orange = Guid.Parse("0aa07b3a-8168-4ab8-8ccf-62c163e5be28");
        public static Guid Ø10_Pink = Guid.Parse("f9d5f15e-d4f2-4d53-9fce-7808a579e853");
        public static Guid Ø10_Silver = Guid.Parse("7e21f619-039b-4a58-b43c-ee5496f0cbb2");
        public static Guid Ø10_Brown = Guid.Parse("b4cde3bf-56ff-43f3-a62a-270cf9afa24d");
        public static Guid Ø10_Turquoise = Guid.Parse("9d05b556-2eb4-4a30-89d6-4b813c10dabe");
        public static Guid Ø10_Violet = Guid.Parse("c09ca8f5-cd37-4cd8-9d32-b6274f3c2c64");

        public static Guid Ø12_Orange = Guid.Parse("5e0bef90-6838-49a7-9063-7d099a06e23d");
        public static Guid Ø12_Red = Guid.Parse("f90dfced-3244-4cde-9839-2ca8b49a4483");

        public static Guid Ø32_Orange = Guid.Parse("e7dea74e-df7f-4a0d-a752-98f9e02be1a8");
        public static Guid Ø40_Orange = Guid.Parse("ac417fea-b6f6-4a5a-9c9e-10ee05ecbf56");
        public static Guid Ø40_Red = Guid.Parse("be4deb0f-8d15-49ba-bbeb-fafb4ed66de5");
        public static Guid Ø50_Orange = Guid.Parse("7960355a-4dab-4d60-b3a5-e20ac4301176");
        public static Guid Ø110_Red = Guid.Parse("e078e830-f79d-4220-bd9a-87ed7cf81f1d");

        public OpenFTTH.Results.Result<CreateSpecifications> Run()
        {
            var nodeContainerSpecification = _queryDispatcher.HandleAsync<GetNodeContainerSpecifications, Result<LookupCollection<NodeContainerSpecification>>>(new GetNodeContainerSpecifications()).Result;

            if (nodeContainerSpecification.Value.ContainsKey(Well_Unknown))
                return OpenFTTH.Results.Result.Fail("Additional specification already present in system");

            AddNodeContainerSpecifications();

            AddSpanEquipmentSpecifications();

            Thread.Sleep(100);

            return OpenFTTH.Results.Result.Ok(this);
        }

        private void AddNodeContainerSpecifications()
        {
            // Uknown manhole
            AddSpecification(new NodeContainerSpecification(Well_Unknown, "ManHole", "Brønd Ukendt Type")
            {
                Description = "Brønd Ukendt Type",
                ManufacturerRefs = new Guid[] { }
            });

            // Uknown conduit closure
            AddSpecification(new NodeContainerSpecification(ConduitClosure_Unknown, "ConduitClosure", "Rørmuffe Ukendt Type")
            {
                Description = "Rørmuffe Ukendt Type",
                ManufacturerRefs = new Guid[] { }
            });

        }

        private void AddSpanEquipmentSpecifications()
        {

            AddSpecification(new SpanStructureSpecification(Ø7_3_5_Orange, "Conduit", "Ø7/3.5", "Orange") { OuterDiameter = 7, InnerDiameter = 4 });


            // Ø7 customer conduit
            AddSpecification(new SpanEquipmentSpecification(CustomerConduit_Ø7_Orange, "CustomerConduit", "Ø7", new SpanStructureTemplate(Ø7_3_5_Orange, 1, 1, Array.Empty<SpanStructureTemplate>()))
            {
                Description = "ø7/3,5 orange",
                ManufacturerRefs = new Guid[] { Manu_GMPlast, Manu_Emtelle },
                IsFixed = true,
                IsMultiLevel = false
            });

            // Ø12 customer conduit
            AddSpecification(new SpanEquipmentSpecification(CustomerConduit_Ø12_Orange, "CustomerConduit", "Ø12", new SpanStructureTemplate(Ø12_Orange, 1, 1, Array.Empty<SpanStructureTemplate>()))
            {
                Description = "ø12/8 orange",
                ManufacturerRefs = new Guid[] { Manu_GMPlast, Manu_Emtelle },
                IsFixed = true,
                IsMultiLevel = false
            });


        }

      

        private void AddSpecification(SpanEquipmentSpecification spec)
        {
            var cmd = new AddSpanEquipmentSpecification(Guid.NewGuid(), new UserContext("specification seeder", _specSeederId), spec);

            var cmdResult = _commandDispatcher.HandleAsync<AddSpanEquipmentSpecification, Result>(cmd).Result;

            if (cmdResult.IsFailed)
                throw new ApplicationException(cmdResult.Errors.First().Message);
        }

        private void AddSpecification(SpanStructureSpecification spec)
        {
            var cmd = new AddSpanStructureSpecification(Guid.NewGuid(), new UserContext("specification seeder", _specSeederId), spec);

            var cmdResult = _commandDispatcher.HandleAsync<AddSpanStructureSpecification, Result>(cmd).Result;

            if (cmdResult.IsFailed)
                throw new ApplicationException(cmdResult.Errors.First().Message);

        }

        private void AddSpecification(NodeContainerSpecification spec)
        {
            var cmd = new AddNodeContainerSpecification(Guid.NewGuid(), new UserContext("specification seeder",_specSeederId), spec);

            var cmdResult = _commandDispatcher.HandleAsync<AddNodeContainerSpecification, Result>(cmd).Result;

            if (cmdResult.IsFailed)
                throw new ApplicationException(cmdResult.Errors.First().Message);
        }

        private void AddManufacturer(Manufacturer manufacturer)
        {
            var cmd = new AddManufacturer(Guid.NewGuid(), new UserContext("specification seeder", _specSeederId), manufacturer);

            var cmdResult = _commandDispatcher.HandleAsync<AddManufacturer, Result>(cmd).Result;

            if (cmdResult.IsFailed)
                throw new ApplicationException(cmdResult.Errors.First().Message);
        }
    }
}

