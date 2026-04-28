using OpenFTTH.Core;
using OpenFTTH.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record SpanEquipmentSpecification : IIdentifiedObject
    {
        public Guid Id { get; }
        public string Category { get;}
        public string Name { get; }
        public SpanStructureTemplate RootTemplate { get; }
        public bool Deprecated { get; init; }
        public bool IsFixed { get; init; }
        public bool IsMultiLevel { get; init; }
        public string? Description { get; init; }
        public Guid[]? ManufacturerRefs { get; init; }
        public bool IsCable { get; init; }
        public CableTubeSpecification[] CableTubes { get; init; }

        /// <summary>
        /// </summary>
        /// <param name="id">The specification id</param>
        /// <param name="category">What kind of category: Conduit, Fiber Cable etc.</param>
        /// <param name="name">Short human readable name of the specification - i.e. Ø50 12x10</param>
        /// <param name="version">Since specifications are immutable, a version must always be provided</param>
        public SpanEquipmentSpecification(Guid id, string category, string name, SpanStructureTemplate rootTemplate)
        {
            this.Id = id;
            this.Category = category;
            this.Name = name;
            this.RootTemplate = rootTemplate;
        }

        /// <summary>
        /// Get formatted text - e.g. [K232323 (2)] [1 #{RD} RD 2 #{BL} BL (2)].
        /// If not possible to create formatted text, the function returns null.
        /// </summary>
        /// <param name="cableName"></param>
        /// <param name="fiberPosition"></param>
        /// <param name="spanStructureSpecifications"></param>
        /// <returns></returns>
        public string? GetFormattedCableString(string cableName, int fiberPosition, bool includeCableName, LookupCollection<SpanStructureSpecification> spanStructureSpecifications)
        {
            HashSet<string> colorCodes = new HashSet<string> { "RD", "GR", "BL", "YL", "WH", "SL", "BR", "VI", "TQ", "BK", "OR", "PK" };

            if (RootTemplate.ChildTemplates.Length > 1)
            {
                var firstFiberSpec = spanStructureSpecifications[RootTemplate.ChildTemplates.First().SpanStructureSpecificationId];

                // Only try to create formatted cable string if the specification is using two-letter color codes
                if (!colorCodes.Contains(firstFiberSpec.Color))
                    return null;

                string line1 = "[" + cableName + "(" + RootTemplate.ChildTemplates.Count() + ")] ";

                string line2 = "[";

                // Tube info
                if (CableTubes != null)
                {
                    var tubeNo = GetTubeNumber(fiberPosition);

                    var tubeColor = GetTubeColor(tubeNo);

                    line2 += tubeNo;

                    line2 += $" #{{{tubeColor}}} {{{tubeColor}}} ";
                }

                // Fiber info
                var fiberNo = GetFiberNumber(fiberPosition);

                var fiberColor = GetFiberColor(fiberPosition, spanStructureSpecifications);

                line2 += fiberNo;

                line2 += $" #{{{fiberColor}}} {{{fiberColor}}} ({fiberPosition})]";

                if (includeCableName)
                    return (line1 + line2);
                else
                    return line2;
            }

            return null;
        }

        public int GetTubeNumber(int fiberPosition)
        {
            int numberOfFibersPerTube = 12;

            if (CableTubes != null)
                numberOfFibersPerTube = RootTemplate.ChildTemplates.Count() / CableTubes.Count();

            int tubeNo = ((fiberPosition - 1) / numberOfFibersPerTube) + 1;

            return tubeNo;
        }

        public int GetFiberNumber(int fiberPosition)
        {
            int numberOfFibersPerTube = 12;

            if (CableTubes != null)
                numberOfFibersPerTube = RootTemplate.ChildTemplates.Count() / CableTubes.Count();

            int fiberNo = ((fiberPosition - 1) % numberOfFibersPerTube) + 1;

            return fiberNo;
        }

        private string? GetTubeColor(int tubeNo)
        {
            if (CableTubes == null)
                return null;
         
            if (CableTubes.Count() < tubeNo)
                return null;

            return CableTubes[tubeNo - 1].Color;
        }

        private string? GetFiberColor(int fiberPosition, LookupCollection<SpanStructureSpecification> spanStructureSpecifications)
        {
            if (RootTemplate.ChildTemplates.Count() < fiberPosition)
                return null;

            var specId = RootTemplate.ChildTemplates[fiberPosition - 1].SpanStructureSpecificationId;
            var color = spanStructureSpecifications[specId].Color;

            return color;
        }
    }
}

