﻿using Xunit.Abstractions;
using static Terminal.Gui.Pos;

namespace Terminal.Gui.ViewTests;

public class AnchorEndTests (ITestOutputHelper output)
{
    [Fact]
    public void PosAnchorEnd_Constructor ()
    {
        var posAnchorEnd = new PosAnchorEnd (10);
        Assert.NotNull (posAnchorEnd);
    }

    [Theory]
    [InlineData (0, 0, true)]
    [InlineData (10, 10, true)]
    [InlineData (0, 10, false)]
    [InlineData (10, 1, false)]
    public void PosAnchorEnd_Equals (int offset1, int offset2, bool expectedEquals)
    {
        var posAnchorEnd1 = new PosAnchorEnd (offset1);
        var posAnchorEnd2 = new PosAnchorEnd (offset2);

        Assert.Equal (expectedEquals, posAnchorEnd1.Equals (posAnchorEnd2));
        Assert.Equal (expectedEquals, posAnchorEnd2.Equals (posAnchorEnd1));
    }

    [Fact]
    public void PosAnchorEnd_GetHashCode ()
    {
        var posAnchorEnd = new PosAnchorEnd (10);
        var expectedHashCode = 10.GetHashCode ();

        Assert.Equal (expectedHashCode, posAnchorEnd.GetHashCode ());
    }

    [Fact]
    public void PosAnchorEnd_ToString ()
    {
        var posAnchorEnd = new PosAnchorEnd (10);
        var expectedString = "AnchorEnd(10)";

        Assert.Equal (expectedString, posAnchorEnd.ToString ());
    }

    [Fact]
    public void PosAnchorEnd_Anchor ()
    {
        var posAnchorEnd = new PosAnchorEnd (10);
        var width = 50;
        var expectedAnchor = width - 10;

        Assert.Equal (expectedAnchor, posAnchorEnd.Anchor (width));
    }

    [Fact]
    public void AnchorEnd_CreatesCorrectInstance ()
    {
        var pos = Pos.AnchorEnd (10);
        Assert.IsType<PosAnchorEnd> (pos);
    }

    [Fact]
    public void AnchorEnd_Negative_Throws ()
    {
        Pos pos;
        int n = -1;
        Assert.Throws<ArgumentException> (() => pos = Pos.AnchorEnd (n));
    }

    [Theory]
    [InlineData (0)]
    [InlineData (1)]
    public void AnchorEnd_SetsValue_Anchor_Is_Negative (int offset)
    {
        Pos pos = Pos.AnchorEnd (offset);
        Assert.Equal (offset, -pos.Anchor (0));
    }

    [Theory]
    [InlineData (0, 0, 25)]
    [InlineData (0, 10, 25)]
    [InlineData (1, 10, 24)]
    [InlineData (10, 10, 15)]
    [InlineData (20, 10, 5)]
    [InlineData (25, 10, 0)]
    [InlineData (26, 10, -1)]
    public void AnchorEnd_With_Offset_PositionsViewOffsetFromRight (int offset, int width, int expectedXPosition)
    {
        // Arrange
        var superView = new View { Width = 25, Height = 25 };
        var view = new View
        {
            X = Pos.AnchorEnd (offset),
            Width = width,
            Height = 1
        };
        superView.Add (view);
        superView.BeginInit ();
        superView.EndInit ();

        // Act
        superView.LayoutSubviews ();

        // Assert
        Assert.Equal (expectedXPosition, view.Frame.X);
    }

    // UseDimForOffset tests

    [Fact]
    public void AnchorEnd_UseDimForOffset_CreatesCorrectInstance ()
    {
        var pos = Pos.AnchorEnd ();
        Assert.IsType<PosAnchorEnd> (pos);
        Assert.True (((PosAnchorEnd)pos).UseDimForOffset);
    }

    [Fact]
    public void AnchorEnd_UseDimForOffset_SetsValue_Anchor_Is_Negative ()
    {
        Pos pos = Pos.AnchorEnd ();
        Assert.Equal (-10, -pos.Anchor (10));
    }

    [Theory]
    [InlineData (0, 25)]
    [InlineData (10, 15)]
    [InlineData (9, 16)]
    [InlineData (11, 14)]
    [InlineData (25, 0)]
    [InlineData (26, -1)]
    public void AnchorEnd_UseDimForOffset_PositionsViewOffsetByDim (int dim, int expectedXPosition)
    {
        // Arrange
        var superView = new View { Width = 25, Height = 25 };
        var view = new View
        {
            X = Pos.AnchorEnd (),
            Width = dim,
            Height = 1
        };
        superView.Add (view);
        superView.BeginInit ();
        superView.EndInit ();

        // Act
        superView.LayoutSubviews ();

        // Assert
        Assert.Equal (expectedXPosition, view.Frame.X);
    }

