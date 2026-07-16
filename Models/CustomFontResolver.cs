using PdfSharp.Fonts;
using System;
using System.IO;

namespace MenuQr.Models // Đổi tên namespace này cho khớp với project của bạn nếu cần
{
    public class CustomFontResolver : IFontResolver
    {
        public string DefaultFontName => "Arial";

        public byte[] GetFont(string faceName)
        {
            // Trỏ đường dẫn thẳng vào thư mục chứa Font mặc định của hệ điều hành Windows
            string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), faceName);
            
            if (File.Exists(fontPath))
            {
                return File.ReadAllBytes(fontPath);
            }

            // Nếu không tìm thấy, trả về Arial mặc định cho chắc cú
            return File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"));
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Bắt các trường hợp font Arial (In đậm, In nghiêng, Thường)
            if (familyName.Equals("Arial", StringComparison.OrdinalIgnoreCase))
            {
                if (isBold && isItalic) return new FontResolverInfo("arialbi.ttf");
                if (isBold) return new FontResolverInfo("arialbd.ttf");
                if (isItalic) return new FontResolverInfo("ariali.ttf");
                
                return new FontResolverInfo("arial.ttf");
            }

            // Fallback nếu gọi font khác mà không có thì vẫn nhét Arial vào để không bị Crash
            return new FontResolverInfo("arial.ttf");
        }
    }
}