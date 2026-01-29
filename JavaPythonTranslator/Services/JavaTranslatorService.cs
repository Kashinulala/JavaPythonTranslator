using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using JavaPythonTranslator.Models;
using TranslatorLibrary.CodeGenerator;
using TranslatorLibrary.SemanticAnalyzer;

namespace JavaPythonTranslator.Services
{
    public class JavaTranslatorService : IJavaTranslatorService
    {
        public async Task<TranslatorResponse> AnalyzeCodeAsync(string javaCode)
        {
            if (string.IsNullOrWhiteSpace(javaCode))
            {
                return new TranslatorResponse
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

                lexer.RemoveErrorListeners();
                lexer.AddErrorListener(syntaxErrorListener);

                ITokenStream tokens = new CommonTokenStream(lexer);
                JavaGrammarParser parser = new JavaGrammarParser(tokens);
                parser.RemoveErrorListeners();
                parser.AddErrorListener(syntaxErrorListener);

                IParseTree tree = parser.compilationUnit();

                var syntaxErrors = syntaxErrorListener.SyntaxErrors.ToList();
                if (syntaxErrors.Any())
                {
                    return new TranslatorResponse
                    {
                        Success = false,
                        Message = "Syntax errors found.",
                        Errors = syntaxErrors
                    };
                }

                SemanticAnalyzer analyzer = new SemanticAnalyzer();
                analyzer.Visit(tree);

                var response = new TranslatorResponse();
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
                    JavaCodeGenerator generator = new JavaCodeGenerator(analyzer.GetSymbolTable());
                    generator.Generate(tree);
                    string pythonCode = generator.GetGeneratedCode();
                    response.Success = true;
                    response.Message = "Code translated successfully.";
                    response.TranslatedCode = pythonCode;
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