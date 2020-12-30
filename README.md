# NET IoT for LCD Displays: 4x20 and 2x16

## Displays
* [LCD 16x2](http://wiki.sunfounder.cc/index.php?title=LCD1602_Module)
* [LCD 20x4](http://wiki.sunfounder.cc/index.php?title=I2C_LCD2004)

## Chipsets
* [PCF8574](https://www.nxp.com/docs/en/data-sheet/PCF8574_PCF8574A.pdf) I2C I/O Expansion
* [HD44870U])(https://www.sparkfun.com/datasheets/LCD/HD44780.pdf) LCD Driver

The high four bits of each byte the PCF8574 get sent directly to the HD44870U, that's in 4-bit mode.
The low for bits drive the control pins, show here:

```csharp
[Flags]
private enum ControlByteFlags : byte
{
    Data = 0b_0000_0001,
    Read = 0b_0000_0010,
    Enabled = 0b_0000_0100,
    Backlight = 0b_0000_1000,
}
```

That means every byte that the host sends out the I2C bus need to encode four data bits, and four controls bits.
To send an eight-bit command, we need to send it as two sets of nibbles:
```csharp
public override void SendCommand(byte command)
{
    SendNibble((byte)(command & 0xF0));
    SendNibble((byte)(command << 4));
}
```

To send a byte to the data port, we need to add the Data flag:
```csharp
public override void SendData(byte value)
{
    SendNibble((byte)((value & 0xF0) | (byte)ControlByteFlags.Data));
    SendNibble((byte)((value << 4) | (byte)ControlByteFlags.Data));
}
```

Each nibble, we encode the backlight and toggle the Enable bit to tell the HD44870U the data is available:
```csharp
protected void SendNibble(byte command)
{
    var buffer = (byte)(command | (byte)(BacklightOn ? ControlByteFlags.Backlight : 0x00));

    _device.WriteByte(buffer);
    WaitForNotBusy(1);

    _device.WriteByte((byte)(buffer | (byte)ControlByteFlags.Enabled));
    WaitForNotBusy(1);

    _device.WriteByte(buffer);
    WaitForNotBusy(24);
}
```

Then to initialize the system, we send this stream of characters:
```csharp
protected void Initialize()
{
    // Send the command three times to get chip into 8-bit mode.
    SendNibble(0x30);        // Function set 0b0011 - 8-bit
    WaitForNotBusy(4500);
    SendNibble(0x30);        // Function set 0b0011 - 8-bit
    WaitForNotBusy(4500);
    SendNibble(0x30);        // Function set 0b0011 - 8-bit
    WaitForNotBusy(150);

    // Put it back in 4-bit mode, with two lines and a 5x8 font
    SendNibble(0x20);        // Function set 0b0010 - 4-bit, as an 8-bit instruction
    WaitForNotBusy(37);
    SendNibble(0x20);        // Function set 0b0010 - 4-bit, as the first 4-bits
    WaitForNotBusy(37);
    SendNibble(0xC0);        // Function set 0bnn** - 2-line and 5x8 Font, as the second 4-bits
    WaitForNotBusy(37);

    // Number of display lines, and font cannot be changed after this command 
}
```

