using JavaPythonTranslator.Models;
using JavaPythonTranslator.Services;
using Microsoft.AspNetCore.Mvc;

namespace JavaPythonTranslator.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TranslatorController : ControllerBase
    {
        private readonly IJavaTranslatorService _javaAnalyzerService;

        public TranslatorController(IJavaTranslatorService javaAnalyzerService)
        {
            _javaAnalyzerService = javaAnalyzerService;
        }

        [HttpPost("translate")]
        public async Task<IActionResult> AnalyzeJavaCode([FromBody] TranslatorRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.JavaCode))
            {
                return BadRequest(new TranslatorResponse
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
                return StatusCode(500, new TranslatorResponse
                {
                    Success = false,
                    Message = $"An internal server error occurred: {ex.Message}"
                });
            }
        }
    }
}