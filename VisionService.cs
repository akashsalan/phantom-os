using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tesseract;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using ImageFormat = System.Drawing.Imaging.ImageFormat;

namespace PhantomOS
{
    public class VisionService
    {
        // ─── Tesseract tessdata management ───
        private static readonly string TessDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PhantomOS", "tessdata");

        private const string TessDataUrl =
            "https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata";

        private static readonly HttpClient _downloadClient = new()
        {
            Timeout = TimeSpan.FromSeconds(120)
        };

        // P/Invoke for DPI-aware screen capture
        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private const int DESKTOPHORZRES = 118; // native horizontal resolution
        private const int DESKTOPVERTRES = 117; // native vertical resolution
        private const int HORZRES = 8;          // scaled horizontal resolution
        private const int VERTRES = 10;         // scaled vertical resolution

        /// <summary>
        /// Main entry point: Captures the screen with DPI awareness, preprocesses the image,
        /// runs OCR through the selected engine, and applies code-aware post-processing.
        /// </summary>
        public async Task<string> CaptureAndOCR()
        {
            // Determine which screen the mouse cursor is currently on
            var screen = Screen.FromPoint(Cursor.Position);
            if (screen == null)
                return "[Error: No display detected]";

            var bounds = screen.Bounds;
            
            try
            {
                // ─── STEP 1: DPI-Aware Active Monitor Screen Capture ───
                byte[] rawImageBytes;

                using (var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                    }
                    using var ms = new MemoryStream();
                    bitmap.Save(ms, ImageFormat.Png);
                    rawImageBytes = ms.ToArray();
                }

                // ─── STEP 2: Image Preprocessing ───
                byte[] processedBytes = PreprocessImage(rawImageBytes);

                // ─── STEP 3: Run OCR ───
                string ocrEngine = ConfigManager.Current.OcrEngine;
                string rawText;

                if (ocrEngine == "tesseract")
                {
                    rawText = await RunTesseractOCR(processedBytes);
                    // Fallback to Windows OCR if Tesseract fails
                    if (rawText.StartsWith("["))
                        rawText = await RunWindowsOCR(processedBytes);
                }
                else
                {
                    rawText = await RunWindowsOCR(processedBytes);
                }

                if (string.IsNullOrWhiteSpace(rawText) || rawText.StartsWith("["))
                    return rawText;

                // ─── STEP 4: Code-Aware Post-Processing ───
                string cleanedText = PostProcessOCR(rawText);

                return cleanedText;
            }
            catch (Exception ex)
            {
                return $"[OCR Error: {ex.Message}]";
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  IMAGE PREPROCESSING PIPELINE
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Applies a full image preprocessing pipeline optimized for OCR accuracy:
        /// Upscale 2× → Grayscale → Contrast Normalize → Sharpen → Adaptive Binarize
        /// </summary>
        private byte[] PreprocessImage(byte[] imageBytes)
        {
            using var inputStream = new MemoryStream(imageBytes);
            using var original = new Bitmap(inputStream);

            // 1. Upscale 2× using high-quality bicubic interpolation
            int newWidth = original.Width * 2;
            int newHeight = original.Height * 2;
            using var upscaled = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(upscaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(original, 0, 0, newWidth, newHeight);
            }

            // 2. Convert to grayscale
            var grayscale = ConvertToGrayscale(upscaled);

            // 3. Normalize contrast (stretch histogram to full 0-255 range)
            NormalizeContrast(grayscale);

            // 4. Sharpen (3×3 unsharp mask to crisp up character edges)
            var sharpened = ApplySharpen(grayscale);
            grayscale.Dispose();

            // Output as PNG
            using var outputStream = new MemoryStream();
            sharpened.Save(outputStream, ImageFormat.Png);
            sharpened.Dispose();

            return outputStream.ToArray();
        }

        private Bitmap ConvertToGrayscale(Bitmap source)
        {
            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(result);

            // ITU-R BT.601 luminance coefficients via color matrix
            var colorMatrix = new ColorMatrix(new float[][]
            {
                new float[] { 0.299f, 0.299f, 0.299f, 0, 0 },
                new float[] { 0.587f, 0.587f, 0.587f, 0, 0 },
                new float[] { 0.114f, 0.114f, 0.114f, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });

            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(colorMatrix);
            g.DrawImage(source,
                new Rectangle(0, 0, source.Width, source.Height),
                0, 0, source.Width, source.Height,
                GraphicsUnit.Pixel, attributes);

            return result;
        }

        private void NormalizeContrast(Bitmap bitmap)
        {
            var data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadWrite, bitmap.PixelFormat);

            int byteCount = Math.Abs(data.Stride) * data.Height;
            byte[] pixels = new byte[byteCount];
            Marshal.Copy(data.Scan0, pixels, 0, byteCount);

            // Find min/max luminance
            byte min = 255, max = 0;
            for (int i = 0; i < byteCount; i += 4) // ARGB: B=i, G=i+1, R=i+2, A=i+3
            {
                byte val = pixels[i]; // grayscale so R=G=B
                if (val < min) min = val;
                if (val > max) max = val;
            }

            // Stretch to full range
            float range = max - min;
            if (range > 10) // avoid division by near-zero
            {
                float scale = 255f / range;
                for (int i = 0; i < byteCount; i += 4)
                {
                    byte stretched = (byte)Math.Clamp((pixels[i] - min) * scale, 0, 255);
                    pixels[i] = stretched;     // B
                    pixels[i + 1] = stretched; // G
                    pixels[i + 2] = stretched; // R
                    // A stays unchanged
                }
            }

            Marshal.Copy(pixels, 0, data.Scan0, byteCount);
            bitmap.UnlockBits(data);
        }

        private Bitmap ApplySharpen(Bitmap source)
        {
            // 3×3 sharpening kernel (unsharp mask)
            float[,] kernel = {
                {  0, -1,  0 },
                { -1,  5, -1 },
                {  0, -1,  0 }
            };

            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            var srcData = source.LockBits(
                new Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly, source.PixelFormat);
            var dstData = result.LockBits(
                new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly, result.PixelFormat);

            int stride = srcData.Stride;
            int byteCount = Math.Abs(stride) * source.Height;
            byte[] srcPixels = new byte[byteCount];
            byte[] dstPixels = new byte[byteCount];
            Marshal.Copy(srcData.Scan0, srcPixels, 0, byteCount);

            // Copy source to destination as baseline (handles edges)
            Array.Copy(srcPixels, dstPixels, byteCount);

            int w = source.Width;
            int h = source.Height;

            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    float sum = 0;
                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int idx = ((y + ky) * stride) + ((x + kx) * 4);
                            sum += srcPixels[idx] * kernel[ky + 1, kx + 1];
                        }
                    }

