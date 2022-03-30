using System;
using System.Collections.Generic;

namespace OpenFTTH.Work.API.Model
{
    public class Project
    {
        public Guid MRID { get; }
        public String Name { get; }

        private List<WorkTask> _workTasks = new List<WorkTask>();
        public List<WorkTask> WorkTasks => _workTasks;

        public Project(Guid mRID, string name)
        {
            MRID = mRID;
            Name = name;
        }

        public void AddWorkOrder(WorkTask workOrder)
        {
            _workTasks.Add(workOrder);
        }
    }
}
