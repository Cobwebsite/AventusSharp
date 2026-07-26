using AventusSharp.Data.CustomTableMembers;
using AventusSharp.Data;
using AventusSharp.Data.Attributes;
using AventusSharp.Routes.Request;
using NUnit.Framework;
using SkiaSharp;

namespace AventusSharpTest.Data;

[TestFixture]
public sealed class ImageFileTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "image-file-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void IsSvg_distinguishes_svg_raster_and_missing_files()
    {
        var svgPath = Path.Combine(_directory, "shape.svg");
        var pngPath = CreatePng("shape.png", 12, 8);
        File.WriteAllText(svgPath,
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"></svg>");

        var svg = ImageFile.IsSvg(svgPath);
        var png = ImageFile.IsSvg(pngPath);
        var missing = ImageFile.IsSvg(Path.Combine(_directory, "missing.svg"));

        Assert.Multiple(() =>
        {
            Assert.That(svg.Success, Is.True);
            Assert.That(svg.Result, Is.True);
            Assert.That(png.Success, Is.True);
            Assert.That(png.Result, Is.False);
            Assert.That(missing.Success, Is.False);
        });
    }

    [Test]
    public void Resize_preserves_aspect_ratio_when_only_one_dimension_is_given()
    {
        var source = CreatePng("source.png", 40, 20);
        var target = Path.Combine(_directory, "resized.png");

        var result = ImageFile.Resize(source, width: 10, savePath: target);

        Assert.That(result.Success, Is.True, ErrorMessages(result.Errors));
        using var bitmap = SKBitmap.Decode(target);
        Assert.That(bitmap.Width, Is.EqualTo(10));
        Assert.That(bitmap.Height, Is.EqualTo(5));
    }

    [Test]
    public void Resize_requires_at_least_one_dimension()
    {
        var source = CreatePng("source.png", 20, 10);

        var result = ImageFile.Resize(source);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Select(error => error.Code),
            Does.Contain(ImageFileErrorCode.NoSize));
    }

    [Test]
    public void Compress_processes_raster_images_and_respects_non_square_limits()
    {
        var source = CreatePng("large.png", 80, 40);
        var target = Path.Combine(_directory, "compressed.png");

        var result = ImageFile.Compress(source, maxHeight: 10, maxWidth: 30, savePath: target);

        Assert.That(result.Success, Is.True, ErrorMessages(result.Errors));
        Assert.That(result.Result, Is.Not.Null);
        using var bitmap = SKBitmap.Decode(result.Result);
        Assert.That(bitmap.Width, Is.LessThanOrEqualTo(30));
        Assert.That(bitmap.Height, Is.LessThanOrEqualTo(10));
        Assert.That((double)bitmap.Width / bitmap.Height, Is.EqualTo(2d).Within(0.01));
    }

    [Test]
    public void Transform_maps_max_width_and_height_to_the_correct_axes()
    {
        var source = CreatePng("wide.png", 120, 60);
        var target = Path.Combine(_directory, "transformed.png");

        var result = ImageFile.Transform(source, new ImageSize
        {
            MaxWidth = 40,
            MaxHeight = 15
        }, target);

        Assert.That(result.Success, Is.True, ErrorMessages(result.Errors));
        using var bitmap = SKBitmap.Decode(result.Result);
        Assert.That(bitmap.Width, Is.LessThanOrEqualTo(40));
        Assert.That(bitmap.Height, Is.LessThanOrEqualTo(15));
    }

    [Test]
    public void SvgTo_converts_an_svg_to_the_requested_raster_size()
    {
        var source = Path.Combine(_directory, "source.svg");
        var target = Path.Combine(_directory, "converted.png");
        File.WriteAllText(source,
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20\" height=\"10\">" +
            "<rect width=\"20\" height=\"10\" fill=\"red\"/></svg>");

        var result = ImageFile.SvgTo(
            source,
            SKEncodedImageFormat.Png,
            width: 40,
            height: 20,
            savePath: target);

        Assert.That(result.Success, Is.True, ErrorMessages(result.Errors));
        Assert.That(result.Result, Is.EqualTo(target));
        using var bitmap = SKBitmap.Decode(target);
        Assert.That(bitmap.Width, Is.EqualTo(40));
        Assert.That(bitmap.Height, Is.EqualTo(20));
    }

    [Test]
    public void SvgTo_rejects_a_raster_source()
    {
        var source = CreatePng("source.png", 10, 10);

        var result = ImageFile.SvgTo(
            source,
            SKEncodedImageFormat.Png,
            width: 5,
            height: 5);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Select(error => error.Code),
            Does.Contain(ImageFileErrorCode.FileNotSvg));
    }

    [Test]
    public void AventusFile_before_save_moves_upload_and_clears_transient_state()
    {
        var source = Path.Combine(_directory, "upload.txt");
        var target = Path.Combine(_directory, "stored", "document.txt");
        File.WriteAllText(source, "content");
        var file = new UnitTestFile(target)
        {
            Upload = new HttpFile("document", "document.txt", source, "text/plain")
        };

        var result = file.BeforeSave(new UnitFileOwner());

        Assert.That(result.Success, Is.True, ErrorMessages(result.Errors));
        Assert.That(result.Result, Is.True);
        Assert.That(file.Upload, Is.Null);
        Assert.That(File.Exists(source), Is.False);
        Assert.That(File.ReadAllText(target), Is.EqualTo("content"));
    }

    [Test]
    public void AventusFile_before_save_rejects_an_owner_of_the_wrong_type()
    {
        var file = new UnitTestFile(Path.Combine(_directory, "unused.txt"));

        var result = file.BeforeSave(new object());

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.OfType<DataError>().Select(error => error.Code),
            Does.Contain(DataErrorCode.WrongType));
    }

    [Test]
    public void AventusImage_rejects_non_image_upload_before_moving_it()
    {
        var source = Path.Combine(_directory, "fake.png");
        var target = Path.Combine(_directory, "stored.png");
        File.WriteAllText(source, "this is not an image");
        var image = new UnitTestImage(target);
        var upload = new HttpFile("image", "fake.png", source, "image/png");

        var result = image.MoveFile(new UnitFileOwner(), upload);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.OfType<ImageFileError>().Select(error => error.Code),
            Does.Contain(ImageFileErrorCode.NotValidImage));
        Assert.That(File.Exists(source), Is.True);
        Assert.That(File.Exists(target), Is.False);
    }

    private string CreatePng(string name, int width, int height)
    {
        var path = Path.Combine(_directory, name);
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
        return path;
    }

    private static string ErrorMessages(IEnumerable<AventusSharp.Tools.GenericError> errors) =>
        string.Join(Environment.NewLine, errors.Select(error => error.Message));
}

[ManualInit]
public sealed class UnitFileOwner : Storable<UnitFileOwner>
{
}

public sealed class UnitTestFile : AventusFile<UnitFileOwner>
{
    private readonly string _target;

    public UnitTestFile(string target)
    {
        _target = target;
    }

    protected override AventusSharp.Tools.ResultWithError<string> DefineSavePath(
        UnitFileOwner instance,
        HttpFile file)
    {
        return new AventusSharp.Tools.ResultWithError<string> { Result = _target };
    }
}

public sealed class UnitTestImage : AventusImage<UnitFileOwner>
{
    private readonly string _target;

    public UnitTestImage(string target)
    {
        _target = target;
    }

    protected override ImageSize? DefineMaxSize() => ImageSize.MaxSize(16);

    protected override AventusSharp.Tools.ResultWithError<string> DefineSavePath(
        UnitFileOwner instance,
        HttpFile file)
    {
        return new AventusSharp.Tools.ResultWithError<string> { Result = _target };
    }
}
