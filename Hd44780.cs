// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// 
// Greg Eakin <greg@eakin.dev>

// Data Sheets
// https://www.sparkfun.com/datasheets/LCD/HD44780.pdf
// https://www.nxp.com/docs/en/data-sheet/PCF8574_PCF8574A.pdf

using Iot.Device.CharacterLcd;
using System;
using System.Buffers;
using System.Drawing;
using System.Threading;

namespace tmp102
{
    [Flags]
    internal enum DisplayFunction : byte
    {
        ExtendedInstructionSet = 1,
        Font5x10 = 4,
        TwoLine = 8,
        EightBit = 16, // 0x10
        Command = 32, // 0x20
    }

    [Flags]
    internal enum DisplayControl : byte
    {
        BlinkOn = 1,
        CursorOn = 2,
        DisplayOn = 4,
        Command = 8,
    }

    [Flags]
    internal enum DisplayEntryMode : byte
    {
        DisplayShift = 1,
        Increment = 2,
        Command = 4,
    }

    [Flags]
    internal enum DisplayShift : byte
    {
        Right = 4,
        Display = 8,
        Command = 16, // 0x10
    }

    [Flags]
    internal enum BusControl : byte
    {
        Data = 1,
        Write = 2,
        Enabled = 4,
        Backlight = 8
    }

    /// <summary>
    /// Supports LCD character displays compatible with the HD44780 LCD controller/driver.
    /// Also supports serial interface adapters such as the MCP23008.
    /// </summary>
    /// <remarks>
    /// The Hitatchi HD44780 was released in 1987 and set the standard for LCD controllers. Hitatchi does not make this chipset anymore, but
    /// most character LCD drivers are intended to be fully compatible with this chipset. Some examples: Sunplus SPLC780D, Sitronix ST7066U,
    /// Samsung KS0066U, Aiptek AIP31066, and many more.
    /// 
    /// Some compatible chips extend the HD44780 with additional pins and features. They are still fully compatible. The ST7036 is one example.
    /// 
    /// This implementation was drawn from numerous data sheets and libraries such as Adafruit_Python_CharLCD.
    /// </remarks>
    public class Hd44780 : IDisposable
    {
        private bool _disposed;

        protected const byte ClearDisplayCommand = 0b_0001;
        protected const byte ReturnHomeCommand = 0b_0010;

        protected const byte SetCGRamAddressCommand = 0b_0100_0000;
        protected const byte SetDDRamAddressCommand = 0b_1000_0000;

        internal DisplayFunction _displayFunction = DisplayFunction.Command;
        internal DisplayControl _displayControl = DisplayControl.Command;
        internal DisplayEntryMode _displayMode = DisplayEntryMode.Command;

        protected readonly byte[] _rowOffsets;

        protected readonly LcdInterface _interface;

        /// <summary>
        /// Logical size, in characters, of the LCD.
        /// </summary>
        public Size Size { get; }

        /// <summary>
        /// Initializes a new HD44780 LCD controller.
        /// </summary>
        /// <param name="size">The logical size of the LCD.</param>
        /// <param name="interface">The interface to use with the LCD.</param>
        public Hd44780(Size size, LcdInterface @interface)
        {
            Size = size;
            _interface = @interface;

            // if (_interface.EightBitMode)
            // {
            //     Console.WriteLine("Function: Eight bit.");
            //     _displayFunction |= DisplayFunction.EightBit;
            // }

            Initialize(size.Height);
            _rowOffsets = InitializeRowOffsets(size.Height);
        }

