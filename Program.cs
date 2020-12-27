using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace tmp102
{
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

        public static void Main(string[] args)
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
            WriteCommand(handle, 0x0E, 0x08); // Clear the memroy

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
