//
// ANSIDriver.cs: ANSI Esc Sequence (Virtual Terminal Sequences) driver
//

// HACK:
// WindowsANSIConsole/Terminal has two issues:
// 1) Tearing can occur when the console is resized.
// 2) The values provided during Init (and the first WindowsANSIConsole.EventType.WindowBufferSize) are not correct.
//
// If HACK_CHECK_WINCHANGED is defined then we ignore WindowsANSIConsole.EventType.WindowBufferSize events
// and instead check the console size every every 500ms in a thread in WidowsMainLoop. 
// As of Windows 11 23H2 25947.1000 and/or WT 1.19.2682 tearing no longer occurs when using 
// the WindowsANSIConsole.EventType.WindowBufferSize event. However, on Init the window size is
// still incorrect so we still need this hack.
//#define HACK_CHECK_WINCHANGED

using System.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Win32;
using static Terminal.Gui.EscSeqUtils;
using Windows.Win32.System.Console;
using Microsoft.Win32.SafeHandles;
using Terminal.Gui.ConsoleDrivers;
using static Terminal.Gui.WindowsANSIConsole;

namespace Terminal.Gui;

internal class WindowsANSIConsole {

	SafeFileHandle _inputHandle, _outputHandle;
	readonly CONSOLE_MODE _originalConsoleMode;
	CursorVisibility? _initialCursorVisibility = null;
	CursorVisibility? _currentCursorVisibility = null;
	CursorVisibility? _pendingCursorVisibility = null;
	
	public WindowsANSIConsole ()
	{
		_inputHandle = new SafeFileHandle (PInvoke.GetStdHandle (STD_HANDLE.STD_INPUT_HANDLE), false);
		_outputHandle = new SafeFileHandle (PInvoke.GetStdHandle (STD_HANDLE.STD_OUTPUT_HANDLE), false);

		_originalConsoleMode = ConsoleMode;
		var newConsoleMode = _originalConsoleMode;

		newConsoleMode |= CONSOLE_MODE.ENABLE_MOUSE_INPUT | CONSOLE_MODE.ENABLE_EXTENDED_FLAGS | CONSOLE_MODE.ENABLE_VIRTUAL_TERMINAL_PROCESSING;
		newConsoleMode &= ~CONSOLE_MODE.ENABLE_QUICK_EDIT_MODE;
		newConsoleMode &= ~CONSOLE_MODE.ENABLE_PROCESSED_INPUT;
		ConsoleMode = newConsoleMode;
	}
	
	public bool Write (string text)
	{
		unsafe {
			var pText = Marshal.AllocHGlobal (text.Length * sizeof (char));
			try {
				var pTextPtr = (char*)pText;
				for (int i = 0; i < text.Length; i++) {
					*pTextPtr++ = text [i];
				}
				uint charsWritten = 0;
				return PInvoke.WriteConsole (_outputHandle, (void*)pText, (uint)text.Length, &charsWritten);
			} catch (Exception) {
				return false;
			} finally {
				Marshal.FreeHGlobal (pText);
			}
		}
	}

	public bool SetCursorPosition (COORD position)
	{
		return PInvoke.SetConsoleCursorPosition (_outputHandle, position);
	}

	public void SetInitialCursorVisibility ()
	{
		if (_initialCursorVisibility.HasValue == false && GetCursorVisibility (out CursorVisibility visibility)) {
			_initialCursorVisibility = visibility;
		}
	}

	public bool GetCursorVisibility (out CursorVisibility visibility)
	{
		if (_outputHandle == null) {
			visibility = CursorVisibility.Invisible;
			return true;
		}
		if (!PInvoke.GetConsoleCursorInfo (_outputHandle, out CONSOLE_CURSOR_INFO info)) {
			var err = Marshal.GetLastWin32Error ();
			if (err != 0) {
				throw new Win32Exception (err);
			}
			visibility = CursorVisibility.Default;

			return false;
		}

		if (!info.bVisible) {
			visibility = CursorVisibility.Invisible;
		} else if (info.dwSize > 50) {
			visibility = CursorVisibility.Box;
		} else {
			visibility = CursorVisibility.Underline;
		}

		return true;
	}

	public bool EnsureCursorVisibility ()
	{
		if (_initialCursorVisibility.HasValue && _pendingCursorVisibility.HasValue && SetCursorVisibility (_pendingCursorVisibility.Value)) {
			_pendingCursorVisibility = null;

			return true;
		}

		return false;
	}

	public void ForceRefreshCursorVisibility ()
	{
		if (_currentCursorVisibility.HasValue) {
			_pendingCursorVisibility = _currentCursorVisibility;
			_currentCursorVisibility = null;
		}
	}

	public bool SetCursorVisibility (CursorVisibility visibility)
	{
		if (_initialCursorVisibility.HasValue == false) {
			_pendingCursorVisibility = visibility;

			return false;
		}

		if (_currentCursorVisibility.HasValue == false || _currentCursorVisibility.Value != visibility) {
			var stringBuilder = new StringBuilder ();
			if (visibility != CursorVisibility.Invisible) {
				stringBuilder.Append (EscSeqUtils.CSI_ShowCursor);
				switch (visibility) {
				case CursorVisibility.Box:
					stringBuilder.Append (EscSeqUtils.CSI_SetCursorStyle (DECSCUSR_Style.BlinkingBlock));
					break;
				case CursorVisibility.BoxFix:
					stringBuilder.Append (EscSeqUtils.CSI_SetCursorStyle (DECSCUSR_Style.SteadyBlock));
					break;

				case CursorVisibility.Vertical:
					stringBuilder.Append (EscSeqUtils.CSI_SetCursorStyle (DECSCUSR_Style.BlinkingBar));
					break;
				case CursorVisibility.VerticalFix:
					stringBuilder.Append (EscSeqUtils.CSI_SetCursorStyle (DECSCUSR_Style.SteadyBar));
					break;

				case CursorVisibility.UnderlineFix:
					stringBuilder.Append (EscSeqUtils.CSI_SetCursorStyle (DECSCUSR_Style.SteadyUnderline));
					break;

				case CursorVisibility.Underline:
					stringBuilder.Append (EscSeqUtils.CSI_SetCursorStyle (DECSCUSR_Style.BlinkingUnderline));
					break;

				case CursorVisibility.Default:
				default:
					stringBuilder.Append (EscSeqUtils.CSI_SetCursorStyle (DECSCUSR_Style.UserShape));
					break;
				}
			} else {
				stringBuilder.Append (EscSeqUtils.CSI_HideCursor);
			}
			Write (stringBuilder.ToString ());

			_currentCursorVisibility = visibility;
		}

		return true;
	}

	public void Cleanup ()
	{
		if (_initialCursorVisibility.HasValue) {
			SetCursorVisibility (_initialCursorVisibility.Value);
		}

		ConsoleMode = _originalConsoleMode;
		//if (!PInvoke.SetConsoleActiveScreenBuffer (_outputHandle)) {
		//	var err = Marshal.GetLastWin32Error ();
		//	Console.WriteLine ("Error: {0}", err);
		//}
	}


