using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using AventusSharp.Routes.Request;
using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using FileTypeChecker;
using FileTypeChecker.Abstracts;
using FileTypeChecker.Extensions;
using FileTypeChecker.Types;
using SkiaSharp;
using SKSvg = Svg.Skia.SKSvg;

namespace AventusSharp.Data.CustomTableMembers;

public class ImageFile
{
    public static ResultWithImageFileError<SKEncodedImageFormat> GetFormat(string path)
    {
        ResultWithImageFileError<SKEncodedImageFormat> result = new ResultWithImageFileError<SKEncodedImageFormat>();
        try
        {
            using (var fileStream = File.OpenRead(path))
            {
                if (FileTypeValidator.IsTypeRecognizable(fileStream))
                {
                    IFileType fileType = FileTypeValidator.GetFileType(fileStream);
                    string extension = fileType.Extension;
                    string[] exts = Enum.GetNames<SKEncodedImageFormat>();
                    bool found = false;
                    foreach (string ext in exts)
                    {
                        if (ext.ToLower() == extension)
                        {
                            result.Result = Enum.Parse<SKEncodedImageFormat>(ext);
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        result.Errors.Add(new ImageFileError(ImageFileErrorCode.NotValidImage, "The file " + path + " isn't recognized"));
                    }
                }

            }
        }
        catch (Exception e)
        {
            result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknownError, e));
        }
        return result;
    }
    public static ResultWithImageFileError<string> Compress(string path, int? maxHeight, int? maxWidth, string? savePath = null)
    {
        ResultWithImageFileError<string> result = new ResultWithImageFileError<string>();
        try
        {
            ResultWithImageFileError<bool> isSvgResult = IsSvg(path);
            if (!isSvgResult.Success)
            {
                result.Errors = isSvgResult.Errors;
                return result;
            }

            if (isSvgResult.Result)
            {
                if (savePath != null)
                {
                    File.Copy(path, savePath);
                    path = savePath;
                }
                result.Result = path;
                return result;
            }

            long previousSize = new FileInfo(path).Length;
            using FileStream sourceStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using SKData data = SKData.Create(sourceStream);
            using SKCodec codec = SKCodec.Create(data);
            SKEncodedImageFormat format = codec.EncodedFormat;

            using SKBitmap skImage = SKBitmap.Decode(data);
            int sourceWidth = skImage.Width;
            // Get the image current height
            int sourceHeight = skImage.Height;
            if ((maxHeight == null || sourceHeight < maxHeight) && (maxWidth == null || sourceWidth < maxWidth))
            {
                result.Result = path;
                return result;
            }


            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;
            // Calculate width and height with new desired size
            nPercentW = maxWidth == null ? 1 : (float)maxWidth / (float)sourceWidth;
            nPercentH = maxHeight == null ? 1 : (float)maxHeight / (float)sourceHeight;
            nPercent = Math.Min(nPercentW, nPercentH);
            // New Width and Height
            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);
            using SKBitmap scaledBitmap = skImage.Resize(new SKImageInfo(destWidth, destHeight), new SKSamplingOptions(SKFilterMode.Nearest));
            using SKImage image = SKImage.FromBitmap(scaledBitmap);
            using SKData encodedImage = image.Encode(format, 75);

            if (savePath == null)
            {
                savePath = path;
            }

            if (encodedImage.Size < previousSize)
            {
                List<string> pathWithExtension = savePath.Split('.').ToList();
                pathWithExtension.RemoveAt(pathWithExtension.Count - 1);
                string? extensionName = Enum.GetName(format);
                if (extensionName == null)
                {
                    result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknownError, "File format isn't inside extension enum"));
                    return result;
                }
                pathWithExtension.Add(extensionName.ToLower());
                string newPath = string.Join(".", pathWithExtension);

                using (FileStream fs = new FileStream(newPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    encodedImage.SaveTo(fs);
                    result.Result = newPath;
                }
            }
            else
            {
                result.Result = path;
            }

        }
        catch (Exception e)
        {
            result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknownError, e));
        }
        return result;

    }
    public static ResultWithImageFileError<string> Resize(string path, int? width = null, int? height = null, string? savePath = null)
    {
        ResultWithImageFileError<string> result = new ResultWithImageFileError<string>();
        try
        {

            ResultWithImageFileError<bool> isSvgResult = IsSvg(path);
            if (!isSvgResult.Success)
            {
                result.Errors = isSvgResult.Errors;
                return result;
            }
            if (isSvgResult.Result)
            {
                if (savePath != null)
                {
                    File.Copy(path, savePath);
                    path = savePath;
                }
                result.Result = path;
                return result;
            }

            using FileStream sourceStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using SKData data = SKData.Create(sourceStream);
            using SKCodec codec = SKCodec.Create(data);
            SKEncodedImageFormat format = codec.EncodedFormat;

            using SKBitmap skImage = SKBitmap.Decode(data);

            int sourceWidth = skImage.Width;
            // Get the image current height
            int sourceHeight = skImage.Height;
            if (sourceHeight == height && sourceWidth == width)
            {
                result.Result = path;
                return result;
            }
            int destWidth, destHeight;
            if (width != null && height != null)
            {
                destWidth = (int)width;
                destHeight = (int)height;
            }
            else if (width != null && height == null)
            {
                destWidth = (int)width;
                destHeight = destWidth * sourceHeight / sourceWidth;
            }
            else if (width == null && height != null)
            {
                destHeight = (int)height;
                destWidth = destHeight * sourceWidth / sourceHeight;
            }
            else
            {
                result.Errors.Add(new ImageFileError(ImageFileErrorCode.NoSize, "You must provide at least width or height to resize"));
                return result;
            }



            using SKBitmap scaledBitmap = skImage.Resize(new SKImageInfo(destWidth, destHeight), new SKSamplingOptions(SKFilterMode.Nearest));
            using SKImage image = SKImage.FromBitmap(scaledBitmap);
            using SKData encodedImage = image.Encode(format, 100);

            if (savePath == null)
            {
                savePath = path;
            }

            using (FileStream fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                encodedImage.SaveTo(fs);
                result.Result = savePath;
            }
        }
        catch (Exception e)
        {
            result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknownError, e));
        }
        return result;
    }
    public static ResultWithImageFileError<string> Transform(string path, ImageSize size, string? savePath = null)
    {
        ResultWithImageFileError<string> result = new ResultWithImageFileError<string>();
        if (size.Height != null || size.Width != null)
        {
            return Resize(path, size.Width, size.Height, savePath);
        }
        else if (size.MaxHeight != null || size.MaxWidth != null)
        {
            return Compress(path, size.MaxHeight, size.MaxWidth, savePath);
        }
        return result;
    }

    public static ResultWithImageFileError<string> SvgTo(string path, SKEncodedImageFormat format, int width, int height, string? savePath = null)
    {
        ResultWithImageFileError<string> result = new ResultWithImageFileError<string>();
        try
        {
            ResultWithImageFileError<bool> isSvgResult = IsSvg(path);
            if (!isSvgResult.Success)
            {
                result.Errors = isSvgResult.Errors;
                return result;
            }
            if (!isSvgResult.Result)
            {
                result.Errors.Add(new ImageFileError(ImageFileErrorCode.FileNotSvg, "File isn't a valid svg"));
                return result;
            }
            using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                SKSvg svg = new SKSvg();
                svg.Load(stream);

                if (svg.Picture == null)
                {
                    result.Errors.Add(new ImageFileError(ImageFileErrorCode.NotValidImage, "Can't parse the svg file"));
                    return result;
                }

                SKImageInfo imageInfo = new SKImageInfo(width, height);
                using (var surface = SKSurface.Create(imageInfo))
                using (var canvas = surface.Canvas)
                {
                    // calculate the scaling need to fit to screen
                    float scaleX = width / svg.Picture.CullRect.Width;
                    float scaleY = height / svg.Picture.CullRect.Height;
                    SKMatrix matrix = SKMatrix.CreateScale(scaleX, scaleY);

                    // draw the svg
                    canvas.Clear(SKColors.Transparent);
                    canvas.DrawPicture(svg.Picture, in matrix);
                    canvas.Flush();

                    using (SKImage data = surface.Snapshot())
                    using (SKData pngImage = data.Encode(format, 100))
                    {
                        if (savePath == null)
                        {
                            string? name = Enum.GetName<SKEncodedImageFormat>(format);
                            if (name == null)
                            {
                                result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknownError, "File format isn't inside extension enum"));
                                return result;
                            }
                            savePath = path.Replace(".svg", "." + name.ToLower());
                        }

                        using (FileStream outputStream = new FileStream(savePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                        {
                            pngImage.SaveTo(outputStream);
                            result.Result = savePath;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknownError, e));
        }
        return result;

    }
    public static ResultWithImageFileError<bool> IsSvg(string path)
    {
        ResultWithImageFileError<bool> result = new ResultWithImageFileError<bool>();
        try
        {
            int count = 10;
            using (var stream = File.OpenRead(path))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                char[] buffer = new char[count];
                int n = reader.ReadBlock(buffer, 0, count);

                string resultTxt = String.Join("", buffer);
                result.Result = resultTxt.StartsWith("<?xml ") || resultTxt.StartsWith("<svg ");
            }
        }
        catch (Exception e)
        {
            result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknownError, e));
        }

        return result;
    }

}

