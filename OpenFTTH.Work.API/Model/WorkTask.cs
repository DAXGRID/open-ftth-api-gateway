using NetTopologySuite.Geometries;
using System;

namespace OpenFTTH.Work.API.Model
{
    /// <summary>
    /// AKA work order
    /// </summary>
    public class WorkTask
    {
        public Guid MRID { get; }
        public Project Project { get; }
        public String Name { get; }
        public Geometry? Location { get; }
        public String? AddressString { get; set; }
        public string? WorkTaskType { get; set; }
        public string? InstallationId { get; set; }
        public string? CentralOfficeArea { get; set; }
        public string? FlexPointArea { get; set; }
        public string? SplicePointArea { get; set; }
        public string? Technology { get; set; }
        public string? Status { get; set; }



        public WorkTask(Guid mRID, Project project, string name, Geometry? location = null)
        {
            MRID = mRID;
            Project = project;
            Name = name;
            Location = location;
        }
    }
}
