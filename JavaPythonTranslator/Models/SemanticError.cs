namespace JavaPythonTranslator.Models
{
    public class SemanticError
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
