using Antlr4.Runtime.Tree;
using Antlr4.Runtime;
using LexerParserLibrary;

string input = File.ReadAllText("../../../JavaCode.java");
try
{
    // Создаем лексер и парсер
    ICharStream stream = CharStreams.fromString(input);
    JavaGrammarLexer lexer = new JavaGrammarLexer(stream);
    ITokenStream tokens = new CommonTokenStream(lexer);
    JavaGrammarParser parser = new JavaGrammarParser(tokens);

    // Создаем слушатель и обходим дерево
    JavaSyntaxAnalyzer analyzer = new JavaSyntaxAnalyzer();
    ParseTreeWalker walker = new ParseTreeWalker();
    walker.Walk(analyzer, parser.compilationUnit());

    // Генерируем отчет
    analyzer.GenerateReport();

    // Выводим результаты
    Console.WriteLine(analyzer.GetAnalysisResult());

    if (analyzer.HasErrors)
    {
        Console.WriteLine("\nSYNTAX ERRORS FOUND:");
        Console.WriteLine(analyzer.GetErrors());
    }
    else
    {
        Console.WriteLine("\nSYNTAX ANALYSIS COMPLETED SUCCESSFULLY. NO ERRORS FOUND.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR DURING ANALYSIS: {ex.Message}");
}