        /// <summary>
        /// Initializes the display by setting the specified columns and lines.
        /// </summary>
        private void Initialize(int rows)
        {
            // Setup 4-bit mode
            // See Figure 24, on page 46
            
	    // Wait for startup
            WaitForNotBusy(15000);

            // Send three three time to get chip into sync
            SendNibble(0x30);        // Function set 0b0011 - 8-bit
            WaitForNotBusy(4100);
            SendNibble(0x30);        // Function set 0b0011 - 8-bit
            WaitForNotBusy(100);
            SendNibble(0x30);        // Function set 0b0011 - 8-bit
            WaitForNotBusy(37);

            // Set 4-bit mode, 2-Line and font
            // Number of display lines, and  font cannot be changed after this command 
            SendNibble(0x20);        // Function set 0b0010 - 4-bit, as an 8-bit instruction
            WaitForNotBusy(37);
            SendNibble(0x20);        // Function set 0b0010 - 4-bit, as first 4-bit
            WaitForNotBusy(37);
            SendNibble(0x80);        // Function set 0bnn** - 2-line, Font, as second 4-bit
            WaitForNotBusy(37);

            // Display on
            // buffer[1] = 0x00;
            // _interface.SendData(buffer);        // Display set 0b0000
            // buffer[1] = 0xC0;
            // _interface.SendData(buffer);        // Display set 0b1nnn - Display, Cursor, Blink
            // WaitForNotBusy(37);
            
            // Clear entire display
            // buffer[1] = 0x00;
            // _interface.SendData(buffer);        // Clear display 0b0000
            // buffer[1] = 0x10;
            // _interface.SendData(buffer);        // Clear display 0b0001 - Clear entire display
            // // WaitForNotBusy(4100);
            
            // Set Mode
            // buffer[1] = 0x00;
            // _interface.SendData(buffer);        // Entry Mode set 0b0000
            // buffer[1] = 0x60;
            // _interface.SendData(buffer);        // Entry Mode set 0b01nn - I/D and S
            // WaitForNotBusy(37);

            // While the chip supports 5x10 pixel characters for one line displays they
            // don't seem to be generally available. Supporting 5x10 would require extra
            // support for CreateCustomCharacter

            // if (SetTwoLineMode(rows))
            //     _displayFunction |= DisplayFunction.TwoLine;

            _displayControl |= DisplayControl.DisplayOn;
            _displayMode |= DisplayEntryMode.Increment;
            ReadOnlySpan<byte> commands = stackalloc byte[]
            {
                // Function must be set first to ensure that we always have the basic
                // instruction set selected. (See PCF2119x datasheet Function_set note
                // for one documented example of where this is necessary.)
                // @@ ReturnHomeCommand,    // 0x02
                // (byte)_displayFunction,  // 0x28
                (byte)_displayControl,      // 0x0c
                ClearDisplayCommand,        // 0x01
                (byte)_displayMode,         // 0x06
                (byte)0x80,                 // 0x80 - Sets address
                // (byte)0x0E,                 // 0x0E <-- not valid?
            };

            SendCommands(commands);
        }

        /// <summary>
        /// Enable/disable the backlight. (Will always return false if no backlight pin was provided.)
        /// </summary>
        public virtual bool BacklightOn
        {
            get => _interface.BacklightOn;
            set => _interface.BacklightOn = value;
        }

        protected void SendData(byte value)
        {
            Span<byte> buffer = stackalloc byte[2];
            buffer[0] = 0x00;

            // Wait for busy flag

            buffer[1] = (byte)((value & 0xF0) | 0x09);
            _interface.SendData(buffer);
            WaitForNotBusy(500);
            buffer[1] = (byte)((value & 0xF0) | 0x09 | 0x04u);
            _interface.SendData(buffer);
            WaitForNotBusy(500);
            buffer[1] = (byte)((value & 0xF0) | 0x09);
            _interface.SendData(buffer);
            WaitForNotBusy(4100);
            buffer[1] = (byte)((value << 4) | 0x09);
            _interface.SendData(buffer);
            WaitForNotBusy(500);
            buffer[1] = (byte)((value << 4) | 0x09 | 0x04u);
            _interface.SendData(buffer);
            WaitForNotBusy(500);
            buffer[1] = (byte)((value << 4) | 0x09);
            _interface.SendData(buffer);
            WaitForNotBusy(4100);
        }

        protected void SendNibble(byte cmd)
        {
            Console.WriteLine("Send nibble: 0x{0:x2}", cmd);

            Span<byte> buffer = stackalloc byte[2];
            buffer[0] = 0x00;

            buffer[1] = (byte)((cmd & 0xF0) | 0x08);
            _interface.SendData(buffer);
            buffer[1] = (byte)((cmd & 0xF0) | 0x08 | 0x04u);
            _interface.SendData(buffer);
            WaitForNotBusy(4);
            buffer[1] = (byte)((cmd & 0xF0) | 0x08);
            _interface.SendData(buffer);
        }

