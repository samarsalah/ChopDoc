using ChopDoc.Tests.Helpers;

var samplesDir = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples"));

Directory.CreateDirectory(samplesDir);

File.WriteAllBytes(Path.Combine(samplesDir, "text-sample.pdf"),
    PdfFixtures.CreateTextPdf("ChopDoc text sample - page 1 with extractable text."));

File.WriteAllBytes(Path.Combine(samplesDir, "multipage-text-sample.pdf"),
    PdfFixtures.CreateTextPdf(
        "Page 1: Introduction to ChopDoc conversion.",
        "Page 2: Splitting happens when output exceeds the size limit.",
        "Page 3: Validation checks parts before success."));

File.WriteAllBytes(Path.Combine(samplesDir, "scanned-no-text-sample.pdf"),
    PdfFixtures.CreateNoTextPdf());

Console.WriteLine($"Wrote samples to {samplesDir}");
foreach (var f in Directory.GetFiles(samplesDir, "*.pdf"))
    Console.WriteLine($" - {Path.GetFileName(f)} ({new FileInfo(f).Length} bytes)");
