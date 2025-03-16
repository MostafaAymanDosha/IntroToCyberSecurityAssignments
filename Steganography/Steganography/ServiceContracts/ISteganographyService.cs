namespace Steganography.ServiceContracts
{
    public interface ISteganographyService
    {
        string EncodeMessage(IFormFile imageFile, string message);
        string DecodeMessage(IFormFile imageFile);
    }
}
