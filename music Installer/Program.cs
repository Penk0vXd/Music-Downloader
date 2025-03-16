using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace YouTubeMusicDownloader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===== YouTube Music Downloader =====");
            Console.ResetColor();

            // Пътища за инструментите
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YTMusicDownloader");
            string ffmpegFolder = Path.Combine(appDataFolder, "ffmpeg");
            string ffmpegExePath = Path.Combine(ffmpegFolder, "bin", "ffmpeg.exe");
            string ytDlpPath = Path.Combine(appDataFolder, "yt-dlp.exe");

            // Проверка и инсталиране на необходимите инструменти
            await EnsureToolsInstalled(appDataFolder, ffmpegFolder, ffmpegExePath, ytDlpPath);

            // Получаваме пътя до входния файл
            Console.Write("Въведете път до текстовия файл с линкове от YouTube (например D:\\songs.txt): ");
            string inputFilePath = Console.ReadLine().Trim('"');

            if (!File.Exists(inputFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Грешка: Файлът {inputFilePath} не съществува!");
                Console.ResetColor();
                Console.WriteLine("Натиснете произволен клавиш за изход...");
                Console.ReadKey();
                return;
            }

            // Получаваме пътя до изходната папка
            Console.Write("Въведете път до папката, където искате да запазите музиката (или Enter за папка 'Music' на работния плот): ");
            string outputFolder = Console.ReadLine().Trim('"');

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Music");
            }

            // Създаваме изходната папка, ако не съществува
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
                Console.WriteLine($"Създадена е папка: {outputFolder}");
            }

            // Четем линковете от файла
            string[] links = await File.ReadAllLinesAsync(inputFilePath);
            int totalLinks = links.Length;
            int successCount = 0;
            int failCount = 0;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Намерени са {totalLinks} линка във файла.");
            Console.ResetColor();

            // Обработваме всеки линк
            for (int i = 0; i < totalLinks; i++)
            {
                string link = links[i].Trim();
                if (string.IsNullOrWhiteSpace(link) || (!link.Contains("youtube.com/") && !link.Contains("youtu.be/")))
                {
                    continue;
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[{i + 1}/{totalLinks}] Изтегляне на: {link}");
                Console.ResetColor();

                try
                {
                    // Изтегляме видеото/аудиото с най-добро качество с помощта на yt-dlp и FFmpeg
                    bool success = await DownloadWithYtDlp(link, outputFolder, ytDlpPath, ffmpegFolder);

                    if (success)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[{i + 1}/{totalLinks}] Успешно изтеглено!");
                        Console.ResetColor();
                        successCount++;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[{i + 1}/{totalLinks}] Грешка при изтегляне!");
                        Console.ResetColor();
                        failCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[{i + 1}/{totalLinks}] Изключение: {ex.Message}");
                    Console.ResetColor();
                    failCount++;
                }

                // Малка пауза между заявките
                await Task.Delay(1000);
            }

            // Показваме обобщение
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===== Обобщение =====");
            Console.WriteLine($"Общо линкове: {totalLinks}");
            Console.WriteLine($"Успешно изтеглени: {successCount}");
            Console.WriteLine($"Неуспешни: {failCount}");
            Console.WriteLine($"Изтеглените файлове са записани в: {outputFolder}");
            Console.ResetColor();

            Console.WriteLine("Натиснете произволен клавиш за изход...");
            Console.ReadKey();
        }

        // Проверява и инсталира необходимите инструменти
        static async Task EnsureToolsInstalled(string appDataFolder, string ffmpegFolder, string ffmpegExePath, string ytDlpPath)
        {
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }

            // Проверка за FFmpeg
            bool ffmpegInstalled = File.Exists(ffmpegExePath);
            if (!ffmpegInstalled)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("FFmpeg не е намерен. Инсталиране на FFmpeg...");
                Console.ResetColor();

                await DownloadAndInstallFFmpeg(ffmpegFolder);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("FFmpeg е намерен и готов за използване.");
                Console.ResetColor();
            }

            // Проверка за yt-dlp
            bool ytDlpInstalled = File.Exists(ytDlpPath);
            if (!ytDlpInstalled)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("yt-dlp не е намерен. Изтегляне на yt-dlp...");
                Console.ResetColor();

                await DownloadYtDlp(ytDlpPath);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("yt-dlp е намерен и готов за използване.");
                Console.ResetColor();
            }
        }

        // Изтегля и инсталира FFmpeg
        static async Task DownloadAndInstallFFmpeg(string ffmpegFolder)
        {
            try
            {
                string ffmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
                string tempZipPath = Path.Combine(Path.GetTempPath(), "ffmpeg.zip");

                using (var httpClient = new HttpClient())
                {
                    Console.WriteLine("Изтегляне на FFmpeg...");
                    byte[] zipData = await httpClient.GetByteArrayAsync(ffmpegUrl);
                    await File.WriteAllBytesAsync(tempZipPath, zipData);

                    Console.WriteLine("Разархивиране на FFmpeg...");
                    if (Directory.Exists(ffmpegFolder))
                    {
                        Directory.Delete(ffmpegFolder, true);
                    }

                    Directory.CreateDirectory(ffmpegFolder);
                    ZipFile.ExtractToDirectory(tempZipPath, ffmpegFolder);

                    // След разархивирането, FFmpeg е в подпапка със специфично име
                    // Преместваме съдържанието на тази папка в главната ffmpeg папка
                    string extractedFolder = Directory.GetDirectories(ffmpegFolder)[0];
                    foreach (string directory in Directory.GetDirectories(extractedFolder))
                    {
                        string destDir = Path.Combine(ffmpegFolder, Path.GetFileName(directory));
                        if (Directory.Exists(destDir))
                        {
                            Directory.Delete(destDir, true);
                        }
                        Directory.Move(directory, destDir);
                    }

                    foreach (string file in Directory.GetFiles(extractedFolder))
                    {
                        string destFile = Path.Combine(ffmpegFolder, Path.GetFileName(file));
                        if (File.Exists(destFile))
                        {
                            File.Delete(destFile);
                        }
                        File.Move(file, destFile);
                    }

                    Directory.Delete(extractedFolder, true);
                    File.Delete(tempZipPath);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("FFmpeg успешно инсталиран!");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Грешка при инсталиране на FFmpeg: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }

        // Изтегля yt-dlp
        static async Task DownloadYtDlp(string ytDlpPath)
        {
            try
            {
                string ytDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

                using (var httpClient = new HttpClient())
                {
                    Console.WriteLine("Изтегляне на yt-dlp...");
                    byte[] exeData = await httpClient.GetByteArrayAsync(ytDlpUrl);
                    await File.WriteAllBytesAsync(ytDlpPath, exeData);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("yt-dlp успешно изтеглен!");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Грешка при изтегляне на yt-dlp: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }

        // Изтегля видео/аудио използвайки yt-dlp и FFmpeg
        static async Task<bool> DownloadWithYtDlp(string link, string outputFolder, string ytDlpPath, string ffmpegFolder)
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = ytDlpPath,
                    // Изтегляме само аудио файла с най-добро качество и го конвертираме в mp3
                    Arguments = $"-x --audio-format mp3 --audio-quality 0 --ffmpeg-location \"{Path.Combine(ffmpegFolder, "bin")}\" -o \"{Path.Combine(outputFolder, "%(title)s.%(ext)s")}\" {link}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                process.Start();

                // Асинхронно четем изхода и грешките
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(outputTask, errorTask);

                process.WaitForExit();

                string output = await outputTask;
                string error = await errorTask;

                // Ако има грешки, покажи ги в конзолата
                if (!string.IsNullOrEmpty(error) && !error.Contains("INFO:"))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Грешка: {error}");
                    Console.ResetColor();
                    return false;
                }

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Изключение при изтегляне: {ex.Message}");
                Console.ResetColor();
                return false;
            }
        }
    }
}