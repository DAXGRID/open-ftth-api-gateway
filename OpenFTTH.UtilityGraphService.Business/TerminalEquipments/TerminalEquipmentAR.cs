using FluentResults;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.Graph.Projections;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments
{
    /// <summary>
    /// A equipment place in a node/rack - i.e. splice closures, OLTs etc.
    /// </summary>
    public class TerminalEquipmentAR : AggregateBase
    {
        private TerminalEquipment? _terminalEquipment;

        public TerminalEquipmentAR()
        {
            Register<TerminalEquipmentPlacedInNodeContainer>(Apply);
            Register<TerminalEquipmentRemoved>(Apply);
            Register<TerminalEquipmentNamingInfoChanged>(Apply);
            Register<TerminalEquipmentAddressInfoChanged>(Apply);
            Register<TerminalEquipmentManufacturerChanged>(Apply);
            Register<TerminalEquipmentSpecificationChanged>(Apply);
            Register<AdditionalStructuresAddedToTerminalEquipment>(Apply);
            Register<TerminalStructureRemoved>(Apply);
            Register<TerminalStructureInterfaceInfoChanged>(Apply);
        }

        #region Place equipment

        public Result Place(
            CommandContext cmdContext,
            LookupCollection<TerminalEquipmentSpecification> terminalEquipmentSpecifications,
            LookupCollection<TerminalStructureSpecification> terminalStructureSpecifications,
            Guid nodeContainerId,
            Guid terminalEquipmentId,
            Guid terminalEquipmentSpecificationId,
            int sequenceNumber,
            TerminalEquipmentNamingMethodEnum namingMethod,
            NamingInfo? namingInfo,
            LifecycleInfo? lifecycleInfo,
            AddressInfo? addressInfo,
            Guid? manufacturerId
        )
        {
            this.Id = terminalEquipmentId;

            if (terminalEquipmentId == Guid.Empty)
                return Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.INVALID_TERMINAL_EQUIPMENT_ID_CANNOT_BE_EMPTY, "Terminal equipment id cannot be empty. A unique id must be provided by client."));

            if (nodeContainerId == Guid.Empty)
                return Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.INVALID_NODE_CONTAINER_ID_CANNOT_BE_EMPTY, "Node container id cannot be empty. Must reference an exiting node container."));

            if (!terminalEquipmentSpecifications.ContainsKey(terminalEquipmentSpecificationId))
                return Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.INVALID_TERMINAL_EQUIPMENT_SPECIFICATION_ID_NOT_FOUND, $"Cannot find terminal specification with id: {terminalEquipmentSpecificationId}"));


            var terminalEquipment = new TerminalEquipment
            (
               id: terminalEquipmentId,
               specificationId: terminalEquipmentSpecificationId,
               nodeContainerId: nodeContainerId,
               terminalStructures: CreateTerminalStructuresFromSpecification(terminalEquipmentSpecifications[terminalEquipmentSpecificationId], terminalStructureSpecifications),
               manufacturerId: manufacturerId,
               namingInfo: CalculateName(namingInfo, sequenceNumber, namingMethod),
               lifecycleInfo: lifecycleInfo,
               addressInfo: addressInfo
            );

            var terminalEquipmentPlacedInNodeContainerEvent = new TerminalEquipmentPlacedInNodeContainer(terminalEquipment)
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(terminalEquipmentPlacedInNodeContainerEvent);

            return Result.Ok();
        }

        private void Apply(TerminalEquipmentPlacedInNodeContainer obj)
        {
            Id = obj.Equipment.Id;
            _terminalEquipment = obj.Equipment;
        }

        #endregion

        #region Remove equipment
        public Result Remove(CommandContext cmdContext, UtilityGraph graph)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Terminal equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (IsAnyTerminalsConnected(graph))
            {
                return Result.Fail(new RemoveTerminalEquipmentError(
                    RemoveTerminalEquipmentErrorCodes.CANNOT_REMOVE_TERMINAL_EQUIPMENT_WITH_CONNECTED_TERMINALS,
                    $"Cannot remove a terminal equipment if some of its terminals are connected")
                );
            }

            var @event = new TerminalEquipmentRemoved(
               terminalEquipmentId: this.Id
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(TerminalEquipmentRemoved @event)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Terminal equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");
        }

 
        #endregion

        #region Add Structure
        public Result AddAdditionalStructures(CommandContext cmdContext, TerminalStructureSpecification terminalStructureSpecification, int startPosition, int numberOfStructures)
        {
            List<TerminalStructure> additionalStructures = new();

            for (int position = startPosition; position < startPosition + numberOfStructures; position++)
            {
                if (_terminalEquipment != null && _terminalEquipment.TerminalStructures.Any(s => s.Position == position && !s.Deleted))
                {
                    return Result.Fail(new TerminalEquipmentError(
                        TerminalEquipmentErrorCodes.POSITION_ALREADY_OCCUPIED_BY_TERMINAL_STRUCTURE,
                        $"Cannot add terminal structure to position: {position} because the slot position is already occupied by another card/tray/module")
                    );
                }
                    //throw new ApplicationException($"A structure already exists at position: {position} in terminal equipment: {_terminalEquipment.Id}");

                additionalStructures.Add(CreateTerminalStructureFromSpecification(terminalStructureSpecification, position));
            }

            var terminalEquipmentAdditionalStructuresAddedEvent = new AdditionalStructuresAddedToTerminalEquipment(this.Id, additionalStructures.ToArray())
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(terminalEquipmentAdditionalStructuresAddedEvent);

            return Result.Ok();
        }

        public Result AddInterface(CommandContext cmdContext, TerminalStructureSpecification terminalStructureSpecification, InterfaceInfo interfaceInfo)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Terminal equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            List<TerminalStructure> additionalStructures = new();

            // Find next avaiable postion
            int nextUnusedPosition = 1;

            if (_terminalEquipment.TerminalStructures.Count() > 0)
                nextUnusedPosition = _terminalEquipment.TerminalStructures.Max(s => s.Position) + 1;
        
            additionalStructures.Add(CreateTerminalStructureFromSpecification(terminalStructureSpecification, nextUnusedPosition, interfaceInfo));

            var terminalEquipmentAdditionalStructuresAddedEvent = new AdditionalStructuresAddedToTerminalEquipment(this.Id, additionalStructures.ToArray())
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(terminalEquipmentAdditionalStructuresAddedEvent);

            return Result.Ok();
        }

        private void Apply(AdditionalStructuresAddedToTerminalEquipment @event)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _terminalEquipment = TerminalEquipmentProjectionFunctions.Apply(_terminalEquipment, @event);
        }

        #endregion

        #region Remove Structure
        public Result RemoveStructure(CommandContext cmdContext, Guid terminalStructureId, UtilityGraph graph)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Terminal equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (!_terminalEquipment.TerminalStructures.Any(t => t.Id == terminalStructureId && !t.Deleted))
            {
                return Result.Fail(new TerminalEquipmentError(
                    TerminalEquipmentErrorCodes.TERMINAL_STRUCTURE_NOT_FOUND,
                    $"Can't find any terminal structure with id: {terminalStructureId} in terminal equipment with id: {this.Id}")
                );
            }

            if (IsAnyTerminalsConnected(graph, terminalStructureId))
            {
                return Result.Fail(new TerminalEquipmentError(
                    TerminalEquipmentErrorCodes.CANNOT_REMOVE_TERMINAL_STRUCTURE_WITH_CONNECTED_TERMINALS,
                    $"Cannot remove a terminal structure if some of its terminals are connected")
                );
            }

            var @event = new TerminalStructureRemoved(
               terminalEquipmentId: this.Id,
               terminalStructureId: terminalStructureId
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(TerminalStructureRemoved @event)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _terminalEquipment = TerminalEquipmentProjectionFunctions.Apply(_terminalEquipment, @event);
        }

        #endregion

        #region Change Naming Info
        public Result ChangeNamingInfo(CommandContext cmdContext, NamingInfo? namingInfo)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Terminal equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if ((_terminalEquipment.NamingInfo == null && namingInfo == null) || (_terminalEquipment.NamingInfo != null && _terminalEquipment.NamingInfo.Equals(namingInfo)))
            {
                return Result.Fail(new UpdateEquipmentPropertiesError(
                       UpdateEquipmentPropertiesErrorCodes.NO_CHANGE_TO_NAMING_INFO,
                       $"Will not update naming info, because the provided value is equal the existing value.")
                   );
            }

            var @event = new TerminalEquipmentNamingInfoChanged(
              terminalEquipmentId: this.Id,
              namingInfo: namingInfo
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(TerminalEquipmentNamingInfoChanged @event)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _terminalEquipment = TerminalEquipmentProjectionFunctions.Apply(_terminalEquipment, @event);
        }

        #endregion

        #region Change Address Info
        public Result ChangeAddressInfo(CommandContext cmdContext, AddressInfo? addressInfo)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Terminal equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if ((_terminalEquipment.AddressInfo == null && addressInfo == null) || (_terminalEquipment.AddressInfo != null && _terminalEquipment.AddressInfo.Equals(addressInfo)))
            {
                return Result.Fail(new UpdateEquipmentPropertiesError(
                       UpdateEquipmentPropertiesErrorCodes.NO_CHANGE_TO_ADDRESS_INFO,
                       $"Will not update address info, because the provided value is equal the existing value.")
                   );
            }

            var @event = new TerminalEquipmentAddressInfoChanged(
              terminalEquipmentId: this.Id,
              addressInfo: addressInfo
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(TerminalEquipmentAddressInfoChanged @event)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _terminalEquipment = TerminalEquipmentProjectionFunctions.Apply(_terminalEquipment, @event);
        }

        #endregion

        #region Change Manufacturer
        public Result ChangeManufacturer(CommandContext cmdContext, Guid manufacturerId)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (_terminalEquipment.ManufacturerId == manufacturerId)
            {
                return Result.Fail(new UpdateEquipmentPropertiesError(
                       UpdateEquipmentPropertiesErrorCodes.NO_CHANGE_TO_MANUFACTURER,
                       $"Will not change manufacturer, because the provided value is equal the existing value.")
                   );
            }

            var @event = new TerminalEquipmentManufacturerChanged(
              terminalEquipmentId: this.Id,
              manufacturerId: manufacturerId
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(TerminalEquipmentManufacturerChanged @event)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _terminalEquipment = TerminalEquipmentProjectionFunctions.Apply(_terminalEquipment, @event);
        }

        #endregion

        #region Change Specification
        public Result ChangeSpecification(CommandContext cmdContext, TerminalEquipmentSpecification currentSpecification, TerminalEquipmentSpecification newSpecification)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Terminal equipment property cannot be null. Seems that terminal equipment has never been placed. Please check command handler logic.");

            if (_terminalEquipment.SpecificationId == newSpecification.Id)
            {
                return Result.Fail(new UpdateEquipmentPropertiesError(
                       UpdateEquipmentPropertiesErrorCodes.NO_CHANGE_TO_SPECIFICATION,
                       $"Will not change specification, because the provided specification id is the same as the existing one.")
                   );
            }

            var @event = new TerminalEquipmentSpecificationChanged(
              terminalEquipmentId: this.Id,
              newSpecificationId: newSpecification.Id
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(TerminalEquipmentSpecificationChanged @event)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _terminalEquipment = TerminalEquipmentProjectionFunctions.Apply(_terminalEquipment, @event);
        }

        #endregion

        #region Change Interface Info

        public Result ChangeInterfaceInfo(CommandContext cmdContext, Guid terminalStructureId, InterfaceInfo? interfaceInfo)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Terminal equipment property cannot be null. Seems that terminal equipment has never been placed. Please check command handler logic.");

            if (!_terminalEquipment.TerminalStructures.Any(t => t.Id == terminalStructureId))
                throw new ApplicationException($"Cannot find terminal structure with id: {terminalStructureId} in terminal equipment with id: {this.Id}");

            var terminalStructure = _terminalEquipment.TerminalStructures.First(t => t.Id == terminalStructureId);

            if ((interfaceInfo == null && terminalStructure.interfaceInfo != null) ||
                (interfaceInfo != null && terminalStructure.interfaceInfo == null) ||
                (interfaceInfo != null && terminalStructure.interfaceInfo != null && !interfaceInfo.EqualTo(terminalStructure.interfaceInfo))
            )
            {
                var @event = new TerminalStructureInterfaceInfoChanged(
                     terminalEquipmentId: this.Id,
                     terminalStructureId: terminalStructure.Id,
                     interfaceInfo: interfaceInfo
                   )
                        {
                            CorrelationId = cmdContext.CorrelationId,
                            IncitingCmdId = cmdContext.CmdId,
                            UserName = cmdContext.UserContext?.UserName,
                            WorkTaskId = cmdContext.UserContext?.WorkTaskId
                        };

                RaiseEvent(@event);

                return Result.Ok();
            }

            return Result.Ok();
        }

        private void Apply(TerminalStructureInterfaceInfoChanged @event)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Terminal equipment property cannot be null. Seems that terminal equipment has never been placed. Please check command handler logic.");

            _terminalEquipment = TerminalEquipmentProjectionFunctions.Apply(_terminalEquipment, @event);
        }

        #endregion

        #region Helper functions

        private TerminalStructure[] CreateTerminalStructuresFromSpecification(TerminalEquipmentSpecification terminalEquipmentSpecification, LookupCollection<TerminalStructureSpecification> terminalStructureSpecifications)
        {
            List<TerminalStructure> terminalStructures = new();

            foreach (var structureTemplate in terminalEquipmentSpecification.StructureTemplates)
            {
                if (terminalStructureSpecifications.TryGetValue(structureTemplate.TerminalStructureSpecificationId, out var terminalStructureSpecification))
                {
                    terminalStructures.Add(
                        CreateTerminalStructureFromSpecification(terminalStructureSpecification, structureTemplate.Position)
                    );
                }
                else
                {
                    throw new ApplicationException($"Invalid/corrupted terminal equipment specification: {terminalEquipmentSpecification.Id} References a non-existing terminal structure specification with id: {structureTemplate.TerminalStructureSpecificationId}");
                }
            }

            return terminalStructures.ToArray();
        }

        private TerminalStructure CreateTerminalStructureFromSpecification(TerminalStructureSpecification terminalStructureSpecification, int position, InterfaceInfo? interfaceInfo = null)
        {
            if (_terminalEquipment != null && _terminalEquipment.TerminalStructures.Any(s => s.Position == position && !s.Deleted))
                throw new ApplicationException($"A structure already exists at position: {position} in terminal equipment: {_terminalEquipment.Id}");

            Dictionary<string, Guid> internalConnectivityNodesByName = new();

            List<Terminal> terminals = new();

            foreach (var terminalTemplate in terminalStructureSpecification.TerminalTemplates)
            {
                // Create internal connectivity node if specified in template and don't exist yet
                if (terminalTemplate.InternalConnectivityNode != null)
                {
                    if (!internalConnectivityNodesByName.TryGetValue(terminalTemplate.InternalConnectivityNode, out var _))
                    {
                        internalConnectivityNodesByName.Add(terminalTemplate.InternalConnectivityNode, Guid.NewGuid());
                    }
                }

                // Get id of eventually specificed internal connectivity node
                Guid internalConnectivityNodeId = terminalTemplate.InternalConnectivityNode == null ? Guid.Empty : internalConnectivityNodesByName[terminalTemplate.InternalConnectivityNode];

                // A non-bi terminal must always be connected to an internal connectivity node
                if (terminalTemplate.Direction != TerminalDirectionEnum.BI && internalConnectivityNodeId == Guid.Empty)
                    throw new ApplicationException($"Invalid/corrupted terminal structure specification: {terminalStructureSpecification.Id} All non-bi terminals must reference an internal connectivity node");

                terminals.Add(
                    new Terminal(Guid.NewGuid(), terminalTemplate.Name, terminalTemplate.Direction, terminalTemplate.IsPigtail, terminalTemplate.IsSplice, terminalTemplate.ConnectorType, internalConnectivityNodeId)
                );
            }

            return new TerminalStructure(Guid.NewGuid(), terminalStructureSpecification.Id, (ushort)position, terminals.ToArray())
            { 
                interfaceInfo = interfaceInfo
            };
        }

        private NamingInfo CalculateName(NamingInfo? namingInfo, int sequenceNumber, TerminalEquipmentNamingMethodEnum namingMethod)
        {
            NamingInfo resultNamingInfo = new();

            resultNamingInfo.Description = namingInfo?.Description;

            switch (namingMethod)
            {
                case TerminalEquipmentNamingMethodEnum.NumberOnly:
                    resultNamingInfo.Name = sequenceNumber.ToString();
                    break;

                case TerminalEquipmentNamingMethodEnum.NameOnly:
                    resultNamingInfo.Name = namingInfo?.Name;
                    break;

                case TerminalEquipmentNamingMethodEnum.NameAndNumber:
                    if (namingInfo != null && !String.IsNullOrEmpty(namingInfo.Name))
                        resultNamingInfo.Name = namingInfo.Name + " " + sequenceNumber.ToString();
                    else
                        resultNamingInfo.Name = sequenceNumber.ToString();
                    break;
            }

            return resultNamingInfo;
        }

        private bool IsAnyTerminalsConnected(UtilityGraph graph, Guid? structureId = null)
        {
            if (_terminalEquipment == null)
                throw new ApplicationException($"Invalid internal state. Terminal equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            var version = graph.LatestCommitedVersion;

            foreach (var terminalStructure in _terminalEquipment.TerminalStructures.Where(t => !t.Deleted))
            {
                if (structureId != null && terminalStructure.Id != structureId)
                    continue;

                foreach (var terminal in terminalStructure.Terminals)
                {
                    var terminalElement = graph.GetTerminal(terminal.Id, version);

                    if (terminalElement != null && terminalElement is UtilityGraphConnectedTerminal connectedTerminal)
                    {
                        if (connectedTerminal.NeighborElements(version).Where(n => n is not UtilityGraphInternalEquipmentConnectivityLink).Count() > 0)
                            return true;
                    }
                }
            }

            return false;
        }

      


        #endregion


    }

  
}
