using OpenFTTH.Core;

namespace OpenFTTH.UtilityGraphService.API.Model.Asset
{
    public interface IProductAssetModel : IIdentifiedObject
    {
        public string Version { get; }
        public ISpecification Specification { get; }
        public IManufacturer Manufacturer { get; }
    }
}
