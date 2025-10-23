using OpenFTTH.Results;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenFTTH.APIGateway.Specifications
{
    public class SpecificationImporter
    {
        private static Guid _specSeederId = Guid.Parse("ac478ae4-9851-4c25-8b1a-d45d69e250d7");

        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private IEventStore _eventStore;

        private ILogger<SpecificationImporter> _logger;

        public SpecificationImporter(ILoggerFactory loggerFactory, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IEventStore eventStore)
        {
            _logger = loggerFactory.CreateLogger<SpecificationImporter>();
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _eventStore = eventStore;
        }

        public void ImportFromJsonString(string json)
        {
            var specificationData = JsonConvert.DeserializeObject<Specifications>(json);

            if (specificationData is null)
            {
                throw new ArgumentNullException($"Could not deserialize specification data: '{json}'.");
            }

            Import(specificationData, false);
        }

        public void ImportFromDictionary(string dictionary)
        {
            if (Directory.Exists(dictionary))
            {
                var jsonFiles = Directory.GetFiles(dictionary, "*.json", SearchOption.AllDirectories);

                foreach(var fileInDirectory in jsonFiles)
                {
                    _logger.LogInformation($"Importing structure specfications from file: " + fileInDirectory);

                    ImportFromFile(fileInDirectory, true);
                }

                foreach (var fileInDirectory in jsonFiles)
                {
                    _logger.LogInformation($"Importing equipment specfications from file: " + fileInDirectory);
                    ImportFromFile(fileInDirectory, false);
                }

            }
            else
            {
                _logger.LogInformation($"Importing specfications from file: " + dictionary);
                ImportFromFile(dictionary, false);
            }
        }

        public void ImportFromFile(string fileName, bool terminalStructuresOnly)
        {
            var specificationData = JsonConvert.DeserializeObject<Specifications>(File.ReadAllText(fileName));
            Import(specificationData, terminalStructuresOnly);
        }

        private void Import(Specifications specifications, bool terminalAndSpanStructuresOnly)
        {
            // Create dict with manufacture specs
            var manufacturerSpecs = _eventStore.Projections.Get<ManufacturerProjection>().Manufacturer;

            Dictionary<string, Manufacturer> manufacturerSpecByName = new();

            foreach (var manufacturer in manufacturerSpecs)
            {
                if (!manufacturerSpecByName.ContainsKey(manufacturer.Name.ToLower()))
                    manufacturerSpecByName.Add(manufacturer.Name.ToLower(), manufacturer);
            }


            // Create dict with node container specs
            var nodeContainerSpecs = _eventStore.Projections.Get<NodeContainerSpecificationsProjection>().Specifications;

            Dictionary<string, NodeContainerSpecification> nodeContainerSpecByName = new();

            foreach (var nodeContainerSpec in nodeContainerSpecs)
            {
                if (!nodeContainerSpecByName.ContainsKey(nodeContainerSpec.Description.ToLower()))
                    nodeContainerSpecByName.Add(nodeContainerSpec.Description.ToLower(), nodeContainerSpec);
            }

            // Create dict with rack specs specs
            var rackSpecs = _eventStore.Projections.Get<RackSpecificationsProjection>().Specifications;

            Dictionary<string, RackSpecification> rackSpecByName = new();

            foreach (var rackSpec in rackSpecs)
            {
                if (!rackSpecByName.ContainsKey(rackSpec.Name.ToLower()))
                    rackSpecByName.Add(rackSpec.Name.ToLower(), rackSpec);
            }


            // Create dict with terminal equipment specs
            var terminalEquipmentSpecs = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;

            Dictionary<string, TerminalEquipmentSpecification> terminalEquipmentSpecByName = new();

            foreach (var terminalEquipmentSpec in terminalEquipmentSpecs)
            {
                if (!terminalEquipmentSpecByName.ContainsKey(terminalEquipmentSpec.Name.ToLower()))
                    terminalEquipmentSpecByName.Add(terminalEquipmentSpec.Name.ToLower(), terminalEquipmentSpec);
            }


            // Create dict with terminal structure specs
            var terminalStructureSpecs = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications;

            Dictionary<string, TerminalStructureSpecification> terminalStructureSpecByName = new();

            foreach (var terminalStructureSpec in terminalStructureSpecs)
            {
                if (!terminalStructureSpecByName.ContainsKey(terminalStructureSpec.Name.ToLower()))
                    terminalStructureSpecByName.Add(terminalStructureSpec.Name.ToLower(), terminalStructureSpec);
            }


            // Create dict with span equipment specs
            var spanEquipmentSpecs = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            Dictionary<string, SpanEquipmentSpecification> spanEquipmentSpecByName = new();

            foreach (var spanEquipmentSpec in spanEquipmentSpecs)
            {
                if (!spanEquipmentSpecByName.ContainsKey(spanEquipmentSpec.Name.ToLower()))
                    spanEquipmentSpecByName.Add(spanEquipmentSpec.Name.ToLower(), spanEquipmentSpec);
            }


            // Create dict with span structure specs
            var spanStructureSpecs = _eventStore.Projections.Get<SpanStructureSpecificationsProjection>().Specifications;

            Dictionary<string, SpanStructureSpecification> spanStructureSpecByName = new();

            foreach (var spanStructureSpec in spanStructureSpecs)
            {
                if (!spanStructureSpecByName.ContainsKey(spanStructureSpec.Name.ToLower()))
                    spanStructureSpecByName.Add(spanStructureSpec.Name.ToLower(), spanStructureSpec);
            }


            // Create manufactures that don't exist
            if (specifications.Manufacturers != null)
            {
                foreach (var manufacturerSpec in specifications.Manufacturers)
                {
                    if (!manufacturerSpecByName.ContainsKey(manufacturerSpec.Name.ToLower()))
                    {
                        _logger.LogInformation($"Creating manufacturer: " + manufacturerSpec.Name);

                        try
                        {
                            AddManufacturer(new Manufacturer(Guid.NewGuid(), manufacturerSpec.Name));

                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Got exception processing manufacturer specification: '{manufacturerSpec.Name}'", ex);
                        }
                    }
                }
            }

            // Reload manufacturer
            manufacturerSpecs = _eventStore.Projections.Get<ManufacturerProjection>().Manufacturer;

            manufacturerSpecByName = new();

            foreach (var manufacturer in manufacturerSpecs)
                manufacturerSpecByName.Add(manufacturer.Name.ToLower(), manufacturer);


            // Create node containers that don't exist
            if (specifications.NodeContainers != null)
            {
                foreach (var nodeContainerSpec in specifications.NodeContainers)
                {
                    if (!nodeContainerSpecByName.ContainsKey(nodeContainerSpec.Name.ToLower()))
                    {
                        _logger.LogInformation($"Creating node container specification: " + nodeContainerSpec.Name);

                        try
                        {
                            AddSpecification(
                                new NodeContainerSpecification(
                                    Guid.NewGuid(),
                                    nodeContainerSpec.Category,
                                    nodeContainerSpec.ShortName
                                )
                                {
                                    Description = nodeContainerSpec.Name,
                                    Deprecated = nodeContainerSpec.Deprecated,
                                    ManufacturerRefs = GetManufactureIdsFromNames(manufacturerSpecByName, nodeContainerSpec.Manufacturers)
                                }
                            );
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Got exception processing container specification: '{nodeContainerSpec.Name}'", ex);
                        }
                    }
                }
            }

            // Create node containers that don't exist
            if (specifications.Racks != null)
            {
                foreach (var rackSpec in specifications.Racks)
                {
                    if (!rackSpecByName.ContainsKey(rackSpec.Name.ToLower()))
                    {
                        _logger.LogInformation($"Creating rack specification: " + rackSpec.Name);

                        try
                        {
                            AddSpecification(
                                new RackSpecification(
                                    Guid.NewGuid(),
                                    rackSpec.Name,
                                    rackSpec.ShortName
                                )
                                {
                                    Description = rackSpec.Name,
                                    Deprecated = rackSpec.Deprecated,
                                }
                            );
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Got exception processing rack specification: '{rackSpec.Name}'", ex);
                        }
                    }
                }
            }

            // Create terminal structure specs that don't exist
            if (specifications.TerminalStructures != null)
            {
                foreach (var terminalStructureSpec in specifications.TerminalStructures)
                {
                    if (!terminalStructureSpecByName.ContainsKey(terminalStructureSpec.Name.ToLower()))
                    {
                        _logger.LogInformation($"Creating terminal structure: " + terminalStructureSpec.Name);

                        try
                        {

                            AddSpecification(
                                new TerminalStructureSpecification(
                                    Guid.NewGuid(),
                                    terminalStructureSpec.Category,
                                    terminalStructureSpec.Name,
                                    terminalStructureSpec.ShortName,
                                    CreateTerminals(terminalStructureSpec.Terminals)
                                )
                                {
                                    Description = terminalStructureSpec.Description,
                                    Deprecated = terminalStructureSpec.Deprecated,
                                    ManufacturerRefs = GetManufactureIdsFromNames(manufacturerSpecByName, terminalStructureSpec.Manufacturers),
                                    IsCustomerSplitter = terminalStructureSpec.IsCustomerSplitter,
                                    IsInterfaceModule = terminalStructureSpec.IsInterfaceModule,
                                    IsPassThrough = terminalStructureSpec.IsPassThrough,
                                    SlotTypeAlias = terminalStructureSpec.SlotTypeAlias,
                                    PortTypeAlias = terminalStructureSpec.PortTypeAlias
                                }
                            );
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Got exception processing terminal structure specification: '{terminalStructureSpec.Name}'", ex);
                        }
                    }
                }
            }

            // Create span structure specs that don't exist
            if (specifications.SpanStructures != null)
            {
                foreach (var spanStructureSpec in specifications.SpanStructures)
                {
                    if (!spanStructureSpecByName.ContainsKey(spanStructureSpec.Name.ToLower()))
                    {
                        _logger.LogInformation($"Creating span structure: " + spanStructureSpec.Name);

                        try
                        {

                            AddSpecification(
                            new SpanStructureSpecification(
                                Guid.NewGuid(),
                                spanStructureSpec.SpanClassType,
                                spanStructureSpec.Name,
                                spanStructureSpec.Color
                            )
                            {
                                Description = spanStructureSpec.Description,
                                Deprecated = spanStructureSpec.Deprecated,
                                InnerDiameter = spanStructureSpec.InnerDiameter,
                                OuterDiameter = spanStructureSpec.OuterDiameter,
                            }
                        );
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Got exception processing span structure specification: '{spanStructureSpec.Name}'", ex);
                        }
                    }
                }
            }



            // Reload terminal structures
            terminalStructureSpecs = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications;

            terminalStructureSpecByName = new();

            foreach (var terminalStructureSpec in terminalStructureSpecs)
                terminalStructureSpecByName.Add(terminalStructureSpec.Name.ToLower(), terminalStructureSpec);

            // Reload span structures
            spanStructureSpecs = _eventStore.Projections.Get<SpanStructureSpecificationsProjection>().Specifications;

            spanStructureSpecByName = new();

            foreach (var spanStructureSpec in spanStructureSpecs)
            {
                if (!spanStructureSpecByName.ContainsKey(spanStructureSpec.Name.ToLower()))
                    spanStructureSpecByName.Add(spanStructureSpec.Name.ToLower(), spanStructureSpec);
            }

            if (!terminalAndSpanStructuresOnly)
            {

                // Create terminal equipment specs that don't exist
                if (specifications.TerminalEquipments != null)
                {
                    foreach (var terminalEquipmentSpec in specifications.TerminalEquipments)
                    {
                        if (!terminalEquipmentSpecByName.ContainsKey(terminalEquipmentSpec.Name.ToLower()))
                        {
                            _logger.LogInformation($"Creating terminal equipment: " + terminalEquipmentSpec.Name);
                            try
                            {

                                AddSpecification(
                                new TerminalEquipmentSpecification(
                                    Guid.NewGuid(),
                                    terminalEquipmentSpec.Category,
                                    terminalEquipmentSpec.Name,
                                    terminalEquipmentSpec.ShortName,
                                    terminalEquipmentSpec.IsRackEquipment,
                                    terminalEquipmentSpec.HeightInRackUnits,
                                    CreateTerminalStructureTemplates(terminalEquipmentSpec.Structures, terminalStructureSpecByName)
                                )
                                {
                                    Description = terminalEquipmentSpec.Description,
                                    Deprecated = terminalEquipmentSpec.Deprecated,
                                    IsAddressable = terminalEquipmentSpec.IsAddressable,
                                    IsFixed = terminalEquipmentSpec.IsFixed,
                                    IsCustomerTermination = terminalEquipmentSpec.IsCustomerTermination,
                                    IsLineTermination = terminalEquipmentSpec.IsLineTermination,
                                    TerminalStructuresIsNameable = terminalEquipmentSpec.TerminalStructuresIsNameable,
                                    ManufacturerRefs = GetManufactureIdsFromNames(manufacturerSpecByName, terminalEquipmentSpec.Manufacturers)
                                }
                            );
                            }
                            catch (Exception ex)
                            {
                                throw new Exception($"Got exception processing terminal equipment specification: '{terminalEquipmentSpec.Name}'", ex);
                            }

                        }
                        else
                        {
                            _logger.LogInformation($"Terminal equipment already exists: " + terminalEquipmentSpec.Name);
                        }
                    }
                }

                // Create span equipment specs that don't exist
                if (specifications.SpanEquipments != null)
                {
                    foreach (var spanEquipmentSpec in specifications.SpanEquipments)
                    {
                        if (!spanEquipmentSpecByName.ContainsKey(spanEquipmentSpec.Name.ToLower()))
                        {
                            _logger.LogInformation($"Creating span equipment: " + spanEquipmentSpec.Name);

                            try
                            {
                                AddSpecification(
                                    new SpanEquipmentSpecification(
                                        Guid.NewGuid(),
                                        spanEquipmentSpec.Category,
                                        spanEquipmentSpec.Name,
                                        CreateSpanStructureTemplates(spanEquipmentSpec.OuterStructure, spanStructureSpecByName)
                                    )
                                    {
                                        Description = spanEquipmentSpec.Description,
                                        Deprecated = spanEquipmentSpec.Deprecated,
                                        IsFixed = spanEquipmentSpec.IsFixed,
                                        IsCable = spanEquipmentSpec.IsCable,
                                        IsMultiLevel = spanEquipmentSpec.IsMultiLevel,
                                        ManufacturerRefs = GetManufactureIdsFromNames(manufacturerSpecByName, spanEquipmentSpec.Manufacturers),
                                        CableTubes = GetCableTubesFromSpecs(spanEquipmentSpec.CableTubes)
                                    }
                                );
                            } catch (Exception ex)
                            {
                                throw new Exception($"Got exception processing span equipment specification: '{spanEquipmentSpec.Name}'", ex);
                            }
                        }
                        else
                        {
                            _logger.LogInformation($"Span equipment already exists: " + spanEquipmentSpec.Name);
                        }
                    }
                }
            }
        }

        private CableTubeSpecification[] GetCableTubesFromSpecs(List<CableTubeSpec> cableTubes)
        {
            if (cableTubes == null || cableTubes.Count == 0)
                return null;

            List<CableTubeSpecification> specs = new();

            foreach (var tube in cableTubes)
            {
                specs.Add(new CableTubeSpecification(tube.Position, tube.Color, tube.Description));
            }

            return specs.ToArray();
        }

        private Guid[] GetManufactureIdsFromNames(Dictionary<string, Manufacturer> manufacturerSpecByName, List<string> manufacturerNames)
        {
            if (manufacturerNames == null)
                return null;

            List<Guid> manufacturerIds = new();

            foreach (var manufacturerName in manufacturerNames)
            {
                if (!manufacturerSpecByName.ContainsKey(manufacturerName.ToLower()))
                {
                    var err = $"Manufacturer with name: '{manufacturerName} does not exist.";
                    _logger.LogError(err);
                    throw new ApplicationException(err);
                }

                manufacturerIds.Add(manufacturerSpecByName[manufacturerName.ToLower()].Id);
            }

            return manufacturerIds.ToArray();
        }

        private TerminalTemplate[] CreateTerminals(List<TerminalTemplateSpec> terminalSpecs)
        {
            List<TerminalTemplate> terminals = new();

            foreach (var terminalSpec in terminalSpecs)
            {
                terminals.Add(
                    new TerminalTemplate(terminalSpec.Name, Enum.Parse<TerminalDirectionEnum>(terminalSpec.Direction), terminalSpec.IsPigtail, terminalSpec.IsSplice)
                    {
                        ConnectorType = terminalSpec.ConnectorType,
                        InternalConnectivityNode = terminalSpec.InternalConnectivityNode
                    }
                 );
            }

            return terminals.ToArray();
        }


        private TerminalStructureTemplate[] CreateTerminalStructureTemplates(List<TerminalStructureTemplateSpec> structureTemplateSpecs, Dictionary<string, TerminalStructureSpecification> terminalStructureSpecByName)
        {
            if (structureTemplateSpecs == null)
                return null;

            List<TerminalStructureTemplate> templates = new();

            HashSet<int> positionAlreadyUsed = new();

            foreach (var template in structureTemplateSpecs)
            {
                if (!terminalStructureSpecByName.ContainsKey(template.RefName.ToLower()))
                {
                    var err = $"Terminal structure with name: '{template.RefName}' does not exist.";
                    _logger.LogError(err);
                    throw new ApplicationException(err);
                }

                var terminalStructureSpec = terminalStructureSpecByName[template.RefName.ToLower()];

                if (positionAlreadyUsed.Contains(template.Position))
                {
                    var err = $"Terminal position already used: {template.Position}";
                    _logger.LogError(err);
                    throw new ApplicationException(err);
                }

                positionAlreadyUsed.Add(template.Position);

                templates.Add(
                   new TerminalStructureTemplate(terminalStructureSpec.Id, template.Position)
                   {
                       Name = template.Name
                   }
                );
            }

            return templates.ToArray();
        }

        private SpanStructureTemplate CreateSpanStructureTemplates(SpanStructureTemplateSpec rootStructure, Dictionary<string, SpanStructureSpecification> spanStructureSpecByName)
        {
            if (rootStructure == null)
                return null;

            // First create list of child structures
            List<SpanStructureTemplate> templates = new();

            HashSet<int> positionAlreadyUsed = new();

            foreach (var template in rootStructure.Children)
            {
                if (!spanStructureSpecByName.ContainsKey(template.RefName.ToLower()))
                {
                    var err = $"Span structure with name: '{template.RefName}' does not exist.";
                    _logger.LogError(err);
                    throw new ApplicationException(err);
                }

                var spanlStructureSpec = spanStructureSpecByName[template.RefName.ToLower()];

                if (positionAlreadyUsed.Contains(template.Position))
                {
                    var err = $"Position already used: {template.Position}";
                    _logger.LogError(err);
                    throw new ApplicationException(err);
                }

                positionAlreadyUsed.Add(template.Position);

                templates.Add(
                    new SpanStructureTemplate(spanlStructureSpec.Id, template.Level, template.Position, new SpanStructureTemplate[] { })
                );
            }

            // Create route structure

            if (!spanStructureSpecByName.ContainsKey(rootStructure.RefName.ToLower()))
            {
                var err = $"Span structure with name: '{rootStructure.RefName}' does not exist.";
                _logger.LogError(err);
                throw new ApplicationException(err);
            }

            var rootStructureSpec = spanStructureSpecByName[rootStructure.RefName.ToLower()];

            SpanStructureTemplate root = new SpanStructureTemplate(rootStructureSpec.Id, rootStructure.Level, rootStructure.Position, templates.ToArray());

            return root;
        }

        private List<string> GetManufacturesFromIds(LookupCollection<UtilityGraphService.API.Model.UtilityNetwork.Manufacturer> manufactures, Guid[] manufacturerRefs)
        {
            if (manufacturerRefs == null)
                return null;

            List<string> manufacturerNames = new();

            foreach (var manufacturerRef in manufacturerRefs)
            {
                manufacturerNames.Add(manufactures[manufacturerRef].Name);
            }

            return manufacturerNames;
        }

        private void AddManufacturer(Manufacturer manufacturer)
        {
            var specifications = _eventStore.Projections.Get<ManufacturerProjection>().Manufacturer;

            if (specifications.ContainsKey(manufacturer.Id))
                return;

            var cmd = new AddManufacturer(Guid.NewGuid(), new UserContext("specification seeder", _specSeederId), manufacturer);

            var cmdResult = _commandDispatcher.HandleAsync<AddManufacturer, Result>(cmd).Result;

            if (cmdResult.IsFailed)
            {
                var errorMsg = cmdResult.Errors.First().Message;
                _logger.LogError(errorMsg);
                throw new ApplicationException(errorMsg);
            }
            else
                _logger.LogInformation("Manufacturer created OK");
        }

        private void AddSpecification(NodeContainerSpecification spec)
        {
            var specifications = _eventStore.Projections.Get<NodeContainerSpecificationsProjection>().Specifications;

            if (specifications.ContainsKey(spec.Id))
                return;

            var cmd = new AddNodeContainerSpecification(Guid.NewGuid(), new UserContext("specification seeder", _specSeederId), spec);
            var cmdResult = _commandDispatcher.HandleAsync<AddNodeContainerSpecification, Result>(cmd).Result;

            if (cmdResult.IsFailed)
            {
                var errorMsg = cmdResult.Errors.First().Message;
                _logger.LogError(errorMsg);
                throw new ApplicationException(errorMsg);
            }
            else
                _logger.LogInformation("Node container specification created OK");
        }

        private void AddSpecification(RackSpecification spec)
        {
            var specifications = _eventStore.Projections.Get<RackSpecificationsProjection>().Specifications;

            if (specifications.ContainsKey(spec.Id))
                return;

            var cmd = new AddRackSpecification(Guid.NewGuid(), new UserContext("specification seeder", _specSeederId), spec);
            var cmdResult = _commandDispatcher.HandleAsync<AddRackSpecification, Result>(cmd).Result;

            if (cmdResult.IsFailed)
            {
                var errorMsg = cmdResult.Errors.First().Message;
                _logger.LogError(errorMsg);
                throw new ApplicationException(errorMsg);
            }
            else
                _logger.LogInformation("Rack specification created OK");
        }

        private void AddSpecification(TerminalStructureSpecification spec)
        {
            var specifications = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications;

            if (specifications.ContainsKey(spec.Id))
                return;

            var cmd = new AddTerminalStructureSpecification(Guid.NewGuid(), new UserContext("specification seeder", _specSeederId), spec);
            var cmdResult = _commandDispatcher.HandleAsync<AddTerminalStructureSpecification, Result>(cmd).Result;

            if (cmdResult.IsFailed)
            {
                var errorMsg = cmdResult.Errors.First().Message;
                _logger.LogError(errorMsg);
                throw new ApplicationException(errorMsg);
            }
            else
                _logger.LogInformation("Terminal structure specification created OK");
        }

        private void AddSpecification(TerminalEquipmentSpecification spec)
        {
            var specifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;

            if (specifications.ContainsKey(spec.Id))
                return;

            var cmd = new AddTerminalEquipmentSpecification(Guid.NewGuid(), new UserContext("specification seeder", _specSeederId), spec);
            var cmdResult = _commandDispatcher.HandleAsync<AddTerminalEquipmentSpecification, Result>(cmd).Result;

            if (cmdResult.IsFailed)
            {
                var errorMsg = cmdResult.Errors.First().Message;
                _logger.LogError(errorMsg);
                throw new ApplicationException(errorMsg);
            }
            else
                _logger.LogInformation("Terminal equipment specification created OK");
        }

        private void AddSpecification(SpanEquipmentSpecification spec)
        {
            var specifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            if (specifications.ContainsKey(spec.Id))
                return;

            var cmd = new AddSpanEquipmentSpecification(Guid.NewGuid(), new UserContext("specification seeder", _specSeederId), spec);
            var cmdResult = _commandDispatcher.HandleAsync<AddSpanEquipmentSpecification, Result>(cmd).Result;

            if (cmdResult.IsFailed)
            {
                var errorMsg = cmdResult.Errors.First().Message;
                _logger.LogError(errorMsg);
                throw new ApplicationException(errorMsg);
            }
            else
                _logger.LogInformation("Span equipment specification created OK");
        }

        private void AddSpecification(SpanStructureSpecification spec)
        {
            var specifications = _eventStore.Projections.Get<SpanStructureSpecificationsProjection>().Specifications;

            if (specifications.ContainsKey(spec.Id))
                return;

            var cmd = new AddSpanStructureSpecification(Guid.NewGuid(), new UserContext("specification seeder", _specSeederId), spec);
            var cmdResult = _commandDispatcher.HandleAsync<AddSpanStructureSpecification, Result>(cmd).Result;

            if (cmdResult.IsFailed)
            {
                var errorMsg = cmdResult.Errors.First().Message;
                _logger.LogError(errorMsg);
                throw new ApplicationException(errorMsg);
            }
            else
                _logger.LogInformation("Span structure specification created OK");
        }

    }
}
