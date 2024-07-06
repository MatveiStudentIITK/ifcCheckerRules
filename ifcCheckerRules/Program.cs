using System.Drawing;
using System.Reflection;
using System.Xml;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace IfcRuleChecker
{
    internal class Program
    {
        static bool CheckIfcVersion(IfcStore IIfcStore, XmlElement IIfcVersionRuleXmlElement)
        {
            return IIfcStore.SchemaVersion.ToString() == IIfcVersionRuleXmlElement.InnerText;
        }
        static bool CheckIfcFileSize(FileStream IIfcFileStream, XmlElement IIfcFileSizeRuleXmlElement)
        {
            return IIfcFileStream.Length <= long.Parse(IIfcFileSizeRuleXmlElement.InnerText);
        }
        static bool CheckFileMaskName(FileStream IIfcFileStream, XmlElement IIfcFileMaskNameRuleXmlElement, ref XmlDocument IXmlDocument, out ushort NameFieldsCount, out ushort CorrectNameCount)
        {
            NameFieldsCount = 0;
            CorrectNameCount = 0;

            String[]
                NameFields = null,
                Values = null;

            char Separator;

            int Order = -1;

            String
                Type = null;

            foreach (XmlElement IIfcFileMaskRule in IIfcFileMaskNameRuleXmlElement.ChildNodes)
            {
                switch (IIfcFileMaskRule.Name)
                {
                    case "separator":
                        {
                            Separator = IIfcFileMaskRule.InnerText[0];

                            String FileName = IIfcFileStream.Name.Split('\\').Last();
                            NameFields = FileName.Remove(FileName.Length - 4).Split(Separator);
                            NameFieldsCount = (ushort)NameFields.Length;
                            break;
                        }
                    case "FileNamePlaceholder":
                        {
                            foreach (XmlElement PlaceholderChild in IIfcFileMaskRule.ChildNodes)
                            {
                                switch (PlaceholderChild.Name)
                                {
                                    case "order":
                                        {
                                            Order = int.Parse(PlaceholderChild.InnerText); break;
                                        }
                                    case "type":
                                        {
                                            Type = PlaceholderChild.InnerText; break;
                                        }
                                    case "value":
                                        {
                                            Values = PlaceholderChild.InnerText.Split(';'); break;
                                        }
                                    default: break;
                                }
                            }

                            bool IsPassed = true;

                            if (Type != "свободное значение")
                            {
                                if (NameFieldsCount <= Order)
                                {
                                    IsPassed = false;
                                }
                                else
                                {
                                    String CurrentPlace = NameFields[Order];

                                    IsPassed = false;

                                    foreach (var AllowedVal in Values)
                                        if (CurrentPlace == AllowedVal)
                                            IsPassed = true;
                                }
                            }

                            if (IsPassed) CorrectNameCount++;

                            break;
                        }
                    default: break;
                }
            }

            return (CorrectNameCount / NameFieldsCount) == 1;
        }
        public static IEnumerable<IIfcColourRgb> GetIfcColours<IIfcLocalType>(IfcStore IIfcStore, String AttributeName, String AttributeValue) where IIfcLocalType : IIfcElement
        {
            IEnumerable<IIfcColourRgb> Colours = new List<IIfcColourRgb>();

            foreach (var Element in IIfcStore.Instances.OfType<IIfcLocalType>())
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
        public static Tuple<bool, long, long> CheckCategoryElementColor<IIfcLocalType>(IfcStore IIfcStore, String AttributeName, String AttributeValue, Color CategoryColor) where IIfcLocalType : IIfcElement
        {
            var CurrentCategoryColors = GetIfcColours<IIfcLocalType>(IIfcStore, AttributeName, AttributeValue);

            long CategoryColorCount = CurrentCategoryColors.Count();
            long CorrectCategoryColorsCount = 0;

            foreach (var CurrColor in CurrentCategoryColors)
            {
                if ((double)CurrColor.Red.Value != ((double)CategoryColor.R / 255)
                    || (double)CurrColor.Green.Value != ((double)CategoryColor.G / 255)
                    || (double)CurrColor.Blue.Value != ((double)CategoryColor.B / 255))
                    CorrectCategoryColorsCount++;
            }
            return new Tuple<bool, long, long>(CorrectCategoryColorsCount / CategoryColorCount == 1
                                              , CategoryColorCount
                                              , CorrectCategoryColorsCount);
        }
        static bool CheckCategoryElementColors(IfcStore IIfcStore, XmlElement IIfcCategoryColorsRuleXmlElement, ref XmlDocument IXmlDocument, out long CategoryColorsCount, out long CorrectCategoryColorsCount)
        {
            CategoryColorsCount = 0;
            CorrectCategoryColorsCount = 0;

            string
                AttributeName = null,
                AttributeValue = null;

            Color?
                CategoryColor = null;

            Type
                CurrentIfcClass = null;

            bool
                IsCorrectCategoryColor,
                CheckResult = true;

            foreach (XmlElement ICategoryColorRule in IIfcCategoryColorsRuleXmlElement.ChildNodes)
            {
                switch (ICategoryColorRule.Name)
                {
                    case "attributeName":
                        {
                            AttributeName = ICategoryColorRule.InnerText; break;
                        }
                    case "attributeValue":
                        {
                            AttributeValue = ICategoryColorRule.InnerText; break;
                        }
                    case "Color":
                        {
                            CategoryColor = Color.FromArgb(
                                byte.Parse(ICategoryColorRule.ChildNodes[0].InnerText),
                                byte.Parse(ICategoryColorRule.ChildNodes[1].InnerText),
                                byte.Parse(ICategoryColorRule.ChildNodes[2].InnerText));

                            break;
                        }
                    case "ifcClass":
                        {
                            foreach (Assembly Asm in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                CurrentIfcClass = Asm.GetType("Xbim.Ifc4.Interfaces.I" + ICategoryColorRule.InnerText);

                                if (CurrentIfcClass != null) break;
                            }

                            break;
                        }
                    default: break;
                }
            }

            var TemplateProgramObjectType = typeof(Program);
            var ProgramMethod = TemplateProgramObjectType.GetMethod("CheckCategoryElementColor");
            var Hurr = ProgramMethod.MakeGenericMethod(CurrentIfcClass);

            var TemplateVariable = (Tuple<bool, long, long>)Hurr.Invoke(ProgramMethod, new object[] { IIfcStore, AttributeName, AttributeValue, CategoryColor });

            IsCorrectCategoryColor = TemplateVariable.Item1;
            CategoryColorsCount += TemplateVariable.Item2;
            CorrectCategoryColorsCount += TemplateVariable.Item3;

            CheckResult = CheckResult ? IsCorrectCategoryColor : false;

            CurrentIfcClass = null;


            return CheckResult;
        }
        static bool CheckCoordinatesCheck(IfcStore IIfcStore, XmlElement IIfcCoordinatesRuleXmlElement, ref XmlDocument IXmlDocument)
        {
            String Name = null;
            double n = Double.MinValue, e = Double.MinValue;
            ushort a = ushort.MinValue, r = ushort.MinValue;

            foreach (XmlElement CoorXml in IIfcCoordinatesRuleXmlElement)
            {
                switch (CoorXml.Name)
                {
                    case "name":
                        {
                            Name = CoorXml.InnerText; break;
                        }
                    case "n":
                        {
                            n = double.Parse(CoorXml.InnerText); break;
                        }
                    case "e":
                        {
                            e = double.Parse(CoorXml.InnerText); break;
                        }
                    case "a":
                        {
                            a = ushort.Parse(CoorXml.InnerText); break;
                        }
                    case "r":
                        {
                            r = ushort.Parse(CoorXml.InnerText); break;
                        }
                    default: break;
                }
            }

            var Context = new Xbim3DModelContext(IIfcStore);
            Context.CreateContext();

            IIfcElement IfcElement = IIfcStore.Instances.OfType<IIfcElement>().FirstOrDefault(i => i.Name == Name);

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

            return n == GlobalPosition.Y
                && e == GlobalPosition.X
                && a == GlobalPosition.Z
                && r == Rottion.Z;
        }
        public static bool CheckAttribute<IIfcLocalType>(IfcStore IIfcStore, String AttributeName, String AttributeType, String[] ComparisonType, String[] ComparisonValue) where IIfcLocalType : IIfcElement
        {
            return false;
        }
        static bool CheckAttributesComperisons(IfcStore IIfcStore, XmlElement IIfcAttributesRules, ref XmlDocument IXmlDocument)
        {
            bool CheckResult = true;

            List<Type> IfcTypes = new List<Type>();

            String
                AttributeName = null,
                AttributeType = null;

            List<String>
                ComparisonTypes = new List<String>(),
                ComparisonValues = new List<string>();

            foreach (XmlElement AttributeXmlElement in IIfcAttributesRules.ChildNodes)
            {
                switch (AttributeXmlElement.Name)
                {
                    case "ifcClass":
                        {
                            foreach (Assembly Asm in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                Type IfcType = Asm.GetType("Xbim.Ifc4.Interfaces.I" + AttributeXmlElement.InnerText);

                                if (IfcType != null)
                                {
                                    IfcTypes.Add(IfcType); break;
                                }
                            }
                            break;
                        }
                    case "Attribute":
                        {
                            foreach (XmlElement Child in AttributeXmlElement.ChildNodes)
                            {
                                switch (Child.Name)
                                {
                                    case "name":
                                        {
                                            AttributeName = Child.InnerText; break;
                                        }
                                    case "type":
                                        {
                                            AttributeName = Child.InnerText; break;
                                        }
                                    default: break;
                                }
                            }
                            break;
                        }
                    case "Comparison":
                        {
                            String Type = null, Value = null;

                            foreach (XmlElement Child in AttributeXmlElement.ChildNodes)
                            {
                                switch (Child.Name)
                                {
                                    case "comparisonType":
                                        {
                                            Type = Child.InnerText; break;
                                        }
                                    case "comparisonValue":
                                        {
                                            Value = Child.InnerText; break;
                                        }
                                    default: break;
                                }
                            }

                            ComparisonTypes.Add(Type);
                            ComparisonValues.Add(Value);

                            break;
                        }
                    default: break;
                }
            }

            foreach (var IfcType in IfcTypes)
            {
                var TemplateProgramObjectType = typeof(Program);
                var ProgramMethod = TemplateProgramObjectType.GetMethod("CheckAttribute");
                var Hurr = ProgramMethod.MakeGenericMethod(IfcType);

                var TemplateVariable = (bool)Hurr.Invoke(ProgramMethod, new object[] { IIfcStore, AttributeName, AttributeType, ComparisonTypes, ComparisonValues });

                CheckResult = CheckResult ? TemplateVariable : false;
            }


            return CheckResult;
        }
        static XmlDocument CheckIfcFile(FileStream IIfcFileStream, FileStream IRuleXmlFileStream, ref XmlDocument? IXmlDocument)
        {
            bool
                IsCorrectIfcVersion,
                IsCorrectIfcFileSize,
                IsPassFileNameMaskCheck,
                IsPassCategoryElementColorCheck,
                IsPassCoordinatesCheck,
                IsCorrectAttributeComperisonCheck;

            ushort
                NameFieldsCount,
                CorrectNameCount;

            long
                CategoryColorsCount,
                CorrectCategoryColorsCount;

            XmlDocument IRuleXmlFile = new XmlDocument();
            IRuleXmlFile.Load(IRuleXmlFileStream);

            IfcStore IIfcFileStore = IfcStore.Open(
                IIfcFileStream, Xbim.IO.StorageType.Ifc
                , IRuleXmlFile.DocumentElement.FirstChild.InnerText == "Ifc4"
                ? Xbim.Common.Step21.XbimSchemaVersion.Ifc4
                : Xbim.Common.Step21.XbimSchemaVersion.Ifc2X3
                , Xbim.IO.XbimModelType.MemoryModel);

            foreach (XmlElement IIfcRuleXmlElement in IRuleXmlFile.DocumentElement)
            {
                switch (IIfcRuleXmlElement.Name)
                {
                    case "ifcVersion":
                        {
                            IsCorrectIfcVersion = CheckIfcVersion(IIfcFileStore, IIfcRuleXmlElement); break;
                        }
                    case "fileSize":
                        {
                            IsCorrectIfcFileSize = CheckIfcFileSize(IIfcFileStream, IIfcRuleXmlElement); break;
                        }
                    case "FileNameMask":
                        {
                            IsPassFileNameMaskCheck = CheckFileMaskName(IIfcFileStream, IIfcRuleXmlElement, ref IXmlDocument, out NameFieldsCount, out CorrectNameCount); break;
                        }
                    case "CategoryElementColors":
                        {
                            IsPassCategoryElementColorCheck = CheckCategoryElementColors(IIfcFileStore, IIfcRuleXmlElement, ref IXmlDocument, out CategoryColorsCount, out CorrectCategoryColorsCount); break;
                        }
                    case "Coordinates":
                        {
                            IsPassCoordinatesCheck = CheckCoordinatesCheck(IIfcFileStore, IIfcRuleXmlElement, ref IXmlDocument); break;
                        }
                    case "AttributesComparsion":
                        {
                            IsCorrectAttributeComperisonCheck = CheckAttributesComperisons(IIfcFileStore, IIfcRuleXmlElement, ref IXmlDocument); break;
                        }
                    default: break;
                }
            }

            return new XmlDocument();
        }
        static XmlDocument CheckIfcFiles(FileStream[] IIfcFilesStreams, FileStream[] IRuleXmlFileStream)
        {
            return new XmlDocument();
        }
        static void Main()
        {
            var XmlDocumentResult = new XmlDocument();
            var Result = CheckIfcFile(
                new FileStream("C:\\Users\\matve\\Downloads\\Telegram Desktop\\SYLA_ALL_CPTI_B_1.6_ХХ_24.1.1.1_M6_MF_1_AR22_I4000.ifc", FileMode.Open)
                , new FileStream("D:\\Практика\\ifcCheckerRules\\TestFile.xml", FileMode.Open)
                , ref XmlDocumentResult);
        }
    }
}
