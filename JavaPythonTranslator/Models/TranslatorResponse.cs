namespace JavaPythonTranslator.Models
{
    public class TranslatorResponse
    {
        public bool Success { get; set; }
        public List<SemanticError> Errors { get; set; } = new List<SemanticError>();
        public string Message { get; set; } = string.Empty;
        public string TranslatedCode { get; set; } = string.Empty;
    }
}
