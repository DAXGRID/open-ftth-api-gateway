using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.CoreTypes;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.Work.API.Model;
using OpenFTTH.Work.Business;

namespace OpenFTTH.APIGateway.GraphQL.Work.Types
{
    public class WorkTaskAndProjectType : ObjectGraphType<WorkTaskAndProject>
    {
        public WorkTaskAndProjectType(ILogger<WorkTaskAndProjectType> logger, WorkContextManager workContextManager)
        {
            var coordinateConverter = new UTM32WGS84Converter();

            Field<IdGraphType>(
                name: "WorkTaskId",
                description: "Work Task GUID",
                resolve: context =>
                {
                    return context.Source.WorkTask.Id;
                }
            );

            Field<IdGraphType>(
               name: "ProjectId",
               description: "Work Project Id",
               resolve: context =>
               {
                   return context.Source.WorkProject != null ? context.Source.WorkProject.Id : null;
               }
           );

            Field<StringGraphType>(
                name: "ProjectNumber",
                description: "Project Number",
                resolve: context =>
                {
                    return context.Source.WorkProject != null ? context.Source.WorkProject.Number : null;
                }
            );

            Field<StringGraphType>(
                name: "ProjectName",
                description: "Project Name",
                resolve: context =>
                {
                    return context.Source.WorkProject != null ? context.Source.WorkProject.Name : null;
                }
            );

            Field<StringGraphType>(
                name: "ProjectOwner",
                description: "Project Owner",
                resolve: context =>
                {
                    return context.Source.WorkProject != null ? context.Source.WorkProject.Owner : null;
                }
            );

            Field<StringGraphType>(
                name: "ProjectType",
                description: "Project Type",
                resolve: context =>
                {
                    return context.Source.WorkProject != null ? context.Source.WorkProject.Type : null;
                }
            );

            Field<StringGraphType>(
               name: "ProjectStatus",
               description: "Project Status",
               resolve: context =>
               {
                   return context.Source.WorkProject != null ? context.Source.WorkProject.Status : null;
               }
            );

            Field<StringGraphType>(
               name: "Number",
               description: "Work Task Number",
               resolve: context =>
               {
                   return context.Source.WorkTask.Number;
               }
            );

            Field<DateGraphType>(
               name: "CreatedDate",
               description: "Work Task Created Date",
               resolve: context =>
               {
                   return context.Source.WorkTask.CreatedDate.Date;
               }
            );

            Field<StringGraphType>(
               name: "Name",
               description: "Work Task Name",
               resolve: context =>
               {
                   if (context.Source.AddressString != null && string.IsNullOrEmpty(context.Source.WorkTask.Name))
                       return context.Source.AddressString;
                   else
                       return context.Source.WorkTask.Name;
               }
            );

            Field<StringGraphType>(
               name: "SubtaskName",
               description: "Work Task Subtask Name",
               resolve: context =>
               {
                   return context.Source.WorkTask.SubtaskName;
               }
            );

            Field<StringGraphType>(
               name: "Type",
               description: "Work Task Type",
               resolve: context =>
               {
                   return context.Source.WorkTask.Type;
               }
            );

            Field<StringGraphType>(
               name: "Status",
               description: "Work Task Status",
               resolve: context =>
               {
                   return context.Source.WorkTask.Status;
               }
            );

            Field<StringGraphType>(
               name: "Owner",
               description: "Work Task Owner",
               resolve: context =>
               {
                   return context.Source.WorkTask.Owner;
               }
            );

            Field<StringGraphType>(
               name: "InstallationId",
               description: "Work Task Installation Id",
               resolve: context =>
               {
                   return context.Source.WorkTask.InstallationId;
               }
            );

            Field<StringGraphType>(
               name: "AreaId",
               description: "Work Task Area Id",
               resolve: context =>
               {
                   return context.Source.WorkTask.AreaId;
               }
            );

            Field<IdGraphType>(
              name: "UnitAddressId",
              description: "Work Task Unit Address Id",
              resolve: context =>
              {
                  return context.Source.WorkTask.UnitAddressId;
              }
           );

            Field<GeometryType>(
               name: "Geometry",
               description: "Work Task Geometry",
               resolve: context =>
               {
                   if (context.Source.X > 0)
                   {
                       var coordinatConversionResult = coordinateConverter.ConvertFromUTM32NToWGS84(context.Source.X, context.Source.Y);

                       return Geometry.MapToPointFromXY(coordinatConversionResult[0],coordinatConversionResult[1]);
                   }
                   else
                       return null;
               }
            );

            Field<StringGraphType>(
              name: "ModifiedBy",
              description: "Users modifying the task",
              resolve: context =>
              {
                  var userList = workContextManager.GetUsersAssignedToWorkTask(context.Source.WorkTask.Id);

                  if (userList.Count > 0 )
                  {
                      return string.Join(",", userList);

                  }
                  else
                  {
                      return null;
                  }

              }
           );

        }
    }


}
