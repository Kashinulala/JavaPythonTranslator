<<<<<<<< HEAD:TranslatorLibrary/SemanticAnalyzer/SemanticErrorsReport/SemanticReport.cs
﻿namespace TranslatorLibrary.SemanticAnalyzer.SemanticErrorsReport
========
﻿namespace TranslatorLibrary.SemanticAnalyzer.SemanticErrorReport
>>>>>>>> 13d7b904bdfd577efe51ac866aa81f7265d8c7cd:TranslatorLibrary/SemanticAnalyzer/SemanticErrorReport/SemanticReport.cs
{
    public class SemanticReport
    {
        public List<ReportItem> Errors { get; set; } = new List<ReportItem>();
        public List<ReportItem> Warnings { get; set; } = new List<ReportItem>();
        public string AnalysisDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public string FileName { get; set; } = "unknown";
    }
}
