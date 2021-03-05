namespace RemoveUnused
{
    internal class Issue
    {
        public string TypeId { get; set; }
        public string File { get; set; }
        public int From { get; set; }
        public int To { get; set; }
        public int Line { get; set; }
        public string Message { get; set; }
    }
}