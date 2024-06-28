using System.Xml;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace ifcCheckerRules
{
    internal class Program
    {
        private double AllowedFileSize, ActualFileSize;
        static void Main()
        {
            CheckCurrentFile(
                new FileStream("C:\\Users\\matve\\Downloads\\Telegram Desktop\\SYLA_ALL_CPTI_B_1.6_ХХ_24.1.1.1_M6_MF_1_AR22_I4000.ifc", FileMode.Open),
                new XmlDocument()
                );
        }
        private static bool CheckIfcFileVersion(IfcStore IStore, XmlElement Root)
        {
            return IStore.SchemaVersion.ToString() == Root.Attributes.GetNamedItem("ifcVersion").InnerText;
        }
        private static bool CheckIfcFileColors()
        {
            return true;
        }
        public static XmlDocument CheckCurrentFile(FileStream IFCFileStream, XmlDocument RulesXMLFile)
        {
            IfcStore IStore = IfcStore.Open(IFCFileStream, Xbim.IO.StorageType.Ifc, Xbim.IO.XbimModelType.MemoryModel);

            IEnumerable<IIfcColourRgb> Colours = GetColours<IIfcPipeSegment>(IStore, "Наименование системы по ПИМ", "Трубопроводная система: Прочее");

            foreach (IIfcColourRgb colour in Colours) Console.WriteLine(colour);

            CheckIfcFileVersion(IStore, RulesXMLFile.DocumentElement);

            var theDoor = IStore.Model.Instances.FirstOrDefault<IIfcPipeSegment>();
            Console.WriteLine($"Pipe ID: {theDoor.GlobalId}, Name: {theDoor.Name}");

            //get all single-value properties of the door
            var properties = theDoor.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                .SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties)
                .OfType<IIfcPropertySingleValue>();

            foreach (var property in properties)
                Console.WriteLine($"Property: {property.Name},\tValue: {property.NominalValue}");

            return new XmlDocument();
        }

        private static IEnumerable<IIfcColourRgb> GetColours<ILocalIfcType>(IfcStore IIfcStore, String AttributeName, String AttributeValue) where ILocalIfcType : IIfcElement
        {
            IEnumerable<IIfcColourRgb> Colours = new List<IIfcColourRgb>();

            IIfcColourRgb Color;

            foreach (var Element in IIfcStore.Instances
                .OfType<ILocalIfcType>())
            {
                var Properties = Element
                    .IsDefinedBy
                    .Where(p => p.RelatingPropertyDefinition is IIfcPropertySet)
                    .SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties)
                    .OfType<IIfcPropertySingleValue>();

                if (Properties
                    .Where(p => p.Name == AttributeName)
                    .FirstOrDefault()?
                    .NominalValue
                    .Value
                    .ToString() == AttributeValue)
                {
                    Color = (((Element
                        .Representation
                        .Representations
                        .FirstOrDefault(r => r.RepresentationType == "SweptSolid")
                        .Items
                        .FirstOrDefault(i => i is IIfcExtrudedAreaSolid) as IIfcExtrudedAreaSolid)
                        .StyledByItem
                        .FirstOrDefault()
                        .Styles
                        .FirstOrDefault(s => s is IIfcSurfaceStyle) as IIfcSurfaceStyle)
                        .Styles
                        .FirstOrDefault(s => s is IIfcSurfaceStyleRendering) as IIfcSurfaceStyleRendering)
                        .SurfaceColour;

                    (Colours as List<IIfcColourRgb>).Add(Color);
                }
            }

            return Colours;
        }
    }
}
