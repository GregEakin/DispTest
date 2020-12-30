// Copyright © 2020-2020. All Rights Reserved.
// 
// SUBSYSTEM: Tmp102
// FILE:  I2cGE.cs
// AUTHOR:  Greg Eakin

using System;
using System.Device.I2c;
using Iot.Device.CharacterLcd;

namespace tmp102
{
    /// <summary>
    /// Built-in I2c access to the Hd44780 compatible controller. The Philips/NXP LCD driver ICs
    /// (such as the PCF2119x) are examples of this support.
    /// </summary>
    public class I2C4Bit : LcdInterface
    {
        [Flags]
        private enum ControlByteFlags : byte
        {
            Data = 0b_0000_0001,
            Read = 0b_0000_0010,
            Enabled = 0b_0000_0100,
            Backlight = 0b_0000_1000,
        }

        private readonly I2cDevice _device;

        public I2C4Bit(I2cDevice device)
        {
            _device = device;
            Initialize();
        }

        public override bool EightBitMode => false;

        public override bool BacklightOn { get; set; }

        protected void Initialize()
        {
            // Send the command three times to get chip into 8-bit mode.
            SendNibble(0x30);        // Function set 0b0011 - 8-bit
            WaitForNotBusy(4500);
            SendNibble(0x30);        // Function set 0b0011 - 8-bit
            WaitForNotBusy(4500);
            SendNibble(0x30);        // Function set 0b0011 - 8-bit
            WaitForNotBusy(150);

            // Put it back in 4-bit mode, with two Lines and 5x8 font
            SendNibble(0x20);        // Function set 0b0010 - 4-bit, as an 8-bit instruction
            WaitForNotBusy(37);
            SendNibble(0x20);        // Function set 0b0010 - 4-bit, as the first 4-bits
            WaitForNotBusy(37);
            SendNibble(0xC0);        // Function set 0bnn** - 2-line, Font, as the second 4-bits
            WaitForNotBusy(37);

            // Number of display lines, and  font cannot be changed after this command 
        }

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

        // protected byte ReadData()
        // {
        //     var buffer = (byte)(ControlByteFlags.Read | (BacklightOn ? ControlByteFlags.Backlight : 0x00));
        //
        //     _device.WriteByte((byte)(buffer | (byte)ControlByteFlags.Enabled));
        //     WaitForNotBusy(25);
        //
        //     var data = _device.ReadByte();
        //     
        //     _device.WriteByte(buffer);
        //     WaitForNotBusy(25);
        //
        //     _device.WriteByte((byte)(buffer | (byte)ControlByteFlags.Enabled));
        //     WaitForNotBusy(25);
        //
        //     var dataL = _device.ReadByte();
        //
        //     _device.WriteByte((byte)(BacklightOn ? ControlByteFlags.Backlight : 0x00));
        //     WaitForNotBusy(1);
        //
        //     if ((data & 0x04) > 0)
        //         Console.WriteLine("dd 0x{0:X2} 0x{1:X2}", data, dataL);
        //
        //     return data;
        // }

        public override void SendCommand(byte command)
        {
            // Wait for busy flag

            SendNibble((byte)(command & 0xF0));
            SendNibble((byte)(command << 4));
        }

        public override void SendCommands(ReadOnlySpan<byte> commands)
        {
            // There is a limit to how much data the controller can accept at once. Haven't found documentation
            // for this yet, can probably iterate a bit more on this to find a true "max". Not adding additional
            // logic like SendData as we don't expect a need to send more than a handful of commands at a time.

            if (commands.Length > 20)
                throw new ArgumentOutOfRangeException(nameof(commands), "Too many commands in one request.");

            foreach (var cmd in commands)
                SendCommand(cmd);
        }

        public override void SendData(byte value)
        {
            // Wait for busy flag

            SendNibble((byte)((value & 0xF0) | (byte)ControlByteFlags.Data));
            SendNibble((byte)((value << 4) | (byte)ControlByteFlags.Data));
        }

        public override void SendData(ReadOnlySpan<byte> values)
        {
            // There is a limit to how much data the controller can accept at once. Haven't found documentation
            // for this yet, can probably iterate a bit more on this to find a true "max". 40 was too much.

            foreach (var value in values)
                SendData(value);
        }
    }
}
