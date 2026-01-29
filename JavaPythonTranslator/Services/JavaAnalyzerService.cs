using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using JavaPythonTranslator.Models;
using TranslatorLibrary.SemanticAnalyzer;

namespace JavaPythonTranslator.Services
{
    public class JavaAnalyzerService : IJavaAnalyzerService
    {
        public async Task<AnalyzeResponse> AnalyzeCodeAsync(string javaCode)
        {
            if (string.IsNullOrWhiteSpace(javaCode))
            {
                return new AnalyzeResponse
                {
                    Success = false,
                    Message = "Java code is required."
                };
            }

            try
            {
                var syntaxErrorListener = new SyntaxErrorListener();

                ICharStream stream = CharStreams.fromString(javaCode);
                JavaGrammarLexer lexer = new JavaGrammarLexer(stream);
                // --- Устанавливаем слушатель для лексера ---
                lexer.RemoveErrorListeners(); // Удаляем стандартные слушатели (например, ConsoleErrorListener)
                lexer.AddErrorListener(syntaxErrorListener); // <-- Теперь должно работать

                ITokenStream tokens = new CommonTokenStream(lexer);
                JavaGrammarParser parser = new JavaGrammarParser(tokens);
                // --- Устанавливаем слушатель для парсера ---
                parser.RemoveErrorListeners(); // Удаляем стандартные слушатели
                parser.AddErrorListener(syntaxErrorListener); // <-- Теперь должно работать

                // 2. Строим дерево разбора
                IParseTree tree = parser.compilationUnit();

                // 3. Проверяем синтаксические ошибки, собранные слушателем
                var syntaxErrors = syntaxErrorListener.SyntaxErrors.ToList(); // Копируем список
                if (syntaxErrors.Any())
                {
                    return new AnalyzeResponse
                    {
                        Success = false,
                        Message = "Syntax errors found.",
                        Errors = syntaxErrors // Возвращаем синтаксические ошибки
                    };
                }

                // 4. Если синтаксических ошибок нет, запускаем семантический анализ
                SemanticAnalyzer analyzer = new SemanticAnalyzer();
                analyzer.Visit(tree); // Это заполняет analyzer.Errors

                // 5. Формируем ответ для семантического анализа
                var response = new AnalyzeResponse();
                // Сначала добавим возможные синтаксические ошибки, если они возникли *после* построения дерева (редко, но возможно)
                syntaxErrors = syntaxErrorListener.SyntaxErrors.ToList();
                if (syntaxErrors.Any())
                {
                    response.Success = false;
                    response.Message = "Syntax errors found after initial parse.";
                    response.Errors.AddRange(syntaxErrors);
                    return response;
                }

                if (analyzer.HasErrors)
                {
                    response.Success = false;
                    response.Message = "Semantic errors found.";
                    // Добавляем семантические ошибки
                    foreach (var error in analyzer.Errors)
                    {
                        response.Errors.Add(new SemanticError
                        {
                            Line = error.Line,
                            Column = error.Column,
                            Message = error.Message
                        });
                    }
                }
                else
                {
                    response.Success = true;
                    response.Message = "Analysis completed successfully. No syntax or semantic errors found.";
                }

                return response;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}