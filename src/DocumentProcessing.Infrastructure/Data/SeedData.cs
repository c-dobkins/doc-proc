using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocumentProcessing.Infrastructure.Data;

/// <summary>
/// Seeds the database with sample extraction templates and documents on first run.
/// </summary>
public static class SeedData
{
    /// <summary>Marker value stored on Document.Source for all seed-generated records.</summary>
    public const string SeedSource = "seed";

    public static async Task SeedAsync(
        DocumentProcessingDbContext db,
        IBlobStorage blobStorage,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (await db.ExtractionTemplates.AnyAsync(ct))
        {
            // DB already seeded — but files may be missing (ephemeral container storage)
            await RegenerateMissingFilesAsync(db, blobStorage, logger, ct);
            return;
        }

        logger.LogInformation("Seeding sample templates and documents...");

        // --- Template 1: Invoice ---
        var invoiceTemplate = new ExtractionTemplate
        {
            Name = "Invoice",
            Description = "Standard commercial invoice with line items",
            PromptTemplate = """
                Extract the following fields from this invoice document.

                Field definitions:
                {fieldDefinitions}

                OCR text:
                {ocrText}

                Return JSON in this exact format:
                {outputFormat}
                """,
            Fields =
            [
                new TemplateField { Name = "InvoiceNumber", DataType = FieldDataType.String, IsRequired = true, Description = "The invoice number or ID" },
                new TemplateField { Name = "InvoiceDate", DataType = FieldDataType.Date, IsRequired = true, Description = "Date the invoice was issued" },
                new TemplateField { Name = "DueDate", DataType = FieldDataType.Date, IsRequired = false, Description = "Payment due date" },
                new TemplateField { Name = "VendorName", DataType = FieldDataType.String, IsRequired = true, Description = "Name of the company issuing the invoice" },
                new TemplateField { Name = "CustomerName", DataType = FieldDataType.String, IsRequired = true, Description = "Name of the customer being billed" },
                new TemplateField { Name = "Subtotal", DataType = FieldDataType.Currency, IsRequired = false, Description = "Subtotal before tax" },
                new TemplateField { Name = "TaxAmount", DataType = FieldDataType.Currency, IsRequired = false, Description = "Tax amount" },
                new TemplateField { Name = "TotalAmount", DataType = FieldDataType.Currency, IsRequired = true, Description = "Total amount due including tax" },
            ]
        };

        // --- Template 2: Receipt ---
        var receiptTemplate = new ExtractionTemplate
        {
            Name = "Receipt",
            Description = "Store receipt or proof of purchase",
            PromptTemplate = """
                Extract the following fields from this receipt.

                Field definitions:
                {fieldDefinitions}

                OCR text:
                {ocrText}

                Return JSON in this exact format:
                {outputFormat}
                """,
            Fields =
            [
                new TemplateField { Name = "StoreName", DataType = FieldDataType.String, IsRequired = true, Description = "Name of the store or business" },
                new TemplateField { Name = "ReceiptDate", DataType = FieldDataType.Date, IsRequired = true, Description = "Date of the transaction" },
                new TemplateField { Name = "ReceiptNumber", DataType = FieldDataType.String, IsRequired = false, Description = "Receipt or transaction number" },
                new TemplateField { Name = "PaymentMethod", DataType = FieldDataType.String, IsRequired = false, Description = "Payment method used (Cash, Card, etc.)" },
                new TemplateField { Name = "TotalAmount", DataType = FieldDataType.Currency, IsRequired = true, Description = "Total amount paid" },
                new TemplateField { Name = "TaxAmount", DataType = FieldDataType.Currency, IsRequired = false, Description = "Tax amount if shown" },
            ]
        };

        db.ExtractionTemplates.Add(invoiceTemplate);
        db.ExtractionTemplates.Add(receiptTemplate);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded 2 extraction templates: Invoice, Receipt");

        // --- Sample Document 1: Invoice PDF ---
        var invoicePdf = GenerateInvoicePdf();
        await using var invoiceStream = new MemoryStream(invoicePdf);
        var invoicePath = await blobStorage.SaveAsync(invoiceStream, "sample-invoice.pdf", ct);

        var invoiceDoc = new Document
        {
            FileName = "sample-invoice.pdf",
            FileType = FileType.Pdf,
            FileSizeBytes = invoicePdf.Length,
            StoragePath = invoicePath,
            Source = SeedSource,
            TemplateId = invoiceTemplate.Id
        };

        db.Documents.Add(invoiceDoc);

        // --- Sample Document 2: Receipt PDF ---
        var receiptPdf = GenerateReceiptPdf();
        await using var receiptStream = new MemoryStream(receiptPdf);
        var receiptPath = await blobStorage.SaveAsync(receiptStream, "sample-receipt.pdf", ct);

        var receiptDoc = new Document
        {
            FileName = "sample-receipt.pdf",
            FileType = FileType.Pdf,
            FileSizeBytes = receiptPdf.Length,
            StoragePath = receiptPath,
            Source = SeedSource,
            TemplateId = receiptTemplate.Id
        };

        db.Documents.Add(receiptDoc);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded 2 sample documents: sample-invoice.pdf, sample-receipt.pdf");
    }