        protected void SendCommand(byte cmd)
        {
            Console.WriteLine("Send cmd: 0x{0:x2}", cmd);

            Span<byte> buffer = stackalloc byte[2];
            buffer[0] = 0x00;

            // Wait for busy flag

            buffer[1] = (byte)((cmd & 0xF0) | 0x08);
            _interface.SendData(buffer);
            WaitForNotBusy(500);
            buffer[1] = (byte)((cmd & 0xF0) | 0x08 | 0x04u);
            _interface.SendData(buffer);
            WaitForNotBusy(500);
            buffer[1] = (byte)((cmd & 0xF0) | 0x08);
            _interface.SendData(buffer);
            WaitForNotBusy(4100);
            buffer[1] = (byte)((cmd << 4) | 0x08);
            _interface.SendData(buffer);
            WaitForNotBusy(500);
            buffer[1] = (byte)((cmd << 4) | 0x08 | 0x04u);
            _interface.SendData(buffer);
            WaitForNotBusy(500);
            buffer[1] = (byte)((cmd << 4) | 0x08);
            _interface.SendData(buffer);
            WaitForNotBusy(4100);
        }


        protected void SendData(ReadOnlySpan<byte> values)
        {
            foreach (var value in values)
                SendData(value);
        }

        protected void SendCommands(ReadOnlySpan<byte> commands) // => _interface.SendCommands(commands);
        {
            // There is a limit to how much data the controller can accept at once. Haven't found documentation
            // for this yet, can probably iterate a bit more on this to find a true "max". Not adding additional
            // logic like SendData as we don't expect a need to send more than a handful of commands at a time.
            if (commands.Length > 20)
                throw new ArgumentOutOfRangeException(nameof(commands), "Too many commands in one request.");

            foreach (var cmd in commands)
                SendCommand(cmd);
        }


        protected virtual bool SetTwoLineMode(int rows) => rows > 1;


        protected virtual byte[] InitializeRowOffsets(int rows)
        {
            // In one-line mode DDRAM addresses go from 0 - 79 [0x00 - 0x4F]
            //
            // In two-line mode DDRAM addresses are laid out as follows:
            //
            //   First row:  0 - 39   [0x00 - 0x27]
            //   Second row: 64 - 103 [0x40 - 0x67]
            //
            // (The address gap presumably is to allow all second row addresses to be
            // identifiable with one bit? Not sure what the value of that is.)
            //
            // The chipset doesn't natively support more than two rows. For tested
            // four row displays the two rows are split as follows:
            //
            //   First row:  0 - 19   [0x00 - 0x13]
            //   Second row: 64 - 83  [0x40 - 0x53]
            //   Third row:  20 - 39  [0x14 - 0x27]  (Continues first row)
            //   Fourth row: 84 - 103 [0x54 - 0x67]  (Continues second row)

            byte[] rowOffsets;

            switch (rows)
            {
                case 1:
                    rowOffsets = new byte[1];
                    break;
                case 2:
                    rowOffsets = new byte[] { 0, 64 };
                    break;
                case 4:
                    rowOffsets = new byte[] { 0, 64, 20, 84 };
                    break;
                default:
                    // We don't support other rows, users can derive for odd cases.
                    // (Three row LCDs exist, but aren't common.)
                    throw new ArgumentOutOfRangeException(nameof(rows));
            }

            return rowOffsets;
        }

        /// <summary>
        /// Wait for the device to not be busy.
        /// </summary>
        /// <param name="microseconds">Time to wait if checking busy state isn't possible/practical.</param>
        protected void WaitForNotBusy(int microseconds)
        {
            _interface.WaitForNotBusy(microseconds);
        }

        /// <summary>
        /// Clears the LCD, returning the cursor to home and unshifting if shifted.
        /// Will also set to Increment.
        /// </summary>
        public void Clear()
        {
            SendCommand(ClearDisplayCommand);

            // The HD44780 spec doesn't call out how long this takes. Home is documented as
            // taking 1.52ms, and as this does more work (sets all memory to the space character)
            // we do a longer wait. On the PCF2119x it is described as taking 165 clock cycles which
            // would be 660μs on the "typical" clock.
            WaitForNotBusy(2000);
        }

