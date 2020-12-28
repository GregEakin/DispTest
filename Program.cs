using System;
using System.Device.I2c;
using System.Device.I2c.Drivers;
using System.Drawing;
using System.IO;
using System.Threading;
using Iot.Device.CharacterLcd;

namespace tmp102
{

    class Program
    {
        public static void Main(string[] args)
        {
            var settings = new I2cConnectionSettings(0x00, 0x27);
            using var device = new UnixI2cDevice(settings);
            using var lcd = new Hd44780(new Size(16, 2), LcdInterface.CreateI2c(device));

            var name0 = File.ReadAllText("/sys/class/thermal/thermal_zone0/type").Substring(0, 3);
            var name1 = File.ReadAllText("/sys/class/thermal/thermal_zone1/type").Substring(0, 3);

            for (var i = 0; i < 200; i++)
            {
                var temp0 = File.ReadAllText("/sys/class/thermal/thermal_zone0/temp");
                var x0 = float.Parse(temp0);
                var y0 = x0 * 9.0f / 5000.0f + 32.0f;
                var msg0 = $"{name0}: {y0:F2}";
                lcd.SetCursorPosition(0, 0);
                lcd.Write(msg0);

                var temp1 = File.ReadAllText("/sys/class/thermal/thermal_zone1/temp");
                var x1 = float.Parse(temp1);
                var y1 = x1 * 9.0f / 5000.0f + 32.0f;
                var msg1 = $"{name1}: {y1:F2}";
                lcd.SetCursorPosition(0, 1);
                lcd.Write(msg1);

                Thread.Sleep(500);
            }
        }
    }
}