[Export]
public abstract class AventusImage<T> : AventusFile<T> where T : IStorable
{
    public override ResultWithError<bool> MoveFile(T instance, HttpFile file)
    {
        ResultWithError<bool> result = new();
        result.Run(() => ValidateUpload(file).ToGeneric());
        result.Run(() => Transform(DefineMaxSize(), file).ToGeneric());
        result.Run(() => base.MoveFile(instance, file));
        return result;
    }
    protected abstract ImageSize? DefineMaxSize();
    protected virtual ImageUploadConstraints? DefineUploadConstraints() => null;

    public virtual VoidWithImageFileError ValidateUpload(HttpFile file)
    {
        VoidWithImageFileError result = new();


        try
        {
            ImageUploadConstraints? constraints = DefineUploadConstraints();
            FileInfo source = new(file.FilePath);
            if (constraints?.MaximumFileSizeBytes is long maximumFileSize && source.Length > maximumFileSize)
            {
                result.Errors.Add(new ImageFileError(ImageFileErrorCode.FileTooLarge, $"The image cannot exceed {maximumFileSize} bytes."));
                return result;
            }

            bool isImg = result.Extract(() => IsImg(file));
            if (!isImg) return result;

            if (constraints == null) return result;

            using SKData data = SKData.Create(file.FilePath);
            using SKCodec? codec = SKCodec.Create(data);
            if (codec == null)
            {
                result.Errors.Add(new ImageFileError(ImageFileErrorCode.NotValidImage, "The uploaded file is not a valid image."));
                return result;
            }

            SKEncodedImageFormat format = codec.EncodedFormat;
            if (constraints.AllowedFormats is { Count: > 0 } && !constraints.AllowedFormats.Contains(format))
            {
                result.Errors.Add(new ImageFileError(ImageFileErrorCode.FormatNotAllowed, $"The image format {format} is not allowed."));
            }

            string? expectedContentType = ImageUploadConstraints.GetContentType(format);
            if (
                constraints.RequireMatchingContentType &&
                (
                    expectedContentType == null ||
                    !file.Type.Equals(expectedContentType, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                result.Errors.Add(new ImageFileError(ImageFileErrorCode.ContentTypeMismatch, "The image content does not match its content type."));
            }

            int width = codec.Info.Width;
            int height = codec.Info.Height;
            if (
                (constraints.MinimumWidth is int minimumWidth && width < minimumWidth) ||
                (constraints.MinimumHeight is int minimumHeight && height < minimumHeight)
            )
            {
                result.Errors.Add(new ImageFileError(ImageFileErrorCode.DimensionsTooSmall, "The image dimensions are too small."));
            }
            if (
                (constraints.MaximumWidth is int maximumWidth && width > maximumWidth) ||
                (constraints.MaximumHeight is int maximumHeight && height > maximumHeight)
            )
            {
                result.Errors.Add(new ImageFileError(ImageFileErrorCode.DimensionsTooLarge, "The image dimensions are too large."));
            }
        }
        catch (Exception exception)
        {
            result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknownError, exception));
        }
        return result;
    }
    protected VoidWithImageFileError Transform(ImageSize? size, HttpFile Upload)
    {
        VoidWithImageFileError result = new VoidWithImageFileError();
        try
        {
            if (size == null || (size.Height == null && size.Width == null))
            {
                return result;
            }
            if (Upload == null)
                return result;
            ResultWithImageFileError<string> compressAction = ImageFile.Transform(Upload.FilePath, size);
            if (!compressAction.Success || compressAction.Result == null)
            {
                result.Errors.AddRange(compressAction.Errors);
                return result;
            }

            Upload.FilePath = compressAction.Result;
            Upload.FileName = Path.GetFileName(Upload.FilePath);

            return result;
        }
        catch (Exception e)
        {
            result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknownError, e));
        }
        return result;
    }
    protected ResultWithImageFileError<bool> IsImg(HttpFile Upload)
    {
        if (Upload.FilePath.EndsWith(".svg"))
        {
            return ImageFile.IsSvg(Upload.FilePath);
        }

        bool isValidImg = false;
        FileStream fileStream = File.OpenRead(Upload.FilePath);
        if (FileTypeValidator.IsTypeRecognizable(fileStream))
        {
            isValidImg = fileStream.IsImage();
        }
        fileStream.Close();
        fileStream.Dispose();

        return new ResultWithImageFileError<bool>()
        {
            Result = isValidImg,
            Errors = isValidImg ? new() : new() {
                    new ImageFileError(ImageFileErrorCode.NotValidImage, "The file " + Upload.FileName + " isn't valid")
                }
        };

    }

}

