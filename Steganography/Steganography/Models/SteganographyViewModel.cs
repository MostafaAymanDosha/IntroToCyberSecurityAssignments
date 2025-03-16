namespace Steganography.Models
{
    public class SteganographyViewModel
    {
        public string? Message { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string? OutputImagePath { get; set; }
        public string? DecodedMessage { get; set; }
        public string? OperationResult { get; set; }
    }
}
