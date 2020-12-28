using System;
using System.Device.I2c;
using System.Device.I2c.Drivers;
using System.Drawing;
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
            lcd.Write("Hello World!");
        }
    }
}
