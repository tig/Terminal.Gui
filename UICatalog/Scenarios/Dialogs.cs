﻿using System;
using System.Collections.Generic;
using Terminal.Gui;

namespace UICatalog.Scenarios; 

[ScenarioMetadata ("Dialogs", "Demonstrates how to the Dialog class")]
[ScenarioCategory ("Dialogs")]
public class Dialogs : Scenario {
    private static readonly int CODE_POINT = '你'; // We know this is a wide char

    public override void Setup () {
        var frame = new FrameView {
                                      Title = "Dialog Options",
                                      X = Pos.Center (),
                                      Y = 1,
                                      Width = Dim.Percent (75)
                                  };

        var label = new Label {
                                  Text = "Width:",
                                  AutoSize = false,
                                  X = 0,
                                  Y = 0,
                                  Width = 15,
                                  Height = 1,
                                  TextAlignment = TextAlignment.Right
                              };
        frame.Add (label);

        var widthEdit = new TextField {
                                          Text = "0",
                                          X = Pos.Right (label) + 1,
                                          Y = Pos.Top (label),
                                          Width = 5,
                                          Height = 1
                                      };
        frame.Add (widthEdit);

        label = new Label {
                              Text = "Height:",
                              AutoSize = false,
                              X = 0,
                              Y = Pos.Bottom (label),
                              Width = Dim.Width (label),
                              Height = 1,
                              TextAlignment = TextAlignment.Right
                          };
        frame.Add (label);

        var heightEdit = new TextField {
                                           Text = "0",
                                           X = Pos.Right (label) + 1,
                                           Y = Pos.Top (label),
                                           Width = 5,
                                           Height = 1
                                       };
        frame.Add (heightEdit);

        frame.Add (
                   new Label {
                                 Text = "If height & width are both 0,",
                                 X = Pos.Right (widthEdit) + 2,
                                 Y = Pos.Top (widthEdit)
                             });
        frame.Add (
                   new Label {
                                 Text = "the Dialog will size to 80% of container.",
                                 X = Pos.Right (heightEdit) + 2,
                                 Y = Pos.Top (heightEdit)
                             });

        label = new Label {
                              Text = "Title:",
                              AutoSize = false,
                              X = 0,
                              Y = Pos.Bottom (label),
                              Width = Dim.Width (label),
                              Height = 1,
                              TextAlignment = TextAlignment.Right
                          };
        frame.Add (label);

        var titleEdit = new TextField {
                                          Text = "Title",
                                          X = Pos.Right (label) + 1,
                                          Y = Pos.Top (label),
                                          Width = Dim.Fill (),
                                          Height = 1
                                      };
        frame.Add (titleEdit);

        label = new Label {
                              Text = "Num Buttons:",
                              AutoSize = false,
                              X = 0,
                              Y = Pos.Bottom (
                                              label), // BUGBUG: if this is Pos.Bottom (titleEdit) the initial LayoutSubviews does not work correctly?!?!
                              Width = Dim.Width (label),
                              Height = 1,
                              TextAlignment = TextAlignment.Right
                          };
        frame.Add (label);

        var numButtonsEdit = new TextField {
                                               Text = "3",
                                               X = Pos.Right (label) + 1,
                                               Y = Pos.Top (label),
                                               Width = 5,
                                               Height = 1
                                           };
        frame.Add (numButtonsEdit);

        var glyphsNotWords = new CheckBox (
                                           $"Add {char.ConvertFromUtf32 (CODE_POINT)} to button text to stress wide char support") {
                                 X = Pos.Left (numButtonsEdit),
                                 Y = Pos.Bottom (label),
                                 TextAlignment = TextAlignment.Right
                             };
        frame.Add (glyphsNotWords);

        label = new Label {
                              Text = "Button Style:",
                              X = 0,
                              Y = Pos.Bottom (glyphsNotWords),
                              TextAlignment = TextAlignment.Right
                          };
        frame.Add (label);

        var styleRadioGroup = new RadioGroup (new[] { "_Center", "_Justify", "_Left", "_Right" }) {
                                  X = Pos.Right (label) + 1,
                                  Y = Pos.Top (label)
                              };
        frame.Add (styleRadioGroup);

        frame.ValidatePosDim = true;

        void Top_LayoutComplete (object sender, EventArgs args) {
            frame.Height =
                widthEdit.Frame.Height +
                heightEdit.Frame.Height +
                titleEdit.Frame.Height +
                numButtonsEdit.Frame.Height +
                glyphsNotWords.Frame.Height +
                styleRadioGroup.Frame.Height + frame.GetAdornmentsThickness ().Vertical;
        }

        Application.Top.LayoutComplete += Top_LayoutComplete;

        Win.Add (frame);

        label = new Label {
                              Text = "Button Pressed:",
                              X = Pos.Center (),
                              Y = Pos.Bottom (frame) + 4,
                              TextAlignment = TextAlignment.Right
                          };
        Win.Add (label);

        var buttonPressedLabel = new Label {
                                               Text = " ",
                                               AutoSize = false,
                                               X = Pos.Center (),
                                               Y = Pos.Bottom (frame) + 5,
                                               Width = 25,
                                               Height = 1,
                                               ColorScheme = Colors.ColorSchemes["Error"]
                                           };

        // glyphsNotWords
        // false:var btnText = new [] { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" };
        // true: var btnText = new [] { "0", "\u2780", "➁", "\u2783", "\u2784", "\u2785", "\u2786", "\u2787", "\u2788", "\u2789" };
        // \u2781 is ➁ dingbats \ufb70 is	

        var showDialogButton = new Button {
                                              Text = "_Show Dialog",
                                              X = Pos.Center (),
                                              Y = Pos.Bottom (frame) + 2,
                                              IsDefault = true
                                          };
        showDialogButton.Clicked += (s, e) => {
            Dialog dlg = CreateDemoDialog (
                                           widthEdit,
                                           heightEdit,
                                           titleEdit,
                                           numButtonsEdit,
                                           glyphsNotWords,
                                           styleRadioGroup,
                                           buttonPressedLabel);
            Application.Run (dlg);
        };

        Win.Add (showDialogButton);

        Win.Add (buttonPressedLabel);
    }