    /// <summary>
    /// Regenerates seed PDF files when the container restarts with ephemeral storage
    /// but the database already has seed document records.
    /// </summary>
    private static async Task RegenerateMissingFilesAsync(
        DocumentProcessingDbContext db,
        IBlobStorage blobStorage,
        ILogger logger,
        CancellationToken ct)
    {
        var seedDocs = await db.Documents
            .Where(d => d.Source == SeedSource)
            .ToListAsync(ct);

        foreach (var doc in seedDocs)
        {
            try
            {
                // Try to access the file — if it throws, regenerate
                await using var _ = await blobStorage.GetAsync(
                    doc.StoragePath ?? throw new FileNotFoundException("Seed document has no storage path"), ct);
            }
            catch (Exception ex) when (ex is FileNotFoundException or IOException or UnauthorizedAccessException)
            {
                logger.LogInformation("Regenerating missing seed file: {FileName} ({Reason})",
                    doc.FileName, ex.GetType().Name);

                var pdf = doc.FileName.Contains("invoice", StringComparison.OrdinalIgnoreCase)
                    ? GenerateInvoicePdf()
                    : GenerateReceiptPdf();

                await using var stream = new MemoryStream(pdf);
                var newPath = await blobStorage.SaveAsync(stream, doc.FileName, ct);

                doc.StoragePath = newPath;
                doc.FileSizeBytes = pdf.Length;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static byte[] GenerateInvoicePdf()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var fontBold = builder.AddStandard14Font(Standard14Font.HelveticaBold);

        // Page 1
        var page = builder.AddPage(PageSize.A4);
        double y = 800;

        void AddLine(string text, double fontSize = 10, bool bold = false, double? xPos = null)
        {
            var f = bold ? fontBold : font;
            page.AddText(text, fontSize, new PdfPoint(xPos ?? 50, y), f);
            y -= fontSize + 4;
        }

        void AddRow(string left, string right)
        {
            page.AddText(left, 10, new PdfPoint(50, y), font);
            page.AddText(right, 10, new PdfPoint(400, y), font);
            y -= 14;
        }

        AddLine("INVOICE", 20, bold: true);
        y -= 10;
        AddLine("Acme Consulting Services Ltd.", 12, bold: true);
        AddLine("123 Business Road, Suite 400");
        AddLine("Cape Town, Western Cape 8001");
        AddLine("South Africa");
        AddLine("VAT No: 4567890123");
        AddLine("Email: billing@acme-consulting.example");
        y -= 20;

        AddRow("Invoice Number:", "INV-2026-00482");
        AddRow("Invoice Date:", "2026-03-15");
        AddRow("Due Date:", "2026-04-15");
        y -= 10;

        AddLine("Bill To:", 11, bold: true);
        AddLine("Global Imports (Pty) Ltd");
        AddLine("456 Commerce Ave");
        AddLine("Johannesburg, Gauteng 2000");
        AddLine("Attn: Sarah Mokoena");
        y -= 20;

        // Table header
        AddLine("Description                                              Qty     Unit Price     Amount", 9, bold: true);
        AddLine("-----------------------------------------------------------------------------", 8);

        AddRow("IT Strategy Consultation (8 hours)", "R 12,000.00");
        AddRow("Cloud Migration Assessment", "R 8,500.00");
        AddRow("Security Audit Report", "R 6,500.00");
        AddRow("Staff Training Workshop (2 days)", "R 15,000.00");

        y -= 10;
        AddLine("-----------------------------------------------------------------------------", 8);
        AddRow("Subtotal:", "R 42,000.00");
        AddRow("VAT (15%):", "R 6,300.00");
        AddLine("-----------------------------------------------------------------------------", 8);
        AddRow("TOTAL DUE:", "R 48,300.00");
        y -= 20;

        AddLine("Payment Details:", 10, bold: true);
        AddLine("Bank: First National Bank");
        AddLine("Account: 62845901234");
        AddLine("Branch Code: 250655");
        AddLine("Reference: INV-2026-00482");
        y -= 20;

        AddLine("Thank you for your business.", 9);

        return builder.Build();
    }

    private static byte[] GenerateReceiptPdf()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Courier);
        var fontBold = builder.AddStandard14Font(Standard14Font.CourierBold);

        var page = builder.AddPage(PageSize.A4);
        double y = 780;

        void AddLine(string text, double fontSize = 10, bool bold = false)
        {
            var f = bold ? fontBold : font;
            page.AddText(text, fontSize, new PdfPoint(150, y), f);
            y -= fontSize + 4;
        }

        AddLine("================================", 10, bold: true);
        AddLine("  HARBOUR BEAN COFFEE CO.", 13, bold: true);
        AddLine("================================", 10, bold: true);
        AddLine("  112 Long Street, Cape Town");
        AddLine("      Tel: 021-555-0198");
        AddLine("   VAT No: 9876543210");
        y -= 6;
        AddLine("--------------------------------");
        AddLine("  Receipt #: TXN-88421");
        AddLine("  Date: 2026-03-20 09:47");
        AddLine("  Cashier: Employee #12");
        AddLine("--------------------------------");
        y -= 4;

        AddLine("  2x Flat White          R 64.00");
        AddLine("  1x Croissant           R 35.00");
        AddLine("  1x Orange Juice        R 42.00");
        AddLine("  1x Avocado Toast       R 78.00");
        y -= 4;

        AddLine("--------------------------------");
        AddLine("  Subtotal:             R 219.00");
        AddLine("  VAT (15%):             R 32.85");
        AddLine("================================");
        AddLine("  TOTAL:                R 251.85", 11, bold: true);
        AddLine("================================");
        y -= 6;

        AddLine("  Payment: Visa **** 4829");
        AddLine("  Auth Code: A7729B");
        y -= 6;

        AddLine("--------------------------------");
        AddLine("    Thank you for visiting!");
        AddLine("     www.harbourbean.example");
        AddLine("--------------------------------");

        return builder.Build();
    }
}
