using System.Drawing;
using System.Reflection;
using System.Xml;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace ifcCheckerRules
{
    internal class Program
    {
        struct CheckResult
        {
            string FileName;
            double ColorCheckResult, MaskCheckResult, AttributeCheckResult;
            bool VersionCheckResult, SizeCheckResult;
        }
        static void Main()
        {
            CheckIfcRules(new FileStream("C:\\Users\\matve\\Downloads\\Telegram Desktop\\SYLA_ALL_CPTI_B_1.6_ХХ_24.1.1.1_M6_MF_1_AR22_I4000.ifc"
                , FileMode.Open)
                , new FileStream("D:\\Практика\\ifcCheckerRules\\TestFile.xml"
                , FileMode.Open));
        }
        static XmlDocument CheckIfcRules(FileStream IIfcFileStream, FileStream IXmlFileStream)
        {
            IfcStore IIfcStore = IfcStore.Open(
                IIfcFileStream,
                Xbim.IO.StorageType.Ifc,
                Xbim.Common.Step21.XbimSchemaVersion.Ifc4,
                Xbim.IO.XbimModelType.MemoryModel);

            XmlDocument XmlRules = new XmlDocument();
            XmlRules.Load(IXmlFileStream);

            XmlDocument Result = new XmlDocument();

            Result.CreateElement("IfcCheckResults");

            XmlElement ResultRoot = Result.DocumentElement;

            var ColorCheck = CheckColorsRules(IIfcStore, XmlRules.DocumentElement);

            return Result;
        }
        static bool CheckVersionRule(IfcStore IIfcStore, XmlElement IXmlVersionRule)
        {
            return IIfcStore.SchemaVersion.ToString() == IXmlVersionRule.InnerText;
        }
        static bool CheckFileSizeRule(FileStream IIfcFileStream, XmlElement IXmlFileSizeRule)
        {
            return IIfcFileStream.Length <= long.Parse(IXmlFileSizeRule.InnerText);
        }
        static double CheckFileNameMaskRule(FileStream IIfcFileStream, XmlElement IXmlFileNameMaskRule)
        {
            char Separator = char.MinValue;

            foreach (XmlNode IXmlFileNameRule in IXmlFileNameMaskRule)
            {
                if (IXmlFileNameRule.Name == "FileNameMask")
                {
                    foreach (XmlElement IXmlFileMaskElement in IXmlFileNameRule.ChildNodes)
                    {
                        switch (IXmlFileMaskElement.Name)
                        {
                            case "separator": { Separator = IXmlFileMaskElement.InnerText[0]; break; }

                            case "FileNamePlaceholder":
                                {
                                    foreach (XmlElement IXmlFileNamePlace in IXmlFileMaskElement.ChildNodes)
                                    {
                                        switch (IXmlFileNamePlace.Name)
                                        {
                                            case "order": { break; }

                                            case "type": { break; }

                                            case "value": { break; }

                                            default: break;
                                        }
                                    }

                                    break;
                                }

                            default: break;
                        }
                    }
                }
            }

            return 0;
        }
        static double CheckColorsRules(IfcStore IIfcStore, XmlElement IXmlColorRules)
        {
            double ElementsCount = 0, CorrectElementsCount = 0;
            foreach (XmlNode IXmlColorRule in IXmlColorRules)
            {
                if (IXmlColorRule.Name == "CategoryElementColors")
                {
                    String AttributeName = null;
                    String AttributeValue = null;
                    Color CategoryColor = Color.White;
                    Type IfcClass = null;

                    foreach (XmlElement IXmlCategoryElementColor in IXmlColorRule.ChildNodes)
                    {
                        switch (IXmlCategoryElementColor.Name)
                        {
                            case "attributeName": { AttributeName = IXmlCategoryElementColor.InnerText; break; }

                            case "attributeValue": { AttributeValue = IXmlCategoryElementColor.InnerText; break; }

                            case "Color":
                                {
                                    int R = 0, G = 0, B = 0;

                                    foreach (XmlElement XmlColor in IXmlCategoryElementColor.ChildNodes)
                                    {
                                        if (XmlColor.Name == "r") R = int.Parse(XmlColor.InnerText);
                                        if (XmlColor.Name == "g") G = int.Parse(XmlColor.InnerText);
                                        if (XmlColor.Name == "b") B = int.Parse(XmlColor.InnerText);
                                    }

                                    CategoryColor = Color.FromArgb(R, G, B);

                                    break;
                                }

                            case "ifcClass":
                                {
                                    foreach (Assembly Asm in AppDomain.CurrentDomain.GetAssemblies())
                                    {
                                        IfcClass = Asm.GetType("Xbim.Ifc4.Interfaces." + IXmlCategoryElementColor.InnerText);
                                        if (IfcClass != null) break;
                                    }
                                    break;
                                }

                            default: break;
                        }
                    }

                    var Templ = typeof(Program);
                    var Method = Templ.GetMethod("GetIfcColours");
                    var Hurr = Method.MakeGenericMethod(IfcClass);
                    var TemplClass = new Program();

                    IEnumerable<IIfcColourRgb> ElementsColours = Hurr.Invoke(TemplClass, new object[] { IIfcStore, AttributeName, AttributeValue }) as IEnumerable<IIfcColourRgb>;

                    ElementsCount += (ushort)ElementsColours.Count();

                    foreach (var c in ElementsColours)
                    {
                        if ((double)c.Red.Value == (double)(CategoryColor.R / byte.MaxValue)) CorrectElementsCount++;
                    }
                }
            }
            return CorrectElementsCount / ElementsCount;
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
        static double CheckAttributesRules(IfcStore IIfcStore, XmlElement IXmlAttributesRule)
        {
            return 0;
        }
    }
}
