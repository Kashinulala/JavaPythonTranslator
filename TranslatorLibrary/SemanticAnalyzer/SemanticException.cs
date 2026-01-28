namespace TranslatorLibrary.SemanticAnalyzer
{
    // Исключение для семантических ошибок
    public class SemanticException : Exception
    {
        public int Line { get; }
        public int Column { get; }

        public SemanticException(string message, int line = -1, int column = -1)
            : base(message)
        {
            Line = line;
            Column = column;
        }
    }
}