    private Dialog CreateDemoDialog (
        TextField widthEdit,
        TextField heightEdit,
        TextField titleEdit,
        TextField numButtonsEdit,
        CheckBox glyphsNotWords,
        RadioGroup styleRadioGroup,
        Label buttonPressedLabel
    ) {
        Dialog dialog = null;
        try {
            var width = 0;
            int.TryParse (widthEdit.Text, out width);
            var height = 0;
            int.TryParse (heightEdit.Text, out height);
            var numButtons = 3;
            int.TryParse (numButtonsEdit.Text, out numButtons);

            List<Button> buttons = new List<Button> ();
            int clicked = -1;
            for (var i = 0; i < numButtons; i++) {
                int buttonId = i;
                Button button = null;
                if (glyphsNotWords.Checked == true) {
                    buttonId = i;
                    button = new Button (
                                         NumberToWords.Convert (buttonId) + " "
                                                                          + char.ConvertFromUtf32 (
                                                                           buttonId + CODE_POINT),
                                         buttonId == 0);
                } else {
                    button = new Button (
                                         NumberToWords.Convert (buttonId),
                                         buttonId == 0);
                }

                button.Clicked += (s, e) => {
                    clicked = buttonId;
                    Application.RequestStop ();
                };
                buttons.Add (button);
            }

            //if (buttons.Count > 1) {
            //	buttons [1].Text = "Accept";
            //	buttons [1].IsDefault = true;
            //	buttons [0].Visible = false;
            //	buttons [0].Text = "_Back";
            //	buttons [0].IsDefault = false;
            //}

            // This tests dynamically adding buttons; ensuring the dialog resizes if needed and 
            // the buttons are laid out correctly
            dialog = new Dialog (buttons.ToArray ()) {
                                                         Title = titleEdit.Text,
                                                         ButtonAlignment =
                                                             (Dialog.ButtonAlignments)styleRadioGroup.SelectedItem
                                                     };
            if ((height != 0) || (width != 0)) {
                dialog.Height = height;
                dialog.Width = width;
            }

            var add = new Button {
                                     Text = "Add a button",
                                     X = Pos.Center (),
                                     Y = Pos.Center ()
                                 };
            add.Clicked += (s, e) => {
                int buttonId = buttons.Count;
                Button button;
                if (glyphsNotWords.Checked == true) {
                    button = new Button (
                                         NumberToWords.Convert (buttonId) + " "
                                                                          + char.ConvertFromUtf32 (
                                                                           buttonId + CODE_POINT),
                                         buttonId == 0);
                } else {
                    button = new Button (
                                         NumberToWords.Convert (buttonId),
                                         buttonId == 0);
                }

                button.Clicked += (s, e) => {
                    clicked = buttonId;
                    Application.RequestStop ();
                };
                buttons.Add (button);
                dialog.AddButton (button);
                if (buttons.Count > 1) {
                    button.TabIndex = buttons[buttons.Count - 2].TabIndex + 1;
                }
            };
            dialog.Add (add);

            var addChar = new Button ($"Add a {char.ConvertFromUtf32 (CODE_POINT)} to each button") {
                              X = Pos.Center (),
                              Y = Pos.Center () + 1
                          };
            addChar.Clicked += (s, e) => {
                foreach (Button button in buttons) {
                    button.Text += char.ConvertFromUtf32 (CODE_POINT);
                }

                dialog.LayoutSubviews ();
            };
            dialog.Closed += (s, e) => { buttonPressedLabel.Text = $"{clicked}"; };
            dialog.Add (addChar);
        }
        catch (FormatException) {
            buttonPressedLabel.Text = "Invalid Options";
        }

        return dialog;
    }
}
