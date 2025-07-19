using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
using BeatBay.DTOs;

namespace BeatBay.API.Services
{
    public class PdfReportService
    {
        public byte[] GenerateArtistStatisticsReport(
            string artistName,
            object summary,
            List<SongStatsDto> songs,
            List<SongStatsDto> topSongs)
        {
            using (var memoryStream = new MemoryStream())
            {
                // Crear el documento PDF
                var document = new Document(PageSize.A4, 50, 50, 25, 25);
                var writer = PdfWriter.GetInstance(document, memoryStream);

                document.Open();

                // Fuentes - Corregido para iTextSharp.LGPLv2.Core
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.Gray);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.Gray);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.Black);
                var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.Gray);

                // Título principal
                var title = new Paragraph($"Reporte Estadísticas - {artistName}", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 20;
                document.Add(title);

                // Fecha de generación
                var fecha = new Paragraph($"Generado el: {DateTime.Now:dd/MM/yyyy HH:mm}", smallFont);
                fecha.Alignment = Element.ALIGN_RIGHT;
                fecha.SpacingAfter = 20;
                document.Add(fecha);

                // Línea separadora
                document.Add(new Paragraph("_________________________________________________"));
                document.Add(new Paragraph("\n")); // Reemplazado Chunk.NEWLINE

                // Resumen
                var summaryTitle = new Paragraph("RESUMEN GENERAL", headerFont);
                summaryTitle.SpacingAfter = 10;
                document.Add(summaryTitle);

                if (summary != null)
                {
                    var summaryDict = summary.GetType().GetProperties()
                        .ToDictionary(p => p.Name, p => p.GetValue(summary));

                    var summaryTable = new PdfPTable(2);
                    summaryTable.WidthPercentage = 100;
                    summaryTable.SetWidths(new float[] { 1, 1 });

                    // Estilo para celdas del resumen
                    var cellStyle = new PdfPCell();
                    cellStyle.BackgroundColor = new BaseColor(245, 245, 245);
                    cellStyle.Padding = 10;
                    cellStyle.Border = Rectangle.BOX;

                    // Total de canciones
                    var totalSongsLabel = new PdfPCell(new Phrase("Total de Canciones:", normalFont));
                    totalSongsLabel.BackgroundColor = new BaseColor(245, 245, 245);
                    totalSongsLabel.Padding = 10;
                    summaryTable.AddCell(totalSongsLabel);

                    var totalSongsValue = new PdfPCell(new Phrase(summaryDict.GetValueOrDefault("TotalSongs", 0).ToString(), normalFont));
                    totalSongsValue.Padding = 10;
                    summaryTable.AddCell(totalSongsValue);

                    // Total de reproducciones
                    var totalPlaysLabel = new PdfPCell(new Phrase("Total de Reproducciones:", normalFont));
                    totalPlaysLabel.BackgroundColor = new BaseColor(245, 245, 245);
                    totalPlaysLabel.Padding = 10;
                    summaryTable.AddCell(totalPlaysLabel);

                    var totalPlaysValue = new PdfPCell(new Phrase(summaryDict.GetValueOrDefault("TotalPlays", 0).ToString(), normalFont));
                    totalPlaysValue.Padding = 10;
                    summaryTable.AddCell(totalPlaysValue);

                    // Canciones activas
                    var activeSongsLabel = new PdfPCell(new Phrase("Canciones Activas:", normalFont));
                    activeSongsLabel.BackgroundColor = new BaseColor(245, 245, 245);
                    activeSongsLabel.Padding = 10;
                    summaryTable.AddCell(activeSongsLabel);

                    var activeSongsValue = new PdfPCell(new Phrase(summaryDict.GetValueOrDefault("ActiveSongs", 0).ToString(), normalFont));
                    activeSongsValue.Padding = 10;
                    summaryTable.AddCell(activeSongsValue);

                    // Tiempo total reproducido
                    var totalDurationLabel = new PdfPCell(new Phrase("Tiempo Total Reproducido:", normalFont));
                    totalDurationLabel.BackgroundColor = new BaseColor(245, 245, 245);
                    totalDurationLabel.Padding = 10;
                    summaryTable.AddCell(totalDurationLabel);

                    var totalDuration = Convert.ToInt32(summaryDict.GetValueOrDefault("TotalDurationPlayed", 0));
                    var timeSpan = TimeSpan.FromSeconds(totalDuration);
                    var totalDurationValue = new PdfPCell(new Phrase(timeSpan.ToString(@"hh\:mm\:ss"), normalFont));
                    totalDurationValue.Padding = 10;
                    summaryTable.AddCell(totalDurationValue);

                    document.Add(summaryTable);
                }

                document.Add(new Paragraph("\n")); // Reemplazado Chunk.NEWLINE

                // Top canciones
                if (topSongs != null && topSongs.Any())
                {
                    var topSongsTitle = new Paragraph("TOP 10 CANCIONES MÁS REPRODUCIDAS", headerFont);
                    topSongsTitle.SpacingAfter = 10;
                    document.Add(topSongsTitle);

                    var topSongsTable = new PdfPTable(3);
                    topSongsTable.WidthPercentage = 100;
                    topSongsTable.SetWidths(new float[] { 3, 1, 1.5f });

                    // Headers
                    var headerCell1 = new PdfPCell(new Phrase("Título", headerFont));
                    headerCell1.BackgroundColor = new BaseColor(70, 130, 180);
                    headerCell1.Padding = 8;
                    topSongsTable.AddCell(headerCell1);

                    var headerCell2 = new PdfPCell(new Phrase("Reproducciones", headerFont));
                    headerCell2.BackgroundColor = new BaseColor(70, 130, 180);
                    headerCell2.Padding = 8;
                    topSongsTable.AddCell(headerCell2);

                    var headerCell3 = new PdfPCell(new Phrase("Tiempo Total", headerFont));
                    headerCell3.BackgroundColor = new BaseColor(70, 130, 180);
                    headerCell3.Padding = 8;
                    topSongsTable.AddCell(headerCell3);

                    // Datos
                    int position = 1;
                    foreach (var song in topSongs.Take(10))
                    {
                        var titleCell = new PdfPCell(new Phrase($"{position}. {song.Title}", normalFont));
                        titleCell.Padding = 8;
                        if (position % 2 == 0)
                            titleCell.BackgroundColor = new BaseColor(250, 250, 250);
                        topSongsTable.AddCell(titleCell);

                        var playsCell = new PdfPCell(new Phrase(song.PlayCount.ToString(), normalFont));
                        playsCell.Padding = 8;
                        playsCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        if (position % 2 == 0)
                            playsCell.BackgroundColor = new BaseColor(250, 250, 250);
                        topSongsTable.AddCell(playsCell);

                        var durationCell = new PdfPCell(new Phrase(
                            TimeSpan.FromSeconds(song.TotalDurationPlayed).ToString(@"hh\:mm\:ss"),
                            normalFont));
                        durationCell.Padding = 8;
                        durationCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        if (position % 2 == 0)
                            durationCell.BackgroundColor = new BaseColor(250, 250, 250);
                        topSongsTable.AddCell(durationCell);

                        position++;
                    }

                    document.Add(topSongsTable);
                }

                // Nueva página para todas las canciones
                document.NewPage();

                // Todas las canciones
                if (songs != null && songs.Any())
                {
                    var allSongsTitle = new Paragraph("TODAS LAS CANCIONES", headerFont);
                    allSongsTitle.SpacingAfter = 10;
                    document.Add(allSongsTitle);

                    var allSongsTable = new PdfPTable(3);
                    allSongsTable.WidthPercentage = 100;
                    allSongsTable.SetWidths(new float[] { 3, 1, 1.5f });

                    // Headers
                    var headerCell1 = new PdfPCell(new Phrase("Título", headerFont));
                    headerCell1.BackgroundColor = new BaseColor(70, 130, 180);
                    headerCell1.Padding = 8;
                    allSongsTable.AddCell(headerCell1);

                    var headerCell2 = new PdfPCell(new Phrase("Reproducciones", headerFont));
                    headerCell2.BackgroundColor = new BaseColor(70, 130, 180);
                    headerCell2.Padding = 8;
                    allSongsTable.AddCell(headerCell2);

                    var headerCell3 = new PdfPCell(new Phrase("Tiempo Total", headerFont));
                    headerCell3.BackgroundColor = new BaseColor(70, 130, 180);
                    headerCell3.Padding = 8;
                    allSongsTable.AddCell(headerCell3);

                    // Datos
                    int count = 1;
                    foreach (var song in songs)
                    {
                        var titleCell = new PdfPCell(new Phrase(song.Title, normalFont));
                        titleCell.Padding = 8;
                        if (count % 2 == 0)
                            titleCell.BackgroundColor = new BaseColor(250, 250, 250);
                        allSongsTable.AddCell(titleCell);

                        var playsCell = new PdfPCell(new Phrase(song.PlayCount.ToString(), normalFont));
                        playsCell.Padding = 8;
                        playsCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        if (count % 2 == 0)
                            playsCell.BackgroundColor = new BaseColor(250, 250, 250);
                        allSongsTable.AddCell(playsCell);

                        var durationCell = new PdfPCell(new Phrase(
                            TimeSpan.FromSeconds(song.TotalDurationPlayed).ToString(@"hh\:mm\:ss"),
                            normalFont));
                        durationCell.Padding = 8;
                        durationCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        if (count % 2 == 0)
                            durationCell.BackgroundColor = new BaseColor(250, 250, 250);
                        allSongsTable.AddCell(durationCell);

                        count++;
                    }

                    document.Add(allSongsTable);
                }

                // Pie de página
                document.Add(new Paragraph("\n")); // Reemplazado Chunk.NEWLINE
                document.Add(new Paragraph("_________________________________________________"));
                var footer = new Paragraph($"BeatBay - Reporte generado automáticamente", smallFont);
                footer.Alignment = Element.ALIGN_CENTER;
                document.Add(footer);

                document.Close();
                return memoryStream.ToArray();
            }
        }
    }
}