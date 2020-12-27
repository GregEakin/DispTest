using System;
using System.Runtime.InteropServices;

namespace tmp102
{
    class Program
    {
 	private static int OPEN_READ_ONLY = 0;
 	private static int OPEN_WRITE_ONLY = 1;
 	private static int OPEN_READ_WRITE = 2;
        private static int I2C_SLAVE = 0x0703;
 
        [DllImport("libc.so.6", EntryPoint = "open")]
        public static extern int Open(string fileName, int mode);

        [DllImport("libc.so.6", EntryPoint = "close")]
	public static extern int Close (int handle);

        [DllImport("libc.so.6", EntryPoint = "ioctl", SetLastError = true)]
        private extern static int Ioctl(int fd, int request, int data);
 
        [DllImport("libc.so.6", EntryPoint = "read", SetLastError = true)]
        internal static extern int Read(int handle, byte[] data, int length);
        
	[DllImport("libc.so.6", EntryPoint = "write", SetLastError = true)]
	internal static extern int Write(int handle, byte[] data, int length);

	[DllImport("libc.so.6", EntryPoint = "usleep")]
	internal static extern int usleep(uint useconds);

        static void Main(string[] args)
        {
            // read from I2C device bus 0
	    var i2cBushandle = Open("/dev/i2c-0", OPEN_READ_WRITE);
	    if (i2cBushandle < 0)
		throw new Exception("Open failed");
 
            // open the slave device at address 0x48 for communication
	    int registerAddress = 0x27;
	    var deviceReturnCode = Ioctl(i2cBushandle, I2C_SLAVE, registerAddress);
	    if (deviceReturnCode < 0)
	    {
		var closed1 = Close(i2cBushandle);
	        if (closed1 < 0)
		    throw new Exception("Closed failed");	    
		throw new Exception("Ioctl failed");
	    }
 
            // read the first two bytes from the device into an array
	//    var deviceDataInMemory = new byte[2];
	//    Read(i2cBushandle, deviceDataInMemory, deviceDataInMemory.Length);
 
        //    Console.WriteLine($"Most significant byte = {deviceDataInMemory[0]}");
        //    Console.WriteLine($"Least significant byte = {deviceDataInMemory[1]}");
	
	    // Init
	    WriteCommand(i2cBushandle, (byte)0x02, (byte)0x08u); // Set 4-bit mode of LCD controller
	    WriteCommand(i2cBushandle, (byte)0x28, (byte)0x08u); // 2 line, 5x8 dot matrix
	    WriteCommand(i2cBushandle, (byte)0x0C, (byte)0x08u); // display on, cursor off
	    WriteCommand(i2cBushandle, (byte)0x06, (byte)0x08u); // inc cursor to right when writing and don't scroll
	    WriteCommand(i2cBushandle, (byte)0x80, (byte)0x08u); // set cursor to row 1, column 1

	    // Clear the screen
	    WriteCommand(i2cBushandle, (byte)0x0E, (byte)0x08u); // Clear the memroy

	    // Test Msg
	    WriteString(i2cBushandle, "Greg was here!", (byte)0x08u);

	    Control(i2cBushandle, (byte)0x08u, false, true);

	    var closed = Close(i2cBushandle);
	    if (closed < 0)
		throw new Exception("Closed failed");	    
        }

	static void WriteCommand(int handle, byte cmd, byte backlight)
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

	static void WriteString(int handle, string text, byte backlight)
	{
	    var uca = new byte[1];
	    foreach (var letter in text)
 	    {
		var cmd = (byte)letter;

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

	static void Control(int handle, byte backlight, bool cursor, bool blink)
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
