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

            Field<IdGraphType>("WorkTaskId")
                .Description("Work Task GUID")
                .Resolve(context =>
                {
                    return context.Source.WorkTask.Id;
                });

            Field<IdGraphType>("ProjectId")
               .Description("Work Project Id")
               .Resolve(context =>
               {
                   return context.Source.WorkProject != null ? context.Source.WorkProject.Id : null;
               });

            Field<StringGraphType>("ProjectNumber")
                .Description("Project Number")
                .Resolve(context =>
                {
                    return context.Source.WorkProject != null ? context.Source.WorkProject.Number : null;
                });

            Field<StringGraphType>("ProjectName")
                .Description("Project Name")
                .Resolve(context =>
                {
                    return context.Source.WorkProject != null ? context.Source.WorkProject.Name : null;
                });

            Field<StringGraphType>("ProjectOwner")
                .Description("Project Owner")
                .Resolve(context =>
                {
                    return context.Source.WorkProject != null ? context.Source.WorkProject.Owner : null;
                });

            Field<StringGraphType>("ProjectType")
                .Description("Project Type")
                .Resolve(context =>
                {
                    return context.Source.WorkProject != null ? context.Source.WorkProject.Type : null;
                });

            Field<StringGraphType>("ProjectStatus")
               .Description("Project Status")
               .Resolve(context =>
               {
                   return context.Source.WorkProject != null ? context.Source.WorkProject.Status : null;
               });

            Field<StringGraphType>("Number")
               .Description("Work Task Number")
               .Resolve(context =>
               {
                   return context.Source.WorkTask.Number;
               });

            Field<DateGraphType>("CreatedDate")
               .Description("Work Task Created Date")
               .Resolve(context =>
               {
                   return context.Source.WorkTask.CreatedDate.Date;
               });

            Field<StringGraphType>("Name")
               .Description("Work Task Name")
               .Resolve(context =>
               {
                   if (context.Source.AddressString != null && string.IsNullOrEmpty(context.Source.WorkTask.Name))
                       return context.Source.AddressString;
                   else
                       return context.Source.WorkTask.Name;
               });

            Field<StringGraphType>("SubtaskName")
               .Description("Work Task Subtask Name")
               .Resolve(context =>
               {
                   return context.Source.WorkTask.SubtaskName;
               });

            Field<StringGraphType>("Type")
               .Description("Work Task Type")
               .Resolve(context =>
               {
                   return context.Source.WorkTask.Type;
               });

            Field<StringGraphType>("Status")
               .Description("Work Task Status")
               .Resolve(context =>
               {
                   return context.Source.WorkTask.Status;
               });

            Field<StringGraphType>("Owner")
               .Description("Work Task Owner")
               .Resolve(context =>
               {
                   return context.Source.WorkTask.Owner;
               });

            Field<StringGraphType>("InstallationId")
               .Description("Work Task Installation Id")
               .Resolve(context =>
               {
                   return context.Source.WorkTask.InstallationId;
               });

            Field<StringGraphType>("AreaId")
               .Description("Work Task Area Id")
               .Resolve(context =>
               {
                   return context.Source.WorkTask.AreaId;
               });

            Field<IdGraphType>("UnitAddressId")
              .Description("Work Task Unit Address Id")
              .Resolve(context =>
              {
                  return context.Source.WorkTask.UnitAddressId;
              });

            Field<GeometryType>("Geometry")
               .Description("Work Task Geometry")
               .Resolve(context =>
               {
                   if (context.Source.X > 0)
                   {
                       var coordinatConversionResult = coordinateConverter.ConvertFromUTM32NToWGS84(context.Source.X, context.Source.Y);

                       return Geometry.MapToPointFromXY(coordinatConversionResult[0], coordinatConversionResult[1]);
                   }
                   else
                       return null;
               });

            Field<StringGraphType>("ModifiedBy")
              .Description("Users modifying the task")
              .Resolve(context =>
              {
                  var userList = workContextManager.GetUsersAssignedToWorkTask(context.Source.WorkTask.Id);

                  if (userList.Count > 0)
                  {
                      return string.Join(",", userList);

                  }
                  else
                  {
                      return null;
                  }

              });

        }
    }


}
