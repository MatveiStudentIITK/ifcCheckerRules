using Internship.Checker;
using System.Xml.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace Internship
{
    internal class Program
    {
        static void Main(string[] args)
        {
            IfcFileChecker checker = new IfcFileChecker();
            XDocument xDocument = new(checker.CheckIfcFile(new FileStream("C:\\Users\\matve\\Downloads\\Telegram Desktop\\SYLA_ALL_CPTI_B_1.6_ХХ_24.1.1.1_M6_MF_1_AR22_I4000.ifc", FileMode.Open), new FileStream("D:\\Практика\\Internship\\TestFile.xml", FileMode.Open)));
            xDocument.Save("TestCheckResult.xml");
        }
    }
}
