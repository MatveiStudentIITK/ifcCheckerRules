using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Xml.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace Internship.Checker
{
    internal partial class IfcFileChecker
    {
        internal XElement CheckIfcFile(FileStream IFCFileStream, FileStream XMLFileStream)
        {
            var IFCFile = IfcStore.Open(IFCFileStream, Xbim.IO.StorageType.Ifc, Xbim.IO.XbimModelType.MemoryModel)
                ?? throw new Exception("Не удалось открыть IFC-файл: '" + IFCFileStream.Name + "'.");

            var RuleXmlDocument = XDocument.Load(XMLFileStream)
                ?? throw new Exception("Не удалось загрузить XML-файл: '" + XMLFileStream.Name + "'.");

            var IfcCheckerRules = RuleXmlDocument.Root
                ?? throw new Exception("XML-файл: '" + XMLFileStream.Name + "' не содержит корневого элемента.");

            var CheckResultXmlElement = new XElement("FileCheckResult",
                new XAttribute("ModelName", GetModelName(IFCFileStream)),
                new XAttribute("ifcVersion", IFCFile.SchemaVersion.ToString()),
                new XAttribute("ifcVersionCheckResult", CheckIfcVersion(IFCFile, IfcCheckerRules.Attribute("ifcVersion").Value)),
                new XAttribute("fileSize", IFCFileStream.Length.ToString()),
                new XAttribute("fileSizeCheckResult", CheckIfcFileSize(IFCFileStream, long.Parse(IfcCheckerRules.Attribute("fileSize").Value))));

            foreach (XElement IfcCheckerRule in IfcCheckerRules.Elements())
            {
                switch (IfcCheckerRule.Name.LocalName)
                {
                    case "FileNameMask":
                        {
                            CheckResultXmlElement.Add(CheckFileNameMask(IFCFileStream, IfcCheckerRule)); break;
                        }
                    case "CategoryElementColor":
                        {
                            CheckResultXmlElement.Add(CheckCategoryElementColor(IFCFile, IfcCheckerRule)); break;
                        }
                    case "Coordinates":
                        {
                            CheckResultXmlElement.Add(CheckCoordinates(IFCFile, IfcCheckerRule)); break;
                        }
                    case "AttributesComparison":
                        {
                            CheckResultXmlElement.Add(); break;
                        }
                    default: throw new Exception("XML-файл: '" + XMLFileStream.Name + "' содержит неизвестный XML-элемент: '" + IfcCheckerRule.Name + "'.");
                }
            }

            return CheckResultXmlElement;
        }
        string GetModelName(FileStream IFCFileStream) => IFCFileStream.Name.Split('\\').Last().Split('.').First();
        static string AcceptedOrNot(bool Result) => Result ? "Пройдена" : "Не пройдена";
        string CheckIfcVersion(IfcStore IFCFile, string IfcVersion) => AcceptedOrNot(IFCFile.SchemaVersion.ToString() == IfcVersion);
        string CheckIfcFileSize(FileStream IFCFileStream, long MaxFileSize) => AcceptedOrNot(IFCFileStream.Length <= MaxFileSize);
        XElement CheckFileNameMask(FileStream IFCFileStream, XElement FileNameMaskRule)
        {
            var CheckResult = new XElement("FileNameMaskCheckResult",
                new XAttribute("Result", "")
                , new XAttribute("Procent", ""));

            char Separator = FileNameMaskRule.Attribute("separator").Value[0];

            string[] NameMasks = GetModelName(IFCFileStream).Split(Separator);

            long CorrectCount = 0;

            foreach (var FileNamePlaceHolder in FileNameMaskRule.Elements())
            {
                XElement CurrentCheckResult = new("SingleMaskCheckResult",
                    new XAttribute("Result", "")
                , new XAttribute("Type", "")
                , new XAttribute("Value", ""));

                var Order = ushort.Parse(FileNamePlaceHolder.Attribute("order").Value);

                var TargetType = FileNamePlaceHolder.Attribute("type").Value;

                CurrentCheckResult.Attribute("Type").Value = TargetType;

                string[] TargetValues = null;

                if (Order < NameMasks.Count())
                {
                    switch (TargetType)
                    {
                        case "Свободное значение":
                            {
                                CurrentCheckResult.Attribute("Result").Value = AcceptedOrNot(NameMasks[Order].Length > 0);

                                CurrentCheckResult.Attribute("Value").Value = NameMasks[Order];

                                if (NameMasks[Order].Length > 0) CorrectCount++; break;
                            }
                        case "Значение из списка":
                            {
                                TargetValues = FileNamePlaceHolder.Attribute("value").Value.Split(';');

                                bool IsAccepted = false;

                                foreach (var TargetValue in TargetValues)
                                    if (TargetValue == NameMasks[Order]) { IsAccepted = true; break; }

                                CurrentCheckResult.Attribute("Result").Value = AcceptedOrNot(IsAccepted);

                                CurrentCheckResult.Attribute("Value").Value = NameMasks[Order];

                                if (IsAccepted) CorrectCount++; break;
                            }
                        default: throw new Exception("XML-элемент: '" + FileNamePlaceHolder.Name.LocalName + "' содержит некорректное значение атрибута 'type': '" + TargetType + "'.");
                    }
                }
                else
                {
                    CurrentCheckResult.Attribute("Result").Value = AcceptedOrNot(false);

                    CurrentCheckResult.Attribute("Value").Value = "Вне диапазона 'Order':" + Order.ToString(); break;
                }

                CheckResult.Add(CurrentCheckResult);
            }

            CheckResult.Attribute("Result").Value = AcceptedOrNot((CorrectCount / NameMasks.Count()) == 1);

            CheckResult.Attribute("Procent").Value = (CorrectCount / NameMasks.Count()).ToString();

            return CheckResult;
        }
        XElement CheckCategoryElementColor(IfcStore IFCFile, XElement CategoryElementColorRule)
        {
            var CheckResult = new XElement("CategoryElementColorCheckResult",
                new XAttribute("Result", "")
                , new XAttribute("Procent", "")
                , new XAttribute("Name", ""));

            string AttributeName
                = CheckResult.Attribute("Name").Value
                = CategoryElementColorRule.Attribute("attributeName").Value;

            string AttributeValue = CategoryElementColorRule.Attribute("attributeValue").Value;

            byte Red = byte.Parse(CategoryElementColorRule.Element("Color").Attribute("r").Value)

                , Green = byte.Parse(CategoryElementColorRule.Element("Color").Attribute("g").Value)

                , Blue = byte.Parse(CategoryElementColorRule.Element("Color").Attribute("b").Value);

            Color CategoryColor = Color.FromArgb(Red, Green, Blue);

            string IfcClassName = "Xbim.Ifc4.Interfaces.I" + CategoryElementColorRule.Element("ifcClass").Attribute("ifcClass").Value;

            Type IfcClass = null;

            foreach (Assembly Asm in AppDomain.CurrentDomain.GetAssemblies())
                if ((IfcClass = Asm.GetType(IfcClassName)) != null) break;

            if (IfcClass == null) throw new Exception("XML-элемент: 'ifcClass' содержит некорректное значение атрибута 'ifcClass': '" + CategoryElementColorRule.Element("ifcClass").Attribute("ifcClass").Value + "'.");

            var ObjectType = typeof(IfcFileChecker);
            var Method = ObjectType.GetMethod("GetColors");
            var Hurr = Method.MakeGenericMethod(IfcClass);

            var Colors = Hurr.Invoke(Method, [IFCFile, AttributeName, AttributeValue]) as List<Color>;

            long CorrectElementColorCount = 0;

            foreach (var ElementColor in Colors)
                if (ElementColor == CategoryColor) CorrectElementColorCount++;

            CheckResult.Attribute("Result").Value = AcceptedOrNot((CorrectElementColorCount / Colors.Count) == 1);

            CheckResult.Attribute("Procent").Value = (CorrectElementColorCount / Colors.Count).ToString();

            return CheckResult;
        }
        static List<Color> GetColors<IIfcLocalType>(IfcStore IFCFile, string AttributeName, string AttributeValue) where IIfcLocalType : IIfcElement
        {
            List<Color> Colors = new();

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
                    var Colour = (((Element
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

                    Colors.Add(Color.FromArgb(
                    (byte)Math.Round(Colour.Red * 255, 0)
                    , (byte)Math.Round(Colour.Green * 255, 0)
                    , (byte)Math.Round(Colour.Blue * 255, 0)));
                }
            }

            return Colors;
        }
        XElement CheckCoordinates(IfcStore IFCFile, XElement CoordinatesRule)
        {
            string Name = CoordinatesRule.Attribute("name").Value;

            double N = double.Parse(CoordinatesRule.Attribute("n").Value)
                , E = double.Parse(CoordinatesRule.Attribute("e").Value)
                , A = double.Parse(CoordinatesRule.Attribute("a").Value)
                , R = double.Parse(CoordinatesRule.Attribute("r").Value);

            var Context = new Xbim3DModelContext(IFCFile.Model);
            Context.CreateContext();

            IIfcElement IfcElement = IFCFile.Instances.OfType<IIfcElement>().FirstOrDefault(i => i.Name == Name)
                ?? throw new Exception("Модели с именем: '" + Name + "' нет.");

            if (IfcElement == null)
            {
                return new("CoordinatesCheckResult"
                    , new XAttribute("Result", AcceptedOrNot(false))
                    , new XAttribute("NCheckResult", "Модель не была найдена")
                    , new XAttribute("n", "Модель не была найдена")
                    , new XAttribute("ECheckResult", "Модель не была найдена")
                    , new XAttribute("e", "Модель не была найдена")
                    , new XAttribute("ACheckResult", "Модель не была найдена")
                    , new XAttribute("a", "Модель не была найдена")
                    , new XAttribute("RCheckResult", "Модель не была найдена")
                    , new XAttribute("r", "Модель не была найдена"));
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

            return new("CoordinatesCheckResult"
                , new XAttribute("Result", AcceptedOrNot(N == GlobalPosition.X && E == GlobalPosition.Y && A == GlobalPosition.Z && R == Rottion.Z))
                , new XAttribute("NCheckResult", AcceptedOrNot(N == GlobalPosition.X))
                , new XAttribute("n", GlobalPosition.X)
                , new XAttribute("ECheckResult", AcceptedOrNot(E == GlobalPosition.Y))
                , new XAttribute("e", GlobalPosition.Y)
                , new XAttribute("ACheckResult", AcceptedOrNot(A == GlobalPosition.Z))
                , new XAttribute("a", GlobalPosition.Z)
                , new XAttribute("RCheckResult", AcceptedOrNot(R == Rottion.Z))
                , new XAttribute("r", Rottion.Z));
        }
        XElement CheckAttributesComparison(IfcStore IFCFile, XElement AttributesComparisonRule)
        {
            XElement CheckResult = new("AttributesComparisonCheckResult"
                , new XAttribute("Result", "")
                , new XAttribute("Procent", ""));

            List<Type> IfcClasses = new();

            List<Tuple<string, string>> Comparisons = new();

            string AttributeName = null
                , AttributeType = null;

            foreach(XElement AttributesComparison in AttributesComparisonRule.Elements())
            {
                switch(AttributesComparison.Name.LocalName)
                {
                    case "ifcClass":
                        {
                            string IfcClassName = AttributesComparison.Attribute("ifcClass").Value;

                            Type IfcCLass = null;

                            foreach (Assembly Asm in AppDomain.CurrentDomain.GetAssemblies())
                                if ((IfcCLass = Asm.GetType(IfcClassName)) != null) break;

                            if (IfcCLass == null) throw new Exception("XML-элемент: 'ifcClass' содержит некорректное значение атрибута 'ifcClass': '" + AttributesComparison.Attribute("ifcClass").Value + "'.");
                            else IfcClasses.Add(IfcCLass);
                            break;
                        }
                    case "Attribute":
                        {
                            AttributeName = AttributesComparison.Attribute("name").Value;
                            AttributeType = AttributesComparison.Attribute("type").Value;
                            break;
                        }
                    case "Comparison":
                        {
                            Comparisons.Add(new(
                                AttributesComparison.Attribute("comparisonType").Value
                                , AttributesComparison.Attribute("comparisonValue").Value));
                            break;
                        }
                    default: throw new Exception("Неизвестный XML-элемент: '" + AttributesComparison.Name.LocalName + "' в '" + AttributesComparisonRule.Name.LocalName + "'.");
                }
            }

            long CorrectAttributesCount = 0;

            foreach(var IfcClass in IfcClasses)
            {
                var ObjectType = typeof(IfcFileChecker);
                var Method = ObjectType.GetMethod("GetColors");
                var Hurr = Method.MakeGenericMethod(IfcClass);

                foreach(var Comparison in Comparisons)
                {
                    CheckResult.Add(Hurr.Invoke(Method, [IFCFile, AttributeName, AttributeType, Comparison.Item1, Comparison.Item2]) as XElement);

                    if ((CheckResult.LastNode as XElement).Attribute("Result").Value == "Пройдена") CorrectAttributesCount++;
                }
            }

            return CheckResult;
        }
        static XElement CompareAttributes<IIfcLocalType>(IfcStore IFCFile, string AttributeName, string AttributeType, string ComparisonType, string ComparisonValue) where IIfcLocalType : IIfcElement
        {
            XElement CheckResult = new("SingleCheckResult"
                , new XAttribute("Result", "")
                , new XAttribute("Procent", ""));

            long CorrectComparisonsCount = 0;

            foreach (var Element in IFCFile.Instances.OfType<IIfcLocalType>())
            {
                var Properties = Element
                    .IsDefinedBy
                    .Where(p => p.RelatingPropertyDefinition is IIfcPropertySet)
                    .SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties)
                    .OfType<IIfcPropertySingleValue>();

                var CurrentValue = Properties
                    .Where(p => p.Name == AttributeName)
                    .FirstOrDefault()?
                    .NominalValue
                    .Value
                    .ToString();

                bool IsPass = false;

                switch (AttributeType)
                {
                    case "Text":
                        {
                            if (IsPass = CompareValues(CurrentValue, ComparisonValue, ComparisonType)) CorrectComparisonsCount++;
                            CheckResult.Add(new XElement("CheckResult"
                                , new XAttribute("IfcType", typeof(IIfcLocalType).Name.Remove(0, 1))
                                , new XAttribute("AttributeName", AttributeName)
                                , new XAttribute("Result", AcceptedOrNot(IsPass))));
                            break;
                        }
                    case "Real":
                        {
                            if (IsPass = CompareValues(double.Parse(CurrentValue), double.Parse(ComparisonValue), ComparisonType)) CorrectComparisonsCount++;
                            CheckResult.Add(new XElement("CheckResult"
                                , new XAttribute("IfcType", typeof(IIfcLocalType).Name.Remove(0, 1))
                                , new XAttribute("AttributeName", AttributeName)
                                , new XAttribute("Result", AcceptedOrNot(IsPass))));
                            break;
                        }
                    case "Length":
                        {
                            if (IsPass = CompareValues(ulong.Parse(CurrentValue), ulong.Parse(ComparisonValue), ComparisonType)) CorrectComparisonsCount++;
                            CheckResult.Add(new XElement("CheckResult"
                                , new XAttribute("IfcType", typeof(IIfcLocalType).Name.Remove(0, 1))
                                , new XAttribute("AttributeName", AttributeName)
                                , new XAttribute("Result", AcceptedOrNot(IsPass))));
                            break;
                        }
                    case "Boolean":
                        {
                            if (IsPass = CompareValues(bool.Parse(CurrentValue), bool.Parse(ComparisonValue), ComparisonType)) CorrectComparisonsCount++;
                            CheckResult.Add(new XElement("CheckResult"
                                , new XAttribute("IfcType", typeof(IIfcLocalType).Name.Remove(0, 1))
                                , new XAttribute("AttributeName", AttributeName)
                                , new XAttribute("Result", AcceptedOrNot(IsPass))));
                            break;
                        }
                    default: throw new Exception("Неизвестный тип атрибута: '" + AttributeType + "'.");
                }
            }

            return new("");
        }
        static bool CompareValues<T>(T A, T B, string ComparisonType) where T : IComparable<T>, IComparable
        {
            switch (ComparisonType)
            {
                case "Соответствие списку":
                    {
                        string aValue = A as string;
                        string[] bValues = (B as string).Split(';');

                        foreach (var Value in bValues) if (aValue == Value) return true;

                        return false;
                    }
                case "<": return A.CompareTo(B) < 0;
                case "<=": return A.CompareTo(B) <= 0;
                case "==": return A.CompareTo(B) == 0;
                case ">=": return A.CompareTo(B) >= 0;
                case ">": return A.CompareTo(B) > 0;
                default: throw new Exception("Неизвестный тип сравнения: '" + ComparisonType + "'.");
            }
        }
    }
}
