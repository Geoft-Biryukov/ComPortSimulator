using System.Globalization;
using System.IO.Ports;

class SimpleGpsSimulator
{
    private static SerialPort _serialPort;
    private static bool _isRunning = true;

    static void Main()
    {
        // Подключаемся к нашей половине виртуальной пары com0com
        _serialPort = new SerialPort("COM2", 9600, Parity.None, 8, StopBits.One);

        try
        {
            _serialPort.Open();
            Console.WriteLine("Эмулятор GPS (GGA + RMC) запущен на COM2...");

            Thread generatorThread = new Thread(GenerateNmeaStream);
            generatorThread.Start();

            Console.WriteLine("Нажмите Enter для остановки...");
            Console.ReadLine();

            _isRunning = false;
            generatorThread.Join();
            _serialPort.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    private static void GenerateNmeaStream()
    {
        // Стартовая точка (Саратов)
        double latitude = 51.5335;
        double longitude = 46.0342;
        double altitude = 120.5;

        int satellitesTracked = 9;
        double hdop = 1.2;

        while (_isRunning)
        {
            latitude += 0.00005;
            longitude += 0.00008;
            altitude += 0.1;

            // Обязательно InvariantCulture, чтобы разделитель миллисекунд был точкой
            DateTime utcNow = DateTime.UtcNow;
            string timeString = utcNow.ToString("HHmmss.ff", CultureInfo.InvariantCulture);
            string dateString = utcNow.ToString("ddMMyy", CultureInfo.InvariantCulture);

            string latFormatted = FormatNmeaCoord(latitude, true);
            string lonFormatted = FormatNmeaCoord(longitude, false);

            // ==========================================
            // ГЕНЕРАЦИЯ СТРОКИ RMC
            // ==========================================
            string rmcRaw = FormattableString.Invariant(
                $"GPRMC,{timeString},A,{latFormatted},{lonFormatted},005.2,280.0,{dateString},,,A");
            string rmcChecksum = CalculateNmeaChecksum(rmcRaw);
            string rmcSentence = $"${rmcRaw}*{rmcChecksum}\r\n";

            _serialPort.Write(rmcSentence);
            Console.Write(rmcSentence);

            Thread.Sleep(10);

            // ==========================================
            // ГЕНЕРАЦИЯ СТРОКИ GGA
            // ==========================================
            // FormattableString.Invariant гарантирует точки в {hdop:F1} и {altitude:F1}
            string ggaRaw = FormattableString.Invariant(
                $"GPGGA,{timeString},{latFormatted},{lonFormatted},1,{satellitesTracked:D2},{hdop:F1},{altitude:F1},M,0.0,M,,");
            string ggaChecksum = CalculateNmeaChecksum(ggaRaw);
            string ggaSentence = $"${ggaRaw}*{ggaChecksum}\r\n";

            _serialPort.Write(ggaSentence);
            Console.Write(ggaSentence);

            Console.WriteLine("---------------------------------------------");

            // Для 1 Гц обычно ставят 1000 мс (сейчас стоит 100 мс = 10 Гц)
            Thread.Sleep(100);
        }
    }

    // Хелпер для перевода десятичных градусов в формат NMEA (DDMM.MMMM)
    //private static string FormatNmeaCoord(double coord, bool isLatitude)
    //{
    //    double absCoord = Math.Abs(coord);
    //    int degrees = (int)absCoord;
    //    double minutes = (absCoord - degrees) * 60;

        //    if (isLatitude)
        //        return $"{degrees:D2}{minutes:00.0000},{(coord >= 0 ? "N" : "S")}";
        //    else
        //        return $"{degrees:D3}{minutes:00.0000},{(coord >= 0 ? "E" : "W")}";
        //}

    private static string FormatNmeaCoord(double coord, bool isLatitude)
    {
        double absCoord = Math.Abs(coord);
        int degrees = (int)absCoord;
        double minutes = (absCoord - degrees) * 60;

        if (isLatitude)
            return string.Create(CultureInfo.InvariantCulture, $"{degrees:D2}{minutes:00.0000},{(coord >= 0 ? "N" : "S")}");
        else
            return string.Create(CultureInfo.InvariantCulture, $"{degrees:D3}{minutes:00.0000},{(coord >= 0 ? "E" : "W")}");
    }

    // Расчет контрольной суммы XOR
    private static string CalculateNmeaChecksum(string sentence)
    {
        int checksum = 0;
        foreach (char c in sentence)
        {
            checksum ^= Convert.ToByte(c);
        }
        return checksum.ToString("X2");
    }
}