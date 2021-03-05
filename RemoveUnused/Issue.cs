using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace RemoveUnused
{
    public class Issue
    {
        public Issue(string typeId, string file, int start, int end, int line, string message)
        {
            TypeId = typeId;
            File = file;
            Start = start;
            End = end;
            Line = line;
            Message = message;
        }
        public string TypeId { get; }
        public string File { get; }
        public int Start { get; }
        public int End { get; }
        public int Line { get; }
        public string Message { get; }

        public static IEnumerable<Issue> ParseReSharperReportText(string xml, string solutionDir)
        {
            var doc = XDocument.Parse(xml);
            var issues = doc.Elements("Report")?.Elements("Issues")?.Elements("Project").Elements("Issue")
                .Select(e => new Issue(
                    e.Attribute("TypeId")?.Value,
                    Path.Combine(solutionDir, e.Attribute("File")?.Value),
                    int.Parse(e.Attribute("Offset").Value.Split('-')[0]),
                    int.Parse(e.Attribute("Offset").Value.Split('-')[1]),
                    int.Parse(e.Attribute("Line")?.Value),
                    e.Attribute("Message")?.Value
                ));
            return issues.ToArray();
        }
    }
}