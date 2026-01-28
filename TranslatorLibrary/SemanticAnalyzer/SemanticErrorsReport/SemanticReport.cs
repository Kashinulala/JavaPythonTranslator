namespace TranslatorLibrary.SemanticAnalyzer.SemanticErrorReport
{
    public class SemanticReport
    {
        public List<ReportItem> Errors { get; set; } = new List<ReportItem>();
        public List<ReportItem> Warnings { get; set; } = new List<ReportItem>();
        public string AnalysisDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public string FileName { get; set; } = "unknown";
    }
}
