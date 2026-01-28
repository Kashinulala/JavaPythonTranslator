namespace TranslatorLibrary.SemanticAnalyzer
{
    // Класс для хранения информации о символах
    public class SymbolInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Scope { get; set; }
        public bool IsInitialized { get; set; }
        public bool IsFinal { get; set; }
        public bool IsStatic { get; set; }
    }
}
