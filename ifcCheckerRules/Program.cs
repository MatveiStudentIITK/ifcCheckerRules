using System.Drawing;
using System.Reflection;
using System.Xml;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

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

            String[] NameFields;

            char Separator;

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
        public static bool CheckCategoryElementColor<IIfcLocalType>(IfcStore IIfcStore, String AttributeName, String AttributeValue, Color CategoryColor) where IIfcLocalType : IIfcElement
        {
            var CurrentCategoryColors = GetIfcColours<IIfcLocalType>(IIfcStore, AttributeName, AttributeValue);

            foreach (var CurrColor in CurrentCategoryColors)
                if ((double)CurrColor.Red.Value != ((double)CategoryColor.R / 255)
                    || (double)CurrColor.Green.Value != ((double)CategoryColor.G / 255)
                    || (double)CurrColor.Blue.Value != ((double)CategoryColor.B / 255))
                    return false;

            return true;
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

                if (CurrentIfcClass != null)
                {
                    var TemplateProgramObjectType = typeof(Program);
                    var ProgramMethod = TemplateProgramObjectType.GetMethod("CheckCategoryElementColor");
                    var Hurr = ProgramMethod.MakeGenericMethod(CurrentIfcClass);

                    IsCorrectCategoryColor = (bool)Hurr.Invoke(ProgramMethod, new object[] { IIfcStore, AttributeName, AttributeValue, CategoryColor });

                    CheckResult = CheckResult ? IsCorrectCategoryColor : false;
                }
            }

            return CheckResult;
        }
        static bool CheckCoordinatesCheck(IfcStore IIfcStore, XmlElement IIfcCoordinatesRuleXmlElement, ref XmlDocument IXmlDocument)
        {
            return false;
        }
        static XmlDocument CheckIfcFile(FileStream IIfcFileStream, FileStream IRuleXmlFileStream, ref XmlDocument IXmlDocument)
        {
            bool
                IsCorrectIfcVersion,
                IsCorrectIfcFileSize,
                IsPassFileNameMaskCheck,
                IsPassCategoryElementColorCheck,
                IsPassCoordinatesCheck;

            ushort
                NameFieldsCount,
                CorrectNameCount;

            long
                CategoryColorsCount,
                CorrectCategoryColorsCount;

            IfcStore IIfcFileStore = IfcStore.Open(IIfcFileStream, Xbim.IO.StorageType.Ifc, Xbim.Common.Step21.XbimSchemaVersion.Ifc4, Xbim.IO.XbimModelType.MemoryModel);

            XmlDocument IRuleXmlFile = new XmlDocument();
            IRuleXmlFile.Load(IRuleXmlFileStream);

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
                            IsPassCoordinatesCheck = CheckCoordinatesCheck(IIfcFileStore, IIfcRuleXmlElement, ref IXmlDocument);
                            break;
                        }
                    case "AttributesComparsion":
                        {
                            break;
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