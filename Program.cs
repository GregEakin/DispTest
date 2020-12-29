using System.Device.I2c;
using System.Device.I2c.Drivers;
using System.Drawing;
using System.IO;
using System.Threading;

namespace tmp102
{

    class Program
    {
        public static void ShowSpecialSymbols(Hd44780 lcd)
        {
            // We have space for eight custom characters
            var bell = new byte[] { 0x04, 0x0e, 0x0e, 0x0e, 0x1f, 0x00, 0x04, 0x00 };
            var note = new byte[] { 0x02, 0x03, 0x02, 0x0e, 0x1e, 0x0c, 0x00, 0x00 };
            var clock = new byte[] { 0x00, 0x0e, 0x15, 0x17, 0x11, 0x0e, 0x00, 0x00 };
            var heart = new byte[] { 0x00, 0x0a, 0x1f, 0x1f, 0x0e, 0x04, 0x00, 0x00 };
            var duck = new byte[] { 0x00, 0x0c, 0x1d, 0x0f, 0x0f, 0x06, 0x00, 0x00 };
            var check = new byte[] { 0x00, 0x01, 0x03, 0x16, 0x1c, 0x08, 0x00, 0x00 };
            var cross = new byte[] { 0x00, 0x1b, 0x0e, 0x04, 0x0e, 0x1b, 0x00, 0x00 };
            var retArrow = new byte[] { 0x01, 0x01, 0x05, 0x09, 0x1f, 0x08, 0x04, 0x00 };

            lcd.CreateCustomCharacter(0, bell);
            lcd.CreateCustomCharacter(1, note);
            lcd.CreateCustomCharacter(2, clock);
            lcd.CreateCustomCharacter(3, heart);
            lcd.CreateCustomCharacter(4, duck);
            lcd.CreateCustomCharacter(5, check);
            lcd.CreateCustomCharacter(6, cross);
            lcd.CreateCustomCharacter(7, retArrow);

            lcd.SetCursorPosition(0, 3);
            var msg = $"\x0\x1\x2\x3\x4\x5\x6\x7 \x8\x9\xA";
            lcd.Write(msg);
        }

        public static void Main(string[] args)
        {
            var settings = new I2cConnectionSettings(0x00, 0x27);
            using var device = new UnixI2cDevice(settings);
            using var lcd = new Hd44780(new Size(20, 4), new I2C4Bit(device));

            var name0 = File.ReadAllText("/sys/class/thermal/thermal_zone0/type").Substring(0, 3);
            var name1 = File.ReadAllText("/sys/class/thermal/thermal_zone1/type").Substring(0, 3);

            lcd.BacklightOn = true;
            ShowSpecialSymbols(lcd);

            for (var i = 0; i < 200; i++)
            {
                var temp0 = File.ReadAllText("/sys/class/thermal/thermal_zone0/temp");
                var x0 = float.Parse(temp0);
                var y0 = x0 * 9.0f / 5000.0f + 32.0f;
                var msg0 = $"{name0}: {y0:F2}\xDF";
                lcd.SetCursorPosition(0, 0);
                lcd.Write(msg0);

                var temp1 = File.ReadAllText("/sys/class/thermal/thermal_zone1/temp");
                var x1 = float.Parse(temp1);
                var y1 = x1 * 9.0f / 5000.0f + 32.0f;
                var msg1 = $"{name1}: {y1:F2}\xDF";
                lcd.SetCursorPosition(0, 1);
                lcd.Write(msg1);

                Thread.Sleep(500);
            }

            lcd.BacklightOn = false;
            lcd.Clear();
        }
    }
}
