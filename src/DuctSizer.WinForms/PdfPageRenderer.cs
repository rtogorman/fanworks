using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DuctSizer.WinForms;

internal readonly record struct PdfPageInfo(float WidthPoints, float HeightPoints);

internal sealed class PdfPageRenderSession : IDisposable
{
    private static readonly object PdfiumSync = new();
    private static bool _initialized;
    private IntPtr _document;

    internal PdfPageRenderSession(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A PDF path is required.", nameof(path));
        lock (PdfiumSync)
        {
            EnsureInitialized();
            _document = PdfiumNative.FPDF_LoadDocument(path, IntPtr.Zero);
            if (_document == IntPtr.Zero)
                throw new InvalidDataException(PdfiumErrorMessage(PdfiumNative.FPDF_GetLastError()));
            PageCount = PdfiumNative.FPDF_GetPageCount(_document);
            if (PageCount <= 0)
            {
                Dispose();
                throw new InvalidDataException("The selected PDF does not contain a renderable page.");
            }
        }
    }

    internal int PageCount { get; }

    internal PdfPageInfo GetPageInfo(int pageIndex)
    {
        lock (PdfiumSync)
        {
            var page = LoadPage(pageIndex);
            try
            {
                return new PdfPageInfo(PdfiumNative.FPDF_GetPageWidthF(page),
                    PdfiumNative.FPDF_GetPageHeightF(page));
            }
            finally
            {
                PdfiumNative.FPDF_ClosePage(page);
            }
        }
    }

    internal Bitmap RenderPage(int pageIndex, int maximumDimension)
    {
        if (maximumDimension is < 200 or > 8192)
            throw new ArgumentOutOfRangeException(nameof(maximumDimension));
        lock (PdfiumSync)
        {
            var page = LoadPage(pageIndex);
            try
            {
                var widthPoints = PdfiumNative.FPDF_GetPageWidthF(page);
                var heightPoints = PdfiumNative.FPDF_GetPageHeightF(page);
                if (!float.IsFinite(widthPoints) || !float.IsFinite(heightPoints)
                    || widthPoints <= 0 || heightPoints <= 0)
                    throw new InvalidDataException("The selected PDF page has invalid dimensions.");
                var scale = maximumDimension / Math.Max(widthPoints, heightPoints);
                var width = Math.Max(1, (int)Math.Round(widthPoints * scale));
                var height = Math.Max(1, (int)Math.Round(heightPoints * scale));
                var bitmapHandle = PdfiumNative.FPDFBitmap_Create(width, height, 1);
                if (bitmapHandle == IntPtr.Zero)
                    throw new InvalidOperationException("FanWorks could not allocate the PDF page image.");
                try
                {
                    PdfiumNative.FPDFBitmap_FillRect(bitmapHandle, 0, 0, width, height, 0xFFFFFFFF);
                    PdfiumNative.FPDF_RenderPageBitmap(bitmapHandle, page, 0, 0, width, height, 0,
                        PdfiumNative.FpdfAnnot | PdfiumNative.FpdfLcdText);
                    var buffer = PdfiumNative.FPDFBitmap_GetBuffer(bitmapHandle);
                    var stride = PdfiumNative.FPDFBitmap_GetStride(bitmapHandle);
                    if (buffer == IntPtr.Zero || stride < width * 4)
                        throw new InvalidOperationException("FanWorks could not read the rendered PDF page.");
                    using var view = new Bitmap(width, height, stride, PixelFormat.Format32bppArgb, buffer);
                    return new Bitmap(view);
                }
                finally
                {
                    PdfiumNative.FPDFBitmap_Destroy(bitmapHandle);
                }
            }
            finally
            {
                PdfiumNative.FPDF_ClosePage(page);
            }
        }
    }

    public void Dispose()
    {
        lock (PdfiumSync)
        {
            if (_document == IntPtr.Zero)
                return;
            PdfiumNative.FPDF_CloseDocument(_document);
            _document = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }

    private IntPtr LoadPage(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_document == IntPtr.Zero, this);
        if (pageIndex < 0 || pageIndex >= PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        var page = PdfiumNative.FPDF_LoadPage(_document, pageIndex);
        if (page == IntPtr.Zero)
            throw new InvalidDataException($"PDF page {pageIndex + 1} could not be rendered.");
        return page;
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
            return;
        PdfiumNative.FPDF_InitLibrary();
        _initialized = true;
    }

    private static string PdfiumErrorMessage(uint error) => error switch
    {
        2 => "The selected file is not a valid PDF.",
        3 => "The selected PDF could not be read because its format is unsupported.",
        4 => "The selected PDF is password protected. Remove the password before importing it.",
        5 => "The selected PDF uses an unsupported security handler.",
        6 => "The selected PDF page could not be found.",
        _ => "The selected PDF could not be opened."
    };

    private static class PdfiumNative
    {
        internal const int FpdfAnnot = 0x01;
        internal const int FpdfLcdText = 0x02;
        private const string Library = "pdfium.dll";

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern void FPDF_InitLibrary();

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern IntPtr FPDF_LoadDocument(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string filePath, IntPtr password);

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern uint FPDF_GetLastError();

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern int FPDF_GetPageCount(IntPtr document);

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern float FPDF_GetPageWidthF(IntPtr page);

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern float FPDF_GetPageHeightF(IntPtr page);

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern IntPtr FPDFBitmap_Create(int width, int height, int alpha);

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern int FPDFBitmap_FillRect(IntPtr bitmap, int left, int top,
            int width, int height, uint color);

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page,
            int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern int FPDFBitmap_GetStride(IntPtr bitmap);

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern void FPDFBitmap_Destroy(IntPtr bitmap);

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern void FPDF_ClosePage(IntPtr page);

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
        internal static extern void FPDF_CloseDocument(IntPtr document);
    }
}