	internal Size GetConsoleOutputWindow (out Point position)
	{
		var csbi = new CONSOLE_SCREEN_BUFFER_INFOEX ();
		csbi.cbSize = (uint)Marshal.SizeOf (csbi);
		if (!PInvoke.GetConsoleScreenBufferInfoEx (_outputHandle, ref csbi)) {
			throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
		}
		var sz = new Size (csbi.srWindow.Right - csbi.srWindow.Left + 1,
			csbi.srWindow.Bottom - csbi.srWindow.Top + 1);
		position = new Point (csbi.srWindow.Left, csbi.srWindow.Top);

		return sz;
	}

	CONSOLE_MODE ConsoleMode {
		get {
			PInvoke.GetConsoleMode (_inputHandle, out CONSOLE_MODE v);
			return v;
		}
		set => PInvoke.SetConsoleMode (_inputHandle, value);
	}

	[Flags]
	public enum MOUSE_BUTTON_STATE {
		FROM_LEFT_1ST_BUTTON_PRESSED = 1,
		FROM_LEFT_2ND_BUTTON_PRESSED = 4,
		FROM_LEFT_3RD_BUTTON_PRESSED = 8,
		FROM_LEFT_4TH_BUTTON_PRESSED = 16,
		RIGHTMOST_BUTTON_PRESSED = 2
	}

	[Flags]
	public enum CONTROL_KEY_STATE {
		RIGHT_ALT_PRESSED = 1,
		LEFT_ALT_PRESSED = 2,
		RIGHT_CTRL_PRESSED = 4,
		LEFT_CTRL_PRESSED = 8,
		SHIFT_PRESSED = 16,
		NUMLOCK_ON = 32,
		SCROLLLOCK_ON = 64,
		CAPSLOCK_ON = 128,
		ENHANCED_KEY = 256
	}

	[Flags]
	public enum MOUSE_EVENT_FLAGS {
		MOUSE_MOVED = 1,
		DOUBLE_CLICK = 2,
		MOUSE_WHEELED = 4,
		MOUSE_HWHEELED = 8
	}

	public enum INPUT_RECORD_EVENT_TYPE : ushort {
		FOCUS_EVENT = 0x10,
		KEY_EVENT = 0x1,
		MENU_EVENT = 0x8,
		MOUSE_EVENT = 2,
		WINDOW_BUFFER_SIZE_EVENT = 4
	}

	[StructLayout (LayoutKind.Explicit, CharSet = CharSet.Unicode)]
	public struct CharUnion {
		[FieldOffset (0)] public char UnicodeChar;
		[FieldOffset (0)] public byte AsciiChar;
	}

	public struct ExtendedCharInfo {
		public char Char { get; set; }
		public Attribute Attribute { get; set; }
		public bool Empty { get; set; } // TODO: Temp hack until virutal terminal sequences