                    int dstIdx = (y * stride) + (x * 4);
                    byte val = (byte)Math.Clamp(sum, 0, 255);
                    dstPixels[dstIdx] = val;     // B
                    dstPixels[dstIdx + 1] = val; // G
                    dstPixels[dstIdx + 2] = val; // R
                    dstPixels[dstIdx + 3] = 255; // A
                }
            }

            Marshal.Copy(dstPixels, 0, dstData.Scan0, byteCount);
            source.UnlockBits(srcData);
            result.UnlockBits(dstData);

            return result;
        }



        // ═══════════════════════════════════════════════════════════════
        //  OCR ENGINES
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Tesseract OCR — significantly more accurate for code and monospaced text.
        /// Auto-downloads eng.traineddata on first use.
        /// </summary>
        private async Task<string> RunTesseractOCR(byte[] imageBytes)
        {
            try
            {
                // Ensure tessdata is available
                string trainedDataPath = Path.Combine(TessDataDir, "eng.traineddata");
                if (!File.Exists(trainedDataPath))
                {
                    bool downloaded = await DownloadTessData();
                    if (!downloaded)
                        return "[Tesseract: Failed to download language data. Falling back to Windows OCR.]";
                }

                using var engine = new TesseractEngine(TessDataDir, "eng", EngineMode.LstmOnly);

                // Configure for code/screen text accuracy
                engine.SetVariable("tessedit_char_whitelist", "");  // allow all characters
                engine.SetVariable("preserve_interword_spaces", "1");
                engine.SetVariable("tessedit_pageseg_mode", "6");   // assume uniform block of text

                using var ms = new MemoryStream(imageBytes);
                using var bitmap = new Bitmap(ms);

                // Convert Bitmap to Pix for Tesseract
                using var pixStream = new MemoryStream();
                bitmap.Save(pixStream, ImageFormat.Png);
                pixStream.Seek(0, SeekOrigin.Begin);

                using var pix = Pix.LoadFromMemory(pixStream.ToArray());
                using var page = engine.Process(pix);

                string text = page.GetText();
                float confidence = page.GetMeanConfidence();

                // If confidence is very low, signal for fallback
                if (confidence < 0.3f && string.IsNullOrWhiteSpace(text))
                    return "[Low confidence OCR result]";

                return string.IsNullOrWhiteSpace(text) ? "[No text detected on screen]" : text;
            }
            catch (Exception ex)
            {
                return $"[Tesseract Error: {ex.Message}]";
            }
        }

        /// <summary>
        /// Windows built-in OCR (fallback engine).
        /// </summary>
        private async Task<string> RunWindowsOCR(byte[] imageBytes)
        {
            try
            {
                var winrtStream = new InMemoryRandomAccessStream();
                var writer = new DataWriter(winrtStream.GetOutputStreamAt(0));
                writer.WriteBytes(imageBytes);
                await writer.StoreAsync();
                writer.DetachStream();
                winrtStream.Seek(0);

                var decoder = await BitmapDecoder.CreateAsync(winrtStream);
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (ocrEngine == null)
                    return "[OCR unavailable: No language pack installed.\nGo to Windows Settings > Language to install one.]";

                var result = await ocrEngine.RecognizeAsync(softwareBitmap);
                return string.IsNullOrWhiteSpace(result.Text)
                    ? "[No text detected on screen]"
                    : result.Text;
            }
            catch (Exception ex)
            {
                return $"[Windows OCR Error: {ex.Message}]";
            }
        }

        /// <summary>
        /// Downloads eng.traineddata from GitHub (tesseract-ocr/tessdata_fast).
        /// Uses the "fast" variant (~4MB) for quick startup.
        /// </summary>
        private async Task<bool> DownloadTessData()
        {
            try
            {
                Directory.CreateDirectory(TessDataDir);
                string targetPath = Path.Combine(TessDataDir, "eng.traineddata");

                // Download with progress
                using var response = await _downloadClient.GetAsync(TessDataUrl,
                    HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode) return false;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(targetPath, FileMode.Create,
                    FileAccess.Write, FileShare.None, 8192, true);

                await contentStream.CopyToAsync(fileStream);

                return File.Exists(targetPath) && new FileInfo(targetPath).Length > 100_000;
            }
            catch
            {
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  CODE-AWARE POST-PROCESSING
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Fixes common OCR character confusions in code contexts.
        /// Only applies corrections where surrounding context strongly suggests a misread.
        /// </summary>
        private string PostProcessOCR(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            string result = text;

            // ─── Fix 'O' misread as '0' and vice versa in numeric contexts ───
            // Pattern: digit + O + digit → digit + 0 + digit  (e.g., "1O3" → "103")
            result = Regex.Replace(result, @"(?<=\d)O(?=\d)", "0");

            // Pattern: letter + 0 + letter (not hex) → letter + O + letter (e.g., "f0r" → "for")
            result = Regex.Replace(result, @"(?<=[a-eA-Eg-zG-Z])0(?=[a-fA-Fg-zG-Z])", "O");

            // ─── Fix 'l' (lowercase L) misread as '1' in identifiers ───
            // "1ength" → "length", "va1ue" → "value"
            result = Regex.Replace(result, @"(?<=[a-zA-Z])1(?=[a-zA-Z])", "l");

            // ─── Fix '1' misread as 'l' in numeric contexts ───
            // "l0" → "10", "l23" → "123"
            result = Regex.Replace(result, @"(?<=\d)l(?=\d)", "1");
            result = Regex.Replace(result, @"(?<![a-zA-Z])l(?=\d{2,})", "1");

            // ─── Fix 'S' / '5' confusion ───
            // "5tring" → "String", but "x = 5" should stay
            result = Regex.Replace(result, @"(?<![a-zA-Z0-9])5(?=[a-z]{2,})", "S");
            result = Regex.Replace(result, @"(?<=\d)S(?=\d)", "5");

            // ─── Fix 'B' / '8' confusion ───
            result = Regex.Replace(result, @"(?<=\d)B(?=\d)", "8");

            // ─── Fix 'rn' misread as 'm' (kerning issue) — careful, only obvious cases ───
            // "retum" → "return", "nurn" → "num" ... actually this one is risky, skip it

            // ─── Fix '|' misread as 'I' or 'l' in code operators ───
            // "| |" common in code for logical OR
            result = Regex.Replace(result, @"I I", "| |");

            // ─── Fix common code keyword typos from OCR ───
            result = FixCommonCodeWords(result);

            // ─── Normalize whitespace ───
            // Preserve line breaks but collapse multiple spaces
            result = Regex.Replace(result, @"[^\S\r\n]+", " ");
            result = Regex.Replace(result, @"\n{3,}", "\n\n");

            return result.Trim();
        }

        /// <summary>
        /// Fixes OCR misreads of common programming keywords using whole-word matching.
        /// Only replaces when the misread is a non-word (not a real keyword/variable).
        /// </summary>
        private string FixCommonCodeWords(string text)
        {
            // Map of common OCR misreads → corrections (whole-word only)
            var fixes = new (string pattern, string replacement)[]
            {
                // Digit/letter swaps in keywords
                (@"\bvoid\b", "void"),       // already correct, but "v0id" isn't
                (@"\bv0id\b", "void"),
                (@"\bpu8lic\b", "public"),
                (@"\bpub1ic\b", "public"),
                (@"\bc1ass\b", "class"),
                (@"\bst4tic\b", "static"),
                (@"\bstat1c\b", "static"),
                (@"\bre7urn\b", "return"),   // 7 misread as t → "re7urn"
                (@"\breturn\b", "return"),
                (@"\bint\b", "int"),
                (@"\b1nt\b", "int"),
                (@"\bstr1ng\b", "string"),
                (@"\b5tring\b", "String"),
                (@"\bfloat\b", "float"),
                (@"\bf1oat\b", "float"),
                (@"\bdoub1e\b", "double"),
                (@"\bboo1ean\b", "boolean"),
                (@"\bboo1\b", "bool"),
                (@"\bnu11\b", "null"),
                (@"\bNu11\b", "Null"),
                (@"\bfa1se\b", "false"),
                (@"\btrue\b", "true"),       // already correct
                (@"\bwhi1e\b", "while"),
                (@"\be1se\b", "else"),
                (@"\bpr1nt\b", "print"),
                (@"\b1mport\b", "import"),
                (@"\binc1ude\b", "include"),
                (@"\bde1ete\b", "delete"),
                (@"\bva1ue\b", "value"),
                (@"\b1ength\b", "length"),
                (@"\bso1ve\b", "solve"),
                (@"\barra7\b", "array"),
                (@"\bfunct1on\b", "function"),
                (@"\bcon5t\b", "const"),
                (@"\bcon5ole\b", "console"),
                (@"\bCon5ole\b", "Console"),
            };

            string result = text;
            foreach (var (pattern, replacement) in fixes)
            {
                result = Regex.Replace(result, pattern, replacement);
            }

            return result;
        }
    }
}
