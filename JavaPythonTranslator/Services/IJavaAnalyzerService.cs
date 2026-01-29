using JavaPythonTranslator.Models;

namespace JavaPythonTranslator.Services
{
    public interface IJavaAnalyzerService
    {
        Task<AnalyzeResponse> AnalyzeCodeAsync(string javaCode);
    }
}