		public ExtendedCharInfo (char character, Attribute attribute)
		{
			Char = character;
			Attribute = attribute;
			Empty = false;
		}
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct SmallRect {
		public short Left;
		public short Top;
		public short Right;
		public short Bottom;

		public SmallRect (short left, short top, short right, short bottom)
		{
			Left = left;
			Top = top;
			Right = right;
			Bottom = bottom;
		}

		public static void MakeEmpty (ref SmallRect rect)
		{
			rect.Left = -1;
		}

		public static void Update (ref SmallRect rect, short col, short row)
		{
			if (rect.Left == -1) {
				rect.Left = rect.Right = col;
				rect.Bottom = rect.Top = row;
				return;
			}
			if (col >= rect.Left && col <= rect.Right && row >= rect.Top && row <= rect.Bottom)
				return;
			if (col < rect.Left)
				rect.Left = col;
			if (col > rect.Right)
				rect.Right = col;
			if (row < rect.Top)
				rect.Top = row;
			if (row > rect.Bottom)
				rect.Bottom = row;
		}

		public override readonly string ToString () => $"Left={Left},Top={Top},Right={Right},Bottom={Bottom}";
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct ConsoleKeyInfoEx {
		public ConsoleKeyInfo ConsoleKeyInfo;
		public bool CapsLock;
		public bool NumLock;
		public bool ScrollLock;

		public ConsoleKeyInfoEx (ConsoleKeyInfo consoleKeyInfo, bool capslock, bool numlock, bool scrolllock)
		{
			ConsoleKeyInfo = consoleKeyInfo;
			CapsLock = capslock;
			NumLock = numlock;
			ScrollLock = scrolllock;
		}
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct ConsoleCursorInfo {
		public uint dwSize;
		public bool bVisible;
	}
	public INPUT_RECORD [] ReadConsoleInput ()
	{
		Span<INPUT_RECORD> records = new Span<INPUT_RECORD> (new INPUT_RECORD [128]);
		unsafe {
			try {
				const int bufferSize = 1;
				if (!PInvoke.ReadConsoleInput (_inputHandle, records, out uint numberEventsRead)) {

				}

			} catch (Exception) {
				return null;
			}
		}
		return records.ToArray ();
	}


	[StructLayout (LayoutKind.Explicit, Size = 4)]
	public struct COLORREF {
		public COLORREF (byte r, byte g, byte b)
		{
			Value = 0;
			R = r;
			G = g;
			B = b;
		}

		public COLORREF (uint value)
		{
			R = 0;
			G = 0;
			B = 0;
			Value = value & 0x00FFFFFF;
		}

		[FieldOffset (0)]
		public byte R;
		[FieldOffset (1)]
		public byte G;
		[FieldOffset (2)]
		public byte B;

		[FieldOffset (0)]
		public uint Value;
	}
}

internal class ANSIDriver : ConsoleDriver {
	WindowsANSIConsole.SmallRect _damageRegion;

	public WindowsANSIConsole WinConsole { get; private set; }

	public override bool SupportsTrueColor => RunningUnitTests || (Environment.OSVersion.Version.Build >= 14931 && _isWindowsTerminal);

	readonly bool _isWindowsTerminal = false;
	AnsiMainLoopDriver _mainLoopDriver = null;

	public ANSIDriver ()
	{
		if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
			WinConsole = new WindowsANSIConsole ();
			// otherwise we're probably running in unit tests
			Clipboard = new WindowsClipboard ();
		} else {
			Clipboard = new FakeDriver.FakeClipboard ();
		}

		// TODO: if some other Windows-based terminal supports true color, update this logic to not
		// force 16color mode (.e.g ConEmu which really doesn't work well at all).
		_isWindowsTerminal = Environment.GetEnvironmentVariable ("WT_SESSION") != null;
		if (!_isWindowsTerminal) {
			Force16Colors = true;
		}
	}

	internal override MainLoop Init ()
	{
		_mainLoopDriver = new AnsiMainLoopDriver (this);
		if (!RunningUnitTests) {
			try {
				if (WinConsole != null) {
					// BUGBUG: The results from GetConsoleOutputWindow are incorrect when called from Init. 
					// Our thread in AnsiMainLoopDriver.CheckWin will get the correct results. See #if HACK_CHECK_WINCHANGED
					var winSize = WinConsole.GetConsoleOutputWindow (out Point pos);
					Cols = winSize.Width;
					Rows = winSize.Height;
				}
				WindowsANSIConsole.SmallRect.MakeEmpty (ref _damageRegion);

				if (_isWindowsTerminal) {
					WinConsole.Write (EscSeqUtils.CSI_SaveCursorAndActivateAltBufferNoBackscroll);
				}
			} catch (Win32Exception e) {
				// We are being run in an environment that does not support a console
				// such as a unit test, or a pipe.
				Debug.WriteLine ($"Likely running unit tests. Setting WinConsole to null so we can test it elsewhere. Exception: {e}");
				WinConsole = null;
			}
		}

		CurrentAttribute = new Attribute (Color.White, Color.Black);

		Clip = new Rect (0, 0, Cols, Rows);
		_damageRegion = new WindowsANSIConsole.SmallRect () {
			Top = 0,
			Left = 0,
			Bottom = (short)Rows,
			Right = (short)Cols
		};

		ClearContents ();

		WinConsole?.SetInitialCursorVisibility ();

#if HACK_CHECK_WINCHANGED
		_mainLoopDriver.WinChanged = ChangeWin;
#endif
		return new MainLoop (_mainLoopDriver);

	}

#if HACK_CHECK_WINCHANGED
	private void ChangeWin (Object s, SizeChangedEventArgs e)
	{
		var w = e.Size.Width;
		if (w == Cols - 3 && e.Size.Height < Rows) {
			w += 3;
		}
		Left = 0;
		Top = 0;
		Cols = e.Size.Width;
		Rows = e.Size.Height;

		ResizeScreen ();
		ClearContents ();
		OnSizeChanged (new SizeChangedEventArgs (new Size (Cols, Rows)));
	}
#endif
	
	public override bool IsRuneSupported (Rune rune)
	{
		return base.IsRuneSupported (rune) && rune.IsBmp;
	}

	void ResizeScreen ()
	{
		Clip = new Rect (0, 0, Cols, Rows);
		_damageRegion = new WindowsANSIConsole.SmallRect () {
			Top = 0,
			Left = 0,
			Bottom = (short)Rows,
			Right = (short)Cols
		};
		_dirtyLines = new bool [Rows];

		WinConsole?.ForceRefreshCursorVisibility ();
	}

	public override void UpdateScreen ()
	{
		if (RunningUnitTests) {
			return;
		}

		var stringBuilder = new StringBuilder ();
		stringBuilder.Clear ();

		stringBuilder.Append (EscSeqUtils.CSI_SetCursorPosition (1, 1));
		WinConsole.Write (stringBuilder.ToString ());

		var top = 0;
		var left = 0;
		var rows = Rows;
		var cols = Cols;
		Attribute redrawAttr = new Attribute ();
		var nextCol = -1; // The net column we can write to. -1 if we've not processed anything yet.

		for (var row = top; row < rows; row++) {
			stringBuilder.Clear ();
			stringBuilder.Append (EscSeqUtils.CSI_SetCursorPosition (row + 1, 1));
			_dirtyLines [row] = false;
			for (var col = left; col < cols; col++) {
				nextCol = -1;
				var outputWidth = 0;
				for (; col < cols; col++) {
					if (!Contents [row, col].IsDirty) {
						if (stringBuilder.Length > 0) {
							// We've collected outputWidth dirty cells on this row. Output them.
							WriteToConsole (stringBuilder, ref nextCol, row, ref outputWidth);
							// nextCol now points to the last column we write to (nextCol += outputWidth)
							// outputWidth is now 0
						} else if (nextCol == -1) {
							nextCol = col;
						}
						if (nextCol + 1 < cols) {
							nextCol++;
						}
						continue;
					}

					if (nextCol == -1) {
						nextCol = col;
					}

					Attribute attr = Contents [row, col].Attribute.Value;
					// Performance: Only send the escape sequence if the attribute has changed.
					if (attr != redrawAttr) {
						redrawAttr = attr;
						if (Force16Colors) {
							stringBuilder.Append (EscSeqUtils.CSI_SetForegroundColor (attr.Foreground.AnsiColorCode));
							stringBuilder.Append (EscSeqUtils.CSI_SetBackgroundColor (attr.Background.AnsiColorCode));
						} else {
							stringBuilder.Append (EscSeqUtils.CSI_SetForegroundColorRGB (attr.Foreground.R, attr.Foreground.G, attr.Foreground.B));
							stringBuilder.Append (EscSeqUtils.CSI_SetBackgroundColorRGB (attr.Background.R, attr.Background.G, attr.Background.B));
						}

					}

					outputWidth++;
					var rune = (Rune)Contents [row, col].Rune;
					stringBuilder.Append (rune);
					if (Contents [row, col].CombiningMarks.Count > 0) {
						// AtlasEngine does not support NON-NORMALIZED combining marks in a way
						// compatible with the driver architecture. Any CMs (except in the first col)
						// are correctly combined with the base char, but are ALSO treated as 1 column
						// width codepoints E.g. `echo "[e`u{0301}`u{0301}]"` will output `[�  ]`.
						// 
#if COMBINING_MARKS_SUPPORTED
						foreach (var combMark in Contents [row, col].CombiningMarks) {
							stringBuilder.Append (combMark);
						}
						WriteToConsole (stringBuilder, ref nextCol, row, ref outputWidth);
#endif
					}
					if ((rune.IsSurrogatePair () && rune.GetColumns () < 2)) {
						// AtlasEngine treats all SurrogatePairs as 2 columns
						// BUGBUG: Regional Indicator Symbol Letters A-Z - U+1f1e6-U+1f1ff render in WT 
						// weird
						WriteToConsole (stringBuilder, ref nextCol, row, ref outputWidth);
						//SetCursorPosition (col - 1, row);
					}
					Contents [row, col].IsDirty = false;
				}
			}
			if (stringBuilder.Length > 0) {
				stringBuilder.Insert (0, EscSeqUtils.CSI_SetCursorPosition (row + 1, nextCol + 1));
				WinConsole.Write (stringBuilder.ToString ());
			}
		}

		stringBuilder.Append (EscSeqUtils.CSI_SetCursorPosition (Row + 1, Col + 1));

		WinConsole.Write (stringBuilder.ToString ());

		void WriteToConsole (StringBuilder output, ref int lastCol, int row, ref int outputWidth)
		{
			var s = output.ToString ();
			WinConsole.Write (s);
			output.Clear ();
			lastCol += outputWidth;
			outputWidth = 0;
		}
	}

	public override void Refresh ()
	{
		UpdateScreen ();
		UpdateCursor ();
	}
	
	bool _altDown = false;
	
	internal void ProcessInput (INPUT_RECORD inputEvent)
	{
		switch (inputEvent.EventType) {
		case (ushort)INPUT_RECORD_EVENT_TYPE.KEY_EVENT:
			var fromPacketKey = inputEvent.Event.KeyEvent.wVirtualKeyCode == (uint)ConsoleKey.Packet;
			if (fromPacketKey) {
				inputEvent.Event.KeyEvent = FromVKPacketToKeyEventRecord (inputEvent.Event.KeyEvent);
			}
			var keyInfo = ToConsoleKeyInfoEx (inputEvent.Event.KeyEvent);
			Debug.WriteLine ($"event: {inputEvent.ToString ()} {keyInfo.ToString ()}");

			var map = MapKey (keyInfo);

			if (inputEvent.Event.KeyEvent.bKeyDown) {
				_altDown = keyInfo.ConsoleKeyInfo.Modifiers == ConsoleModifiers.Alt;
				// Avoid sending repeat keydowns
				OnKeyDown (new Key (map));
			} else {
				var keyPressedEventArgs = new Key (map);

				// PROTOTYPE: This logic enables `Alt` key presses (down, up, pressed).
				// However, if while the 'Alt' key is down, if another key is pressed and
				// released, there will be a keypressed event for that and the
				// keypressed event for just `Alt` will be suppressed. 
				// This allows MenuBar to have `Alt` as a keybinding
				if (map != KeyCode.AltMask) {
					if (keyInfo.ConsoleKeyInfo.Modifiers.HasFlag (ConsoleModifiers.Alt)) {
						if (_altDown) {
							_altDown = false;
							OnKeyUp (new Key (map));
						}

					}
					_altDown = false;
					// KeyUp of an Alt-key press. 
					OnKeyUp (keyPressedEventArgs);
				} else {
					OnKeyUp (keyPressedEventArgs);
					if (_altDown) {
						_altDown = false;
					}
				}
			}
			break;

		case (ushort)INPUT_RECORD_EVENT_TYPE.MOUSE_EVENT:
			var me = ToDriverMouse (inputEvent.Event.MouseEvent);
			OnMouseEvent (new MouseEventEventArgs (me));
			if (_processButtonClick) {
				OnMouseEvent (new MouseEventEventArgs (new MouseEvent () {
					X = me.X,
					Y = me.Y,
					Flags = ProcessButtonClick (inputEvent.Event.MouseEvent)
				}));
			}
			break;

		case (ushort)INPUT_RECORD_EVENT_TYPE.FOCUS_EVENT:
			break;

#if !HACK_CHECK_WINCHANGED
		case (ushort)INPUT_RECORD_EVENT_TYPE.WINDOW_BUFFER_SIZE_EVENT:
			
			Cols = inputEvent.Event.WindowBufferSizeEvent.dwSize.X;
			Rows = inputEvent.Event.WindowBufferSizeEvent.dwSize.Y;

			ResizeScreen ();
			ClearContents ();
			OnSizeChanged (new SizeChangedEventArgs (new Size (Cols, Rows)));
			break;
#endif
		}
	}

	MOUSE_BUTTON_STATE? _lastMouseButtonPressed = null;
	bool _isButtonPressed = false;
	bool _isButtonReleased = false;
	bool _isButtonDoubleClicked = false;
	Point? _point;
	Point _pointMove;
	bool _isOneFingerDoubleClicked = false;
	bool _processButtonClick;

	MouseEvent ToDriverMouse (MOUSE_EVENT_RECORD mouseEvent)
	{
		MouseFlags mouseFlag = MouseFlags.AllEvents;

		//System.Diagnostics.Debug.WriteLine (
		//	$"X:{mouseEvent.dwMousePosition.X};Y:{mouseEvent.dwMousePosition.Y};ButtonState:{mouseEvent.dwButtonState};EventFlags:{mouseEvent.dwEventFlags}");

		if (_isButtonDoubleClicked || _isOneFingerDoubleClicked) {
			Application.MainLoop.AddIdle (() => {
				Task.Run (async () => await ProcessButtonDoubleClickedAsync ());
				return false;
			});
		}

		// The ButtonState member of the MouseEvent structure has bit corresponding to each mouse button.
		// This will tell when a mouse button is pressed. When the button is released this event will
		// be fired with it's bit set to 0. So when the button is up ButtonState will be 0.
		// To map to the correct driver events we save the last pressed mouse button so we can
		// map to the correct clicked event.
		if ((_lastMouseButtonPressed != null || _isButtonReleased) && mouseEvent.dwButtonState != 0) {
			_lastMouseButtonPressed = null;
			//isButtonPressed = false;
			_isButtonReleased = false;
		}

		var p = new Point () {
			X = mouseEvent.dwMousePosition.X,
			Y = mouseEvent.dwMousePosition.Y
		};

		if ((mouseEvent.dwButtonState != 0 && mouseEvent.dwEventFlags == 0 && _lastMouseButtonPressed == null && !_isButtonDoubleClicked) ||
		     (_lastMouseButtonPressed == null && ((MOUSE_EVENT_FLAGS)mouseEvent.dwEventFlags).HasFlag (MOUSE_EVENT_FLAGS.MOUSE_MOVED) &&
		     mouseEvent.dwButtonState != 0 && !_isButtonReleased && !_isButtonDoubleClicked)) {
			switch ((MOUSE_BUTTON_STATE)mouseEvent.dwButtonState) {
			case MOUSE_BUTTON_STATE.FROM_LEFT_1ST_BUTTON_PRESSED:
				mouseFlag = MouseFlags.Button1Pressed;
				break;

			case MOUSE_BUTTON_STATE.FROM_LEFT_2ND_BUTTON_PRESSED:
				mouseFlag = MouseFlags.Button2Pressed;
				break;

			case MOUSE_BUTTON_STATE.RIGHTMOST_BUTTON_PRESSED:
				mouseFlag = MouseFlags.Button3Pressed;
				break;
			}

			if (_point == null) {
				_point = p;
			}

			if (((MOUSE_EVENT_FLAGS)mouseEvent.dwEventFlags).HasFlag (MOUSE_EVENT_FLAGS.MOUSE_MOVED)) {
				mouseFlag |= MouseFlags.ReportMousePosition;
				_isButtonReleased = false;
				_processButtonClick = false;
			}
			_lastMouseButtonPressed = (MOUSE_BUTTON_STATE?)mouseEvent.dwButtonState;
			_isButtonPressed = true;

			if ((mouseFlag & MouseFlags.ReportMousePosition) == 0) {
				Application.MainLoop.AddIdle (() => {
					Task.Run (async () => await ProcessContinuousButtonPressedAsync (mouseFlag));
					return false;
				});
			}

		} else if (_lastMouseButtonPressed != null && mouseEvent.dwEventFlags == 0
		      && !_isButtonReleased && !_isButtonDoubleClicked && !_isOneFingerDoubleClicked) {
			switch (_lastMouseButtonPressed) {
			case MOUSE_BUTTON_STATE.FROM_LEFT_1ST_BUTTON_PRESSED:
				mouseFlag = MouseFlags.Button1Released;
				break;

			case MOUSE_BUTTON_STATE.FROM_LEFT_2ND_BUTTON_PRESSED:
				mouseFlag = MouseFlags.Button2Released;
				break;

			case MOUSE_BUTTON_STATE.RIGHTMOST_BUTTON_PRESSED:
				mouseFlag = MouseFlags.Button3Released;
				break;
			}
			_isButtonPressed = false;
			_isButtonReleased = true;
			if (_point != null && (((Point)_point).X == mouseEvent.dwMousePosition.X && ((Point)_point).Y == mouseEvent.dwMousePosition.Y)) {
				_processButtonClick = true;
			} else {
				_point = null;
			}
		} else if (((MOUSE_EVENT_FLAGS)mouseEvent.dwEventFlags).HasFlag (MOUSE_EVENT_FLAGS.MOUSE_MOVED)
			&& !_isOneFingerDoubleClicked && _isButtonReleased && p == _point) {

			mouseFlag = ProcessButtonClick (mouseEvent);

		} else if (((MOUSE_EVENT_FLAGS)mouseEvent.dwEventFlags).HasFlag (MOUSE_EVENT_FLAGS.DOUBLE_CLICK)) {
			switch (mouseEvent.dwButtonState) {
			case (uint)MOUSE_BUTTON_STATE.FROM_LEFT_1ST_BUTTON_PRESSED:
				mouseFlag = MouseFlags.Button1DoubleClicked;
				break;

			case (uint)MOUSE_BUTTON_STATE.FROM_LEFT_2ND_BUTTON_PRESSED:
				mouseFlag = MouseFlags.Button2DoubleClicked;
				break;

			case (uint)MOUSE_BUTTON_STATE.RIGHTMOST_BUTTON_PRESSED:
				mouseFlag = MouseFlags.Button3DoubleClicked;
				break;
			}
			_isButtonDoubleClicked = true;
		} else if (mouseEvent.dwEventFlags == 0 && mouseEvent.dwButtonState != 0 && _isButtonDoubleClicked) {
			switch (mouseEvent.dwButtonState) {
			case (uint)MOUSE_BUTTON_STATE.FROM_LEFT_1ST_BUTTON_PRESSED:
				mouseFlag = MouseFlags.Button1TripleClicked;
				break;

			case (uint)MOUSE_BUTTON_STATE.FROM_LEFT_2ND_BUTTON_PRESSED:
				mouseFlag = MouseFlags.Button2TripleClicked;
				break;

			case (uint)MOUSE_BUTTON_STATE.RIGHTMOST_BUTTON_PRESSED:
				mouseFlag = MouseFlags.Button3TripleClicked;
				break;
			}
			_isButtonDoubleClicked = false;
		} else if (mouseEvent.dwEventFlags == (uint)MOUSE_EVENT_FLAGS.MOUSE_WHEELED) {
			switch ((int)mouseEvent.dwButtonState) {
			case int v when v > 0:
				mouseFlag = MouseFlags.WheeledUp;
				break;

			case int v when v < 0:
				mouseFlag = MouseFlags.WheeledDown;
				break;
			}

		} else if (mouseEvent.dwEventFlags == (uint)MOUSE_EVENT_FLAGS.MOUSE_WHEELED &&
		      mouseEvent.dwControlKeyState == (uint)CONTROL_KEY_STATE.SHIFT_PRESSED) {
			switch ((int)mouseEvent.dwButtonState) {
			case int v when v > 0:
				mouseFlag = MouseFlags.WheeledLeft;
				break;

			case int v when v < 0:
				mouseFlag = MouseFlags.WheeledRight;
				break;
			}

		} else if (mouseEvent.dwEventFlags == (uint)MOUSE_EVENT_FLAGS.MOUSE_HWHEELED) {
			switch ((int)mouseEvent.dwButtonState) {
			case int v when v < 0:
				mouseFlag = MouseFlags.WheeledLeft;
				break;

			case int v when v > 0:
				mouseFlag = MouseFlags.WheeledRight;
				break;
			}

		} else if (mouseEvent.dwEventFlags == (uint)MOUSE_EVENT_FLAGS.MOUSE_MOVED) {
			mouseFlag = MouseFlags.ReportMousePosition;
			if (mouseEvent.dwMousePosition.X != _pointMove.X || mouseEvent.dwMousePosition.Y != _pointMove.Y) {
				_pointMove = new Point (mouseEvent.dwMousePosition.X, mouseEvent.dwMousePosition.Y);
			}
		} else if (mouseEvent.dwButtonState == 0 && mouseEvent.dwEventFlags == 0) {
			mouseFlag = 0;
		}

		mouseFlag = SetControlKeyStates (mouseEvent, mouseFlag);

		//System.Diagnostics.Debug.WriteLine (
		//	$"point.X:{(point != null ? ((Point)point).X : -1)};point.Y:{(point != null ? ((Point)point).Y : -1)}");

		return new MouseEvent () {
			X = mouseEvent.dwMousePosition.X,
			Y = mouseEvent.dwMousePosition.Y,
			Flags = mouseFlag
		};
	}

	MouseFlags ProcessButtonClick (MOUSE_EVENT_RECORD mouseEvent)
	{
		MouseFlags mouseFlag = 0;
		switch (_lastMouseButtonPressed) {
		case MOUSE_BUTTON_STATE.FROM_LEFT_1ST_BUTTON_PRESSED:
			mouseFlag = MouseFlags.Button1Clicked;
			break;

		case MOUSE_BUTTON_STATE.FROM_LEFT_2ND_BUTTON_PRESSED:
			mouseFlag = MouseFlags.Button2Clicked;
			break;

		case MOUSE_BUTTON_STATE.RIGHTMOST_BUTTON_PRESSED:
			mouseFlag = MouseFlags.Button3Clicked;
			break;
		}
		_point = new Point () {
			X = mouseEvent.dwMousePosition.X,
			Y = mouseEvent.dwMousePosition.Y
		};
		_lastMouseButtonPressed = null;
		_isButtonReleased = false;
		_processButtonClick = false;
		_point = null;
		return mouseFlag;
	}

	async Task ProcessButtonDoubleClickedAsync ()
	{
		await Task.Delay (300);
		_isButtonDoubleClicked = false;
		_isOneFingerDoubleClicked = false;
	}

	async Task ProcessContinuousButtonPressedAsync (MouseFlags mouseFlag)
	{
		while (_isButtonPressed) {
			await Task.Delay (100);
			var me = new MouseEvent () {
				X = _pointMove.X,
				Y = _pointMove.Y,
				Flags = mouseFlag
			};

			var view = Application.WantContinuousButtonPressedView;
			if (view == null) {
				break;
			}
			if (_isButtonPressed && (mouseFlag & MouseFlags.ReportMousePosition) == 0) {
				Application.Invoke (() => OnMouseEvent (new MouseEventEventArgs (me)));
			}
		}
	}

	static MouseFlags SetControlKeyStates (MOUSE_EVENT_RECORD mouseEvent, MouseFlags mouseFlag)
	{
		var state = (CONTROL_KEY_STATE)mouseEvent.dwControlKeyState;
		if (state.HasFlag (CONTROL_KEY_STATE.RIGHT_CTRL_PRESSED) || state.HasFlag (CONTROL_KEY_STATE.LEFT_CTRL_PRESSED)) {
			mouseFlag |= MouseFlags.ButtonCtrl;
		}

		if (state.HasFlag (CONTROL_KEY_STATE.SHIFT_PRESSED)) {
			mouseFlag |= MouseFlags.ButtonShift;
		}

		if (state.HasFlag (CONTROL_KEY_STATE.RIGHT_ALT_PRESSED) || state.HasFlag (CONTROL_KEY_STATE.LEFT_ALT_PRESSED)) {
			mouseFlag |= MouseFlags.ButtonAlt;
		}
		return mouseFlag;
	}

	public ConsoleKeyInfoEx ToConsoleKeyInfoEx (KEY_EVENT_RECORD keyEvent)
	{
		var state = (CONTROL_KEY_STATE)keyEvent.dwControlKeyState;

		var shift = (state & CONTROL_KEY_STATE.SHIFT_PRESSED) != 0;
		var alt = (state & (CONTROL_KEY_STATE.LEFT_ALT_PRESSED | CONTROL_KEY_STATE.RIGHT_ALT_PRESSED)) != 0;
		var control = (state & (CONTROL_KEY_STATE.LEFT_CTRL_PRESSED | CONTROL_KEY_STATE.RIGHT_CTRL_PRESSED)) != 0;
		var capsLock = (state & (CONTROL_KEY_STATE.CAPSLOCK_ON)) != 0;
		var numLock = (state & (CONTROL_KEY_STATE.NUMLOCK_ON)) != 0;
		var scrollLock = (state & (CONTROL_KEY_STATE.SCROLLLOCK_ON)) != 0;

		var consoleKeyInfo = new ConsoleKeyInfo (keyEvent.uChar.UnicodeChar, (ConsoleKey)keyEvent.wVirtualKeyCode, shift, alt, control);

		return new ConsoleKeyInfoEx (consoleKeyInfo, capsLock, numLock, scrollLock);
	}

	public KEY_EVENT_RECORD FromVKPacketToKeyEventRecord (KEY_EVENT_RECORD keyEvent)
	{
		if (keyEvent.wVirtualKeyCode != (uint)ConsoleKey.Packet) {
			return keyEvent;
		}

		var state = (CONTROL_KEY_STATE)keyEvent.dwControlKeyState;
		var mod = new ConsoleModifiers ();
		if ((state & CONTROL_KEY_STATE.SHIFT_PRESSED) != 0) {
			mod |= ConsoleModifiers.Shift;
		}
		if ((state & (CONTROL_KEY_STATE.LEFT_ALT_PRESSED | CONTROL_KEY_STATE.RIGHT_ALT_PRESSED)) != 0) {
			mod |= ConsoleModifiers.Alt;
		}
		if ((state & (CONTROL_KEY_STATE.LEFT_CTRL_PRESSED | CONTROL_KEY_STATE.RIGHT_CTRL_PRESSED)) != 0) {
			mod |= ConsoleModifiers.Control;
		}
		var cKeyInfo = ConsoleKeyMapping.GetConsoleKeyFromKey (keyEvent.uChar.UnicodeChar, mod, out uint scanCode);

		return new KEY_EVENT_RECORD {
			uChar = keyEvent.uChar,
			bKeyDown = keyEvent.bKeyDown,
			dwControlKeyState = keyEvent.dwControlKeyState,
			wRepeatCount = keyEvent.wRepeatCount,
			wVirtualKeyCode = (ushort)cKeyInfo.Key,
			wVirtualScanCode = (ushort)scanCode
		};
	}

	KeyCode MapKey (ConsoleKeyInfoEx keyInfoEx)
	{
		var keyInfo = keyInfoEx.ConsoleKeyInfo;
		switch (keyInfo.Key) {
		case ConsoleKey.Escape:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.Esc);
		case ConsoleKey.Tab:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.Tab);
		case ConsoleKey.Clear:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.Clear);
		case ConsoleKey.Home:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.Home);
		case ConsoleKey.End:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.End);
		case ConsoleKey.LeftArrow:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.CursorLeft);
		case ConsoleKey.RightArrow:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.CursorRight);
		case ConsoleKey.UpArrow:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.CursorUp);
		case ConsoleKey.DownArrow:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.CursorDown);
		case ConsoleKey.PageUp:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.PageUp);
		case ConsoleKey.PageDown:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.PageDown);
		case ConsoleKey.Enter:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.Enter);
		case ConsoleKey.Spacebar:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, keyInfo.KeyChar == 0 ? KeyCode.Space : (KeyCode)keyInfo.KeyChar);
		case ConsoleKey.Backspace:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.Backspace);
		case ConsoleKey.Delete:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.DeleteChar);
		case ConsoleKey.Insert:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.InsertChar);
		case ConsoleKey.PrintScreen:
			return ConsoleKeyMapping.MapKeyModifiers (keyInfo, KeyCode.PrintScreen);

		//case ConsoleKey.NumPad0:
		//	return keyInfoEx.NumLock ? Key.D0 : Key.InsertChar;
		//case ConsoleKey.NumPad1:
		//	return keyInfoEx.NumLock ? Key.D1 : Key.End;
		//case ConsoleKey.NumPad2:
		//	return keyInfoEx.NumLock ? Key.D2 : Key.CursorDown;
		//case ConsoleKey.NumPad3:
		//	return keyInfoEx.NumLock ? Key.D3 : Key.PageDown;
		//case ConsoleKey.NumPad4:
		//	return keyInfoEx.NumLock ? Key.D4 : Key.CursorLeft;
		//case ConsoleKey.NumPad5:
		//	return keyInfoEx.NumLock ? Key.D5 : (Key)((uint)keyInfo.KeyChar);
		//case ConsoleKey.NumPad6:
		//	return keyInfoEx.NumLock ? Key.D6 : Key.CursorRight;
		//case ConsoleKey.NumPad7:
		//	return keyInfoEx.NumLock ? Key.D7 : Key.Home;
		//case ConsoleKey.NumPad8:
		//	return keyInfoEx.NumLock ? Key.D8 : Key.CursorUp;
		//case ConsoleKey.NumPad9:
		//	return keyInfoEx.NumLock ? Key.D9 : Key.PageUp;

		case ConsoleKey.Oem1:
		case ConsoleKey.Oem2:
		case ConsoleKey.Oem3:
		case ConsoleKey.Oem4:
		case ConsoleKey.Oem5:
		case ConsoleKey.Oem6:
		case ConsoleKey.Oem7:
		case ConsoleKey.Oem8:
		case ConsoleKey.Oem102:
			var ret = ConsoleKeyMapping.MapKeyModifiers (keyInfo, (KeyCode)((uint)keyInfo.KeyChar));
			if (ret.HasFlag (KeyCode.ShiftMask)) {
				ret &= ~KeyCode.ShiftMask;
			}
			return ret;

		case ConsoleKey.OemPeriod:
		case ConsoleKey.OemComma:
		case ConsoleKey.OemPlus:
		case ConsoleKey.OemMinus:
			return (KeyCode)((uint)keyInfo.KeyChar);
		}

		var key = keyInfo.Key;

		if (key >= ConsoleKey.A && key <= ConsoleKey.Z) {
			var delta = key - ConsoleKey.A;
			if (keyInfo.Modifiers == ConsoleModifiers.Control) {
				return (KeyCode)(((uint)KeyCode.CtrlMask) | ((uint)KeyCode.A + delta));
			}
			if (keyInfo.Modifiers == ConsoleModifiers.Alt) {
				return (KeyCode)(((uint)KeyCode.AltMask) | ((uint)KeyCode.A + delta));
			}
			if (keyInfo.Modifiers == (ConsoleModifiers.Shift | ConsoleModifiers.Alt)) {
				return ConsoleKeyMapping.MapKeyModifiers (keyInfo, (KeyCode)((uint)KeyCode.A + delta));
			}
			if ((keyInfo.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0) {
				if (keyInfo.KeyChar == 0 || (keyInfo.KeyChar != 0 && keyInfo.KeyChar >= 1 && keyInfo.KeyChar <= 26)) {
					return ConsoleKeyMapping.MapKeyModifiers (keyInfo, (KeyCode)((uint)KeyCode.A + delta));
				}
			}

			if (((keyInfo.Modifiers == ConsoleModifiers.Shift) ^ (keyInfoEx.CapsLock))) {
				if (keyInfo.KeyChar <= (uint)KeyCode.Z) {
					return (KeyCode)((uint)KeyCode.A + delta) | KeyCode.ShiftMask;
				}
			}

			if (((KeyCode)((uint)keyInfo.KeyChar) & KeyCode.Space) == 0) {
				return (KeyCode)((uint)keyInfo.KeyChar) & ~KeyCode.Space;
			}

			if (((KeyCode)((uint)keyInfo.KeyChar) & KeyCode.Space) != 0) {
				if (((KeyCode)((uint)keyInfo.KeyChar) & ~KeyCode.Space) == (KeyCode)keyInfo.Key) {
					return (KeyCode)((uint)keyInfo.KeyChar) & ~KeyCode.Space;
				}
				return (KeyCode)((uint)keyInfo.KeyChar);
			}

			return (KeyCode)(uint)keyInfo.KeyChar;

		}

		if (key >= ConsoleKey.D0 && key <= ConsoleKey.D9) {
			var delta = key - ConsoleKey.D0;
			if (keyInfo.Modifiers == ConsoleModifiers.Alt) {
				return (KeyCode)(((uint)KeyCode.AltMask) | ((uint)KeyCode.D0 + delta));
			}
			if (keyInfo.Modifiers == ConsoleModifiers.Control) {
				return (KeyCode)(((uint)KeyCode.CtrlMask) | ((uint)KeyCode.D0 + delta));
			}
			if (keyInfo.Modifiers == (ConsoleModifiers.Shift | ConsoleModifiers.Alt)) {
				return ConsoleKeyMapping.MapKeyModifiers (keyInfo, (KeyCode)((uint)KeyCode.D0 + delta));
			}
			if ((keyInfo.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0) {
				if (keyInfo.KeyChar == 0 || keyInfo.KeyChar == 30 || keyInfo.KeyChar == ((uint)KeyCode.D0 + delta)) {
					return ConsoleKeyMapping.MapKeyModifiers (keyInfo, (KeyCode)((uint)KeyCode.D0 + delta));
				}
			}
			return (KeyCode)((uint)keyInfo.KeyChar);
		}

		if (key >= ConsoleKey.F1 && key <= ConsoleKey.F12) {
			var delta = key - ConsoleKey.F1;
			if ((keyInfo.Modifiers & (ConsoleModifiers.Shift | ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0) {
				return ConsoleKeyMapping.MapKeyModifiers (keyInfo, (KeyCode)((uint)KeyCode.F1 + delta));
			}

			return (KeyCode)((uint)KeyCode.F1 + delta);
		}

		if (key == (ConsoleKey)16) { // Shift
			return KeyCode.Null | KeyCode.ShiftMask;
		}

		if (key == (ConsoleKey)17) { // Ctrl
			return KeyCode.Null | KeyCode.CtrlMask;
		}

		if (key == (ConsoleKey)18) { // Alt
			return KeyCode.Null | KeyCode.AltMask;
		}

		return ConsoleKeyMapping.MapKeyModifiers (keyInfo, (KeyCode)((uint)keyInfo.KeyChar));
	}

	CursorVisibility _cachedCursorVisibility;

	public override void UpdateCursor ()
	{
		if (Col < 0 || Row < 0 || Col > Cols || Row > Rows) {
			GetCursorVisibility (out CursorVisibility cursorVisibility);
			_cachedCursorVisibility = cursorVisibility;
			SetCursorVisibility (CursorVisibility.Invisible);
			return;
		}

		var position = new COORD () {
			X = (short)Col,
			Y = (short)Row
		};
		WinConsole?.SetCursorPosition (position);
		var result = SetCursorVisibility (_cachedCursorVisibility);
		Debug.Assert (result);
	}

	/// <inheritdoc/>
	public override bool GetCursorVisibility (out CursorVisibility visibility)
	{
		if (WinConsole != null) {
			return WinConsole.GetCursorVisibility (out visibility);
		}
		visibility = _cachedCursorVisibility;
		return true;
	}

	/// <inheritdoc/>
	public override bool SetCursorVisibility (CursorVisibility visibility)
	{
		_cachedCursorVisibility = visibility;
		return WinConsole == null || WinConsole.SetCursorVisibility (visibility);
	}

	/// <inheritdoc/>
	public override bool EnsureCursorVisibility ()
	{
		return WinConsole == null || WinConsole.EnsureCursorVisibility ();
	}

	public override void SendKeys (char keyChar, ConsoleKey key, bool shift, bool alt, bool control)
	{
		INPUT_RECORD input = new INPUT_RECORD {
			EventType = (ushort)INPUT_RECORD_EVENT_TYPE.KEY_EVENT
		};

		KEY_EVENT_RECORD keyEvent = new KEY_EVENT_RECORD {
			bKeyDown = true
		};
		CONTROL_KEY_STATE controlKey = new CONTROL_KEY_STATE ();
		if (shift) {
			controlKey |= CONTROL_KEY_STATE.SHIFT_PRESSED;
			keyEvent.uChar.UnicodeChar = '\0';
			keyEvent.wVirtualKeyCode = 16;
		}
		if (alt) {
			controlKey |= CONTROL_KEY_STATE.LEFT_ALT_PRESSED;
			controlKey |= CONTROL_KEY_STATE.RIGHT_ALT_PRESSED;
			keyEvent.uChar.UnicodeChar = '\0';
			keyEvent.wVirtualKeyCode = 18;
		}
		if (control) {
			controlKey |= CONTROL_KEY_STATE.LEFT_CTRL_PRESSED;
			controlKey |= CONTROL_KEY_STATE.RIGHT_CTRL_PRESSED;
			keyEvent.uChar.UnicodeChar = '\0';
			keyEvent.wVirtualKeyCode = 17;
		}
		keyEvent.dwControlKeyState = (uint)controlKey;

		input.Event.KeyEvent = keyEvent;

		if (shift || alt || control) {
			ProcessInput (input);
		}

		keyEvent.uChar.UnicodeChar = keyChar;
		if ((uint)key < 255) {
			keyEvent.wVirtualKeyCode = (ushort)key;
		} else {
			keyEvent.wVirtualKeyCode = '\0';
		}

		input.Event.KeyEvent = keyEvent;

		try {
			ProcessInput (input);
		} catch (OverflowException) { } finally {
			keyEvent.bKeyDown = false;
			input.Event.KeyEvent = keyEvent;
			ProcessInput (input);
		}
	}

	internal override void End ()
	{
		if (_mainLoopDriver != null) {
#if HACK_CHECK_WINCHANGED
			//_mainLoop.WinChanged -= ChangeWin;
#endif
		}
		_mainLoopDriver = null;

		if (!RunningUnitTests && _isWindowsTerminal) {
			// Disable alternative screen buffer.
			WinConsole.Write (EscSeqUtils.CSI_RestoreCursorAndRestoreAltBufferWithBackscroll);
		}
		WinConsole?.Cleanup ();
		WinConsole = null;
	}

	#region Not Implemented
	public override void Suspend ()
	{
		throw new NotImplementedException ();
	}
	#endregion
}

/// <summary>
/// Mainloop intended to be used with the <see cref="ANSIDriver"/>, and can
/// only be used on Windows.
/// </summary>
/// <remarks>
/// This implementation is used for ANSIDriver.
/// </remarks>
internal class AnsiMainLoopDriver : IMainLoopDriver {
	readonly ManualResetEventSlim _eventReady = new ManualResetEventSlim (false);
	readonly ManualResetEventSlim _waitForProbe = new ManualResetEventSlim (false);
	MainLoop _mainLoop;
	readonly ConsoleDriver _consoleDriver;
	readonly WindowsANSIConsole _winConsole;
	CancellationTokenSource _eventReadyTokenSource = new CancellationTokenSource ();
	CancellationTokenSource _inputHandlerTokenSource = new CancellationTokenSource ();

	// The records that we keep fetching
	readonly Queue<INPUT_RECORD []> _resultQueue = new Queue<INPUT_RECORD []> ();

	/// <summary>
	/// Invoked when the window is changed.
	/// </summary>
	public EventHandler<SizeChangedEventArgs> WinChanged;

	public AnsiMainLoopDriver (ConsoleDriver consoleDriver = null)
	{
		_consoleDriver = consoleDriver ?? throw new ArgumentNullException (nameof (consoleDriver));
		_winConsole = ((ANSIDriver)consoleDriver).WinConsole;
	}

	void IMainLoopDriver.Setup (MainLoop mainLoop)
	{
		_mainLoop = mainLoop;
		Task.Run (WindowsInputHandler, _inputHandlerTokenSource.Token);
#if HACK_CHECK_WINCHANGED
		Task.Run (CheckWinChange);
#endif
	}

	void WindowsInputHandler ()
	{
		while (_mainLoop != null) {
			try {
				if (!_inputHandlerTokenSource.IsCancellationRequested) {
					_waitForProbe.Wait (_inputHandlerTokenSource.Token);
				}

			} catch (OperationCanceledException) {
				return;
			} finally {
				_waitForProbe.Reset ();
			}

			if (_resultQueue?.Count == 0) {
				_resultQueue.Enqueue (_winConsole.ReadConsoleInput ());
			}

			_eventReady.Set ();
		}
	}

#if HACK_CHECK_WINCHANGED
	readonly ManualResetEventSlim _winChange = new ManualResetEventSlim (false);
	bool _winChanged;
	Size _windowSize;
	void CheckWinChange ()
	{
		while (_mainLoop != null) {
			_winChange.Wait ();
			_winChange.Reset ();

			// Check if the window size changed every half second. 
			// We do this to minimize the weird tearing seen on Windows when resizing the console
			while (_mainLoop != null) {
				Task.Delay (500).Wait ();
				_windowSize = _winConsole.GetConsoleBufferWindow (out _);
				if (_windowSize != Size.Empty && (_windowSize.Width != _consoleDriver.Cols
								|| _windowSize.Height != _consoleDriver.Rows)) {
					break;
				}
			}

			_winChanged = true;
			_eventReady.Set ();
		}
	}
#endif

	void IMainLoopDriver.Wakeup ()
	{
		_eventReady.Set ();
	}

	bool IMainLoopDriver.EventsPending ()
	{
		_waitForProbe.Set ();
#if HACK_CHECK_WINCHANGED
		_winChange.Set ();
#endif
		if (_mainLoop.CheckTimersAndIdleHandlers (out var waitTimeout)) {
			return true;
		}

		try {
			if (!_eventReadyTokenSource.IsCancellationRequested) {
				// Note: ManualResetEventSlim.Wait will wait indefinitely if the timeout is -1. The timeout is -1 when there
				// are no timers, but there IS an idle handler waiting.
				_eventReady.Wait (waitTimeout, _eventReadyTokenSource.Token);
			}
		} catch (OperationCanceledException) {
			return true;
		} finally {
			_eventReady.Reset ();
		}

		if (!_eventReadyTokenSource.IsCancellationRequested) {
#if HACK_CHECK_WINCHANGED
			return _resultQueue.Count > 0 || _mainLoop.CheckTimersAndIdleHandlers (out _) || _winChanged;
#else
			return _resultQueue.Count > 0 || _mainLoop.CheckTimersAndIdleHandlers (out _);
#endif
		}

		_eventReadyTokenSource.Dispose ();
		_eventReadyTokenSource = new CancellationTokenSource ();
		return true;
	}

	void IMainLoopDriver.Iteration ()
	{
		while (_resultQueue.Count > 0) {
			var inputRecords = _resultQueue.Dequeue ();
			if (inputRecords is { Length: > 0 }) {
				((ANSIDriver)_consoleDriver).ProcessInput (inputRecords [0]);
			}
		}
#if HACK_CHECK_WINCHANGED
		if (_winChanged) {
			_winChanged = false;
			WinChanged?.Invoke (this, new SizeChangedEventArgs (_windowSize));
		}
#endif
	}

	void IMainLoopDriver.TearDown ()
	{
		_inputHandlerTokenSource?.Cancel ();
		_inputHandlerTokenSource?.Dispose ();

		_eventReadyTokenSource?.Cancel ();
		_eventReadyTokenSource?.Dispose ();
		_eventReady?.Dispose ();

		_resultQueue?.Clear ();

#if HACK_CHECK_WINCHANGED
		_winChange?.Dispose ();
#endif
		//_waitForProbe?.Dispose ();

		_mainLoop = null;
	}
}