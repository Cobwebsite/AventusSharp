using System;
using System.Collections.Generic;
using System.IO;
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
using SKSvg = SkiaSharp.Extended.Svg.SKSvg;

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
            result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknowError, e));
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

            if (isSvgResult.Success)
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

            using SKBitmap skImage = SKBitmap.Decode(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
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
            using SKBitmap scaledBitmap = skImage.Resize(new SKImageInfo(destWidth, destHeight), SKFilterQuality.None);
            using SKImage image = SKImage.FromBitmap(scaledBitmap);
            using SKData encodedImage = image.Encode(format, 75);

            if (savePath == null)
            {
                savePath = path;
            }

            if (encodedImage.Size < previousSize)
            {
                List<string> pathWithExtension = savePath.Split('.').ToList<string>();
                pathWithExtension.RemoveAt(pathWithExtension.Count - 1);
                string? extensionName = Enum.GetName(format);
                if (extensionName == null)
                {
                    result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknowError, "File format isn't inside extension enum"));
                    return result;
                }
                pathWithExtension.Add(extensionName.ToLower());
                string newPath = string.Join(".", pathWithExtension);

                using (FileStream fs = new FileStream(newPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
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
            result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknowError, e));
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



            using SKBitmap scaledBitmap = skImage.Resize(new SKImageInfo(destWidth, destHeight), SKFilterQuality.None);
            using SKImage image = SKImage.FromBitmap(scaledBitmap);
            using SKData encodedImage = image.Encode(format, 100);

            if (savePath == null)
            {
                savePath = path;
            }

            using (FileStream fs = new FileStream(savePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                encodedImage.SaveTo(fs);
                result.Result = savePath;
            }
        }
        catch (Exception e)
        {
            result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknowError, e));
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
            return Compress(path, size.MaxWidth, size.MaxHeight, savePath);
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
                    canvas.DrawPicture(svg.Picture, ref matrix);
                    canvas.Flush();

                    using (SKImage data = surface.Snapshot())
                    using (SKData pngImage = data.Encode(format, 100))
                    {
                        if (savePath == null)
                        {
                            string? name = Enum.GetName<SKEncodedImageFormat>(format);
                            if (name == null)
                            {
                                result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknowError, "File format isn't inside extension enum"));
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
            result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknowError, e));
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
            result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknowError, e));
        }

        return result;
    }

}

[Export]
public abstract class AventusImage<T> : AventusFile<T> where T : IStorable
{
    public override ResultWithError<bool> MoveFile(T instance, HttpFile file)
    {
        ResultWithError<bool> result = IsImg(file);
        result.Run(() => Transform(DefineMaxSize(), file).ToGeneric());
        result.Run(() => base.MoveFile(instance, file));
        return result;
    }
    protected abstract ImageSize? DefineMaxSize();
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
            result.Errors.Add(new ImageFileError(ImageFileErrorCode.UnknowError, e));
        }
        return result;
    }
    protected ResultWithError<bool> IsImg(HttpFile Upload)
    {
        if (Upload.FilePath.EndsWith(".svg"))
        {
            return ImageFile.IsSvg(Upload.FilePath).ToGeneric();
        }

        bool isValidImg = false;
        FileStream fileStream = File.OpenRead(Upload.FilePath);
        if (FileTypeValidator.IsTypeRecognizable(fileStream))
        {
            isValidImg = fileStream.IsImage();
        }
        fileStream.Close();
        fileStream.Dispose();

        return new ResultWithError<bool>()
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
    UnknowError,
    NotValidImage,
    FileNotSvg,
    NoSize,
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