    // This test used to be Dialog_In_Window_With_TextField_And_Button_AnchorEnd in DialogTests.
    [Fact]
    [SetupFakeDriver]
    public void AnchorEnd_View_And_Button ()
    {
        ((FakeDriver)Application.Driver).SetBufferSize (20, 5);

        var b = $"{CM.Glyphs.LeftBracket} Ok {CM.Glyphs.RightBracket}";

        var frame = new FrameView { Width = 18, Height = 3 };
        Assert.Equal (16, frame.Viewport.Width);

        Button btn = null;

        int Btn_Width () { return btn?.Viewport.Width ?? 0; }

        btn = new () { Text = "Ok", X = Pos.AnchorEnd (0) - Pos.Function (Btn_Width) };

        var view = new View
        {
            Text = "0123456789abcdefghij",

            // Dim.Fill (1) fills remaining space minus 1 (16 - 1 = 15)
            // Dim.Function (Btn_Width) is 6
            // Width should be 15 - 6 = 9
            Width = Dim.Fill (1) - Dim.Function (Btn_Width),
            Height = 1
        };

        frame.Add (btn, view);
        frame.BeginInit ();
        frame.EndInit ();
        frame.Draw ();

        Assert.Equal (6, btn.Viewport.Width);
        Assert.Equal (10, btn.Frame.X); // frame.Viewport.Width (16) - btn.Frame.Width (6) = 10
        Assert.Equal (0, btn.Frame.Y);
        Assert.Equal (6, btn.Frame.Width);
        Assert.Equal (1, btn.Frame.Height);

        Assert.Equal (9, view.Viewport.Width); // frame.Viewport.Width (16) - Dim.Fill (1) - Dim.Function (6) = 9
        Assert.Equal (0, view.Frame.X);
        Assert.Equal (0, view.Frame.Y);
        Assert.Equal (9, view.Frame.Width);
        Assert.Equal (1, view.Frame.Height);

        var expected = $@"
┌────────────────┐
│012345678 {b}│
└────────────────┘
";
        _ = TestHelpers.AssertDriverContentsWithFrameAre (expected, output);
    }


    // TODO: This actually a SetRelativeLayout/LayoutSubViews test and should be moved
    // TODO: A new test that calls SetRelativeLayout directly is needed.
    [Fact]
    [AutoInitShutdown]
    public void AnchorEnd_Equal_Inside_Window ()
    {
        var viewWidth = 10;
        var viewHeight = 1;

        var tv = new TextView
        {
            X = Pos.AnchorEnd (viewWidth), Y = Pos.AnchorEnd (viewHeight), Width = viewWidth, Height = viewHeight
        };

        var win = new Window ();

        win.Add (tv);

        Toplevel top = new ();
        top.Add (win);
        RunState rs = Application.Begin (top);

        Assert.Equal (new (0, 0, 80, 25), top.Frame);
        Assert.Equal (new (0, 0, 80, 25), win.Frame);
        Assert.Equal (new (68, 22, 10, 1), tv.Frame);
        Application.End (rs);
    }

    // TODO: This actually a SetRelativeLayout/LayoutSubViews test and should be moved
    // TODO: A new test that calls SetRelativeLayout directly is needed.
    [Fact]
    [AutoInitShutdown]
    public void AnchorEnd_Equal_Inside_Window_With_MenuBar_And_StatusBar_On_Toplevel ()
    {
        var viewWidth = 10;
        var viewHeight = 1;

        var tv = new TextView
        {
            X = Pos.AnchorEnd (viewWidth), Y = Pos.AnchorEnd (viewHeight), Width = viewWidth, Height = viewHeight
        };

        var win = new Window ();

        win.Add (tv);

        var menu = new MenuBar ();
        var status = new StatusBar ();
        Toplevel top = new ();
        top.Add (win, menu, status);
        RunState rs = Application.Begin (top);

        Assert.Equal (new (0, 0, 80, 25), top.Frame);
        Assert.Equal (new (0, 0, 80, 1), menu.Frame);
        Assert.Equal (new (0, 24, 80, 1), status.Frame);
        Assert.Equal (new (0, 1, 80, 23), win.Frame);
        Assert.Equal (new (68, 20, 10, 1), tv.Frame);

        Application.End (rs);
    }

}