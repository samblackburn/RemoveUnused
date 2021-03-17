using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace RemoveUnused
{
    public class ForReal
    {
        [Test, Explicit("Not a test, actually removes unused methods")]
        public void RemoveUnusedMethods()
        {
            DoTheThing(true);
        }

        [Test]
        public void IterateUnusedMethods()
        {
            DoTheThing(false);
        }

        private static void DoTheThing(bool writeOutChanges)
        {
            var solutionDir = @"E:\repos\SQLCompareEngine\";
            var reportFile = @"E:\repos\RemoveUnused\Code Issues in 'SQLCompareEngine'.xml";
            var xml = File.ReadAllText(reportFile, Encoding.Default);
            StringAssert.StartsWith("<?xml", xml);
            var issues = Issue.ParseReSharperReportText(xml, solutionDir)
                .Where(i => i.TypeId.StartsWith("UnusedMember."))
                .OrderBy(i => i.File)
                .OrderByDescending(i => i.Start) // Process end of file first so offsets don't get mangled
                .ToList();
            foreach (var issue in issues)
            {
                var cs = File.ReadAllText(issue.File);
                var modified = CSharpParsing.RemoveMethodWhoseNameIsAtPosition(cs, issue.Start, issue.End);
                if (writeOutChanges)
                {
                    File.WriteAllText(issue.File, modified);
                }
            }
        }
    }
}
