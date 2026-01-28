<<<<<<<< HEAD:TranslatorLibrary/SemanticAnalyzer/SemanticErrorsReport/ReportItem.cs
﻿namespace TranslatorLibrary.SemanticAnalyzer.SemanticErrorsReport
========
﻿namespace TranslatorLibrary.SemanticAnalyzer.SemanticErrorReport
>>>>>>>> 13d7b904bdfd577efe51ac866aa81f7265d8c7cd:TranslatorLibrary/SemanticAnalyzer/SemanticErrorReport/ReportItem.cs
{
    public class ReportItem
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public string Message { get; set; }
        public string Severity { get; set; } // "Error" или "Warning"
    }
}
