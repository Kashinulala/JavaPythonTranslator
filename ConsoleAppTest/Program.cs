using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using LexerParserLibrary;

string javaCode = File.ReadAllText("../../../JavaCode.java");

// 1. Создаем лексер и парсер
ICharStream stream = CharStreams.fromString(javaCode);
JavaGrammarLexer lexer = new JavaGrammarLexer(stream);
ITokenStream tokens = new CommonTokenStream(lexer);
JavaGrammarParser parser = new JavaGrammarParser(tokens);

// 2. Строим дерево разбора
IParseTree tree = parser.compilationUnit();

// 3. Проверяем, что синтаксический анализ прошел успешно
if (parser.NumberOfSyntaxErrors > 0)
{
    Console.WriteLine("Syntax errors found. Aborting semantic analysis.");
    return;
}

// 4. Запускаем семантический анализ
SemanticAnalyzer analyzer = new SemanticAnalyzer();
analyzer.Visit(tree);

// 5. Выводим результаты
if (analyzer.HasErrors)
{
    Console.WriteLine("SEMANTIC ERRORS FOUND:");
    foreach (var error in analyzer.Errors)
    {
        Console.WriteLine($"Line {error.Line}, Column {error.Column}: {error.Message}");
    }
}
else
{
    Console.WriteLine("SEMANTIC ANALYSIS COMPLETED SUCCESSFULLY. NO ERRORS FOUND.");
}