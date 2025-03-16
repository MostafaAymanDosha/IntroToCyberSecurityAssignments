using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Steganography.Models;
using Steganography.ServiceContracts;

namespace Steganography.Controllers;

public class SteganographyController : Controller
{
    private readonly ISteganographyService _steganographyService;

    public SteganographyController(ISteganographyService steganographyService)
    {
        _steganographyService = steganographyService;
    }

    public IActionResult Index()
    {
        return View(new SteganographyViewModel());
    }

    [HttpPost]
    public IActionResult Encode(SteganographyViewModel model)
    {
        if (model.ImageFile == null || string.IsNullOrWhiteSpace(model.Message))
        {
            model.OperationResult = "Please provide both an image and a message to encode.";
            return View("Index", model);
        }

        // Check file type
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp" };
        var extension = Path.GetExtension(model.ImageFile.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            model.OperationResult = "Please upload an image file (JPG, PNG, or BMP).";
            return View("Index", model);
        }

        try
        {
            model.OutputImagePath = _steganographyService.EncodeMessage(model.ImageFile, model.Message);
            model.OperationResult = "Message successfully encoded in the image.";
        }
        catch (Exception ex)
        {
            model.OperationResult = $"Error encoding message: {ex.Message}";
        }

        return View("Index", model);
    }

    [HttpPost]
    public IActionResult Decode(SteganographyViewModel model)
    {
        if (model.ImageFile == null)
        {
            model.OperationResult = "Please provide an image to decode.";
            return View("Index", model);
        }

        // Restrict to lossless formats
        var allowedExtensions = new[] { ".png", ".bmp" };
        var extension = Path.GetExtension(model.ImageFile.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            model.OperationResult = "Only PNG or BMP files can be decoded.";
            return View("Index", model);
        }

        try
        {
            string decodedMessage = _steganographyService.DecodeMessage(model.ImageFile);
            model.DecodedMessage = decodedMessage;
            model.OperationResult = "Message decoded successfully!";
            ViewBag.ActiveTab = "decode";
        }
        catch (Exception ex)
        {
            model.OperationResult = $"Error: {ex.Message}";
        }

        return View("Index", model);
    }
}
