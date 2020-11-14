﻿using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFTTH.WorkService.QueryModel
{
    /// <summary>
    /// AKA work order
    /// </summary>
    public class WorkTask
    {
        public Guid MRID { get; }
        public String Name { get; }
        public Geometry? Location { get; }
        public String? AddressString { get; set;  }
        public string? WorkTaskType { get; set; }
        public string? InstallationId { get; set; }
        public string? CentralOfficeArea { get; set; }
        public string? FlexPointArea { get; set; }
        public string? SplicePointArea { get; set; }
        public string? Technology { get; set; }
        public string Status { get; set; }

        public WorkTask(Guid mRID, string name, Geometry? location = null)
        {
            MRID = mRID;
            Name = name;
            Location = location;
        }
    }
}