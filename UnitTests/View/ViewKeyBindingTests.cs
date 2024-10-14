﻿using Xunit.Abstractions;

namespace Terminal.Gui.ViewTests;

public class ViewKeyBindingTests (ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    [AutoInitShutdown]
    public void Focus_KeyBinding ()
    {
        var view = new ScopedKeyBindingView ();
        var invoked = false;
        view.InvokingKeyBindings += (s, e) => invoked = true;

        var top = new Toplevel ();
        top.Add (view);
        Application.Begin (top);

        Application.RaiseKeyDownEvent (Key.A);
        Assert.False (invoked);
        Assert.True (view.ApplicationCommand);

        invoked = false;
        Application.RaiseKeyDownEvent (Key.H);
        Assert.True (invoked);

        invoked = false;
        Assert.False (view.HasFocus);
        Application.RaiseKeyDownEvent (Key.F);
        Assert.False (invoked);
        Assert.False (view.FocusedCommand);

        invoked = false;
        view.CanFocus = true;
        view.SetFocus ();
        Assert.True (view.HasFocus);
        Application.RaiseKeyDownEvent (Key.F);
        Assert.True (invoked);

        Assert.True (view.ApplicationCommand);
        Assert.True (view.HotKeyCommand);
        Assert.True (view.FocusedCommand);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void Focus_KeyBinding_Negative ()
    {
        var view = new ScopedKeyBindingView ();
        var invoked = false;
        view.InvokingKeyBindings += (s, e) => invoked = true;

        var top = new Toplevel ();
        top.Add (view);
        Application.Begin (top);

        Application.RaiseKeyDownEvent (Key.Z);
        Assert.False (invoked);
        Assert.False (view.ApplicationCommand);
        Assert.False (view.HotKeyCommand);
        Assert.False (view.FocusedCommand);

        invoked = false;
        Assert.False (view.HasFocus);
        Application.RaiseKeyDownEvent (Key.F);
        Assert.False (invoked);
        Assert.False (view.ApplicationCommand);
        Assert.False (view.HotKeyCommand);
        Assert.False (view.FocusedCommand);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HotKey_KeyBinding ()
    {
        var view = new ScopedKeyBindingView ();
        var invoked = false;
        view.InvokingKeyBindings += (s, e) => invoked = true;

        var top = new Toplevel ();
        top.Add (view);
        Application.Begin (top);

        invoked = false;
        Application.RaiseKeyDownEvent (Key.H);
        Assert.True (invoked);
        Assert.True (view.HotKeyCommand);

        view.HotKey = KeyCode.Z;
        invoked = false;
        view.HotKeyCommand = false;
        Application.RaiseKeyDownEvent (Key.H); // old hot key
        Assert.False (invoked);
        Assert.False (view.HotKeyCommand);

        Application.RaiseKeyDownEvent (Key.Z); // new hot key
        Assert.True (invoked);
        Assert.True (view.HotKeyCommand);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HotKey_KeyBinding_Negative ()
    {
        var view = new ScopedKeyBindingView ();
        var invoked = false;
        view.InvokingKeyBindings += (s, e) => invoked = true;

        var top = new Toplevel ();
        top.Add (view);
        Application.Begin (top);

        Application.RaiseKeyDownEvent (Key.Z);
        Assert.False (invoked);
        Assert.False (view.HotKeyCommand);

        invoked = false;
        Application.RaiseKeyDownEvent (Key.F);
        Assert.False (view.HotKeyCommand);
        top.Dispose ();
    }

    // tests that test KeyBindingScope.Focus and KeyBindingScope.HotKey (tests for KeyBindingScope.Application are in Application/KeyboardTests.cs)

    public class ScopedKeyBindingView : View
    {
        public ScopedKeyBindingView ()
        {
            AddCommand (Command.Save, () => ApplicationCommand = true);
            AddCommand (Command.HotKey, () => HotKeyCommand = true);
            AddCommand (Command.Left, () => FocusedCommand = true);

            Application.KeyBindings.Add (Key.A, this, Command.Save);
            HotKey = KeyCode.H;
            KeyBindings.Add (Key.F, KeyBindingScope.Focused, Command.Left);
        }

        public bool ApplicationCommand { get; set; }
        public bool FocusedCommand { get; set; }
        public bool HotKeyCommand { get; set; }
    }
}
