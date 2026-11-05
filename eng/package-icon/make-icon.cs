#:package Svg.Skia

// Draws the logo and writes every file that shows it. From the repository root:
//
//   dotnet run eng/package-icon/make-icon.cs
//
// It writes eng/package-icon/nuget.svg, the nuget.png the package ships (rendered from that SVG), and the sample app's
// icon and splash screen. The mark is a speech bubble with a four-point sparkle cut into it: a chat, with AI in it.

using System.Globalization;
using System.Runtime.CompilerServices;
using SkiaSharp;
using Svg.Skia;

const string GradientFrom = "#8B5CF6";
const string GradientTo = "#5B21B6";
// The sparkle is cut into the bubble, so it shows the gradient's middle.
const string Sparkle = "#6D28D9";

var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", ".."));
var sampleResources = Path.Combine(root, "samples", "Plugin.Maui.AiChat.Sample", "Resources");

// The package icon: the mark on a rounded square with the violet gradient.
var packageSvg = Svg(128, Gradient() + "<rect width=\"128\" height=\"128\" rx=\"26\" fill=\"url(#g)\"/>" + Mark());
File.WriteAllText(Path.Combine(root, "eng", "package-icon", "nuget.svg"), packageSvg);
RenderPng(packageSvg, 256, Path.Combine(root, "nuget.png"));

// The app icon: the platforms shape the background themselves, and the mark keeps inside the safe area of adaptive icons.
File.WriteAllText(Path.Combine(sampleResources, "AppIcon", "appicon.svg"), Svg(456,
    Gradient() + "<rect width=\"456\" height=\"456\" fill=\"url(#g)\"/>"));
var centred = $"<g transform=\"translate(84 84) scale({(288 / 128.0).ToString(CultureInfo.InvariantCulture)})\">{Mark()}</g>";
File.WriteAllText(Path.Combine(sampleResources, "AppIcon", "appiconfg.svg"), Svg(456, centred));
File.WriteAllText(Path.Combine(sampleResources, "Splash", "splash.svg"), Svg(456, centred));

Console.WriteLine("Wrote the package icon (SVG and PNG), the app icon and the splash screen.");

static string Svg(int size, string body) =>
    $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 {size} {size}\">{body}</svg>\n";

static string Gradient() =>
    $"<defs><linearGradient id=\"g\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"><stop offset=\"0\" stop-color=\"{GradientFrom}\"/>" +
    $"<stop offset=\"1\" stop-color=\"{GradientTo}\"/></linearGradient></defs>";

// In a 128-unit box: a rounded bubble with a tail at the lower left, and the sparkle in its middle.
static string Mark() =>
    "<path d=\"M44 26H84A20 20 0 0 1 104 46V70A20 20 0 0 1 84 90H58L38 105L42 89A20 20 0 0 1 24 70V46A20 20 0 0 1 44 26Z\" fill=\"#FFFFFF\"/>" +
    $"<path d=\"M64 38Q66.6 55.4 84 58Q66.6 60.6 64 78Q61.4 60.6 44 58Q61.4 55.4 64 38Z\" fill=\"{Sparkle}\"/>";

static void RenderPng(string svg, int size, string path)
{
    using var document = new SKSvg();
    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));
    var picture = document.Load(stream) ?? throw new InvalidOperationException("The SVG did not load.");
    using var bitmap = new SKBitmap(size, size);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);
    canvas.Scale(size / picture.CullRect.Width);
    canvas.DrawPicture(picture);
    using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
    using var file = File.Create(path);
    png.SaveTo(file);
}

static string ScriptPath([CallerFilePath] string path = "") => path;
