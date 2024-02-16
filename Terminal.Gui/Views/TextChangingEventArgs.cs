﻿//
// TextField.cs: single-line text editor with Emacs keybindings
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//

using System.ComponentModel;

namespace Terminal.Gui;

/// <summary>An <see cref="EventArgs"/> which allows passing a cancelable new text value event.</summary>
public class TextChangingEventArgs : CancelEventArgs
{
    /// <summary>Initializes a new instance of <see cref="TextChangingEventArgs"/></summary>
    /// <param name="newText">The new <see cref="TextField.Text"/> to be replaced.</param>
    public TextChangingEventArgs (string newText) { NewText = newText; }

    /// <summary>Flag which allows to cancel the new text value.</summary>
    public bool Cancel { get; set; }

    /// <summary>The new text to be replaced.</summary>
    public string NewText { get; set; }
}
