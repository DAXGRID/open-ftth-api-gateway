using System;
using System.Collections.Generic;

namespace OpenFTTH.UtilityGraphService.API.Model.Outage
{
    public class OutageViewNode
    {
        public Guid Id { get; }
        public string Label { get; set; }
        public string? Description { get; set; }
        public string? Value { get; set; }
        public bool? Expanded { get; set; }
        public List<OutageViewNode>? Nodes { get; set; }

        public Guid? InterestId { get; set; }

        public OutageViewNode(Guid id, string label, string? description = null, string? value = null, List<OutageViewNode>? nodes = null)
        {
            Id = id;
            Label = label;
            Description = description;
            Value = value;
            Nodes = nodes;
        }

        public void AddNode(OutageViewNode conduitNode)
        {
            if (Nodes == null)
                Nodes = new List<OutageViewNode>();

            Nodes.Add(conduitNode);
        }

        public override string ToString()
        {
            return Label + (Description != null ? " " + Description : null);
        }

    }
}
