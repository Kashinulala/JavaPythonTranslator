using Antlr4.Runtime;
using JavaPythonTranslator.Models;

namespace JavaPythonTranslator.Services
{
    public class SyntaxErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        private readonly List<SemanticError> _syntaxErrors = new List<SemanticError>();

        public IReadOnlyList<SemanticError> SyntaxErrors => _syntaxErrors.AsReadOnly();

        // Лексер
        void IAntlrErrorListener<int>.SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var syntaxError = new SemanticError
            {
                Line = line,
                Column = charPositionInLine,
                Message = $"Syntax error (lex): {msg} (at token type: {offendingSymbol})"
            };
            _syntaxErrors.Add(syntaxError);
        }

        // Парсер
        void IAntlrErrorListener<IToken>.SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var syntaxError = new SemanticError
            {
                Line = line,
                Column = charPositionInLine,
                Message = $"Syntax error (parse): {msg} (at token: '{offendingSymbol?.Text ?? "null"}')"
            };
            _syntaxErrors.Add(syntaxError);
        }
        public void ClearErrors()
        {
            _syntaxErrors.Clear();
        }
    }
}