        /// <summary>
        /// Moves the cursor to the first line and first column, unshifting if shifted.
        /// </summary>
        public void Home()
        {
            SendCommand(ReturnHomeCommand);

            // The return home command is documented as taking 1.52ms with the standard 270KHz clock.
            // SendCommand already waits for 37μs, 
            WaitForNotBusy(1520);
        }

        /// <summary>
        /// Moves the cursor to an explicit column and row position.
        /// </summary>
        /// <param name="left">The column position from left to right starting with 0.</param>
        /// <param name="top">The row position from the top starting with 0.</param>
        public void SetCursorPosition(int left, int top)
        {
            var rows = _rowOffsets.Length;
            if (top < 0 || top >= rows)
                throw new ArgumentOutOfRangeException(nameof(top));

            // Throw if we're given a negative left value or the calculated address would be
            // larger than the max "good" address. Addressing is covered in detail in
            // InitializeRowOffsets above.

            var newAddress = left + _rowOffsets[top];
            if (left < 0 || (rows == 1 && newAddress >= 80) || (rows > 1 && newAddress >= 104))
                throw new ArgumentOutOfRangeException(nameof(left));

            SendCommand((byte)(SetDDRamAddressCommand | newAddress));
        }

        /// <summary>
        /// Enable/disable the display.
        /// </summary>
        public bool DisplayOn
        {
            get => (_displayControl & DisplayControl.DisplayOn) > 0;
            set => SendCommand((byte)(value ? _displayControl |= DisplayControl.DisplayOn
                : _displayControl &= ~DisplayControl.DisplayOn));
        }

        /// <summary>
        /// Enable/disable the underline cursor.
        /// </summary>
        public bool UnderlineCursorVisible
        {
            get => (_displayControl & DisplayControl.CursorOn) > 0;
            set => SendCommand((byte)(value ? _displayControl |= DisplayControl.CursorOn
                : _displayControl &= ~DisplayControl.CursorOn));
        }

        /// <summary>
        /// Enable/disable the blinking cursor.
        /// </summary>
        public bool BlinkingCursorVisible
        {
            get => (_displayControl & DisplayControl.BlinkOn) > 0;
            set => SendCommand((byte)(value ? _displayControl |= DisplayControl.BlinkOn
                : _displayControl &= ~DisplayControl.BlinkOn));
        }

        /// <summary>
        /// When enabled the display will shift rather than the cursor.
        /// </summary>
        public bool AutoShift
        {
            get => (_displayMode & DisplayEntryMode.DisplayShift) > 0;
            set => SendCommand((byte)(value ? _displayMode |= DisplayEntryMode.DisplayShift
                : _displayMode &= ~DisplayEntryMode.DisplayShift));
        }

        /// <summary>
        /// Gets/sets whether the cursor location increments (true) or decrements (false).
        /// </summary>
        public bool Increment
        {
            get => (_displayMode & DisplayEntryMode.Increment) > 0;
            set => SendCommand((byte)(value ? _displayMode |= DisplayEntryMode.Increment
                : _displayMode &= ~DisplayEntryMode.Increment));
        }

        /// <summary>
        /// Move the display left one position.
        /// </summary>
        public void ShiftDisplayLeft() => SendCommand((byte)(DisplayShift.Command | DisplayShift.Display));

        /// <summary>
        /// Move the display right one position.
        /// </summary>
        public void ShiftDisplayRight() => SendCommand((byte)(DisplayShift.Command | DisplayShift.Display | DisplayShift.Right));

        /// <summary>
        /// Move the cursor left one position.
        /// </summary>
        public void ShiftCursorLeft() => SendCommand((byte)(DisplayShift.Command | DisplayShift.Display));

        /// <summary>
        /// Move the cursor right one position.
        /// </summary>
        public void ShiftCursorRight() => SendCommand((byte)(DisplayShift.Command | DisplayShift.Display | DisplayShift.Right));