[Export]
public enum ImageFileErrorCode
{
    UnknownError,
    NotValidImage,
    FileNotSvg,
    NoSize,
    FileTooLarge,
    FormatNotAllowed,
    ContentTypeMismatch,
    DimensionsTooSmall,
    DimensionsTooLarge,
}
public class ImageFileError : GenericError<ImageFileErrorCode>
{
    public ImageFileError(ImageFileErrorCode code, string message, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0) : base(code, message, callerPath, callerNo)
    {
    }

    public ImageFileError(ImageFileErrorCode code, Exception e, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0) : base(code, e, callerPath, callerNo)
    {
    }
}
public class VoidWithImageFileError : VoidWithError<ImageFileError> { }
public class ResultWithImageFileError<T> : ResultWithError<T, ImageFileError> { }

public sealed class ImageUploadConstraints
{
    public long? MaximumFileSizeBytes { get; init; }
    public int? MinimumWidth { get; init; }
    public int? MinimumHeight { get; init; }
    public int? MaximumWidth { get; init; }
    public int? MaximumHeight { get; init; }
    public IReadOnlySet<SKEncodedImageFormat>? AllowedFormats { get; init; }
    public bool RequireMatchingContentType { get; init; } = true;

    public static string? GetContentType(SKEncodedImageFormat format) => format switch
    {
        SKEncodedImageFormat.Png => "image/png",
        SKEncodedImageFormat.Jpeg => "image/jpeg",
        SKEncodedImageFormat.Webp => "image/webp",
        SKEncodedImageFormat.Gif => "image/gif",
        SKEncodedImageFormat.Bmp => "image/bmp",
        SKEncodedImageFormat.Ico => "image/x-icon",
        _ => null
    };
}

public class ImageSize
{
    public static ImageSize Size(int value)
    {
        return new ImageSize() { Width = value, Height = value };
    }
    public static ImageSize MaxSize(int value)
    {
        return new ImageSize() { MaxWidth = value, MaxHeight = value };
    }

    public int? Width;
    public int? MaxWidth;
    public int? Height;
    public int? MaxHeight;
}
