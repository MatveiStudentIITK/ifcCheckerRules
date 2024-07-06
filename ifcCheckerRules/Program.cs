using System.Drawing;
using System.Reflection;
using System.Xml;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace Internship
{
    internal class IfcRuleChecker
    {
        public struct CheckResult
        {
            public CheckResult() {}
            public String FileName;
            public Tuple<bool, XbimSchemaVersion> VersionCheckResult;
            public Tuple<bool, long> SizeCheckResult;
            public Tuple<bool, double, IEnumerable<Tuple<bool, string, string>>> FileMaskCheckResult;
            public Tuple<bool, double, IEnumerable<Tuple<bool, double, string>>> CategoryColorCheckResult = new(false, 0, new List<Tuple<bool, double, string>>());
            public Tuple<bool, double, IEnumerable<Tuple<bool, Tuple<double, double, double, double>, string>>> CoordinatesCheckResult = new(false, 0, new List<Tuple<bool, Tuple<double, double, double, double>, string>>());
            public Tuple<bool, double, IEnumerable<Tuple<bool, string, string>>> AttributesCheckResult = new(false, 0, new List<Tuple<bool, string, string>>());
        }
        public CheckResult CheckIfcFile(FileStream IFCFileStream, FileStream XMLFileStream)
        {
            CheckResult IResult = new CheckResult();

            XmlDocument IXMLFile = OpenXMLDocument(XMLFileStream);
            if (IXMLFile.BaseURI == null) throw new Exception("Не удалось открыть XML-файл.");



            IfcStore? IFCFile = OpenIFCFile(IFCFileStream);
            if (IFCFile == null) throw new Exception("Не удалось открыть IFC-файл.");

            IResult.FileName = IFCFileStream.Name;



            XmlElement? IRoot = IXMLFile.DocumentElement;
            if (IRoot == null) throw new Exception("Не удалось получить корневой элемент в XML-файле: '" + XMLFileStream.Name + "'.");



            string? IIfcFileVersion = IRoot.Attributes.GetNamedItem("ifcVersion").Value;
            if (string.IsNullOrEmpty(IIfcFileVersion)) throw new Exception("Не удалось получить атрибут 'ifcVersion' в XML-файле: '" + XMLFileStream.Name + "'.");



            IResult.VersionCheckResult = new Tuple<bool, XbimSchemaVersion>(CheckSchemaVersion(IFCFile, IIfcFileVersion), IFCFile.SchemaVersion);



            long? IIfcFileSize = long.Parse(IRoot.Attributes.GetNamedItem("fileSize").Value);
            if (IIfcFileSize == null) throw new Exception("Не удалось получить атрибут 'fileSize' в XML-файле: '" + XMLFileStream.Name + "'.");



            IResult.SizeCheckResult = new Tuple<bool, long>(CheckFileSize(IFCFileStream, IRoot.Attributes.GetNamedItem("fileSize").Value), IFCFileStream.Length);




            foreach (XmlElement ICurrentXMLElement in IRoot.ChildNodes)
            {
                switch (ICurrentXMLElement.Name)
                {
                    case "FileNameMask":
                        {
                            IResult.FileMaskCheckResult = CheckFileMask(IFCFileStream, ICurrentXMLElement); break;
                        }
                    case "CategoryElementColors":
                        {
                            (IResult.CategoryColorCheckResult.Item3 as List<Tuple<bool, double, string>>)
                                .Add(CheckCategoryColor(IFCFile, ICurrentXMLElement));
                            break;
                        }
                    case "Coordinates":
                        {
                            (IResult.CoordinatesCheckResult.Item3 as List<Tuple<bool, Tuple<double, double, double, double>, string>>)
                                .Add(CheckCoordinates(IFCFile, ICurrentXMLElement));
                            break;
                        }
                    case "AttributesComparison":
                        {
                            (IResult.AttributesCheckResult.Item3 as List<Tuple<bool, string, string>>)
                                .Add(CheckAttribute());
                            break;
                        }
                    default: throw (new Exception("Неизвестный XML-элемент в XML-файле: '" + XMLFileStream.Name + "'."));
                }
            }



            return IResult;
        }
        XmlDocument OpenXMLDocument(FileStream XMLFileStream)
        {
            XmlDocument XMLDocument = new XmlDocument();

            try
            {
                XMLDocument.Load(XMLFileStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return XMLDocument;
        }
        IfcStore? OpenIFCFile(FileStream IFCFileStream)
        {
            IfcStore? IFCFile = null;

            try
            {
                IFCFile = IfcStore.Open(IFCFileStream, Xbim.IO.StorageType.Ifc, Xbim.IO.XbimModelType.MemoryModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return IFCFile;
        }
        bool CheckSchemaVersion(IfcStore IFCFile, String IFCVersion) => IFCFile.SchemaVersion.ToString() == IFCVersion;
        bool CheckFileSize(FileStream IFCFileStream, String FileSize) => IFCFileStream.Length <= long.Parse(FileSize);
        Tuple<bool, double, IEnumerable<Tuple<bool, string, string>>> CheckFileMask(FileStream IFCFileStream, XmlElement FileNameMaskRule)
        {
            var CheckResults = new List<Tuple<bool, string, string>>();



            char? Separator = FileNameMaskRule.Attributes.GetNamedItem("separator").Value[0];
            if (Separator == null) throw new Exception("Некорректный атрибут 'separator'.");



            string[] FileMasks;
            {
                var fileName = IFCFileStream.Name.Split('\\').Last();
                FileMasks = fileName.Remove(fileName.Length - 4).Split(Separator.Value);
            }


            foreach (XmlElement Mask in FileNameMaskRule.ChildNodes)
            {
                ushort Order;
                if (!ushort.TryParse(Mask.Attributes.GetNamedItem("order").Value, out Order)) throw new Exception("Некорректный атрибут 'order'.");



                string? Type = Mask.Attributes.GetNamedItem("type").Value;
                if (Type == null) throw new Exception("Некорректный атрибут 'type'.");



                switch (Type)
                {
                    case "Свободное значение":
                        {
                            CheckResults.Add(new Tuple<bool, string, string>(FileMasks[Order].Length > 0, Type, FileMasks[Order])); break;
                        }
                    case "Значение из списка":
                        {
                            string[] Values = Mask.Attributes.GetNamedItem("value").Value.Split(';');
                            if (Values.Length < 2) throw new Exception("Некорректное значение атрибута 'value': " + Mask.Attributes.GetNamedItem("value").Value + '.');

                            bool IsAllowed = false;

                            if (Order < FileMasks.Count())
                            {
                                foreach (var Value in Values)
                                {
                                    IsAllowed = FileMasks[Order] == Value;
                                    if (IsAllowed) break;
                                }
                                CheckResults.Add(new Tuple<bool, string, string>(IsAllowed, Type, FileMasks[Order]));
                            }
                            else
                                CheckResults.Add(new Tuple<bool, string, string>(IsAllowed, Type, "'Order' за пределами количества масок"));

                            break;
                        }
                    default: throw new Exception("Некорректное значения атрибута 'type': " + Type + '.');
                }
            }

            ushort CorrectMasksCount = 0;

            foreach (var CheckResult in CheckResults) if (CheckResult.Item1) CorrectMasksCount++;

            return new Tuple<bool, double, IEnumerable<Tuple<bool, string, string>>>
                (CorrectMasksCount == CheckResults.Count,
                (double)CorrectMasksCount / (double)CheckResults.Count,
                CheckResults);
        }
        Tuple<bool, double, string> CheckCategoryColor(IfcStore IFCFile, XmlElement CategoryColorRule)
        {
            string AttributeName = CategoryColorRule.Attributes.GetNamedItem("attributeName").Value;
            if (string.IsNullOrEmpty(AttributeName)) throw new Exception("Некорректный атрибут 'attributeName'.");



            string AttributeValue = CategoryColorRule.Attributes.GetNamedItem("attributeValue").Value;
            if (string.IsNullOrEmpty(AttributeValue)) throw new Exception("Некорректный атрибут 'attributeValue'.");


            Color CategoryColor = Color.Black;

            Type IfcType = null;
            foreach (XmlElement CategoryAttribute in CategoryColorRule.ChildNodes)
            {
                switch (CategoryAttribute.Name)
                {
                    case "Color":
                        {
                            CategoryColor = Color.FromArgb
                                (byte.Parse(CategoryAttribute.Attributes.GetNamedItem("r").Value)
                                , byte.Parse(CategoryAttribute.Attributes.GetNamedItem("g").Value)
                                , byte.Parse(CategoryAttribute.Attributes.GetNamedItem("b").Value));
                            break;
                        }
                    case "ifcClass":
                        {
                            string TypeName = CategoryAttribute.Attributes.GetNamedItem("ifcClass").Value;
                            if (string.IsNullOrEmpty(TypeName)) throw new Exception("Некорректный атрибут 'ifcClass'.");

                            foreach (Assembly Asm in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                IfcType = Asm.GetType("Xbim.Ifc4.Interfaces.I" + TypeName);
                                if (IfcType != null) break;
                            }

                            if (IfcType == null) throw new Exception("Некорректное значение атрибута 'ifcClass': " + TypeName + '.');
                            break;
                        }
                    default: throw new Exception("Неизвестный XML-элемент в 'CategoryElementColors'.");
                }
            }

            var TemplateObjectType = typeof(IfcRuleChecker);
            var Method = TemplateObjectType.GetMethod("CheckColor");
            var Hurr = Method.MakeGenericMethod(IfcType);

            return Hurr.Invoke(Method, new object[] { IFCFile, AttributeName, AttributeValue, CategoryColor }) as Tuple<bool, double, string>;
        }
        static public Tuple<bool, double, string> CheckColor<IIfcLocalType>(IfcStore IFCFile, string AttributeName, string AttributeValue, Color CategotyColor) where IIfcLocalType : IIfcElement
        {
            IEnumerable<IIfcColourRgb> Colours = GetColours<IIfcLocalType>(IFCFile, AttributeName, AttributeValue);

            long CorrectColors = 0;

            foreach (var Colour in Colours)
            {
                Color CurrentColour = Color.FromArgb(
                    (byte)Math.Round(Colour.Red * 255, 0)
                    , (byte)Math.Round(Colour.Green * 255, 0)
                    , (byte)Math.Round(Colour.Blue * 255, 0));

                if (CurrentColour == CategotyColor) CorrectColors++;
            }

            return new(CorrectColors / Colours.Count() == 1, CorrectColors / Colours.Count(), AttributeName);
        }
        static IEnumerable<IIfcColourRgb> GetColours<IIfcLocalType>(IfcStore IFCFile, string AttributeName, string AttributeValue) where IIfcLocalType : IIfcElement
        {
            IEnumerable<IIfcColourRgb> Colours = new List<IIfcColourRgb>();

            foreach (var Element in IFCFile.Instances.OfType<IIfcLocalType>())
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
                    var Color = (((Element
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
        Tuple<bool, Tuple<double, double, double, double>, string> CheckCoordinates(IfcStore IFCFile, XmlElement CoordenatesRule)
        {
            string Name = CoordenatesRule.Attributes.GetNamedItem("name").Value;

            double
                X = double.Parse(CoordenatesRule.Attributes.GetNamedItem("e").Value),
                Y = double.Parse(CoordenatesRule.Attributes.GetNamedItem("n").Value),
                R = double.Parse(CoordenatesRule.Attributes.GetNamedItem("r").Value);

            ushort Z = ushort.Parse(CoordenatesRule.Attributes.GetNamedItem("a").Value);

            var Context = new Xbim3DModelContext(IFCFile.Model);
            Context.CreateContext();

            IIfcElement? IfcElement = IFCFile.Instances.OfType<IIfcElement>().FirstOrDefault(i => i.Name == Name);

            if(IfcElement == null)
            {
                return new(false, new(double.MinValue, double.MinValue, double.MinValue, double.MinValue), Name);
            }

            var GlobalPosition = Context
                .ShapeInstances()
                .FirstOrDefault(i => i.IfcProductLabel == IfcElement.EntityLabel)
                .Transformation
                .Translation;

            var Rottion = Context
                .ShapeInstances()
                .FirstOrDefault(i => i.IfcProductLabel == IfcElement.EntityLabel)
                .Transformation
                .GetRotationQuaternion();

            return new(
                   Y == GlobalPosition.Y
                && X == GlobalPosition.X
                && Z == GlobalPosition.Z
                && R == Rottion.Z, new(Y, X, Z, R), Name); 
        }
        Tuple<bool, string, string> CheckAttribute()
        {
            return new(false, "", "");
        }
    }
    class Program
    {
        static void Main()
        {
            IfcRuleChecker ifcRuleChecker = new IfcRuleChecker();
            try
            {
                ifcRuleChecker.CheckIfcFile(
                    new FileStream("C:\\Users\\matve\\Downloads\\Telegram Desktop\\SYLA_ALL_CPTI_B_1.6_ХХ_24.1.1.1_M6_MF_1_AR22_I4000.ifc", FileMode.Open)
                  , new FileStream("D:\\Практика\\ifcCheckerRules\\TestFile.xml", FileMode.Open));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
