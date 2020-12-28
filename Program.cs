using System;
using System.Device.I2c;
using System.Device.I2c.Drivers;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Iot.Device.CharacterLcd;

namespace tmp102
{
    public class Lcd : Lcd1602
    {
        public Lcd(I2cDevice device)
            : base(device)
        {
            Console.WriteLine("Eight bit mode = {0}", _interface.EightBitMode); // True
            Console.WriteLine("Wait {0}", _interface.WaitMultiplier);           // 1
            Console.WriteLine("Size {0}x{1}", Size.Width, Size.Height);         // 16x2

            _interface.WaitMultiplier = 2;
        }

    }


    class Program
    {
        // private static int OPEN_READ_ONLY = 0;
        // private static int OPEN_WRITE_ONLY = 1;
        private static int OPEN_READ_WRITE = 2;
        private static int I2C_SLAVE = 0x0703;

        [DllImport("libc.so.6", EntryPoint = "open")]
        public static extern int Open(string fileName, int mode);

        [DllImport("libc.so.6", EntryPoint = "close")]
        public static extern int Close(int handle);

        [DllImport("libc.so.6", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int Ioctl(int fd, int request, int data);

        [DllImport("libc.so.6", EntryPoint = "read", SetLastError = true)]
        internal static extern int Read(int handle, byte[] data, int length);

        [DllImport("libc.so.6", EntryPoint = "write", SetLastError = true)]
        internal static extern int Write(int handle, byte[] data, int length);

        [DllImport("libc.so.6", EntryPoint = "usleep")]
        internal static extern int usleep(uint useconds);

        public static void Write(I2cDevice device, byte cmd, byte backlight)
        {
            byte uca = cmd;
            uca = (byte)((cmd & 0xF0u) | backlight);
            device.WriteByte(uca);
            Task.Delay(1);

            uca = (byte)((cmd & 0xF0u) | backlight | 0x04u);
            device.WriteByte(uca);
            Task.Delay(1);

            uca = (byte)((cmd & 0xF0u) | backlight);
            device.WriteByte(uca);
            Task.Delay(4);

            uca = (byte)((cmd << 4) | backlight);
            device.WriteByte(uca);
            Task.Delay(1);

            uca = (byte)((cmd << 4) | backlight | 0x04u);
            device.WriteByte(uca);
            Task.Delay(1);

            uca = (byte)((cmd << 4) | backlight);
            device.WriteByte(uca);
            Task.Delay(4);
        }


        public static void Main(string[] args)
        {
            var settings = new I2cConnectionSettings(0x00, 0x27);
            using var device = new UnixI2cDevice(settings);
            Console.WriteLine("Connection {0}", device.ConnectionSettings);
            Console.WriteLine("device path {0}", device.DevicePath);

            // {
            //     using var xx = LcdInterface.CreateI2c(device);
            //     xx.BacklightOn = true;
            // }

            // using var lcd = new Lcd1602(18, 5, new int[] { 6, 16, 20, 21 });
            var lcd = new Lcd(device);

            lcd.DisplayOn = true;
            
            lcd.Clear();
            lcd.Home();
            lcd.Write("Hello World!");
            
            Thread.Sleep(5000);
            
            lcd.Clear();
            lcd.DisplayOn = false;
        }

        public static void Main3(string[] args)
        {
            var settings = new I2cConnectionSettings(0x00, 0x27);
            using var device = new UnixI2cDevice(settings);

            // Init
            Write(device, 0x02, 0x08);  // Set 4-bit mode of LCD controller
            Write(device, 0x28, 0x08);  // 2 line, 5x8 dot matrix
            Write(device, 0x0C, 0x08);  // display on, cursor off
            Write(device, 0x06, 0x08);  // inc cursor to right when writing and don't scroll
            Write(device, 0x80, 0x08);  // set cursor to row 1, column 1

            // Clear the screen
            Write(device, 0x0E, 0x08);  // Clear the memory

            // Test Msg
            Write(device, 0xC0, 0x08);  // Set the cursor
            
            var message = Encoding.ASCII.GetBytes("DOTNET 5.0");
            foreach (var cmd in message)
                Write(device, cmd, 0x08);
        }

        public static void Main2(string[] args)
        {
            // read from I2C device bus 0
            var bus = 0;
            var handle = Open($"/dev/i2c-{bus}", OPEN_READ_WRITE);
            if (handle < 0)
                throw new Exception("Open failed");

            var address = 0x27;
            var returnCode = Ioctl(handle, I2C_SLAVE, address);
            if (returnCode < 0)
            {
                var closed1 = Close(handle);
                if (closed1 < 0)
                    throw new Exception("Closed failed");
                throw new Exception("Ioctl failed");
            }

            // Init
            WriteCommand(handle, 0x02, 0x08); // Set 4-bit mode of LCD controller
            WriteCommand(handle, 0x28, 0x08); // 2 line, 5x8 dot matrix
            WriteCommand(handle, 0x0C, 0x08); // display on, cursor off
            WriteCommand(handle, 0x06, 0x08); // inc cursor to right when writing and don't scroll
            WriteCommand(handle, 0x80, 0x08); // set cursor to row 1, column 1

            // Clear the screen
            WriteCommand(handle, 0x0E, 0x08); // Clear the memory

            // Test Msg
            WriteString(handle, "Greg was here!", 0x08);

            Control(handle, 0x08, false, true);

            var closed2 = Close(handle);
            if (closed2 < 0)
                throw new Exception("Closed failed");
        }

        public static void WriteCommand(int handle, byte cmd, byte backlight)
        {
            var uca = new byte[1];

            uca[0] = (byte)((cmd & 0xF0u) | backlight);
            Write(handle, uca, uca.Length);
            usleep(500);

            uca[0] = (byte)((cmd & 0xF0u) | backlight | 0x04u);
            Write(handle, uca, uca.Length);
            usleep(500);

            uca[0] = (byte)((cmd & 0xF0u) | backlight);
            Write(handle, uca, uca.Length);
            usleep(4100);

            uca[0] = (byte)((cmd << 4) | backlight);
            Write(handle, uca, uca.Length);
            usleep(500);

            uca[0] = (byte)((cmd << 4) | backlight | 0x04u);
            Write(handle, uca, uca.Length);
            usleep(500);

            uca[0] = (byte)((cmd << 4) | backlight);
            Write(handle, uca, uca.Length);
            usleep(4100);
        }

        public static void WriteString(int handle, string text, byte backlight)
        {
            var uca = new byte[1];
            foreach (var cmd in text.Select(letter => (byte)letter))
            {
                uca[0] = (byte)((cmd & 0xF0u) | backlight);
                Write(handle, uca, uca.Length);
                usleep(500);

                uca[0] = (byte)((cmd & 0xF0u) | backlight | 0x04u);
                Write(handle, uca, uca.Length);
                usleep(500);

                uca[0] = (byte)((cmd & 0xF0u) | backlight);
                Write(handle, uca, uca.Length);
                usleep(500);

                uca[0] = (byte)((cmd << 4) | backlight);
                Write(handle, uca, uca.Length);
                //usleep(500);

                uca[0] = (byte)((cmd << 4) | backlight | 0x04u);
                Write(handle, uca, uca.Length);
                usleep(500);

                uca[0] = (byte)((cmd << 4) | backlight);
                Write(handle, uca, uca.Length);
                usleep(4100);
            }
        }

        public static void Control(int handle, byte backlight, bool cursor, bool blink)
        {
            var cmd = (byte)0x0C; // display control
            if (cursor)
                cmd |= (byte)0x02u;
            if (blink)
                cmd |= (byte)0x01u;
            WriteCommand(handle, cmd, backlight);

        }
    }
}
