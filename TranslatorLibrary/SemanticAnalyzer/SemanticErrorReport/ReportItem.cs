namespace TranslatorLibrary.SemanticAnalyzer.SemanticErrorReport
{
    public class ReportItem
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public string Message { get; set; }
        public string Severity { get; set; } // "Error" или "Warning"
    }
}
