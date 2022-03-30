using NetTopologySuite.Geometries;
using OpenFTTH.Work.API.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.Work.Business.InMemTestImpl
{
    public class InMemRepoImpl
    {
        public Dictionary<Guid, Project> Projects { get; set; }

        private Dictionary<string, UserWorkContext> _userWorkContextByName = new Dictionary<string, UserWorkContext>();

        public InMemRepoImpl()
        {
            // throw in some test data


            // Dataopretning
            Projects = new Dictionary<Guid, Project>();

            var project1 = new Project(Guid.Parse("55783514-4481-4f62-a321-e025c62741ac"), "Dataopretning");

            project1.AddWorkOrder(
                new WorkTask(Guid.Parse("dd07c615-0497-48f0-825f-ccac0c7596d0"), project1, "Dataopretning af tracénet"));

            project1.AddWorkOrder(
                new WorkTask(Guid.Parse("4640df62-f4a9-4490-9400-f7ef88b64bfa"), project1, "Dataopretning af Bentley rør-konnektivitet"));

            Projects.Add(project1.MRID, project1);


            // Anlægsprojekter

            var project2 = new Project(Guid.Parse("9c27006c-b03e-401d-bdb4-3d0c5f4f915a"), "Anlægsprojekter");

            project2.AddWorkOrder(
                new WorkTask(Guid.Parse("f9883306-4692-48f9-b1bd-8fca5706a2d3"), project2, "Nyt område Hindhøjen", new Point(566673, 6235825))
                {
                    AddressString = "Hindhøjen",
                    CentralOfficeArea = "HINSON",
                    FlexPointArea = "F3320",
                    Technology = "PON",
                    Status = "Til projektering",
                    WorkTaskType = "Access-netværk"
                });

            Projects.Add(project2.MRID, project2);


            // Kundetilslutninger

            var project3 = new Project(Guid.Parse("68929c17-400d-4379-9794-137879c5959a"), "Kundetilslutning");

            project3.AddWorkOrder(
                new WorkTask(Guid.Parse("523a168d-c14f-4fb9-b64a-b3adc2fee628"), project3, "PON Privat 692345", new Point(566156, 6237162))
                {
                    AddressString = "Svalevej 5, Hinnerup",
                    CentralOfficeArea = "HINSON",
                    FlexPointArea = "F3410",
                    SplicePointArea = "F3416",
                    Technology = "PON",
                    InstallationId = "692345",
                    Status = "Til projektering",
                    WorkTaskType = "Privat kundetilslutning"
                });

            project3.AddWorkOrder(
                new WorkTask(Guid.Parse("08ce7a8f-4ad9-4b03-8386-86d3cebf408a"), project3, "PON Privat 703443", new Point(566139, 6237187))
                {
                    AddressString = "Svalevej 7, Hinnerup",
                    CentralOfficeArea = "HINSON",
                    FlexPointArea = "F3410",
                    SplicePointArea = "F3416",
                    Technology = "PON",
                    InstallationId = "703443",
                    Status = "Til projektering",
                    WorkTaskType = "Privat kundetilslutning"
                });

            project3.AddWorkOrder(
                new WorkTask(Guid.Parse("efb70f56-7d0e-41a8-9a48-82a30aa48395"), project3, "PON Privat 712323", new Point(566367, 6237104))
                {
                    AddressString = "Nørrevangen 54, Hinnerup",
                    CentralOfficeArea = "HINSON",
                    FlexPointArea = "F3410",
                    SplicePointArea = "F3413",
                    Technology = "PON",
                    InstallationId = "712323",
                    Status = "Til projektering",
                    WorkTaskType = "Privat kundetilslutning"
                });

            project3.AddWorkOrder(
              new WorkTask(Guid.Parse("086a5364-2cd4-481e-80d3-4f2d2025904d"), project3, "PtP Erhverv 125377", new Point(565264, 6236742))
              {
                  AddressString = "Vestergade 82, Hinnerup",
                  CentralOfficeArea = "HINSON",
                  FlexPointArea = "F3570",
                  SplicePointArea = "F3575",
                  Technology = "PtP",
                  InstallationId = "125377",
                  Status = "Til projektering",
                  WorkTaskType = "Erhverv kundetilslutning"
              });

            Projects.Add(project3.MRID, project3);
        }

        public UserWorkContext GetUserWorkContext(string userName)
        {
            if (_userWorkContextByName.ContainsKey(userName))
                return _userWorkContextByName[userName];

            return null;
        }

        public UserWorkContext SetUserCurrentWorkTask(string userName, Guid currentWorkTaskId)
        {
            if (GetWorkTaskById(currentWorkTaskId) == null)
                throw new ArgumentException("WorkTask with id: " + currentWorkTaskId + " does not exists!");

            if (!_userWorkContextByName.ContainsKey(userName))
            {
                _userWorkContextByName[userName] = new UserWorkContext(userName, GetWorkTaskById(currentWorkTaskId));
            }
            else
            {
                _userWorkContextByName[userName].CurrentWorkTask = GetWorkTaskById(currentWorkTaskId);
            }

            return _userWorkContextByName[userName];
        }

        private WorkTask GetWorkTaskById(Guid id)
        {
            if (Projects.Values.Any(p => p.WorkTasks.Any(w => w.MRID == id)))
                return Projects.Values.First(p => p.WorkTasks.Any(w => w.MRID == id)).WorkTasks.First(w => w.MRID == id);

            return null;
        }
    }
}
