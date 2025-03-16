using Steganography.ServiceContracts;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace Steganography.Services
{
    public class SteganographyService : ISteganographyService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly string _uploadsFolder;
        private const int HeaderSize = 32; // 32 bits for message length

        public SteganographyService(IWebHostEnvironment environment)
        {
            _environment = environment;
            _uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");

            if (!Directory.Exists(_uploadsFolder))
            {
                Directory.CreateDirectory(_uploadsFolder);
            }
        }

        public string EncodeMessage(IFormFile imageFile, string message)
        {
            // Generate file paths
            var originalFileName = Guid.NewGuid().ToString() + ".png";
            var filePath = Path.Combine(_uploadsFolder, originalFileName);
            string outputPath = Path.Combine(_uploadsFolder, "encoded_" + originalFileName);

            try
            {
                // Convert message to byte array using UTF-8 encoding
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);

                // Validate message length against image capacity
                int maxBytes = GetMaxCapacity(imageFile) / 8; // Convert bits to bytes
                if (messageBytes.Length > maxBytes)
                {
                    throw new InvalidOperationException($"Message is too large for the selected image. Maximum capacity: {maxBytes} bytes.");
                }

                // Create a bitmap from the uploaded image
                using Bitmap bitmap = CreateBitmapFromUpload(imageFile, filePath);

                // Encode message length (in bytes) into the first 32 pixels
                int messageLength = messageBytes.Length;
                EncodeInt32(bitmap, 0, 0, messageLength);

                // Encode actual message bytes into the image
                EncodeByteArray(bitmap, messageBytes, HeaderSize);

                // Save the encoded image
                bitmap.Save(outputPath, ImageFormat.Png);

                // Clean up the original file
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                return "/uploads/" + Path.GetFileName(outputPath);
            }
            catch (Exception ex)
            {
                // Clean up any files if an error occurs
                CleanupFiles(filePath, outputPath);
                throw new Exception($"Error encoding message: {ex.Message}", ex);
            }
        }

        public string DecodeMessage(IFormFile imageFile)
        {
            var filePath = Path.Combine(_uploadsFolder, Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName));

            try
            {
                // Create a bitmap from the uploaded image
                using Bitmap bitmap = CreateBitmapFromUpload(imageFile, filePath);

                // Decode the message length from the first 32 pixels
                int messageLength = DecodeInt32(bitmap, 0, 0);

                // Validate the decoded length
                if (messageLength < 0 || messageLength > (bitmap.Width * bitmap.Height - HeaderSize) / 8)
                {
                    throw new InvalidOperationException("Invalid message length detected. The image may not contain valid steganographic data.");
                }

                // Decode the message bytes
                byte[] messageBytes = DecodeByteArray(bitmap, messageLength, HeaderSize);

                // Convert bytes back to string using UTF-8 encoding
                string decodedMessage = Encoding.UTF8.GetString(messageBytes);

                return decodedMessage;
            }
            catch (Exception ex)
            {
                // Clean up the temporary file
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { /* Ignore cleanup errors */ }
                }
                throw new Exception($"Error decoding message: {ex.Message}", ex);
            }
            finally
            {
                // Ensure file cleanup
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { /* Ignore cleanup errors */ }
                }
            }
        }

        #region Helper Methods

        private Bitmap CreateBitmapFromUpload(IFormFile imageFile, string filePath)
        {
            // Save the uploaded file temporarily
            using (var fileStreamm = new FileStream(filePath, FileMode.Create))
            {
                imageFile.CopyTo(fileStreamm);
            }

            // Load the image and convert to 32bpp ARGB format
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var tempImage = Image.FromStream(fileStream);

            // Create a new bitmap with 32bpp ARGB format for consistent pixel manipulation
            var bitmap = new Bitmap(tempImage.Width, tempImage.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.DrawImage(tempImage, 0, 0);
            }

            return bitmap;
        }

        private void EncodeInt32(Bitmap bitmap, int startX, int startY, int value)
        {
            // Convert the integer to a 32-bit binary string
            string binaryValue = Convert.ToString(value, 2).PadLeft(32, '0');

            int bitIndex = 0;
            for (int y = startY; y < bitmap.Height && bitIndex < 32; y++)
            {
                for (int x = (y == startY ? startX : 0); x < bitmap.Width && bitIndex < 32; x++)
                {
                    // Get the current pixel
                    Color pixel = bitmap.GetPixel(x, y);

                    // Modify the least significant bit of the red channel
                    int r = (pixel.R & 0xFE) | (binaryValue[bitIndex] == '1' ? 1 : 0);

                    // Set the modified pixel
                    bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, r, pixel.G, pixel.B));

                    bitIndex++;
                }
            }
        }

        private void EncodeByteArray(Bitmap bitmap, byte[] data, int startOffset)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int totalPixels = width * height;

            // Calculate starting position after the header
            int startY = startOffset / width;
            int startX = startOffset % width;

            int bitIndex = 0;
            int totalBits = data.Length * 8;

            // For each byte in the data
            for (int i = 0; i < data.Length; i++)
            {
                // Convert each byte to 8 bits
                string byteBits = Convert.ToString(data[i], 2).PadLeft(8, '0');

                // Encode each bit
                for (int j = 0; j < 8; j++)
                {
                    int pixelOffset = startOffset + bitIndex;
                    if (pixelOffset >= totalPixels)
                    {
                        throw new InvalidOperationException("Message exceeds image capacity.");
                    }

                    int y = pixelOffset / width;
                    int x = pixelOffset % width;

                    // Get pixel color
                    Color pixel = bitmap.GetPixel(x, y);

                    // Modify red channel's least significant bit
                    int r = (pixel.R & 0xFE) | (byteBits[j] == '1' ? 1 : 0);

                    // Update pixel
                    bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, r, pixel.G, pixel.B));

                    bitIndex++;
                }
            }
        }

        private int DecodeInt32(Bitmap bitmap, int startX, int startY)
        {
            StringBuilder binaryBuilder = new StringBuilder(32);

            int bitIndex = 0;
            for (int y = startY; y < bitmap.Height && bitIndex < 32; y++)
            {
                for (int x = (y == startY ? startX : 0); x < bitmap.Width && bitIndex < 32; x++)
                {
                    // Get pixel color
                    Color pixel = bitmap.GetPixel(x, y);

                    // Extract the least significant bit from the red channel
                    char bit = (pixel.R & 1) == 1 ? '1' : '0';
                    binaryBuilder.Append(bit);

                    bitIndex++;
                }
            }

            // Convert binary string to integer
            try
            {
                return Convert.ToInt32(binaryBuilder.ToString(), 2);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to decode message length.", ex);
            }
        }

        private byte[] DecodeByteArray(Bitmap bitmap, int byteCount, int startOffset)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int totalPixels = width * height;

            byte[] result = new byte[byteCount];

            for (int byteIndex = 0; byteIndex < byteCount; byteIndex++)
            {
                StringBuilder byteBits = new StringBuilder(8);

                // Extract 8 bits for each byte
                for (int bit = 0; bit < 8; bit++)
                {
                    int pixelOffset = startOffset + (byteIndex * 8) + bit;
                    if (pixelOffset >= totalPixels)
                    {
                        throw new InvalidOperationException("Message extends beyond image bounds.");
                    }

                    int y = pixelOffset / width;
                    int x = pixelOffset % width;

                    // Get pixel and extract LSB from red channel
                    Color pixel = bitmap.GetPixel(x, y);
                    char bit_value = (pixel.R & 1) == 1 ? '1' : '0';
                    byteBits.Append(bit_value);
                }

                // Convert 8 bits to a byte
                result[byteIndex] = Convert.ToByte(byteBits.ToString(), 2);
            }

            return result;
        }

        private int GetMaxCapacity(IFormFile imageFile)
        {
            using (var stream = imageFile.OpenReadStream())
            using (var image = Image.FromStream(stream))
            {
                // Total available bits minus header size (32 bits)
                return (image.Width * image.Height) - HeaderSize;
            }
        }

        private void CleanupFiles(params string[] filePaths)
        {
            foreach (var path in filePaths)
            {
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch { /* Ignore deletion errors */ }
                }
            }
        }

        #endregion
    }
}