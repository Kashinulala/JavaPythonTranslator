using JavaPythonTranslator.Models;
using JavaPythonTranslator.Services;
using Microsoft.AspNetCore.Mvc;

namespace JavaPythonTranslator.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TranslatorController : ControllerBase
    {
        private readonly IJavaAnalyzerService _javaAnalyzerService;

        public TranslatorController(IJavaAnalyzerService javaAnalyzerService)
        {
            _javaAnalyzerService = javaAnalyzerService;
        }

        [HttpPost("analyze")]
        public async Task<IActionResult> AnalyzeJavaCode([FromBody] AnalyzeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.JavaCode))
            {
                return BadRequest(new AnalyzeResponse
                {
                    Success = false,
                    Message = "Java code is required."
                });
            }

            try
            {
                var result = await _javaAnalyzerService.AnalyzeCodeAsync(request.JavaCode);
                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new AnalyzeResponse
                {
                    Success = false,
                    Message = $"An internal server error occurred: {ex.Message}"
                });
            }
        }
    }
}