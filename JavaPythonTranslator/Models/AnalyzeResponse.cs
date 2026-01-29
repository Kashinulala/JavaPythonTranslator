namespace JavaPythonTranslator.Models
{
    public class AnalyzeResponse
    {
        public bool Success { get; set; }
        public List<SemanticError> Errors { get; set; } = new List<SemanticError>();
        public string Message { get; set; } = string.Empty;
    }
}