        /// <summary>
        /// Fill one of the 8 CGRAM locations (character codes 0 - 7) with custom characters.
        /// </summary>
        /// <remarks>
        /// The custom characters also occupy character codes 8 - 15.
        /// 
        /// You can find help designing characters at https://www.quinapalus.com/hd44780udg.html.
        /// 
        /// The datasheet description for custom characters is very difficult to follow. Here is
        /// a rehash of the technical details that is hopefully easier:
        /// 
        /// Only 6 bits of addresses are available for character ram. That makes for 64 bytes of
        /// available character data. 8 bytes of data are used for each character, which is where
        /// the 8 total custom characters comes from (64/8).
        /// 
        /// Each byte corresponds to a character line. Characters are only 5 bits wide so only
        /// bits 0-4 are used for display. Whatever is in bits 5-7 is just ignored. Store bits
        /// there if it makes you happy, but it won't impact the display. '1' is on, '0' is off.
        /// 
        /// In the built-in characters the 8th byte is usually empty as this is where the underline
        /// cursor will be if enabled. You can put data there if you like, which gives you the full
        /// 5x8 character. The underline cursor just turns on the entire bottom row.
        /// 
        /// 5x10 mode is effectively useless as displays aren't available that utilize it. In 5x10
        /// mode *16* bytes of data are used for each character. That leaves room for only *4*
        /// custom characters. The first character is addressable from code 0, 1, 8, and 9. The
        /// second is 2, 3, 10, 11 and so on...
        /// 
        /// In this mode *11* bytes of data are actually used for the character data, which
        /// effectively gives you a 5x11 character, although typically the last line is blank to
        /// leave room for the underline cursor. Why the modes are referred to as 5x8 and 5x10 as
        /// opposed to 5x7 and 5x10 or 5x8 and 5x11 is a mystery. In an early pre-release data
        /// book 5x7 and 5x10 is used (Advance Copy #AP4 from July 1985). Perhaps it was a
        /// marketing change?
        /// 
        /// As only 11 bytes are used in 5x10 mode, but 16 bytes are reserved, the last 5 bytes
        /// are useless. The datasheet helpfully suggests that you can store your own data there.
        /// The same would be true for bits 5-7 of lines that matter for both 5x8 and 5x10.
        /// </remarks>
        /// <param name="location">Should be between 0 and 7</param>
        /// <param name="characterMap">Provide an array of 8 bytes containing the pattern</param>
        public void CreateCustomCharacter(byte location, params byte[] characterMap)
        {
            if (characterMap == null)
                throw new ArgumentNullException(nameof(characterMap));

            CreateCustomCharacter(location, characterMap.AsSpan());
        }

        /// <summary>
        /// Fill one of the 8 CGRAM locations (character codes 0 - 7) with custom characters.
        /// </summary>
        /// <param name="location">Should be between 0 and 7</param>
        /// <param name="characterMap">Provide an array of 8 bytes containing the pattern</param>
        public void CreateCustomCharacter(byte location, ReadOnlySpan<byte> characterMap)
        {
            if (location > 7)
                throw new ArgumentOutOfRangeException(nameof(location));

            if (characterMap.Length != 8)
                throw new ArgumentException(nameof(characterMap));

            // The character address is set in bits 3-5 of the command byte
            SendCommand((byte)(SetCGRamAddressCommand | (location << 3)));
            SendData(characterMap);
        }

        /// <summary>
        /// Write text to display.
        /// </summary>
        /// <remarks>
        /// There are only 256 characters available. There are chip variants
        /// with different character sets. Characters from space ' ' (32) to
        /// '}' are usually the same with the exception of '\', which is a
        /// yen symbol on some chips '¥'.
        /// </remarks>
        /// <param name="value">Text to be displayed.</param>
        public void Write(string value)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(value.Length);
            for (var i = 0; i < value.Length; ++i)
                buffer[i] = (byte)value[i];

            SendData(new ReadOnlySpan<byte>(buffer, 0, value.Length));
            ArrayPool<byte>.Shared.Return(buffer);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                _interface?.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Dispose(true);
            _disposed = true;
        }
    }
}
