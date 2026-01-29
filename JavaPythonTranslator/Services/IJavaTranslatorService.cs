using JavaPythonTranslator.Models;

namespace JavaPythonTranslator.Services
{
    public interface IJavaTranslatorService
    {
        Task<TranslatorResponse> AnalyzeCodeAsync(string javaCode);
    }
}